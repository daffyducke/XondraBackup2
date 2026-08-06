using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class LocalDriveRepositoryTests
{
    [Fact]
    public void GetOrInsert_inserts_a_new_drive_and_returns_its_id()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDriveRepository(connection);

        var id = repository.GetOrInsert(@"C:\");

        id.Should().BePositive();
    }

    [Fact]
    public void GetOrInsert_returns_the_same_id_for_the_same_drive()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDriveRepository(connection);

        var first = repository.GetOrInsert(@"C:\");
        var second = repository.GetOrInsert(@"C:\");

        second.Should().Be(first);
    }

    [Fact]
    public void GetOrInsert_gives_different_drives_different_ids()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDriveRepository(connection);

        var c = repository.GetOrInsert(@"C:\");
        var d = repository.GetOrInsert(@"D:\");

        d.Should().NotBe(c);
    }

    [Fact]
    public void GetOrInsert_round_trips_a_value_containing_a_sql_injection_attempt()
    {
        const string malicious = "C'; DROP TABLE File; --";
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDriveRepository(connection);

        var id = repository.GetOrInsert(malicious);
        var idAgain = repository.GetOrInsert(malicious);

        idAgain.Should().Be(id);

        using var readBack = connection.CreateCommand();
        readBack.CommandText = "SELECT Drive FROM LocalDrive WHERE ID = @id";
        readBack.Parameters.AddWithValue("@id", id);
        readBack.ExecuteScalar().Should().Be(malicious);

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'File'";
        tableCheck.ExecuteScalar().Should().Be("File");
    }
}
