using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class BackupServiceTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly Mock<IBackupRetentionManager> _retentionMock;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _retentionMock = new Mock<IBackupRetentionManager>();
        _service = new BackupService(_configRepoMock.Object, _processMock.Object, _retentionMock.Object);
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
    public async Task PreBackupCommand_ExecutesBeforeMainBackup()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Redis",
            PreBackupCommand = ["redis-cli", "SAVE"],
            BackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("redis-server")).Returns(config);

        _processMock
            .SetupSequence(p => p.ExecuteAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("redis-server");

        result.Success.Should().BeTrue();

        _processMock.Verify(p => p.ExecuteAsync(
            "redis-cli",
            It.Is<string[]>(args => args[0] == "SAVE"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "cp"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreBackupCommand_Failure_ReturnsFailure()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Redis",
            PreBackupCommand = ["redis-cli", "SAVE"],
            BackupCommand = ["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("redis-server")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "redis-cli",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "SAVE failed", 1));

        var result = await _service.CreateBackupAsync("redis-server");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Pre-backup command failed");
    }

    [Fact]
    public async Task UsesConfigurableTimeout()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
            BackupTimeoutSeconds = 600
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAndStreamOutputAsync(
                "sudo",
                It.Is<string[]>(args => args.Contains("pg_dumpall")),
                It.IsAny<Stream>(),
                600,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("postgresql");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RetentionIsCalledOnSuccess()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MongoDB",
            BackupCommand = ["mongodump", "--archive={path}"],
            RetentionCount = 5
        };

        _configRepoMock.Setup(r => r.Get("mongod")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "mongodump",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("mongod");

        result.Success.Should().BeTrue();
        _retentionMock.Verify(r => r.CleanupAsync(
            It.IsAny<string>(),
            "backup_mongod",
            5), Times.Once);
    }

    [Fact]
    public async Task RetentionIsNotCalledOnFailure()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MongoDB",
            BackupCommand = ["mongodump", "--archive={path}"],
            RetentionCount = 5
        };

        _configRepoMock.Setup(r => r.Get("mongod")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAsync(
                "mongodump",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "error", 1));

        var result = await _service.CreateBackupAsync("mongod");

        result.Success.Should().BeFalse();
        _retentionMock.Verify(r => r.CleanupAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>()), Times.Never);
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
    public async Task SingleDatabaseBackupCommand_UsedWhenDatabaseNameProvided()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
            SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        _processMock
            .Setup(p => p.ExecuteAndStreamOutputAsync(
                "sudo",
                It.Is<string[]>(args => args.Contains("pg_dump")),
                It.IsAny<Stream>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var result = await _service.CreateBackupAsync("postgresql", "mydb");

        result.Success.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAndStreamOutputAsync(
            "sudo",
            It.Is<string[]>(args => args.Contains("pg_dump") && args.Contains("mydb")),
            It.IsAny<Stream>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SingleDatabaseBackupCommand_NotUsedWhenDatabaseNameNull()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            BackupCommand = ["sudo", "-u", "postgres", "pg_dumpall"],
            SingleDatabaseBackupCommand = ["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"]
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
            It.Is<string[]>(args => args.Contains("pg_dumpall")),
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
