namespace FinancialMonitoring.Core.DTO
{
    public class AccountDto
    {
        public string Iban { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;

        public List<TransactionDto> Transactions { get; set; } = new List<TransactionDto>();
    }
}
