using System.Security.Cryptography;

namespace Xondra.Engine.Hashing;

public static class Sha512Hasher
{
    public static string HashStream(Stream content)
    {
        var hash = SHA512.HashData(content);
        return Convert.ToHexStringLower(hash);
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return HashStream(stream);
    }
}
