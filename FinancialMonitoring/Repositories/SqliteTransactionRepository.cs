using FinancialMonitoring.Core.Data;
using FinancialMonitoring.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialMonitoring.Core.Repositories
{
    public class SqliteTransactionRepository
        : ITransactionRepository
    {
        private readonly AppDbContext _context;

        public SqliteTransactionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ClientEntity>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.Clients
                .AsNoTracking()
                .Include(c => c.Accounts)
                .ThenInclude(a => a.Transactions)
                .ToListAsync(cancellationToken);
        }

        public async Task<TransactionEntity?> GetByIdAsync(
            string txId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Transactions
                .Include(t => t.Account)
                .FirstOrDefaultAsync(
                    t => t.TxId == txId,
                    cancellationToken);
        }

        public async Task AddAsync(
            TransactionEntity transaction,
            CancellationToken cancellationToken = default)
        {
            await _context.Transactions.AddAsync(
                transaction, cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> DeleteAsync(
            string txId,
            CancellationToken cancellationToken = default)
        {
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(
                    t => t.TxId == txId,
                    cancellationToken);

            if (transaction == null)
                return false;

            _context.Transactions.Remove(transaction);

            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<List<TransactionEntity>> FindAsync(
            Func<TransactionEntity, bool> predicate,
            CancellationToken cancellationToken = default)
        {
            var transactions = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            return transactions.Where(predicate).ToList();
        }
    }
}
