using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xondra.Engine.Backup;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class BackupRunnerInMemoryModeTests
{
    [Fact]
    public void BackupRunner_produces_the_same_end_state_whether_run_on_disk_or_in_memory_then_flushed()
    {
        using var cfgTemp = new TempDirectory();
        using var sourceTemp = new TempDirectory();
        using var onDiskCatalogTemp = new TempDirectory();
        using var onDiskBlobTemp = new TempDirectory();
        using var flushedCatalogTemp = new TempDirectory();
        using var flushedBlobTemp = new TempDirectory();
        using var cfgConnection = SqliteTestDatabase.CreateConfig(cfgTemp.FullPath);

        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "b.txt"), "beta");
        SeedJobSettings(cfgConnection, sourceTemp.FullPath);

        using var onDiskCatalogConnection = SqliteTestDatabase.CreateCatalog(onDiskCatalogTemp.FullPath);
        var onDiskBackupId = RunPipeline(onDiskCatalogConnection, onDiskCatalogConnection, cfgConnection,
            onDiskBlobTemp.FullPath, sourceTemp.FullPath, flushInterval: null);

        using var inMemoryConnection = SqliteTestDatabase.CreateInMemoryCatalog();
        using var flushedConnection = SqliteTestDatabase.CreateCatalog(flushedCatalogTemp.FullPath);
        var inMemoryBackupId = RunPipeline(inMemoryConnection, flushedConnection, cfgConnection,
            flushedBlobTemp.FullPath, sourceTemp.FullPath, flushInterval: 1);

        var onDiskRows = new BackupSetRepository(onDiskCatalogConnection).GetByBackupId(onDiskBackupId);
        var flushedRows = new BackupSetRepository(flushedConnection).GetByBackupId(inMemoryBackupId);
        flushedRows.Should().HaveCount(onDiskRows.Count);

        var onDiskBackup = new BackupRepository(onDiskCatalogConnection).GetById(onDiskBackupId);
        var flushedBackup = new BackupRepository(flushedConnection).GetById(inMemoryBackupId);
        flushedBackup!.Status.Should().Be(onDiskBackup!.Status);
        flushedBackup.FileCount.Should().Be(onDiskBackup.FileCount);
        flushedBackup.ErrorCount.Should().Be(onDiskBackup.ErrorCount);

        // Every processed file's blob really did land on disk via the flush, not just its catalog row.
        var flushedFileRepository = new FileRepository(flushedConnection);
        foreach (var row in flushedRows)
        {
            var file = flushedFileRepository.GetById(row.FileId);
            file!.BackupHash.Should().NotBeNull();
            new BlobStore(flushedBlobTemp.FullPath).Exists(file.BackupHash!).Should().BeTrue();
        }
    }

    private static long RunPipeline(SqliteConnection repoConnection, SqliteConnection flushDestinationConnection,
        SqliteConnection cfgConnection, string blobRoot, string sourceRoot, int? flushInterval)
    {
        var fileSystem = new WindowsFileSystem();
        var blobStore = new BlobStore(blobRoot);
        var driveRepository = new LocalDriveRepository(repoConnection);
        var directoryRepository = new LocalDirectoryRepository(repoConnection);
        var filenameRepository = new LocalFilenameRepository(repoConnection);
        var fileRepository = new FileRepository(repoConnection);
        var backupSetRepository = new BackupSetRepository(repoConnection);
        var backupRepository = new BackupRepository(repoConnection);
        var errorRepository = new ErrorRepository(repoConnection);
        var emptyDirRepository = new BackupSetEmptyDirRepository(repoConnection);
        var jobSettingsRepository = new JobSettingsRepository(cfgConnection);
        var vssProvider = new FakeVssSnapshotProvider(sourceRoot);

        var incrementalPlanner = new IncrementalPlanner(driveRepository, directoryRepository, filenameRepository, backupSetRepository);
        IFileBackupWorker fileBackupWorker = new FileBackupWorker(fileSystem, blobStore, fileRepository, backupSetRepository,
            driveRepository, directoryRepository, filenameRepository, errorRepository);

        InMemoryFlushingFileBackupWorker? flushingWorker = null;
        if (flushInterval is { } interval)
        {
            flushingWorker = new InMemoryFlushingFileBackupWorker(fileBackupWorker, repoConnection, flushDestinationConnection, interval);
            fileBackupWorker = flushingWorker;
        }

        var runner = new BackupRunner(fileSystem, vssProvider, jobSettingsRepository, backupRepository,
            backupSetRepository, emptyDirRepository, driveRepository, directoryRepository, errorRepository,
            incrementalPlanner, fileBackupWorker);

        var backupId = runner.Run(jobId: 1);
        flushingWorker?.Flush();
        return backupId;
    }

    private static void SeedJobSettings(SqliteConnection cfgConnection, string sourceDirectory)
    {
        Execute(cfgConnection, "INSERT INTO Job (ID, Type) VALUES (1, 'Backup')");
        Execute(cfgConnection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (1, 'SourceDirectory', 0, 'Backup')");
        Execute(cfgConnection, $"INSERT INTO Value (AttributeID, Value, JobID) VALUES (1, '{sourceDirectory}', 1)");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
