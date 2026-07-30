using System.Text.Json;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class ExternalEditorServiceTests
{
    [Fact]
    public async Task DetectEditor_FallsBackToNanoVimVi()
    {
        var processMock = new Mock<IProcessExecutor>();
        var configRepoMock = new Mock<IDatabaseConfigRepository>();

        var service = new ExternalEditorService(processMock.Object, configRepoMock.Object);

        var configs = new Dictionary<string, DatabaseConfig>
        {
            { "test", new DatabaseConfig { FriendlyName = "Test" } }
        };
        configRepoMock.Setup(r => r.GetAll()).Returns(configs);

        string? tempFile = null;
        processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                It.IsAny<string>(),
                It.Is<string[]>(args => args.Length == 1),
                It.IsAny<CancellationToken>()))
            .Callback<string, string[], CancellationToken>((_, args, _) => tempFile = args[0])
            .Returns(Task.CompletedTask);

        var validJson = JsonSerializer.Serialize(configs, new JsonSerializerOptions { WriteIndented = true });

        processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string[], CancellationToken>(async (_, args, _) =>
            {
                tempFile = args[0];
                await File.WriteAllTextAsync(tempFile, validJson);
            })
            .Returns(Task.CompletedTask);

        configRepoMock.Setup(r => r.Reload());

        var result = await service.EditConfigInExternalEditorAsync();

        result.Should().BeTrue();
        configRepoMock.Verify(r => r.Reload(), Times.Once);
    }

    [Fact]
    public async Task InvalidJson_AfterEdit_OffersRetry_CancelReturnsFalse()
    {
        var processMock = new Mock<IProcessExecutor>();
        var configRepoMock = new Mock<IDatabaseConfigRepository>();

        var service = new ExternalEditorService(processMock.Object, configRepoMock.Object);

        configRepoMock.Setup(r => r.GetAll()).Returns(new Dictionary<string, DatabaseConfig>());

        processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string[], CancellationToken>(async (_, args, _) =>
            {
                var tempFile = args[0];
                await File.WriteAllTextAsync(tempFile, "invalid json{{{");
            })
            .Returns(Task.CompletedTask);

        configRepoMock.Setup(r => r.Reload());

        var originalIn = Console.In;
        Console.SetIn(new StringReader("2\n"));
        try
        {
            var result = await service.EditConfigInExternalEditorAsync();
            result.Should().BeFalse();
        }
        finally
        {
            Console.SetIn(originalIn);
        }

        configRepoMock.Verify(r => r.Reload(), Times.Never);
    }

    [Fact]
    public async Task ValidJson_AfterEdit_SavesAndReloads()
    {
        var processMock = new Mock<IProcessExecutor>();
        var configRepoMock = new Mock<IDatabaseConfigRepository>();

        var service = new ExternalEditorService(processMock.Object, configRepoMock.Object);

        var configs = new Dictionary<string, DatabaseConfig>
        {
            { "pg", new DatabaseConfig { FriendlyName = "PostgreSQL", BackupCommand = ["pg_dumpall"] } }
        };
        configRepoMock.Setup(r => r.GetAll()).Returns(configs);

        processMock
            .Setup(p => p.ExecuteInteractiveAsync(
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string[], CancellationToken>(async (_, args, _) =>
            {
                var tempFile = args[0];
                var modified = new Dictionary<string, DatabaseConfig>
                {
                    { "pg", new DatabaseConfig { FriendlyName = "PostgreSQL Prod", BackupCommand = ["pg_dumpall", "--compress=9"] } }
                };
                await File.WriteAllTextAsync(tempFile, JsonSerializer.Serialize(modified, new JsonSerializerOptions { WriteIndented = true }));
            })
            .Returns(Task.CompletedTask);

        configRepoMock.Setup(r => r.Reload());

        var result = await service.EditConfigInExternalEditorAsync();

        result.Should().BeTrue();
        configRepoMock.Verify(r => r.Reload(), Times.Once);

        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "databases.json");
        if (File.Exists(configPath))
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, DatabaseConfig>>(await File.ReadAllTextAsync(configPath));
            saved.Should().ContainKey("pg");
            saved["pg"].FriendlyName.Should().Be("PostgreSQL Prod");
            File.Delete(configPath);
        }
    }
}
