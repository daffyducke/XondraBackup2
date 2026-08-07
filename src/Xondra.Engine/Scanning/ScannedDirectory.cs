namespace Xondra.Engine.Scanning;

// Represents a directory with no files and no subdirectories — the only kind
// of directory that needs its own catalog row (BackupSetEmptyDir), since every
// other directory is implied by the files/subdirectories found inside it.
public record ScannedDirectory(string Drive, string Directory);
