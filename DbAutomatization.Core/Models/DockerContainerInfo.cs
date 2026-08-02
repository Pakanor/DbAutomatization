namespace DbAutomatization.Core.Models;

public class DockerContainerInfo
{
    public string Name { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Family { get; set; } = string.Empty;

    public string FamilyTitle { get; set; } = string.Empty;

    public bool IsRunning { get; set; }

    public DatabaseConfig Config { get; set; } = new();
}
