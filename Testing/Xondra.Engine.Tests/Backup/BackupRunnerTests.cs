using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xondra.Engine.Backup;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Hashing;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xondra.Engine.Vss;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class BackupRunnerTests
{
    private static void SeedJobSettings(SqliteConnection cfgConnection, string sourceDirectory, bool useVss, string backupType)
    {
        Execute(cfgConnection, "INSERT INTO Job (ID, Type) VALUES (1, 'Backup')");
        Execute(cfgConnection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (1, 'SourceDirectory', 0, 'Backup')");
        Execute(cfgConnection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (2, 'UseVSS', 0, 'Backup')");
        Execute(cfgConnection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (3, 'BackupType', 0, 'Backup')");
        Execute(cfgConnection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (4, 'ComputerGUID', 0, 'Backup')");
        Execute(cfgConnection, $"INSERT INTO Value (AttributeID, Value, JobID) VALUES (1, '{sourceDirectory}', 1)");
        Execute(cfgConnection, $"INSERT INTO Value (AttributeID, Value, JobID) VALUES (2, '{useVss}', 1)");
        Execute(cfgConnection, $"INSERT INTO Value (AttributeID, Value, JobID) VALUES (3, '{backupType}', 1)");
        Execute(cfgConnection, "INSERT INTO Value (AttributeID, Value, JobID) VALUES (4, 'test-computer-guid', 1)");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private sealed record Harness(
        BackupRunner Runner, FakeVssSnapshotProvider VssProvider, BackupRepository BackupRepository,
        BackupSetRepository BackupSetRepository, FileRepository FileRepository, ErrorRepository ErrorRepository,
        BackupSetEmptyDirRepository EmptyDirRepository, string BlobRoot);

    private static Harness Build(SqliteConnection catalogConnection, SqliteConnection cfgConnection, string vssSnapshotRoot, string blobRoot)
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
        var vssProvider = new FakeVssSnapshotProvider(vssSnapshotRoot);

        var incrementalPlanner = new IncrementalPlanner(driveRepository, directoryRepository, filenameRepository, backupSetRepository);
        var fileBackupWorker = new FileBackupWorker(fileSystem, blobStore, fileRepository, backupSetRepository,
            driveRepository, directoryRepository, filenameRepository, errorRepository);

        var runner = new BackupRunner(fileSystem, vssProvider, jobSettingsRepository, backupRepository,
            backupSetRepository, emptyDirRepository, driveRepository, directoryRepository, errorRepository,
            incrementalPlanner, fileBackupWorker);

        return new Harness(runner, vssProvider, backupRepository, backupSetRepository, fileRepository,
            errorRepository, emptyDirRepository, blobRoot);
    }

    private static int CountBlobFiles(string blobRoot) =>
        Directory.Exists(blobRoot) ? Directory.GetFiles(blobRoot, "*", SearchOption.AllDirectories).Length : 0;

    [Fact]
    public void Run_backs_up_a_real_directory_tree_end_to_end()
    {
        using var dbTemp = new TempDirectory();
        using var sourceTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var catalogConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        using var cfgConnection = SqliteTestDatabase.CreateConfig(dbTemp.FullPath);

        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "b.txt"), "beta");
        Directory.CreateDirectory(Path.Combine(sourceTemp.FullPath, "empty"));

        SeedJobSettings(cfgConnection, sourceTemp.FullPath, useVss: false, backupType: "FULL");
        var h = Build(catalogConnection, cfgConnection, sourceTemp.FullPath, blobTemp.FullPath);

        var backupId = h.Runner.Run(jobId: 1);

        var backup = h.BackupRepository.GetById(backupId);
        backup.Should().NotBeNull();
        backup!.Status.Should().Be("Done!");
        backup.FileCount.Should().Be(2);
        backup.ErrorCount.Should().Be(0);

        h.BackupSetRepository.GetByBackupId(backupId).Should().HaveCount(2);
        h.EmptyDirRepository.GetByBackupId(backupId).Should().ContainSingle();
        CountBlobFiles(blobTemp.FullPath).Should().Be(2);
        h.VssProvider.CreateSnapshotCallCount.Should().Be(0);
    }

    [Fact]
    public void Run_takes_the_incremental_path_on_a_second_run_against_an_unmodified_tree()
    {
        using var dbTemp = new TempDirectory();
        using var sourceTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var catalogConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        using var cfgConnection = SqliteTestDatabase.CreateConfig(dbTemp.FullPath);

        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(sourceTemp.FullPath, "b.txt"), "beta");

        SeedJobSettings(cfgConnection, sourceTemp.FullPath, useVss: false, backupType: "ARCHIVEBIT");
        var h = Build(catalogConnection, cfgConnection, sourceTemp.FullPath, blobTemp.FullPath);

        var firstBackupId = h.Runner.Run(jobId: 1);
        var blobCountAfterFirstRun = CountBlobFiles(blobTemp.FullPath);

        var secondBackupId = h.Runner.Run(jobId: 1);

        secondBackupId.Should().NotBe(firstBackupId);
        CountBlobFiles(blobTemp.FullPath).Should().Be(blobCountAfterFirstRun);
        h.BackupSetRepository.GetByBackupId(secondBackupId).Should().HaveCount(2);

        var secondBackup = h.BackupRepository.GetById(secondBackupId);
        secondBackup!.FileCount.Should().Be(2);

        var firstFileIds = h.BackupSetRepository.GetByBackupId(firstBackupId).Select(r => r.FileId).OrderBy(id => id);
        var secondFileIds = h.BackupSetRepository.GetByBackupId(secondBackupId).Select(r => r.FileId).OrderBy(id => id);
        secondFileIds.Should().Equal(firstFileIds);
    }

    [Fact]
    public void Run_records_the_logical_source_path_while_reading_content_from_the_physical_vss_snapshot_path()
    {
        using var dbTemp = new TempDirectory();
        using var physicalSnapshotTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var catalogConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        using var cfgConnection = SqliteTestDatabase.CreateConfig(dbTemp.FullPath);

        File.WriteAllText(Path.Combine(physicalSnapshotTemp.FullPath, "a.txt"), "alpha");
        const string logicalSourceDirectory = @"D:\LogicalOnlySource";

        SeedJobSettings(cfgConnection, logicalSourceDirectory, useVss: true, backupType: "FULL");
        var h = Build(catalogConnection, cfgConnection, physicalSnapshotTemp.FullPath, blobTemp.FullPath);

        var backupId = h.Runner.Run(jobId: 1);

        h.VssProvider.CreateSnapshotCallCount.Should().Be(1);
        h.VssProvider.LastRequestedSourceRoot.Should().Be(logicalSourceDirectory);

        var row = h.BackupSetRepository.GetByBackupId(backupId).Should().ContainSingle().Subject;
        var expectedDirId = new LocalDirectoryRepository(catalogConnection).GetOrInsert("LogicalOnlySource");
        row.DirId.Should().Be(expectedDirId);

        var file = h.FileRepository.FindByHash(Sha512Hasher.HashStream(new MemoryStream(Encoding.UTF8.GetBytes("alpha"))));
        file.Should().NotBeNull();
        file!.BackupHash.Should().NotBeNull();
    }
}
