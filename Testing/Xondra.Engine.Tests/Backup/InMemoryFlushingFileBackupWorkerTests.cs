using FluentAssertions;
using Xondra.Engine.Backup;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Scanning;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class InMemoryFlushingFileBackupWorkerTests
{
    private static ScannedFile MakeScannedFile(string filename) =>
        new(@"C:\", "root", filename, $@"C:\root\{filename}", 1,
            new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc),
            FileAttributes.Normal);

    [Fact]
    public void BackupFile_delegates_to_the_inner_worker_on_every_call()
    {
        using var dbTemp = new TempDirectory();
        using var inMemoryConnection = SqliteTestDatabase.CreateInMemoryCatalog();
        using var onDiskConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);

        var backupRepository = new BackupRepository(inMemoryConnection);
        var backupId = backupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var inner = new RecordingFileBackupWorker();
        var worker = new InMemoryFlushingFileBackupWorker(inner, inMemoryConnection, onDiskConnection, flushInterval: 3);

        for (var i = 0; i < 5; i++)
            worker.BackupFile(backupId, MakeScannedFile($"file{i}.txt"));

        inner.CallCount.Should().Be(5);
    }

    [Fact]
    public void BackupFile_flushes_the_in_memory_connection_to_disk_every_N_calls()
    {
        using var dbTemp = new TempDirectory();
        using var inMemoryConnection = SqliteTestDatabase.CreateInMemoryCatalog();
        using var onDiskConnection = SqliteTestDatabase.CreateCatalog(dbTemp.FullPath);

        var inMemoryBackupRepository = new BackupRepository(inMemoryConnection);
        var backupId = inMemoryBackupRepository.Start("computer-guid", DateTime.UtcNow, null);
        var inMemoryBackupSetRepository = new BackupSetRepository(inMemoryConnection);
        var driveRepository = new LocalDriveRepository(inMemoryConnection);
        var directoryRepository = new LocalDirectoryRepository(inMemoryConnection);
        var filenameRepository = new LocalFilenameRepository(inMemoryConnection);
        var fileRepository = new FileRepository(inMemoryConnection);

        var inner = new RecordingFileBackupWorker(file =>
        {
            var fileId = fileRepository.Insert($"hash-{file.Filename}", "hmac", 1);
            var driveId = driveRepository.GetOrInsert(file.Drive);
            var dirId = directoryRepository.GetOrInsert(file.Directory);
            var filenameId = filenameRepository.GetOrInsert(file.Filename);
            inMemoryBackupSetRepository.Insert(backupId, dirId, fileId, filenameId, driveId, null, null, null, null);
        });
        var worker = new InMemoryFlushingFileBackupWorker(inner, inMemoryConnection, onDiskConnection, flushInterval: 3);

        worker.BackupFile(backupId, MakeScannedFile("a.txt"));
        worker.BackupFile(backupId, MakeScannedFile("b.txt"));
        new BackupSetRepository(onDiskConnection).GetByBackupId(backupId).Should().BeEmpty("only 2 of 3 files processed, no flush yet");

        worker.BackupFile(backupId, MakeScannedFile("c.txt"));
        new BackupSetRepository(onDiskConnection).GetByBackupId(backupId).Should().HaveCount(3, "the 3rd call crosses the flush interval");

        worker.BackupFile(backupId, MakeScannedFile("d.txt"));
        new BackupSetRepository(onDiskConnection).GetByBackupId(backupId).Should().HaveCount(3, "the 4th call hasn't crossed another interval yet");

        worker.Flush();
        new BackupSetRepository(onDiskConnection).GetByBackupId(backupId).Should().HaveCount(4, "a manual flush catches up any remaining writes");
    }

    private sealed class RecordingFileBackupWorker(Action<ScannedFile>? onBackupFile = null) : IFileBackupWorker
    {
        public int CallCount { get; private set; }

        public void BackupFile(long backupId, ScannedFile file)
        {
            CallCount++;
            onBackupFile?.Invoke(file);
        }
    }
}
