namespace DbAutomatization.Core.Models;

public record BackupResult(string FilePath, string Message, int ExitCode)
{
    public bool Success => ExitCode == 0;
}
