using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly List<string> _createdFiles;
    private readonly string _backupDir;
    private readonly string _serviceName;

    public IntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "databases.json");
        
        // Use a dedicated temp backup directory for this test to ensure isolation
        _backupDir = Path.Combine(_tempDir, "backups");
        Directory.CreateDirectory(_backupDir);
        Environment.SetEnvironmentVariable("DBAUTOMATE_BACKUP_DIR", _backupDir);
        
        _serviceName = $"testdb_{Guid.NewGuid():N}";
        _createdFiles = [];
    }

    public void Dispose()
    {
        foreach (var file in _createdFiles)
        {
            TryDeleteFile(file);
        }
        TryDeleteFile(_configPath);
        TryDeleteDir(_tempDir);
    }

    [Fact]
    public async Task FullBackupCycle_RegisterBackupRetention()
    {
        var configRepo = new DatabaseConfigRepository(_configPath);
        var executor = new ProcessExecutor();
        var retention = new BackupRetentionManager();
        var backupService = new BackupService(configRepo, executor, retention);

        configRepo.Register(_serviceName, new DatabaseConfig
        {
            FriendlyName = "TestDB",
            BackupCommand = ["touch", "{path}"],
            RetentionCount = 2
        });

        CleanupTestFiles();

        var result1 = await backupService.CreateBackupAsync(_serviceName);
        result1.Success.Should().BeTrue();
        File.Exists(result1.FilePath).Should().BeTrue();
        _createdFiles.Add(result1.FilePath);

        var result2 = await backupService.CreateBackupAsync(_serviceName);
        result2.Success.Should().BeTrue();
        _createdFiles.Add(result2.FilePath);

        Directory.GetFiles(_backupDir, $"backup_{_serviceName}*").Length.Should().Be(2);

        var result3 = await backupService.CreateBackupAsync(_serviceName);
        result3.Success.Should().BeTrue();
        _createdFiles.Add(result3.FilePath);

        var remaining = Directory.GetFiles(_backupDir, $"backup_{_serviceName}*")
            .Select(Path.GetFileName)
            .OrderBy(n => n)
            .ToList();

        remaining.Count.Should().Be(2);
        remaining.Should().BeEquivalentTo(
            Path.GetFileName(result2.FilePath),
            Path.GetFileName(result3.FilePath));
    }

    private void CleanupTestFiles()
    {
        if (!Directory.Exists(_backupDir)) return;
        foreach (var f in Directory.GetFiles(_backupDir, $"backup_{_serviceName}*"))
            TryDeleteFile(f);
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
