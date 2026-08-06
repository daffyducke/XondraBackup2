using FluentAssertions;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Storage;

public class BlobStoreTests
{
    private const string Hash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void Write_places_the_blob_at_the_expected_sharded_path()
    {
        using var root = new TempDirectory();
        var store = new BlobStore(root.FullPath);

        store.Write(Hash, new MemoryStream([1, 2, 3]));

        var expectedPath = Path.Combine(
            root.FullPath, Hash[0].ToString(), Hash[1].ToString(), Hash[2].ToString(), Hash);
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public void Write_then_Read_returns_the_same_bytes()
    {
        using var root = new TempDirectory();
        var store = new BlobStore(root.FullPath);
        byte[] content = [10, 20, 30, 40, 50];

        store.Write(Hash, new MemoryStream(content));

        using var readBack = store.Read(Hash);
        using var buffer = new MemoryStream();
        readBack.CopyTo(buffer);
        buffer.ToArray().Should().Equal(content);
    }

    [Fact]
    public void Exists_reflects_whether_the_blob_has_been_written()
    {
        using var root = new TempDirectory();
        var store = new BlobStore(root.FullPath);

        store.Exists(Hash).Should().BeFalse();

        store.Write(Hash, new MemoryStream([1]));

        store.Exists(Hash).Should().BeTrue();
    }

    [Fact]
    public void Write_is_idempotent_for_the_same_hash()
    {
        using var root = new TempDirectory();
        var store = new BlobStore(root.FullPath);
        byte[] content = [7, 8, 9];

        store.Write(Hash, new MemoryStream(content));
        store.Write(Hash, new MemoryStream(content));

        using var readBack = store.Read(Hash);
        using var buffer = new MemoryStream();
        readBack.CopyTo(buffer);
        buffer.ToArray().Should().Equal(content);
    }
}
