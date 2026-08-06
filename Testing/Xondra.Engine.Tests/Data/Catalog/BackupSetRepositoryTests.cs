using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class BackupSetRepositoryTests
{
    private const string Hash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void Insert_then_GetByBackupId_round_trips_the_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (backupId, driveId, dirId, filenameId, fileId) = Seed(connection);
        var repository = new BackupSetRepository(connection);

        var id = repository.Insert(backupId, dirId, fileId, filenameId, driveId, error: null,
            attributes: 32, creationTime: "2026-08-06T09:00:00", lastWriteTime: "2026-08-06T09:00:00");

        var rows = repository.GetByBackupId(backupId);
        rows.Should().ContainSingle(r => r.Id == id && r.FileId == fileId && r.DirId == dirId
            && r.FilenameId == filenameId && r.DriveId == driveId && r.Attributes == 32);
    }

    [Fact]
    public void FindLatestForPath_returns_the_row_from_the_most_recent_backup()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (backup1Id, driveId, dirId, filenameId, fileId) = Seed(connection);
        var repository = new BackupSetRepository(connection);
        repository.Insert(backup1Id, dirId, fileId, filenameId, driveId, null, null, null, null);

        var backupRepository = new BackupRepository(connection);
        var backup2Id = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var latestId = repository.Insert(backup2Id, dirId, fileId, filenameId, driveId, null, null, null, null);

        var latest = repository.FindLatestForPath(driveId, dirId, filenameId);

        latest.Should().NotBeNull();
        latest!.Id.Should().Be(latestId);
        latest.BackupId.Should().Be(backup2Id);
    }

    [Fact]
    public void CopyForward_creates_a_new_row_under_the_new_backup_without_changing_the_source_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (backup1Id, driveId, dirId, filenameId, fileId) = Seed(connection);
        var repository = new BackupSetRepository(connection);
        var sourceId = repository.Insert(backup1Id, dirId, fileId, filenameId, driveId, error: null,
            attributes: 32, creationTime: "2026-08-06T09:00:00", lastWriteTime: "2026-08-06T09:00:00");

        var backupRepository = new BackupRepository(connection);
        var backup2Id = backupRepository.Start("computer-guid", DateTime.UtcNow, null);

        var newId = repository.CopyForward(sourceId, backup2Id);

        newId.Should().NotBe(sourceId);
        var copied = repository.GetByBackupId(backup2Id).Should().ContainSingle().Subject;
        copied.FileId.Should().Be(fileId);
        copied.DirId.Should().Be(dirId);
        copied.FilenameId.Should().Be(filenameId);
        copied.DriveId.Should().Be(driveId);
        copied.Attributes.Should().Be(32);

        repository.GetByBackupId(backup1Id).Should().ContainSingle(r => r.Id == sourceId);
    }

    private static (long BackupId, long DriveId, long DirId, long FilenameId, long FileId) Seed(
        Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var driveId = new LocalDriveRepository(connection).GetOrInsert(@"C:\");
        var dirId = new LocalDirectoryRepository(connection).GetOrInsert(@"Users\daffy");
        var filenameId = new LocalFilenameRepository(connection).GetOrInsert("report.docx");
        var fileId = new FileRepository(connection).Insert(Hash, "hmac-value", 12345);
        return (backupId, driveId, dirId, filenameId, fileId);
    }
}
