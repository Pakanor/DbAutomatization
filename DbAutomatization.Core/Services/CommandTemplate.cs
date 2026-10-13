using System.Text.RegularExpressions;
using DbAutomatization.Core.Exceptions;

namespace DbAutomatization.Core.Services;

public static class CommandTemplate
{
    private static readonly Regex DatabaseNameRegex = new(@"^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

    public static string[] Substitute(
        string[] command,
        string? serviceName = null,
        string? path = null,
        string? databaseName = null,
        string? backupDirectory = null)
    {
        if (!string.IsNullOrEmpty(databaseName))
        {
            ValidateDatabaseName(databaseName);
        }

        string? resolvedPath = null;
        if (!string.IsNullOrEmpty(path))
        {
            resolvedPath = ResolveAndValidatePath(path, backupDirectory);
        }

        string[] substituted = command
            .Select(arg => resolvedPath != null ? arg.Replace("{path}", resolvedPath) : arg)
            .Select(arg => serviceName != null ? arg.Replace("{serviceName}", serviceName) : arg)
            .ToArray();

        if (!string.IsNullOrEmpty(databaseName))
            substituted = substituted.Select(arg => arg.Replace("{db}", databaseName)).ToArray();

        return substituted;
    }

    private static void ValidateDatabaseName(string databaseName)
    {
        if (!DatabaseNameRegex.IsMatch(databaseName))
        {
            throw new DatabaseConfigurationException(
                $"Nazwa bazy danych '{databaseName}' zawiera niedozwolone znaki. " +
                "Dozwolone: litery, cyfry, podkreślnik, kropka, myślnik.");
        }
    }

    private static string ResolveAndValidatePath(string path, string? backupDirectory)
    {
        string fullPath = Path.GetFullPath(path);

        if (!string.IsNullOrEmpty(backupDirectory))
        {
            string fullBackupDir = Path.GetFullPath(backupDirectory);
            if (!fullPath.StartsWith(fullBackupDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new DatabaseConfigurationException(
                    $"Ścieżka backupu '{path}' próbuje wyjść poza dozwolony katalog '{backupDirectory}'.");
            }
        }

        return fullPath;
    }
}
