using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class AptPackageManagerTests
{
    private readonly Mock<IProcessExecutor> _processMock;
    private readonly AptPackageManager _apt;

    public AptPackageManagerTests()
    {
        _processMock = new Mock<IProcessExecutor>();
        _apt = new AptPackageManager(_processMock.Object);
    }

    [Fact]
    public async Task DoesPackageExistAsync_UsesWhichCommand()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "which",
                It.Is<string[]>(args => args[0] == "postgresql"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("/usr/bin/postgresql", "", 0));

        var exists = await _apt.DoesPackageExistAsync("postgresql");

        exists.Should().BeTrue();
        _processMock.Verify(p => p.ExecuteAsync(
            "which",
            It.Is<string[]>(args => args[0] == "postgresql"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DoesPackageExistAsync_NonZeroExit_ReturnsFalse()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "which",
                It.IsAny<string[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "not found", 1));

        var exists = await _apt.DoesPackageExistAsync("nonexistent");

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task InstallPackageAsync_ExecutesCorrectCommandSequence()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "apt" && args[1] == "update"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "apt" &&
                    args[1] == "install" &&
                    args[2] == "-y" &&
                    args[3] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "daemon-reload"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "enable" &&
                    args[2] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "start" &&
                    args[2] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var (success, errorMessage) = await _apt.InstallPackageAsync("redis-server", "redis-server");

        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "apt" && args[1] == "update"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "apt" && args[1] == "install"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "systemctl" && args[1] == "daemon-reload"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "systemctl" && args[1] == "enable"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo",
            It.Is<string[]>(args => args[0] == "systemctl" && args[1] == "start"),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallPackageAsync_WithConfig_UsesAptPackageName()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Redis",
            AptPackageName = "redis-server",
            CliCommand = ["redis-cli"]
        };

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "apt" && args[1] == "update"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "apt" &&
                    args[1] == "install" &&
                    args[2] == "-y" &&
                    args[3] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "daemon-reload"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "enable" &&
                    args[2] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args =>
                    args[0] == "systemctl" &&
                    args[1] == "start" &&
                    args[2] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var (success, errorMessage) = await _apt.InstallPackageAsync(config);

        success.Should().BeTrue();
        errorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesPackageExistAsync_WithConfig_UsesAptPackageName()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "Redis",
            AptPackageName = "redis-server"
        };

        _processMock
            .Setup(p => p.ExecuteAsync(
                "which",
                It.Is<string[]>(args => args[0] == "redis-server"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("/usr/bin/redis-server", "", 0));

        var exists = await _apt.DoesPackageExistAsync(config);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DoesPackageExistAsync_WithConfig_FallsBackToFriendlyName()
    {
        var config = new DatabaseConfig
        {
            FriendlyName = "customdb",
            AptPackageName = ""
        };

        _processMock
            .Setup(p => p.ExecuteAsync(
                "which",
                It.Is<string[]>(args => args[0] == "customdb"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("/usr/bin/customdb", "", 0));

        var exists = await _apt.DoesPackageExistAsync(config);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task InstallPackageAsync_InstallFails_ReturnsError()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "apt" && args[1] == "update"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "apt" && args[1] == "install"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "E: Package not found", 100));

        var (success, errorMessage) = await _apt.InstallPackageAsync("nonexistent", "nonexistent");

        success.Should().BeFalse();
        errorMessage.Should().Be("E: Package not found");
    }
}
