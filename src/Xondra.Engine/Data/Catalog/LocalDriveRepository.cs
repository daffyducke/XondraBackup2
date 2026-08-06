using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class LocalDriveRepository(SqliteConnection connection)
{
    public long GetOrInsert(string drive)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT ID FROM LocalDrive WHERE Drive = @drive";
        select.Parameters.AddWithValue("@drive", drive);
        if (select.ExecuteScalar() is { } existingId)
            return Convert.ToInt64(existingId);

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO LocalDrive (Drive) VALUES (@drive); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("@drive", drive);
        return (long)insert.ExecuteScalar()!;
    }
}
