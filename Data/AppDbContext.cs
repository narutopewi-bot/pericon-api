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
