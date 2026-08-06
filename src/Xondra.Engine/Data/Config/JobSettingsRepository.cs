using Microsoft.Data.Sqlite;

namespace Xondra.Engine.Data.Config;

public class JobSettingsRepository(SqliteConnection connection)
{
    public string? GetBackupSettingsJson(long jobId)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT BackupSettings FROM Settings_Json WHERE JobID = @jobId";
        select.Parameters.AddWithValue("@jobId", jobId);
        return select.ExecuteScalar() as string;
    }
}
