namespace DbAutomatization.Core.Services;

public static class CommandTemplate
{
    public static string[] Substitute(
        string[] command,
        string? serviceName = null,
        string? path = null,
        string? databaseName = null)
    {
        string[] substituted = command
            .Select(arg => path != null ? arg.Replace("{path}", path) : arg)
            .Select(arg => serviceName != null ? arg.Replace("{serviceName}", serviceName) : arg)
            .ToArray();

        if (!string.IsNullOrEmpty(databaseName))
            substituted = substituted.Select(arg => arg.Replace("{db}", databaseName)).ToArray();

        return substituted;
    }
}
