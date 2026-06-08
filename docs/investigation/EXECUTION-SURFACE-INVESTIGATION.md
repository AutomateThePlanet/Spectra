# Execution Surface Consolidation — Investigation (CLI ⟷ MCP)

> **Scope:** investigation only. No spec, no code changes. Every claim carries `path:line` evidence; absences are marked **NOT FOUND**. "Observed in code" is distinguished from "documented but unverified."
> **Method note:** the load-bearing findings (Q1–Q4) were verified by reading the engine, queue, resolver, tool handlers, `ExecutionDb`, and `TestHandle` directly — not from summaries alone.
>
> **⚠️ Status update (spec 064 — merged):** the lossy queue reconstruction documented below (Q1 "Real losses"; Work-to-extract item #2) is **fixed**. A durable orchestration snapshot is now persisted at run-build (`queue_snapshot` table) and reconstruction rebuilds `DependsOn`/`Priority`/`Title`/order from it — DB-complete and drift-immune — or fails loud (`QueueReconstructionException` → `RECONSTRUCTION_FAILED`) rather than degrading silently. `GetStatusAsync`/`AdvanceTestAsync`/`BulkRecordResultsAsync`/`RetestAsync`/`FinalizeRunAsync` now route through the reconstruct-aware `GetQueueAsync`, so the **B-column collapses to 0** and the `retest` cross-process bug (Open Unknown #3) is resolved. The Q1–Q4 analysis below reflects the pre-064 state and is retained as the historical grounding for the fix.

---

## One-line verdict on the core premise

**REFUTED-as-stated → Hybrid.** Execution *results* (run/test status, notes, timestamps, handles, screenshots, attempts) are fully durable in SQLite and session-free. But execution *orchestration* — dependency-block propagation and priority/topological ordering — depends on a transient in-memory `TestQueue` that is **lossily** reconstructed from the DB (`DependsOn`, `Priority`, and original order are never persisted). A per-command CLI is functionally equivalent for **recording** results, but would **silently lose dependency-blocking and ordering** between commands unless reconstruction is fixed. That loss has a **single shared root cause**, not one-per-tool.

---

## Q1 — Execution state ownership

**Verdict: Hybrid** — one runtime-authoritative in-memory structure that is *re-derivable from disk* but **not** from the DB alone.

### Authoritative durable state — all in SQLite
- Schema owned by `ExecutionDb` — `src/Spectra.MCP/Storage/ExecutionDb.cs:57-111`. Two tables: `runs` (`:61-71`) and `test_results` (`:77-89`; `test_handle TEXT NOT NULL UNIQUE`, `attempt`, `blocked_by`, plus a migration-added `screenshot_paths` column `:104`).
- `runs` is written/read via `RunRepository`; `test_results` via `ResultRepository` (both `src/Spectra.MCP/Storage/`). Read-only dashboard access via `src/Spectra.Core/Storage/ExecutionDbReader.cs` (`Mode=ReadOnly`, `:43`).
- Every status mutation writes the DB **first**, then updates the in-memory queue: `AdvanceTestAsync` calls `ResultRepository.UpdateStatusAsync` (`src/Spectra.MCP/Execution/ExecutionEngine.cs:220`) before `queue.MarkCompleted` (`:231`). The DB is never behind the queue → a crash cannot lose a recorded result.

### The only process-resident state
- `ExecutionEngine._queues` — `src/Spectra.MCP/Execution/ExecutionEngine.cs:22` (`Dictionary<string, TestQueue>`), one `TestQueue` per active run.
- **NOT FOUND:** any `static` run/test state, `IHostedService`/`BackgroundService`, `Channel<T>`, or cross-call locks (swept `Spectra.MCP/Execution/`, `Server/`, and DI in `Program.cs`).

### Is `_queues` authoritative or derivable? — **Partially derivable, lossily.**
`GetQueueAsync` reconstructs a missing queue from `test_results` (`ExecutionEngine.cs:125-143`) via `ReconstructQueue` (`:148-165`). **Reconstruction drops orchestration data** — `TestQueue.AddFromResult` hard-codes:
- `Title = result.TestId` — real title lost (`src/Spectra.MCP/Execution/TestQueue.cs:97`)
- `Priority = Priority.Medium` — priority lost (`TestQueue.cs:98`)
- `DependsOn = null` — **dependency edge lost** (`TestQueue.cs:99`)

…and `ReconstructQueue` re-orders **alphabetically by `TestId`** (`ExecutionEngine.cs:156`, `.OrderBy(r => r.TestId)`), discarding the original priority-then-topological order from `TestQueue.Build` (`TestQueue.cs:29-50`; `OrderByDependencies` `:288-320`).

**Consequence:** the rich queue (real `DependsOn`/`Priority`/order) exists **only** in the process that called `StartRunAsync` (`ExecutionEngine.cs:59,93`). After a restart or in a fresh CLI process, a reconstructed queue has `DependsOn = null`, so `TestQueue.BlockDependents` (`TestQueue.cs:171-186`; predicate `test.DependsOn == testId` at `:178`) can **never** match → **dependency block-propagation silently no-ops**, and "next test" reverts to alphabetical.

> The lost fields *are* re-derivable — they originate from the on-disk test index (`_index.json` → `TestIndexEntry.DependsOn/Priority/Title`, consumed in `TestQueue.Build:37-46`). The current reconstruction path simply does **not** consult the index. So the queue is "derivable from disk" but **not** "derivable from the DB alone."

---

## Q2 — State-machine location

- **Pure state-machine logic is already transport-agnostic.** `StateMachine` is static and side-effect-free, returning immutable `TransitionResult<T>` — `src/Spectra.MCP/Execution/StateMachine.cs` (transition tables `:14-35`, `CanTransition` `:43-57`, `Transition` `:65-84`, `IsTerminal` used at `TestQueue.cs:18`). It lives in the `Spectra.MCP` assembly but has **zero** MCP/JSON-RPC coupling.
- **`ExecutionEngine` is the transport-agnostic orchestration service** — constructor takes repositories + identity + config (`ExecutionEngine.cs:24-35`), returns domain objects, no `JsonElement`/MCP types on its surface. The MCP **tool handlers are thin adapters**: deserialize → call `_engine.*` → serialize `McpToolResponse` (e.g. `AdvanceTestCaseTool.cs:42-196`, `GetExecutionStatusTool.cs:39-130`).
- **`TestHandle` is already in Core** — `src/Spectra.Core/Models/Execution/TestHandle.cs` (not in MCP). Domain models (`Run`, `TestResult`, `TestStatus`, `RunStatus`, `QueuedTest`, `TestIndexEntry`, `RunFilters`) are all in `Spectra.Core.Models.Execution`.

**Smallest type set for a CLI to replicate `start → status → advance → skip → finalize`:**
`ExecutionEngine` + `TestQueue` + `DependencyResolver` + `StateMachine` (`Spectra.MCP/Execution/`) and `RunRepository` + `ResultRepository` + `ExecutionDb` (`Spectra.MCP/Storage/`), over Core models. A CLI would construct these as `Program.cs` does (`:131-166`) and call `_engine.StartRunAsync / GetQueueAsync / GetStatusCountsAsync / AdvanceTestAsync / SkipTestAsync / FinalizeRunAsync` directly — **bypassing the tool handlers entirely**. These seven types currently sit in the `Spectra.MCP` assembly; CLI access means either referencing `Spectra.MCP` from the CLI or relocating `Execution/` + `Storage/` to `Spectra.Core` (no MCP dependency — the move is mechanical).

---

## Q3 — Opaque handle semantics

- **Generation:** `TestHandle.Generate` — `src/Spectra.Core/Models/Execution/TestHandle.cs:20-37`. Format `{runId[..8]}-{testId}-{rand4}`; suffix from `RandomNumberGenerator.GetBytes(3)` (`:29`). Built per test in `TestQueue.Build` (`TestQueue.cs:37`) and on retest (`TestQueue.Requeue:197`).
- **Purely a DB key — no in-memory handle→run map.** Lookup is `ResultRepository.GetByHandleAsync` (`SELECT … WHERE test_handle = @h`), keyed by the `UNIQUE` index `idx_results_handle` (`ExecutionDb.cs:92`). `TestHandle.Validate` (`:46-74`) is *structural only* (prefix + testId) and is **not invoked on the advance path** — advance trusts the DB lookup.
- **Unknown/expired handle → fail-loud.** `GetByHandleAsync` returns `null`; handlers check and return a structured error — `AdvanceTestCaseTool.cs:101-107` (`INVALID_HANDLE`), same pattern in `SkipTestCaseTool`/`GetTestCaseDetailsTool`. Inside the engine, `AdvanceTestAsync` throws `InvalidOperationException` on a missing handle (`ExecutionEngine.cs:208-212`), surfaced as `INVALID_TRANSITION`/`INVALID_HANDLE`. No silent default.
- Handles are **not signed** — the random suffix deters guessing/accidental collision only; not an auth token. Ownership is enforced separately via `ActiveRunResolver`/`VerifyOwnerAsync` (`ExecutionEngine.cs:401-408`). Handle loss (e.g. context compaction) is recoverable: `get_execution_status` / `advance_test_case` auto-resolve the in-progress or next-pending handle (`AdvanceTestCaseTool.cs:48-76`).

---

## Q4 — MCP tool inventory & classification (decisive table)

**Exactly 25 tools** registered in `src/Spectra.MCP/Program.cs:135-166` (enumerated below; the count is verified against that block).
Classes: **(A)** stateless RPC over SQLite/filesystem, equivalent today · **(B)** correctness/success depends on in-process `_queues` *today* · **(C)** needs a host-side OS capability.

| Tool | Class | Evidence (`path:line`) | CLI-shape / note |
|---|---|---|---|
| `start_execution_run` | A | `Program.cs:135`; `ExecutionEngine.StartRunAsync:40-96` | Builds rich queue & persists results; in-mem queue is a forward optimization, output is durable. |
| `get_execution_status` | B* | `Program.cs:136`; `GetExecutionStatusTool.cs:55-60`; `GetQueueAsync:125-143` | Summary counts from DB (accurate); **current-test ordering** uses the lossy reconstructed queue. Degrades, doesn't fail. |
| `pause_execution_run` | A | `Program.cs:137`; `PauseRunAsync:287-297` | Run-status only; no queue read. |
| `resume_execution_run` | A | `Program.cs:138`; `ResumeRunAsync:302-312` | Run-status only. |
| `cancel_execution_run` | A | `Program.cs:139`; `CancelRunAsync:317-328` | `_queues.Remove` is a harmless cache evict. |
| `finalize_execution_run` | B* | `Program.cs:140`; `FinalizeRunAsync:333-349` | Non-`force` "pending tests block finalize" guard reads `_queues` (`:338`) → **silently skipped when queue not in memory**. Pending count is in DB (`GetStatusCountsAsync`), so fixable to A. |
| `list_available_suites` | A | `Program.cs:141`; `ListAvailableSuitesTool.cs` | Filesystem/index read. |
| `list_active_runs` | A | `Program.cs:142`; `ListActiveRunsTool.cs` | DB query. |
| `cancel_all_active_runs` | A | `Program.cs:143`; `CancelAllActiveRunsTool.cs` | DB writes. |
| `get_test_case_details` | A | `Program.cs:146`; `GetTestCaseDetailsTool.cs` | Index + DB read (see Open Unknown #5). |
| `advance_test_case` | B | `Program.cs:147`; `AdvanceTestAsync:202-261` (blocking + `next` inside `if (_queues.TryGetValue…)` `:229`) | Result recorded to DB regardless; **block cascade & `next` skipped when queue absent**, and never cascades after lossy reconstruct (`DependsOn=null`). |
| `skip_test_case` | B | `Program.cs:148`; `SkipTestAsync:266-274` → `AdvanceTestAsync` | Same dependency as advance. |
| `bulk_record_results` | B | `Program.cs:149`; `BulkRecordResultsAsync:422-503` (blocking inside `if (_queues…)` `:457`) | Same. |
| `add_test_note` | A | `Program.cs:150`; `AddNoteAsync:279-282` | Pure DB append. |
| `retest_test_case` | **B (hard)** | `Program.cs:151`; `RetestTestCaseTool.cs:62` → `GetStatusAsync:109-120` (**returns `null` if queue not in `_queues`, does NOT reconstruct**); `RetestAsync:377-380` | **Hard-fails `RUN_NOT_FOUND` across any process boundary** — even under MCP after a server restart. See Open Unknown #3 (latent bug). |
| `save_screenshot` | C* | `Program.cs:152-153`; `SaveScreenshotTool.cs` | base64/file → ImageSharp → write file; no held handle. "Host" need is just a writable FS — **replicable from any CLI process**. |
| `save_clipboard_screenshot` | C | `Program.cs:154`; `SaveClipboardScreenshotTool.cs:103-144` | Shells to PowerShell `Get-Clipboard` (Win) / `pngpaste`·`osascript` (mac) / `xclip`·`wl-paste` (Linux). Host-bound — but a **local CLI is a better clipboard host** than a possibly-remote server. |
| `get_run_history` | A | `Program.cs:157`; `GetRunHistoryTool.cs` | DB query. |
| `get_execution_summary` | A | `Program.cs:158`; `GetExecutionSummaryTool.cs` | DB query. |
| `validate_tests` | A | `Program.cs:161`; `ValidateTestsTool.cs` | Filesystem/schema. |
| `rebuild_indexes` | A | `Program.cs:162`; `RebuildIndexesTool.cs` | Filesystem. |
| `analyze_coverage_gaps` | A | `Program.cs:163`; `AnalyzeCoverageGapsTool.cs` | Filesystem/index. |
| `find_test_cases` | A | `Program.cs:164`; `FindTestCasesTool.cs` | Index query. |
| `get_test_execution_history` | A | `Program.cs:165`; `GetTestExecutionHistoryTool.cs` | DB query. |
| `list_saved_selections` | A | `Program.cs:166`; `ListSavedSelectionsTool.cs` | Filesystem. |

**Counts — today:** A = 17 · B = 6 (`advance`, `skip`, `bulk_record`, `retest`, + degraded `status`, `finalize`) · C = 2 (both replicable from a local CLI).
**Counts — after the single shared reconstruction fix (Work-to-extract #2):** A = 23 · B = 0 · C = 2. **The entire B column collapses to one root cause.**

---

## Q5 — DB concurrency under short-lived processes

- **Bare connection string** `Data Source={dbPath}` — `src/Spectra.MCP/Storage/ExecutionDb.cs:21`. **NOT FOUND:** any `journal_mode`/WAL, `busy_timeout`, or PRAGMA (schema init is DDL-only, `:57-111`). Reader uses `Mode=ReadOnly` (`ExecutionDbReader.cs:43`) — also no PRAGMA.
- **Single long-lived connection per process** — `_connection` cached and reused (`ExecutionDb.cs:15,29-32,42`), disposed at shutdown (`DisposeAsync:116-123`). This in-process reuse **disappears** under a per-command CLI: each invocation opens/inits/closes its own connection.
- **No cross-process lock on the DB.** The pattern exists elsewhere — `PersistentTestIdAllocator` uses a `FileShare.None` lock + 10 s backoff (`src/Spectra.Core/IdAllocation/FileLockHandle.cs:46-52`) — but execution storage has **nothing** equivalent.
- With default `journal_mode=DELETE` and `busy_timeout=0` (Microsoft.Data.Sqlite default), two concurrent writer processes → the second hits `SQLITE_BUSY` as an immediate exception, no retry.

**Verdict: (b) → (c).** Today's single-user, sequential loop is fine even per-command. To be *safe* under a per-command model with any concurrency it needs **config at minimum** (`PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;` at open) and is *fully* safe with that plus the existing file-lock pattern if true concurrent writers appear. As-is (no PRAGMA), rapid writes from separate processes are **not** guaranteed safe.

---

## Q6 — Host-side capabilities

- **`save_clipboard_screenshot`** reads the clipboard by **shelling out**, not via an in-process API: PowerShell `Get-Clipboard -Format Image` on Windows (`SaveClipboardScreenshotTool.cs:103-112`), `pngpaste`/`osascript` on macOS (`:115-132`), `xclip`/`wl-paste` on Linux (`:135-144`), dispatched by `RuntimeInformation.IsOSPlatform` (`:93-100`); temp file cleaned in `finally`. Because it spawns OS processes, **any local process — including a `spectra` CLI — can do the same**. The only truly "server-bound" factor is the *machine*: a remote MCP server reads the *server's* clipboard. A **local CLI is strictly better** here.
- **`save_screenshot`** is pure file I/O — decode base64 or read a path, ImageSharp resize/encode to WebP in memory, `File.WriteAllBytesAsync`, store the relative path in the DB. **No held file handles, no streaming** (`SaveScreenshotTool.cs`). Replicable from any process with FS access.
- **No other tool** holds open handles, streams responses, or binds a long-lived OS resource (swept all of `Tools/`).

---

## Q7 — CLI cold-start cost

- **Code config only — runtime NOT measured (see Open Unknown #1).** `src/Spectra.CLI/Spectra.CLI.csproj` sets `OutputType=Exe`, `net8.0`, `PackAsTool=true`, `ToolCommandName=spectra`. **NOT FOUND:** `PublishReadyToRun`, `PublishAot`, `PublishTrimmed`/`TrimMode` — here or in `Directory.Build.props`. `Program.cs:Main` builds the full System.CommandLine graph on every invocation.
- Implication: ships as a JIT-compiled global tool (`dotnet tool install -g`), full dependency tree loaded each launch (System.CommandLine, Spectre.Console, GitHub Copilot SDK, YamlDotNet…). Typical managed-CLI cold start is in the low-hundreds-of-ms range — **an estimate, not a measurement**; timing `spectra --help` cold/warm requires running the binary, out of scope for a read-only investigation.
- Per-turn overhead for an N-step advance/skip loop under per-command processing ≈ N × (process start + connection open + schema `CREATE TABLE IF NOT EXISTS` no-op `ExecutionDb.cs:45-49`, cheap but non-zero per process).

---

## Q8 — Regression surface

**Pure-engine tests (survive a transport swap):**
- `tests/Spectra.MCP.Tests/Execution/StateMachineTests.cs` — transition logic.
- `tests/Spectra.MCP.Tests/Execution/TestQueueReconstructionTests.cs` — **directly exercises the lossy reconstruction path; read this first before any reconstruction change.**
- `tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs` — filtering/ordering.
- `tests/Spectra.MCP.Tests/Storage/ExecutionDbTests.cs`, `RunRepositoryTests.cs` — schema/CRUD.

**Mixed (engine logic invoked *through* tool wrappers — would need re-pointing at a CLI/engine entry; logic intact):**
- `tests/Spectra.MCP.Tests/Integration/{ExecutionFlowTests,ConcurrentUsersTests,BlockingCascadeTests,PauseResumeTests,RetestFlowTests,FilteredExecutionTests}.cs`. **`BlockingCascadeTests` and `RetestFlowTests` would expose the cross-process degradation** if a CLI path reconstructs lossily.

**Transport-coupled (the genuine risk if the JSON-RPC layer changes) — ~14 files:**
- `tests/Spectra.MCP.Tests/Server/McpProtocolStrictTests.cs` (param strictness) and the per-tool `tests/Spectra.MCP.Tests/Tools/*Tests.cs` set. These assert request/response envelopes and would not transfer to a CLI surface as-is.

**Gate check (flag, do NOT modify):**
- `tests/Spectra.Core.Tests/Storage/ExecutionDbReaderTests.cs` — the only execution-adjacent test under `Spectra.Core.Tests` (read-only, dashboard-side).
- `tests/Spectra.CLI.Tests/IO/TestPersistenceServiceTests.cs` — `TestPersistenceService` tests (generation-side, *not* execution). Both flagged per the regression gate; neither is in scope for an execution-surface change.

---

## Work-to-extract (minimal path to a transport-agnostic engine)

1. **Relocate (or reference) seven types** out of `Spectra.MCP` so the CLI can call them: `ExecutionEngine`, `TestQueue`, `DependencyResolver`, `StateMachine`, `RunRepository`, `ResultRepository`, `ExecutionDb`. They carry no MCP dependency (verified) — a mechanical move to `Spectra.Core` or a new `Spectra.Execution` assembly.
2. **Fix the single shared root cause — lossy queue reconstruction.** This collapses the entire B column. Either (a) make `ReconstructQueue`/`GetQueueAsync` re-read the on-disk index to restore `DependsOn`/`Priority`/`Title`/order (reusing `TestQueue.Build`'s `OrderByDependencies`), **or** (b) persist `depends_on`, `priority`, and an `order_index` onto `test_results` so reconstruction is DB-complete. Then route `advance`/`skip`/`bulk_record`/`retest`/`finalize`/`status` blocking, ordering, and guards through a uniform reconstructed queue. Also unify the two divergent queue-access paths — `GetQueueAsync` (reconstructs) vs. `GetStatusAsync` (returns `null`, `ExecutionEngine.cs:109-120`) — which is what makes `retest` hard-fail. `retest`'s null and `finalize`'s skipped guard both resolve as a consequence.
3. **Add SQLite per-process safety** (Q5): `PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;` at connection open; mirror the `FileLockHandle` pattern only if concurrent writers become real.
4. **Author the CLI command surface** as thin adapters (same shape as today's tool handlers) over `ExecutionEngine`. Per-tool CLI shapes are in the Q4 table's last column.

---

## Real losses — drop MCP entirely vs. keep a thin optional adapter

**If MCP is dropped entirely:**
- The ~14 transport tests (`Server/`, `Tools/`) become dead — deleted/rewritten; real churn, low risk.
- The in-process long-lived rich queue is gone, so the reconstruction fix (Work item #2) becomes **mandatory, not optional** — without it every CLI advance loses dependency blocking.
- Lose remote/networked execution (an MCP server driving a host the user isn't on). For clipboard/screenshot this is a *gain* locally.
- Lose the per-request JSON envelope contract that external MCP clients may depend on (unknown consumers — Open Unknown #4).

**If MCP is kept as a thin optional adapter over the extracted engine:**
- Both surfaces call the same `ExecutionEngine`; transport tests stay green; no loss of networked execution; the reconstruction fix benefits both. **Strictly dominant** *if* the extraction (Work item #1) is done — the adapter is the current tool handlers, unchanged.

---

## Open unknowns / needs measurement before a spec

1. **Q7 runtime numbers** — actual `spectra --help` cold/warm timings and per-advance process overhead were **not measured** (read-only). Measure before sizing a per-command loop.
2. ~~Exact registered tool count~~ — **RESOLVED: exactly 25** (`Program.cs:135-166`). (A sub-agent's "29" was incorrect.)
3. **`retest_test_case` is a latent bug, independent of the CLI question** — confirmed: `RetestTestCaseTool` checks the run via `_engine.GetStatusAsync` (`RetestTestCaseTool.cs:62`), and `GetStatusAsync` returns `null` whenever the queue is absent from `_queues` without reconstructing (`ExecutionEngine.cs:114-117`). So after any MCP server restart mid-run, `retest_test_case` returns `RUN_NOT_FOUND` even though the run is alive in the DB. Worth a standalone fix regardless of consolidation.
4. **External MCP consumers** — are CI jobs or other agents bound to the JSON-RPC tool schemas such that a CLI-only world would break them? Not determinable from this repo.
5. **`get_test_case_details` index source** — confirm it reads suite test files the same way a CLI would (assumed A; not deeply traced).
