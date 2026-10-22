using FinancialMonitoring.Core.Entities;
using FinancialMonitoring.Core.Repositories;

namespace FinancialMonitoring.Core
{
    public class TransactionCatalog
    {
        private readonly ITransactionRepository _repository;

        public TransactionCatalog(
            ITransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<List<ClientEntity>> GetClientsAsync(
            CancellationToken cancellationToken = default)
        {
            return _repository.GetAllAsync(cancellationToken);
        }

        public Task<TransactionEntity?> GetTransactionAsync(
            string txId,
            CancellationToken cancellationToken = default)
        {
            return _repository.GetByIdAsync(
                txId, cancellationToken);
        }

        public Task AddTransactionAsync(
            TransactionEntity transaction,
            CancellationToken cancellationToken = default)
        {
            return _repository.AddAsync(
                transaction, cancellationToken);
        }

        public Task<bool> RemoveTransactionAsync(
            string txId,
            CancellationToken cancellationToken = default)
        {
            return _repository.DeleteAsync(
                txId, cancellationToken);
        }

        public Task<List<TransactionEntity>> FilterTransactionsAsync(
            Func<TransactionEntity, bool> predicate,
            CancellationToken cancellationToken = default)
        {
            return _repository.FindAsync(
                predicate, cancellationToken);
        }
    }
}
