using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdServiceManager : IServiceManager
{
    private readonly IBashExecutor _bash;

    public SystemdServiceManager(IBashExecutor bash)
    {
        _bash = bash;
    }

    public async Task StartServiceAsync(string serviceName)
    {
        await _bash.ExecuteAsync($"sudo systemctl start {serviceName}");
    }

    public async Task StopServiceAsync(string serviceName)
    {
        await _bash.ExecuteAsync($"sudo systemctl stop {serviceName}");
    }
    public async Task RestartServiceAsync(string serviceName)
        {
            await _bash.ExecuteAsync($"sudo systemctl restart {serviceName}");
            
        }


    private async Task<bool> IsActiveAsync(string serviceName)
    {
        var status = await _bash.ExecuteAsync($"systemctl is-active {serviceName}");
        return status.Trim() == "active";
    }

    public async Task OpenDatabaseCliAsync(string serviceName, string customCliCommand)
    {
        string finalCommand = string.IsNullOrWhiteSpace(customCliCommand) 
            ? serviceName 
            : customCliCommand;

        await _bash.ExecuteInteractiveAsync(finalCommand);
    }

    
}