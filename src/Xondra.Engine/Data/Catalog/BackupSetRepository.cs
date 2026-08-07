using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class BackupSetRepository(SqliteConnection connection)
{
    public long Insert(long backupId, long dirId, long fileId, long filenameId, long driveId,
        int? error, int? attributes, string? creationTime, string? lastWriteTime)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO BackupSet (BackupID, DirID, FileID, FilenameID, DriveID, Error, Attributes, CreationTime, LastWriteTime)
            VALUES (@backupId, @dirId, @fileId, @filenameId, @driveId, @error, @attributes, @creationTime, @lastWriteTime);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@backupId", backupId);
        insert.Parameters.AddWithValue("@dirId", dirId);
        insert.Parameters.AddWithValue("@fileId", fileId);
        insert.Parameters.AddWithValue("@filenameId", filenameId);
        insert.Parameters.AddWithValue("@driveId", driveId);
        insert.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
        insert.Parameters.AddWithValue("@attributes", (object?)attributes ?? DBNull.Value);
        insert.Parameters.AddWithValue("@creationTime", (object?)creationTime ?? DBNull.Value);
        insert.Parameters.AddWithValue("@lastWriteTime", (object?)lastWriteTime ?? DBNull.Value);
        return (long)insert.ExecuteScalar()!;
    }

    public long CopyForward(long backupSetId, long newBackupId)
    {
        var source = GetById(backupSetId)
            ?? throw new InvalidOperationException($"BackupSet row {backupSetId} does not exist.");

        return Insert(newBackupId, source.DirId, source.FileId, source.FilenameId, source.DriveId,
            source.Error, source.Attributes, source.CreationTime, source.LastWriteTime);
    }

    public BackupSetRecord? GetById(long id)
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE ID = @id";
        select.Parameters.AddWithValue("@id", id);
        return ReadSingle(select);
    }

    public IReadOnlyList<BackupSetRecord> GetByBackupId(long backupId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE BackupID = @backupId";
        select.Parameters.AddWithValue("@backupId", backupId);

        var results = new List<BackupSetRecord>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
            results.Add(Read(reader));
        return results;
    }

    public BackupSetRecord? FindLatestForPath(long driveId, long dirId, long filenameId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns +
            "WHERE DriveID = @driveId AND DirID = @dirId AND FilenameID = @filenameId ORDER BY BackupID DESC LIMIT 1";
        select.Parameters.AddWithValue("@driveId", driveId);
        select.Parameters.AddWithValue("@dirId", dirId);
        select.Parameters.AddWithValue("@filenameId", filenameId);
        return ReadSingle(select);
    }

    public IReadOnlyList<RestorableFileRecord> GetRestorableFiles(long backupId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT f.ID, ld.Drive, ldir.Directory, lf.Filename, f.OriginalFileHash, f.BackupHash, bs.Attributes, bs.CreationTime, bs.LastWriteTime
            FROM BackupSet bs
            INNER JOIN File f ON f.ID = bs.FileID
            INNER JOIN LocalDrive ld ON ld.ID = bs.DriveID
            INNER JOIN LocalDirectory ldir ON ldir.ID = bs.DirID
            INNER JOIN LocalFilename lf ON lf.ID = bs.FilenameID
            WHERE bs.BackupID = @backupId AND f.LocalVerified = 1 AND f.BackupHash IS NOT NULL
            """;
        select.Parameters.AddWithValue("@backupId", backupId);

        var results = new List<RestorableFileRecord>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new RestorableFileRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return results;
    }

    private const string SelectColumns =
        "SELECT ID, BackupID, DirID, FileID, FilenameID, DriveID, Error, Attributes, CreationTime, LastWriteTime FROM BackupSet ";

    private static BackupSetRecord? ReadSingle(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    private static BackupSetRecord Read(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetInt64(1),
        reader.GetInt64(2),
        reader.GetInt64(3),
        reader.GetInt64(4),
        reader.GetInt64(5),
        reader.IsDBNull(6) ? null : reader.GetInt32(6),
        reader.IsDBNull(7) ? null : reader.GetInt32(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9));
}
