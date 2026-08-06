namespace Xondra.Engine.Data.Catalog;

public record BackupSetEmptyDirRecord(long Id, long BackupId, long DirId, long DriveId, int? Error);
