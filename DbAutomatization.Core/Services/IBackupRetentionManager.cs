namespace DbAutomatization.Core.Services;

public interface IBackupRetentionManager
{
    Task CleanupAsync(string backupDirectory, string filePrefix, int retentionCount, CancellationToken cancellationToken = default);
}
