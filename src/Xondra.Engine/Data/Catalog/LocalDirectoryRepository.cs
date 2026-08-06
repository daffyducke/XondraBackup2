using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class LocalDirectoryRepository(SqliteConnection connection)
{
    public long GetOrInsert(string directory)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT ID FROM LocalDirectory WHERE Directory = @directory";
        select.Parameters.AddWithValue("@directory", directory);
        if (select.ExecuteScalar() is { } existingId)
            return Convert.ToInt64(existingId);

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO LocalDirectory (Directory) VALUES (@directory); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("@directory", directory);
        return (long)insert.ExecuteScalar()!;
    }
}
