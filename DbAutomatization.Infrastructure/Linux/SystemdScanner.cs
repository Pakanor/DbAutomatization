using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdScanner : IDatabaseScanner
{
    private readonly IProcessExecutor _process;
    private readonly IDatabaseConfigRepository _configRepo;

    public SystemdScanner(IProcessExecutor process, IDatabaseConfigRepository configRepo)
    {
        _process = process;
        _configRepo = configRepo;
    }

    public async Task<List<DetectedDatabase>> ScanForDatabasesAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<DetectedDatabase>();

        var unitFilesResult = await _process.ExecuteAsync(
            "systemctl",
            ["list-unit-files", "--type=service", "--no-legend"],
            timeoutSeconds: 15,
            cancellationToken: cancellationToken);

        var existingUnits = ParseUnitFiles(unitFilesResult.Output);

        var trackedServices = _configRepo.GetAll();

        foreach (var kvp in trackedServices)
        {
            string serviceName = kvp.Key;
            string unitName = $"{serviceName}.service";
            bool isInstalled = existingUnits.Contains(unitName);

            bool isRunning = false;
            if (isInstalled)
            {
                var activeResult = await _process.ExecuteAsync(
                    "systemctl", ["is-active", serviceName],
                    timeoutSeconds: 10,
                    cancellationToken: cancellationToken);

                isRunning = activeResult.Output.Trim() == "active";
            }

            results.Add(new DetectedDatabase
            {
                Name = kvp.Value.FriendlyName,
                ServiceName = serviceName,
                IsInstalled = isInstalled,
                IsRunning = isRunning
            });
        }

        return results;
    }

    private static HashSet<string> ParseUnitFiles(string output)
    {
        var units = new HashSet<string>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            int spaceIndex = trimmed.IndexOf(' ');
            string unitName = spaceIndex > 0 ? trimmed[..spaceIndex] : trimmed;

            if (unitName.EndsWith(".service"))
                units.Add(unitName);
        }

        return units;
    }
}
