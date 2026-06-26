namespace DbAutomatization.Core.Models;

public class DatabaseConfig{
    public string FriendlyName { get; set; } = string.Empty;
    public string CliCommand { get; set; } = string.Empty;
    public string BackupCommand { get; set; } = string.Empty;
    public string ListDatabasesCommand { get; set; } = string.Empty;

}