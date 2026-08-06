using System.IO.Compression;
using System.Security.Cryptography;

namespace Xondra.Engine.Crypto;

public static class BlobCodec
{
    public static void CompressThenEncrypt(Stream plaintextSource, Stream destination, string plaintextHashHex)
    {
        var (key, iv) = AesKeyDerivation.Derive(plaintextHashHex);

        using var aes = Aes.Create();
        using var encryptor = aes.CreateEncryptor(key, iv);
        using var cryptoStream = new CryptoStream(destination, encryptor, CryptoStreamMode.Write, leaveOpen: true);
        using var gzip = new GZipStream(cryptoStream, CompressionMode.Compress, leaveOpen: true);

        plaintextSource.CopyTo(gzip);
    }

    public static void DecryptThenDecompress(Stream encryptedSource, Stream destination, string originalHashHex)
    {
        var (key, iv) = AesKeyDerivation.Derive(originalHashHex);

        using var aes = Aes.Create();
        using var decryptor = aes.CreateDecryptor(key, iv);
        using var cryptoStream = new CryptoStream(encryptedSource, decryptor, CryptoStreamMode.Read, leaveOpen: true);
        using var gzip = new GZipStream(cryptoStream, CompressionMode.Decompress, leaveOpen: true);

        gzip.CopyTo(destination);
    }
}
