using FluentAssertions;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Vss;

// FakeVssSnapshotProvider is the seam orchestration (BackupRunner, Phase 10)
// will use to prove it asks for and tears down a snapshot. These tests prove
// the fake itself models that create/use/dispose lifecycle correctly.
public class FakeVssSnapshotProviderTests
{
    [Fact]
    public void CreateSnapshot_points_at_the_configured_directory_and_records_the_request()
    {
        using var temp = new TempDirectory();
        var provider = new FakeVssSnapshotProvider(temp.FullPath);

        using var snapshot = provider.CreateSnapshot(@"C:\Users\daffy\Documents");

        snapshot.SnapshotRoot.Should().Be(temp.FullPath);
        provider.CreateSnapshotCallCount.Should().Be(1);
        provider.LastRequestedSourceRoot.Should().Be(@"C:\Users\daffy\Documents");
        provider.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Disposing_the_snapshot_tears_it_down()
    {
        using var temp = new TempDirectory();
        var provider = new FakeVssSnapshotProvider(temp.FullPath);
        var snapshot = provider.CreateSnapshot(@"C:\Users\daffy\Documents");

        snapshot.Dispose();

        provider.IsDisposed.Should().BeTrue();
    }
}
