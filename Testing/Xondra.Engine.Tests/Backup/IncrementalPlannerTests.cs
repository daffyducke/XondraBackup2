using FluentAssertions;
using Xondra.Engine.Backup;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Scanning;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class IncrementalPlannerTests
{
    private const string Hash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void Plan_copies_forward_an_unchanged_file_and_excludes_it_from_reprocessing()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupRepository = new BackupRepository(connection);
        var backupSetRepository = new BackupSetRepository(connection);
        var planner = new IncrementalPlanner(
            new LocalDriveRepository(connection), new LocalDirectoryRepository(connection),
            new LocalFilenameRepository(connection), backupSetRepository);

        var priorBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var driveId = new LocalDriveRepository(connection).GetOrInsert(@"C:\");
        var dirId = new LocalDirectoryRepository(connection).GetOrInsert(@"Users\daffy");
        var filenameId = new LocalFilenameRepository(connection).GetOrInsert("report.docx");
        var fileId = new FileRepository(connection).Insert(Hash, "hmac-value", 12345);
        backupSetRepository.Insert(priorBackupId, dirId, fileId, filenameId, driveId,
            error: null, attributes: 32, creationTime: "2026-08-01T09:00:00", lastWriteTime: "2026-08-01T09:00:00");

        var newBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var unchangedFile = new ScannedFile(
            @"C:\", @"Users\daffy", "report.docx", @"C:\Users\daffy\report.docx",
            12345, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Normal);

        var filesToProcess = planner.Plan("ARCHIVEBIT", newBackupId, [unchangedFile]);

        filesToProcess.Should().BeEmpty();
        var copiedRow = backupSetRepository.GetByBackupId(newBackupId).Should().ContainSingle().Subject;
        copiedRow.FileId.Should().Be(fileId);
        copiedRow.DirId.Should().Be(dirId);
        copiedRow.FilenameId.Should().Be(filenameId);
        copiedRow.DriveId.Should().Be(driveId);
    }

    [Fact]
    public void Plan_sends_a_changed_file_to_reprocessing_without_copying_anything_forward()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupRepository = new BackupRepository(connection);
        var backupSetRepository = new BackupSetRepository(connection);
        var planner = new IncrementalPlanner(
            new LocalDriveRepository(connection), new LocalDirectoryRepository(connection),
            new LocalFilenameRepository(connection), backupSetRepository);

        var priorBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var driveId = new LocalDriveRepository(connection).GetOrInsert(@"C:\");
        var dirId = new LocalDirectoryRepository(connection).GetOrInsert(@"Users\daffy");
        var filenameId = new LocalFilenameRepository(connection).GetOrInsert("report.docx");
        var fileId = new FileRepository(connection).Insert(Hash, "hmac-value", 12345);
        backupSetRepository.Insert(priorBackupId, dirId, fileId, filenameId, driveId,
            null, 32, "2026-08-01T09:00:00", "2026-08-01T09:00:00");

        var newBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var changedFile = new ScannedFile(
            @"C:\", @"Users\daffy", "report.docx", @"C:\Users\daffy\report.docx",
            99999, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Archive);

        var filesToProcess = planner.Plan("ARCHIVEBIT", newBackupId, [changedFile]);

        filesToProcess.Should().ContainSingle().Which.Should().Be(changedFile);
        backupSetRepository.GetByBackupId(newBackupId).Should().BeEmpty();
    }

    [Fact]
    public void Plan_reprocesses_an_unchanged_file_that_has_never_been_backed_up_before()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupRepository = new BackupRepository(connection);
        var backupSetRepository = new BackupSetRepository(connection);
        var planner = new IncrementalPlanner(
            new LocalDriveRepository(connection), new LocalDirectoryRepository(connection),
            new LocalFilenameRepository(connection), backupSetRepository);

        var newBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var neverBackedUpFile = new ScannedFile(
            @"C:\", @"Users\daffy", "brandnew.docx", @"C:\Users\daffy\brandnew.docx",
            42, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Normal);

        var filesToProcess = planner.Plan("ARCHIVEBIT", newBackupId, [neverBackedUpFile]);

        filesToProcess.Should().ContainSingle().Which.Should().Be(neverBackedUpFile);
    }

    [Fact]
    public void Plan_processes_every_file_when_the_backup_type_is_not_ARCHIVEBIT()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupRepository = new BackupRepository(connection);
        var backupSetRepository = new BackupSetRepository(connection);
        var planner = new IncrementalPlanner(
            new LocalDriveRepository(connection), new LocalDirectoryRepository(connection),
            new LocalFilenameRepository(connection), backupSetRepository);

        var newBackupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var unchangedLookingFile = new ScannedFile(
            @"C:\", @"Users\daffy", "report.docx", @"C:\Users\daffy\report.docx",
            12345, DateTime.UtcNow, DateTime.UtcNow, FileAttributes.Normal);

        var filesToProcess = planner.Plan("FULL", newBackupId, [unchangedLookingFile]);

        filesToProcess.Should().ContainSingle().Which.Should().Be(unchangedLookingFile);
        backupSetRepository.GetByBackupId(newBackupId).Should().BeEmpty();
    }
}
