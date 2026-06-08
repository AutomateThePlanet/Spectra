# Contract: Queue Snapshot & Reconstruction

This feature is internal to the execution engine — it adds **no** new CLI command and **no** new MCP
tool (both out of scope). The contracts that matter are: the storage schema, the repository surface,
the engine reconstruction behaviour, and the one new error code that tool handlers surface.

## Storage contract — `queue_snapshot` table

Defined in `data-model.md`. Created idempotently in `ExecutionDb.InitializeSchemaAsync`. Write-once at
run-build; read-only thereafter.

## Repository contract — `QueueSnapshotRepository`

`src/Spectra.MCP/Storage/QueueSnapshotRepository.cs` (mirrors `ResultRepository` style: takes
`ExecutionDb`, uses `GetConnectionAsync`).

```csharp
public sealed class QueueSnapshotRepository
{
    public QueueSnapshotRepository(ExecutionDb db);

    /// Persists all snapshot rows for a run in a single transaction.
    /// Throws on failure (FR-007 — run creation fails if the snapshot cannot be persisted).
    Task CreateManyAsync(IEnumerable<QueueSnapshotEntry> entries);

    /// Returns the run's snapshot rows ordered by order_index (empty list if none).
    Task<IReadOnlyList<QueueSnapshotEntry>> GetByRunIdAsync(string runId);
}
```

## Engine contract — reconstruction

`ExecutionEngine.GetQueueAsync(runId)`:

| Condition | Result |
|---|---|
| Queue already in `_queues` (warm / long-lived) | Return it unchanged (FR-005). |
| Cold, results `== ∅` and snapshot `== ∅` | Return `null` (benign "run not found"). |
| Cold, snapshot missing/incomplete/inconsistent while results exist | **Throw `QueueReconstructionException`** (FR-003). |
| Cold, snapshot + results consistent | Reconstruct a faithful `TestQueue` (FR-001/FR-006) and cache it. |

`ExecutionEngine.StartRunAsync(...)`: after `TestQueue.Build`, persist the snapshot (derived from the
ordered `queue.Tests`) via `QueueSnapshotRepository.CreateManyAsync` **before** returning; a failure
propagates and run creation fails (FR-007).

**Unified callers** (now route through `GetQueueAsync` instead of reading `_queues` directly):
`GetStatusAsync`, `AdvanceTestAsync`, `BulkRecordResultsAsync`, `RetestAsync`, and
`FinalizeRunAsync`'s pending-guard. Behaviour for a warm queue is identical to today; behaviour for a
cold queue becomes faithful instead of degraded/null (FR-004).

## New exception

```csharp
namespace Spectra.MCP.Execution;

/// Thrown when a run's persisted orchestration snapshot cannot faithfully rebuild the queue.
/// Distinct from the benign "run not found" (null) signal — never surfaced as RUN_NOT_FOUND.
public sealed class QueueReconstructionException : Exception
{
    public string RunId { get; }
    public QueueReconstructionException(string runId, string message) : base(message);
}
```

## Tool error-surface contract (additive)

Tool handlers that call `GetQueueAsync`/`GetStatusAsync` on the execution path (`advance_test_case`,
`skip_test_case`, `bulk_record_results`, `retest_test_case`, `get_execution_status`,
`finalize_execution_run`, `get_test_case_details`) catch `QueueReconstructionException` and return a
**distinct** structured error:

- `error_code`: `RECONSTRUCTION_FAILED` (new; MUST NOT be `RUN_NOT_FOUND`)
- `message`: the exception message (names the run and what was missing/inconsistent)

This is the only change to the MCP response surface. All other response shapes are unchanged
(Constitution III).

## Behavioural parity guarantee (the deliverable — FR-004)

For any run, the observable orchestration outcomes — next-test selection, full ordering,
dependency-block propagation (incl. transitive via `DependencyResolver.PropagateBlocks`), priority,
and `pending`/`completed` counts — MUST be identical whether obtained from:
- (a) the original in-memory queue held by the process that called `StartRunAsync`, or
- (b) a queue reconstructed from `queue_snapshot` + `test_results` in a fresh engine instance.

Verified by `QueueReconstructionParityTests` and `ReconstructedExecutionFlowTests`.
