using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public class BackupService : IBackupService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;

    public BackupService(IDatabaseConfigRepository configRepo, IProcessExecutor process)
    {
        _configRepo = configRepo;
        _process = process;
    }

    public async Task<BackupResult> CreateBackupAsync(
        string serviceName,
        string? databaseName = null,
        CancellationToken cancellationToken = default)
    {
        var config = _configRepo.Get(serviceName);
        if (config == null || string.IsNullOrWhiteSpace(config.BackupCommand))
        {
            return new BackupResult(
                string.Empty,
                $"Brak zdefiniowanej komendy backupu dla '{serviceName}'.",
                -1);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
        string dbNamePart = !string.IsNullOrEmpty(databaseName) ? $"_{databaseName}" : "";
        string fileName = $"backup_{serviceName}{dbNamePart}_{timestamp}.bak";
        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        string finalCommand = config.BackupCommand.Replace("{path}", fullPath);

        if (!string.IsNullOrEmpty(databaseName))
            finalCommand = finalCommand.Replace("{db}", databaseName);

        bool needsShell = finalCommand.Contains("&&")
                       || finalCommand.Contains('|')
                       || finalCommand.Contains(';');

        var (exeName, args) = CommandParser.Parse(finalCommand);
        bool hasPathInTemplate = config.BackupCommand.Contains("{path}");

        ProcessResult result;
        if (needsShell)
        {
            result = await _process.ExecuteAsync(
                "/bin/bash", ["-c", finalCommand],
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }
        else if (hasPathInTemplate)
        {
            result = await _process.ExecuteAsync(
                exeName, args,
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }
        else
        {
            using var fileStream = File.Create(fullPath);
            result = await _process.ExecuteAndStreamOutputAsync(
                exeName, args, fileStream,
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }

        return new BackupResult(fullPath, result.Error, result.ExitCode);
    }
}
