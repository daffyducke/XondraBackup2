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

    private const string SelectColumns =
        "SELECT ID, OriginalFileHash, OrigHMACSHA512, Filesize, LocalVerified, BackupHash, FilesizeCompressed FROM File ";

    private static FileRecord? ReadSingle(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new FileRecord(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt64(3),
            reader.IsDBNull(4) ? null : reader.GetBoolean(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt64(6));
    }
}
