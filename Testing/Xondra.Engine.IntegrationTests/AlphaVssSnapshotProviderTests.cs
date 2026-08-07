using FluentAssertions;
using Xondra.Engine.Scanning;
using Xondra.Engine.Vss;
using Xunit;

namespace Xondra.Engine.IntegrationTests;

// Exercises the real Windows Volume Shadow Copy Service. Requires an
// elevated (Administrator) session with VSS running — run manually, not
// part of the default `dotnet test Testing/Xondra.Engine.Tests` loop.
// See BuildPlan.md Phase 9.
public class AlphaVssSnapshotProviderTests
{
    [Fact]
    public void CreateSnapshot_exposes_a_readable_copy_of_a_file_on_the_source_volume()
    {
        var sourceRoot = Path.GetTempPath();
        var probeFileName = $"vss-probe-{Guid.NewGuid()}.txt";
        var probeFile = Path.Combine(sourceRoot, probeFileName);
        File.WriteAllText(probeFile, "vss integration probe");

        try
        {
            var provider = new AlphaVssSnapshotProvider();
            using var snapshot = provider.CreateSnapshot(sourceRoot);

            snapshot.SnapshotRoot.Should().NotBeNullOrEmpty();

            var fileSystem = new WindowsFileSystem();
            var snapshotProbePath = Path.Combine(snapshot.SnapshotRoot, probeFileName);

            using var reader = new StreamReader(fileSystem.OpenRead(snapshotProbePath));
            reader.ReadToEnd().Should().Be("vss integration probe");
        }
        finally
        {
            File.Delete(probeFile);
        }
    }
}
