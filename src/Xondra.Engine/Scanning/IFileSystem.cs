namespace Xondra.Engine.Scanning;

public interface IFileSystem
{
    IEnumerable<string> EnumerateFiles(string directoryPath);
    IEnumerable<string> EnumerateDirectories(string directoryPath);
    FileEntryInfo GetFileInfo(string filePath);
    void ClearArchiveBit(string filePath);
}

public record FileEntryInfo(long Size, DateTime CreationTimeUtc, DateTime LastWriteTimeUtc, FileAttributes Attributes)
{
    public bool ArchiveBitSet => Attributes.HasFlag(FileAttributes.Archive);
}
