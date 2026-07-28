using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class PermissionServiceTests
{
    private readonly Mock<IProcessExecutor> _processMock;

    public PermissionServiceTests()
    {
        _processMock = new Mock<IProcessExecutor>();
    }

    [Fact]
    public void ExitCodeZero_SetsHasSudoTrue()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo",
                It.Is<string[]>(args => args[0] == "-n" && args[1] == "true"),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var service = new PermissionService(_processMock.Object);

        service.HasSudo.Should().BeTrue();
    }

    [Fact]
    public void ExitCodeNonZero_SetsHasSudoFalse()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo", It.IsAny<string[]>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 1));

        var service = new PermissionService(_processMock.Object);

        service.HasSudo.Should().BeFalse();
    }

    [Fact]
    public void Exception_SetsHasSudoFalse()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo", It.IsAny<string[]>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new PermissionService(_processMock.Object);

        service.HasSudo.Should().BeFalse();
    }

    [Fact]
    public void ResultIsCached_ExecutorCalledOnce()
    {
        _processMock
            .Setup(p => p.ExecuteAsync(
                "sudo", It.IsAny<string[]>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult("", "", 0));

        var service = new PermissionService(_processMock.Object);

        _ = service.HasSudo;
        _ = service.HasSudo;
        _ = service.HasSudo;

        _processMock.Verify(p => p.ExecuteAsync(
            "sudo", It.IsAny<string[]>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
