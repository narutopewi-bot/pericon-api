using Microsoft.EntityFrameworkCore;
using PericonAPI.Models;

namespace PericonAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<PaymentRecharge> PaymentRecharges { get; set; }
        public DbSet<PaymentWithdrawal> PaymentWithdrawals { get; set; }
        public DbSet<MatchBetRecord> MatchBetRecords { get; set; }
        public DbSet<BotMatchRecord> BotMatchRecords { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<PromoCodeRedemption> PromoCodeRedemptions { get; set; }
        public DbSet<AppErrorLog> AppErrorLogs { get; set; }
        public DbSet<SystemAnnouncement> SystemAnnouncements { get; set; }
        public DbSet<PlayerFeedback> PlayerFeedbacks { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentParticipant> TournamentParticipants { get; set; }
        public DbSet<TournamentMatch> TournamentMatches { get; set; }
        public DbSet<ReferralRecord> ReferralRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });
        }
    }
}
