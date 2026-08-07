namespace Xondra.Engine.Scanning;

public interface IFileSystem
{
    IEnumerable<string> EnumerateFiles(string directoryPath);
    IEnumerable<string> EnumerateDirectories(string directoryPath);
    FileEntryInfo GetFileInfo(string filePath);
    void ClearArchiveBit(string filePath);
    Stream OpenRead(string filePath);
    Stream Create(string filePath);
    void CreateDirectory(string directoryPath);
    void SetTimestampsAndAttributes(string filePath, DateTime creationTimeUtc, DateTime lastWriteTimeUtc, FileAttributes attributes);
    void DeleteFile(string filePath);
}

public record FileEntryInfo(long Size, DateTime CreationTimeUtc, DateTime LastWriteTimeUtc, FileAttributes Attributes)
{
    public bool ArchiveBitSet => Attributes.HasFlag(FileAttributes.Archive);
}
