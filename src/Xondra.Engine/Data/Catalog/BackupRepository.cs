using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Catalog;

public class BackupRepository(SqliteConnection connection)
{
    public long Start(string? computerGuid, DateTime startDate, string? settingsJson)
    {
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO Backup (ComputerGUID, StartDate, FileCount, ErrorCount, Status, SettingsJSON)
            VALUES (@computerGuid, @startDate, 0, 0, 'Running', @settingsJson);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@computerGuid", (object?)computerGuid ?? DBNull.Value);
        insert.Parameters.AddWithValue("@startDate", startDate);
        insert.Parameters.AddWithValue("@settingsJson", (object?)settingsJson ?? DBNull.Value);
        return (long)insert.ExecuteScalar()!;
    }

    public void Complete(long id, DateTime endDate, int fileCount, int errorCount, string status)
    {
        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE Backup SET EndDate = @endDate, FileCount = @fileCount, ErrorCount = @errorCount, Status = @status
            WHERE ID = @id
            """;
        update.Parameters.AddWithValue("@endDate", endDate);
        update.Parameters.AddWithValue("@fileCount", fileCount);
        update.Parameters.AddWithValue("@errorCount", errorCount);
        update.Parameters.AddWithValue("@status", status);
        update.Parameters.AddWithValue("@id", id);
        update.ExecuteNonQuery();
    }

    public BackupRecord? GetById(long id)
    {
        using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT ID, ComputerGUID, StartDate, EndDate, FileCount, ErrorCount, Status, SettingsJSON
            FROM Backup WHERE ID = @id
            """;
        select.Parameters.AddWithValue("@id", id);

        using var reader = select.ExecuteReader();
        if (!reader.Read())
            return null;

        return new BackupRecord(
            reader.GetInt64(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }
}
