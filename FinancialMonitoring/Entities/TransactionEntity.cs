namespace FinancialMonitoring.Core.Entities
{
    public class TransactionEntity
    {
        public int Id { get; set; }

        public string TxId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
        public string Merchant { get; set; } = string.Empty;

        public int AccountId { get; set; }
        public AccountEntity Account { get; set; } = null!;
    }
}
