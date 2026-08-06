using FluentAssertions;
using Xondra.Engine.Data.Config;
using Xondra.Engine.Tests.TestSupport;
using Xunit;

namespace Xondra.Engine.Tests.Data.Config;

public class JobSettingsRepositoryTests
{
    [Fact]
    public void GetBackupSettingsJson_returns_null_when_the_job_has_no_values()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateConfig(temp.FullPath);
        var repository = new JobSettingsRepository(connection);

        repository.GetBackupSettingsJson(jobId: 1).Should().BeNull();
    }

    [Fact]
    public void GetBackupSettingsJson_merges_job_specific_and_global_attribute_values()
    {
        using var temp = new TempDirectory();
        using var connection = SqliteTestDatabase.CreateConfig(temp.FullPath);
        Seed(connection);
        var repository = new JobSettingsRepository(connection);

        var json = repository.GetBackupSettingsJson(jobId: 1);

        json.Should().NotBeNull();
        json.Should().Contain("\"SourceDirectory\":\"C:\\\\Data\"");
        json.Should().Contain("\"UseVSS\":\"true\"");
    }

    private static void Seed(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        Execute(connection, "INSERT INTO Job (ID, Type) VALUES (1, 'FullBackup')");
        Execute(connection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (1, 'SourceDirectory', 0, 'FullBackup')");
        Execute(connection, "INSERT INTO Attribute (ID, Name, MultipleValues, JobTypes) VALUES (2, 'UseVSS', 0, 'FullBackup')");
        // job-specific value
        Execute(connection, "INSERT INTO Value (AttributeID, Value, JobID) VALUES (1, 'C:\\Data', 1)");
        // global/default value (JobID = 0) picked up because job 1 doesn't override it
        Execute(connection, "INSERT INTO Value (AttributeID, Value, JobID) VALUES (2, 'true', 0)");
    }

    private static void Execute(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
