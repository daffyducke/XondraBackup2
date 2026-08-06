using System.Text;

namespace Xondra.Engine.Crypto;

public static class AesKeyDerivation
{
    public static (byte[] Key, byte[] IV) Derive(string hashHex)
    {
        var key = Encoding.ASCII.GetBytes(hashHex[..32]);

        var reversed = new string(hashHex.Reverse().ToArray());
        var iv = Encoding.ASCII.GetBytes(reversed[..16]);

        return (key, iv);
    }
}
