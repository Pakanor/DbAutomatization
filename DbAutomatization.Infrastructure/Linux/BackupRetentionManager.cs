using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class BackupRetentionManager : IBackupRetentionManager
{
    public Task CleanupAsync(string backupDirectory, string filePrefix, int retentionCount)
    {
        if (retentionCount <= 0 || !Directory.Exists(backupDirectory))
            return Task.CompletedTask;

        var files = Directory.GetFiles(backupDirectory, $"{filePrefix}*")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        if (files.Count <= retentionCount)
            return Task.CompletedTask;

        var toDelete = files.Skip(retentionCount);
        foreach (var file in toDelete)
        {
            try
            {
                file.Delete();
            }
            catch
            {
            }
        }

        return Task.CompletedTask;
    }
}
