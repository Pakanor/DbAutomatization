using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public class BackupService : IBackupService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;
    private readonly IBackupRetentionManager _retention;

    public BackupService(
        IDatabaseConfigRepository configRepo,
        IProcessExecutor process,
        IBackupRetentionManager retention)
    {
        _configRepo = configRepo;
        _process = process;
        _retention = retention;
    }

    public async Task<BackupResult> CreateBackupAsync(
        string serviceName,
        string? databaseName = null,
        CancellationToken cancellationToken = default)
    {
        var config = _configRepo.Get(serviceName);
        if (config == null)
        {
            return new BackupResult(
                string.Empty,
                $"Brak zdefiniowanej komendy backupu dla '{serviceName}'.",
                -1);
        }

        string[] baseCommand = SelectBackupCommand(config, databaseName);
        if (baseCommand.Length == 0)
        {
            return new BackupResult(
                string.Empty,
                $"Brak zdefiniowanej komendy backupu dla '{serviceName}'.",
                -1);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string dbNamePart = !string.IsNullOrEmpty(databaseName) ? $"_{databaseName}" : "";
        string fileName = $"backup_{serviceName}{dbNamePart}_{timestamp}.bak";
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        int timeout = config.BackupTimeoutSeconds;

        if (config.PreBackupCommand is { Length: > 0 })
        {
            string[] preArgs = config.PreBackupCommand
                .Select(arg => arg.Replace("{path}", fullPath))
                .ToArray();

            if (!string.IsNullOrEmpty(databaseName))
                preArgs = preArgs.Select(arg => arg.Replace("{db}", databaseName)).ToArray();

            var preResult = await _process.ExecuteAsync(
                preArgs[0], preArgs[1..],
                timeoutSeconds: timeout,
                cancellationToken: cancellationToken);

            if (!preResult.Success)
            {
                return new BackupResult(
                    string.Empty,
                    $"Pre-backup command failed: {preResult.Error}",
                    preResult.ExitCode);
            }
        }

        string[] finalArgs = baseCommand
            .Select(arg => arg.Replace("{path}", fullPath))
            .ToArray();

        if (!string.IsNullOrEmpty(databaseName))
            finalArgs = finalArgs.Select(arg => arg.Replace("{db}", databaseName)).ToArray();

        bool hasPathInTemplate = baseCommand.Any(arg => arg.Contains("{path}"));

        ProcessResult result;
        if (hasPathInTemplate)
        {
            result = await _process.ExecuteAsync(
                finalArgs[0], finalArgs[1..],
                timeoutSeconds: timeout,
                cancellationToken: cancellationToken);
        }
        else
        {
            using var fileStream = File.Create(fullPath);
            result = await _process.ExecuteAndStreamOutputAsync(
                finalArgs[0], finalArgs[1..], fileStream,
                timeoutSeconds: timeout,
                cancellationToken: cancellationToken);
        }

        if (result.Success && config.RetentionCount > 0)
        {
            string filePrefix = $"backup_{serviceName}";
            await _retention.CleanupAsync(
                AppDomain.CurrentDomain.BaseDirectory,
                filePrefix,
                config.RetentionCount);
        }

        return new BackupResult(fullPath, result.Error, result.ExitCode);
    }

    private static string[] SelectBackupCommand(DatabaseConfig config, string? databaseName)
    {
        if (!string.IsNullOrEmpty(databaseName) && config.SingleDatabaseBackupCommand is { Length: > 0 })
            return config.SingleDatabaseBackupCommand;

        return config.BackupCommand ?? [];
    }
}
