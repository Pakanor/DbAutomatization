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

        var startInfo = CreateStartInfo(fileName, captureIo: true);
        AddArguments(startInfo, arguments);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(string.Empty, ex.Message, -1);
        }

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
        var startInfo = CreateStartInfo(fileName, captureIo: false);
        AddArguments(startInfo, arguments);

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

        var startInfo = CreateStartInfo(fileName, captureIo: true);
        AddArguments(startInfo, arguments);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(string.Empty, ex.Message, -1);
        }

        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, timeoutCts.Token);
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(timeoutCts.Token);

        await stdoutTask;
        string error = await errorTask;

        return new ProcessResult(string.Empty, error, process.ExitCode);
    }

    public async Task<ProcessResult> ExecuteWithStdinFileAsync(
        string fileName,
        string[] arguments,
        string stdinFilePath,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startInfo = CreateStartInfo(fileName, captureIo: true);
        AddArguments(startInfo, arguments);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(string.Empty, ex.Message, -1);
        }

        var stdinTask = Task.Run(async () =>
        {
            await using var fileStream = File.OpenRead(stdinFilePath);
            await fileStream.CopyToAsync(process.StandardInput.BaseStream, timeoutCts.Token);
            process.StandardInput.Close();
        }, timeoutCts.Token);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(timeoutCts.Token);
        await stdinTask;

        string output = await stdoutTask;
        string error = await stderrTask;

        return new ProcessResult(output, error, process.ExitCode);
    }

    public async Task<ProcessResult> ExecuteWithStdinStreamAsync(
        string fileName,
        string[] arguments,
        Stream inputStream,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var startInfo = CreateStartInfo(fileName, captureIo: true);
        AddArguments(startInfo, arguments);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return new ProcessResult(string.Empty, ex.Message, -1);
        }

        var stdinTask = Task.Run(async () =>
        {
            await inputStream.CopyToAsync(process.StandardInput.BaseStream, timeoutCts.Token);
            process.StandardInput.Close();
        }, timeoutCts.Token);

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(timeoutCts.Token);
        await stdinTask;

        string output = await stdoutTask;
        string error = await stderrTask;

        return new ProcessResult(output, error, process.ExitCode);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, bool captureIo)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = captureIo,
            RedirectStandardError = captureIo,
            RedirectStandardInput = captureIo,
            UseShellExecute = false,
            CreateNoWindow = captureIo
        };
    }

    private static void AddArguments(ProcessStartInfo startInfo, string[] arguments)
    {
        foreach (var arg in arguments)
            startInfo.ArgumentList.Add(arg);
    }
}
