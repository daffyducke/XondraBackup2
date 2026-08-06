using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class BackupRepositoryTests
{
    [Fact]
    public void Start_inserts_a_running_backup_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new BackupRepository(connection);
        var startDate = new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc);

        var id = repository.Start("computer-guid", startDate, """{"UseVSS":true}""");
        var found = repository.GetById(id);

        found.Should().NotBeNull();
        found!.ComputerGuid.Should().Be("computer-guid");
        found.StartDate.Should().Be(startDate);
        found.SettingsJson.Should().Be("""{"UseVSS":true}""");
        found.Status.Should().Be("Running");
        found.EndDate.Should().BeNull();
    }

    [Fact]
    public void Complete_updates_end_date_counters_and_status()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new BackupRepository(connection);
        var id = repository.Start("computer-guid", DateTime.UtcNow, null);
        var endDate = new DateTime(2026, 8, 6, 10, 0, 0, DateTimeKind.Utc);

        repository.Complete(id, endDate, fileCount: 42, errorCount: 1, status: "Done!");

        var found = repository.GetById(id);
        found!.EndDate.Should().Be(endDate);
        found.FileCount.Should().Be(42);
        found.ErrorCount.Should().Be(1);
        found.Status.Should().Be("Done!");
    }

    [Fact]
    public void GetById_returns_null_for_an_unknown_id()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new BackupRepository(connection);

        repository.GetById(999).Should().BeNull();
    }

    [Fact]
    public void Start_round_trips_settings_json_containing_a_sql_injection_attempt()
    {
        const string malicious = """{"Note":"'; DROP TABLE File; --"}""";
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new BackupRepository(connection);

        var id = repository.Start("computer-guid", DateTime.UtcNow, malicious);

        repository.GetById(id)!.SettingsJson.Should().Be(malicious);

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'File'";
        tableCheck.ExecuteScalar().Should().Be("File");
    }
}
