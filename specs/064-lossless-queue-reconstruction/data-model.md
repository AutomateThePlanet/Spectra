# Phase 1 Data Model: Lossless Execution-Queue Reconstruction

## New entity: `QueueSnapshotEntry`

Location: `src/Spectra.Core/Models/Execution/QueueSnapshotEntry.cs` (immutable record, JSON-attributed
to match sibling models).

| Field | Type | Notes |
|---|---|---|
| `RunId` | `string` (required) | Owning run. |
| `TestId` | `string` (required) | Test case ID (e.g. `TC-101`). |
| `Title` | `string` (required) | Display title captured at run-build time. |
| `Priority` | `string` (required) | Canonical priority string (`high`/`medium`/`low`) — stored as-is from `TestIndexEntry.Priority`; parsed to the `Priority` enum on reconstruction via the existing `ParsePriority`. |
| `DependsOn` | `string?` | The single dependency test ID, or null. Mirrors `QueuedTest.DependsOn`. |
| `OrderIndex` | `int` (required) | 0-based position in the built queue's ordered `Tests` list — preserves the priority-then-topological order from `OrderByDependencies`. |

**Why string priority, not the enum**: the snapshot is a faithful capture of the index value; storing
the canonical string avoids an enum-mapping round-trip and matches how `test_results.status` already
stores enum names as text. Reconstruction reuses `TestQueue.ParsePriority` to produce the
`Priority` enum, identical to `Build`.

## New table: `queue_snapshot`

Added to `ExecutionDb.InitializeSchemaAsync` via `CREATE TABLE IF NOT EXISTS` (additive; safe on
existing DBs — same migration style as `runs`/`test_results`).

```sql
CREATE TABLE IF NOT EXISTS queue_snapshot (
    run_id      TEXT NOT NULL,
    test_id     TEXT NOT NULL,
    title       TEXT NOT NULL,
    priority    TEXT NOT NULL,
    depends_on  TEXT,
    order_index INTEGER NOT NULL,
    PRIMARY KEY (run_id, test_id),
    FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_queue_snapshot_run ON queue_snapshot(run_id);
```

- **PK `(run_id, test_id)`**: one orchestration row per test per run; invariant across retest attempts
  (unlike `test_results`, keyed by attempt).
- **`ON DELETE CASCADE`**: snapshot lifecycle follows the run, identical to `test_results`.
- **`idx_queue_snapshot_run`**: supports the single read shape (`WHERE run_id = ? ORDER BY order_index`).

## Relationships

```
runs (1) ──< (N) test_results        -- per attempt (existing)
runs (1) ──< (N) queue_snapshot      -- per test, write-once at run-build (NEW)
queue_snapshot.test_id  ── (logical) ──  test_results.test_id   -- joined on reconstruction
queue_snapshot.depends_on ── (logical, intra-run) ── queue_snapshot.test_id
```

The reconstruction join is in code, not SQL: load all snapshot rows for the run (ordered by
`order_index`) and the latest-attempt result per `test_id`, then zip them into `QueuedTest`s.

## Validation rules (enforced during reconstruction — FR-002/FR-003)

Let `S` = snapshot test_ids for the run, `R` = test_ids present in `test_results` for the run.

1. **Benign absence (not an error)**: `R == ∅` **and** `S == ∅` → run not found → return `null`.
2. **Snapshot missing**: `R ≠ ∅` **and** `S == ∅` → **throw** `QueueReconstructionException`
   ("results exist but no orchestration snapshot").
3. **Snapshot incomplete (result without snapshot)**: some `t ∈ R` with `t ∉ S` → **throw**
   (a recorded test has no orchestration row).
4. **Snapshot incomplete (snapshot without result)**: some `t ∈ S` with `t ∉ R` → **throw**
   (StartRun always creates a result per queued test, so this means corruption).
5. **Dependency inconsistency**: some snapshot row with non-null `depends_on = d` where `d ∉ S` →
   **throw** (dangling dependency edge cannot be faithfully rebuilt).
6. No field is ever defaulted or inferred to avoid a throw (FR-002).

## Reconstruction output (the rebuilt `QueuedTest` list — FR-001/FR-006)

For each snapshot row ordered by `order_index`:

| `QueuedTest` field | Source |
|---|---|
| `TestId` | snapshot `test_id` |
| `Title` | snapshot `title` (no longer `= TestId`) |
| `Priority` | `ParsePriority(snapshot.priority)` (no longer hard-coded `Medium`) |
| `DependsOn` | snapshot `depends_on` (no longer `null`) |
| `TestHandle` | **latest-attempt** result's `test_handle` (handles retest: newest handle wins) |
| `Status` | **latest-attempt** result's `status` |

`_currentIndex` is set to the position of the single `InProgress` test if one exists (preserving the
existing `AddFromResult` in-progress-restore behaviour, FR-006). Latest attempt per `test_id` is
`GroupBy(test_id).Select(max attempt)` — same dedup the current `ReconstructQueue` already performs.

## Touched existing entities (no schema/shape change)

- `QueuedTest` (`Spectra.Core/Models/Execution/QueuedTest.cs`): unchanged — it is the reconstruction
  **target**; all six fields are now populated faithfully.
- `TestResult`: unchanged — still authoritative for per-attempt status/handle.
- `TestIndexEntry`: unchanged — still the build-time source feeding the snapshot at `StartRunAsync`.
