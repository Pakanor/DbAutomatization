using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class RestoreServiceTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly Mock<IDatabaseConfigRepository> _configRepoMock;
    private readonly RestoreService _service;

    public RestoreServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _service = new RestoreService(_configRepoMock.Object, _processMock.Object);
    }

    [Fact]
    public async Task RestoreAsync_WithPathPlaceholder_ExecutesCommand()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string tempFile = Path.GetTempFileName();
        try
        {
            _processMock
                .Setup(p => p.ExecuteAsync(
                    "sudo",
                    It.Is<string[]>(args => args[0] == "-u" && args.Contains("-f")),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", tempFile);

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args.Any(a => a.Contains(tempFile))),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_SingleDatabase_SelectsCorrectCommand()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"],
            SingleDatabaseRestoreCommand = ["sudo", "-u", "postgres", "psql", "-d", "{db}", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string tempFile = Path.GetTempFileName();
        try
        {
            _processMock
                .Setup(p => p.ExecuteAsync(
                    "sudo",
                    It.Is<string[]>(args => args.Contains("-d") && args.Contains("mydb")),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", tempFile, "mydb");

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args.Contains("-d") && args.Contains("mydb") && args.Any(a => a.Contains(tempFile))),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_FileNotFound_ReturnsFailure()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        var result = await _service.RestoreAsync("postgresql", "/nonexistent/path/file.sql");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("nie istnieje");
    }

    [Fact]
    public async Task RestoreAsync_NoRestoreCommand_ReturnsFailure()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MongoDB",
            BackupCommand = ["mongodump", "--archive={path}"]
        };

        _configRepoMock.Setup(r => r.Get("mongod")).Returns(config);

        var result = await _service.RestoreAsync("mongod", "/some/path");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Brak zdefiniowanej komendy restore");
    }

    [Fact]
    public async Task RestoreAsync_NullConfig_ReturnsFailure()
    {
        _configRepoMock.Setup(r => r.Get("unknown")).Returns((DatabaseConfig?)null);

        var result = await _service.RestoreAsync("unknown", "/some/path");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Brak konfiguracji");
    }

    [Fact]
    public async Task RestoreAsync_NoPathPlaceholder_StreamsFileToStdin()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "MySQL",
            RestoreCommand = ["mysql", "-u", "root"]
        };

        _configRepoMock.Setup(r => r.Get("mysql")).Returns(config);

        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "SQL content");
        try
        {
            _processMock
                .Setup(p => p.ExecuteWithStdinFileAsync(
                    "mysql",
                    It.Is<string[]>(args => args[0] == "-u"),
                    tempFile,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("mysql", tempFile);

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteWithStdinFileAsync(
                "mysql",
                It.Is<string[]>(args => args[0] == "-u"),
                tempFile,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
