using Microsoft.Data.Sqlite;
using Spectra.Core.Storage;

namespace Spectra.Core.Tests.Storage;

/// <summary>
/// Unit tests for ExecutionDbReader.
/// </summary>
public class ExecutionDbReaderTests : IAsyncLifetime
{
    private readonly string _testDir;
    private readonly string _executionDir;
    private readonly string _dbPath;

    public ExecutionDbReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-dbreader-test-{Guid.NewGuid():N}");
        _executionDir = Path.Combine(_testDir, ".execution");
        _dbPath = Path.Combine(_executionDir, "spectra.db");
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_executionDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Give time for connections to close
        GC.Collect();
        GC.WaitForPendingFinalizers();

        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Ignore cleanup failures
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void DatabaseExists_NoDatabase_ReturnsFalse()
    {
        // Delete the db if it exists from setup
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        var reader = new ExecutionDbReader(_testDir);

        Assert.False(reader.DatabaseExists);
    }

    [Fact]
    public async Task DatabaseExists_WithDatabase_ReturnsTrue()
    {
        await CreateEmptyDatabaseAsync();

        await using var reader = new ExecutionDbReader(_testDir);

        Assert.True(reader.DatabaseExists);
    }

    [Fact]
    public async Task GetRunSummariesAsync_NoDatabase_ReturnsEmptyList()
    {
        // Delete the db if it exists from setup
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Empty(runs);
    }

    [Fact]
    public async Task GetRunSummariesAsync_EmptyDatabase_ReturnsEmptyList()
    {
        await CreateEmptyDatabaseAsync();

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Empty(runs);
    }

    [Fact]
    public async Task GetRunSummariesAsync_WithRuns_ReturnsRunSummaries()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Single(runs);
        Assert.Equal("run-001", runs[0].RunId);
        Assert.Equal("checkout", runs[0].Suite);
        Assert.Equal("completed", runs[0].Status);
        Assert.Equal("user1", runs[0].StartedBy);
    }

    [Fact]
    public async Task GetRunSummariesAsync_WithMultipleRuns_ReturnsSortedByDate()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00"),
            ("run-002", "payments", "Completed", "2024-01-16T10:00:00", "user2", "2024-01-16T10:30:00"),
            ("run-003", "checkout", "Completed", "2024-01-14T10:00:00", "user1", "2024-01-14T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Equal(3, runs.Count);
        Assert.Equal("run-002", runs[0].RunId); // Most recent first
        Assert.Equal("run-001", runs[1].RunId);
        Assert.Equal("run-003", runs[2].RunId);
    }

    [Fact]
    public async Task GetRunSummariesAsync_WithTestResults_CalculatesCounts()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00")
        ]);
        await AddTestResultsAsync("run-001", [
            ("TC-001", "Passed"),
            ("TC-002", "Passed"),
            ("TC-003", "Failed"),
            ("TC-004", "Skipped"),
            ("TC-005", "Blocked")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Single(runs);
        Assert.Equal(5, runs[0].Total);
        Assert.Equal(2, runs[0].Passed);
        Assert.Equal(1, runs[0].Failed);
        Assert.Equal(1, runs[0].Skipped);
        Assert.Equal(1, runs[0].Blocked);
    }

    [Fact]
    public async Task GetRunSummariesAsync_CalculatesDuration()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Single(runs);
        Assert.Equal(1800, runs[0].DurationSeconds); // 30 minutes = 1800 seconds
    }

    [Fact]
    public async Task GetRunSummariesAsync_IncompleteRun_NoDuration()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "InProgress", "2024-01-15T10:00:00", "user1", null)
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesAsync();

        Assert.Single(runs);
        Assert.Null(runs[0].DurationSeconds);
        Assert.Null(runs[0].CompletedAt);
    }

    [Fact]
    public async Task GetRunSummariesForSuiteAsync_FiltersBySuite()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00"),
            ("run-002", "payments", "Completed", "2024-01-16T10:00:00", "user2", "2024-01-16T10:30:00"),
            ("run-003", "checkout", "Completed", "2024-01-17T10:00:00", "user1", "2024-01-17T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesForSuiteAsync("checkout");

        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal("checkout", r.Suite));
    }

    [Fact]
    public async Task GetRunSummariesForSuiteAsync_CaseInsensitive()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "Checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var runs = await reader.GetRunSummariesForSuiteAsync("CHECKOUT");

        Assert.Single(runs);
    }

    [Fact]
    public async Task GetLastRunForSuiteAsync_ReturnsLatestRun()
    {
        await CreateDatabaseWithRunsAsync([
            ("run-001", "checkout", "Completed", "2024-01-15T10:00:00", "user1", "2024-01-15T10:30:00"),
            ("run-002", "checkout", "Completed", "2024-01-17T10:00:00", "user1", "2024-01-17T10:30:00"),
            ("run-003", "checkout", "Completed", "2024-01-16T10:00:00", "user1", "2024-01-16T10:30:00")
        ]);

        await using var reader = new ExecutionDbReader(_testDir);

        var run = await reader.GetLastRunForSuiteAsync("checkout");

        Assert.NotNull(run);
        Assert.Equal("run-002", run.RunId); // Most recent
    }

    [Fact]
    public async Task GetLastRunForSuiteAsync_NoRuns_ReturnsNull()
    {
        await CreateEmptyDatabaseAsync();

        await using var reader = new ExecutionDbReader(_testDir);

        var run = await reader.GetLastRunForSuiteAsync("checkout");

        Assert.Null(run);
    }

    private async Task CreateEmptyDatabaseAsync()
    {
        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS runs (
                run_id TEXT PRIMARY KEY,
                suite TEXT NOT NULL,
                status TEXT NOT NULL,
                started_at TEXT NOT NULL,
                started_by TEXT NOT NULL,
                completed_at TEXT
            );

            CREATE TABLE IF NOT EXISTS test_results (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                run_id TEXT NOT NULL,
                test_id TEXT NOT NULL,
                status TEXT NOT NULL,
                FOREIGN KEY (run_id) REFERENCES runs(run_id)
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateDatabaseWithRunsAsync(
        (string RunId, string Suite, string Status, string StartedAt, string StartedBy, string? CompletedAt)[] runs)
    {
        await CreateEmptyDatabaseAsync();

        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        foreach (var (runId, suite, status, startedAt, startedBy, completedAt) in runs)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO runs (run_id, suite, status, started_at, started_by, completed_at)
                VALUES ($run_id, $suite, $status, $started_at, $started_by, $completed_at)
                """;
            command.Parameters.AddWithValue("$run_id", runId);
            command.Parameters.AddWithValue("$suite", suite);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$started_at", startedAt);
            command.Parameters.AddWithValue("$started_by", startedBy);
            command.Parameters.AddWithValue("$completed_at", completedAt is not null ? (object)completedAt : DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task AddTestResultsAsync(string runId, (string TestId, string Status)[] results)
    {
        var connectionString = $"Data Source={_dbPath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        foreach (var (testId, status) in results)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO test_results (run_id, test_id, status)
                VALUES ($run_id, $test_id, $status)
                """;
            command.Parameters.AddWithValue("$run_id", runId);
            command.Parameters.AddWithValue("$test_id", testId);
            command.Parameters.AddWithValue("$status", status);
            await command.ExecuteNonQueryAsync();
        }
    }
}
