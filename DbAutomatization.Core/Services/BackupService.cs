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
        if (config == null || config.BackupCommand == null || config.BackupCommand.Length == 0)
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

        string[] finalArgs = config.BackupCommand
            .Select(arg => arg.Replace("{path}", fullPath))
            .ToArray();

        if (!string.IsNullOrEmpty(databaseName))
            finalArgs = finalArgs.Select(arg => arg.Replace("{db}", databaseName)).ToArray();

        bool hasPathInTemplate = config.BackupCommand.Any(arg => arg.Contains("{path}"));
        bool needsShell = finalArgs.Any(arg => arg.Contains("&&") || arg.Contains('|') || arg.Contains(';'));

        ProcessResult result;
        if (needsShell)
        {
            result = await _process.ExecuteAsync(
                finalArgs[0], finalArgs[1..],
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }
        else if (hasPathInTemplate)
        {
            result = await _process.ExecuteAsync(
                finalArgs[0], finalArgs[1..],
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }
        else
        {
            using var fileStream = File.Create(fullPath);
            result = await _process.ExecuteAndStreamOutputAsync(
                finalArgs[0], finalArgs[1..], fileStream,
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }

        return new BackupResult(fullPath, result.Error, result.ExitCode);
    }
}
