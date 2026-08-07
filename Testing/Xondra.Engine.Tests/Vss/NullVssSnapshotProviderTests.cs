using FluentAssertions;
using Xondra.Engine.Vss;
using Xunit;

namespace Xondra.Engine.Tests.Vss;

public class NullVssSnapshotProviderTests
{
    [Fact]
    public void CreateSnapshot_passes_the_source_root_through_unchanged()
    {
        var provider = new NullVssSnapshotProvider();

        using var snapshot = provider.CreateSnapshot(@"C:\Users\daffy\Documents");

        snapshot.SnapshotRoot.Should().Be(@"C:\Users\daffy\Documents");
    }

    [Fact]
    public void Dispose_does_not_throw()
    {
        var provider = new NullVssSnapshotProvider();
        var snapshot = provider.CreateSnapshot(@"C:\Users\daffy\Documents");

        var act = snapshot.Dispose;

        act.Should().NotThrow();
    }
}
