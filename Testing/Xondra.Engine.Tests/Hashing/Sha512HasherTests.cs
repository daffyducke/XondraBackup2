using System.Text;
using FluentAssertions;
using Xondra.Engine.Hashing;
using Xunit;

namespace Xondra.Engine.Tests.Hashing;

public class Sha512HasherTests
{
    private const string AbcHash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void HashStream_of_empty_input_matches_known_vector()
    {
        using var stream = new MemoryStream();

        var hash = Sha512Hasher.HashStream(stream);

        hash.Should().Be(
            "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e");
    }

    [Fact]
    public void HashStream_of_abc_matches_known_vector()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("abc"));

        var hash = Sha512Hasher.HashStream(stream);

        hash.Should().Be(AbcHash);
    }

    [Fact]
    public void HashFile_matches_HashStream_for_the_same_content()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "abc");
        try
        {
            var hash = Sha512Hasher.HashFile(path);

            hash.Should().Be(AbcHash);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HashStream_is_lowercase_hex_of_the_correct_length()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("xondra"));

        var hash = Sha512Hasher.HashStream(stream);

        hash.Should().HaveLength(128);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }
}
