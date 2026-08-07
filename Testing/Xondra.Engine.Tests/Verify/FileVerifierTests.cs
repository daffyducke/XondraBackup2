using System.Text;
using FluentAssertions;
using Xondra.Engine.Crypto;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Hashing;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xondra.Engine.Verify;
using Xunit;

namespace Xondra.Engine.Tests.Verify;

public class FileVerifierTests
{
    private const string CorruptedOriginalHash =
        "ddaf35a193617abacc417349ae20413112e6fa4e89a97ea20a9eeee64b55d39a2192992a274fc1a836ba3c23a3feebbd454d4423643ce80e2a9ac94fa54ca49f";
    private const string CorruptedBackupHash =
        "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3";

    private static long SeedGoodFile(FileRepository fileRepository, BlobStore blobStore, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var originalHash = Sha512Hasher.HashStream(new MemoryStream(bytes));

        using var ciphertext = new MemoryStream();
        BlobCodec.CompressThenEncrypt(new MemoryStream(bytes), ciphertext, originalHash);
        ciphertext.Position = 0;
        var backupHash = Sha512Hasher.HashStream(ciphertext);
        ciphertext.Position = 0;
        blobStore.Write(backupHash, ciphertext);

        var fileId = fileRepository.Insert(originalHash, "hmac-value", bytes.Length);
        fileRepository.MarkStored(fileId, backupHash, ciphertext.Length);
        return fileId;
    }

    private static long SeedCorruptedFile(FileRepository fileRepository, BlobStore blobStore)
    {
        blobStore.Write(CorruptedBackupHash, new MemoryStream(Encoding.UTF8.GetBytes("not a valid encrypted blob")));
        var fileId = fileRepository.Insert(CorruptedOriginalHash, "hmac-value", 5);
        fileRepository.MarkStored(fileId, CorruptedBackupHash, 5);
        return fileId;
    }

    [Fact]
    public void VerifyFiles_marks_a_good_file_as_verified()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        var fileRepository = new FileRepository(connection);
        var blobStore = new BlobStore(blobTemp.FullPath);
        var goodFileId = SeedGoodFile(fileRepository, blobStore, "hello world");
        var verifier = new FileVerifier(fileRepository, blobStore);

        var summary = verifier.VerifyFiles(VerifyMode.All);

        summary.PassedCount.Should().Be(1);
        summary.FailedCount.Should().Be(0);
        fileRepository.GetById(goodFileId)!.LocalVerified.Should().BeTrue();
    }

    [Fact]
    public void VerifyFiles_marks_a_corrupted_file_as_failed()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        var fileRepository = new FileRepository(connection);
        var blobStore = new BlobStore(blobTemp.FullPath);
        var corruptedFileId = SeedCorruptedFile(fileRepository, blobStore);
        var verifier = new FileVerifier(fileRepository, blobStore);

        var summary = verifier.VerifyFiles(VerifyMode.All);

        summary.PassedCount.Should().Be(0);
        summary.FailedCount.Should().Be(1);
        fileRepository.GetById(corruptedFileId)!.LocalVerified.Should().BeFalse();
    }

    [Fact]
    public void VerifyFiles_does_not_leak_temp_files()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        var fileRepository = new FileRepository(connection);
        var blobStore = new BlobStore(blobTemp.FullPath);
        SeedGoodFile(fileRepository, blobStore, "hello world");
        SeedCorruptedFile(fileRepository, blobStore);
        var verifier = new FileVerifier(fileRepository, blobStore);

        var tempFilesBefore = Directory.GetFiles(Path.GetTempPath()).Length;

        verifier.VerifyFiles(VerifyMode.All);

        var tempFilesAfter = Directory.GetFiles(Path.GetTempPath()).Length;
        tempFilesAfter.Should().Be(tempFilesBefore);
    }

    [Fact]
    public void VerifyFiles_AllNotVerified_skips_files_already_verified()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        var fileRepository = new FileRepository(connection);
        var blobStore = new BlobStore(blobTemp.FullPath);
        var verifier = new FileVerifier(fileRepository, blobStore);
        var firstFileId = SeedGoodFile(fileRepository, blobStore, "already verified");
        verifier.VerifyFiles(VerifyMode.All);
        var secondFileId = SeedGoodFile(fileRepository, blobStore, "not yet verified");

        var summary = verifier.VerifyFiles(VerifyMode.AllNotVerified);

        summary.PassedCount.Should().Be(1);
        fileRepository.GetById(firstFileId)!.LocalVerified.Should().BeTrue();
        fileRepository.GetById(secondFileId)!.LocalVerified.Should().BeTrue();
    }

    [Fact]
    public void VerifyFiles_CurrentBackup_only_verifies_files_in_the_given_backup()
    {
        using var dbTemp = new TempDirectory();
        using var blobTemp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);
        var fileRepository = new FileRepository(connection);
        var blobStore = new BlobStore(blobTemp.FullPath);
        var inBackupFileId = SeedGoodFile(fileRepository, blobStore, "in this backup");
        var outsideBackupFileId = SeedGoodFile(fileRepository, blobStore, "in a different backup");

        var backupRepository = new BackupRepository(connection);
        var backupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var driveId = new LocalDriveRepository(connection).GetOrInsert(@"C:\");
        var dirId = new LocalDirectoryRepository(connection).GetOrInsert("root");
        var filenameId = new LocalFilenameRepository(connection).GetOrInsert("a.txt");
        new BackupSetRepository(connection).Insert(backupId, dirId, inBackupFileId, filenameId, driveId,
            null, null, null, null);

        var verifier = new FileVerifier(fileRepository, blobStore);
        var summary = verifier.VerifyFiles(VerifyMode.CurrentBackup, backupId);

        summary.PassedCount.Should().Be(1);
        fileRepository.GetById(inBackupFileId)!.LocalVerified.Should().BeTrue();
        fileRepository.GetById(outsideBackupFileId)!.LocalVerified.Should().BeNull();
    }
}
