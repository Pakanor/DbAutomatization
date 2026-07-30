using System.Reflection;
using DbAutomatization.Cli;
using DbAutomatization.Core.Models;
using DbAutomatization.Core.Services;
using FluentAssertions;
using Moq;

namespace DbAutomatization.Tests;

public class DatabaseWizardServiceTests
{
    private static readonly Type TemplateType = typeof(DatabaseWizardService).GetNestedType("DatabaseTemplate",
        System.Reflection.BindingFlags.NonPublic)!;
    private static readonly MethodInfo BuildMethod = typeof(DatabaseWizardService).GetMethod("BuildConfig",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

    private static DatabaseConfig BuildForTemplate(int templateIndex, string serviceName, string friendlyName)
    {
        var enumValues = TemplateType.GetEnumValues();
        var template = enumValues.GetValue(templateIndex);
        return (DatabaseConfig)BuildMethod.Invoke(null, [template, serviceName, friendlyName])!;
    }

    [Fact]
    public void BuildConfig_PostgreSQL_GeneratesCorrectStructure()
    {
        var config = BuildForTemplate(0, "postgresql", "PostgreSQL");

        config.FriendlyName.Should().Be("PostgreSQL");
        config.AptPackageName.Should().Be("postgresql");
        config.CliCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql"]);
        config.BackupCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "pg_dumpall"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "pg_dump", "--clean", "--if-exists", "-d", "{db}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "--set", "ON_ERROR_STOP=on", "-d", "{db}"]);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["sudo", "-u", "postgres", "psql", "-l", "-t", "-A"]);
    }

    [Fact]
    public void BuildConfig_Redis_GeneratesCorrectStructure()
    {
        var config = BuildForTemplate(3, "redis-server", "Redis");

        config.FriendlyName.Should().Be("Redis");
        config.AptPackageName.Should().Be("redis-server");
        config.CliCommand.Should().BeEquivalentTo(["redis-cli"]);
        config.PreBackupCommand.Should().BeEquivalentTo(["redis-cli", "SAVE"]);
        config.BackupCommand.Should().BeEquivalentTo(["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["sudo", "cp", "/var/lib/redis/dump.rdb", "{path}"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["sudo", "cp", "{path}", "/var/lib/redis/dump.rdb"]);
        config.ListDatabasesCommand.Should().BeEmpty();
    }

    [Fact]
    public void BuildConfig_MongoDB_HasArchiveCommands()
    {
        var config = BuildForTemplate(2, "mongod", "MongoDB");

        config.AptPackageName.Should().Be("mongodb-org");
        config.BackupCommand.Should().BeEquivalentTo(["mongodump", "--archive={path}"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["mongodump", "--db", "{db}", "--archive={path}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["mongorestore", "--archive={path}"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["mongorestore", "--nsInclude", "{db}.*", "--archive={path}"]);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["mongosh", "--quiet", "--eval", "db.adminCommand('listDatabases').databases.forEach(d => print(d.name))"]);
    }

    [Fact]
    public void BuildConfig_InfluxDB_HasOutputColumnIndex()
    {
        var config = BuildForTemplate(4, "influxdb", "InfluxDB");

        config.AptPackageName.Should().Be("influxdb");
        config.BackupCommand.Should().BeEquivalentTo(["influx", "backup", "{path}"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["influx", "backup", "-database", "{db}", "{path}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["influx", "restore", "{path}"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["influx", "restore", "-database", "{db}", "{path}"]);
        config.OutputColumnIndex.Should().Be(1);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["influx", "bucket", "list", "--hide-headers"]);
    }

    [Fact]
    public void BuildConfig_Custom_ProducesMinimalConfig()
    {
        var config = BuildForTemplate(5, "customdb", "Custom");

        config.FriendlyName.Should().Be("Custom");
        config.CliCommand.Should().BeEquivalentTo(["customdb"]);
        config.BackupCommand.Should().BeEmpty();
        config.RestoreCommand.Should().BeEmpty();
        config.SingleDatabaseBackupCommand.Should().BeEmpty();
        config.SingleDatabaseRestoreCommand.Should().BeEmpty();
        config.ListDatabasesCommand.Should().BeEmpty();
    }

    [Fact]
    public void BuildConfig_MariaDB_GeneratesCorrectStructure()
    {
        var config = BuildForTemplate(1, "mariadb", "MariaDB");

        config.FriendlyName.Should().Be("MariaDB");
        config.AptPackageName.Should().Be("mariadb-server");
        config.CliCommand.Should().BeEquivalentTo(["mariadb"]);
        config.BackupCommand.Should().BeEquivalentTo(["sudo", "mariadb-dump", "--all-databases"]);
        config.SingleDatabaseBackupCommand.Should().BeEquivalentTo(["sudo", "mariadb-dump", "{db}"]);
        config.RestoreCommand.Should().BeEquivalentTo(["sudo", "mariadb"]);
        config.SingleDatabaseRestoreCommand.Should().BeEquivalentTo(["sudo", "mariadb", "{db}"]);
        config.ListDatabasesCommand.Should().BeEquivalentTo(["sudo", "mariadb", "-BN", "-e", "SHOW DATABASES;"]);
    }
}
