using System.Runtime.CompilerServices;
using System.Text.Json;
using FinancialMonitoring.Core.DTO;

namespace FinancialMonitoring.Core.Services
{
    public class JsonFileLoader : IFileLoader
    {
        public async IAsyncEnumerable<ClientDto> LoadAsync(
            string filePath,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await using FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            await foreach (var client in JsonSerializer
                .DeserializeAsyncEnumerable<ClientDto>(
                    stream,
                    options,
                    cancellationToken: cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (client != null)
                {
                    yield return client;
                }
            }
        }
    }
}
