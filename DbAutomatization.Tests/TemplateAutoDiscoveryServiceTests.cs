using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class TemplateAutoDiscoveryServiceTests
{
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly TemplateAutoDiscoveryService _service;

    public TemplateAutoDiscoveryServiceTests()
    {
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _processMock = new Mock<IProcessExecutor>();
        _service = new TemplateAutoDiscoveryService(_configRepoMock.Object, _processMock.Object);
    }

    [Fact]
    public async Task AutoDiscoverAsync_InstalledServiceNotInConfig_RegistersIt()
    {
        _configRepoMock
            .Setup(r => r.GetAll())
            .Returns(new Dictionary<string, DatabaseConfig>());

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("enabled", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "No such container", 1));

        await _service.AutoDiscoverAsync();

        _configRepoMock.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<DatabaseConfig>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task AutoDiscoverAsync_AlreadyRegistered_DoesNotRegisterAgain()
    {
        var existing = new Dictionary<string, DatabaseConfig>
        {
            { "postgresql", new DatabaseConfig { FriendlyName = "PostgreSQL" } }
        };

        _configRepoMock
            .Setup(r => r.GetAll())
            .Returns(existing);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "disabled", 1));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "No such container", 1));

        await _service.AutoDiscoverAsync();

        _configRepoMock.Verify(r => r.Register("postgresql", It.IsAny<DatabaseConfig>()), Times.Never);
    }

    [Fact]
    public async Task AutoDiscoverAsync_NotInstalled_DoesNotRegister()
    {
        _configRepoMock
            .Setup(r => r.GetAll())
            .Returns(new Dictionary<string, DatabaseConfig>());

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "disabled", 1));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "No such container", 1));

        await _service.AutoDiscoverAsync();

        _configRepoMock.Verify(r => r.Register(It.IsAny<string>(), It.IsAny<DatabaseConfig>()), Times.Never);
    }

    [Fact]
    public async Task AutoDiscoverAsync_PostgresqlInstalled_RegistersWithCorrectConfig()
    {
        _configRepoMock
            .Setup(r => r.GetAll())
            .Returns(new Dictionary<string, DatabaseConfig>());

        DatabaseConfig? registeredConfig = null;
        _configRepoMock
            .Setup(r => r.Register(It.IsAny<string>(), It.IsAny<DatabaseConfig>()))
            .Callback<string, DatabaseConfig>((key, config) => registeredConfig = config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.Is<string[]>(a => a[0] == "is-enabled" && a[1] == "postgresql"),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("enabled", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.Is<string[]>(a => a[0] == "is-enabled" && a[1] != "postgresql"),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "disabled", 1));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "No such container", 1));

        await _service.AutoDiscoverAsync();

        registeredConfig.Should().NotBeNull();
        registeredConfig!.FriendlyName.Should().Be("PostgreSQL");
    }

    [Fact]
    public async Task AutoDiscoverAsync_DockerContainerPresent_RegistersDockerTemplate()
    {
        _configRepoMock
            .Setup(r => r.GetAll())
            .Returns(new Dictionary<string, DatabaseConfig>());

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl", It.IsAny<string[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "disabled", 1));

        DatabaseConfig? registeredDocker = null;
        _configRepoMock
            .Setup(r => r.Register("postgres-main", It.IsAny<DatabaseConfig>()))
            .Callback<string, DatabaseConfig>((key, config) => registeredDocker = config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.Is<string[]>(a => a[0] == "inspect" && a[1] == "postgres-main"),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "docker", It.Is<string[]>(a => a[0] == "inspect" && a[1] != "postgres-main"),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "No such container", 1));

        await _service.AutoDiscoverAsync();

        registeredDocker.Should().NotBeNull();
        registeredDocker!.EngineType.Should().Be("docker");
        registeredDocker.BackupCommand.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "{serviceName}", "pg_dumpall", "-U", "postgres"]);
    }
}
