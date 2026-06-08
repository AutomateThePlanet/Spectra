# Phase 1 Data Model: Execution Console Infrastructure

The console introduces **no new persisted run data** — it reads and mutates the existing execution model
through `ExecutionEngine`. The only net-new persisted artifact is an ephemeral, local-only **console
marker** file. This document records the reused entities (for reference) and the one new entity.

---

## Reused entities (owned by `Spectra.Execution` / `Spectra.Core` — NOT modified)

### ExecutionRun
The run being driven. Read via `GetRunAsync` / `GetActiveRunAsync` / `GetStatusAsync`.
- `RunId`, `Suite`, `Status` (active/paused/completed/cancelled), progress counts.
- Persisted in `runs` + reconstructed queue (`queue_snapshot`). **Source of truth: SQLite.**

### TestResult
One test's recorded outcome — the target of every console write-back.
- `TestHandle` (write target), `TestId`, `Status` (`Pending`/`InProgress`/`Passed`/`Failed`/`Blocked`/`Skipped`), `Notes`, `Attempt`, `ScreenshotPaths` (JSON array), `BlockedBy`.
- Mutated only via engine: `StartTestAsync`, `AdvanceTestAsync`, `SkipTestAsync`, `AddNoteAsync` →
  `ResultRepository.UpdateStatusAsync` / `AppendNoteAsync` / `AppendScreenshotPathAsync`.
- **Validation/state rules** are the engine's `StateMachine` (unchanged); the console never bypasses them.

### QueuedTest / TestQueue
Ordering + dependency view; reconstructed DB-complete (spec 064). Read via `GetQueueAsync`. Drives
"current test" and "next test" on the page.

### SavedScreenshot
Returned by `ScreenshotService.EncodeAndSaveAsync` — `RelativePath` (`{runId}/attachments/{file}.webp`)
is appended to `TestResult.ScreenshotPaths`. No console-specific shape.

---

## New entity

### ConsoleMarker  *(ephemeral, local-only, gitignored)*

A small JSON file written at console launch so the running console can be **discovered** and **stopped**
(FR-010), and so stale launches are detectable (FR-012).

| Field | Type | Notes |
|---|---|---|
| `pid` | int | Worker process id (the detached `HttpListener` host). |
| `port` | int | Bound localhost port. |
| `url` | string | `http://127.0.0.1:{port}/` — printed at launch and echoed by `--stop`. |
| `startedUtc` | string (ISO-8601) | Launch timestamp; combined with `pid` to detect a **stale** marker (pid reused by an unrelated process). |
| `runId` | string? | Optional: the run the console was launched against (for display/diagnostics). |

**Location**: `.execution/console.json` (the `.execution/` dir is already gitignored — FR-011 satisfied
with no `.gitignore` change required, though the plan may add an explicit `console.json` comment).

**Lifecycle / state**:
- **Write** on successful launch (worker bound a port).
- **Read** by `--stop` (and by a fresh launch, to refuse/replace a live console — "already running" per FR-012).
- **Stale detection**: if `pid` is not a live process, or the live process's start time is inconsistent
  with `startedUtc`, the marker is treated as absent (launch overwrites; `--stop` reports "no running console").
- **Delete** on clean `--stop`.

**Not persisted to SQLite.** It is process-coordination metadata, not run state — keeping it out of the
DB preserves the "SQLite = run source of truth, nothing else" invariant.

---

## Entity relationships

```
ConsoleMarker (file) ──identifies──> Worker process ──hosts──> HttpListener
                                                                    │
                                          dispatches each endpoint  ▼
                                                            ExecutionEngine
                                                                    │
                                  StartRun/Status/Advance/Skip/Note/Screenshot/Finalize
                                                                    ▼
                                                       SQLite (.execution/spectra.db)
                                                  runs · test_results · queue_snapshot
```

The browser holds **none** of these — it renders a projection fetched per poll and posts write-backs.
