namespace Xondra.Engine.Data.Catalog;

public record ErrorRecord(long Id, long BackupId, string? ProcedureName, string Error);
