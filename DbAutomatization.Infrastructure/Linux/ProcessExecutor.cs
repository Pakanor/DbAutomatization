using System.Diagnostics;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class ProcessExecutor : IProcessExecutor
{
    public async Task<ProcessResult> ExecuteAsync(
        string fileName,
        string[] arguments,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(timeoutCts.Token);

        string output = await outputTask;
        string error = await errorTask;

        return new ProcessResult(output, error, process.ExitCode);
    }

    public async Task ExecuteInteractiveAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);

        if (process != null)
            await process.WaitForExitAsync(cancellationToken);
    }

    public async Task<ProcessResult> ExecuteAndStreamOutputAsync(
        string fileName,
        string[] arguments,
        Stream outputStream,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, timeoutCts.Token);
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(timeoutCts.Token);

        await stdoutTask;
        string error = await errorTask;

        return new ProcessResult(string.Empty, error, process.ExitCode);
    }
}
