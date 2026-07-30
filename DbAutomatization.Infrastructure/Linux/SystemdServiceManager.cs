using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class SystemdServiceManager : IServiceManager
{
    private readonly IProcessExecutor _process;
    private readonly IDatabaseConfigRepository _configRepo;

    public SystemdServiceManager(IProcessExecutor process, IDatabaseConfigRepository configRepo)
    {
        _process = process;
        _configRepo = configRepo;
    }

    public async Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await _process.ExecuteAsync(
            "sudo", ["systemctl", "start", serviceName],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
    }

    public async Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await _process.ExecuteAsync(
            "sudo", ["systemctl", "stop", serviceName],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
    }

    public async Task RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        await _process.ExecuteAsync(
            "sudo", ["systemctl", "restart", serviceName],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
    }

    public async Task<List<string>> GetDatabasesAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var config = _configRepo.Get(serviceName);
        if (config == null || config.ListDatabasesCommand == null || config.ListDatabasesCommand.Length == 0)
            return new List<string>();

        string[] commandArgs = config.ListDatabasesCommand;

        ProcessResult result = await _process.ExecuteAsync(
            commandArgs[0], commandArgs[1..],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);

        int col = config.OutputColumnIndex;
        var databases = new List<string>();
        foreach (string line in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            string[] parts = trimmed.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            string name = col > 0 && parts.Length > col ? parts[col] : parts[0];

            if (!string.IsNullOrEmpty(name))
                databases.Add(name);
        }
        return databases;
    }

    public async Task OpenDatabaseCliAsync(string serviceName, string[] cliCommand, CancellationToken cancellationToken = default)
    {
        if (cliCommand == null || cliCommand.Length == 0)
            cliCommand = [serviceName];

        await _process.ExecuteInteractiveAsync(cliCommand[0], cliCommand[1..], cancellationToken);
    }
}
