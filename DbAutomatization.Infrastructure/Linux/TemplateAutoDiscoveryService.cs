using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class TemplateAutoDiscoveryService : ITemplateAutoDiscoveryService
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;

    private static readonly (string ServiceName, DatabaseConfig Config)[] Templates =
    [
        ("postgresql", new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            AptPackageName = "postgresql",
            CliCommand = ["sudo", "-u", "postgres", "psql"],
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
            SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"],
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"],
            SingleDatabaseRestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"],
            ListDatabasesCommand = ["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"]
        }),
        ("mariadb", new DatabaseConfig
        {
            FriendlyName = "MariaDB",
            AptPackageName = "mariadb-server",
            CliCommand = ["mariadb"],
            BackupCommand = ["sudo", "mariadb-dump", "--all-databases"],
            SingleDatabaseBackupCommand = ["sudo", "mariadb-dump", "{db}"],
            RestoreCommand = ["sudo", "mariadb"],
            SingleDatabaseRestoreCommand = ["sudo", "mariadb", "{db}"],
            ListDatabasesCommand = ["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"]
        }),
        ("mongod", new DatabaseConfig
        {
            FriendlyName = "MongoDB",
            AptPackageName = "mongodb-org",
            CliCommand = ["mongosh"],
            BackupCommand = ["mongodump", "--archive={path}"],
            SingleDatabaseBackupCommand = ["mongodump", "--db", "{db}", "--archive={path}"],
            RestoreCommand = ["mongorestore", "--archive={path}"],
            SingleDatabaseRestoreCommand = ["mongorestore", "--nsInclude", "{db}.*", "--archive={path}"],
            ListDatabasesCommand = ["mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"]
        }),
        ("redis-server", new DatabaseConfig
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
        }),
        ("influxdb", new DatabaseConfig
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
        })
    ];

    public TemplateAutoDiscoveryService(
        IDatabaseConfigRepository configRepo,
        IProcessExecutor process)
    {
        _configRepo = configRepo;
        _process = process;
    }

    public async Task AutoDiscoverAsync(CancellationToken cancellationToken = default)
    {
        var existing = _configRepo.GetAll();

        foreach (var (serviceName, config) in Templates)
        {
            if (existing.ContainsKey(serviceName))
                continue;

            var result = await _process.ExecuteAsync(
                "systemctl", ["is-enabled", serviceName],
                timeoutSeconds: 5, cancellationToken: cancellationToken);

            if (result.Success)
            {
                _configRepo.Register(serviceName, config);
                Console.WriteLine($"[Auto-Discovery] Wykryto {config.FriendlyName} ({serviceName}) — dodano do monitorowania.");
            }
        }
    }
}
