using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IDockerContainerDiscoveryService
{
    Task<List<DockerContainerInfo>> DiscoverDatabasesAsync(CancellationToken cancellationToken = default);
}
