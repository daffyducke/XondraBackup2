using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class LocalDirectoryRepositoryTests
{
    [Fact]
    public void GetOrInsert_inserts_a_new_directory_and_returns_its_id()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDirectoryRepository(connection);

        var id = repository.GetOrInsert(@"Users\daffy\Documents");

        id.Should().BePositive();
    }

    [Fact]
    public void GetOrInsert_returns_the_same_id_for_the_same_directory()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDirectoryRepository(connection);

        var first = repository.GetOrInsert(@"Users\daffy\Documents");
        var second = repository.GetOrInsert(@"Users\daffy\Documents");

        second.Should().Be(first);
    }

    [Fact]
    public void GetOrInsert_round_trips_a_value_containing_a_sql_injection_attempt()
    {
        const string malicious = @"Users\o'brien's docs'; DROP TABLE File; --";
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalDirectoryRepository(connection);

        var id = repository.GetOrInsert(malicious);
        var idAgain = repository.GetOrInsert(malicious);

        idAgain.Should().Be(id);

        using var readBack = connection.CreateCommand();
        readBack.CommandText = "SELECT Directory FROM LocalDirectory WHERE ID = @id";
        readBack.Parameters.AddWithValue("@id", id);
        readBack.ExecuteScalar().Should().Be(malicious);

        using var tableCheck = connection.CreateCommand();
        tableCheck.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'File'";
        tableCheck.ExecuteScalar().Should().Be("File");
    }
}
