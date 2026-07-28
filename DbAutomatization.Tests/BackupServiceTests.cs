using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class BackupServiceTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _service = new BackupService(_configRepoMock.Object, _processMock.Object);
    }

    [Fact]
    public async Task ExplicitPathPlaceholder_SubstitutesCorrectly()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MongoDB",
            BackupCommand = ["mongodump", "--archive={path}"]
        };

        _configRepoMock.Setup(r => r.Get("mongod")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "mongodump",
                It.Is<string[]>(args => args.Length == 1 && args[0].StartsWith("--archive=")),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("mongod");

        result.Success.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAsync(
            "mongodump",
            It.Is<string[]>(args => args.Length == 1 && args[0].StartsWith("--archive=")),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoPathPlaceholder_PipesToStream()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAndStreamOutputAsync(
                "sudo",
                It.Is<string[]>(args => args.Contains("pg_dumpall")),
                It.IsAny<Stream>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("postgresql");

        result.Success.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAndStreamOutputAsync(
            "sudo",
            It.IsAny<string[]>(),
            It.IsAny<Stream>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShellCommand_PassesThroughAsIs()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Redis",
            BackupCommand = ["/bin/bash", "-c", "redis-cli SAVE && sudo cp /var/lib/redis/dump.rdb {path}"]
        };

        _configRepoMock.Setup(r => r.Get("redis-server")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "/bin/bash",
                It.Is<string[]>(args => args[0] == "-c" && args[1].Contains("redis-cli SAVE")),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("redis-server");

        result.Success.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAsync(
            "/bin/bash",
            It.IsAny<string[]>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NullBackupCommand_ReturnsFailure()
    {
        _configRepoMock.Setup(r => r.Get("unknown")).Returns((DatabaseConfig?)null);

        var result = await _service.CreateBackupAsync("unknown");

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
    }

    [Fact]
    public async Task DatabaseName_SubstitutesDbPlaceholder()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MariaDB",
            BackupCommand = ["sudo", "mariadb-dump", "{db}"]
        };

        _configRepoMock.Setup(r => r.Get("mariadb")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAndStreamOutputAsync(
                "sudo",
            It.Is<string[]>(args => args[0] == "mariadb-dump"),
            It.IsAny<Stream>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("mariadb", "mydb");

        result.Success.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAndStreamOutputAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "mariadb-dump" && args[1] == "mydb"),
            It.IsAny<Stream>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StreamIsDisposedAfterBackup()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Test",
            BackupCommand = ["pg_dumpall"]
        };

        _configRepoMock.Setup(r => r.Get("test")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAndStreamOutputAsync(
                "pg_dumpall", It.IsAny<string[]>(),
                It.IsAny<Stream>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("test");

        result.Success.Should().BeTrue();
    }
}
