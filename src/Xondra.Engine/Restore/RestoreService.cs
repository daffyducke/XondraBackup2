using System.Globalization;
using Xondra.Engine.Crypto;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Hashing;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;

namespace Xondra.Engine.Restore;

public class RestoreService(
    IFileSystem fileSystem,
    BlobStore blobStore,
    BackupSetRepository backupSetRepository,
    BackupSetEmptyDirRepository emptyDirRepository,
    ErrorRepository errorRepository)
{
    public RestoreSummary RestoreFiles(long backupId, string targetRoot)
    {
        var restored = 0;
        var failed = 0;
        foreach (var file in backupSetRepository.GetRestorableFiles(backupId))
        {
            if (RestoreOne(backupId, file, targetRoot))
                restored++;
            else
                failed++;
        }

        return new RestoreSummary(restored, failed);
    }

    public void RestoreEmptyDirectories(long backupId, string targetRoot)
    {
        foreach (var (_, directory) in emptyDirRepository.GetDirectories(backupId))
            fileSystem.CreateDirectory(Path.Combine(targetRoot, directory));
    }

    private bool RestoreOne(long backupId, RestorableFileRecord file, string targetRoot)
    {
        var targetPath = Path.Combine(targetRoot, file.Directory, file.Filename);
        try
        {
            fileSystem.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            using (var encrypted = blobStore.Read(file.BackupHash))
            using (var destination = fileSystem.Create(targetPath))
                BlobCodec.DecryptThenDecompress(encrypted, destination, file.OriginalFileHash);

            string restoredHash;
            using (var restored = fileSystem.OpenRead(targetPath))
                restoredHash = Sha512Hasher.HashStream(restored);

            if (restoredHash != file.OriginalFileHash)
            {
                errorRepository.Insert(backupId, nameof(RestoreFiles),
                    $"Restored file '{targetPath}' failed hash verification and was deleted.");
                fileSystem.DeleteFile(targetPath);
                return false;
            }

            if (file.Attributes is { } attributes)
            {
                fileSystem.SetTimestampsAndAttributes(targetPath,
                    ParseTimestamp(file.CreationTime), ParseTimestamp(file.LastWriteTime), (FileAttributes)attributes);
            }

            return true;
        }
        catch (Exception ex)
        {
            errorRepository.Insert(backupId, nameof(RestoreFiles), ex.Message);
            TryDelete(targetPath);
            return false;
        }
    }

    private void TryDelete(string targetPath)
    {
        try { fileSystem.DeleteFile(targetPath); } catch { /* best-effort cleanup after a failed restore */ }
    }

    private static DateTime ParseTimestamp(string? value) =>
        value is null ? default : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}

public record RestoreSummary(int RestoredCount, int FailedCount);
