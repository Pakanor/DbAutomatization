using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class DockerContainerDiscoveryService : IDockerContainerDiscoveryService
{
    private readonly IProcessExecutor _process;

    private static readonly (string Family, string Title, string[] Keywords, Func<string, IReadOnlyDictionary<string, string>, DatabaseConfig> Builder)[] FamilyBuilders =
    [
        ("postgresql", "PostgreSQL", ["postgres"], BuildPostgresConfig),
        ("mariadb", "MariaDB", ["mariadb", "mysql"], BuildMariadbConfig),
        ("mongodb", "MongoDB", ["mongo"], BuildMongoConfig),
        ("redis", "Redis", ["redis"], BuildRedisConfig),
        ("influxdb", "InfluxDB", ["influx"], BuildInfluxConfig)
    ];

    public DockerContainerDiscoveryService(IProcessExecutor process)
    {
        _process = process;
    }

    public async Task<List<DockerContainerInfo>> DiscoverDatabasesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _process.ExecuteAsync(
            "docker", ["ps", "-a", "--format", "{{.Names}}\t{{.Image}}\t{{.State}}"],
            timeoutSeconds: 15,
            cancellationToken: cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            return [];

        var containers = new List<DockerContainerInfo>();

        foreach (string line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            string name = parts[0].Trim();
            string image = parts[1].Trim();
            bool isRunning = parts.Length > 2 && parts[2].Trim() == "running";

            var (family, title, builder) = DetectFamily(image);
            if (family == null)
                continue;

            var env = await ReadContainerEnvAsync(name, cancellationToken);
            var config = builder(name, env);

            containers.Add(new DockerContainerInfo
            {
                Name = name,
                Image = image,
                Family = family,
                FamilyTitle = title,
                IsRunning = isRunning,
                Config = config
            });
        }

        return containers;
    }

    private static (string? Family, string Title, Func<string, IReadOnlyDictionary<string, string>, DatabaseConfig> Builder) DetectFamily(string image)
    {
        string lower = image.ToLowerInvariant();

        foreach (var family in FamilyBuilders)
        {
            if (family.Keywords.Any(keyword => lower.Contains(keyword)))
                return (family.Family, family.Title, family.Builder);
        }

        return (null, string.Empty, null!);
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadContainerEnvAsync(
        string containerName,
        CancellationToken cancellationToken)
    {
        var envResult = await _process.ExecuteAsync(
            "docker", ["inspect", "-f", "{{range .Config.Env}}{{println .}}{{end}}", containerName],
            timeoutSeconds: 10,
            cancellationToken: cancellationToken);

        var env = new Dictionary<string, string>();

        if (!envResult.Success)
            return env;

        foreach (string line in envResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int eqIndex = line.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            string key = line[..eqIndex];
            string value = line[(eqIndex + 1)..];
            env[key] = value;
        }

        return env;
    }

    private static DatabaseConfig BuildPostgresConfig(string name, IReadOnlyDictionary<string, string> env)
    {
        string user = GetEnv(env, "POSTGRES_USER", "postgres");
        string? db = GetEnvOrNull(env, "POSTGRES_DB");

        var cliArgs = new List<string> { "-U", user };
        if (!string.IsNullOrEmpty(db))
        {
            cliArgs.Add("-d");
            cliArgs.Add(db);
        }

        return new DatabaseConfig
        {
            EngineType = "docker",
            CliCommand = DockerExecCommand("-it", "psql", [.. cliArgs]),
            BackupCommand = DockerExecCommand("-i", "pg_dumpall", "-U", user),
            SingleDatabaseBackupCommand = DockerExecCommand("-i", "pg_dump", "-U", user, "-d", "{db}"),
            RestoreCommand = DockerExecCommand("-i", "psql", "-U", user, "-d", "postgres"),
            SingleDatabaseRestoreCommand = DockerExecCommand("-i", "psql", "-U", user, "-d", "{db}"),
            ListDatabasesCommand = DockerExecCommand("-i", "psql", "-U", user, "-d", "postgres", "-l", "-t", "-A")
        };
    }

    private static DatabaseConfig BuildMariadbConfig(string name, IReadOnlyDictionary<string, string> env)
    {
        string user = GetEnv(env, "MYSQL_USER", "root");
        string? pass = GetEnvOrNull(env, "MYSQL_PASSWORD") ?? GetEnvOrNull(env, "MYSQL_ROOT_PASSWORD");

        return new DatabaseConfig
        {
            EngineType = "docker",
            CliCommand = MariaDbCommand("-it", "mariadb", user, pass),
            BackupCommand = MariaDbCommand("-i", "mariadb-dump", user, pass, "--all-databases"),
            SingleDatabaseBackupCommand = MariaDbCommand("-i", "mariadb-dump", user, pass, "{db}"),
            RestoreCommand = MariaDbCommand("-i", "mariadb", user, pass),
            SingleDatabaseRestoreCommand = MariaDbCommand("-i", "mariadb", user, pass, "{db}"),
            ListDatabasesCommand = MariaDbCommand("-i", "mariadb", user, pass, "-BN", "-e", "SHOW DATABASES;")
        };
    }

    private static DatabaseConfig BuildMongoConfig(string name, IReadOnlyDictionary<string, string> env)
    {
        return new DatabaseConfig
        {
            EngineType = "docker",
            CliCommand = DockerExecCommand("-it", "mongosh"),
            BackupCommand = DockerExecCommand("-i", "mongodump", "--archive"),
            SingleDatabaseBackupCommand = DockerExecCommand("-i", "mongodump", "--db", "{db}", "--archive"),
            RestoreCommand = DockerExecCommand("-i", "mongorestore", "--archive"),
            SingleDatabaseRestoreCommand = DockerExecCommand("-i", "mongorestore", "--nsInclude", "{db}.*", "--archive"),
            ListDatabasesCommand = DockerExecCommand("-i", "mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))")
        };
    }

    private static DatabaseConfig BuildRedisConfig(string name, IReadOnlyDictionary<string, string> env)
    {
        return new DatabaseConfig
        {
            EngineType = "docker",
            CliCommand = DockerExecCommand("-it", "redis-cli"),
            PreBackupCommand = DockerExecCommand("-i", "redis-cli", "SAVE"),
            BackupCommand = ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"],
            SingleDatabaseBackupCommand = ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"],
            RestoreCommand = ["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"],
            SingleDatabaseRestoreCommand = ["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"],
            ListDatabasesCommand = []
        };
    }

    private static DatabaseConfig BuildInfluxConfig(string name, IReadOnlyDictionary<string, string> env)
    {
        return new DatabaseConfig
        {
            EngineType = "docker",
            CliCommand = DockerExecCommand("-it", "influx"),
            BackupCommand = DockerExecCommand("-i", "influx", "backup", "{path}"),
            SingleDatabaseBackupCommand = DockerExecCommand("-i", "influx", "backup", "-database", "{db}", "{path}"),
            RestoreCommand = DockerExecCommand("-i", "influx", "restore", "{path}"),
            SingleDatabaseRestoreCommand = DockerExecCommand("-i", "influx", "restore", "-database", "{db}", "{path}"),
            ListDatabasesCommand = DockerExecCommand("-i", "influx", "bucket", "list", "--hide-headers"),
            OutputColumnIndex = 1
        };
    }

    private static string[] DockerExecCommand(string execMode, string binary, params string[] args)
        => ["docker", "exec", execMode, "{serviceName}", binary, .. args];

    private static string[] MariaDbCommand(string execMode, string binary, string user, string? pass, params string[] tail)
    {
        var cmd = new List<string> { "docker", "exec", execMode, "{serviceName}", binary, "-u", user };
        if (pass != null)
            cmd.Add($"-p{pass}");
        cmd.AddRange(tail);
        return [.. cmd];
    }

    private static string GetEnv(IReadOnlyDictionary<string, string> env, string key, string fallback)
        => env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string? GetEnvOrNull(IReadOnlyDictionary<string, string> env, string key)
        => env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
