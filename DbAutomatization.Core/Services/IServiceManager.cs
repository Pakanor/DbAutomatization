namespace DbAutomatization.Core.Services
{
    public interface IServiceManager
    {
        Task StartServiceAsync(string serviceName);
        Task StopServiceAsync(string serviceName);
        Task RestartServiceAsync(string serviceName);
        Task<List<string>> GetDatabasesAsync(string serviceName);
        Task OpenDatabaseCliAsync(string serviceName, string customCliCommand);
        Task<string> CreateBackupAsync(string serviceName, string rawBackupCommand);


    }
}