using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbAutomatization.Core.Models;

public class DatabaseConfig
{
    public string EngineType { get; set; } = "systemd";

    public string FriendlyName { get; set; } = string.Empty;

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] CliCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] BackupCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] PreBackupCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] ListDatabasesCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] SingleDatabaseBackupCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] RestoreCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] SingleDatabaseRestoreCommand { get; set; } = [];

    public int BackupTimeoutSeconds { get; set; } = 300;

    public int RetentionCount { get; set; } = 0;

    public int OutputColumnIndex { get; set; } = 0;

    public ScheduleConfig? Schedule { get; set; }
}

public class ScheduleConfig
{
    public bool Enabled { get; set; }

    public string Type { get; set; } = string.Empty; // "interval" | "daily"

    public int? IntervalHours { get; set; }

    public string? DailyTime { get; set; } // HH:mm format

    public string? DatabaseName { get; set; }

    public bool Compress { get; set; } = true;
}

public class CommandArrayConverter : JsonConverter<string[]>
{
    public override bool HandleNull => true;

    public override string[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string value = reader.GetString() ?? "";
            return SplitCommand(value);
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                list.Add(reader.GetString() ?? "");
            }
            return [.. list];
        }

        return [];
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, options);
    }

    private static string[] SplitCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return [];

        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}