using Microsoft.Data.Sqlite;
using Spectra.CLI.Commands.Run;
using Spectra.CLI.Infrastructure;
using Spectra.CLI.Options;
using Spectra.Core.Storage;
using Spectra.MCP.Storage;

namespace Spectra.CLI.Tests.Commands.Run;

/// <summary>
/// Runs recorded by <c>spectra run</c> must land where the dashboard reads them
/// (<c>.execution/spectra.db</c>). Up to 2.7.0 they went to <c>.execution/.execution/spectra.db</c>, so the
/// dashboard's Run History never showed them; such a database is moved once.
/// </summary>
public class RunDatabaseLocationTests
{
    private static RunHandler Handler(string root) => new(VerbosityLevel.Quiet, OutputFormat.Json, root);

    private static string Db(string root) => Path.Combine(root, ".execution", "spectra.db");

    private static string NestedDir(string root) => Path.Combine(root, ".execution", ".execution");

    [Fact]
    public async Task Start_WritesTheDatabaseWhereTheDashboardReadsIt()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));

        Assert.Equal(ExitCodes.Success, await Handler(ws.Root).StartAsync("s", null, null, null, null, null, null));
        SqliteConnection.ClearAllPools();

        Assert.True(File.Exists(Db(ws.Root)));
        Assert.False(Directory.Exists(NestedDir(ws.Root)));
        await using var reader = new ExecutionDbReader(ws.Root);
        var runs = await reader.GetRunSummariesAsync();
        Assert.Equal("s", Assert.Single(runs).Suite);
    }

    [Fact]
    public async Task NextRunCommand_MovesADatabaseAnOlderBuildWroteOneFolderTooDeep()
    {
        using var ws = new RunTestWorkspace();
        ws.WriteSuite("s", ("TC-001", "A", "high", null));
        await Handler(ws.Root).StartAsync("s", null, null, null, null, null, null);
        SqliteConnection.ClearAllPools();

        // Recreate the 2.7.0 layout: the same database, one folder deeper.
        Directory.CreateDirectory(NestedDir(ws.Root));
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            if (File.Exists(Db(ws.Root) + suffix))
            {
                File.Move(Db(ws.Root) + suffix, Path.Combine(NestedDir(ws.Root), "spectra.db" + suffix));
            }
        }

        await Handler(ws.Root).ListActiveAsync();
        SqliteConnection.ClearAllPools();

        Assert.True(File.Exists(Db(ws.Root)));
        Assert.False(Directory.Exists(NestedDir(ws.Root)));
        await using var reader = new ExecutionDbReader(ws.Root);
        Assert.Equal("s", Assert.Single(await reader.GetRunSummariesAsync()).Suite);
    }

    [Fact]
    public void Migration_LeavesBothFilesAloneWhenBothExist()
    {
        using var ws = new RunTestWorkspace();
        Directory.CreateDirectory(NestedDir(ws.Root));
        File.WriteAllText(Path.Combine(NestedDir(ws.Root), "spectra.db"), "older");
        File.WriteAllText(Db(ws.Root), "current");

        Assert.Equal(ExecutionDbLocation.MigrationOutcome.BothExist, ExecutionDbLocation.MigrateNestedDatabase(ws.Root));
        Assert.Equal("older", File.ReadAllText(Path.Combine(NestedDir(ws.Root), "spectra.db")));
        Assert.Equal("current", File.ReadAllText(Db(ws.Root)));
    }
}
