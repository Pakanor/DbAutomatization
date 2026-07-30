using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Cli;

public class DatabaseWizardService
{
    private readonly IDatabaseConfigRepository _configRepo;

    public DatabaseWizardService(IDatabaseConfigRepository configRepo)
    {
        _configRepo = configRepo;
    }

    public async Task RunWizardAsync()
    {
        await Task.CompletedTask;

        Console.WriteLine("\n=== Kreator dodawania nowej bazy ===");

        var template = SelectTemplate();
        if (template == null)
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        Console.Write("\nPodaj nazwę usługi systemd (np. postgresql): ");
        var serviceName = Console.ReadLine()?.ToLower().Trim();
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        Console.Write("Podaj nazwę wyświetlaną (np. PostgreSQL): ");
        var friendlyName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            Console.WriteLine("Anulowano.");
            return;
        }

        var config = BuildConfig(template.Value, serviceName, friendlyName);
        _configRepo.Register(serviceName, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[V] Baza '{friendlyName}' ({serviceName}) została dodana.");
        Console.ResetColor();
    }

    private static DatabaseTemplate? SelectTemplate()
    {
        Console.WriteLine("\n--- Wybierz szablon silnika ---");
        Console.WriteLine("[1] PostgreSQL");
        Console.WriteLine("[2] MariaDB / MySQL");
        Console.WriteLine("[3] MongoDB");
        Console.WriteLine("[4] Redis");
        Console.WriteLine("[5] InfluxDB");
        Console.WriteLine("[6] Custom (czysta konfiguracja)");
        Console.WriteLine("[0] Anuluj");
        Console.Write("\nWybór: ");

        var choice = Console.ReadLine()?.Trim();
        return choice switch
        {
            "1" => DatabaseTemplate.PostgreSQL,
            "2" => DatabaseTemplate.MariaDB,
            "3" => DatabaseTemplate.MongoDB,
            "4" => DatabaseTemplate.Redis,
            "5" => DatabaseTemplate.InfluxDB,
            "6" => DatabaseTemplate.Custom,
            _ => null
        };
    }

    private static DatabaseConfig BuildConfig(DatabaseTemplate template, string serviceName, string friendlyName)
    {
        var config = new DatabaseConfig
        {
            FriendlyName = friendlyName,
            CliCommand = [serviceName],
            BackupCommand = [],
            RestoreCommand = [],
            ListDatabasesCommand = []
        };

        switch (template)
        {
            case DatabaseTemplate.PostgreSQL:
                config.AptPackageName = "postgresql";
                config.CliCommand = ["sudo", "-u", "postgres", "psql"];
                config.BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"];
                config.SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"];
                config.RestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"];
                config.SingleDatabaseRestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"];
                config.ListDatabasesCommand = ["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"];
                break;

            case DatabaseTemplate.MariaDB:
                config.AptPackageName = "mariadb-server";
                config.CliCommand = ["mariadb"];
                config.BackupCommand = ["sudo", "mariadb-dump", "--all-databases"];
                config.SingleDatabaseBackupCommand = ["sudo", "mariadb-dump", "{db}"];
                config.RestoreCommand = ["sudo", "mariadb"];
                config.SingleDatabaseRestoreCommand = ["sudo", "mariadb", "{db}"];
                config.ListDatabasesCommand = ["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"];
                break;

            case DatabaseTemplate.MongoDB:
                config.AptPackageName = "mongodb-org";
                config.CliCommand = ["mongosh"];
                config.BackupCommand = ["mongodump", "--archive={path}"];
                config.SingleDatabaseBackupCommand = ["mongodump", "--db", "{db}", "--archive={path}"];
                config.RestoreCommand = ["mongorestore", "--archive={path}"];
                config.SingleDatabaseRestoreCommand = ["mongorestore", "--nsInclude", "{db}.*", "--archive={path}"];
                config.ListDatabasesCommand = ["mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"];
                break;

            case DatabaseTemplate.Redis:
                config.AptPackageName = "redis-server";
                config.CliCommand = ["redis-cli"];
                config.PreBackupCommand = ["redis-cli", "SAVE"];
                config.BackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"];
                config.SingleDatabaseBackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"];
                config.RestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"];
                config.SingleDatabaseRestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"];
                break;

            case DatabaseTemplate.InfluxDB:
                config.AptPackageName = "influxdb";
                config.CliCommand = ["influx"];
                config.BackupCommand = ["influx", "backup", "{path}"];
                config.SingleDatabaseBackupCommand = ["influx", "backup", "-database", "{db}", "{path}"];
                config.RestoreCommand = ["influx", "restore", "{path}"];
                config.SingleDatabaseRestoreCommand = ["influx", "restore", "-database", "{db}", "{path}"];
                config.ListDatabasesCommand = ["influx", "bucket", "list", "--hide-headers"];
                config.OutputColumnIndex = 1;
                break;

            case DatabaseTemplate.Custom:
                break;
        }

        return config;
    }

    private enum DatabaseTemplate
    {
        PostgreSQL,
        MariaDB,
        MongoDB,
        Redis,
        InfluxDB,
        Custom
    }
}
