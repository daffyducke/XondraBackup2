using System.Text;
using FluentAssertions;
using Xondra.Engine.Hashing;
using Xunit;

namespace Xondra.Engine.Tests.Hashing;

public class HmacSha512DeriverTests
{
    [Fact]
    public void Derive_matches_known_RFC4231_test_vector()
    {
        var key = Enumerable.Repeat((byte)0x0b, 20).ToArray();
        using var data = new MemoryStream(Encoding.ASCII.GetBytes("Hi There"));

        var hmac = HmacSha512Deriver.Derive(key, data);

        hmac.Should().Be(
            "87aa7cdea5ef619d4ff0b4241a1d6cb02379f4e2ce4ec2787ad0b30545e17cdedaa833b7d6b8a702038b274eaea3f4e4be9d914eeb61f1702e696c203a126854");
    }

    [Fact]
    public void Derive_is_deterministic_for_the_same_key_and_content()
    {
        var key = Encoding.ASCII.GetBytes("some-key-bytes-here!");
        var content = Encoding.ASCII.GetBytes("xondra file content");

        var first = HmacSha512Deriver.Derive(key, new MemoryStream(content));
        var second = HmacSha512Deriver.Derive(key, new MemoryStream(content));

        first.Should().Be(second);
    }

    [Fact]
    public void Derive_changes_when_the_key_changes()
    {
        var content = Encoding.ASCII.GetBytes("xondra file content");
        var keyA = Encoding.ASCII.GetBytes("key-a");
        var keyB = Encoding.ASCII.GetBytes("key-b");

        var hmacA = HmacSha512Deriver.Derive(keyA, new MemoryStream(content));
        var hmacB = HmacSha512Deriver.Derive(keyB, new MemoryStream(content));

        hmacA.Should().NotBe(hmacB);
    }
}
