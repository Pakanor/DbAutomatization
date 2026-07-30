using DbAutomatization.Infrastructure.Linux;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class BackupRetentionManagerTests
{
    [Fact]
    public async Task CleanupAsync_RetentionCountZero_DoesNothing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "backup_test_1.bak"), "");
            File.WriteAllText(Path.Combine(tempDir, "backup_test_2.bak"), "");

            var manager = new BackupRetentionManager();
            await manager.CleanupAsync(tempDir, "backup_test", 0);

            Directory.GetFiles(tempDir).Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanupAsync_RemovesOldestFilesBeyondRetention()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "backup_test_1.bak");
            var file2 = Path.Combine(tempDir, "backup_test_2.bak");
            var file3 = Path.Combine(tempDir, "backup_test_3.bak");

            File.WriteAllText(file1, "");
            File.WriteAllText(file2, "");
            File.WriteAllText(file3, "");

            new FileInfo(file1).LastWriteTime = DateTime.Now.AddDays(-3);
            new FileInfo(file2).LastWriteTime = DateTime.Now.AddDays(-2);
            new FileInfo(file3).LastWriteTime = DateTime.Now.AddDays(-1);

            var manager = new BackupRetentionManager();
            await manager.CleanupAsync(tempDir, "backup_test", 2);

            var remaining = Directory.GetFiles(tempDir).Select(Path.GetFileName).ToList();
            remaining.Should().HaveCount(2);
            remaining.Should().Contain("backup_test_2.bak");
            remaining.Should().Contain("backup_test_3.bak");
            remaining.Should().NotContain("backup_test_1.bak");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanupAsync_DirectoryDoesNotExist_DoesNotThrow()
    {
        var manager = new BackupRetentionManager();
        var act = async () => await manager.CleanupAsync("/nonexistent/path", "backup", 5);
        await act.Should().NotThrowAsync();
    }
}
