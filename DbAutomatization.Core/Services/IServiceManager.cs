namespace DbAutomatization.Core.Services
{
    public interface IServiceManager
    {
        Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
        Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);
        Task RestartServiceAsync(string serviceName, CancellationToken cancellationToken = default);
        Task<List<string>> GetDatabasesAsync(string serviceName, CancellationToken cancellationToken = default);
        Task OpenDatabaseCliAsync(string serviceName, string[] cliCommand, CancellationToken cancellationToken = default);
    }
}
