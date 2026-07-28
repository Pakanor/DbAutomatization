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
        if (config == null || string.IsNullOrWhiteSpace(config.ListDatabasesCommand))
            return new List<string>();

        bool needsShell = config.ListDatabasesCommand.Contains('>')
                       || config.ListDatabasesCommand.Contains('|')
                       || config.ListDatabasesCommand.Contains('&');

        ProcessResult result;
        if (needsShell)
        {
            result = await _process.ExecuteAsync(
                "/bin/bash", ["-c", config.ListDatabasesCommand],
                timeoutSeconds: 30,
                cancellationToken: cancellationToken);
        }
        else
        {
            var (fileName, args) = CommandParser.Parse(config.ListDatabasesCommand);
            result = await _process.ExecuteAsync(
                fileName, args,
                timeoutSeconds: 30,
                cancellationToken: cancellationToken);
        }

        return result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(db => db.Trim())
            .Where(db => !string.IsNullOrEmpty(db))
            .ToList();
    }

    public async Task OpenDatabaseCliAsync(string serviceName, string cliCommand, CancellationToken cancellationToken = default)
    {
        string finalCommand = string.IsNullOrWhiteSpace(cliCommand) ? serviceName : cliCommand;

        var (fileName, args) = CommandParser.Parse(finalCommand);
        await _process.ExecuteInteractiveAsync(fileName, args, cancellationToken);
    }
}
