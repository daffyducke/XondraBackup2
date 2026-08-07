using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xondra.Engine.Backup;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Restore;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xondra.Engine.Verify;
using Xunit;

namespace Xondra.Engine.Tests.Restore;

public class RestoreServiceTests
{
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

    private sealed record Harness(
        BackupRunner BackupRunner, FileVerifier Verifier, RestoreService RestoreService,
        ErrorRepository ErrorRepository, FileRepository FileRepository);

    private static Harness Build(SqliteConnection catalogConnection, SqliteConnection cfgConnection, string sourceRoot, string blobRoot)
    {
        var fileSystem = new WindowsFileSystem();
        var blobStore = new BlobStore(blobRoot);
        var driveRepository = new LocalDriveRepository(catalogConnection);
        var directoryRepository = new LocalDirectoryRepository(catalogConnection);
        var filenameRepository = new LocalFilenameRepository(catalogConnection);
        var fileRepository = new FileRepository(catalogConnection);
        var backupSetRepository = new BackupSetRepository(catalogConnection);
        var backupRepository = new BackupRepository(catalogConnection);
        var errorRepository = new ErrorRepository(catalogConnection);
        var emptyDirRepository = new BackupSetEmptyDirRepository(catalogConnection);
        var jobSettingsRepository = new JobSettingsRepository(cfgConnection);
        var vssProvider = new FakeVssSnapshotProvider(sourceRoot);

        var incrementalPlanner = new IncrementalPlanner(driveRepository, directoryRepository, filenameRepository, backupSetRepository);
        var fileBackupWorker = new FileBackupWorker(fileSystem, blobStore, fileRepository, backupSetRepository,
            driveRepository, directoryRepository, filenameRepository, errorRepository);
        var backupRunner = new BackupRunner(fileSystem, vssProvider, jobSettingsRepository, backupRepository,
            backupSetRepository, emptyDirRepository, driveRepository, directoryRepository, errorRepository,
            incrementalPlanner, fileBackupWorker);

        var verifier = new FileVerifier(fileRepository, blobStore);
        var restoreService = new RestoreService(fileSystem, blobStore, backupSetRepository, emptyDirRepository, errorRepository);

        return new Harness(backupRunner, verifier, restoreService, errorRepository, fileRepository);
    }

    [Fact]
    public void Restore_round_trips_a_full_backup_including_dedup_and_an_empty_directory()
    {
        using var dbTemp = new TempDirectory();
        using var sourceTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var targetTemp = new TempDirectory();
        using var catalogConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        using var cfgConnection = SqliteTestDatabase.CreateConfig(dbTemp.FullPath);

        var fileA = Path.Combine(sourceTemp.FullPath, "a.txt");
        var fileB = Path.Combine(sourceTemp.FullPath, "b.txt");
        File.WriteAllText(fileA, "duplicate content");
        File.WriteAllText(fileB, "duplicate content");
        Directory.CreateDirectory(Path.Combine(sourceTemp.FullPath, "empty"));

        var expectedCreationTime = File.GetCreationTimeUtc(fileA);
        var expectedLastWriteTime = File.GetLastWriteTimeUtc(fileA);
        var expectedAttributes = File.GetAttributes(fileA);
        expectedAttributes.HasFlag(FileAttributes.Archive).Should().BeTrue("newly created files carry the Archive bit before backup clears it");

        SeedJobSettings(cfgConnection, sourceTemp.FullPath);
        var h = Build(catalogConnection, cfgConnection, sourceTemp.FullPath, blobTemp.FullPath);

        var backupId = h.BackupRunner.Run(jobId: 1);
        h.Verifier.VerifyFiles(VerifyMode.CurrentBackup, backupId);

        h.RestoreService.RestoreFiles(backupId, targetTemp.FullPath);
        h.RestoreService.RestoreEmptyDirectories(backupId, targetTemp.FullPath);

        // BackupSet.Directory records the full source directory minus its drive letter (not a path
        // relative to some backup root), so restore reconstructs that whole structure under targetRoot.
        var sourceRootBelowDrive = sourceTemp.FullPath[Path.GetPathRoot(sourceTemp.FullPath)!.Length..];
        var restoredRoot = Path.Combine(targetTemp.FullPath, sourceRootBelowDrive);
        var restoredA = Path.Combine(restoredRoot, "a.txt");
        var restoredB = Path.Combine(restoredRoot, "b.txt");
        File.Exists(restoredA).Should().BeTrue();
        File.Exists(restoredB).Should().BeTrue();
        File.ReadAllText(restoredA).Should().Be("duplicate content");
        File.ReadAllText(restoredB).Should().Be("duplicate content");
        Directory.Exists(Path.Combine(restoredRoot, "empty")).Should().BeTrue();

        File.GetCreationTimeUtc(restoredA).Should().Be(expectedCreationTime);
        File.GetLastWriteTimeUtc(restoredA).Should().Be(expectedLastWriteTime);
        File.GetAttributes(restoredA).Should().Be(expectedAttributes);
    }

    [Fact]
    public void RestoreFiles_deletes_a_restored_file_that_fails_re_verification()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var targetTemp = new TempDirectory();
        using var catalogConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        using var cfgConnection = SqliteTestDatabase.CreateConfig(dbTemp.FullPath);

        var fileSystem = new WindowsFileSystem();
        var blobStore = new BlobStore(blobTemp.FullPath);
        var driveRepository = new LocalDriveRepository(catalogConnection);
        var directoryRepository = new LocalDirectoryRepository(catalogConnection);
        var filenameRepository = new LocalFilenameRepository(catalogConnection);
        var fileRepository = new FileRepository(catalogConnection);
        var backupSetRepository = new BackupSetRepository(catalogConnection);
        var backupRepository = new BackupRepository(catalogConnection);
        var errorRepository = new ErrorRepository(catalogConnection);
        var emptyDirRepository = new BackupSetEmptyDirRepository(catalogConnection);

        const string originalHash =
            "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";
        const string backupHash =
            "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3";
        blobStore.Write(backupHash, new MemoryStream(Encoding.UTF8.GetBytes("not a valid encrypted blob")));

        var backupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var driveId = driveRepository.GetOrInsert(@"C:\");
        var dirId = directoryRepository.GetOrInsert("root");
        var filenameId = filenameRepository.GetOrInsert("corrupted.txt");
        var fileId = fileRepository.Insert(originalHash, "hmac-value", 5);
        fileRepository.MarkStored(fileId, backupHash, 5);
        fileRepository.SetVerified(fileId, true);
        backupSetRepository.Insert(backupId, dirId, fileId, filenameId, driveId, null, null, null, null);

        var restoreService = new RestoreService(fileSystem, blobStore, backupSetRepository, emptyDirRepository, errorRepository);

        restoreService.RestoreFiles(backupId, targetTemp.FullPath);

        var restoredPath = Path.Combine(targetTemp.FullPath, "root", "corrupted.txt");
        File.Exists(restoredPath).Should().BeFalse();
        errorRepository.GetByBackupId(backupId).Should().ContainSingle();
    }
}
