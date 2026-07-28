using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IDatabaseConfigRepository
{
    Dictionary<string, DatabaseConfig> GetAll();
    DatabaseConfig? Get(string serviceName);
    void Register(string serviceName, DatabaseConfig config);
    void Unregister(string serviceName);
}
