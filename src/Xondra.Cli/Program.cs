using Microsoft.Data.Sqlite;
using Xondra.Engine.Backup;
using Xondra.Engine.Data;
using Xondra.Engine.Data.Catalog;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Restore;
using Xondra.Engine.Scanning;
using Xondra.Engine.Storage;
using Xondra.Engine.Verify;
using Xondra.Engine.Vss;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0].ToLowerInvariant() switch
{
    "backup" => RunBackup(args),
    "verify" => RunVerify(args),
    "restore" => RunRestore(args),
    _ => Unknown(),
};

int Unknown()
{
    PrintUsage();
    return 1;
}

static int RunBackup(string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: backup <jobId> <cfgDbPath> <backupDirectory>");
        return 1;
    }

    var jobId = long.Parse(args[1]);
    var cfgDbPath = args[2];
    var backupDirectory = args[3];
    Directory.CreateDirectory(backupDirectory);

    using var cfgConnection = OpenExistingConfig(cfgDbPath);
    using var catalogConnection = OpenOrCreateCatalog(backupDirectory);

    var fileSystem = new WindowsFileSystem();
    var blobStore = new BlobStore(backupDirectory);
    var driveRepository = new LocalDriveRepository(catalogConnection);
    var directoryRepository = new LocalDirectoryRepository(catalogConnection);
    var filenameRepository = new LocalFilenameRepository(catalogConnection);
    var fileRepository = new FileRepository(catalogConnection);
    var backupSetRepository = new BackupSetRepository(catalogConnection);
    var backupRepository = new BackupRepository(catalogConnection);
    var errorRepository = new ErrorRepository(catalogConnection);
    var emptyDirRepository = new BackupSetEmptyDirRepository(catalogConnection);
    var jobSettingsRepository = new JobSettingsRepository(cfgConnection);

    var incrementalPlanner = new IncrementalPlanner(driveRepository, directoryRepository, filenameRepository, backupSetRepository);
    var fileBackupWorker = new FileBackupWorker(fileSystem, blobStore, fileRepository, backupSetRepository,
        driveRepository, directoryRepository, filenameRepository, errorRepository);

    IVssSnapshotProvider vssSnapshotProvider = new AlphaVssSnapshotProvider();

    var runner = new BackupRunner(fileSystem, vssSnapshotProvider, jobSettingsRepository, backupRepository,
        backupSetRepository, emptyDirRepository, driveRepository, directoryRepository, errorRepository,
        incrementalPlanner, fileBackupWorker);

    var backupId = runner.Run(jobId);
    Console.WriteLine($"Backup {backupId} complete.");
    return 0;
}

static int RunVerify(string[] args)
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: verify <CurrentBackup|AllNotVerified|All> <backupDirectory> [backupId]");
        return 1;
    }

    if (!Enum.TryParse<VerifyMode>(args[1], ignoreCase: true, out var mode))
    {
        Console.Error.WriteLine($"Unknown verify mode '{args[1]}'. Expected CurrentBackup, AllNotVerified, or All.");
        return 1;
    }

    var backupDirectory = args[2];
    long? backupId = args.Length > 3 ? long.Parse(args[3]) : null;

    if (mode == VerifyMode.CurrentBackup && backupId is null)
    {
        Console.Error.WriteLine("CurrentBackup mode requires a backupId argument.");
        return 1;
    }

    using var catalogConnection = OpenOrCreateCatalog(backupDirectory);
    var fileRepository = new FileRepository(catalogConnection);
    var blobStore = new BlobStore(backupDirectory);
    var verifier = new FileVerifier(fileRepository, blobStore);

    var summary = verifier.VerifyFiles(mode, backupId);
    Console.WriteLine($"Verified: {summary.PassedCount} passed, {summary.FailedCount} failed.");
    return summary.FailedCount == 0 ? 0 : 1;
}

static int RunRestore(string[] args)
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: restore <backupId> <restoreTargetDirectory> <backupDirectory>");
        return 1;
    }

    var backupId = long.Parse(args[1]);
    var restoreTargetDirectory = args[2];
    var backupDirectory = args[3];

    using var catalogConnection = OpenOrCreateCatalog(backupDirectory);
    var backupSetRepository = new BackupSetRepository(catalogConnection);
    var emptyDirRepository = new BackupSetEmptyDirRepository(catalogConnection);
    var errorRepository = new ErrorRepository(catalogConnection);
    var fileSystem = new WindowsFileSystem();
    var blobStore = new BlobStore(backupDirectory);

    var restoreService = new RestoreService(fileSystem, blobStore, backupSetRepository, emptyDirRepository, errorRepository);
    var summary = restoreService.RestoreFiles(backupId, restoreTargetDirectory);
    restoreService.RestoreEmptyDirectories(backupId, restoreTargetDirectory);

    Console.WriteLine($"Restored: {summary.RestoredCount} succeeded, {summary.FailedCount} failed.");
    return summary.FailedCount == 0 ? 0 : 1;
}

static SqliteConnection OpenExistingConfig(string path)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"Config database not found: {path}", path);

    var connection = new SqliteConnection($"Data Source={path}");
    connection.Open();
    return connection;
}

static SqliteConnection OpenOrCreateCatalog(string backupDirectory)
{
    var dbPath = Path.Combine(backupDirectory, "Xondra.dat");
    var connection = new SqliteConnection($"Data Source={dbPath}");
    connection.Open();
    SqliteSchemaInitializer.InitializeCatalog(connection);
    return connection;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Usage:
          Xondra.Cli backup <jobId> <cfgDbPath> <backupDirectory>
          Xondra.Cli verify <CurrentBackup|AllNotVerified|All> <backupDirectory> [backupId]
          Xondra.Cli restore <backupId> <restoreTargetDirectory> <backupDirectory>
        """);
}
