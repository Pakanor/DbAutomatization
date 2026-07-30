using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IAptPackageManager
{
    Task<(bool Success, string ErrorMessage)> InstallPackageAsync(
        string packageName,
        string serviceName,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string ErrorMessage)> InstallPackageAsync(
        DatabaseConfig config,
        CancellationToken cancellationToken = default);

    Task<bool> DoesPackageExistAsync(
        string packageName,
        CancellationToken cancellationToken = default);

    Task<bool> DoesPackageExistAsync(
        DatabaseConfig config,
        CancellationToken cancellationToken = default);
}
