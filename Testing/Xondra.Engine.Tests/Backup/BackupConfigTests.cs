using FluentAssertions;
using Xondra.Engine.Backup;
using Xunit;

namespace Xondra.Engine.Tests.Backup;

public class BackupConfigTests
{
    [Fact]
    public void Parse_reads_all_fields_from_the_settings_json()
    {
        var json = """{"SourceDirectory":"C:\\Data","ComputerGUID":"guid-1","UseVSS":"true","BackupType":"ARCHIVEBIT"}""";

        var config = BackupConfig.Parse(json);

        config.SourceDirectory.Should().Be(@"C:\Data");
        config.ComputerGuid.Should().Be("guid-1");
        config.UseVss.Should().BeTrue();
        config.BackupType.Should().Be("ARCHIVEBIT");
    }

    [Fact]
    public void Parse_accepts_a_json_boolean_for_UseVSS()
    {
        var json = """{"SourceDirectory":"C:\\Data","UseVSS":true}""";

        var config = BackupConfig.Parse(json);

        config.UseVss.Should().BeTrue();
    }

    [Fact]
    public void Parse_defaults_UseVSS_to_false_and_BackupType_to_FULL_when_absent()
    {
        var json = """{"SourceDirectory":"C:\\Data"}""";

        var config = BackupConfig.Parse(json);

        config.UseVss.Should().BeFalse();
        config.BackupType.Should().Be("FULL");
        config.ComputerGuid.Should().BeNull();
    }

    [Fact]
    public void Parse_throws_when_SourceDirectory_is_missing()
    {
        const string json = "{}";

        var act = () => BackupConfig.Parse(json);

        act.Should().Throw<FormatException>();
    }
}
