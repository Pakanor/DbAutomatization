using System.Diagnostics;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class BashExecutor : IBashExecutor
{
    public async Task<string> ExecuteAsync(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        
        if (!string.IsNullOrWhiteSpace(error) && process.ExitCode != 0)
        {
            return string.Empty; 
        }
        
        return output;
    }
}