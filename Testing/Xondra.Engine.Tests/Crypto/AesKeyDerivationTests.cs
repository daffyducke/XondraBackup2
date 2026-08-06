using System.Text;
using FluentAssertions;
using Xondra.Engine.Crypto;
using Xunit;

namespace Xondra.Engine.Tests.Crypto;

public class AesKeyDerivationTests
{
    private const string KnownHash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";

    [Fact]
    public void Derive_produces_a_32_byte_key_and_16_byte_iv()
    {
        var (key, iv) = AesKeyDerivation.Derive(KnownHash);

        key.Should().HaveCount(32);
        iv.Should().HaveCount(16);
    }

    [Fact]
    public void Derive_key_is_the_first_32_characters_of_the_hash_as_ascii_bytes()
    {
        var (key, _) = AesKeyDerivation.Derive(KnownHash);

        key.Should().Equal(Encoding.ASCII.GetBytes("ddaf35a193617abacc417349ae204131"));
    }

    [Fact]
    public void Derive_iv_is_the_first_16_characters_of_the_reversed_hash_as_ascii_bytes()
    {
        var (_, iv) = AesKeyDerivation.Derive(KnownHash);

        iv.Should().Equal(Encoding.ASCII.GetBytes("f94ac45af49ca9a2"));
    }

    [Fact]
    public void Derive_iv_is_not_simply_the_first_16_characters_of_the_forward_hash()
    {
        var (_, iv) = AesKeyDerivation.Derive(KnownHash);

        iv.Should().NotEqual(Encoding.ASCII.GetBytes(KnownHash[..16]));
    }

    [Fact]
    public void Derive_is_a_pure_function_of_the_hash_string()
    {
        var (key1, iv1) = AesKeyDerivation.Derive(KnownHash);
        var (key2, iv2) = AesKeyDerivation.Derive(KnownHash);

        key1.Should().Equal(key2);
        iv1.Should().Equal(iv2);
    }
}
