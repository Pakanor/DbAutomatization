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

    public async Task<List<string>> GetDatabasesAsync(string rawListCommand)
        {
            
            if (string.IsNullOrWhiteSpace(rawListCommand)) 
                return new List<string>();

            var output = await _bash.ExecuteAsync(rawListCommand);
            
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(db => db.Trim())
                        .Where(db => !string.IsNullOrEmpty(db))
                        .ToList();
        }

    public async Task<string> CreateBackupAsync(string serviceName, string rawBackupCommand)
        {
            if (string.IsNullOrWhiteSpace(rawBackupCommand))
            {
                return "Błąd: Brak zdefiniowanej komendy backupu dla tej bazy.";
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            string fileName = $"backup_{serviceName}_{timestamp}.bak";
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

            string finalCommand = rawBackupCommand.Replace("{path}", fullPath);

            var output = await _bash.ExecuteAsync(finalCommand);

            if (output.Contains("error") || output.Contains("failed"))
            {
                return $"[X] Coś poszło nie tak podczas backupu:\n{output}";
            }

            return $"[V] Backup wykonany pomyślnie!\nPlik zapisano w: {fullPath}";
        }

    
}