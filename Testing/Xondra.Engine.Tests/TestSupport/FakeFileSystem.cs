using Xondra.Engine.Scanning;

namespace Xondra.Engine.Tests.TestSupport;

public class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, List<string>> _filesByDirectory = new();
    private readonly Dictionary<string, List<string>> _subdirectoriesByDirectory = new();
    private readonly Dictionary<string, FileEntryInfo> _fileInfoByPath = new();
    private readonly Dictionary<string, byte[]> _contentByPath = new();
    private readonly HashSet<string> _deniedDirectories = new();
    private readonly HashSet<string> _missingFiles = new();
    private readonly HashSet<string> _openReadFailures = new();

    public List<string> ClearedArchiveBitCalls { get; } = [];

    public void AddDirectory(string directoryPath, IEnumerable<string>? files = null, IEnumerable<string>? subdirectories = null)
    {
        _filesByDirectory[directoryPath] = files?.ToList() ?? [];
        _subdirectoriesByDirectory[directoryPath] = subdirectories?.ToList() ?? [];
    }

    public void AddFile(string filePath, FileEntryInfo info) => _fileInfoByPath[filePath] = info;

    public void DenyAccess(string directoryPath) => _deniedDirectories.Add(directoryPath);

    public void MakeFileDisappear(string filePath) => _missingFiles.Add(filePath);

    public void AddFileContent(string filePath, byte[] content) => _contentByPath[filePath] = content;

    public void FailOpenRead(string filePath) => _openReadFailures.Add(filePath);

    public IEnumerable<string> EnumerateFiles(string directoryPath)
    {
        ThrowIfDenied(directoryPath);
        return _filesByDirectory.TryGetValue(directoryPath, out var files) ? files : [];
    }

    public IEnumerable<string> EnumerateDirectories(string directoryPath)
    {
        ThrowIfDenied(directoryPath);
        return _subdirectoriesByDirectory.TryGetValue(directoryPath, out var directories) ? directories : [];
    }

    public FileEntryInfo GetFileInfo(string filePath)
    {
        if (_missingFiles.Contains(filePath))
            throw new IOException($"File no longer exists: {filePath}");
        return _fileInfoByPath[filePath];
    }

    public void ClearArchiveBit(string filePath) => ClearedArchiveBitCalls.Add(filePath);

    public Stream OpenRead(string filePath)
    {
        if (_openReadFailures.Contains(filePath))
            throw new IOException($"Simulated read failure: {filePath}");
        return new MemoryStream(_contentByPath[filePath]);
    }

    private void ThrowIfDenied(string directoryPath)
    {
        if (_deniedDirectories.Contains(directoryPath))
            throw new UnauthorizedAccessException($"Access denied: {directoryPath}");
    }
}
