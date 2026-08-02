using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class SystemdServiceManagerTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly SystemdServiceManager _manager;

    public SystemdServiceManagerTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _manager = new SystemdServiceManager(_processMock.Object, _configRepoMock.Object);
    }

    [Fact]
    public async Task StartServiceAsync_UsesCorrectArgumentList()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args.Length == 3 &&
                    args[0] == "systemctl" &&
                    args[1] == "start" &&
                    args[2] == "postgresql"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.StartServiceAsync("postgresql");

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args =>
                args[0] == "systemctl" &&
                args[1] == "start" &&
                args[2] == "postgresql"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopServiceAsync_UsesCorrectArgumentList()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "stop" &&
                    args[2] == "mariadb"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.StopServiceAsync("mariadb");

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args =>
                args[0] == "systemctl" &&
                args[1] == "stop" &&
                args[2] == "mariadb"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartServiceAsync_UsesCorrectArgumentList()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "restart" &&
                    args[2] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.RestartServiceAsync("redis-server");

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args =>
                args[0] == "systemctl" &&
                args[1] == "restart" &&
                args[2] == "redis-server"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NonZeroExitCode_DoesNotThrow()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "error", 1));

        var act = async () => await _manager.StartServiceAsync("postgresql");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDatabasesAsync_UsesListDatabasesCommand()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            ListDatabasesCommand = ["psql", "-c", "SELECT datname FROM pg_database"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "psql",
                It.Is<string[]>(args =>
                    args[0] == "-c" &&
                    args[1] == "SELECT datname FROM pg_database"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("mydb\npostgres\ntest", "", 0));

        var databases = await _manager.GetDatabasesAsync("postgresql");

        databases.Should().BeEquivalentTo(["mydb", "postgres", "test"]);
    }

    [Fact]
    public async Task GetDatabasesAsync_TabularOutput_ExtractsSecondColumn()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "InfluxDB",
            ListDatabasesCommand = ["influx", "bucket", "list", "--hide-headers"],
            OutputColumnIndex = 1
        };

        _configRepoMock.Setup(r => r.Get("influxdb")).Returns(config);

        string tabularOutput = "094c3b2a1b200001\tmybucket\tinfinity\t2025-01-15T10:00:00Z\n" +
                               "094c3b2a1b200002\tmyotherbucket\t72h\t2025-01-15T10:00:00Z";

        _processMock
            .Setup(p => p.ExecuteAsync(
                "influx",
                It.Is<string[]>(args => args[0] == "bucket"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(tabularOutput, "", 0));

        var databases = await _manager.GetDatabasesAsync("influxdb");

        databases.Should().BeEquivalentTo(["mybucket", "myotherbucket"]);
    }

    [Fact]
    public async Task GetDatabasesAsync_SingleColumnOutput_ReturnsLinesAsIs()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            ListDatabasesCommand = ["psql", "-c", "SELECT datname"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "psql",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("mydb\npostgres\ntest\n", "", 0));

        var databases = await _manager.GetDatabasesAsync("postgresql");

        databases.Should().BeEquivalentTo(["mydb", "postgres", "test"]);
    }

    [Fact]
    public async Task GetDatabasesAsync_NullConfig_ReturnsEmpty()
    {
        _configRepoMock.Setup(r => r.Get("unknown")).Returns((DatabaseConfig?)null);

        var databases = await _manager.GetDatabasesAsync("unknown");

        databases.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenDatabaseCliAsync_UsesExecuteInteractive()
    {
        _processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                "redis-cli",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _manager.OpenDatabaseCliAsync("redis-server", ["redis-cli"]);

        _processMock.Verify(p => p.ExecuteInteractiveAsync(
            "redis-cli",
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartServiceAsync_DockerEngine_UsesDockerStart()
    {
        _configRepoMock.Setup(r => r.Get("postgres-main")).Returns(new DatabaseConfig { EngineType = "docker" });

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args[0] == "start" && args[1] == "postgres-main"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.StartServiceAsync("postgres-main");

        _processMock.Verify(p => p.ExecuteAsync(
            "docker",
            It.Is<string[]>(args => args[0] == "start" && args[1] == "postgres-main"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopServiceAsync_DockerEngine_UsesDockerStop()
    {
        _configRepoMock.Setup(r => r.Get("mariadb-main")).Returns(new DatabaseConfig { EngineType = "docker" });

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args[0] == "stop" && args[1] == "mariadb-main"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.StopServiceAsync("mariadb-main");

        _processMock.Verify(p => p.ExecuteAsync(
            "docker",
            It.Is<string[]>(args => args[0] == "stop" && args[1] == "mariadb-main"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartServiceAsync_DockerEngine_UsesDockerRestart()
    {
        _configRepoMock.Setup(r => r.Get("redis-main")).Returns(new DatabaseConfig { EngineType = "docker" });

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args[0] == "restart" && args[1] == "redis-main"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        await _manager.RestartServiceAsync("redis-main");

        _processMock.Verify(p => p.ExecuteAsync(
            "docker",
            It.Is<string[]>(args => args[0] == "restart" && args[1] == "redis-main"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenDatabaseCliAsync_Docker_SubstitutesServiceName()
    {
        _processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                "docker",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _manager.OpenDatabaseCliAsync(
            "postgres-main",
            ["docker", "exec", "-it", "{serviceName}", "psql", "-U", "postgres"]);

        _processMock.Verify(p => p.ExecuteInteractiveAsync(
            "docker",
            It.Is<string[]>(args =>
                args[0] == "exec" &&
                args.Contains("-it") &&
                args.Contains("postgres-main") &&
                !args.Contains("{serviceName}")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDatabasesAsync_DockerEngine_SubstitutesServiceName()
    {
        var config = new DatabaseConfig
        {
            EngineType = "docker",
            ListDatabasesCommand = ["docker", "exec", "-i", "{serviceName}", "psql", "-U", "postgres", "-l", "-t", "-A"]
        };

        _configRepoMock.Setup(r => r.Get("postgres-main")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker",
                It.Is<string[]>(args => args.Contains("postgres-main") && !args.Contains("{serviceName}")),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("mydb\npostgres\ntest", "", 0));

        var databases = await _manager.GetDatabasesAsync("postgres-main");

        databases.Should().BeEquivalentTo(["mydb", "postgres", "test"]);
        _processMock.Verify(p => p.ExecuteAsync(
            "docker",
            It.Is<string[]>(args => args[0] == "exec" && args[2] == "postgres-main"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
