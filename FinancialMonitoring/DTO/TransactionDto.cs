namespace FinancialMonitoring.Core.DTO
{
    public class TransactionDto
    {
        public string TxId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
        public string Merchant { get; set; } = string.Empty;
    }
}
