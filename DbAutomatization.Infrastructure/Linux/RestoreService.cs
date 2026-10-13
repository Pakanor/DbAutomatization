using System.IO.Compression;
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

        string backupDir = BackupService.GetBackupDirectory();
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

        return await RestoreFromFileAsync(finalArgs, filePath, cancellationToken);
    }

    private async Task<ProcessResult> RestoreFromFileAsync(
        string[] finalArgs,
        string filePath,
        CancellationToken cancellationToken)
    {
        bool isCompressed = IsGZipFile(filePath);

        if (isCompressed)
        {
            await using var fileStream = File.OpenRead(filePath);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress, leaveOpen: false);
            return await _process.ExecuteWithStdinStreamAsync(
                finalArgs[0], finalArgs[1..], gzipStream,
                timeoutSeconds: 300,
                cancellationToken: cancellationToken);
        }

        return await _process.ExecuteWithStdinFileAsync(
            finalArgs[0], finalArgs[1..], filePath,
            timeoutSeconds: 300,
            cancellationToken: cancellationToken);
    }

    private static bool IsGZipFile(string filePath)
    {
        if (filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            var buffer = new byte[2];
            int read = stream.Read(buffer, 0, 2);
            return read == 2 && buffer[0] == 0x1F && buffer[1] == 0x8B;
        }
        catch
        {
            return false;
        }
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
        var allowedExtensions = new[] { ".sql", ".bak", ".dump", ".gz" };
        if (!allowedExtensions.Contains(extension))
        {
            throw new DatabaseConfigurationException(
                $"Plik backupu '{filePath}' ma nieobsługiwane rozszerzenie '{extension}'. " +
                "Oczekiwane: .sql, .bak, .dump, .gz");
        }
    }

    private static string[] SelectRestoreCommand(DatabaseConfig config, string? databaseName)
    {
        if (!string.IsNullOrEmpty(databaseName) && config.SingleDatabaseRestoreCommand is { Length: > 0 })
            return config.SingleDatabaseRestoreCommand;

        return config.RestoreCommand ?? [];
    }
}
