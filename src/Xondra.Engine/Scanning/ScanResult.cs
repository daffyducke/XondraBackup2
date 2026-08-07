namespace Xondra.Engine.Scanning;

public record ScanResult(
    IReadOnlyList<ScannedFile> Files,
    IReadOnlyList<ScannedDirectory> EmptyDirectories,
    IReadOnlyList<ScanError> Errors);

public record ScanError(string Path, string Message);
