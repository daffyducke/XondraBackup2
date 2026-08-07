using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Scanning;
using Xondra.Engine.Vss;

namespace Xondra.Engine.Backup;

public class BackupRunner(
    IFileSystem fileSystem,
    IVssSnapshotProvider vssSnapshotProvider,
    JobSettingsRepository jobSettingsRepository,
    BackupRepository backupRepository,
    BackupSetRepository backupSetRepository,
    BackupSetEmptyDirRepository emptyDirRepository,
    LocalDriveRepository driveRepository,
    LocalDirectoryRepository directoryRepository,
    ErrorRepository errorRepository,
    IncrementalPlanner incrementalPlanner,
    FileBackupWorker fileBackupWorker)
{
    public long Run(long jobId)
    {
        var settingsJson = jobSettingsRepository.GetBackupSettingsJson(jobId)
            ?? throw new InvalidOperationException($"No backup settings found for job {jobId}.");
        var config = BackupConfig.Parse(settingsJson);

        var backupId = backupRepository.Start(config.ComputerGuid, DateTime.UtcNow, settingsJson);

        var provider = config.UseVss ? vssSnapshotProvider : new NullVssSnapshotProvider();
        using var snapshot = provider.CreateSnapshot(config.SourceDirectory);

        var scanResult = new DirectoryScanner(fileSystem).Scan(snapshot.SnapshotRoot);
        var scannedFiles = scanResult.Files
            .Select(file => Relocate(file, snapshot.SnapshotRoot, config.SourceDirectory))
            .ToList();

        var filesToProcess = incrementalPlanner.Plan(config.BackupType, backupId, scannedFiles);
        foreach (var file in filesToProcess)
            fileBackupWorker.BackupFile(backupId, file);

        foreach (var emptyDirectory in scanResult.EmptyDirectories)
        {
            var relocated = Relocate(emptyDirectory, snapshot.SnapshotRoot, config.SourceDirectory);
            var driveId = driveRepository.GetOrInsert(relocated.Drive);
            var dirId = directoryRepository.GetOrInsert(relocated.Directory);
            emptyDirRepository.Insert(backupId, dirId, driveId, error: null);
        }

        foreach (var scanError in scanResult.Errors)
            errorRepository.Insert(backupId, "Scan", scanError.Message);

        var fileCount = backupSetRepository.GetByBackupId(backupId).Count;
        var errorCount = errorRepository.GetByBackupId(backupId).Count;
        backupRepository.Complete(backupId, DateTime.UtcNow, fileCount, errorCount, status: "Done!");

        return backupId;
    }

    // A VSS snapshot's physical device path (e.g. \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\...)
    // is only valid for reading content during this run. Everything persisted to the catalog must use
    // the real, logical source path instead, so incremental matching and restore work across runs.
    private static ScannedFile Relocate(ScannedFile file, string physicalRoot, string logicalRoot)
    {
        var (drive, directory) = RelocateDriveAndDirectory(file.Drive, file.Directory, physicalRoot, logicalRoot);
        return file with { Drive = drive, Directory = directory };
    }

    private static ScannedDirectory Relocate(ScannedDirectory directory, string physicalRoot, string logicalRoot)
    {
        var (drive, dir) = RelocateDriveAndDirectory(directory.Drive, directory.Directory, physicalRoot, logicalRoot);
        return directory with { Drive = drive, Directory = dir };
    }

    private static (string Drive, string Directory) RelocateDriveAndDirectory(
        string drive, string directory, string physicalRoot, string logicalRoot)
    {
        if (string.Equals(physicalRoot, logicalRoot, StringComparison.OrdinalIgnoreCase))
            return (drive, directory);

        var physicalContainingDirectory = drive + directory;
        var logicalContainingDirectory = logicalRoot + physicalContainingDirectory[physicalRoot.Length..];

        var logicalDrive = Path.GetPathRoot(logicalContainingDirectory) ?? string.Empty;
        var logicalDirectory = logicalContainingDirectory[logicalDrive.Length..];
        return (logicalDrive, logicalDirectory);
    }
}
