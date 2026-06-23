using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdServiceManager : IServiceManager
{
    private readonly IBashExecutor _bash;

    public SystemdServiceManager(IBashExecutor bash)
    {
        _bash = bash;
    }

    public async Task<bool> StartServiceAsync(string serviceName)
    {
        _ = await _bash.ExecuteAsync($"sudo systemctl start {serviceName}");
        return await IsActiveAsync(serviceName);
    }

    public async Task<bool> StopServiceAsync(string serviceName)
    {
        _ = await _bash.ExecuteAsync($"sudo systemctl stop {serviceName}");
        return !await IsActiveAsync(serviceName);
    }

    private async Task<bool> IsActiveAsync(string serviceName)
    {
        var status = await _bash.ExecuteAsync($"systemctl is-active {serviceName}");
        return status.Trim() == "active";
    }
}