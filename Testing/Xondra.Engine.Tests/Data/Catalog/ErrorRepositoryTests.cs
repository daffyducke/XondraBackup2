using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class ErrorRepositoryTests
{
    [Fact]
    public void Insert_then_GetByBackupId_round_trips_the_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var repository = new ErrorRepository(connection);

        var id = repository.Insert(backupId, "BackupFile", "UnauthorizedAccessException: access denied");

        var rows = repository.GetByBackupId(backupId);
        rows.Should().ContainSingle(r =>
            r.Id == id && r.ProcedureName == "BackupFile" && r.Error == "UnauthorizedAccessException: access denied");
    }

    [Fact]
    public void Insert_round_trips_an_error_message_containing_a_sql_injection_attempt()
    {
        const string malicious = "IOException: file '; DROP TABLE File; --";
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var repository = new ErrorRepository(connection);

        var id = repository.Insert(backupId, "BackupFile", malicious);

        repository.GetByBackupId(backupId).Should().ContainSingle(r => r.Id == id && r.Error == malicious);

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'File'";
        tableCheck.ExecuteScalar().Should().Be("File");
    }
}
