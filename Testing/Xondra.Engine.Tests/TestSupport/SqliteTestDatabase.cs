using Microsoft.Data.Sqlite;
using Xondra.Engine.Data;

namespace Xondra.Engine.Tests.TestSupport;

public static class SqliteTestDatabase
{
    public static SqliteConnection CreateCatalog(string directory)
    {
        var connection = OpenFile(directory, "catalog.db");
        SqliteSchemaInitializer.InitializeCatalog(connection);
        return connection;
    }

    public static SqliteConnection CreateConfig(string directory)
    {
        var connection = OpenFile(directory, "config.db");
        SqliteSchemaInitializer.InitializeConfig(connection);
        return connection;
    }

    private static SqliteConnection OpenFile(string directory, string fileName)
    {
        var connection = new SqliteConnection($"Data Source={Path.Combine(directory, fileName)};Pooling=False");
        connection.Open();
        return connection;
    }
}
