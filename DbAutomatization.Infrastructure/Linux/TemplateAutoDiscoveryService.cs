using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class TemplateAutoDiscoveryService : ITemplateAutoDiscoveryService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;

    private static readonly (string ServiceName, DatabaseConfig Config)[] Templates =
    [
        ("postgresql", EngineTemplates.Systemd("postgresql")),
        ("mariadb", EngineTemplates.Systemd("mariadb")),
        ("mongod", EngineTemplates.Systemd("mongod")),
        ("redis-server", EngineTemplates.Systemd("redis-server")),
        ("influxdb", EngineTemplates.Systemd("influxdb")),
        ("postgres-main", EngineTemplates.Docker("postgresql")),
        ("mariadb-main", EngineTemplates.Docker("mariadb")),
        ("mongod-main", EngineTemplates.Docker("mongod")),
        ("redis-main", EngineTemplates.Docker("redis")),
        ("influxdb-main", EngineTemplates.Docker("influxdb"))
    ];

    public TemplateAutoDiscoveryService(
        IDatabaseConfigRepository configRepo,
        IProcessExecutor process)
    {
        _configRepo = configRepo;
        _process = process;
    }

    public async Task AutoDiscoverAsync(CancellationToken cancellationToken = default)
    {
        var existing = _configRepo.GetAll();

        foreach (var (serviceName, config) in Templates)
        {
            if (existing.ContainsKey(serviceName))
                continue;

            bool isPresent = config.EngineType == "docker"
                ? await IsDockerContainerPresent(serviceName, cancellationToken)
                : (await _process.ExecuteAsync(
                    "systemctl", ["is-enabled", serviceName],
                    timeoutSeconds: 5, cancellationToken: cancellationToken)).Success;

            if (isPresent)
            {
                _configRepo.Register(serviceName, config);
                Console.WriteLine($"[Auto-Discovery] Wykryto {config.FriendlyName} ({serviceName}) — dodano do monitorowania.");
            }
        }
    }

    private async Task<bool> IsDockerContainerPresent(string containerName, CancellationToken cancellationToken)
    {
        var result = await _process.ExecuteAsync(
            "docker", ["inspect", containerName],
            timeoutSeconds: 5, cancellationToken: cancellationToken);
        
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
        {
            string error = result.Error.ToLowerInvariant();
            if (error.Contains("cannot connect to the docker daemon") || 
                error.Contains("dial unix") || 
                error.Contains("no such file") ||
                error.Contains("is the docker daemon running"))
            {
                return false;
            }
        }
        
        return result.Success;
    }
}
