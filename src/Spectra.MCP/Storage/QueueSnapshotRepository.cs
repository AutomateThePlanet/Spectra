using Microsoft.Data.Sqlite;
using Spectra.Core.Models.Execution;

namespace Spectra.MCP.Storage;

/// <summary>
/// Repository for the queue_snapshot table (spec 064) — the durable, write-once-at-run-build
/// orchestration capture used to reconstruct the execution queue losslessly from the database.
/// </summary>
public sealed class QueueSnapshotRepository
{
    private readonly ExecutionDb _db;

    public QueueSnapshotRepository(ExecutionDb db)
    {
        _db = db;
    }

    /// <summary>
    /// Persists all snapshot rows for a run in a single transaction. Throws on failure so that
    /// run creation fails loud rather than leaving an unreconstructable run (FR-007).
    /// </summary>
    public async Task CreateManyAsync(IEnumerable<QueueSnapshotEntry> entries)
    {
        var connection = await _db.GetConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            foreach (var entry in entries)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;

                command.CommandText = """
                    INSERT INTO queue_snapshot (run_id, test_id, title, priority, depends_on, order_index)
                    VALUES (@run_id, @test_id, @title, @priority, @depends_on, @order_index)
                    """;

                command.Parameters.AddWithValue("@run_id", entry.RunId);
                command.Parameters.AddWithValue("@test_id", entry.TestId);
                command.Parameters.AddWithValue("@title", entry.Title);
                command.Parameters.AddWithValue("@priority", entry.Priority);
                command.Parameters.AddWithValue("@depends_on", (object?)entry.DependsOn ?? DBNull.Value);
                command.Parameters.AddWithValue("@order_index", entry.OrderIndex);

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
    /// Returns the run's snapshot rows ordered by order_index (empty list if none exist).
    /// </summary>
    public async Task<IReadOnlyList<QueueSnapshotEntry>> GetByRunIdAsync(string runId)
    {
        var connection = await _db.GetConnectionAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT run_id, test_id, title, priority, depends_on, order_index
            FROM queue_snapshot
            WHERE run_id = @run_id
            ORDER BY order_index
            """;
        command.Parameters.AddWithValue("@run_id", runId);

        var entries = new List<QueueSnapshotEntry>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new QueueSnapshotEntry
            {
                RunId = reader.GetString(0),
                TestId = reader.GetString(1),
                Title = reader.GetString(2),
                Priority = reader.GetString(3),
                DependsOn = reader.IsDBNull(4) ? null : reader.GetString(4),
                OrderIndex = reader.GetInt32(5)
            });
        }

        return entries;
    }
}
