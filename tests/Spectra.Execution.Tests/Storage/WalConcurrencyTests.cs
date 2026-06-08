using Microsoft.Data.Sqlite;
using Spectra.Core.Models;
using Spectra.Core.Models.Execution;
using Spectra.MCP.Execution;
using Spectra.MCP.Identity;
using Spectra.MCP.Infrastructure;
using Spectra.MCP.Storage;

namespace Spectra.Execution.Tests.Storage;

/// <summary>
/// US4 (spec 065): short-lived processes are safe under concurrent SQLite access. WAL + busy_timeout
/// are set at connection open, so concurrent short-lived <see cref="ExecutionDb"/> instances writing
/// to one DB file do not fail with database-locked errors (SC-005).
/// </summary>
public class WalConcurrencyTests : IDisposable
{
    private readonly string _testDir;

    public WalConcurrencyTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-wal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public async Task ConnectionOpen_EnablesWalJournalMode()
    {
        await using var db = new ExecutionDb(Path.Combine(_testDir, ".execution"));
        var conn = await db.GetConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("wal", mode, ignoreCase: true);
    }

    [Fact]
    public async Task ConcurrentShortLivedWriters_DoNotHitLockFailures()
    {
        // Seed a run so concurrent writers append result/note rows against the same file.
        var execDir = Path.Combine(_testDir, ".execution");
        var entries = Enumerable.Range(1, 12)
            .Select(i => new TestIndexEntry { Id = $"TC-{i:000}", File = $"tc-{i}.md", Title = $"T{i}", Priority = "medium" })
            .ToList();

        string runId;
        await using (var seedDb = new ExecutionDb(execDir))
        {
            var engine = new ExecutionEngine(new RunRepository(seedDb), new ResultRepository(seedDb),
                new QueueSnapshotRepository(seedDb), new UserIdentityResolver(), new McpConfig { BasePath = _testDir });
            var (run, _) = await engine.StartRunAsync("suite", entries);
            runId = run.RunId;
        }

        // 8 concurrent short-lived processes, each its own ExecutionDb/connection, each writes a note.
        var exceptions = new List<Exception>();
        var tasks = Enumerable.Range(0, 8).Select(async n =>
        {
            try
            {
                await using var db = new ExecutionDb(execDir);
                var resultRepo = new ResultRepository(db);
                var results = await resultRepo.GetByRunIdAsync(runId);
                foreach (var r in results.Take(6))
                {
                    await resultRepo.AppendNoteAsync(r.TestHandle, $"writer-{n}");
                }
            }
            catch (SqliteException ex)
            {
                lock (exceptions) { exceptions.Add(ex); }
            }
        });

        await Task.WhenAll(tasks);

        Assert.True(exceptions.Count == 0,
            $"Expected zero SQLITE_BUSY/locked failures, got {exceptions.Count}: {string.Join("; ", exceptions.Select(e => e.Message))}");
    }
}
