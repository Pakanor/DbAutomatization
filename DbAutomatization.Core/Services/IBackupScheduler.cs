using DbAutomatization.Core.Models;

namespace DbAutomatization.Core.Services;

public interface IBackupScheduler
{
    Task<ScheduleResult> CreateScheduleAsync(
        string serviceName,
        ScheduleConfig schedule,
        CancellationToken cancellationToken = default);

    Task<ScheduleResult> RemoveScheduleAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    ScheduleConfig ParseScheduleInput(string input);
}

public record ScheduleResult(bool Success, string Message, int ExitCode);