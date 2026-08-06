using System.Security.Cryptography;

namespace Xondra.Engine.Hashing;

public static class HmacSha512Deriver
{
    public static string Derive(byte[] key, Stream content)
    {
        using var hmac = new HMACSHA512(key);
        var hash = hmac.ComputeHash(content);
        return Convert.ToHexStringLower(hash);
    }
}
