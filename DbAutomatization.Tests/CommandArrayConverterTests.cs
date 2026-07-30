using System.Text.Json;
using DbAutomatization.Core.Models;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class CommandArrayConverterTests
{
    [Fact]
    public void OldFormat_ReturnsParsedArray()
    {
        string json = """{ "FriendlyName": "Test", "CliCommand": "sudo -u postgres psql" }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql"]);
    }

    [Fact]
    public void NewFormat_ReturnsExactArray()
    {
        string json = """{ "FriendlyName": "Test", "CliCommand": ["sudo", "-u", "postgres"] }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres"]);
    }

    [Fact]
    public void EmptyString_ReturnsEmptyArray()
    {
        string json = """{ "FriendlyName": "Test", "CliCommand": "" }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEmpty();
    }

    [Fact]
    public void Null_ReturnsEmptyArray()
    {
        string json = """{ "FriendlyName": "Test", "CliCommand": null }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEmpty();
    }

    [Fact]
    public void Default_ReturnsEmptyArray()
    {
        string json = """{ "FriendlyName": "Test" }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEmpty();
    }

    [Fact]
    public void PipeInCommand_IsNotWrappedInShell()
    {
        string json = """{ "FriendlyName": "Test", "CliCommand": "influx bucket list --hide-headers | awk '{print $2}'" }""";

        var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

        config.Should().NotBeNull();
        config!.CliCommand.Should().BeEquivalentTo(["influx", "bucket", "list", "--hide-headers", "|", "awk", "'{print", "$2}'"]);
    }
}
