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
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);
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
builder.Services.AddSingleton<ITesoroPagosService, TesoroPagosService>();

var app = builder.Build();

try
{
    var hubContext = app.Services.GetRequiredService<IHubContext<MessagingHub>>();
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    MessagingHub.InitializeScavenger(hubContext, scopeFactory);
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
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN BonusCoins INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsVirtualBot INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN BotDifficulty TEXT NOT NULL DEFAULT 'facil';"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN DailyWins INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LastDailyWinDate TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LastReactivatedAt TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN ReactivationCount INTEGER NOT NULL DEFAULT 0;"); } catch { }
        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS DailyLeaderboardRewards (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Username TEXT NOT NULL,
                    Rank INTEGER NOT NULL,
                    CoinsAwarded INTEGER NOT NULL,
                    WinsCount INTEGER NOT NULL,
                    RewardDate TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
            ");
        } catch { }
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
                    IsBotMatch INTEGER NOT NULL DEFAULT 0,
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
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Cedula TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN BankName TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN DeviceFingerprint TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN BirthDate TEXT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_Cedula ON Users(Cedula) WHERE Cedula IS NOT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_Users_PhoneNumber ON Users(PhoneNumber) WHERE PhoneNumber IS NOT NULL;"); } catch { }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE PaymentRecharges ADD COLUMN ReceiptBase64 TEXT NULL;"); } catch { }
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

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS BotMatchRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Username TEXT NOT NULL,
                    BotName TEXT NOT NULL,
                    BetAmount INTEGER NOT NULL,
                    UserWon INTEGER NOT NULL,
                    CoinsWon INTEGER NOT NULL,
                    CoinsLost INTEGER NOT NULL,
                    HouseCommission INTEGER NOT NULL DEFAULT 0,
                    HouseProfit INTEGER NOT NULL,
                    UserCoinsBefore INTEGER NOT NULL,
                    UserCoinsAfter INTEGER NOT NULL,
                    EndReason TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
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
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Cedula"" VARCHAR(20) NULL;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""BankName"" VARCHAR(100) NULL;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DeviceFingerprint"" VARCHAR(128) NULL;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""BirthDate"" TIMESTAMP WITH TIME ZONE NULL;");
            db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Cedula"" ON ""Users""(""Cedula"") WHERE ""Cedula"" IS NOT NULL;");
            db.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_PhoneNumber"" ON ""Users""(""PhoneNumber"") WHERE ""PhoneNumber"" IS NOT NULL;");
        }
        catch { }

        try
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""PaymentRecharges"" ADD COLUMN IF NOT EXISTS ""ReceiptBase64"" TEXT NULL;");
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
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""BonusCoins"" INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""IsVirtualBot"" BOOLEAN NOT NULL DEFAULT FALSE;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""BotDifficulty"" VARCHAR(20) NOT NULL DEFAULT 'facil';");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DailyWins"" INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastDailyWinDate"" TIMESTAMP WITH TIME ZONE NULL;");
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""DailyLeaderboardRewards"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" INT NOT NULL,
                    ""Username"" VARCHAR(50) NOT NULL,
                    ""Rank"" INT NOT NULL,
                    ""CoinsAwarded"" INT NOT NULL,
                    ""WinsCount"" INT NOT NULL,
                    ""RewardDate"" VARCHAR(20) NOT NULL,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );
            ");
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

        try
        {
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""BotMatchRecords"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" INT NOT NULL,
                    ""Username"" VARCHAR(150) NOT NULL,
                    ""BotName"" VARCHAR(150) NOT NULL,
                    ""BetAmount"" INT NOT NULL,
                    ""UserWon"" BOOLEAN NOT NULL,
                    ""CoinsWon"" INT NOT NULL,
                    ""CoinsLost"" INT NOT NULL,
                    ""HouseCommission"" INT NOT NULL DEFAULT 0,
                    ""HouseProfit"" INT NOT NULL,
                    ""UserCoinsBefore"" INT NOT NULL,
                    ""UserCoinsAfter"" INT NOT NULL,
                    ""EndReason"" TEXT NOT NULL,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                );
            ");
        }
        catch { }

        try
        {
            if (db.Database.IsSqlite())
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS PlayerFeedbacks (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        UserId INTEGER NULL,
                        Username TEXT NOT NULL,
                        UserEmail TEXT NULL,
                        UserPhone TEXT NULL,
                        Rating INTEGER NOT NULL DEFAULT 5,
                        Category TEXT NOT NULL DEFAULT 'General',
                        Message TEXT NOT NULL,
                        CanPublish BOOLEAN NOT NULL DEFAULT 1,
                        IsFeatured BOOLEAN NOT NULL DEFAULT 0,
                        CreatedAt TEXT NOT NULL
                    );
                ");
            }
            else
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""PlayerFeedbacks"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""UserId"" INT NULL,
                        ""Username"" VARCHAR(100) NOT NULL,
                        ""UserEmail"" VARCHAR(150) NULL,
                        ""UserPhone"" VARCHAR(30) NULL,
                        ""Rating"" INT NOT NULL DEFAULT 5,
                        ""Category"" VARCHAR(50) NOT NULL DEFAULT 'General',
                        ""Message"" TEXT NOT NULL,
                        ""CanPublish"" BOOLEAN NOT NULL DEFAULT TRUE,
                        ""IsFeatured"" BOOLEAN NOT NULL DEFAULT FALSE,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                ");
            }
        }
        catch { }

        try
        {
            if (db.Database.IsSqlite())
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS SystemSettings (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    );
                ");
            }
            else
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""SystemSettings"" (
                        ""Key"" VARCHAR(100) PRIMARY KEY,
                        ""Value"" TEXT NOT NULL,
                        ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                    );
                ");
            }

            var botDiffSetting = db.SystemSettings.FirstOrDefault(s => s.Key == "BotDifficultyMode");
            if (botDiffSetting != null && !string.IsNullOrWhiteSpace(botDiffSetting.Value))
            {
                GamePlayOneVsOne.SetBotDifficulty(botDiffSetting.Value);
                Console.WriteLine($"[Startup] Dificultad del bot restaurada desde BD: Modo {GamePlayOneVsOne.BotDifficultyMode}");
            }
        }
        catch { }

        try
        {
            // Limpieza de partidas de prueba históricas para el reinicio oficial del panel a cero
            db.Database.ExecuteSqlRaw(@"DELETE FROM ""MatchBetRecords"" WHERE ""CreatedAt"" < '2026-09-26 12:00:00+00';");
        }
        catch { }

        // Tabla de Códigos de Verificación de Correo (OTP) y ajuste de dificultad general
        try
        {
            if (db.Database.IsSqlite())
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS EmailVerificationCodes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Email TEXT NOT NULL,
                        Code TEXT NOT NULL,
                        CreatedAt TEXT NOT NULL,
                        ExpiresAt TEXT NOT NULL,
                        IsUsed INTEGER NOT NULL DEFAULT 0
                    );
                ");
                try { db.Database.ExecuteSqlRaw("UPDATE Users SET BotDifficulty = NULL WHERE BotDifficulty = 'facil' AND Username NOT IN ('Guardian');"); } catch { }
            }
            else
            {
                db.Database.ExecuteSqlRaw(@"
                    CREATE TABLE IF NOT EXISTS ""EmailVerificationCodes"" (
                        ""Id"" SERIAL PRIMARY KEY,
                        ""Email"" VARCHAR(150) NOT NULL,
                        ""Code"" VARCHAR(10) NOT NULL,
                        ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                        ""ExpiresAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                        ""IsUsed"" BOOLEAN NOT NULL DEFAULT FALSE
                    );
                ");
                try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ALTER COLUMN ""BotDifficulty"" DROP NOT NULL;"); } catch { }
                try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ALTER COLUMN ""BotDifficulty"" DROP DEFAULT;"); } catch { }
                try { db.Database.ExecuteSqlRaw(@"UPDATE ""Users"" SET ""BotDifficulty"" = NULL WHERE ""BotDifficulty"" = 'facil' AND ""Username"" NOT IN ('Guardian');"); } catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup] Error inicializando EmailVerificationCodes: {ex.Message}");
        }
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

    // Migración automática: Columna HouseCommission en BotMatchRecords (10% de comisión por jugar contra el bot)
    try
    {
        if (db.Database.IsSqlite())
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE BotMatchRecords ADD COLUMN HouseCommission INTEGER NOT NULL DEFAULT 0;");
        }
        else
        {
            db.Database.ExecuteSqlRaw(@"ALTER TABLE ""BotMatchRecords"" ADD COLUMN IF NOT EXISTS ""HouseCommission"" INT NOT NULL DEFAULT 0;");
        }
    }
    catch { }

    // Migración automática: Columna IsBotMatch en MatchBetRecords para diferenciar humanos de bots
    try
    {
        if (db.Database.IsSqlite())
        {
            try { db.Database.ExecuteSqlRaw("ALTER TABLE MatchBetRecords ADD COLUMN IsBotMatch INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    UPDATE MatchBetRecords 
                    SET IsBotMatch = 1 
                    WHERE PlayerTwoName IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR PlayerOneName IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR WinnerUsername IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR LoserUsername IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)');
                ");
            }
            catch { }
        }
        else
        {
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""MatchBetRecords"" ADD COLUMN IF NOT EXISTS ""IsBotMatch"" BOOLEAN NOT NULL DEFAULT FALSE;"); } catch { }
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    UPDATE ""MatchBetRecords"" 
                    SET ""IsBotMatch"" = TRUE 
                    WHERE ""PlayerTwoName"" IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR ""PlayerOneName"" IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR ""WinnerUsername"" IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)')
                       OR ""LoserUsername"" IN ('Cheo', 'Tibisay', 'Coromoto', 'El Catire', 'Rosendo', 'Oswaldo', 'Pericón (Bot IA)');
                ");
            }
            catch { }
        }
    }
    catch { }

    // Migración automática: Tablas de Torneo, Referidos y Columnas de Racha en Users
    try
    {
        if (db.Database.IsSqlite())
        {
            try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN DailyStreak INTEGER NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN ReferralCode TEXT NULL;"); } catch { }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN ReferredByUserId INTEGER NULL;"); } catch { }

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Tournaments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IsPrivate INTEGER NOT NULL DEFAULT 1,
                    Password TEXT NOT NULL DEFAULT 'PERICON2026',
                    BuyInCoins INTEGER NOT NULL DEFAULT 50,
                    MaxParticipants INTEGER NOT NULL DEFAULT 4,
                    CurrentParticipants INTEGER NOT NULL DEFAULT 0,
                    Status TEXT NOT NULL DEFAULT 'Inscripciones',
                    TotalPot INTEGER NOT NULL DEFAULT 0,
                    HouseCommission INTEGER NOT NULL DEFAULT 0,
                    WinnerUserId INTEGER NULL,
                    WinnerUsername TEXT NULL,
                    RunnerUpUserId INTEGER NULL,
                    RunnerUpUsername TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    StartedAt TEXT NULL,
                    FinishedAt TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS TournamentParticipants (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TournamentId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Username TEXT NOT NULL,
                    AvatarUrl TEXT NULL,
                    SlotIndex INTEGER NOT NULL DEFAULT 0,
                    IsEliminated INTEGER NOT NULL DEFAULT 0,
                    EliminatedInRound TEXT NULL,
                    JoinedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS TournamentMatches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TournamentId INTEGER NOT NULL,
                    Round TEXT NOT NULL,
                    MatchIndex INTEGER NOT NULL DEFAULT 0,
                    PlayerOneId INTEGER NULL,
                    PlayerOneUsername TEXT NULL,
                    PlayerOneScore INTEGER NOT NULL DEFAULT 0,
                    PlayerTwoId INTEGER NULL,
                    PlayerTwoUsername TEXT NULL,
                    PlayerTwoScore INTEGER NOT NULL DEFAULT 0,
                    WinnerId INTEGER NULL,
                    WinnerUsername TEXT NULL,
                    Status TEXT NOT NULL DEFAULT 'Pendiente',
                    GameId INTEGER NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS ReferralRecords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ReferrerUserId INTEGER NOT NULL,
                    ReferredUserId INTEGER NOT NULL,
                    ReferralCode TEXT NOT NULL,
                    ReferredUsername TEXT NOT NULL,
                    CoinsAwardedReferred INTEGER NOT NULL DEFAULT 100,
                    CoinsAwardedReferrer INTEGER NOT NULL DEFAULT 150,
                    Status TEXT NOT NULL DEFAULT 'Pendiente',
                    CreatedAt TEXT NOT NULL,
                    QualifiedAt TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS PlatformExpenses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Description TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    Currency TEXT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    UsdtReceived DECIMAL(18,2) NULL,
                    ExchangeRate DECIMAL(18,4) NULL,
                    CreatedAt TEXT NOT NULL,
                    CreatedBy TEXT NULL,
                    Notes TEXT NULL
                );
            ");
        }
        else
        {
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""DailyStreak"" INT NOT NULL DEFAULT 0;"); } catch { }
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ReferralCode"" VARCHAR(20) NULL;"); } catch { }
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ReferredByUserId"" INT NULL;"); } catch { }
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastReactivatedAt"" TIMESTAMP WITH TIME ZONE NULL;"); } catch { }
            try { db.Database.ExecuteSqlRaw(@"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""ReactivationCount"" INT NOT NULL DEFAULT 0;"); } catch { }

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS ""Tournaments"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Name"" VARCHAR(100) NOT NULL,
                    ""IsPrivate"" BOOLEAN NOT NULL DEFAULT TRUE,
                    ""Password"" VARCHAR(50) NOT NULL DEFAULT 'PERICON2026',
                    ""BuyInCoins"" INT NOT NULL DEFAULT 50,
                    ""MaxParticipants"" INT NOT NULL DEFAULT 4,
                    ""CurrentParticipants"" INT NOT NULL DEFAULT 0,
                    ""Status"" VARCHAR(30) NOT NULL DEFAULT 'Inscripciones',
                    ""TotalPot"" INT NOT NULL DEFAULT 0,
                    ""HouseCommission"" INT NOT NULL DEFAULT 0,
                    ""WinnerUserId"" INT NULL,
                    ""WinnerUsername"" VARCHAR(50) NULL,
                    ""RunnerUpUserId"" INT NULL,
                    ""RunnerUpUsername"" VARCHAR(50) NULL,
                    ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    ""StartedAt"" TIMESTAMP NULL,
                    ""FinishedAt"" TIMESTAMP NULL
                );
                CREATE TABLE IF NOT EXISTS ""TournamentParticipants"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""TournamentId"" INT NOT NULL,
                    ""UserId"" INT NOT NULL,
                    ""Username"" VARCHAR(50) NOT NULL,
                    ""AvatarUrl"" VARCHAR(255) NULL,
                    ""SlotIndex"" INT NOT NULL DEFAULT 0,
                    ""IsEliminated"" BOOLEAN NOT NULL DEFAULT FALSE,
                    ""EliminatedInRound"" VARCHAR(30) NULL,
                    ""JoinedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                );
                CREATE TABLE IF NOT EXISTS ""TournamentMatches"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""TournamentId"" INT NOT NULL,
                    ""Round"" VARCHAR(30) NOT NULL,
                    ""MatchIndex"" INT NOT NULL DEFAULT 0,
                    ""PlayerOneId"" INT NULL,
                    ""PlayerOneUsername"" VARCHAR(50) NULL,
                    ""PlayerOneScore"" INT NOT NULL DEFAULT 0,
                    ""PlayerTwoId"" INT NULL,
                    ""PlayerTwoUsername"" VARCHAR(50) NULL,
                    ""PlayerTwoScore"" INT NOT NULL DEFAULT 0,
                    ""WinnerId"" INT NULL,
                    ""WinnerUsername"" VARCHAR(50) NULL,
                    ""Status"" VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
                    ""GameId"" INT NULL,
                    ""UpdatedAt"" TIMESTAMP NOT NULL DEFAULT NOW()
                );
                CREATE TABLE IF NOT EXISTS ""ReferralRecords"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""ReferrerUserId"" INT NOT NULL,
                    ""ReferredUserId"" INT NOT NULL,
                    ""ReferralCode"" VARCHAR(30) NOT NULL,
                    ""ReferredUsername"" VARCHAR(50) NOT NULL,
                    ""CoinsAwardedReferred"" INT NOT NULL DEFAULT 100,
                    ""CoinsAwardedReferrer"" INT NOT NULL DEFAULT 150,
                    ""Status"" VARCHAR(30) NOT NULL DEFAULT 'Pendiente',
                    ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    ""QualifiedAt"" TIMESTAMP NULL
                );
                CREATE TABLE IF NOT EXISTS ""PlatformExpenses"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Description"" VARCHAR(255) NOT NULL,
                    ""Category"" VARCHAR(80) NOT NULL,
                    ""Currency"" VARCHAR(10) NOT NULL,
                    ""Amount"" DECIMAL(18,2) NOT NULL,
                    ""UsdtReceived"" DECIMAL(18,2) NULL,
                    ""ExchangeRate"" DECIMAL(18,4) NULL,
                    ""CreatedAt"" TIMESTAMP NOT NULL DEFAULT NOW(),
                    ""CreatedBy"" VARCHAR(100) NULL,
                    ""Notes"" VARCHAR(500) NULL
                );
            ");
        }

        // Semillero del Torneo Piloto si no existe ninguno
        if (!db.Tournaments.Any())
        {
            db.Tournaments.Add(new Tournament
            {
                Name = "Torneo Piloto Casona (Privado)",
                IsPrivate = true,
                Password = "PERICON2026",
                BuyInCoins = 50,
                MaxParticipants = 4,
                CurrentParticipants = 0,
                Status = "Inscripciones",
                TotalPot = 200,
                HouseCommission = 20,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            Console.WriteLine("[Torneos] Torneo Piloto Casona (4 jugadores) sembrado exitosamente con contraseña PERICON2026.");
        }
        else
        {
            var openTournament = db.Tournaments.OrderByDescending(t => t.Id).FirstOrDefault(t => t.Status == "Inscripciones" && t.MaxParticipants != 4);
            if (openTournament != null)
            {
                var participantCount = db.TournamentParticipants.Count(p => p.TournamentId == openTournament.Id);
                if (participantCount == 0)
                {
                    openTournament.MaxParticipants = 4;
                    openTournament.TotalPot = 4 * openTournament.BuyInCoins;
                    openTournament.HouseCommission = (int)Math.Round(openTournament.TotalPot * 0.10);
                    db.SaveChanges();
                    Console.WriteLine("[Torneos] Torneo activo en inscripciones ajustado a 4 jugadores.");
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Tournaments & Referrals Migration Error] {ex.Message}");
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

    // Garantizar existencia y renovación de los 6 Bots Virtuales 1 vs 1 (Cheo, Tibisay, Coromoto, El Catire, Rosendo, Oswaldo)
    try
    {
        var oldToNewMap = new Dictionary<string, (string NewName, string NewEmail, string Level, int Wins, int Losses, int Coins)>
        {
            ["joel"] = ("Cheo", "cheo.bot@pericon.lat", "Gallo de Espuela", 28, 14, 1650),
            ["maría"] = ("Tibisay", "tibisay.bot@pericon.lat", "Doña de la Baraja", 22, 11, 1340),
            ["maria"] = ("Tibisay", "tibisay.bot@pericon.lat", "Doña de la Baraja", 22, 11, 1340),
            ["gloria"] = ("Coromoto", "coromoto.bot@pericon.lat", "Patrona del Perico", 26, 13, 1550),
            ["la gorda"] = ("El Catire", "elcatire.bot@pericon.lat", "Bravo de la Mesa", 35, 19, 2100),
            ["pedro"] = ("Rosendo", "rosendo.bot@pericon.lat", "Taimador del Llano", 30, 17, 1820),
            ["ramón"] = ("Oswaldo", "oswaldo.bot@pericon.lat", "Veterano de Apure", 33, 18, 1950),
            ["ramon"] = ("Oswaldo", "oswaldo.bot@pericon.lat", "Veterano de Apure", 33, 18, 1950)
        };

        // Migrar bots viejos existentes por Username o Email antiguo para que no queden duplicados
        var existingOldBots = db.Users.Where(u => u.IsVirtualBot || u.Email.Contains(".bot@pericon.lat")).ToList();
        foreach (var b in existingOldBots)
        {
            var uLower = (b.Username ?? "").ToLower().Trim();
            var eLower = (b.Email ?? "").ToLower().Trim();

            string? matchKey = null;
            if (oldToNewMap.ContainsKey(uLower)) matchKey = uLower;
            else if (eLower.StartsWith("joel.")) matchKey = "joel";
            else if (eLower.StartsWith("maria.")) matchKey = "maría";
            else if (eLower.StartsWith("gloria.")) matchKey = "gloria";
            else if (eLower.StartsWith("lagorda.")) matchKey = "la gorda";
            else if (eLower.StartsWith("pedro.")) matchKey = "pedro";
            else if (eLower.StartsWith("ramon.")) matchKey = "ramón";

            if (matchKey != null && oldToNewMap.TryGetValue(matchKey, out var mapping))
            {
                b.Username = mapping.NewName;
                b.Email = mapping.NewEmail;
                b.Level = mapping.Level;
                b.IsVirtualBot = true;
                b.IsActive = true;
            }
        }
        db.SaveChanges();

        var virtualBots = new (string Username, string Email, string Level, int Wins, int Losses, int Coins, string Avatar, string Diff)[]
        {
            ("Cheo", "cheo.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil"),
            ("Tibisay", "tibisay.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil"),
            ("Coromoto", "coromoto.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil"),
            ("El Catire", "elcatire.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil"),
            ("Rosendo", "rosendo.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil"),
            ("Oswaldo", "oswaldo.bot@pericon.lat", "Peón de Casona", 0, 0, 1000, "/avatar.png", "facil")
        };

        bool anyBotAdded = false;
        var totalExistingVirtualBots = db.Users.Count(u => u.IsVirtualBot);
        if (totalExistingVirtualBots == 0)
        {
            foreach (var (bName, bEmail, bLevel, bWins, bLosses, bCoins, bAvatar, bDiff) in virtualBots)
            {
                var botUser = db.Users.FirstOrDefault(u => u.Username.ToLower() == bName.ToLower() || u.Email.ToLower() == bEmail.ToLower());
                if (botUser == null)
                {
                    db.Users.Add(new User
                    {
                        Username = bName,
                        Email = bEmail,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword($"BotPass.{bName}.2026"),
                        Coins = bCoins,
                        Wins = bWins,
                        Losses = bLosses,
                        Level = bLevel,
                        AvatarUrl = bAvatar,
                        IsActive = true,
                        IsAdmin = false,
                        IsVirtualBot = true,
                        BotDifficulty = bDiff,
                        CreatedAt = DateTime.UtcNow.AddDays(-15)
                    });
                    anyBotAdded = true;
                }
            }
        }

        // Restauración y resguardo del historial de partidas vs bots desde MatchBetRecords si BotMatchRecords está vacío
        try
        {
            if (!db.BotMatchRecords.Any())
            {
                var botBetMatches = db.MatchBetRecords.Where(m => m.IsBotMatch).OrderBy(m => m.CreatedAt).ToList();
                if (botBetMatches.Count > 0)
                {
                    var dbBots = db.Users.Where(u => u.IsVirtualBot).ToList();
                    var botNames = new HashSet<string>(dbBots.Select(u => u.Username.ToLower()), StringComparer.OrdinalIgnoreCase);

                    foreach (var m in botBetMatches)
                    {
                        bool p2IsBot = botNames.Contains(m.PlayerTwoName.ToLower()) || 
                                       m.PlayerTwoName.ToLower().Contains("bot") || 
                                       !botNames.Contains(m.PlayerOneName.ToLower());
                        
                        string botName = p2IsBot ? m.PlayerTwoName : m.PlayerOneName;
                        string humanName = p2IsBot ? m.PlayerOneName : m.PlayerTwoName;
                        bool humanWon = m.WinnerUsername.Equals(humanName, StringComparison.OrdinalIgnoreCase);

                        int bet = m.BetPerPlayer > 0 ? m.BetPerPlayer : 200;
                        int houseCommission = m.HouseCommission > 0 ? m.HouseCommission : (int)Math.Round(bet * 0.1);
                        int winnerPrize = m.WinnerPrize > 0 ? m.WinnerPrize : (bet * 2 - houseCommission);

                        var humanUser = db.Users.FirstOrDefault(u => u.Username == humanName);

                        var botRecord = new BotMatchRecord
                        {
                            UserId = humanUser?.Id ?? 0,
                            Username = humanName,
                            BotName = botName,
                            BetAmount = bet,
                            UserWon = humanWon,
                            CoinsWon = humanWon ? (winnerPrize - bet) : 0,
                            CoinsLost = humanWon ? 0 : bet,
                            HouseCommission = houseCommission,
                            HouseProfit = humanWon ? -(winnerPrize - bet) : bet,
                            UserCoinsBefore = 0,
                            UserCoinsAfter = humanUser?.Coins ?? 0,
                            EndReason = !string.IsNullOrEmpty(m.EndReason) ? m.EndReason : "VictoriaPorPuntos",
                            CreatedAt = m.CreatedAt
                        };
                        db.BotMatchRecords.Add(botRecord);
                    }
                    db.SaveChanges();
                    Console.WriteLine($"[RestoreBotMatches] Se restauraron con éxito {botBetMatches.Count} partidas vs Bot desde MatchBetRecords.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RestoreBotMatches Error] {ex.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[VirtualBots Seed Error] {ex.Message}");
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

        if (!db.PromoCodes.Any(p => p.Code == "PERICONQR"))
        {
            db.PromoCodes.Add(new PromoCode
            {
                Code = "PERICONQR",
                CoinsReward = 100,
                MaxUses = 100000,
                TimesUsed = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            Console.WriteLine("[PromoCode] Código promocional de Flyer 'PERICONQR' (+100 monedas) activado.");
        }

        if (!db.PromoCodes.Any(p => p.Code == "DEMO400"))
        {
            db.PromoCodes.Add(new PromoCode
            {
                Code = "DEMO400",
                CoinsReward = 100,
                MaxUses = 500000,
                TimesUsed = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            Console.WriteLine("[PromoCode] Código promocional de Modo Demo 'DEMO400' (+100 monedas) activado.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[PromoCode Error] {ex.Message}");
    }

    // Anuncio Global Oficial: Cambio a Banco del Tesoro y Acreditación Automática
    try
    {
        var existingBancoTesoroAnnouncement = db.SystemAnnouncements.FirstOrDefault(a => a.Title.Contains("BANCO DEL TESORO"));
        if (existingBancoTesoroAnnouncement == null)
        {
            db.SystemAnnouncements.Add(new SystemAnnouncement
            {
                Title = "🏦 ¡NUEVO BANCO RECEPTOR: BANCO DEL TESORO (0163)!",
                Message = "¡Atención a todos los jugadores! A partir de ahora las recargas de saldo se realizan a Banco del Tesoro (0163) | Teléfono: 0412-9278135 | Cédula: 26554121. ¡Tus pagos ahora se validan y acreditan automáticamente en segundos al reportarlos!",
                Type = "alerta",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Sistema El Pericón"
            });
            db.SaveChanges();
            Console.WriteLine("[Announcements] Anuncio de Banco del Tesoro activado con éxito.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Announcements Error] {ex.Message}");
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
