namespace DbAutomatization.Core.Services;

public interface ITemplateAutoDiscoveryService
{
    Task AutoDiscoverAsync(CancellationToken cancellationToken = default);
}
