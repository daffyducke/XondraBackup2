using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class BackupSetEmptyDirRepositoryTests
{
    [Fact]
    public void Insert_then_GetByBackupId_round_trips_the_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var driveId = new LocalDriveRepository(connection).GetOrInsert(@"C:\");
        var dirId = new LocalDirectoryRepository(connection).GetOrInsert(@"Users\daffy\Empty");
        var repository = new BackupSetEmptyDirRepository(connection);

        var id = repository.Insert(backupId, dirId, driveId, error: null);

        var rows = repository.GetByBackupId(backupId);
        rows.Should().ContainSingle(r => r.Id == id && r.DirId == dirId && r.DriveId == driveId && r.Error == null);
    }
}
