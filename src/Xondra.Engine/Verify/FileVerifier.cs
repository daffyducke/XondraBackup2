using Xondra.Engine.Crypto;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Hashing;
using Xondra.Engine.Storage;

namespace Xondra.Engine.Verify;

public class FileVerifier(FileRepository fileRepository, BlobStore blobStore)
{
    public VerifySummary VerifyFiles(VerifyMode mode, long? backupId = null)
    {
        var candidates = mode switch
        {
            VerifyMode.CurrentBackup => fileRepository.FindByBackupId(
                backupId ?? throw new ArgumentException("CurrentBackup mode requires a backupId.", nameof(backupId))),
            VerifyMode.AllNotVerified => fileRepository.FindNotVerified(),
            VerifyMode.All => fileRepository.FindAllStored(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

        var passed = 0;
        var failed = 0;
        foreach (var file in candidates)
        {
            var verified = VerifyOne(file);
            fileRepository.SetVerified(file.Id, verified);
            if (verified) passed++; else failed++;
        }

        return new VerifySummary(passed, failed);
    }

    private bool VerifyOne(FileRecord file)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            using (var encrypted = blobStore.Read(file.BackupHash!))
            using (var destination = File.Create(tempPath))
            {
                BlobCodec.DecryptThenDecompress(encrypted, destination, file.OriginalFileHash);
            }

            return Sha512Hasher.HashFile(tempPath) == file.OriginalFileHash;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}

public record VerifySummary(int PassedCount, int FailedCount);
