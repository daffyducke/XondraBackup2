using Xondra.Engine.Crypto;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Hashing;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;

namespace Xondra.Engine.Backup;

public class FileBackupWorker(
    IFileSystem fileSystem,
    BlobStore blobStore,
    FileRepository fileRepository,
    BackupSetRepository backupSetRepository,
    LocalDriveRepository driveRepository,
    LocalDirectoryRepository directoryRepository,
    LocalFilenameRepository filenameRepository,
    ErrorRepository errorRepository)
{
    public void BackupFile(long backupId, ScannedFile file)
    {
        try
        {
            BackupFileCore(backupId, file);
        }
        catch (Exception ex)
        {
            errorRepository.Insert(backupId, nameof(BackupFile), ex.Message);
        }
    }

    private void BackupFileCore(long backupId, ScannedFile file)
    {
        using var source = fileSystem.OpenRead(file.FullPath);
        var originalFileHash = Sha512Hasher.HashStream(source);

        source.Position = 0;
        var hmacKey = AesKeyDerivation.Derive(originalFileHash).Key;
        var origHmacSha512 = HmacSha512Deriver.Derive(hmacKey, source);

        var existing = fileRepository.FindByHash(originalFileHash);
        var fileId = existing?.Id ?? fileRepository.Insert(originalFileHash, origHmacSha512, file.Size);

        if (existing?.BackupHash is null)
        {
            source.Position = 0;
            StoreBlob(fileId, source, originalFileHash);
        }

        var driveId = driveRepository.GetOrInsert(file.Drive);
        var dirId = directoryRepository.GetOrInsert(file.Directory);
        var filenameId = filenameRepository.GetOrInsert(file.Filename);

        backupSetRepository.Insert(backupId, dirId, fileId, filenameId, driveId,
            error: null, attributes: (int)file.Attributes,
            creationTime: file.CreationTimeUtc.ToString("O"), lastWriteTime: file.LastWriteTimeUtc.ToString("O"));

        fileSystem.ClearArchiveBit(file.FullPath);
    }

    private void StoreBlob(long fileId, Stream source, string originalFileHash)
    {
        using var ciphertext = new MemoryStream();
        BlobCodec.CompressThenEncrypt(source, ciphertext, originalFileHash);

        ciphertext.Position = 0;
        var backupHash = Sha512Hasher.HashStream(ciphertext);

        ciphertext.Position = 0;
        blobStore.Write(backupHash, ciphertext);
        fileRepository.MarkStored(fileId, backupHash, ciphertext.Length);
    }
}
