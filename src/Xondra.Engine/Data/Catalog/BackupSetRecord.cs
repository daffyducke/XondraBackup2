namespace Xondra.Engine.Data.Catalog;

public record BackupSetRecord(
    long Id,
    long BackupId,
    long DirId,
    long FileId,
    long FilenameId,
    long DriveId,
    int? Error,
    int? Attributes,
    string? CreationTime,
    string? LastWriteTime);
