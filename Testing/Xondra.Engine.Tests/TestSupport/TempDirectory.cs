namespace Xondra.Engine.Tests.TestSupport;

public sealed class TempDirectory : IDisposable
{
    public string FullPath { get; } =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "xondra-tests-" + Guid.NewGuid())).FullName;

    public void Dispose() => Directory.Delete(FullPath, recursive: true);
}
