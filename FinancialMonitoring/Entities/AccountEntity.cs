namespace FinancialMonitoring.Core.Entities
{
    public class AccountEntity
    {
        public int Id { get; set; }

        public string Iban { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;

        public int ClientId { get; set; }
        public ClientEntity Client { get; set; } = null!;

        public List<TransactionEntity> Transactions { get; set; }
            = new List<TransactionEntity>();
    }
}
