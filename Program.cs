using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PericonAPI.Data;
using PericonAPI.Hubs;
using PericonAPI.Models;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Configuración de base de datos persistente: PostgreSQL (Supabase / Render) o SQLite local
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=aws-0-us-west-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.nmbyauelfhhyvfctxwpr;Password=@Guardian.2026;SSL Mode=Require;Trust Server Certificate=true;";

bool isPostgres = !string.IsNullOrWhiteSpace(connectionString) &&
    (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
     connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
     connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
     connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase));

static string ConvertPostgresUrlToConnectionString(string databaseUrl)
{
    if (string.IsNullOrWhiteSpace(databaseUrl)) return databaseUrl;
    if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        databaseUrl.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        return databaseUrl;
    }

    if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
    }

    return databaseUrl;
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isPostgres)
    {
        var npgsqlConn = ConvertPostgresUrlToConnectionString(connectionString!);
        options.UseNpgsql(npgsqlConn);
    }
    else
    {
        options.UseSqlite(connectionString ?? "Data Source=pericon.db");
    }
});

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    try
    {
        var databaseCreator = (Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator)db.Database.GetService<Microsoft.EntityFrameworkCore.Storage.IDatabaseCreator>();
        databaseCreator.CreateTables();
        Console.WriteLine("[Database] Tablas creadas exitosamente en PostgreSQL / Supabase.");
    }
    catch
    {
        // Las tablas ya existen o EnsureCreated es suficiente
    }

    try { db.Database.EnsureCreated(); } catch { }

    if (db.Database.IsSqlite())
    {
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Wins INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Losses INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LastDailyClaim TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("UPDATE Users SET Coins = 1000 WHERE Coins < 1000;"); } catch { }
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PaymentRecharges (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    AmountBs DECIMAL(18,2) NOT NULL,
                    CoinsAmount INTEGER NOT NULL,
                    Reference TEXT NOT NULL,
                    ReceiptImageUrl TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'PENDIENTE',
                    AdminNotes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    ProcessedAt TEXT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );
            ");
        }
        catch { }
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS PaymentWithdrawals (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    CoinsAmount INTEGER NOT NULL,
                    AmountBs DECIMAL(18,2) NOT NULL,
                    BankName TEXT NOT NULL,
                    PhoneNumber TEXT NOT NULL,
                    IdCard TEXT NOT NULL,
                    Status TEXT NOT NULL DEFAULT 'PENDIENTE',
                    AdminReference TEXT NULL,
                    AdminNotes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    ProcessedAt TEXT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                );
            ");
        }
        catch { }
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS MatchBetRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GameId INTEGER NOT NULL,
                    PlayerOneName TEXT NOT NULL,
                    PlayerTwoName TEXT NOT NULL,
                    BetPerPlayer INTEGER NOT NULL,
                    TotalPot INTEGER NOT NULL,
                    HouseCommission INTEGER NOT NULL,
                    WinnerPrize INTEGER NOT NULL,
                    WinnerUsername TEXT NOT NULL,
                    LoserUsername TEXT NOT NULL,
                    EndReason TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
            ");
        }
        catch { }
    }

    // Auto-seed inicial si la base de datos está vacía (por ejemplo al conectar PostgreSQL en Render)
    if (!db.Users.Any())
    {
        try
        {
            var sqlitePath = Path.Combine(builder.Environment.ContentRootPath, "pericon.db");
            if (File.Exists(sqlitePath))
            {
                var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlite($"Data Source={sqlitePath}")
                    .Options;
                using var sqliteDb = new AppDbContext(sqliteOptions);
                if (sqliteDb.Users.Any())
                {
                    var existingUsers = sqliteDb.Users.AsNoTracking().ToList();
                    foreach (var u in existingUsers)
                    {
                        db.Users.Add(new User
                        {
                            Username = u.Username,
                            Email = u.Email,
                            PasswordHash = u.PasswordHash,
                            Coins = u.Coins,
                            Wins = u.Wins,
                            Losses = u.Losses,
                            Level = u.Level,
                            Experience = u.Experience,
                            CreatedAt = u.CreatedAt,
                            GoogleId = u.GoogleId,
                            AvatarUrl = u.AvatarUrl,
                            IsActive = u.IsActive,
                            IsAdmin = u.IsAdmin,
                            LastDailyClaim = u.LastDailyClaim
                        });
                    }
                    db.SaveChanges();
                    Console.WriteLine($"[AutoSeed] Se migraron exitosamente {existingUsers.Count} usuarios desde pericon.db a la base de datos.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoSeed Error] {ex.Message}");
        }
    }

    // Garantizar existencia y credenciales del usuario Administrador 'Guardian'
    try
    {
        var guardian = db.Users.FirstOrDefault(u => u.Username.ToLower() == "guardian");
        if (guardian == null)
        {
            guardian = new User
            {
                Username = "Guardian",
                Email = "guardian@elpericon.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Guardian.2026"),
                Coins = 100000,
                Wins = 50,
                Losses = 0,
                Level = "Experto",
                Experience = 5000,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsAdmin = true
            };
            db.Users.Add(guardian);
            db.SaveChanges();
            Console.WriteLine("[Guardian] Usuario administrador 'Guardian' creado exitosamente.");
        }
        else
        {
            guardian.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Guardian.2026");
            guardian.IsAdmin = true;
            guardian.IsActive = true;
            db.SaveChanges();
            Console.WriteLine("[Guardian] Usuario administrador 'Guardian' actualizado correctamente.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Guardian Setup Error] {ex.Message}");
    }

    // Garantizar código promocional inicial de bienvenida
    try
    {
        if (!db.PromoCodes.Any(p => p.Code == "PERICON2026"))
        {
            db.PromoCodes.Add(new PromoCode
            {
                Code = "PERICON2026",
                CoinsReward = 200,
                MaxUses = 5000,
                TimesUsed = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            Console.WriteLine("[PromoCode] Código promocional 'PERICON2026' activado.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[PromoCode Error] {ex.Message}");
    }
}

var uploadsDir = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads", "receipts");
if (!Directory.Exists(uploadsDir))
{
    Directory.CreateDirectory(uploadsDir);
}

app.UseCors("CorsPolicy");
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<MessagingHub>("/hub");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
