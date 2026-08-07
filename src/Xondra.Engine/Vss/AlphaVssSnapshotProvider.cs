using Alphaleonis.Win32.Vss;

namespace Xondra.Engine.Vss;

// Not covered by the fast unit test suite — real VSS requires an elevated
// Windows session with the shadow copy service running. Exercised by
// Testing/Xondra.Engine.IntegrationTests, run manually (see BuildPlan.md Phase 9).
public class AlphaVssSnapshotProvider : IVssSnapshotProvider
{
    public IVssSnapshot CreateSnapshot(string sourceRoot)
    {
        var volumeRoot = Path.GetPathRoot(sourceRoot);
        if (string.IsNullOrEmpty(volumeRoot))
            throw new ArgumentException($"Could not determine the volume root of '{sourceRoot}'.", nameof(sourceRoot));

        var factory = VssFactoryProvider.Default.GetVssFactory();
        var backup = factory.CreateVssBackupComponents();
        backup.InitializeForBackup(null);
        backup.GatherWriterMetadata();
        backup.FreeWriterMetadata();

        var snapshotSetId = backup.StartSnapshotSet();
        if (!backup.IsVolumeSupported(volumeRoot))
            throw new NotSupportedException($"VSS snapshots are not supported for volume '{volumeRoot}'.");
        var snapshotId = backup.AddToSnapshotSet(volumeRoot);

        backup.SetBackupState(selectComponents: false, backupBootableSystemState: false, VssBackupType.Full, partialFileSupport: false);
        backup.PrepareForBackup();
        backup.DoSnapshotSet();

        var properties = backup.GetSnapshotProperties(snapshotId);
        var relativePath = sourceRoot[volumeRoot.Length..];
        var snapshotRoot = Path.Combine(properties.SnapshotDeviceObject, relativePath);

        return new Snapshot(backup, snapshotSetId, snapshotRoot);
    }

    private sealed class Snapshot(IVssBackupComponents backup, Guid snapshotSetId, string snapshotRoot) : IVssSnapshot
    {
        public string SnapshotRoot => snapshotRoot;

        public void Dispose()
        {
            try
            {
                backup.DeleteSnapshotSet(snapshotSetId, forceDelete: true);
            }
            finally
            {
                backup.Dispose();
            }
        }
    }
}
