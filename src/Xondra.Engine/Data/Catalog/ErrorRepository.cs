using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class ErrorRepository(SqliteConnection connection)
{
    public long Insert(long backupId, string? procedureName, string error)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Error (BackupID, ProcedureName, Error)
            VALUES (@backupId, @procedureName, @error);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@backupId", backupId);
        insert.Parameters.AddWithValue("@procedureName", (object?)procedureName ?? DBNull.Value);
        insert.Parameters.AddWithValue("@error", error);
        return (long)insert.ExecuteScalar()!;
    }

    public IReadOnlyList<ErrorRecord> GetByBackupId(long backupId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT ID, BackupID, ProcedureName, Error FROM Error WHERE BackupID = @backupId";
        select.Parameters.AddWithValue("@backupId", backupId);

        var results = new List<ErrorRecord>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ErrorRecord(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3)));
        }
        return results;
    }
}
