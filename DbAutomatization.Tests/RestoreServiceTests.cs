using System.IO.Compression;
using DbAutomatization.Core.Exceptions;
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
    private readonly string _backupDir;

    public RestoreServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _configRepoMock = new Mock<IDatabaseConfigRepository>();
        _service = new RestoreService(_configRepoMock.Object, _processMock.Object);
        
        // Set backup directory to test project's bin directory for consistent testing
        string testBackupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
        Environment.SetEnvironmentVariable("DBAUTOMATE_BACKUP_DIR", testBackupDir);
        
        _backupDir = BackupService.GetBackupDirectory();
        Directory.CreateDirectory(_backupDir);
    }

    private string CreateValidBackupFile(string content = "SQL content", string extension = ".sql")
    {
        Directory.CreateDirectory(_backupDir);
        string fileName = $"backup_test_{Guid.NewGuid():N}{extension}";
        string fullPath = Path.Combine(_backupDir, fileName);
        File.WriteAllText(fullPath, content);
        return fullPath;
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

        string backupFile = CreateValidBackupFile("-- PostgreSQL dump\nSET statement_timeout = 0;\nCREATE TABLE test (id int);");
        try
        {
            _processMock
                .Setup(p => p.ExecuteAsync(
                    "sudo",
                    It.Is<string[]>(args => args[0] == "-u" && args.Contains("-f")),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", backupFile);

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args.Any(a => a.Contains(backupFile))),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
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

        string backupFile = CreateValidBackupFile("-- PostgreSQL dump\nCREATE DATABASE mydb;\n\\connect mydb;\nCREATE TABLE test (id int);");
        try
        {
            _processMock
                .Setup(p => p.ExecuteAsync(
                    "sudo",
                    It.Is<string[]>(args => args.Contains("-d") && args.Contains("mydb")),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", backupFile, "mydb");

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args.Contains("-d") && args.Contains("mydb") && args.Any(a => a.Contains(backupFile))),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_FileNotFound_ThrowsException()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        var act = async () => await _service.RestoreAsync("postgresql", "/nonexistent/path/file.sql");

        await act.Should().ThrowAsync<DatabaseConfigurationException>()
            .WithMessage("*nie istnieje*");
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

        string backupFile = CreateValidBackupFile("binary content", ".dump");
        try
        {
            var result = await _service.RestoreAsync("mongod", backupFile);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("Brak zdefiniowanej komendy restore");
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_NullConfig_ReturnsFailure()
    {
        _configRepoMock.Setup(r => r.Get("unknown")).Returns((DatabaseConfig?)null);

        string backupFile = CreateValidBackupFile("content", ".sql");
        try
        {
            var result = await _service.RestoreAsync("unknown", backupFile);

            result.Success.Should().BeFalse();
            result.Error.Should().Contain("Brak konfiguracji");
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
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

        string backupFile = CreateValidBackupFile("-- MySQL dump\nCREATE DATABASE test;\nUSE test;\nCREATE TABLE t (id int);");
        try
        {
            _processMock
                .Setup(p => p.ExecuteWithStdinFileAsync(
                    "mysql",
                    It.Is<string[]>(args => args[0] == "-u"),
                    backupFile,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("mysql", backupFile);

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteWithStdinFileAsync(
                "mysql",
                It.Is<string[]>(args => args[0] == "-u"),
                backupFile,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_EmptyFile_ThrowsException()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string emptyFile = CreateValidBackupFile("", ".sql");
        try
        {
            var act = async () => await _service.RestoreAsync("postgresql", emptyFile);

            await act.Should().ThrowAsync<DatabaseConfigurationException>()
                .WithMessage("*pusty*");
        }
        finally
        {
            if (File.Exists(emptyFile)) File.Delete(emptyFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_FileOutsideBackupDir_ThrowsException()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "SQL content");
        try
        {
            var act = async () => await _service.RestoreAsync("postgresql", tempFile);

            await act.Should().ThrowAsync<DatabaseConfigurationException>()
                .WithMessage("*poza dozwolonym katalogiem*");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_InvalidExtension_ThrowsException()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql", "-f", "{path}"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string invalidFile = CreateValidBackupFile("content", ".txt");
        try
        {
            var act = async () => await _service.RestoreAsync("postgresql", invalidFile);

            await act.Should().ThrowAsync<DatabaseConfigurationException>()
                .WithMessage("*nieobsługiwane rozszerzenie*");
        }
        finally
        {
            if (File.Exists(invalidFile)) File.Delete(invalidFile);
        }
    }

    private string CreateCompressedBackupFile(string content = "SQL content")
    {
        Directory.CreateDirectory(_backupDir);
        string fileName = $"backup_test_{Guid.NewGuid():N}.bak.gz";
        string fullPath = Path.Combine(_backupDir, fileName);
        using var fileStream = File.Create(fullPath);
        using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: false);
        using var writer = new StreamWriter(gzipStream);
        writer.Write(content);
        return fullPath;
    }

    [Fact]
    public async Task RestoreAsync_GzFile_DecompressesAndStreamsToStdin()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string backupFile = CreateCompressedBackupFile("-- PostgreSQL dump\nCREATE TABLE test (id int);");
        try
        {
            Stream? capturedStream = null;
            _processMock
                .Setup(p => p.ExecuteWithStdinStreamAsync(
                    "sudo",
                    It.Is<string[]>(args => args[0] == "-u"),
                    It.IsAny<Stream>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string[], Stream, int, CancellationToken>((_, __, stream, _, _) => capturedStream = stream)
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", backupFile);

            result.Success.Should().BeTrue();
            capturedStream.Should().BeOfType<GZipStream>();
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }

    [Fact]
    public async Task RestoreAsync_GzFileWithMagicBytes_DecompressesAndStreamsToStdin()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        // Create a .bak file with gzip content (magic bytes)
        string fileName = $"backup_test_{Guid.NewGuid():N}.bak";
        string fullPath = Path.Combine(_backupDir, fileName);
        using (var fileStream = File.Create(fullPath))
        using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: false))
        using (var writer = new StreamWriter(gzipStream))
        {
            writer.Write("-- PostgreSQL dump\nCREATE TABLE test (id int);");
        }

        try
        {
            Stream? capturedStream = null;
            _processMock
                .Setup(p => p.ExecuteWithStdinStreamAsync(
                    "sudo",
                    It.Is<string[]>(args => args[0] == "-u"),
                    It.IsAny<Stream>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string[], Stream, int, CancellationToken>((_, __, stream, _, _) => capturedStream = stream)
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", fullPath);

            result.Success.Should().BeTrue();
            capturedStream.Should().BeOfType<GZipStream>();
        }
        finally
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
    }

    [Fact]
    public async Task RestoreAsync_RegularFile_UsesExecuteWithStdinFileAsync()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "PostgreSQL",
            RestoreCommand = ["sudo", "-u", "postgres", "psql"]
        };

        _configRepoMock.Setup(r => r.Get("postgresql")).Returns(config);

        string backupFile = CreateValidBackupFile("-- PostgreSQL dump\nCREATE TABLE test (id int);");
        try
        {
            _processMock
                .Setup(p => p.ExecuteWithStdinFileAsync(
                    "sudo",
                    It.Is<string[]>(args => args[0] == "-u"),
                    backupFile,
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcessResult("", "", 0));

            var result = await _service.RestoreAsync("postgresql", backupFile);

            result.Success.Should().BeTrue();
            _processMock.Verify(p => p.ExecuteWithStdinFileAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "-u"),
                backupFile,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Once);
            _processMock.Verify(p => p.ExecuteWithStdinStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<Stream>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            if (File.Exists(backupFile)) File.Delete(backupFile);
        }
    }
}