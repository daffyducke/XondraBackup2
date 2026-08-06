using FluentAssertions;
using Xondra.Engine;
using Xunit;

namespace Xondra.Engine.Tests;

public class EngineInfoTests
{
    [Fact]
    public void SchemaVersion_is_one()
    {
        EngineInfo.SchemaVersion.Should().Be(1);
    }
}
