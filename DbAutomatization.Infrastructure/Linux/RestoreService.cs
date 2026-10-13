using DbAutomatization.Core.Exceptions;
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

        string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        VerifyBackupFile(filePath, backupDir, config);

        string[] finalArgs = CommandTemplate.Substitute(baseCommand, serviceName, filePath, databaseName, backupDir);

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

    private static void VerifyBackupFile(string filePath, string backupDirectory, DatabaseConfig config)
    {
        if (!File.Exists(filePath))
        {
            throw new DatabaseConfigurationException($"Plik backupu nie istnieje: '{filePath}'.");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            throw new DatabaseConfigurationException($"Plik backupu jest pusty: '{filePath}'.");
        }

        string fullBackupDir = Path.GetFullPath(backupDirectory);
        string fullFilePath = Path.GetFullPath(filePath);

        if (!fullFilePath.StartsWith(fullBackupDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new DatabaseConfigurationException(
                $"Plik backupu '{filePath}' znajduje się poza dozwolonym katalogiem '{backupDirectory}'.");
        }

        ValidateFileExtension(filePath, config);
    }

    private static void ValidateFileExtension(string filePath, DatabaseConfig config)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string engineType = config.EngineType?.ToLowerInvariant() ?? "systemd";

        bool isValidExtension = engineType switch
        {
            "systemd" => extension is ".sql" or ".bak" or ".dump" or ".bak",
            "docker" => extension is ".sql" or ".bak" or ".dump" or ".bak",
            _ => extension is ".sql" or ".bak" or ".dump" or ".bak"
        };

        if (!isValidExtension)
        {
            throw new DatabaseConfigurationException(
                $"Plik backupu '{filePath}' ma nieobsługiwane rozszerzenie '{extension}'. " +
                "Oczekiwane: .sql, .bak, .dump");
        }
    }

    private static string[] SelectRestoreCommand(DatabaseConfig config, string? databaseName)
    {
        if (!string.IsNullOrEmpty(databaseName) && config.SingleDatabaseRestoreCommand is { Length: > 0 })
            return config.SingleDatabaseRestoreCommand;

        return config.RestoreCommand ?? [];
    }
}
