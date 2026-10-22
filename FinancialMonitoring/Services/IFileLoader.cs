using FinancialMonitoring.Core.DTO;

namespace FinancialMonitoring.Core.Services
{
    public interface IFileLoader
    {
        IAsyncEnumerable<ClientDto> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }
}
