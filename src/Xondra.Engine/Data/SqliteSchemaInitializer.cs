using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data;

public static class SqliteSchemaInitializer
{
    public static void InitializeCatalog(SqliteConnection connection) =>
        Initialize(connection, "Xondra.Engine.Data.Resources.Xondra.dat.DDL.sql", "Backup");

    public static void InitializeConfig(SqliteConnection connection) =>
        Initialize(connection, "Xondra.Engine.Data.Resources.Xondra.cfg.DDL.sql", "Job");

    private static void Initialize(SqliteConnection connection, string resourceName, string bootstrapCheckTable)
    {
        if (TableExists(connection, bootstrapCheckTable))
            return;

        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedDdl(resourceName);
        command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        return command.ExecuteScalar() is not null;
    }

    private static string ReadEmbeddedDdl(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded DDL resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
