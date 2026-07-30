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

    private static TestableConfigRepo CreateRepo(string tempDir)
    {
        return new TestableConfigRepo(Path.Combine(tempDir, "databases.json"));
    }

    private class TestableConfigRepo : DatabaseConfigRepository
    {
        public TestableConfigRepo(string filePath) : base(filePath) { }
    }
}
