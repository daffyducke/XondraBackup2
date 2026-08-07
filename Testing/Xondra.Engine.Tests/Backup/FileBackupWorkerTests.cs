using System.Text;
using FluentAssertions;
using Xondra.Engine.Backup;
using Xondra.Engine.Crypto;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Hashing;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class FileBackupWorkerTests
{
    private static ScannedFile MakeScannedFile(string drive, string directory, string filename, string fullPath, long size) =>
        new(drive, directory, filename, fullPath, size,
            new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc),
            FileAttributes.Archive);

    private static (FakeFileSystem FileSystem, BlobStore BlobStore, FileRepository FileRepo,
        BackupSetRepository BackupSetRepo, ErrorRepository ErrorRepo, FileBackupWorker Worker) Build(
        Microsoft.Data.Sqlite.SqliteConnection connection, string blobRoot)
    {
        var fileSystem = new FakeFileSystem();
        var blobStore = new BlobStore(blobRoot);
        var fileRepository = new FileRepository(connection);
        var backupSetRepository = new BackupSetRepository(connection);
        var errorRepository = new ErrorRepository(connection);
        var worker = new FileBackupWorker(
            fileSystem, blobStore, fileRepository, backupSetRepository,
            new LocalDriveRepository(connection), new LocalDirectoryRepository(connection),
            new LocalFilenameRepository(connection), errorRepository);

        return (fileSystem, blobStore, fileRepository, backupSetRepository, errorRepository, worker);
    }

    [Fact]
    public void BackupFile_stores_new_content_and_records_the_backup_set_row()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (fileSystem, blobStore, fileRepo, backupSetRepo, errorRepo, worker) = Build(connection, temp.FullPath);
        var content = Encoding.UTF8.GetBytes("hello world");
        fileSystem.AddFileContent(@"C:\root\report.docx", content);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var scannedFile = MakeScannedFile(@"C:\", "root", "report.docx", @"C:\root\report.docx", content.Length);

        worker.BackupFile(backupId, scannedFile);

        var originalHash = Sha512Hasher.HashStream(new MemoryStream(content));
        var stored = fileRepo.FindByHash(originalHash);
        stored.Should().NotBeNull();
        stored!.BackupHash.Should().NotBeNull();
        stored.FilesizeCompressed.Should().BePositive();

        blobStore.Exists(stored.BackupHash!).Should().BeTrue();
        using var ciphertext = blobStore.Read(stored.BackupHash!);
        using var decoded = new MemoryStream();
        BlobCodec.DecryptThenDecompress(ciphertext, decoded, originalHash);
        decoded.ToArray().Should().Equal(content);

        var row = backupSetRepo.GetByBackupId(backupId).Should().ContainSingle().Subject;
        row.FileId.Should().Be(stored.Id);
        row.Attributes.Should().Be((int)FileAttributes.Archive);

        fileSystem.ClearedArchiveBitCalls.Should().ContainSingle().Which.Should().Be(@"C:\root\report.docx");
        errorRepo.GetByBackupId(backupId).Should().BeEmpty();
    }

    [Fact]
    public void BackupFile_deduplicates_identical_content_backed_up_from_two_paths()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (fileSystem, blobStore, fileRepo, backupSetRepo, _, worker) = Build(connection, temp.FullPath);
        var content = Encoding.UTF8.GetBytes("duplicate content");
        fileSystem.AddFileContent(@"C:\root\a.txt", content);
        fileSystem.AddFileContent(@"C:\root\copy\b.txt", content);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var fileA = MakeScannedFile(@"C:\", "root", "a.txt", @"C:\root\a.txt", content.Length);
        var fileB = MakeScannedFile(@"C:\", @"root\copy", "b.txt", @"C:\root\copy\b.txt", content.Length);

        worker.BackupFile(backupId, fileA);
        worker.BackupFile(backupId, fileB);

        var originalHash = Sha512Hasher.HashStream(new MemoryStream(content));
        var stored = fileRepo.FindByHash(originalHash);
        stored.Should().NotBeNull();

        var rows = backupSetRepo.GetByBackupId(backupId);
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.FileId == stored!.Id);

        var blobFileCount = Directory.GetFiles(temp.FullPath, "*", SearchOption.AllDirectories)
            .Count(path => !Path.GetFileName(path).StartsWith("catalog.db"));
        blobFileCount.Should().Be(1);
    }

    [Fact]
    public void BackupFile_logs_a_failure_and_lets_the_caller_continue_with_the_next_file()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateCatalog(temp.FullPath);
        var (fileSystem, _, fileRepo, backupSetRepo, errorRepo, worker) = Build(connection, temp.FullPath);
        fileSystem.FailOpenRead(@"C:\root\bad.txt");
        var goodContent = Encoding.UTF8.GetBytes("this one works");
        fileSystem.AddFileContent(@"C:\root\good.txt", goodContent);
        var backupId = new BackupRepository(connection).Start("computer-guid", DateTime.UtcNow, null);
        var badFile = MakeScannedFile(@"C:\", "root", "bad.txt", @"C:\root\bad.txt", 123);
        var goodFile = MakeScannedFile(@"C:\", "root", "good.txt", @"C:\root\good.txt", goodContent.Length);

        worker.BackupFile(backupId, badFile);
        worker.BackupFile(backupId, goodFile);

        var errors = errorRepo.GetByBackupId(backupId);
        errors.Should().ContainSingle(e => e.Error.Contains("Simulated read failure"));

        backupSetRepo.GetByBackupId(backupId).Should().ContainSingle();
        var originalHash = Sha512Hasher.HashStream(new MemoryStream(goodContent));
        fileRepo.FindByHash(originalHash).Should().NotBeNull();
    }
}
