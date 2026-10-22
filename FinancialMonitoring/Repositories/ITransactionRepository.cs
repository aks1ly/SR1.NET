using FinancialMonitoring.Core.Entities;

namespace FinancialMonitoring.Core.Repositories
{
    public interface ITransactionRepository
    {
        Task<List<ClientEntity>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<TransactionEntity?> GetByIdAsync(
            string txId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            TransactionEntity transaction,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            string txId,
            CancellationToken cancellationToken = default);

        Task<List<TransactionEntity>> FindAsync(
            Func<TransactionEntity, bool> predicate,
            CancellationToken cancellationToken = default);
    }
}
