using System.Text.Json;
using DbAutomatization.Core.Exceptions;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class DatabaseConfigRepository : IDatabaseConfigRepository
{
    private readonly string _filePath;
    private Dictionary<string, DatabaseConfig> _cache = new();

    public DatabaseConfigRepository()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            _cache = GetDefaults();
            Save();
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(_filePath);
            _cache = JsonSerializer.Deserialize<Dictionary<string, DatabaseConfig>>(jsonString)
                     ?? new Dictionary<string, DatabaseConfig>();

            MigrateLegacyTemplates();
        }
        catch (JsonException ex)
        {
            throw new DatabaseConfigurationException(
                $"Plik konfiguracyjny '{_filePath}' jest uszkodzony. Usuń go lub napraw.", ex);
        }
    }

    public void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(_cache, options);
        File.WriteAllText(_filePath, jsonString);
    }

    public Dictionary<string, DatabaseConfig> GetAll()
    {
        return new Dictionary<string, DatabaseConfig>(_cache);
    }

    public DatabaseConfig? Get(string serviceName)
    {
        string key = NormalizeKey(serviceName);
        return _cache.TryGetValue(key, out var config) ? config : null;
    }

    public void Register(string serviceName, DatabaseConfig config)
    {
        string key = NormalizeKey(serviceName);
        if (!_cache.ContainsKey(key))
        {
            _cache[key] = config;
            Save();
        }
    }

    public void Unregister(string serviceName)
    {
        string key = NormalizeKey(serviceName);
        if (_cache.Remove(key))
            Save();
    }

    private void MigrateLegacyTemplates()
    {
        bool changed = false;

        foreach (var key in _cache.Keys)
        {
            var config = _cache[key];
            var migrated = RemoveShellRedirect(config.BackupCommand);

            if (migrated != null)
            {
                config.BackupCommand = migrated;
                changed = true;
            }
        }

        if (changed)
            Save();
    }

    private static string[]? RemoveShellRedirect(string[] args)
    {
        if (args == null || args.Length == 0)
            return null;

        var list = new List<string>(args.Length);
        bool changed = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == ">" && i + 1 < args.Length && args[i + 1] == "{path}")
            {
                i++;
                changed = true;
                continue;
            }
            list.Add(args[i]);
        }

        return changed ? [.. list] : null;
    }

    private static string NormalizeKey(string serviceName) =>
        serviceName.ToLowerInvariant().Trim();

    private static Dictionary<string, DatabaseConfig> GetDefaults()
    {
        return new Dictionary<string, DatabaseConfig>
        {
            {
                "postgresql",
                new DatabaseConfig
                {
                    FriendlyName = "PostgreSQL",
                    CliCommand = ["sudo", "-u", "postgres", "psql"],
                    BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
                    ListDatabasesCommand = ["sudo", "-u", "postgres", "psql", "-t", "-A", "-c", "SELECT datname FROM pg_database WHERE datistemplate = false;"]
                }
            },
            {
                "mariadb",
                new DatabaseConfig
                {
                    FriendlyName = "MariaDB",
                    CliCommand = ["mariadb"],
                    BackupCommand = ["sudo", "mariadb-dump", "--all-databases"],
                    ListDatabasesCommand = ["sudo", "mysql", "-e", "SHOW DATABASES;", "-s", "--N"]
                }
            },
            {
                "mongod",
                new DatabaseConfig
                {
                    FriendlyName = "MongoDB",
                    CliCommand = ["mongosh"],
                    BackupCommand = ["mongodump", "--archive={path}"],
                    ListDatabasesCommand = []
                }
            },
            {
                "redis-server",
                new DatabaseConfig
                {
                    FriendlyName = "Redis",
                    CliCommand = ["redis-cli"],
                    BackupCommand = ["/bin/bash", "-c", "redis-cli SAVE && sudo cp /var/lib/redis/dump.rdb {path}"],
                    ListDatabasesCommand = []
                }
            }
        };
    }
}
