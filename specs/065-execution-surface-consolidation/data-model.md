# Phase 1 Data Model: Execution Surface Consolidation

This feature introduces **no new persisted data** and **no schema change to data semantics**. The data model below describes the structural (assembly/type-ownership) model and the one behavioral change to the storage layer (connection PRAGMA). Domain entities and the SQLite schema are inherited unchanged from Spec 064.

## Persisted entities (unchanged — inherited)

- **runs** (`run_id` PK, `suite`, `status`, `started_at`, `started_by`, `environment`, `filters`, `updated_at`, `completed_at`) — owned by `RunRepository`.
- **test_results** (`run_id`,`test_id`,`test_handle` UNIQUE,`status`,`notes`,`started_at`,`completed_at`,`attempt`,`blocked_by`,`screenshot_paths`; PK `(run_id,test_id,attempt)`) — owned by `ResultRepository`.
- **queue_snapshot** (`run_id`,`test_id`,`title`,`priority`,`depends_on`,`order_index`; PK `(run_id,test_id)`) — owned by `QueueSnapshotRepository` (Spec 064). This is the orchestration source of truth that makes reconstruction lossless and the CLI path safe.

No columns added, removed, or re-typed. No migration.

## Structural model (the actual change)

### New assembly: `Spectra.Execution` (class library)

Owns the relocated types (namespaces preserved per research R1). Depends on `Spectra.Core` + `Microsoft.Data.Sqlite` + `SixLabors.ImageSharp`.

| Type | Origin namespace (kept) | Role |
|---|---|---|
| `ExecutionEngine` | `Spectra.MCP.Execution` | Orchestration service; public surface unchanged |
| `TestQueue` | `Spectra.MCP.Execution` | Ordered in-memory queue; `AddReconstructed` primitive |
| `DependencyResolver` | `Spectra.MCP.Execution` | Transitive block propagation |
| `StateMachine` | `Spectra.MCP.Execution` | Pure static transition tables |
| `QueueReconstructionException` | `Spectra.MCP.Execution` | Fail-loud reconstruction signal |
| `ExecutionDb` | `Spectra.MCP.Storage` | SQLite connection + schema (**+ WAL/busy_timeout PRAGMA**) |
| `RunRepository` | `Spectra.MCP.Storage` | `runs` CRUD |
| `ResultRepository` | `Spectra.MCP.Storage` | `test_results` CRUD |
| `QueueSnapshotRepository` | `Spectra.MCP.Storage` | `queue_snapshot` CRUD |
| `IUserIdentityResolver`, `UserIdentityResolver` | `Spectra.MCP.Identity` | Current-user resolution (git/OS) |
| `McpConfig` (+ `LogLevel` enum) | `Spectra.MCP.Infrastructure` | Engine ctor dependency (carried; not read) |
| `ScreenshotService` *(new)* | `Spectra.Execution.Screenshots` | Shared encode + clipboard capture |

### Referencing graph (after)

```
Spectra.Core         (no change)
   ▲        ▲
   │        │
Spectra.Execution    (NEW; refs Core + Sqlite + ImageSharp)
   ▲        ▲
   │        │
Spectra.CLI      Spectra.MCP   (both add ProjectReference → Spectra.Execution)
```

`Spectra.MCP` no longer *defines* the engine/storage/identity/`McpConfig` types — it consumes them from `Spectra.Execution`. It retains `McpLogging`, `Server/`, `Tools/`, `Reports/`, `Program.cs`.

## Behavioral change: connection PRAGMA (FR-004)

`ExecutionDb.GetConnectionAsync`, after opening a connection and before schema init, issues:

```
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;
```

- **WAL**: persisted on the DB file; allows a writer and readers to coexist; set idempotently each open.
- **busy_timeout=5000**: per-connection; a contending writer waits up to 5 s and retries rather than throwing `SQLITE_BUSY` immediately.

State transitions, queue semantics, handle semantics, reconstruction, and fail-loud behavior are **unchanged** — they come from the relocated engine as-is.

## CLI surface model (no persistence)

The `spectra run` subcommands are stateless adapters: parse args → build `RunServices` (engine + loaders) → call one `ExecutionEngine` method → render via the JSON/human result writer → deterministic exit code. No CLI-side state; each invocation reconstructs the queue from the DB (Spec 064), which is what makes the short-lived process equivalent to the long-lived server.
