namespace Xondra.Engine.Data.Catalog;

public record RestorableFileRecord(
    long FileId,
    string Drive,
    string Directory,
    string Filename,
    string OriginalFileHash,
    string BackupHash,
    int? Attributes,
    string? CreationTime,
    string? LastWriteTime);
