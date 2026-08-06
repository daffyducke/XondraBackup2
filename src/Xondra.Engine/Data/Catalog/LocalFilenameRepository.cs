using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class LocalFilenameRepository(SqliteConnection connection)
{
    public long GetOrInsert(string filename)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT ID FROM LocalFilename WHERE Filename = @filename";
        select.Parameters.AddWithValue("@filename", filename);
        if (select.ExecuteScalar() is { } existingId)
            return Convert.ToInt64(existingId);

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO LocalFilename (Filename) VALUES (@filename); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("@filename", filename);
        return (long)insert.ExecuteScalar()!;
    }
}
