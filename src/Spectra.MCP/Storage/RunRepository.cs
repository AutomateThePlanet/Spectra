using System.Text.Json;
using Microsoft.Data.Sqlite;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Storage;

/// <summary>
/// Repository for Run entities with CRUD operations.
/// </summary>
public sealed class RunRepository
{
    private readonly ExecutionDb _db;

    public RunRepository(ExecutionDb db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a new run record.
    /// </summary>
    public async Task CreateAsync(Run run)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO runs (run_id, suite, status, started_at, started_by, environment, filters, updated_at, completed_at)
            VALUES (@run_id, @suite, @status, @started_at, @started_by, @environment, @filters, @updated_at, @completed_at)
            """;

        command.Parameters.AddWithValue("@run_id", run.RunId);
        command.Parameters.AddWithValue("@suite", run.Suite);
        command.Parameters.AddWithValue("@status", run.Status.ToString());
        command.Parameters.AddWithValue("@started_at", run.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("@started_by", run.StartedBy);
        command.Parameters.AddWithValue("@environment", (object?)run.Environment ?? DBNull.Value);
        command.Parameters.AddWithValue("@filters", run.Filters is not null ? JsonSerializer.Serialize(run.Filters) : DBNull.Value);
        command.Parameters.AddWithValue("@updated_at", run.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("@completed_at", run.CompletedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets a run by ID.
    /// </summary>
    public async Task<Run?> GetByIdAsync(string runId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT * FROM runs WHERE run_id = @run_id";
        command.Parameters.AddWithValue("@run_id", runId);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapRun(reader);
        }

        return null;
    }

    /// <summary>
    /// Gets an active run for a suite by user.
    /// </summary>
    public async Task<Run?> GetActiveRunAsync(string suite, string user)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT * FROM runs
            WHERE suite = @suite
            AND started_by = @user
            AND status IN ('Created', 'Running', 'Paused')
            ORDER BY started_at DESC
            LIMIT 1
            """;

        command.Parameters.AddWithValue("@suite", suite);
        command.Parameters.AddWithValue("@user", user);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return MapRun(reader);
        }

        return null;
    }

    /// <summary>
    /// Updates run status.
    /// </summary>
    public async Task UpdateStatusAsync(string runId, RunStatus status, DateTime? completedAt = null)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            UPDATE runs
            SET status = @status, updated_at = @updated_at, completed_at = @completed_at
            WHERE run_id = @run_id
            """;

        command.Parameters.AddWithValue("@run_id", runId);
        command.Parameters.AddWithValue("@status", status.ToString());
        command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("@completed_at", completedAt?.ToString("O") ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets run history for a suite with optional user filter.
    /// </summary>
    public async Task<IReadOnlyList<Run>> GetHistoryAsync(string suite, string? user = null, int limit = 10)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        var sql = "SELECT * FROM runs WHERE suite = @suite";
        if (!string.IsNullOrEmpty(user))
        {
            sql += " AND started_by = @user";
        }
        sql += " ORDER BY started_at DESC LIMIT @limit";

        command.CommandText = sql;
        command.Parameters.AddWithValue("@suite", suite);
        command.Parameters.AddWithValue("@limit", limit);
        if (!string.IsNullOrEmpty(user))
        {
            command.Parameters.AddWithValue("@user", user);
        }

        var runs = new List<Run>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            runs.Add(MapRun(reader));
        }

        return runs;
    }

    /// <summary>
    /// Gets all runs with optional filters.
    /// </summary>
    public async Task<IReadOnlyList<Run>> GetAllAsync(string? suite = null, string? user = null, int limit = 50)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(suite))
        {
            conditions.Add("suite = @suite");
        }
        if (!string.IsNullOrEmpty(user))
        {
            conditions.Add("started_by = @user");
        }

        var whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : "";
        command.CommandText = $"SELECT * FROM runs {whereClause} ORDER BY started_at DESC LIMIT @limit";

        command.Parameters.AddWithValue("@limit", limit);
        if (!string.IsNullOrEmpty(suite))
        {
            command.Parameters.AddWithValue("@suite", suite);
        }
        if (!string.IsNullOrEmpty(user))
        {
            command.Parameters.AddWithValue("@user", user);
        }

        var runs = new List<Run>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            runs.Add(MapRun(reader));
        }

        return runs;
    }

    /// <summary>
    /// Gets runs that have been paused longer than the timeout.
    /// </summary>
    public async Task<IReadOnlyList<Run>> GetAbandonedRunsAsync(TimeSpan timeout)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        var cutoff = DateTime.UtcNow.Subtract(timeout);
        command.CommandText = """
            SELECT * FROM runs
            WHERE status = 'Paused'
            AND updated_at < @cutoff
            """;

        command.Parameters.AddWithValue("@cutoff", cutoff.ToString("O"));

        var runs = new List<Run>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            runs.Add(MapRun(reader));
        }

        return runs;
    }

    private static Run MapRun(SqliteDataReader reader)
    {
        var filtersJson = reader.IsDBNull(reader.GetOrdinal("filters")) ? null : reader.GetString(reader.GetOrdinal("filters"));

        return new Run
        {
            RunId = reader.GetString(reader.GetOrdinal("run_id")),
            Suite = reader.GetString(reader.GetOrdinal("suite")),
            Status = Enum.Parse<RunStatus>(reader.GetString(reader.GetOrdinal("status"))),
            StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("started_at"))),
            StartedBy = reader.GetString(reader.GetOrdinal("started_by")),
            Environment = reader.IsDBNull(reader.GetOrdinal("environment")) ? null : reader.GetString(reader.GetOrdinal("environment")),
            Filters = filtersJson is not null ? JsonSerializer.Deserialize<RunFilters>(filtersJson) : null,
            UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("updated_at"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("completed_at")))
        };
    }
}
