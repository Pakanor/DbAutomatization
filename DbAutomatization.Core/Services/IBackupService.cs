using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(
        string serviceName,
        string? databaseName = null,
        bool compress = true,
        CancellationToken cancellationToken = default);
}
