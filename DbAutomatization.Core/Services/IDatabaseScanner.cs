using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services
{
    public interface IDatabaseScanner
    {
        Task<List<DetectedDatabase>> ScanForDatabasesAsync(CancellationToken cancellationToken = default);
    }
}
