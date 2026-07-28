using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class PermissionService : IPermissionService
{
    private readonly IProcessExecutor _process;
    private bool _hasSudo;
    private bool _checked;

    public bool HasSudo
    {
        get
        {
            if (!_checked)
            {
                _hasSudo = CheckSudoAsync().GetAwaiter().GetResult();
                _checked = true;
            }
            return _hasSudo;
        }
    }

    public PermissionService(IProcessExecutor process)
    {
        _process = process;
    }

    private async Task<bool> CheckSudoAsync()
    {
        try
        {
            var result = await _process.ExecuteAsync("sudo", ["-n", "true"], timeoutSeconds: 5);
            return result.Success;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
