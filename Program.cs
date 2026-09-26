using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PericonAPI.Classes;
using PericonAPI.Data;
using PericonAPI.Hubs;
using PericonAPI.Models;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Soporte para puerto dinámico asignado por Railway / Render
var envPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(envPort))
{
    builder.WebHost.UseUrls($"http://*:{envPort}");
}

// Configuración de base de datos persistente: PostgreSQL (Railway / Docker) o SQLite local
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=pericon.db";

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

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Prefer;Trust Server Certificate=true;";
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

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 1024 * 1024;
});
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
builder.Services.AddHttpClient();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

try
{
    var hubContext = app.Services.GetRequiredService<IHubContext<MessagingHub>>();
    MessagingHub.SetHubContext(hubContext);
}
catch (Exception ex)
{
    Console.WriteLine($"[Program.cs Startup Warn] No se pudo inicializar static HubContext: {ex.Message}");
}

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
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN AvatarUrl TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN HasClaimedInstagramReward INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN InstagramHandle TEXT NULL;"); } catch { }
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
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS AppErrorLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Source TEXT NOT NULL,
                    RoomName TEXT NULL,
                    Username TEXT NULL,
                    UserId INTEGER NULL,
                    ErrorMessage TEXT NOT NULL,
                    StackTrace TEXT NULL,
                    ExtraData TEXT NULL,
                    Status TEXT NOT NULL DEFAULT 'NUEVO',
                    AdminNotes TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    ResolvedAt TEXT NULL
                );
            ");
        }
        catch { }

        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN PhoneNumber TEXT NULL;"); } catch { }
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS SystemAnnouncements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    Type TEXT NOT NULL DEFAULT 'info',
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL
                );
            ");
        }
        catch { }
    }
    else
    {
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""AppErrorLogs"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Source"" VARCHAR(50) NOT NULL,
                    ""RoomName"" VARCHAR(100) NULL,
                    ""Username"" VARCHAR(100) NULL,
                    ""UserId"" INT NULL,
                    ""ErrorMessage"" TEXT NOT NULL,
                    ""StackTrace"" TEXT NULL,
                    ""ExtraData"" TEXT NULL,
                    ""Status"" VARCHAR(50) NOT NULL DEFAULT 'NUEVO',
                    ""AdminNotes"" TEXT NULL,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""ResolvedAt"" TIMESTAMP WITH TIME ZONE NULL
                );
            ");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""AvatarUrl"" TEXT NULL;");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""PhoneNumber"" TEXT NULL;");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""HasClaimedInstagramReward"" BOOLEAN NOT NULL DEFAULT FALSE;");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""InstagramHandle"" TEXT NULL;");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""SystemAnnouncements"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Title"" VARCHAR(150) NOT NULL,
                    ""Message"" TEXT NOT NULL,
                    ""Type"" VARCHAR(50) NOT NULL DEFAULT 'info',
                    ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" VARCHAR(100) NULL
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

    // Corregir partidas de Salas en MatchBetRecords (por ejemplo #136) para que reflejen 100% de comisión para la casa
    try
    {
        var record136 = db.MatchBetRecords.FirstOrDefault(r => r.Id == 136);
        if (record136 != null && record136.TotalPot == 20 && record136.HouseCommission == 4)
        {
            record136.HouseCommission = 20;
            record136.WinnerPrize = 0;
            if (!record136.EndReason.StartsWith("[SALA"))
            {
                record136.EndReason = $"[SALA 100%] {record136.EndReason}";
            }
            db.SaveChanges();
            Console.WriteLine("[Database] Partida #136 actualizada con comisión de sala del 100% (+20 monedas para la casa).");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Record136 Fix Error] {ex.Message}");
    }

    // Migración única: Reinicio de Economía y Ranking a Dinero Real (500 Monedas, 0 Victorias, Ranking en cero)
    try
    {
        if (db.Database.IsSqlite())
        {
            try { db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS SystemMigrations (MigrationKey TEXT PRIMARY KEY, AppliedAt TEXT NOT NULL);"); } catch { }
        }
        else
        {
            try { db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ""SystemMigrations"" (""MigrationKey"" VARCHAR(100) PRIMARY KEY, ""AppliedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW());"); } catch { }
        }

        bool migrationDone = false;
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = db.Database.IsSqlite()
                ? "SELECT COUNT(*) FROM SystemMigrations WHERE MigrationKey = 'SEASON_RESET_DINERO_REAL_500_COINS_V1';"
                : @"SELECT COUNT(*) FROM ""SystemMigrations"" WHERE ""MigrationKey"" = 'SEASON_RESET_DINERO_REAL_500_COINS_V1';";

            if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            {
                db.Database.OpenConnection();
            }
            var countObj = cmd.ExecuteScalar();
            migrationDone = countObj != null && Convert.ToInt32(countObj) > 0;
        }
        catch
        {
            migrationDone = false;
        }

        if (!migrationDone)
        {
            Console.WriteLine("[Season Reset] Iniciando migración de Dinero Real: fijando 500 monedas y 0 victorias a todos los usuarios...");
            var usersToReset = db.Users.ToList();
            foreach (var u in usersToReset)
            {
                u.Coins = 500;
                u.Wins = 0;
                u.Losses = 0;
                u.Experience = 0;
                u.Level = "Peón de Casona";
            }

            // Desactivar anuncios anteriores y publicar el aviso oficial de lanzamiento
            var oldAnnouncements = db.SystemAnnouncements.Where(a => a.IsActive).ToList();
            foreach (var a in oldAnnouncements)
            {
                a.IsActive = false;
            }

            db.SystemAnnouncements.Add(new SystemAnnouncement
            {
                Title = "🚨 ¡COMIENZA LA ERA DE DINERO REAL! • SALDO INICIAL Y RANKING REINICIADO 🚨",
                Message = "¡Atención a todos los jugadores de El Pericón! A partir de hoy iniciamos oficialmente las partidas con DINERO REAL. Con motivo del lanzamiento, todos los jugadores han recibido 500 MONEDAS DE SALDO INICIAL y el ranking de victorias se ha reiniciado a cero para una competencia 100% limpia y justa. ¡Recarga desde 800 Bs. por Pago Móvil, compite en mesas 1v1 y 2v2 y retira tus ganancias directo a tu cuenta bancaria! Entra a www.pericon.lat",
                Type = "alerta",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Guardian"
            });

            db.SaveChanges();

            // Marcar la migración como aplicada
            if (db.Database.IsSqlite())
            {
                db.Database.ExecuteSqlRaw("INSERT INTO SystemMigrations (MigrationKey, AppliedAt) VALUES ('SEASON_RESET_DINERO_REAL_500_COINS_V1', datetime('now'));");
            }
            else
            {
                db.Database.ExecuteSqlRaw(@"INSERT INTO ""SystemMigrations"" (""MigrationKey"", ""AppliedAt"") VALUES ('SEASON_RESET_DINERO_REAL_500_COINS_V1', NOW());");
            }

            Console.WriteLine($"[Season Reset] Migración completada con éxito para {usersToReset.Count} usuarios.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Season Reset Migration Error] {ex.Message}");
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
