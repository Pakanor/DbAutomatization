using DbAutomatization.Core.Services;
using FluentAssertions;

namespace DbAutomatization.Tests;

public class CommandTemplateTests
{
    [Fact]
    public void Substitute_ReplacesPathServiceAndDatabase()
    {
        string[] command = ["docker", "exec", "-i", "{serviceName}", "pg_dump", "-d", "{db}", "--file={path}"];

        var result = CommandTemplate.Substitute(command, serviceName: "postgres-main", path: "/tmp/backup.bak", databaseName: "mydb");

        result.Should().BeEquivalentTo(
            ["docker", "exec", "-i", "postgres-main", "pg_dump", "-d", "mydb", "--file=/tmp/backup.bak"]);
    }

    [Fact]
    public void Substitute_WithoutDatabase_LeavesDbPlaceholderIntact()
    {
        string[] command = ["sudo", "psql", "-d", "{db}"];

        var result = CommandTemplate.Substitute(command, serviceName: "postgresql");

        result.Should().BeEquivalentTo(["sudo", "psql", "-d", "{db}"]);
    }

    [Fact]
    public void Substitute_WithoutServiceAndPath_LeavesPlaceholdersIntact()
    {
        string[] command = ["{serviceName}", "{path}"];

        var result = CommandTemplate.Substitute(command);

        result.Should().BeEquivalentTo(["{serviceName}", "{path}"]);
    }

    [Fact]
    public void Substitute_EmptyCommand_ReturnsEmpty()
    {
        var result = CommandTemplate.Substitute([], serviceName: "x", path: "y", databaseName: "z");

        result.Should().BeEmpty();
    }
}
