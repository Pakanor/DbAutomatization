using System.IO;
using DbAutomatization.Core.Exceptions;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;

namespace DbAutomatization.Infrastructure.Linux;

public class BackupScheduler : IBackupScheduler
{
    private readonly IDatabaseConfigRepository _configRepo;
    private readonly IProcessExecutor _process;
    private const string SystemdDirectory = "/etc/systemd/system";
    private const string ServicePrefix = "dbautomatize-backup-";

    public BackupScheduler(IDatabaseConfigRepository configRepo, IProcessExecutor process)
    {
        _configRepo = configRepo;
        _process = process;
    }

    public async Task<ScheduleResult> CreateScheduleAsync(
        string serviceName,
        ScheduleConfig schedule,
        CancellationToken cancellationToken = default)
    {
        var config = _configRepo.Get(serviceName);
        if (config == null)
        {
            return new ScheduleResult(false, $"Brak konfiguracji dla '{serviceName}'.", -1);
        }

        string binaryPath = GetBinaryPath();
        string timerName = $"{ServicePrefix}{serviceName}";
        string serviceNameSystemd = $"{timerName}.service";
        string timerNameSystemd = $"{timerName}.timer";

        string serviceContent = GenerateServiceFile(binaryPath);
        string timerContent = GenerateTimerFile(schedule);

        try
        {
            await WriteSystemdFileAsync(serviceNameSystemd, serviceContent, cancellationToken);
            await WriteSystemdFileAsync(timerNameSystemd, timerContent, cancellationToken);

            var reloadResult = await _process.ExecuteAsync(
                "sudo", ["systemctl", "daemon-reload"],
                timeoutSeconds: 10,
                cancellationToken: cancellationToken);

            if (!reloadResult.Success)
            {
                return new ScheduleResult(false, $"Błąd przeładowania systemd: {reloadResult.Error}", reloadResult.ExitCode);
            }

            var enableResult = await _process.ExecuteAsync(
                "sudo", ["systemctl", "enable", "--now", timerNameSystemd],
                timeoutSeconds: 10,
                cancellationToken: cancellationToken);

            if (!enableResult.Success)
            {
                return new ScheduleResult(false, $"Błąd włączania timera: {enableResult.Error}", enableResult.ExitCode);
            }

            config.Schedule = schedule;
            _configRepo.Register(serviceName, config);

            return new ScheduleResult(true, $"Harmonogram backupu dla '{serviceName}' został utworzony i uruchomiony.", 0);
        }
        catch (UnauthorizedAccessException)
        {
            return new ScheduleResult(false,
                "Brak uprawnień do zapisu w /etc/systemd/system/. Uruchom program z sudo.",
                -1);
        }
        catch (Exception ex)
        {
            return new ScheduleResult(false, $"Błąd tworzenia harmonogramu: {ex.Message}", -1);
        }
    }

    public async Task<ScheduleResult> RemoveScheduleAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        string timerName = $"{ServicePrefix}{serviceName}";
        string serviceNameSystemd = $"{timerName}.service";
        string timerNameSystemd = $"{timerName}.timer";

        try
        {
            await _process.ExecuteAsync(
                "sudo", ["systemctl", "disable", "--now", timerNameSystemd],
                timeoutSeconds: 10,
                cancellationToken: cancellationToken);

            string servicePath = Path.Combine(SystemdDirectory, serviceNameSystemd);
            string timerPath = Path.Combine(SystemdDirectory, timerNameSystemd);

            if (File.Exists(servicePath))
                File.Delete(servicePath);
            if (File.Exists(timerPath))
                File.Delete(timerPath);

            await _process.ExecuteAsync(
                "sudo", ["systemctl", "daemon-reload"],
                timeoutSeconds: 10,
                cancellationToken: cancellationToken);

            var config = _configRepo.Get(serviceName);
            if (config != null)
            {
                config.Schedule = null;
                _configRepo.Register(serviceName, config);
            }

            return new ScheduleResult(true, $"Harmonogram backupu dla '{serviceName}' został usunięty.", 0);
        }
        catch (Exception ex)
        {
            return new ScheduleResult(false, $"Błąd usuwania harmonogramu: {ex.Message}", -1);
        }
    }

    public ScheduleConfig ParseScheduleInput(string input)
    {
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new DatabaseConfigurationException("Pusty opis harmonogramu.");
        }

        if (parts[0].Equals("hourly", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            if (int.TryParse(parts[1], out int hours) && hours > 0 && hours <= 24)
            {
                return new ScheduleConfig
                {
                    Enabled = true,
                    Type = "interval",
                    IntervalHours = hours
                };
            }
            throw new DatabaseConfigurationException("Nieprawidłowa liczba godzin (1-24).");
        }

        if (parts[0].Equals("daily", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
        {
            if (TimeOnly.TryParse(parts[1], out TimeOnly time))
            {
                return new ScheduleConfig
                {
                    Enabled = true,
                    Type = "daily",
                    DailyTime = time.ToString("HH:mm")
                };
            }
            throw new DatabaseConfigurationException("Nieprawidłowy format godziny. Użyj HH:mm (np. 03:00).");
        }

        throw new DatabaseConfigurationException(
            "Nieznany format harmonogramu. Użyj: 'hourly N' lub 'daily HH:mm'.");
    }

    private string GetBinaryPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string binaryName = "DbAutomatization.Cli";
        string fullPath = Path.Combine(baseDir, binaryName);

        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(baseDir, "dbautomate");
        }

        return fullPath;
    }

    private string GenerateServiceFile(string binaryPath)
    {
        return $@"[Unit]
Description=DbAutomatization Scheduled Backup
After=network.target

[Service]
Type=oneshot
ExecStart={binaryPath}
Environment=DBAUTOMATE_RUN_SCHEDULED=1
User=root
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target";
    }

    private string GenerateTimerFile(ScheduleConfig schedule)
    {
        string onCalendar = schedule.Type switch
        {
            "interval" => $"*:0/{schedule.IntervalHours}:00",
            "daily" => $"{schedule.DailyTime}:00",
            _ => throw new DatabaseConfigurationException("Nieobsługiwany typ harmonogramu.")
        };

        return $@"[Unit]
Description=Timer for DbAutomatization Backup

[Timer]
OnCalendar={onCalendar}
Persistent=true
RandomizedDelaySec=15m

[Install]
WantedBy=timers.target";
    }

    private async Task WriteSystemdFileAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.Combine(SystemdDirectory, fileName);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }
}