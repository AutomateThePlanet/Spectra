using System.Text.Json;
using Microsoft.Data.Sqlite;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Storage;

/// <summary>
/// Repository for TestResult entities with CRUD operations.
/// </summary>
public sealed class ResultRepository
{
    private readonly ExecutionDb _db;

    public ResultRepository(ExecutionDb db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a new test result record.
    /// </summary>
    public async Task CreateAsync(TestResult result)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO test_results (run_id, test_id, test_handle, status, notes, started_at, completed_at, attempt, blocked_by, screenshot_paths)
            VALUES (@run_id, @test_id, @test_handle, @status, @notes, @started_at, @completed_at, @attempt, @blocked_by, @screenshot_paths)
            """;

        command.Parameters.AddWithValue("@run_id", result.RunId);
        command.Parameters.AddWithValue("@test_id", result.TestId);
        command.Parameters.AddWithValue("@test_handle", result.TestHandle);
        command.Parameters.AddWithValue("@status", result.Status.ToString());
        command.Parameters.AddWithValue("@notes", (object?)result.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@started_at", result.StartedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@completed_at", result.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@attempt", result.Attempt);
        command.Parameters.AddWithValue("@blocked_by", (object?)result.BlockedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("@screenshot_paths", result.ScreenshotPaths is { Count: > 0 }
            ? JsonSerializer.Serialize(result.ScreenshotPaths)
            : (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Creates multiple test result records in a transaction.
    /// </summary>
    public async Task CreateManyAsync(IEnumerable<TestResult> results)
    {
        var connection = await _db.GetConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var result in results)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;

                command.CommandText = """
                    INSERT INTO test_results (run_id, test_id, test_handle, status, notes, started_at, completed_at, attempt, blocked_by, screenshot_paths)
                    VALUES (@run_id, @test_id, @test_handle, @status, @notes, @started_at, @completed_at, @attempt, @blocked_by, @screenshot_paths)
                    """;

                command.Parameters.AddWithValue("@run_id", result.RunId);
                command.Parameters.AddWithValue("@test_id", result.TestId);
                command.Parameters.AddWithValue("@test_handle", result.TestHandle);
                command.Parameters.AddWithValue("@status", result.Status.ToString());
                command.Parameters.AddWithValue("@notes", (object?)result.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("@started_at", result.StartedAt?.ToString("O") ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@completed_at", result.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@attempt", result.Attempt);
                command.Parameters.AddWithValue("@blocked_by", (object?)result.BlockedBy ?? DBNull.Value);
                command.Parameters.AddWithValue("@screenshot_paths", result.ScreenshotPaths is { Count: > 0 }
                    ? JsonSerializer.Serialize(result.ScreenshotPaths)
                    : (object)DBNull.Value);

                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Gets a test result by handle.
    /// </summary>
    public async Task<TestResult?> GetByHandleAsync(string testHandle)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM test_results WHERE test_handle = @test_handle";
        command.Parameters.AddWithValue("@test_handle", testHandle);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapResult(reader);
        }

        return null;
    }

    /// <summary>
    /// Gets all results for a run.
    /// </summary>
    public async Task<IReadOnlyList<TestResult>> GetByRunIdAsync(string runId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM test_results WHERE run_id = @run_id ORDER BY test_id, attempt";
        command.Parameters.AddWithValue("@run_id", runId);

        var results = new List<TestResult>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapResult(reader));
        }

        return results;
    }

    /// <summary>
    /// Gets result counts by status for a run.
    /// </summary>
    public async Task<Dictionary<TestStatus, int>> GetStatusCountsAsync(string runId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT status, COUNT(*) as count
            FROM test_results
            WHERE run_id = @run_id
            GROUP BY status
            """;
        command.Parameters.AddWithValue("@run_id", runId);

        var counts = new Dictionary<TestStatus, int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var status = Enum.Parse<TestStatus>(reader.GetString(0));
            var count = reader.GetInt32(1);
            counts[status] = count;
        }

        return counts;
    }

    /// <summary>
    /// Updates test result status and timestamps.
    /// </summary>
    public async Task UpdateStatusAsync(string testHandle, TestStatus status, DateTime? startedAt = null, DateTime? completedAt = null, string? notes = null, string? blockedBy = null)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE test_results
            SET status = @status,
                started_at = COALESCE(@started_at, started_at),
                completed_at = COALESCE(@completed_at, completed_at),
                notes = COALESCE(@notes, notes),
                blocked_by = COALESCE(@blocked_by, blocked_by)
            WHERE test_handle = @test_handle
            """;

        command.Parameters.AddWithValue("@test_handle", testHandle);
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@started_at", startedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@completed_at", completedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@notes", (object?)notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@blocked_by", (object?)blockedBy ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Appends a note to an existing test result.
    /// </summary>
    public async Task AppendNoteAsync(string testHandle, string note)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE test_results
            SET notes = CASE
                WHEN notes IS NULL OR notes = '' THEN @note
                ELSE notes || char(10) || @note
            END
            WHERE test_handle = @test_handle
            """;

        command.Parameters.AddWithValue("@test_handle", testHandle);
        command.Parameters.AddWithValue("@note", note);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Blocks tests that depend on the specified test.
    /// </summary>
    public async Task BlockDependentsAsync(string runId, string blockerTestId)
    {
        var connection = await _db.GetConnectionAsync();

        // First, get all tests for this run that depend on the blocker
        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = """
            UPDATE test_results
            SET status = 'Blocked', blocked_by = @blocker_id
            WHERE run_id = @run_id
            AND status = 'Pending'
            AND test_id IN (
                SELECT test_id FROM test_results
                WHERE run_id = @run_id
            )
            """;
        // Note: Actual dependency lookup will be done by DependencyResolver using index data

        selectCommand.Parameters.AddWithValue("@run_id", runId);
        selectCommand.Parameters.AddWithValue("@blocker_id", blockerTestId);

        await selectCommand.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets the latest attempt for a test in a run.
    /// </summary>
    public async Task<int> GetLatestAttemptAsync(string runId, string testId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT MAX(attempt) FROM test_results
            WHERE run_id = @run_id AND test_id = @test_id
            """;

        command.Parameters.AddWithValue("@run_id", runId);
        command.Parameters.AddWithValue("@test_id", testId);

        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? 0 : Convert.ToInt32(result);
    }

    /// <summary>
    /// Appends a screenshot path to an existing test result.
    /// </summary>
    public async Task AppendScreenshotPathAsync(string testHandle, string path)
    {
        var connection = await _db.GetConnectionAsync();

        // Read current paths
        await using var readCmd = connection.CreateCommand();
        readCmd.CommandText = "SELECT screenshot_paths FROM test_results WHERE test_handle = @test_handle";
        readCmd.Parameters.AddWithValue("@test_handle", testHandle);

        var existing = await readCmd.ExecuteScalarAsync();
        var paths = new List<string>();
        if (existing is string json && !string.IsNullOrEmpty(json))
        {
            paths = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        paths.Add(path);

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = "UPDATE test_results SET screenshot_paths = @paths WHERE test_handle = @test_handle";
        updateCmd.Parameters.AddWithValue("@test_handle", testHandle);
        updateCmd.Parameters.AddWithValue("@paths", JsonSerializer.Serialize(paths));

        await updateCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets all in-progress test results for a run.
    /// </summary>
    public async Task<IReadOnlyList<TestResult>> GetInProgressTestsAsync(string runId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM test_results WHERE run_id = @run_id AND status = 'InProgress'";
        command.Parameters.AddWithValue("@run_id", runId);

        var results = new List<TestResult>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapResult(reader));
        }

        return results;
    }

    /// <summary>
    /// Gets execution history statistics per test.
    /// </summary>
    public async Task<Dictionary<string, TestExecutionHistoryEntry>> GetTestExecutionHistoryAsync(
        IReadOnlyList<string>? testIds = null,
        int limit = 10)
    {
        var connection = await _db.GetConnectionAsync();
        var results = new Dictionary<string, TestExecutionHistoryEntry>();

        // Build query - get all terminal results (not Pending/InProgress)
        var sql = """
            SELECT test_id, run_id, status, completed_at, attempt
            FROM test_results
            WHERE status IN ('Passed', 'Failed', 'Skipped', 'Blocked')
            """;

        if (testIds is { Count: > 0 })
        {
            var placeholders = string.Join(", ", testIds.Select((_, i) => $"@id{i}"));
            sql += $" AND test_id IN ({placeholders})";
        }

        sql += " ORDER BY test_id, completed_at DESC";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (testIds is { Count: > 0 })
        {
            for (var i = 0; i < testIds.Count; i++)
            {
                command.Parameters.AddWithValue($"@id{i}", testIds[i]);
            }
        }

        await using var reader = await command.ExecuteReaderAsync();

        // Group by test_id in memory, respecting limit per test
        var testGroups = new Dictionary<string, List<(string RunId, string Status, DateTime? CompletedAt)>>();

        while (await reader.ReadAsync())
        {
            var testId = reader.GetString(0);
            var runId = reader.GetString(1);
            var status = reader.GetString(2);
            var completedAt = reader.IsDBNull(3) ? (DateTime?)null : DateTime.Parse(reader.GetString(3));

            if (!testGroups.ContainsKey(testId))
                testGroups[testId] = [];

            if (testGroups[testId].Count < limit)
                testGroups[testId].Add((runId, status, completedAt));
        }

        // Compute stats per test
        foreach (var (testId, entries) in testGroups)
        {
            var totalRuns = entries.Count;
            var passCount = entries.Count(e => e.Status == "Passed");
            var latest = entries.FirstOrDefault();

            results[testId] = new TestExecutionHistoryEntry
            {
                LastExecuted = latest.CompletedAt,
                LastStatus = latest.Status,
                TotalRuns = totalRuns,
                PassRate = totalRuns > 0 ? Math.Round((decimal)passCount / totalRuns * 100, 1) : null,
                LastRunId = latest.RunId
            };
        }

        // Add null entries for requested test IDs with no history
        if (testIds is not null)
        {
            foreach (var id in testIds)
            {
                if (!results.ContainsKey(id))
                {
                    results[id] = new TestExecutionHistoryEntry();
                }
            }
        }

        return results;
    }

    private static TestResult MapResult(SqliteDataReader reader)
    {
        IReadOnlyList<string>? screenshotPaths = null;
        try
        {
            var ssOrdinal = reader.GetOrdinal("screenshot_paths");
            if (!reader.IsDBNull(ssOrdinal))
            {
                var json = reader.GetString(ssOrdinal);
                screenshotPaths = JsonSerializer.Deserialize<List<string>>(json);
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Column doesn't exist yet (pre-migration)
        }

        return new TestResult
        {
            RunId = reader.GetString(reader.GetOrdinal("run_id")),
            TestId = reader.GetString(reader.GetOrdinal("test_id")),
            TestHandle = reader.GetString(reader.GetOrdinal("test_handle")),
            Status = Enum.Parse<TestStatus>(reader.GetString(reader.GetOrdinal("status"))),
            Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
            StartedAt = reader.IsDBNull(reader.GetOrdinal("started_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("started_at"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("completed_at"))),
            Attempt = reader.GetInt32(reader.GetOrdinal("attempt")),
            BlockedBy = reader.IsDBNull(reader.GetOrdinal("blocked_by")) ? null : reader.GetString(reader.GetOrdinal("blocked_by")),
            ScreenshotPaths = screenshotPaths
        };
    }
}
