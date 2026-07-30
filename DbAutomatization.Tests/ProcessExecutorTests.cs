using System.Diagnostics;
using DbAutomatization.Core.Models;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class ProcessExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WithEcho_ReturnsOutput()
    {
        var executor = new ProcessExecutor();

        var result = await executor.ExecuteAsync("echo", ["Hello World"]);

        result.Success.Should().BeTrue();
        result.Output.Trim().Should().Be("Hello World");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentFile_ReturnsFailure()
    {
        var executor = new ProcessExecutor();

        var result = await executor.ExecuteAsync("nonexistent_command_xyz", []);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(-1);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Timeout_ThrowsOperationCanceled()
    {
        var executor = new ProcessExecutor();

        var act = async () => await executor.ExecuteAsync("sleep", ["10"], timeoutSeconds: 1);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_ExitCodeNonZero_ReflectedInResult()
    {
        var executor = new ProcessExecutor();

        var result = await executor.ExecuteAsync("bash", ["-c", "exit 42"]);

        result.Success.Should().BeFalse();
        result.ExitCode.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_StdErrIsCaptured()
    {
        var executor = new ProcessExecutor();

        var result = await executor.ExecuteAsync("bash", ["-c", "echo error-msg >&2; exit 1"]);

        result.Success.Should().BeFalse();
        result.Error.Trim().Should().Be("error-msg");
    }

    [Fact]
    public async Task ExecuteAndStreamOutputAsync_WritesToStream()
    {
        var executor = new ProcessExecutor();
        using var ms = new MemoryStream();

        var result = await executor.ExecuteAndStreamOutputAsync("echo", ["stream-test"], ms);

        result.Success.Should().BeTrue();
        ms.Position = 0;
        using var reader = new StreamReader(ms);
        var content = await reader.ReadToEndAsync();
        content.Trim().Should().Be("stream-test");
    }
}
