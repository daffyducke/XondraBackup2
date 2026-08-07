namespace Xondra.Engine.Scanning;

public class DirectoryScanner(IFileSystem fileSystem)
{
    public ScanResult Scan(string sourceRoot)
    {
        var files = new List<ScannedFile>();
        var emptyDirectories = new List<ScannedDirectory>();
        var errors = new List<ScanError>();

        ScanDirectory(sourceRoot, files, emptyDirectories, errors);

        return new ScanResult(files, emptyDirectories, errors);
    }

    private void ScanDirectory(
        string directoryPath, List<ScannedFile> files, List<ScannedDirectory> emptyDirectories, List<ScanError> errors)
    {
        List<string> filesHere;
        List<string> subdirectories;
        try
        {
            filesHere = fileSystem.EnumerateFiles(directoryPath).ToList();
            subdirectories = fileSystem.EnumerateDirectories(directoryPath).ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            errors.Add(new ScanError(directoryPath, ex.Message));
            return;
        }

        foreach (var filePath in filesHere)
        {
            try
            {
                files.Add(ToScannedFile(directoryPath, filePath));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                errors.Add(new ScanError(filePath, ex.Message));
            }
        }

        if (filesHere.Count == 0 && subdirectories.Count == 0)
            emptyDirectories.Add(ToScannedDirectory(directoryPath));

        foreach (var subdirectory in subdirectories)
            ScanDirectory(subdirectory, files, emptyDirectories, errors);
    }

    private ScannedFile ToScannedFile(string directoryPath, string filePath)
    {
        var info = fileSystem.GetFileInfo(filePath);
        var (drive, directory) = SplitPath(directoryPath);
        return new ScannedFile(drive, directory, Path.GetFileName(filePath), filePath,
            info.Size, info.CreationTimeUtc, info.LastWriteTimeUtc, info.Attributes);
    }

    private static ScannedDirectory ToScannedDirectory(string directoryPath)
    {
        var (drive, directory) = SplitPath(directoryPath);
        return new ScannedDirectory(drive, directory);
    }

    private static (string Drive, string Directory) SplitPath(string directoryPath)
    {
        var root = Path.GetPathRoot(directoryPath) ?? string.Empty;
        var directory = directoryPath[root.Length..];
        return (root, directory);
    }
}
