namespace Xondra.Engine.Scanning;

public record ScannedFile(
    string Drive,
    string Directory,
    string Filename,
    string FullPath,
    long Size,
    DateTime CreationTimeUtc,
    DateTime LastWriteTimeUtc,
    FileAttributes Attributes)
{
    public bool ArchiveBitSet => Attributes.HasFlag(FileAttributes.Archive);
}
