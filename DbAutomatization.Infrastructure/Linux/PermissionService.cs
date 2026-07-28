using System.ComponentModel;
using System.Diagnostics;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class PermissionService : IPermissionService
{
    public bool HasSudo { get; }

    public PermissionService()
    {
        HasSudo = CheckSudoAccess();
    }

    private static bool CheckSudoAccess()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sudo",
                    ArgumentList = { "-n", "true" },
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            if (!process.WaitForExit(5000))
            {
                process.Kill();
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
