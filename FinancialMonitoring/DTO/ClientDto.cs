namespace FinancialMonitoring.Core.DTO
{
    public class ClientDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;

        public List<AccountDto> Accounts { get; set; } = new List<AccountDto>();
    }
}
