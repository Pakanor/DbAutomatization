namespace DbAutomatization.Core.Services
{
    public interface IServiceManager
    {
        Task<bool> StartServiceAsync(string serviceName);
        Task<bool> StopServiceAsync(string serviceName);
        Task<bool> RestartServiceAsync(string serviceName);

    }
}