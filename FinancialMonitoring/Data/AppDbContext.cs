using FinancialMonitoring.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialMonitoring.Core.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<ClientEntity> Clients
            => Set<ClientEntity>();

        public DbSet<AccountEntity> Accounts
            => Set<AccountEntity>();

        public DbSet<TransactionEntity> Transactions
            => Set<TransactionEntity>();

        public AppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(
            ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Клієнти
            modelBuilder.Entity<ClientEntity>()
                .HasKey(c => c.Id);

            modelBuilder.Entity<ClientEntity>()
                .HasIndex(c => c.ClientId)
                .IsUnique();

            modelBuilder.Entity<ClientEntity>()
                .Property(c => c.ClientId)
                .IsRequired();

            // 2. Банківські рахунки
            modelBuilder.Entity<AccountEntity>()
                .HasKey(a => a.Id);

            modelBuilder.Entity<AccountEntity>()
                .HasIndex(a => a.Iban)
                .IsUnique();

            modelBuilder.Entity<AccountEntity>()
                .HasOne(a => a.Client)
                .WithMany(c => c.Accounts)
                .HasForeignKey(a => a.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // 3. Транзакції
            modelBuilder.Entity<TransactionEntity>()
                .HasKey(t => t.Id);

            modelBuilder.Entity<TransactionEntity>()
                .HasIndex(t => t.TxId)
                .IsUnique();

            modelBuilder.Entity<TransactionEntity>()
                .HasIndex(t => t.Timestamp);

            modelBuilder.Entity<TransactionEntity>()
                .HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TransactionEntity>()
                .Property(t => t.Amount)
                .HasConversion<string>();
        }
    }
}
