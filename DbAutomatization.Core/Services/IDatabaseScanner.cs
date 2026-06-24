using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services
{
    public interface IDatabaseScanner
    {
        Task<List<DetectedDatabase>> ScanForDatabasesAsync();
        void RegisterDatabase(string serviceName, string friendlyName);
        void UnregisterDatabase(string serviceName);
        Task<(bool Success, string ErrorMessage)> InstallServiceViaAptAsync(string packageName, string serviceName);
        Task<bool> DoesServiceExistAsync(string serviceName);


    }
}
