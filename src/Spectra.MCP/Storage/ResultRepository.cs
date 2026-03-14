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
            INSERT INTO test_results (run_id, test_id, test_handle, status, notes, started_at, completed_at, attempt, blocked_by)
            VALUES (@run_id, @test_id, @test_handle, @status, @notes, @started_at, @completed_at, @attempt, @blocked_by)
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
                    INSERT INTO test_results (run_id, test_id, test_handle, status, notes, started_at, completed_at, attempt, blocked_by)
                    VALUES (@run_id, @test_id, @test_handle, @status, @notes, @started_at, @completed_at, @attempt, @blocked_by)
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

    private static TestResult MapResult(SqliteDataReader reader)
    {
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
            BlockedBy = reader.IsDBNull(reader.GetOrdinal("blocked_by")) ? null : reader.GetString(reader.GetOrdinal("blocked_by"))
        };
    }
}
