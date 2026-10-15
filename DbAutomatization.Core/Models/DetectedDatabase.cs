namespace DbAutomatization.Core.Models
{
    public class DetectedDatabase
    {
        public string Name { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public bool IsRunning { get; set; }
        public string? Error { get; set; }
    }
}