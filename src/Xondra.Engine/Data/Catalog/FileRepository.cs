using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class FileRepository(SqliteConnection connection)
{
    public FileRecord? FindByHash(string originalFileHash)
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE OriginalFileHash = @originalFileHash";
        select.Parameters.AddWithValue("@originalFileHash", originalFileHash);
        return ReadSingle(select);
    }

    public FileRecord? GetById(long id)
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE ID = @id";
        select.Parameters.AddWithValue("@id", id);
        return ReadSingle(select);
    }

    public long Insert(string originalFileHash, string? origHmacSha512, long? filesize)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO File (OriginalFileHash, OrigHMACSHA512, Filesize)
            VALUES (@originalFileHash, @origHmacSha512, @filesize);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@originalFileHash", originalFileHash);
        insert.Parameters.AddWithValue("@origHmacSha512", (object?)origHmacSha512 ?? DBNull.Value);
        insert.Parameters.AddWithValue("@filesize", (object?)filesize ?? DBNull.Value);
        return (long)insert.ExecuteScalar()!;
    }

    public void MarkStored(long id, string backupHash, long filesizeCompressed)
    {
        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE File SET BackupHash = @backupHash, FilesizeCompressed = @filesizeCompressed
            WHERE ID = @id
            """;
        update.Parameters.AddWithValue("@backupHash", backupHash);
        update.Parameters.AddWithValue("@filesizeCompressed", filesizeCompressed);
        update.Parameters.AddWithValue("@id", id);
        update.ExecuteNonQuery();
    }

    public void SetVerified(long id, bool verified)
    {
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE File SET LocalVerified = @verified WHERE ID = @id";
        update.Parameters.AddWithValue("@verified", verified);
        update.Parameters.AddWithValue("@id", id);
        update.ExecuteNonQuery();
    }

    public IReadOnlyList<FileRecord> FindNotVerified()
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE LocalVerified IS NULL AND BackupHash IS NOT NULL";
        return ReadList(select);
    }

    public IReadOnlyList<FileRecord> FindAllStored()
    {
        using var select = connection.CreateCommand();
        select.CommandText = SelectColumns + "WHERE BackupHash IS NOT NULL";
        return ReadList(select);
    }

    public IReadOnlyList<FileRecord> FindByBackupId(long backupId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT DISTINCT f.ID, f.OriginalFileHash, f.OrigHMACSHA512, f.Filesize, f.LocalVerified, f.BackupHash, f.FilesizeCompressed
            FROM File f
            INNER JOIN BackupSet bs ON bs.FileID = f.ID
            WHERE bs.BackupID = @backupId AND f.BackupHash IS NOT NULL
            """;
        select.Parameters.AddWithValue("@backupId", backupId);
        return ReadList(select);
    }

    private const string SelectColumns =
        "SELECT ID, OriginalFileHash, OrigHMACSHA512, Filesize, LocalVerified, BackupHash, FilesizeCompressed FROM File ";

    private static FileRecord? ReadSingle(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRecord(reader) : null;
    }

    private static IReadOnlyList<FileRecord> ReadList(SqliteCommand command)
    {
        var results = new List<FileRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            results.Add(ReadRecord(reader));
        return results;
    }

    private static FileRecord ReadRecord(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetInt64(3),
        reader.IsDBNull(4) ? null : reader.GetBoolean(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetInt64(6));
}
