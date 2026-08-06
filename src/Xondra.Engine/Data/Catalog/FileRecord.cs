namespace Xondra.Engine.Data.Catalog;

public record FileRecord(
    long Id,
    string OriginalFileHash,
    string? OrigHmacSha512,
    long? Filesize,
    bool? LocalVerified,
    string? BackupHash,
    long? FilesizeCompressed);
