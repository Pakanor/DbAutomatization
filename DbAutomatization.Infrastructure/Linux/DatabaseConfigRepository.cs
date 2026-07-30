using System.Text.Json;
using DbAutomatization.Core.Exceptions;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class DatabaseConfigRepository : IDatabaseConfigRepository
{
    private readonly string _filePath;
    private Dictionary<string, DatabaseConfig> _cache = new();

    public DatabaseConfigRepository() : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json"))
    {
    }

    internal DatabaseConfigRepository(string filePath)
    {
        _filePath = filePath;
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

    public void Reload()
    {
        Load();
    }

    private void MigrateLegacyTemplates()
    {
        bool changed = false;
        var defaults = GetDefaults();

        foreach (var key in _cache.Keys)
        {
            var config = _cache[key];

            var migrated = RemoveShellRedirect(config.BackupCommand);
            if (migrated != null)
            {
                config.BackupCommand = migrated;
                changed = true;
            }

            if (MigrateRedisShellCommand(config))
                changed = true;

            if (MigrateListDatabasesCommand(config, key))
                changed = true;

            if (defaults.TryGetValue(key, out var defaultConfig))
            {
                changed |= MergeMissingFields(config, defaultConfig);
            }
        }

        if (changed)
            Save();
    }

    private static bool MigrateListDatabasesCommand(DatabaseConfig config, string key)
    {
        bool changed = false;
        var defaults = GetDefaults();

        if (key is "postgresql" or "mariadb")
        {
            if (defaults.TryGetValue(key, out var defaultCfg) && defaultCfg.ListDatabasesCommand is { Length: > 0 })
            {
                if (config.ListDatabasesCommand is { Length: > 0 })
                {
                    bool isOldFormat = config.ListDatabasesCommand.Any(a => a is "-c" or "-e" && a.Length > 2);
                    if (isOldFormat || config.ListDatabasesCommand.Length != defaultCfg.ListDatabasesCommand.Length)
                    {
                        config.ListDatabasesCommand = defaultCfg.ListDatabasesCommand;
                        changed = true;
                    }
                }
            }
        }

        if (key is "postgresql" or "mariadb")
        {
            if (defaults.TryGetValue(key, out var defaultCfg))
            {
                bool hasFileFlag = config.RestoreCommand is { Length: > 0 } &&
                    (config.RestoreCommand.Contains("-f") || config.RestoreCommand.Contains("source"));
                bool hasSingleFileFlag = config.SingleDatabaseRestoreCommand is { Length: > 0 } &&
                    (config.SingleDatabaseRestoreCommand.Contains("-f") || config.SingleDatabaseRestoreCommand.Contains("source"));

                if (hasFileFlag)
                    config.RestoreCommand = defaultCfg.RestoreCommand;
                if (hasSingleFileFlag)
                    config.SingleDatabaseRestoreCommand = defaultCfg.SingleDatabaseRestoreCommand;
                changed |= hasFileFlag | hasSingleFileFlag;
            }
        }

        if (key == "mariadb" && config.ListDatabasesCommand is { Length: > 0 } &&
            config.ListDatabasesCommand.Contains("mysqlshow"))
        {
            config.ListDatabasesCommand = ["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"];
            changed = true;
        }

        return changed;
    }

    private static bool MergeMissingFields(DatabaseConfig target, DatabaseConfig source)
    {
        bool changed = false;

        if (string.IsNullOrEmpty(target.AptPackageName) && !string.IsNullOrEmpty(source.AptPackageName))
        {
            target.AptPackageName = source.AptPackageName;
            changed = true;
        }
        if (target.CliCommand is not { Length: > 0 } && source.CliCommand is { Length: > 0 })
        {
            target.CliCommand = source.CliCommand;
            changed = true;
        }
        if (target.BackupCommand is not { Length: > 0 } && source.BackupCommand is { Length: > 0 })
        {
            target.BackupCommand = source.BackupCommand;
            changed = true;
        }
        if (target.SingleDatabaseBackupCommand is not { Length: > 0 } && source.SingleDatabaseBackupCommand is { Length: > 0 })
        {
            target.SingleDatabaseBackupCommand = source.SingleDatabaseBackupCommand;
            changed = true;
        }
        if (target.RestoreCommand is not { Length: > 0 } && source.RestoreCommand is { Length: > 0 })
        {
            target.RestoreCommand = source.RestoreCommand;
            changed = true;
        }
        if (target.SingleDatabaseRestoreCommand is not { Length: > 0 } && source.SingleDatabaseRestoreCommand is { Length: > 0 })
        {
            target.SingleDatabaseRestoreCommand = source.SingleDatabaseRestoreCommand;
            changed = true;
        }
        if (target.ListDatabasesCommand is not { Length: > 0 } && source.ListDatabasesCommand is { Length: > 0 })
        {
            target.ListDatabasesCommand = source.ListDatabasesCommand;
            changed = true;
        }
        if (target.PreBackupCommand is not { Length: > 0 } && source.PreBackupCommand is { Length: > 0 })
        {
            target.PreBackupCommand = source.PreBackupCommand;
            changed = true;
        }

        return changed;
    }

    private static bool MigrateRedisShellCommand(DatabaseConfig config)
    {
        if (config.BackupCommand is not ["/bin/bash", "-c", var shellCommand])
            return false;

        if (!shellCommand.Contains("redis-cli SAVE") || !shellCommand.Contains("&&"))
            return false;

        string cpPart = shellCommand[(shellCommand.IndexOf("&&") + 2)..].Trim();

        config.PreBackupCommand = ["redis-cli", "SAVE"];
        config.BackupCommand = cpPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return true;
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
                    AptPackageName = "postgresql",
                    CliCommand = ["sudo", "-u", "postgres", "psql"],
                    BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
                    SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"],
                    RestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"],
                    SingleDatabaseRestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"],
                    ListDatabasesCommand = ["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"]
                }
            },
            {
                "mariadb",
                new DatabaseConfig
                {
                    FriendlyName = "MariaDB",
                    AptPackageName = "mariadb-server",
                    CliCommand = ["mariadb"],
                    BackupCommand = ["sudo", "mariadb-dump", "--all-databases"],
                    SingleDatabaseBackupCommand = ["sudo", "mariadb-dump", "{db}"],
                    RestoreCommand = ["sudo", "mariadb"],
                    SingleDatabaseRestoreCommand = ["sudo", "mariadb", "{db}"],
                    ListDatabasesCommand = ["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"]
                }
            },
            {
                "mongod",
                new DatabaseConfig
                {
                    FriendlyName = "MongoDB",
                    AptPackageName = "mongodb-org",
                    CliCommand = ["mongosh"],
                    BackupCommand = ["mongodump", "--archive={path}"],
                    SingleDatabaseBackupCommand = ["mongodump", "--db", "{db}", "--archive={path}"],
                    RestoreCommand = ["mongorestore", "--archive={path}"],
                    SingleDatabaseRestoreCommand = ["mongorestore", "--nsInclude", "{db}.*", "--archive={path}"],
                    ListDatabasesCommand = ["mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"]
                }
            },
            {
                "redis-server",
                new DatabaseConfig
                {
                    FriendlyName = "Redis",
                    AptPackageName = "redis-server",
                    CliCommand = ["redis-cli"],
                    PreBackupCommand = ["redis-cli", "SAVE"],
                    BackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"],
                    SingleDatabaseBackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"],
                    RestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"],
                    SingleDatabaseRestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"],
                    ListDatabasesCommand = []
                }
            },
            {
                "influxdb",
                new DatabaseConfig
                {
                    FriendlyName = "InfluxDB",
                    AptPackageName = "influxdb",
                    CliCommand = ["influx"],
                    BackupCommand = ["influx", "backup", "{path}"],
                    SingleDatabaseBackupCommand = ["influx", "backup", "-database", "{db}", "{path}"],
                    RestoreCommand = ["influx", "restore", "{path}"],
                    SingleDatabaseRestoreCommand = ["influx", "restore", "-database", "{db}", "{path}"],
                    ListDatabasesCommand = ["influx", "bucket", "list", "--hide-headers"],
                    OutputColumnIndex = 1
                }
            }
        };
    }
}
