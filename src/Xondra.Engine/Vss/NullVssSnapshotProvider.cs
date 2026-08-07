namespace Xondra.Engine.Vss;

public class NullVssSnapshotProvider : IVssSnapshotProvider
{
    public IVssSnapshot CreateSnapshot(string sourceRoot) => new PassthroughSnapshot(sourceRoot);

    private sealed class PassthroughSnapshot(string sourceRoot) : IVssSnapshot
    {
        public string SnapshotRoot => sourceRoot;
        public void Dispose() { }
    }
}
