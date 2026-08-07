using AlphaDirectory = Alphaleonis.Win32.Filesystem.Directory;
using AlphaFile = Alphaleonis.Win32.Filesystem.File;
using AlphaFileInfo = Alphaleonis.Win32.Filesystem.FileInfo;

namespace Xondra.Engine.Scanning;

// Alphaleonis.Win32.Filesystem.Directory/File/FileInfo mirror the BCL API but
// transparently apply the \\?\ long-path prefix, which the plain System.IO
// types don't do without extra opt-in on Windows.
public class WindowsFileSystem : IFileSystem
{
    public IEnumerable<string> EnumerateFiles(string directoryPath) => AlphaDirectory.EnumerateFiles(directoryPath);

    public IEnumerable<string> EnumerateDirectories(string directoryPath) => AlphaDirectory.EnumerateDirectories(directoryPath);

    public FileEntryInfo GetFileInfo(string filePath)
    {
        var info = new AlphaFileInfo(filePath);
        return new FileEntryInfo(info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, info.Attributes);
    }

    public void ClearArchiveBit(string filePath)
    {
        var attributes = AlphaFile.GetAttributes(filePath);
        if (attributes.HasFlag(FileAttributes.Archive))
            AlphaFile.SetAttributes(filePath, attributes & ~FileAttributes.Archive);
    }

    public Stream OpenRead(string filePath) => AlphaFile.OpenRead(filePath);

    public Stream Create(string filePath) => AlphaFile.Create(filePath);

    public void CreateDirectory(string directoryPath) => AlphaDirectory.CreateDirectory(directoryPath);

    public void SetTimestampsAndAttributes(string filePath, DateTime creationTimeUtc, DateTime lastWriteTimeUtc, FileAttributes attributes)
    {
        AlphaFile.SetCreationTimeUtc(filePath, creationTimeUtc);
        AlphaFile.SetLastWriteTimeUtc(filePath, lastWriteTimeUtc);
        AlphaFile.SetAttributes(filePath, attributes);
    }

    public void DeleteFile(string filePath) => AlphaFile.Delete(filePath);
}
