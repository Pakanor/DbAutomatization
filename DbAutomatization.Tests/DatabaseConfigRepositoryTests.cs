using System.Text.Json;
using DbAutomatization.Core.Exceptions;
using DbAutomatization.Core.Models;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class DatabaseConfigRepositoryTests
{
    [Fact]
    public void CorruptedJson_ThrowsDatabaseConfigurationException()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "databases.json");
        File.WriteAllText(filePath, "{ this is not valid json }");

        try
        {
            var act = () => CreateRepo(tempDir);
            act.Should().Throw<DatabaseConfigurationException>()
                .WithMessage($"*{filePath}*");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void MissingFile_CreatesDefaultsWithArrays()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = CreateRepo(tempDir);
            var all = repo.GetAll();

            all.Should().ContainKey("postgresql");
            all["postgresql"].BackupCommand.Should().BeEquivalentTo(
                ["sudo", "-u", "postgres", "pg_dumpall"]);
            all["mariadb"].BackupCommand.Should().BeEquivalentTo(
                ["sudo", "mariadb-dump", "--all-databases"]);
            all["redis-server"].PreBackupCommand.Should().BeEquivalentTo(["redis-cli", "SAVE"]);
            all["redis-server"].BackupCommand.Should().BeEquivalentTo(["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"]);
            all.Should().ContainKey("influxdb");
            all["influxdb"].FriendlyName.Should().Be("InfluxDB");
            all["influxdb"].ListDatabasesCommand.Should().BeEquivalentTo(["influx", "bucket", "list", "--hide-headers"]);
            all["influxdb"].OutputColumnIndex.Should().Be(1);
            all.Should().ContainKey("mongod");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Defaults_ContainDockerTemplatesWithDockerExecCommands()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var repo = CreateRepo(tempDir);
            var all = repo.GetAll();

            all.Should().ContainKey("postgres-main");
            all["postgres-main"].EngineType.Should().Be("docker");
            all["postgres-main"].BackupCommand.Should().BeEquivalentTo(
                ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"]);
            all["postgres-main"].CliCommand.Should().BeEquivalentTo(
                ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"]);
            all["postgres-main"].RestoreCommand.Should().BeEquivalentTo(
                ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres"]);

            all.Should().ContainKey("mariadb-main");
            all["mariadb-main"].EngineType.Should().Be("docker");
            all["mariadb-main"].BackupCommand.Should().BeEquivalentTo(
                ["docker", "exec", "-i", "{serviceName}", "mariadb-dump", "--all-databases"]);

            all.Should().ContainKey("redis-main");
            all["redis-main"].EngineType.Should().Be("docker");
            all["redis-main"].BackupCommand.Should().BeEquivalentTo(
                ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"]);

            all.Should().ContainKey("mongod-main");
            all["mongod-main"].EngineType.Should().Be("docker");
            all.Should().ContainKey("influxdb-main");
            all["influxdb-main"].EngineType.Should().Be("docker");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LegacyRedirectOperator_GetsCleanedOnLoad()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "databases.json");

        var legacy = new Dictionary<string, DatabaseConfig>
        {
            ["postgresql"] = new()
            {
                FriendlyName = "PostgreSQL",
                CliCommand = ["sudo", "-u", "postgres", "psql"],
                BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall", ">", "{path}"],
                ListDatabasesCommand = ["psql", "-c", "SELECT 1"]
            }
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var repo = CreateRepo(tempDir);
            var config = repo.Get("postgresql");

            config.Should().NotBeNull();
            config!.BackupCommand.Should().NotContain(">");
            config.BackupCommand.Should().NotContain("{path}");
            config.BackupCommand.Should().BeEquivalentTo(
                ["sudo", "-u", "postgres", "pg_dumpall"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LegacyConfig_MissingSingleDbBackup_FilledFromDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "databases.json");

        var legacy = new Dictionary<string, DatabaseConfig>
        {
            ["postgresql"] = new()
            {
                FriendlyName = "PostgreSQL",
                CliCommand = ["sudo", "-u", "postgres", "psql"],
                BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
                ListDatabasesCommand = ["psql", "-c", "SELECT 1"]
            }
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var repo = CreateRepo(tempDir);
            var config = repo.Get("postgresql");

            config.Should().NotBeNull();
            config!.SingleDatabaseBackupCommand.Should().BeEquivalentTo(
                ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"]);
            config.RestoreCommand.Should().BeEquivalentTo(
                ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"]);
            config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(
                ["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LegacyConfig_MariaDb_MissingFields_FilledFromDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "databases.json");

        var legacy = new Dictionary<string, DatabaseConfig>
        {
            ["mariadb"] = new()
            {
                FriendlyName = "MariaDB",
                BackupCommand = ["sudo", "mariadb-dump", "--all-databases"]
            }
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var repo = CreateRepo(tempDir);
            var config = repo.Get("mariadb");

            config.Should().NotBeNull();
            config!.SingleDatabaseBackupCommand.Should().BeEquivalentTo(
                ["sudo", "mariadb-dump", "{db}"]);
            config.RestoreCommand.Should().BeEquivalentTo(
                ["sudo", "mariadb"]);
            config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(
                ["sudo", "mariadb", "{db}"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void LegacyConfig_ExistingFields_NotOverwritten()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "databases.json");

        var legacy = new Dictionary<string, DatabaseConfig>
        {
            ["postgresql"] = new()
            {
                FriendlyName = "PostgreSQL",
                CliCommand = ["custom", "cli"],
                BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
                SingleDatabaseBackupCommand = ["custom", "single", "backup"]
            }
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var repo = CreateRepo(tempDir);
            var config = repo.Get("postgresql");

            config.Should().NotBeNull();
            config!.CliCommand.Should().BeEquivalentTo(["custom", "cli"]);
            config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["custom", "single", "backup"]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static TestableConfigRepo CreateRepo(string tempDir)
    {
        return new TestableConfigRepo(Path.Combine(tempDir, "databases.json"));
    }

    private class TestableConfigRepo : DatabaseConfigRepository
    {
        public TestableConfigRepo(string filePath) : base(filePath) { }
    }
}
