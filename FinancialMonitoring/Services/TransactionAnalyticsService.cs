using FinancialMonitoring.Core.Entities;

namespace FinancialMonitoring.Core.Services
{
    public class TransactionAnalyticsService
    {
        // 1. Великі транзакції
        public List<TransactionEntity> GetLargeTransactions(
            IEnumerable<TransactionEntity> transactions,
            decimal minAmount,
            string currency)
        {
            return transactions
                .Where(t => t.Account.Currency == currency
                         && t.Amount >= minAmount)
                .OrderByDescending(t => t.Amount)
                .ToList();
        }

        // 2. Серійні транзакції за короткий час
        // Мінімум 3 операції протягом 10 хвилин
        public List<RapidTransactionAlert> GetRapidTransactions(
            IEnumerable<TransactionEntity> transactions,
            int minCount = 3,
            int windowMinutes = 10)
        {
            if (minCount < 2 || windowMinutes <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(minCount),
                    "Некоректні параметри аналізу.");

            return transactions
                .GroupBy(t => new
                {
                    t.Account.Iban,
                    t.Account.Currency
                })
                .SelectMany(group =>
                {
                    var ordered = group
                        .OrderBy(t => t.Timestamp)
                        .ToList();

                    var alerts = new List<RapidTransactionAlert>();

                    // Ковзне часове вікно
                    for (int i = 0; i <= ordered.Count - minCount;)
                    {
                        int end = i + minCount - 1;

                        if ((ordered[end].Timestamp -
                             ordered[i].Timestamp).TotalMinutes
                            <= windowMinutes)
                        {
                            // Розширюємо знайдену серію
                            while (end + 1 < ordered.Count &&
                                   (ordered[end + 1].Timestamp -
                                    ordered[i].Timestamp).TotalMinutes
                                   <= windowMinutes)
                            {
                                end++;
                            }

                            var series = ordered
                                .Skip(i)
                                .Take(end - i + 1)
                                .ToList();

                            alerts.Add(new RapidTransactionAlert
                            {
                                Iban = group.Key.Iban,
                                Currency = group.Key.Currency,
                                TransactionCount = series.Count,
                                TotalAmount = series.Sum(t => t.Amount),
                                StartTime = series.First().Timestamp,
                                EndTime = series.Last().Timestamp,
                                Reason = "Серія операцій за короткий час"
                            });

                            // Не дублюємо вже знайдену серію
                            i = end + 1;
                        }
                        else
                        {
                            i++;
                        }
                    }

                    return alerts;
                })
                .OrderByDescending(a => a.TransactionCount)
                .ThenByDescending(a => a.TotalAmount)
                .ToList();
        }

        // 3. Дроблення сум протягом однієї доби
        public List<StructuringAlert> GetStructuringTransactions(
            IEnumerable<TransactionEntity> transactions,
            decimal threshold,
            string currency)
        {
            if (threshold <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(threshold));

            return transactions
                .Where(t => t.Account.Currency == currency
                         && t.Amount > 0
                         && t.Amount < threshold)
                .GroupBy(t => new
                {
                    t.Account.Iban,
                    Date = t.Timestamp.Date
                })
                .Where(g => g.Count() >= 2 &&
                            g.Sum(t => t.Amount) >= threshold)
                .Select(g => new StructuringAlert
                {
                    Iban = g.Key.Iban,
                    Currency = currency,
                    Date = g.Key.Date,
                    TransactionCount = g.Count(),
                    TotalAmount = g.Sum(t => t.Amount),
                    Reason = "Можливе дроблення суми"
                })
                .OrderByDescending(a => a.TotalAmount)
                .ToList();
        }

        // 4. Загальна статистика
        public List<OverallStatistics> GetOverallStatistics(
            IEnumerable<TransactionEntity> transactions)
        {
            return transactions
                .GroupBy(t => t.Account.Currency)
                .Select(g => new OverallStatistics
                {
                    Currency = g.Key,
                    TransactionCount = g.Count(),
                    TotalAmount = g.Sum(t => t.Amount),
                    AverageAmount = g.Average(t => t.Amount),
                    MinAmount = g.Min(t => t.Amount),
                    MaxAmount = g.Max(t => t.Amount)
                })
                .OrderBy(x => x.Currency)
                .ToList();
        }
    }

    // Результат виявлення серійних операцій
    public class RapidTransactionAlert
    {
        public string Iban { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Результат виявлення дроблення сум
    public class StructuringAlert
    {
        public string Iban { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Загальна статистика
    public class OverallStatistics
    {
        public string Currency { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AverageAmount { get; set; }
        public decimal MinAmount { get; set; }
        public decimal MaxAmount { get; set; }
    }
}
