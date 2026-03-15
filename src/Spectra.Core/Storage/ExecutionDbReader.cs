using Microsoft.Data.Sqlite;
using Spectra.Core.Models.Dashboard;

namespace Spectra.Core.Storage;

/// <summary>
/// Read-only access to the execution database (.execution/spectra.db).
/// Used by dashboard generation to read run history.
/// </summary>
public sealed class ExecutionDbReader : IAsyncDisposable
{
    private const string DbDirectory = ".execution";
    private const string DbFileName = "spectra.db";

    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public ExecutionDbReader(string? basePath = null)
    {
        _dbPath = Path.Combine(basePath ?? Environment.CurrentDirectory, DbDirectory, DbFileName);
    }

    /// <summary>
    /// Checks if the execution database exists.
    /// </summary>
    public bool DatabaseExists => File.Exists(_dbPath);

    /// <summary>
    /// Gets an open read-only database connection.
    /// </summary>
    private async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is not null && _connection.State == System.Data.ConnectionState.Open)
        {
            return _connection;
        }

        if (!DatabaseExists)
        {
            throw new FileNotFoundException("Execution database not found", _dbPath);
        }

        var connectionString = $"Data Source={_dbPath};Mode=ReadOnly";
        _connection = new SqliteConnection(connectionString);
        await _connection.OpenAsync();

        return _connection;
    }

    /// <summary>
    /// Gets all run summaries from the database.
    /// </summary>
    public async Task<IReadOnlyList<RunSummary>> GetRunSummariesAsync()
    {
        if (!DatabaseExists)
        {
            return [];
        }

        var connection = await GetConnectionAsync();
        var runs = new List<RunSummary>();

        const string query = """
            SELECT
                r.run_id,
                r.suite,
                r.status,
                r.started_at,
                r.started_by,
                r.completed_at,
                COUNT(tr.test_id) as total,
                SUM(CASE WHEN tr.status = 'Passed' THEN 1 ELSE 0 END) as passed,
                SUM(CASE WHEN tr.status = 'Failed' THEN 1 ELSE 0 END) as failed,
                SUM(CASE WHEN tr.status = 'Skipped' THEN 1 ELSE 0 END) as skipped,
                SUM(CASE WHEN tr.status = 'Blocked' THEN 1 ELSE 0 END) as blocked
            FROM runs r
            LEFT JOIN test_results tr ON r.run_id = tr.run_id
            GROUP BY r.run_id
            ORDER BY r.started_at DESC
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var startedAt = DateTime.Parse(reader.GetString(3));
            var completedAtStr = reader.IsDBNull(5) ? null : reader.GetString(5);
            var completedAt = completedAtStr is not null ? DateTime.Parse(completedAtStr) : (DateTime?)null;

            runs.Add(new RunSummary
            {
                RunId = reader.GetString(0),
                Suite = reader.GetString(1),
                Status = reader.GetString(2).ToLowerInvariant(),
                StartedAt = startedAt,
                StartedBy = reader.GetString(4),
                CompletedAt = completedAt,
                DurationSeconds = completedAt.HasValue
                    ? (int)(completedAt.Value - startedAt).TotalSeconds
                    : null,
                Total = reader.GetInt32(6),
                Passed = reader.GetInt32(7),
                Failed = reader.GetInt32(8),
                Skipped = reader.GetInt32(9),
                Blocked = reader.GetInt32(10)
            });
        }

        return runs;
    }

    /// <summary>
    /// Gets run summaries for a specific suite.
    /// </summary>
    public async Task<IReadOnlyList<RunSummary>> GetRunSummariesForSuiteAsync(string suite)
    {
        var allRuns = await GetRunSummariesAsync();
        return allRuns.Where(r => r.Suite.Equals(suite, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Gets the most recent run for a suite.
    /// </summary>
    public async Task<RunSummary?> GetLastRunForSuiteAsync(string suite)
    {
        var runs = await GetRunSummariesForSuiteAsync(suite);
        return runs.FirstOrDefault();
    }

    /// <summary>
    /// Closes the database connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
