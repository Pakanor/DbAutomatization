using System.Diagnostics;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class BashExecutor : IBashExecutor
{
    public async Task<string> ExecuteAsync(string command)
    {
        var escapedArgs = command.Replace("\"", "\\\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{escapedArgs}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true, 
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(error))
        {
            return error;
        }

        return output;
    }

    public async Task ExecuteInteractiveAsync(string command)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardInput = false,  
            RedirectStandardOutput = false, 
            RedirectStandardError = false,  
            UseShellExecute = false         
        };

        using var process = Process.Start(processInfo);
        if (process != null)
        {
            await process.WaitForExitAsync();
        }
    }
}