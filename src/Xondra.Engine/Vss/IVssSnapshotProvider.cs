namespace Xondra.Engine.Vss;

public interface IVssSnapshotProvider
{
    IVssSnapshot CreateSnapshot(string sourceRoot);
}

public interface IVssSnapshot : IDisposable
{
    string SnapshotRoot { get; }
}
