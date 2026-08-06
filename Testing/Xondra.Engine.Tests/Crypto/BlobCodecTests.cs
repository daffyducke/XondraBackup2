using System.Text;
using FluentAssertions;
using Xondra.Engine.Crypto;
using Xunit;

namespace Xondra.Engine.Tests.Crypto;

public class BlobCodecTests
{
    private const string HashA =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    private const string HashB =
        "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e";

    [Fact]
    public void Round_trips_empty_content()
    {
        AssertRoundTrips([]);
    }

    [Fact]
    public void Round_trips_small_content()
    {
        AssertRoundTrips(Encoding.UTF8.GetBytes("Xondra backs this file up."));
    }

    [Fact]
    public void Round_trips_content_spanning_multiple_stream_buffers()
    {
        var content = new byte[500_000];
        new Random(42).NextBytes(content);

        AssertRoundTrips(content);
    }

    [Fact]
    public void Encrypted_output_differs_from_the_plaintext()
    {
        var plaintext = Encoding.UTF8.GetBytes(
            "Xondra backs this file up, repeatedly, so compression has something to do.");
        using var source = new MemoryStream(plaintext);
        using var encrypted = new MemoryStream();

        BlobCodec.CompressThenEncrypt(source, encrypted, HashA);

        encrypted.ToArray().Should().NotEqual(plaintext);
    }

    [Fact]
    public void Decrypting_with_the_wrong_hash_does_not_recover_the_original_bytes()
    {
        var plaintext = Encoding.UTF8.GetBytes("Only the original hash should be able to decrypt this.");
        using var source = new MemoryStream(plaintext);
        using var encrypted = new MemoryStream();
        BlobCodec.CompressThenEncrypt(source, encrypted, HashA);
        encrypted.Position = 0;

        using var destination = new MemoryStream();

        try
        {
            BlobCodec.DecryptThenDecompress(encrypted, destination, HashB);
            destination.ToArray().Should().NotEqual(plaintext);
        }
        catch (Exception)
        {
            // A thrown exception (bad GZip header, bad PKCS7 padding, etc.) is just as
            // valid a proof that the wrong hash can't recover the data as a byte mismatch.
        }
    }

    private static void AssertRoundTrips(byte[] plaintext)
    {
        using var source = new MemoryStream(plaintext);
        using var encrypted = new MemoryStream();
        BlobCodec.CompressThenEncrypt(source, encrypted, HashA);

        encrypted.Position = 0;
        using var destination = new MemoryStream();
        BlobCodec.DecryptThenDecompress(encrypted, destination, HashA);

        destination.ToArray().Should().Equal(plaintext);
    }
}
