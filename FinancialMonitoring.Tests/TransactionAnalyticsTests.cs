using Microsoft.VisualStudio.TestTools.UnitTesting;
using FinancialMonitoring.Core.Entities;
using FinancialMonitoring.Core.Services;

namespace FinancialMonitoring.Tests
{
    [TestClass]
    public class TransactionAnalyticsTests
    {
        private readonly TransactionAnalyticsService _service
            = new TransactionAnalyticsService();

        // Створення тестових рахунків
        private List<AccountEntity> CreateTestAccounts()
        {
            var account1 = new AccountEntity
            {
                Iban = "UA111",
                Currency = "UAH"
            };

            var account2 = new AccountEntity
            {
                Iban = "UA222",
                Currency = "UAH"
            };

            var account3 = new AccountEntity
            {
                Iban = "UA333",
                Currency = "USD"
            };

            account1.Transactions = new List<TransactionEntity>
            {
                new TransactionEntity
                {
                    TxId = "TX-1",
                    Amount = 150000m,
                    Merchant = "CryptoExchange",
                    Timestamp = new DateTime(2026, 9, 3, 10, 0, 0),
                    Account = account1
                },
                new TransactionEntity
                {
                    TxId = "TX-2",
                    Amount = 20000m,
                    Merchant = "Shop",
                    Timestamp = new DateTime(2026, 9, 3, 10, 3, 0),
                    Account = account1
                },
                new TransactionEntity
                {
                    TxId = "TX-3",
                    Amount = 30000m,
                    Merchant = "Shop",
                    Timestamp = new DateTime(2026, 9, 3, 10, 7, 0),
                    Account = account1
                }
            };

            account2.Transactions = new List<TransactionEntity>
            {
                new TransactionEntity
                {
                    TxId = "TX-4",
                    Amount = 45000m,
                    Merchant = "Transfer",
                    Timestamp = new DateTime(2026, 9, 3, 11, 0, 0),
                    Account = account2
                },
                new TransactionEntity
                {
                    TxId = "TX-5",
                    Amount = 48000m,
                    Merchant = "Transfer",
                    Timestamp = new DateTime(2026, 9, 3, 12, 0, 0),
                    Account = account2
                },
                new TransactionEntity
                {
                    TxId = "TX-6",
                    Amount = 42000m,
                    Merchant = "Transfer",
                    Timestamp = new DateTime(2026, 9, 3, 13, 0, 0),
                    Account = account2
                }
            };

            account3.Transactions = new List<TransactionEntity>
            {
                new TransactionEntity
                {
                    TxId = "TX-7",
                    Amount = 3000m,
                    Merchant = "CryptoExchange",
                    Timestamp = new DateTime(2026, 9, 3, 14, 0, 0),
                    Account = account3
                }
            };

            return new List<AccountEntity>
            {
                account1, account2, account3
            };
        }

        // Отримання всіх тестових транзакцій
        private List<TransactionEntity> CreateTestTransactions()
        {
            return CreateTestAccounts()
                .SelectMany(a => a.Transactions)
                .ToList();
        }

        // 1. Великі транзакції

        [TestMethod]
        public void GetLargeTransactions_ReturnsCorrectTransactions()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetLargeTransactions(
                transactions, 100000m, "UAH");

            Assert.HasCount(1, result);
            Assert.AreEqual("TX-1", result[0].TxId);
            Assert.AreEqual(150000m, result[0].Amount);
        }

        [TestMethod]
        public void GetLargeTransactions_ReturnsEmptyForHighThreshold()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetLargeTransactions(
                transactions, 200000m, "UAH");

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void GetLargeTransactions_SeparatesCurrencies()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetLargeTransactions(
                transactions, 2000m, "USD");

            Assert.HasCount(1, result);
            Assert.AreEqual("TX-7", result[0].TxId);
        }

        // 2. Серійні транзакції

        [TestMethod]
        public void GetRapidTransactions_DetectsRapidSeries()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetRapidTransactions(
                transactions, 3, 10);

            Assert.HasCount(1, result);

            Assert.AreEqual("UA111", result[0].Iban);
            Assert.AreEqual(3, result[0].TransactionCount);
            Assert.AreEqual(200000m, result[0].TotalAmount);

            Assert.AreEqual(
                new DateTime(2026, 9, 3, 10, 0, 0),
                result[0].StartTime);

            Assert.AreEqual(
                new DateTime(2026, 9, 3, 10, 7, 0),
                result[0].EndTime);
        }

        [TestMethod]
        public void GetRapidTransactions_ReturnsEmptyWithoutRapidSeries()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetRapidTransactions(
                transactions, 4, 10);

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void GetRapidTransactions_DoesNotMixAccounts()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetRapidTransactions(
                transactions, 3, 10);

            Assert.IsFalse(result.Any(x => x.Iban == "UA222"));
            Assert.IsFalse(result.Any(x => x.Iban == "UA333"));
        }

        // 3. Дроблення сум

        [TestMethod]
        public void GetStructuringTransactions_DetectsStructuring()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetStructuringTransactions(
                transactions, 100000m, "UAH");

            Assert.HasCount(1, result);

            Assert.AreEqual("UA222", result[0].Iban);
            Assert.AreEqual(3, result[0].TransactionCount);
            Assert.AreEqual(135000m, result[0].TotalAmount);
        }

        [TestMethod]
        public void GetStructuringTransactions_ReturnsEmptyForHighThreshold()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetStructuringTransactions(
                transactions, 500000m, "UAH");

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void GetStructuringTransactions_SeparatesCurrencies()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetStructuringTransactions(
                transactions, 100000m, "USD");

            Assert.IsEmpty(result);
        }

        // 4. Загальна статистика

        [TestMethod]
        public void GetOverallStatistics_CalculatesCorrectValues()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetOverallStatistics(
                transactions);

            var uah = result.Single(x =>
                x.Currency == "UAH");

            Assert.AreEqual(6, uah.TransactionCount);
            Assert.AreEqual(335000m, uah.TotalAmount);
            Assert.AreEqual(20000m, uah.MinAmount);
            Assert.AreEqual(150000m, uah.MaxAmount);

            Assert.AreEqual(
                335000m / 6m, uah.AverageAmount);
        }

        [TestMethod]
        public void GetOverallStatistics_SeparatesCurrencies()
        {
            var transactions = CreateTestTransactions();

            var result = _service.GetOverallStatistics(
                transactions);

            Assert.HasCount(2, result);

            var usd = result.Single(x =>
                x.Currency == "USD");

            Assert.AreEqual(1, usd.TransactionCount);
            Assert.AreEqual(3000m, usd.TotalAmount);
        }

        [TestMethod]
        public void GetOverallStatistics_ReturnsEmptyForNoTransactions()
        {
            var transactions = new List<TransactionEntity>();

            var result = _service.GetOverallStatistics(
                transactions);

            Assert.IsEmpty(result);
        }
    }
}
