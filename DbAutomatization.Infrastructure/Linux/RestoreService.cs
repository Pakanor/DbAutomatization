using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class RestoreService : IRestoreService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;

    public RestoreService(IDatabaseConfigRepository configRepo, IProcessExecutor process)
    {
        _configRepo = configRepo;
        _process = process;
    }

    public async Task<ProcessResult> RestoreAsync(
        string serviceName,
        string filePath,
        string? databaseName = null,
        CancellationToken cancellationToken = default)
    {
        var config = _configRepo.Get(serviceName);
        if (config == null)
        {
            return new ProcessResult(
                string.Empty,
                $"Brak konfiguracji dla '{serviceName}'.",
                -1);
        }

        string[] baseCommand = SelectRestoreCommand(config, databaseName);
        if (baseCommand.Length == 0)
        {
            return new ProcessResult(
                string.Empty,
                $"Brak zdefiniowanej komendy restore dla '{serviceName}'.",
                -1);
        }

        if (!File.Exists(filePath))
        {
            return new ProcessResult(
                string.Empty,
                $"Plik backupu nie istnieje: '{filePath}'.",
                -1);
        }

        string[] finalArgs = CommandTemplate.Substitute(baseCommand, serviceName, filePath, databaseName);

        bool hasPathInTemplate = baseCommand.Any(arg => arg.Contains("{path}"));

        if (hasPathInTemplate)
        {
            return await _process.ExecuteAsync(
                finalArgs[0], finalArgs[1..],
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }

        return await _process.ExecuteWithStdinFileAsync(
            finalArgs[0], finalArgs[1..], filePath,
            timeoutSeconds: 300,
            cancellationToken: cancellationToken);
    }

    private static string[] SelectRestoreCommand(DatabaseConfig config, string? databaseName)
    {
        if (!string.IsNullOrEmpty(databaseName) && config.SingleDatabaseRestoreCommand is { Length: > 0 })
            return config.SingleDatabaseRestoreCommand;

        return config.RestoreCommand ?? [];
    }
}
