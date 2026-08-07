using Xondra.Engine.Vss;

namespace Xondra.Engine.Tests.TestSupport;

public class FakeVssSnapshotProvider(string snapshotRoot) : IVssSnapshotProvider
{
    public int CreateSnapshotCallCount { get; private set; }
    public string? LastRequestedSourceRoot { get; private set; }
    public bool IsDisposed { get; private set; }

    public IVssSnapshot CreateSnapshot(string sourceRoot)
    {
        CreateSnapshotCallCount++;
        LastRequestedSourceRoot = sourceRoot;
        IsDisposed = false;
        return new FakeSnapshot(snapshotRoot, () => IsDisposed = true);
    }

    private sealed class FakeSnapshot(string snapshotRoot, Action onDispose) : IVssSnapshot
    {
        public string SnapshotRoot => snapshotRoot;
        public void Dispose() => onDispose();
    }
}
