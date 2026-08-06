namespace Xondra.Engine.Data.Catalog;

public record BackupRecord(
    long Id,
    string? ComputerGuid,
    DateTime? StartDate,
    DateTime? EndDate,
    int FileCount,
    int ErrorCount,
    string? Status,
    string? SettingsJson);
