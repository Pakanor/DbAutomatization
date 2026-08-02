using DbAutomatization.Core.Models;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class EngineTemplatesTests
{
    [Fact]
    public void Systemd_Postgresql_ReturnsFullConfig()
    {
        var config = EngineTemplates.Systemd("postgresql");

        config.EngineType.Should().Be("systemd");
        config.FriendlyName.Should().Be("PostgreSQL");
        config.CliCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql"]);
        config.BackupCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "pg_dumpall"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"]);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"]);
    }

    [Fact]
    public void Docker_Postgresql_ReturnsDockerExecCommands()
    {
        var config = EngineTemplates.Docker("postgresql");

        config.EngineType.Should().Be("docker");
        config.FriendlyName.Should().Be("PostgreSQL (Docker)");
        config.CliCommand.Should().BeEquivalentTo(["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"]);
        config.BackupCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "pg_dump", "-U", "postgres", "-d", "{db}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-d", "{db}"]);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-l", "-t", "-A"]);
    }

    [Fact]
    public void Docker_Redis_ReturnsDockerCpCommands()
    {
        var config = EngineTemplates.Docker("redis");

        config.EngineType.Should().Be("docker");
        config.PreBackupCommand.Should().BeEquivalentTo(["docker", "exec", "-i", "{serviceName}", "redis-cli", "SAVE"]);
        config.BackupCommand.Should().BeEquivalentTo(["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"]);
        config.ListDatabasesCommand.Should().BeEmpty();
    }

    [Fact]
    public void Systemd_Influxdb_SetsOutputColumnIndex()
    {
        var config = EngineTemplates.Systemd("influxdb");

        config.OutputColumnIndex.Should().Be(1);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["influx", "bucket", "list", "--hide-headers"]);
    }

    [Fact]
    public void UnknownEngine_ReturnsMinimalConfig()
    {
        var systemd = EngineTemplates.Systemd("unknown");
        systemd.EngineType.Should().Be("systemd");
        systemd.BackupCommand.Should().BeEmpty();

        var docker = EngineTemplates.Docker("unknown");
        docker.EngineType.Should().Be("docker");
        docker.BackupCommand.Should().BeEmpty();
    }
}
