namespace DbAutomatization.Core.Services;

public interface IBashExecutor
{
    Task<string> ExecuteAsync(string command);
    Task ExecuteInteractiveAsync(string command);
}