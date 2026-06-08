# A. Console Infrastructure (Spec 1)

Evidence for the first spec: a local detached server + write-back endpoints + interactive page, over
the extracted `Spectra.Execution` engine with SQLite as the single source of truth.

> Read order: **A2 → A3 first** (they determine whether the console is a thin transport or hidden new
> state), then A1, then A4–A8.

---

## A2. Write-back surface — every operation the console needs already exists on the engine

The console needs: get current/next test, get details, record PASS/FAIL/BLOCKED + comment, skip, add
note, attach screenshot, advance, run status/summary, bulk record, retest, finalize, start. Each maps
**1:1 to an existing public method** on `ExecutionEngine` (`src/Spectra.Execution/Execution/ExecutionEngine.cs`):

| Console operation | Engine method | Line | MCP tool that already proves it (handler → engine) |
|---|---|---|---|
| Start a run | `StartRunAsync` | `:43` | `StartExecutionRunTool` |
| Run status (run + queue) | `GetStatusAsync` | `:127` | `GetExecutionStatusTool` |
| Current/next test (queue) | `GetQueueAsync` | `:144` | `GetExecutionStatusTool`, `GetTestCaseDetailsTool` |
| Get one test's result row | `GetTestResultAsync` | `:239` | `GetTestCaseDetailsTool` |
| Start (mark in-progress) | `StartTestAsync` | `:247` | `GetTestCaseDetailsTool` |
| Record PASS/FAIL/BLOCKED + comment | `AdvanceTestAsync` | `:269` | `AdvanceTestCaseTool` |
| Skip | `SkipTestAsync` | `:336` | `SkipTestCaseTool` |
| Add note | `AddNoteAsync` | `:349` | `AddTestNoteTool` |
| Pause / Resume / Cancel | `PauseRunAsync` / `ResumeRunAsync` / `CancelRunAsync` | `:357` / `:372` / `:387` | `Pause/Resume/CancelExecutionRunTool` |
| Finalize (+ reports) | `FinalizeRunAsync` | `:403` | `FinalizeExecutionRunTool` |
| Status counts (for charts) | `GetStatusCountsAsync` | `:430` | `GetExecutionStatusTool` |
| All results (for the list) | `GetResultsAsync` | `:438` | `FinalizeExecutionRunTool` |
| Retest | `RetestAsync` | `:446` | `RetestTestCaseTool` |
| Active run discovery | `GetActiveRunAsync` | `:492` | `ListActiveRunsTool` |
| Bulk record | `BulkRecordResultsAsync` | `:501` | `BulkRecordResultsTool` |

**Finding:** **nothing is missing on the engine side.** The console's write-back endpoints are thin
HTTP wrappers around these existing methods — exactly as the CLI `RunHandler` and the MCP tools already
are. The only *new* surface Spec 1 must add is (a) the HTTP transport and (b) a screenshot ingest
endpoint (see A4).

Verbatim signature of the central write path:
```csharp
// ExecutionEngine.cs:269
public async Task<(TestResult Recorded, IReadOnlyList<string> Blocked, QueuedTest? Next)> AdvanceTestAsync(
    string runId, string testHandle, TestStatus status, string? notes = null)
```
It returns the recorded result, the cascade of newly-blocked test IDs, and the next test — everything a
button-click handler needs to refresh the page.

---

## A3. Source-of-truth invariant — verdicts persist through the engine to SQLite; the browser holds nothing

Recording a verdict and advancing goes engine → SQLite, the same write path used today:

- `AdvanceTestAsync` writes the verdict via `_resultRepo.UpdateStatusAsync(...)`
  (`ExecutionEngine.cs:287-291`), which is a SQL `UPDATE test_results` (`ResultRepository.cs:165`,
  statement at `:171`).
- The blocked cascade is **also** written to the DB: for each dependent, `UpdateStatusAsync(..., blockedBy: testId)` (`ExecutionEngine.cs:317-322`).
- Notes: `AppendNoteAsync` → `UPDATE test_results` (`ResultRepository.cs:193`, `:199`).
- Results are created at run-build via `CreateAsync`/`CreateManyAsync` → `INSERT INTO test_results …`
  (`ResultRepository.cs:22`, `:28`; `:51`, `:64`).

**The only in-memory state** is the per-run warm cache `_queues` (`ExecutionEngine.cs:23`,
`private readonly Dictionary<string, TestQueue> _queues = [];`). It is **not** a store: on a cold process
it is rebuilt DB-complete from the durable `queue_snapshot` + `test_results` via
`GetQueueAsync` (`ExecutionEngine.cs:144`) → `ReconstructQueue` (`ExecutionEngine.cs:173`), the spec-064
lossless reconstruction. Nothing the browser does needs to be cached browser-side; a page refresh just
re-reads `GetStatusAsync`.

**Invariant for the console:** the browser is a **view + write-back caller, never a store.** Every
button click is an HTTP call that mutates SQLite through the engine; the page state is always a
projection of `GetStatusAsync`. This makes RMH's `localStorage` + export/import model unnecessary —
"autosave so refresh doesn't lose progress" is *free* because SQLite is the store.

---

## A1. Server host — a new `spectra run console` subcommand over `RunServices`, not the MCP host

Today `spectra run` wires the engine **per short-lived process** in `RunServices`
(`src/Spectra.CLI/Commands/Run/RunServices.cs:39-54`):
```csharp
Config = new McpConfig { BasePath = BasePath, ReportsPath = reportsPath };   // :43
_db = new ExecutionDb(Path.Combine(BasePath, ".execution"));                 // :48
RunRepo = new RunRepository(_db); ResultRepo = new ResultRepository(_db);
SnapshotRepo = new QueueSnapshotRepository(_db);
Engine = new ExecutionEngine(RunRepo, ResultRepo, SnapshotRepo, new UserIdentityResolver(), Config); // :52
ReportGenerator = new ReportGenerator();                                     // :53
ReportWriter = new ReportWriter(reportsPath);                               // :54
```
The engine constructor depends only on the three repositories + `UserIdentityResolver` + `McpConfig`
(`ExecutionEngine.cs:25-38`), and `ExecutionDb` points at `.execution/spectra.db` (`ExecutionDb.cs`).

**Smallest transport-neutral seam:** a new `spectra run console` subcommand that constructs **one
long-lived** `RunServices` (or its parts) and serves HTTP, dispatching each endpoint to the same engine
methods listed in A2. This duplicates **zero** engine logic — it is a sibling of `RunHandler` and the
MCP tools.

**Not a route on the MCP host.** The MCP server is **stdio JSON-RPC**, not HTTP: `McpServer` reads/writes
through `TextReader`/`TextWriter` (`McpServer.cs:12-13`), looping `ReadLineAsync` (`:42`) /
`WriteLineAsync` (`:57`). There is no HTTP listener to hang a route on. The console therefore brings its
own HTTP transport (see A6) and sits beside MCP, both over the same engine.

---

## A4. Screenshots — reuse `ScreenshotService`; add one browser-upload ingest endpoint

How screenshots work today (`src/Spectra.Execution/Screenshots/ScreenshotService.cs`):
- `EncodeAndSaveAsync(reportsPath, runId, testId, existingCount, imageBytes, …)` (`:28`) takes **raw
  bytes**, resizes if oversized (`:50`), encodes WebP (`:34`,`:44`), writes under
  `{reportsPath}/{runId}/attachments/` (`:37`), and returns a `SavedScreenshot` whose `RelativePath` is
  `"{runId}/attachments/{filename}"` (`:67`, record at `:21`).
- The path is attached to the result via `ResultRepository.AppendScreenshotPathAsync(testHandle, path)`
  (`:263`): it reads the existing JSON array (`SELECT screenshot_paths …`, `:269`), appends, and writes
  back (`UPDATE test_results SET screenshot_paths …`, `:281`). The column is the result's
  `screenshot_paths` list (model `Spectra.Core/Models/Execution/TestResult.cs`, serialized as a JSON
  array).
- The MCP `SaveScreenshotTool` accepts either `image_data` (base64) **or** `file_path`, and the
  `spectra run screenshot` / `screenshot-clipboard` CLI commands do the same. Clipboard capture is
  `TryCaptureClipboardAsync` (`:76-80`), which is **process-local** (Windows `Get-Clipboard -Format
  Image`, `:87`) — so a **local** console is strictly better than a remote MCP server, which would read
  the *server's* clipboard.

**Finding:** the console **reuses `ScreenshotService` directly**. The one new piece is a thin HTTP
ingest endpoint: browser upload (multipart or base64 data-URL) → `byte[]` →
`ScreenshotService.EncodeAndSaveAsync(...)` → `ResultRepository.AppendScreenshotPathAsync(...)`. No new
storage model, no change to the encode/attach path. (`existingCount` for the filename comes from the
current result's `screenshot_paths` length.)

---

## A5. Rendering reuse — borrow the styling tokens + per-test fragments; the interactive page is new

`src/Spectra.Execution/Reports/ReportWriter.cs` is a **static end-of-run** renderer. What is reusable
vs new:

**Reusable as styling tokens / fragments:**
- The inline `<style>` block (`:412`) with `:root` design tokens (`:413`) — navy `--color-navy: #1B2A4A`
  (`:414`), `--color-navy-light` (`:415`), and per-status colors wired to summary cards
  (`.summary-card.passed/failed/skipped/blocked/total`, `:494-508`).
- The nav bar (`.report-nav`, `:440`, banner `img` `:447`; element at `:727`).
- The summary cards (`.summary-card`, `:485`; markup `:754-770`) and the conic-gradient pass-rate
  circle (`:521`).
- `RenderTestContent(TestResultEntry, reportsPath)` (`:827`) returns a **pure HTML fragment** for one
  test (called four times, `:313/:329/:346/:382`); it inlines screenshots as base64 data URIs, so a
  reused fragment carries its images with no extra fetch.

**Must be new (interactive page):**
- The page is currently a frozen snapshot. The console needs: per-test **status buttons** wired to the
  A2 write-back endpoints, a comment field posting to `AddNoteAsync`/`advance --notes`, a screenshot
  drop wired to the A4 ingest endpoint, clickable charts that **filter** the list, and refresh-after-
  write (poll, A6). None of that exists in `ReportWriter`.

**Concrete recommendation:** the console is a **fresh interactive render that borrows the style tokens**
(extract the `:root` block + card/circle CSS as the shared visual language; optionally reuse
`RenderTestContent` for the read-only test detail panel), not an extension of `ReportWriter`'s static
HTML assembly.

---

## A6. Live update — poll `/current`; no streaming primitive exists today

There is **no HTTP server, SSE, or websocket** anywhere in the codebase today — the only transport is
the MCP stdio loop (`McpServer.cs:12-13,42,57`). No `HttpListener` / `WebApplication` / `Kestrel` /
EventSource usage was found in `src/`.

**Lowest-complexity option:** the page **polls `/current`** (re-fetch `GetStatusAsync` + counts on an
interval and after every write). Because SQLite is the source of truth (A3), poll-refresh is also the
autosave story — a refresh never loses progress. SSE is a possible later optimization but is not needed
for a one-human-at-a-time local console.

---

## A7. Detached lifecycle — no existing pattern; launch/stop lives in `spectra run console`

**Constraint** (from the background-process design intent): the server must start **detached from the
agent session** — survive context compaction, not be killed as a tracked background task, and stop only
on an explicit command.

**Evidence on what exists:** no detached-server pattern is present. A search of `docs/investigation` for
detach/background/compaction found none; the existing `Process.Start` usages are short-lived and
synchronous (e.g. `ScreenshotService` clipboard capture, `:76-80`). So this is **net-new** for Spec 1.

**Where it lives:** launch and stop belong to the `spectra run console` command (start the listener,
write a pid/port marker, expose `spectra run console --stop`).

**Safe pattern — `INFERRED`** (primary platform is native Windows; macOS secondary): start the listener
process with `UseShellExecute=true` fire-and-forget (or `cmd /c start /b …`) so it does not inherit the
agent's stdio and is not a tracked child. **What would confirm it:** a prototype that starts the server,
then verifies it survives the launching process exiting and is not reaped on agent context compaction.
Concurrency is already safe — `ExecutionDb` opens WAL + `busy_timeout=5000` (`ExecutionDb.cs:49-52`), so
the detached server and any short-lived `spectra run` writer coexist without `SQLITE_BUSY`.

---

## A8. Styling source — `ReportWriter` inline CSS is canonical; not ATP/RMH Tailwind

The canonical Spectra report visual identity is **inline in `ReportWriter.cs:412-724`**: navy
`#1B2A4A` + `#2D3F5E` (`:414-415`), the per-status palette (`:494-508`), and the card/nav/circle layout.
`src/Spectra.CLI/Dashboard/BrandingInjector.cs` injects branding into the **dashboard** static template,
**not** into execution reports — so it is not the console's styling source. There is **no ATP /
AutomateThePlanet** palette in the report path, and RMH's Tailwind look is explicitly *not* the target.

**Finding:** the console matches the `ReportWriter` tokens (navy + status colors + Inter / JetBrains
Mono), echoing the RMH *UX* in *Spectra's* skin.
