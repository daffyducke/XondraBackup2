using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class LocalFilenameRepositoryTests
{
    [Fact]
    public void GetOrInsert_inserts_a_new_filename_and_returns_its_id()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalFilenameRepository(connection);

        var id = repository.GetOrInsert("report.docx");

        id.Should().BePositive();
    }

    [Fact]
    public void GetOrInsert_returns_the_same_id_for_the_same_filename()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalFilenameRepository(connection);

        var first = repository.GetOrInsert("report.docx");
        var second = repository.GetOrInsert("report.docx");

        second.Should().Be(first);
    }

    [Fact]
    public void GetOrInsert_gives_different_filenames_different_ids()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new LocalFilenameRepository(connection);

        var a = repository.GetOrInsert("report.docx");
        var b = repository.GetOrInsert("summary.docx");

        b.Should().NotBe(a);
    }
}
