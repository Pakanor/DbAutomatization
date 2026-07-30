using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class AptPackageManager : IAptPackageManager
{
    private readonly IProcessExecutor _process;

    public AptPackageManager(IProcessExecutor process)
    {
        _process = process;
    }

    public async Task<bool> DoesPackageExistAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        var result = await _process.ExecuteAsync(
            "which", [packageName],
            timeoutSeconds: 10, cancellationToken: cancellationToken);

        return result.Success;
    }

    public async Task<bool> DoesPackageExistAsync(
        DatabaseConfig config,
        CancellationToken cancellationToken = default)
    {
        string packageName = !string.IsNullOrEmpty(config.AptPackageName)
            ? config.AptPackageName
            : config.FriendlyName;
        return await DoesPackageExistAsync(packageName, cancellationToken);
    }

    public async Task<(bool Success, string ErrorMessage)> InstallPackageAsync(
        string packageName,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine("\n[APT] Aktualizacja repozytoriów...");
        var updateResult = await _process.ExecuteAsync(
            "sudo", ["apt", "update", "-y"],
            timeoutSeconds: 120, cancellationToken: cancellationToken);

        Console.WriteLine($"[APT] Instalacja pakietu {packageName}... To może chwilę potrwać.");
        var installResult = await _process.ExecuteAsync(
            "sudo", ["apt", "install", "-y", packageName],
            timeoutSeconds: 300, cancellationToken: cancellationToken);

        if (!installResult.Success)
            return (false, installResult.Error);

        await _process.ExecuteAsync(
            "sudo", ["systemctl", "daemon-reload"],
            timeoutSeconds: 10, cancellationToken: cancellationToken);

        await _process.ExecuteAsync(
            "sudo", ["systemctl", "enable", serviceName],
            timeoutSeconds: 10, cancellationToken: cancellationToken);

        await _process.ExecuteAsync(
            "sudo", ["systemctl", "start", serviceName],
            timeoutSeconds: 30, cancellationToken: cancellationToken);

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> InstallPackageAsync(
        DatabaseConfig config,
        CancellationToken cancellationToken = default)
    {
        string packageName = !string.IsNullOrEmpty(config.AptPackageName)
            ? config.AptPackageName
            : config.FriendlyName;
        return await InstallPackageAsync(packageName, packageName.ToLowerInvariant(), cancellationToken);
    }
}
