namespace FinancialMonitoring.Core.Entities
{
    public class ClientEntity
    {
        public int Id { get; set; }

        public string ClientId { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;

        public List<AccountEntity> Accounts { get; set; }
            = new List<AccountEntity>();
    }
}
