using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Scanning;

namespace Xondra.Engine.Backup;

public class IncrementalPlanner(
    LocalDriveRepository driveRepository,
    LocalDirectoryRepository directoryRepository,
    LocalFilenameRepository filenameRepository,
    BackupSetRepository backupSetRepository)
{
    private const string ArchiveBitBackupType = "ARCHIVEBIT";

    public IReadOnlyList<ScannedFile> Plan(string backupType, long newBackupId, IReadOnlyList<ScannedFile> files)
    {
        if (backupType != ArchiveBitBackupType)
            return files;

        var filesToProcess = new List<ScannedFile>();
        foreach (var file in files)
        {
            if (!file.ArchiveBitSet && TryCopyForward(newBackupId, file))
                continue;

            filesToProcess.Add(file);
        }
        return filesToProcess;
    }

    private bool TryCopyForward(long newBackupId, ScannedFile file)
    {
        var driveId = driveRepository.GetOrInsert(file.Drive);
        var dirId = directoryRepository.GetOrInsert(file.Directory);
        var filenameId = filenameRepository.GetOrInsert(file.Filename);

        var previous = backupSetRepository.FindLatestForPath(driveId, dirId, filenameId);
        if (previous is null)
            return false;

        backupSetRepository.CopyForward(previous.Id, newBackupId);
        return true;
    }
}
