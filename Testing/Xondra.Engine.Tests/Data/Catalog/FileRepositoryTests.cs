using FluentAssertions;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Catalog;

public class FileRepositoryTests
{
    private const string Hash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void FindByHash_returns_null_when_no_file_has_been_stored()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new FileRepository(connection);

        repository.FindByHash(Hash).Should().BeNull();
    }

    [Fact]
    public void Insert_then_FindByHash_round_trips_the_new_file_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new FileRepository(connection);

        var id = repository.Insert(Hash, "hmac-value", 12345);
        var found = repository.FindByHash(Hash);

        found.Should().NotBeNull();
        found!.Id.Should().Be(id);
        found.OriginalFileHash.Should().Be(Hash);
        found.OrigHmacSha512.Should().Be("hmac-value");
        found.Filesize.Should().Be(12345);
        found.BackupHash.Should().BeNull();
        found.FilesizeCompressed.Should().BeNull();
    }

    [Fact]
    public void MarkStored_sets_the_backup_hash_and_compressed_size()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new FileRepository(connection);
        var id = repository.Insert(Hash, "hmac-value", 12345);

        repository.MarkStored(id, "backup-hash-value", 6789);

        var found = repository.GetById(id);
        found!.BackupHash.Should().Be("backup-hash-value");
        found.FilesizeCompressed.Should().Be(6789);
    }

    [Fact]
    public void GetById_returns_null_for_an_unknown_id()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var repository = new FileRepository(connection);

        repository.GetById(999).Should().BeNull();
    }
}
