using Microsoft.EntityFrameworkCore;
using PericonAPI.Data;
using PericonAPI.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=pericon.db"));

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
    db.Database.EnsureCreated();
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Wins INTEGER NOT NULL DEFAULT 0;");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN Losses INTEGER NOT NULL DEFAULT 0;");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0;");
    }
    catch { }
    try
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LastDailyClaim TEXT NULL;");
    }
    catch { }
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
