namespace Xondra.Engine.Storage;

public class BlobStore(string rootDirectory)
{
    public bool Exists(string hashHex) => File.Exists(GetPath(hashHex));

    public void Write(string hashHex, Stream content)
    {
        var path = GetPath(hashHex);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var destination = File.Create(path);
        content.CopyTo(destination);
    }

    public Stream Read(string hashHex) => File.OpenRead(GetPath(hashHex));

    private string GetPath(string hashHex) =>
        Path.Combine(rootDirectory, hashHex[0].ToString(), hashHex[1].ToString(), hashHex[2].ToString(), hashHex);
}
