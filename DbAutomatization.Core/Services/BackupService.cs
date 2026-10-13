#pragma warning disable CA1416

using System.IO.Compression;
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
        bool compress = true,
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

        string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        Directory.CreateDirectory(backupDir);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff");
        string dbNamePart = !string.IsNullOrEmpty(databaseName) ? $"_{databaseName}" : "";
        string extension = compress ? ".bak.gz" : ".bak";
        string fileName = $"backup_{serviceName}{dbNamePart}_{timestamp}{extension}";
        string fullPath = Path.Combine(backupDir, fileName);
        int timeout = config.BackupTimeoutSeconds;

        if (config.PreBackupCommand is { Length: > 0 })
        {
            string[] preArgs = CommandTemplate.Substitute(config.PreBackupCommand, serviceName, fullPath, databaseName, backupDir);

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

        string[] finalArgs = CommandTemplate.Substitute(baseCommand, serviceName, fullPath, databaseName, backupDir);

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
            Stream outputStream = compress
                ? new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: false)
                : fileStream;

            if (compress)
            {
                await using var gzipStream = (GZipStream)outputStream;
                result = await _process.ExecuteAndStreamOutputAsync(
                    finalArgs[0], finalArgs[1..], gzipStream,
                    timeoutSeconds: timeout,
                    cancellationToken: cancellationToken);
            }
            else
            {
                result = await _process.ExecuteAndStreamOutputAsync(
                    finalArgs[0], finalArgs[1..], fileStream,
                    timeoutSeconds: timeout,
                    cancellationToken: cancellationToken);
            }
        }

        if (result.Success)
        {
            try { File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead); } catch { }
        }

        if (result.Success && config.RetentionCount > 0)
        {
            string filePrefix = $"backup_{serviceName}";
            await _retention.CleanupAsync(
                backupDir,
                filePrefix,
                config.RetentionCount,
                cancellationToken);
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
