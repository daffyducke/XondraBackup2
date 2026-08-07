using Microsoft.Data.Sqlite;
using Xondra.Engine.Scanning;

namespace Xondra.Engine.Backup;

// Wraps a FileBackupWorker whose repositories are bound to an in-memory SQLite
// connection, periodically flushing that connection to the real on-disk catalog
// via SQLite's online backup API (SqliteConnection.BackupDatabase) so a crash
// mid-run doesn't lose everything captured so far.
public class InMemoryFlushingFileBackupWorker(
    IFileBackupWorker inner, SqliteConnection inMemoryConnection, SqliteConnection onDiskConnection, int flushInterval)
    : IFileBackupWorker
{
    private int _processedCount;

    public void BackupFile(long backupId, ScannedFile file)
    {
        inner.BackupFile(backupId, file);

        _processedCount++;
        if (_processedCount % flushInterval == 0)
            Flush();
    }

    public void Flush() => inMemoryConnection.BackupDatabase(onDiskConnection);
}
