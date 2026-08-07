using Xondra.Engine.Scanning;

namespace Xondra.Engine.Backup;

public interface IFileBackupWorker
{
    void BackupFile(long backupId, ScannedFile file);
}
