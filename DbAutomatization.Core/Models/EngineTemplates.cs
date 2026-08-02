namespace DbAutomatization.Core.Models;

public static class EngineTemplates
{
    public static DatabaseConfig Systemd(string engine) => engine switch
    {
        "postgresql" => new DatabaseConfig
        {
            EngineType = "systemd",
            FriendlyName = "PostgreSQL",
            CliCommand = ["sudo", "-u", "postgres", "psql"],
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
            SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"],
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"],
            SingleDatabaseRestoreCommand = ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"],
            ListDatabasesCommand = ["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"]
        },
        "mariadb" => new DatabaseConfig
        {
            EngineType = "systemd",
            FriendlyName = "MariaDB",
            CliCommand = ["mariadb"],
            BackupCommand = ["sudo", "mariadb-dump", "--all-databases"],
            SingleDatabaseBackupCommand = ["sudo", "mariadb-dump", "{db}"],
            RestoreCommand = ["sudo", "mariadb"],
            SingleDatabaseRestoreCommand = ["sudo", "mariadb", "{db}"],
            ListDatabasesCommand = ["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"]
        },
        "mongod" => new DatabaseConfig
        {
            EngineType = "systemd",
            FriendlyName = "MongoDB",
            CliCommand = ["mongosh"],
            BackupCommand = ["mongodump", "--archive={path}"],
            SingleDatabaseBackupCommand = ["mongodump", "--db", "{db}", "--archive={path}"],
            RestoreCommand = ["mongorestore", "--archive={path}"],
            SingleDatabaseRestoreCommand = ["mongorestore", "--nsInclude", "{db}.*", "--archive={path}"],
            ListDatabasesCommand = ["mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"]
        },
        "redis-server" => new DatabaseConfig
        {
            EngineType = "systemd",
            FriendlyName = "Redis",
            CliCommand = ["redis-cli"],
            PreBackupCommand = ["redis-cli", "SAVE"],
            BackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"],
            SingleDatabaseBackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"],
            RestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"],
            SingleDatabaseRestoreCommand = ["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"],
            ListDatabasesCommand = []
        },
        "influxdb" => new DatabaseConfig
        {
            EngineType = "systemd",
            FriendlyName = "InfluxDB",
            CliCommand = ["influx"],
            BackupCommand = ["influx", "backup", "{path}"],
            SingleDatabaseBackupCommand = ["influx", "backup", "-database", "{db}", "{path}"],
            RestoreCommand = ["influx", "restore", "{path}"],
            SingleDatabaseRestoreCommand = ["influx", "restore", "-database", "{db}", "{path}"],
            ListDatabasesCommand = ["influx", "bucket", "list", "--hide-headers"],
            OutputColumnIndex = 1
        },
        _ => new DatabaseConfig { EngineType = "systemd" }
    };

    public static DatabaseConfig Docker(string engine) => engine switch
    {
        "postgresql" => new DatabaseConfig
        {
            EngineType = "docker",
            FriendlyName = "PostgreSQL (Docker)",
            CliCommand = ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"],
            BackupCommand = ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"],
            SingleDatabaseBackupCommand = ["docker", "exec", "-i", "{serviceName}", "pg_dump", "-U", "postgres", "-d", "{db}"],
            RestoreCommand = ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres"],
            SingleDatabaseRestoreCommand = ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-d", "{db}"],
            ListDatabasesCommand = ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-l", "-t", "-A"]
        },
        "mariadb" => new DatabaseConfig
        {
            EngineType = "docker",
            FriendlyName = "MariaDB (Docker)",
            CliCommand = ["docker", "exec", "-it", "{serviceName}", "mariadb"],
            BackupCommand = ["docker", "exec", "-i", "{serviceName}", "mariadb-dump", "--all-databases"],
            SingleDatabaseBackupCommand = ["docker", "exec", "-i", "{serviceName}", "mariadb-dump", "{db}"],
            RestoreCommand = ["docker", "exec", "-i", "{serviceName}", "mariadb"],
            SingleDatabaseRestoreCommand = ["docker", "exec", "-i", "{serviceName}", "mariadb", "{db}"],
            ListDatabasesCommand = ["docker", "exec", "-i", "{serviceName}", "mariadb", "-BN", "-e", "SHOW DATABASES;"]
        },
        "mongod" => new DatabaseConfig
        {
            EngineType = "docker",
            FriendlyName = "MongoDB (Docker)",
            CliCommand = ["docker", "exec", "-it", "{serviceName}", "mongosh"],
            BackupCommand = ["docker", "exec", "-i", "{serviceName}", "mongodump", "--archive"],
            SingleDatabaseBackupCommand = ["docker", "exec", "-i", "{serviceName}", "mongodump", "--db", "{db}", "--archive"],
            RestoreCommand = ["docker", "exec", "-i", "{serviceName}", "mongorestore", "--archive"],
            SingleDatabaseRestoreCommand = ["docker", "exec", "-i", "{serviceName}", "mongorestore", "--nsInclude", "{db}.*", "--archive"],
            ListDatabasesCommand = ["docker", "exec", "-i", "{serviceName}", "mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"]
        },
        "redis" => new DatabaseConfig
        {
            EngineType = "docker",
            FriendlyName = "Redis (Docker)",
            CliCommand = ["docker", "exec", "-it", "{serviceName}", "redis-cli"],
            PreBackupCommand = ["docker", "exec", "-i", "{serviceName}", "redis-cli", "SAVE"],
            BackupCommand = ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"],
            SingleDatabaseBackupCommand = ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"],
            RestoreCommand = ["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"],
            SingleDatabaseRestoreCommand = ["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"],
            ListDatabasesCommand = []
        },
        "influxdb" => new DatabaseConfig
        {
            EngineType = "docker",
            FriendlyName = "InfluxDB (Docker)",
            CliCommand = ["docker", "exec", "-it", "{serviceName}", "influx"],
            BackupCommand = ["docker", "exec", "-i", "{serviceName}", "influx", "backup", "{path}"],
            SingleDatabaseBackupCommand = ["docker", "exec", "-i", "{serviceName}", "influx", "backup", "-database", "{db}", "{path}"],
            RestoreCommand = ["docker", "exec", "-i", "{serviceName}", "influx", "restore", "{path}"],
            SingleDatabaseRestoreCommand = ["docker", "exec", "-i", "{serviceName}", "influx", "restore", "-database", "{db}", "{path}"],
            ListDatabasesCommand = ["docker", "exec", "-i", "{serviceName}", "influx", "bucket", "list", "--hide-headers"],
            OutputColumnIndex = 1
        },
        _ => new DatabaseConfig { EngineType = "docker" }
    };
}
