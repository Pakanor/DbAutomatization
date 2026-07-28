using System.Text.Json;
using System.Text.Json.Serialization;

namespace DbAutomatization.Core.Models;

public class DatabaseConfig
{
    public string FriendlyName { get; set; } = string.Empty;

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] CliCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] BackupCommand { get; set; } = [];

    [JsonConverter(typeof(CommandArrayConverter))]
    public string[] ListDatabasesCommand { get; set; } = [];
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

        bool needsShell = command.Contains('|')
                       || command.Contains('>')
                       || command.Contains("&&")
                       || command.Contains(';');

        if (needsShell)
            return ["/bin/bash", "-c", command];

        return command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}