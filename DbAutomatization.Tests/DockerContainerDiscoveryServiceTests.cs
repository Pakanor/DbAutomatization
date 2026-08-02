using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class DockerContainerDiscoveryServiceTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly DockerContainerDiscoveryService _service;

    public DockerContainerDiscoveryServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _service = new DockerContainerDiscoveryService(_processMock.Object);
    }

    [Fact]
    public async Task DiscoverDatabasesAsync_FiltersOutNonDatabaseContainers()
    {
        SetupDockerPs(
            "nextcloud-db\tmariadb:10.11\trunning\n" +
            "fitnessapp-db_exercise-1\tpostgres:15\texited\n" +
            "pgadmin4_container_open_space\tdpage/pgadmin4\trunning\n" +
            "nextcloud-app\tnextcloud:28\trunning");

        SetupInspectEnv("nextcloud-db",
            "MARIADB_VERSION=1:10.11.16\nMYSQL_USER=nextcloud\nMYSQL_PASSWORD=secret\n");
        SetupInspectEnv("fitnessapp-db_exercise-1",
            "POSTGRES_USER=fitnessapp\nPOSTGRES_DB=exercise\n");

        var containers = await _service.DiscoverDatabasesAsync();

        containers.Should().HaveCount(2);
        containers.Should().Contain(c => c.Name == "nextcloud-db");
        containers.Should().Contain(c => c.Name == "fitnessapp-db_exercise-1");
        containers.Should().NotContain(c => c.Name.Contains("pgadmin"));
        containers.Should().NotContain(c => c.Name.Contains("nextcloud-app"));
    }

    [Fact]
    public async Task DiscoverDatabasesAsync_Postgres_ReadsUserAndDatabaseFromEnv()
    {
        SetupDockerPs("fitnessapp-db_exercise-1\tpostgres:15\texited");
        SetupInspectEnv("fitnessapp-db_exercise-1",
            "POSTGRES_USER=fitnessapp\nPOSTGRES_DB=exercise\nPOSTGRES_PASSWORD=secret\n");

        var containers = await _service.DiscoverDatabasesAsync();

        var container = containers.Single();
        container.Family.Should().Be("postgresql");
        container.IsRunning.Should().BeFalse();

        container.Config.EngineType.Should().Be("docker");
        container.Config.CliCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "fitnessapp", "-d", "exercise"]);
        container.Config.BackupCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "fitnessapp"]);
        container.Config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "pg_dump", "-U", "fitnessapp", "-d", "{db}"]);
        container.Config.RestoreCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "fitnessapp", "-d", "postgres"]);
        container.Config.ListDatabasesCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "fitnessapp", "-d", "postgres", "-l", "-t", "-A"]);
    }

    [Fact]
    public async Task DiscoverDatabasesAsync_Postgres_WithoutEnvDb_FallsBackToPostgresUser()
    {
        SetupDockerPs("postgres-main\tpostgres:16-alpine\trunning");
        SetupInspectEnv("postgres-main", "POSTGRES_PASSWORD=secret\n");

        var containers = await _service.DiscoverDatabasesAsync();

        var container = containers.Single();
        container.IsRunning.Should().BeTrue();
        container.Config.CliCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"]);
        container.Config.RestoreCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-d", "postgres"]);
    }

    [Fact]
    public async Task DiscoverDatabasesAsync_MariaDb_IncludesCredentialsFromEnv()
    {
        SetupDockerPs("nextcloud-db\tmariadb:10.11\trunning");
        SetupInspectEnv("nextcloud-db",
            "MYSQL_USER=nextcloud\nMYSQL_PASSWORD=Pakan135@\nMYSQL_DATABASE=nextcloud\n");

        var containers = await _service.DiscoverDatabasesAsync();

        var container = containers.Single();
        container.Family.Should().Be("mariadb");
        container.IsRunning.Should().BeTrue();

        container.Config.CliCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-it", "{serviceName}", "mariadb", "-u", "nextcloud", "-pPakan135@"]);
        container.Config.BackupCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "mariadb-dump", "-u", "nextcloud", "-pPakan135@", "--all-databases"]);
        container.Config.ListDatabasesCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "mariadb", "-u", "nextcloud", "-pPakan135@", "-BN", "-e", "SHOW DATABASES;"]);
    }

    [Fact]
    public async Task DiscoverDatabasesAsync_Redis_BuildsDockerCpCommands()
    {
        SetupDockerPs("redis-main\tredis:7\trunning");
        SetupInspectEnv("redis-main", "");

        var containers = await _service.DiscoverDatabasesAsync();

        var container = containers.Single();
        container.Family.Should().Be("redis");
        container.Config.BackupCommand.Should().BeEquivalentTo(
            ["docker", "cp", "{serviceName}:/data/dump.rdb", "{path}"]);
        container.Config.RestoreCommand.Should().BeEquivalentTo(
            ["docker", "cp", "{path}", "{serviceName}:/data/dump.rdb"]);
        container.Config.CliCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-it", "{serviceName}", "redis-cli"]);
    }

    private void SetupDockerPs(string output)
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args[0] == "ps"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(output, "", 0));
    }

    private void SetupInspectEnv(string containerName, string envOutput)
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args[0] == "inspect" && args[3] == containerName),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(envOutput, "", 0));
    }
}
