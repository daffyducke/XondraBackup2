using System.Text.Json;

namespace Xondra.Engine.Backup;

public record BackupConfig(string SourceDirectory, string? ComputerGuid, bool UseVss, string BackupType)
{
    public static BackupConfig Parse(string settingsJson)
    {
        using var document = JsonDocument.Parse(settingsJson);
        var root = document.RootElement;

        return new BackupConfig(
            SourceDirectory: GetOptionalString(root, "SourceDirectory")
                ?? throw new FormatException("Backup settings JSON is missing required property 'SourceDirectory'."),
            ComputerGuid: GetOptionalString(root, "ComputerGUID"),
            UseVss: GetBool(root, "UseVSS"),
            BackupType: GetOptionalString(root, "BackupType") ?? "FULL");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool GetBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            _ => false,
        };
    }
}
