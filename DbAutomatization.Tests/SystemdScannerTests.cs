using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class SystemdScannerTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly SystemdScanner _scanner;

    public SystemdScannerTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _scanner = new SystemdScanner(_processMock.Object, _configRepoMock.Object);
    }

    [Fact]
    public async Task ScanForDatabasesAsync_ReturnsDetectedDatabases()
    {
        var configs = new Dictionary<string, DatabaseConfig>
        {
            ["postgresql"] = new() { FriendlyName = "PostgreSQL" },
            ["redis-server"] = new() { FriendlyName = "Redis" }
        };

        _configRepoMock.Setup(r => r.GetAll()).Returns(configs);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl",
                It.Is<string[]>(args =>
                    args[0] == "list-unit-files" &&
                    args[1] == "--type=service" &&
                    args[2] == "--no-legend"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("postgresql.service enabled\nredis-server.service enabled", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl",
                It.Is<string[]>(args => args[0] == "is-active" && args[1] == "postgresql"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("active", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl",
                It.Is<string[]>(args => args[0] == "is-active" && args[1] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("inactive", "", 0));

        var results = await _scanner.ScanForDatabasesAsync();

        results.Should().HaveCount(2);
        results[0].ServiceName.Should().Be("postgresql");
        results[0].IsInstalled.Should().BeTrue();
        results[0].IsRunning.Should().BeTrue();
        results[1].ServiceName.Should().Be("redis-server");
        results[1].IsInstalled.Should().BeTrue();
        results[1].IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ScanForDatabasesAsync_NonExistentUnit_ReturnsNotInstalled()
    {
        var configs = new Dictionary<string, DatabaseConfig>
        {
            ["postgresql"] = new() { FriendlyName = "PostgreSQL" },
            ["unknown-db"] = new() { FriendlyName = "Unknown" }
        };

        _configRepoMock.Setup(r => r.GetAll()).Returns(configs);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("postgresql.service enabled", "", 0));

        var results = await _scanner.ScanForDatabasesAsync();

        results.Should().HaveCount(2);
        results[0].ServiceName.Should().Be("postgresql");
        results[0].IsInstalled.Should().BeTrue();
        results[1].ServiceName.Should().Be("unknown-db");
        results[1].IsInstalled.Should().BeFalse();
        results[1].IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task ScanForDatabasesAsync_EmptyConfig_ReturnsEmpty()
    {
        _configRepoMock.Setup(r => r.GetAll()).Returns(new Dictionary<string, DatabaseConfig>());

        _processMock
            .Setup(p => p.ExecuteAsync(
                "systemctl",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var results = await _scanner.ScanForDatabasesAsync();

        results.Should().BeEmpty();
    }
}
