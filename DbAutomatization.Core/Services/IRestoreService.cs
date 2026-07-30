using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IRestoreService
{
    Task<ProcessResult> RestoreAsync(
        string serviceName,
        string filePath,
        string? databaseName = null,
        CancellationToken cancellationToken = default);
}
