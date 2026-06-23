using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdScanner : IDatabaseScanner
{
    private readonly IBashExecutor _bash;
    
    private readonly Dictionary<string, string> _monitoredBases = new()
    {
        { "PostgreSQL", "postgresql" },
        { "MariaDB / MySQL", "mariadb" },
        { "MongoDB", "mongod" },
        { "Redis", "redis-server" }
    };

    public SystemdScanner(IBashExecutor bash)
    {
        _bash = bash;
    }

    public async Task<List<DetectedDatabase>> ScanForDatabasesAsync()
    {
        var results = new List<DetectedDatabase>();

        foreach (var db in _monitoredBases)
        {
            bool isInstalled = await IsServiceInstalledAsync(db.Value);
            bool isRunning = isInstalled && await IsServiceRunningAsync(db.Value);

            results.Add(new DetectedDatabase
            {
                Name = db.Key,
                ServiceName = db.Value,
                IsInstalled = isInstalled,
                IsRunning = isRunning
            });
        }

        return results;
    }

    private async Task<bool> IsServiceInstalledAsync(string serviceName)
    {
        var output = await _bash.ExecuteAsync($"systemctl list-unit-files --type=service | grep {serviceName}");
        return !string.IsNullOrWhiteSpace(output) && output.Contains(serviceName);
    }

    private async Task<bool> IsServiceRunningAsync(string serviceName)
    {
        var output = await _bash.ExecuteAsync($"systemctl is-active {serviceName}");
        return output.Trim() == "active";
    }
}