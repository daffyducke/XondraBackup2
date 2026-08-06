using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xondra.Engine.Data;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data;

public class SqliteSchemaInitializerTests
{
    [Theory]
    [InlineData("Backup")]
    [InlineData("BackupSet")]
    [InlineData("BackupSetEmptyDir")]
    [InlineData("Error")]
    [InlineData("File")]
    [InlineData("LocalDrive")]
    [InlineData("LocalDirectory")]
    [InlineData("LocalFilename")]
    public void InitializeCatalog_creates_the_expected_tables(string tableName)
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);

        TableExists(connection, tableName).Should().BeTrue();
    }

    [Fact]
    public void InitializeCatalog_is_safe_to_call_twice()
    {
        using var temp = new TempDirectory();
        var connection = new SqliteConnection($"Data Source={Path.Combine(temp.FullPath, "catalog.db")};Pooling=False");
        connection.Open();

        SqliteSchemaInitializer.InitializeCatalog(connection);
        var act = () => SqliteSchemaInitializer.InitializeCatalog(connection);

        act.Should().NotThrow();
        connection.Dispose();
    }

    [Theory]
    [InlineData("Job")]
    [InlineData("Attribute")]
    [InlineData("Value")]
    [InlineData("MultiValue")]
    public void InitializeConfig_creates_the_expected_tables(string tableName)
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateConfig(temp.FullPath);

        TableExists(connection, tableName).Should().BeTrue();
    }

    [Fact]
    public void InitializeConfig_creates_the_SettingsJson_view()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateConfig(temp.FullPath);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'view' AND name = 'Settings_Json'";
        command.ExecuteScalar().Should().Be("Settings_Json");
    }

    [Fact]
    public void InitializeConfig_is_safe_to_call_twice()
    {
        using var temp = new TempDirectory();
        var connection = new SqliteConnection($"Data Source={Path.Combine(temp.FullPath, "config.db")};Pooling=False");
        connection.Open();

        SqliteSchemaInitializer.InitializeConfig(connection);
        var act = () => SqliteSchemaInitializer.InitializeConfig(connection);

        act.Should().NotThrow();
        connection.Dispose();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        return command.ExecuteScalar() is not null;
    }
}
