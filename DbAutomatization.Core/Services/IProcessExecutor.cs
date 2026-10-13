using System.IO;
using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IProcessExecutor
{
    Task<ProcessResult> ExecuteAsync(
        string fileName,
        string[] arguments,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default);

    Task ExecuteInteractiveAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken = default);

    Task<ProcessResult> ExecuteAndStreamOutputAsync(
        string fileName,
        string[] arguments,
        Stream outputStream,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default);

    Task<ProcessResult> ExecuteWithStdinFileAsync(
        string fileName,
        string[] arguments,
        string stdinFilePath,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default);

    Task<ProcessResult> ExecuteWithStdinStreamAsync(
        string fileName,
        string[] arguments,
        Stream inputStream,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default);
}
