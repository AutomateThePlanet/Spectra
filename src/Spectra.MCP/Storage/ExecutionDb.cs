using Microsoft.Data.Sqlite;

namespace Spectra.MCP.Storage;

/// <summary>
/// Manages SQLite database connection and schema for execution state.
/// Database location: .execution/spectra.db
/// </summary>
public sealed class ExecutionDb : IAsyncDisposable
{
    private const string DbDirectory = ".execution";
    private const string DbFileName = "spectra.db";

    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _initialized;

    public ExecutionDb(string? basePath = null)
    {
        var dbPath = Path.Combine(basePath ?? Environment.CurrentDirectory, DbDirectory, DbFileName);
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Gets an open database connection, initializing schema if needed.
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync()
    {
        if (_connection is not null && _connection.State == System.Data.ConnectionState.Open)
        {
            return _connection;
        }

        // Ensure directory exists
        var dbPath = _connectionString.Replace("Data Source=", "");
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync();

        if (!_initialized)
        {
            await InitializeSchemaAsync(_connection);
            _initialized = true;
        }

        return _connection;
    }

    /// <summary>
    /// Initializes database schema if not exists.
    /// </summary>
    private static async Task InitializeSchemaAsync(SqliteConnection connection)
    {
        const string schema = """
            -- Runs table
            CREATE TABLE IF NOT EXISTS runs (
                run_id TEXT PRIMARY KEY,
                suite TEXT NOT NULL,
                status TEXT NOT NULL CHECK (status IN ('Created','Running','Paused','Completed','Cancelled','Abandoned')),
                started_at TEXT NOT NULL,
                started_by TEXT NOT NULL,
                environment TEXT,
                filters TEXT,
                updated_at TEXT NOT NULL,
                completed_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_runs_suite_user ON runs(suite, started_by);
            CREATE INDEX IF NOT EXISTS idx_runs_status ON runs(status);

            -- Test results table
            CREATE TABLE IF NOT EXISTS test_results (
                run_id TEXT NOT NULL,
                test_id TEXT NOT NULL,
                test_handle TEXT NOT NULL UNIQUE,
                status TEXT NOT NULL CHECK (status IN ('Pending','InProgress','Passed','Failed','Skipped','Blocked')),
                notes TEXT,
                started_at TEXT,
                completed_at TEXT,
                attempt INTEGER NOT NULL DEFAULT 1,
                blocked_by TEXT,
                PRIMARY KEY (run_id, test_id, attempt),
                FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_results_run ON test_results(run_id);
            CREATE INDEX IF NOT EXISTS idx_results_handle ON test_results(test_handle);
            CREATE INDEX IF NOT EXISTS idx_results_status ON test_results(run_id, status);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync();
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
