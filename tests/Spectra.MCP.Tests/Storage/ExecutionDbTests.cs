using Spectra.MCP.Storage;

namespace Spectra.MCP.Tests.Storage;

public class ExecutionDbTests : IAsyncDisposable
{
    private readonly string _testDir;
    private ExecutionDb? _db;

    public ExecutionDbTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"spectra-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }

    [Fact]
    public async Task GetConnectionAsync_CreatesDirectoryAndDatabase()
    {
        _db = new ExecutionDb(_testDir);

        var connection = await _db.GetConnectionAsync();

        Assert.NotNull(connection);
        Assert.True(Directory.Exists(Path.Combine(_testDir, ".execution")));
        Assert.True(File.Exists(Path.Combine(_testDir, ".execution", "spectra.db")));
    }

    [Fact]
    public async Task GetConnectionAsync_MultipleCalls_ReturnsSameConnection()
    {
        _db = new ExecutionDb(_testDir);

        var connection1 = await _db.GetConnectionAsync();
        var connection2 = await _db.GetConnectionAsync();

        Assert.Same(connection1, connection2);
    }

    [Fact]
    public async Task Schema_RunsTableExists()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='runs'";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal("runs", result);
    }

    [Fact]
    public async Task Schema_TestResultsTableExists()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='test_results'";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal("test_results", result);
    }

    [Fact]
    public async Task Schema_RunsIndexExists()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='idx_runs_suite_user'";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal("idx_runs_suite_user", result);
    }

    [Fact]
    public async Task Schema_TestResultsHandleIndexExists()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='idx_results_handle'";
        var result = await command.ExecuteScalarAsync();

        Assert.Equal("idx_results_handle", result);
    }

    [Fact]
    public async Task Schema_RunsStatusConstraint_EnforcesValidValues()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (run_id, suite, status, started_at, started_by, updated_at)
            VALUES ('test', 'suite', 'InvalidStatus', '2026-01-01', 'user', '2026-01-01')
            """;

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Schema_TestResultsStatusConstraint_EnforcesValidValues()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        // First insert a valid run
        await using var runCommand = connection.CreateCommand();
        runCommand.CommandText = """
            INSERT INTO runs (run_id, suite, status, started_at, started_by, updated_at)
            VALUES ('test', 'suite', 'Running', '2026-01-01', 'user', '2026-01-01')
            """;
        await runCommand.ExecuteNonQueryAsync();

        // Try to insert a test result with invalid status
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO test_results (run_id, test_id, test_handle, status, attempt)
            VALUES ('test', 'TC-001', 'handle-1', 'InvalidStatus', 1)
            """;

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => command.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Schema_TestResultsHandleUnique_EnforcesUniqueness()
    {
        _db = new ExecutionDb(_testDir);
        var connection = await _db.GetConnectionAsync();

        // First insert a valid run
        await using var runCommand = connection.CreateCommand();
        runCommand.CommandText = """
            INSERT INTO runs (run_id, suite, status, started_at, started_by, updated_at)
            VALUES ('test', 'suite', 'Running', '2026-01-01', 'user', '2026-01-01')
            """;
        await runCommand.ExecuteNonQueryAsync();

        // Insert first result
        await using var command1 = connection.CreateCommand();
        command1.CommandText = """
            INSERT INTO test_results (run_id, test_id, test_handle, status, attempt)
            VALUES ('test', 'TC-001', 'same-handle', 'Pending', 1)
            """;
        await command1.ExecuteNonQueryAsync();

        // Try to insert duplicate handle
        await using var command2 = connection.CreateCommand();
        command2.CommandText = """
            INSERT INTO test_results (run_id, test_id, test_handle, status, attempt)
            VALUES ('test', 'TC-002', 'same-handle', 'Pending', 1)
            """;

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => command2.ExecuteNonQueryAsync());
    }
}
