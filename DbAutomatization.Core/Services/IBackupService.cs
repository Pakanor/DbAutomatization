using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IBackupService
{
    Task<BackupResult> CreateBackupAsync(
        string serviceName,
        string? databaseName = null,
        CancellationToken cancellationToken = default);
}
