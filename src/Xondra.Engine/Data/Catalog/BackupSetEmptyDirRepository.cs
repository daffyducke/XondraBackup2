using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class BackupSetEmptyDirRepository(SqliteConnection connection)
{
    public long Insert(long backupId, long dirId, long driveId, int? error)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO BackupSetEmptyDir (BackupID, DirID, DriveID, Error)
            VALUES (@backupId, @dirId, @driveId, @error);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@backupId", backupId);
        insert.Parameters.AddWithValue("@dirId", dirId);
        insert.Parameters.AddWithValue("@driveId", driveId);
        insert.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
        return (long)insert.ExecuteScalar()!;
    }

    public IReadOnlyList<BackupSetEmptyDirRecord> GetByBackupId(long backupId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT ID, BackupID, DirID, DriveID, Error FROM BackupSetEmptyDir WHERE BackupID = @backupId
            """;
        select.Parameters.AddWithValue("@backupId", backupId);

        var results = new List<BackupSetEmptyDirRecord>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new BackupSetEmptyDirRecord(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4)));
        }
        return results;
    }
}
