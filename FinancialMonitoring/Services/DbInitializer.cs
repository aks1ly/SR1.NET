using FinancialMonitoring.Core.Data;
using FinancialMonitoring.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinancialMonitoring.Core.Services
{
    public enum ImportMode
    {
        Add,
        Replace
    }

    public class ImportResult
    {
        public int ClientsAdded { get; set; }
        public int AccountsAdded { get; set; }
        public int TransactionsAdded { get; set; }
        public int DuplicatesSkipped { get; set; }
    }

    public class DbInitializer
    {
        private readonly AppDbContext _context;
        private readonly IFileLoader _jsonLoader;

        public DbInitializer(
            AppDbContext context,
            IFileLoader jsonLoader)
        {
            _context = context;
            _jsonLoader = jsonLoader;
        }

        public async Task<ImportResult> InitializeAsync(
            string jsonFilePath,
            ImportMode mode = ImportMode.Add,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jsonFilePath) ||
                !File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException(
                    "JSON-файл не знайдено.", jsonFilePath);
            }

            if (!string.Equals(
                Path.GetExtension(jsonFilePath),
                ".json",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Підтримуються лише файли JSON.");
            }

            await _context.Database.EnsureCreatedAsync(
                cancellationToken);

            var result = new ImportResult();

            // Усі зміни виконуємо в одній транзакції
            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                if (mode == ImportMode.Replace)
                {
                    // Очищаємо таблиці в правильному порядку
                    await _context.Transactions.ExecuteDeleteAsync(
                        cancellationToken);

                    await _context.Accounts.ExecuteDeleteAsync(
                        cancellationToken);

                    await _context.Clients.ExecuteDeleteAsync(
                        cancellationToken);

                    _context.ChangeTracker.Clear();
                }

                // Завантажуємо актуальні дані з БД
                var existingClients = await _context.Clients
                    .Include(c => c.Accounts)
                    .ThenInclude(a => a.Transactions)
                    .ToListAsync(cancellationToken);

                var clientsById = existingClients
                    .ToDictionary(c => c.ClientId);

                var accountsByIban = existingClients
                    .SelectMany(c => c.Accounts)
                    .ToDictionary(a => a.Iban);

                var transactionIds = existingClients
                    .SelectMany(c => c.Accounts)
                    .SelectMany(a => a.Transactions)
                    .Select(t => t.TxId)
                    .ToHashSet();

                // Асинхронне читання JSON
                await foreach (var clientDto in
                    _jsonLoader.LoadAsync(
                        jsonFilePath, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Клієнт
                    if (!clientsById.TryGetValue(
                        clientDto.ClientId,
                        out var clientEntity))
                    {
                        clientEntity = new ClientEntity
                        {
                            ClientId = clientDto.ClientId,
                            TaxCode = clientDto.TaxCode
                        };

                        _context.Clients.Add(clientEntity);

                        clientsById.Add(
                            clientEntity.ClientId, clientEntity);

                        result.ClientsAdded++;
                    }
                    else if (clientEntity.TaxCode != clientDto.TaxCode)
                    {
                        throw new InvalidDataException(
                            $"Клієнт {clientDto.ClientId} " +
                            "має інший податковий код.");
                    }

                    // Рахунки
                    foreach (var accountDto in clientDto.Accounts)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!accountsByIban.TryGetValue(
                            accountDto.Iban,
                            out var accountEntity))
                        {
                            accountEntity = new AccountEntity
                            {
                                Iban = accountDto.Iban,
                                Currency = accountDto.Currency,
                                Client = clientEntity
                            };

                            clientEntity.Accounts.Add(accountEntity);

                            accountsByIban.Add(
                                accountEntity.Iban, accountEntity);

                            result.AccountsAdded++;
                        }
                        else
                        {
                            if (accountEntity.Client.ClientId !=
                                clientDto.ClientId)
                            {
                                throw new InvalidDataException(
                                    $"Рахунок {accountDto.Iban} " +
                                    "належить іншому клієнту.");
                            }

                            if (accountEntity.Currency !=
                                accountDto.Currency)
                            {
                                throw new InvalidDataException(
                                    $"Валюта рахунку {accountDto.Iban} " +
                                    "не збігається.");
                            }
                        }

                        // Транзакції
                        foreach (var transactionDto in
                            accountDto.Transactions)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!transactionIds.Add(
                                transactionDto.TxId))
                            {
                                result.DuplicatesSkipped++;
                                continue;
                            }

                            var transactionEntity =
                                new TransactionEntity
                                {
                                    TxId = transactionDto.TxId,
                                    Amount = transactionDto.Amount,
                                    Timestamp = transactionDto.Timestamp,
                                    Merchant = transactionDto.Merchant,
                                    Account = accountEntity
                                };

                            accountEntity.Transactions.Add(
                                transactionEntity);

                            result.TransactionsAdded++;
                        }
                    }
                }

                await _context.SaveChangesAsync(
                    cancellationToken);

                await dbTransaction.CommitAsync(
                    cancellationToken);

                return result;
            }
            catch
            {
                await dbTransaction.RollbackAsync(
                    CancellationToken.None);

                _context.ChangeTracker.Clear();

                throw;
            }
        }
    }
}
