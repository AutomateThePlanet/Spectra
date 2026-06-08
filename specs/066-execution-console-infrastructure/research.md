# Phase 0 Research: Execution Console Infrastructure

All NEEDS CLARIFICATION items from Technical Context are resolved below. Engine-side facts cite the
investigation under `docs/investigation/console/` and the live source.

---

## R1. HTTP transport — `System.Net.HttpListener` (BCL), not ASP.NET Core, not the MCP host

**Decision**: Host the console on `System.Net.HttpListener` bound to `http://127.0.0.1:{port}/`.

**Rationale**:
- The CLI tool (`Spectra.CLI.csproj`) references only `System.CommandLine`, `Spectre.Console`,
  `Microsoft.Extensions.Logging`, and the Copilot SDK — **no ASP.NET Core**. Adding `Microsoft.AspNetCore.App`
  to a packaged `dotnet tool` is a heavyweight framework dependency for ~7 endpoints + one static page.
  Constitution V (YAGNI / "prefer standard library when adequate") favors the BCL listener.
- `HttpListener` covers everything needed: route on `request.Url.AbsolutePath` + `HttpMethod`, read
  `request.InputStream` for multipart/base64 uploads, write JSON/HTML to `response.OutputStream`. One
  local user means no need for Kestrel's throughput or middleware pipeline.
- The MCP server is **stdio JSON-RPC** (`McpServer.cs:12-13,42,57` — `ReadLineAsync`/`WriteLineAsync` over
  `TextReader`/`TextWriter`); there is no HTTP listener to hang a route on (investigation A1). So the
  console **must** bring its own transport, and it sits beside MCP over the same engine — explicitly
  **not** a route on the MCP host (FR-002).

**Alternatives considered**:
- *ASP.NET Core minimal API* — richer (multipart model binding, DI), but adds a framework reference to the
  tool package and contradicts YAGNI for a single local page. Rejected.
- *A route on the MCP server* — impossible without re-architecting MCP from stdio to HTTP; explicitly out
  of scope and forbidden by FR-002. Rejected.

**Port selection**: bind to a fixed default (e.g. `7878`) and fall back to an ephemeral free port if
taken; the chosen port is written to the marker (R2) and printed at launch.

---

## R2. Detached lifecycle on Windows — self-re-exec a hidden worker; **PROTOTYPE-GATED**

**Decision**: `spectra run console` (the *launcher*) starts a **separate worker process** that runs the
`HttpListener` loop, then returns immediately after printing the URL. The worker is launched detached so
it does not inherit the launcher's stdio and is not reaped when the launcher (or an agent session that
invoked it) exits. A marker file `.execution/console.json` records `{ pid, port, url, startedUtc }`.
`spectra run console --stop` reads the marker, kills the pid, and deletes the marker.

**Why this is the one genuine unknown**: Web research confirms there is **no first-class cross-platform
"start detached" API in .NET today** ([dotnet/runtime #124334](https://github.com/dotnet/runtime/issues/124334)),
and that **child processes on Windows can die when the parent exits** — specifically when the parent is
inside a Windows **Job Object** with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`
([childprocess #99](https://github.com/enkessler/childprocess/issues/99)). Claude Code / agent harnesses
commonly run tracked background tasks inside exactly such a job, so a naively-spawned child could be
reaped on context compaction — the precise failure FR-009 forbids. This is why the spec mandates a
prototype before locking the mechanism.

**Recommended pattern (to be confirmed by the T001 prototype)**, in order of preference:
1. **`cmd.exe /c start "" /B <spectra> run console --serve --port N`** — `start` launches the worker
   independently of the launcher's console; combined with the worker writing its own marker, the launcher
   can exit cleanly. Lowest-friction; works for the human-launched case.
2. **`CREATE_BREAKAWAY_FROM_JOB`** via a `CreateProcess` P/Invoke (when the job permits breakaway) — the
   robust answer for the *agent-launched-inside-a-kill-on-close-job* case. Use only if (1) fails the
   survival check.
3. macOS/Linux: `Process.Start` with `UseShellExecute=false`, redirected stdio closed, and the child in
   its own process group — detached processes survive parent exit on POSIX without special flags.

**PROTOTYPE OUTCOME (T020, executed 2026-06-08 on Windows 11)**: Pattern (1) — `Process.Start` with
`UseShellExecute=true` + `WindowStyle=Hidden` — **confirmed** for the survive-launcher-exit case. The
launcher (`spectra run console --port 7991`) returned exit 0; the detached worker kept serving (`GET
/current` and `GET /` both returned HTTP 200 *after* the launcher had exited); the marker was written
with the real worker pid/port/url; and `spectra run console --stop` terminated it (post-stop the port no
longer responds). `HttpListener` bound `http://127.0.0.1:7991/` with **no elevation / no urlacl**. The
launcher was also made host-agnostic (`ResolveWorkerLauncher`): it re-invokes the apphost directly, or
`dotnet "<entry.dll>" …` when hosted under `dotnet`. **Residual risk:** survival across an *agent-tracked
Windows Job Object with kill-on-close* was not exercised here (no such job in the test harness); the
`CREATE_BREAKAWAY_FROM_JOB` fallback remains documented and should be validated when the agent revision
(Spec 2) actually launches the console from within its tracked task.

**Original prototype acceptance criteria (for reference)**:
- Start the worker, exit the launcher → worker still serving (`GET /current` returns 200).
- Simulate the agent-tracked-job case (launch from within a job object with kill-on-close) → worker
  survives the job closing. If pattern (1) fails this, adopt pattern (2).
- `--stop` reliably terminates the worker and removes the marker.

**Stale-marker handling** (edge case in spec): if `console.json` names a pid that no longer exists (or
whose start time doesn't match), treat the console as not running — a fresh launch overwrites the marker;
`--stop` reports "no running console" cleanly (FR-012).

**Concurrency**: a detached console + any short-lived `spectra run` writer is already safe — `ExecutionDb`
opens `PRAGMA journal_mode=WAL; busy_timeout=5000` (`ExecutionDb.cs:49-52`); `WalConcurrencyTests` is the
guard (C13). No change needed.

**Alternatives considered**: Windows Service / `schtasks` (overkill, requires elevation/registration —
rejected); keeping the listener in the foreground of `spectra run console` (fails FR-009 detachment —
rejected); a tracked agent background task (fails survive-compaction — rejected, that's the whole point).

---

## R3. Write-back surface — every operation already exists 1:1 on `ExecutionEngine`

**Decision**: Each console endpoint is a thin wrapper over an existing engine method; the console adds
**zero** engine logic (investigation A2). Mapping the console uses:

| Console action | Engine method | Line |
|---|---|---|
| Current/next test + run status | `GetStatusAsync` + `GetQueueAsync` + `GetStatusCountsAsync` | `:127` / `:144` / `:430` |
| Test details | `GetTestResultAsync` (+ index/test-case loaders from `RunServices`) | `:239` |
| Mark in-progress | `StartTestAsync` | `:247` |
| Record PASS/FAIL/BLOCKED + comment | `AdvanceTestAsync` | `:269` |
| Skip | `SkipTestAsync` | `:336` |
| Add note | `AddNoteAsync` | `:349` |
| Screenshot ingest | `ScreenshotService.EncodeAndSaveAsync` + `ResultRepository.AppendScreenshotPathAsync` | `:28` / `:263` |
| Finalize | `FinalizeRunAsync` | `:403` |
| Active run discovery | `GetActiveRunAsync` | `:492` |

**Rationale**: The CLI `RunHandler` and MCP tools already prove these calls; the console is a third caller.
`AdvanceTestAsync` returns `(recorded, blockedIds, next)` — exactly what a button-click handler needs to
refresh the page in one round-trip (investigation A2). **The simplest correct console reuses
`RunHandler` directly** where possible (it already returns typed `RunResult`s and enforces guardrails),
rather than re-calling the engine — to be decided per-endpoint in design, but the parity contract is the
same either way.

---

## R4. Source-of-truth invariant — browser is a view + write-back caller, never a store

**Decision**: Page state is always a projection of `GetStatusAsync`; the browser keeps **no** authoritative
run state (no `localStorage`/`sessionStorage` of results, no export/import-as-store).

**Rationale** (investigation A3): verdicts persist engine → `ResultRepository.UpdateStatusAsync`
(`ExecutionEngine.cs:287-291`, SQL `UPDATE test_results` at `ResultRepository.cs:165`) → SQLite; the blocked
cascade is written too (`:317-322`); notes via `AppendNoteAsync` (`:193`). The only in-memory state is the
warm `_queues` cache (`ExecutionEngine.cs:23`), which is **not** a store — a cold process rebuilds it
DB-complete via `GetQueueAsync`→`ReconstructQueue` (spec 064). So "refresh loses nothing" is **free**: a
reload just re-reads `GetStatusAsync`. This makes RMH's `localStorage` + export/import model unnecessary
(FR-003, SC-002).

---

## R5. Live update — poll `/current`

**Decision**: The page polls `GET /current` on an interval (~1.5–2 s) and immediately after every
write-back, replacing the rendered projection.

**Rationale**: No HTTP/SSE/websocket primitive exists in the repo today (investigation A6); for one local
human, polling is the lowest-complexity option and doubles as the autosave story (because SQLite is the
store, R4). SSE is a possible later optimization, explicitly out of scope (FR-006).

---

## R6. Styling — borrow `ReportWriter` `:root` tokens; the interactive page is new

**Decision**: Extract `ReportWriter.cs:413-431` `:root` design tokens (navy `#1B2A4A` / `#2D3F5E`, the
per-status palette, card/circle CSS at `:485-531`) into the console page's inline `<style>` as the shared
visual language. The interactive page (status buttons, comment box, screenshot drop, poll-refresh) is
**new** — `ReportWriter` is a frozen end-of-run snapshot, not extended (investigation A5/A8).

**Rationale**: Visual consistency with reports (FR-007) without coupling the console to `ReportWriter`'s
static assembly. The read-only test-detail panel **may** reuse `RenderTestContent` (`:827`) if convenient,
but the page does not inherit `ReportWriter`'s structure. Explicitly **not** RMH Tailwind or any ATP
palette (FR-007). Fonts: Inter / JetBrains Mono, matching the report `<head>` (`:411`).

---

## R7. Screenshot ingest — one new endpoint, existing storage path

**Decision**: A single ingest endpoint accepts a browser upload (multipart form-data **or** base64
data-URL), converts to `byte[]`, and calls `ScreenshotService.EncodeAndSaveAsync(reportsPath, runId,
testId, existingCount, bytes)` then `ResultRepository.AppendScreenshotPathAsync(testHandle, relativePath)`
— the exact path the MCP `SaveScreenshotTool` and `spectra run screenshot` already use (investigation A4).

**Rationale**: No new storage model, no change to the encode/attach path. `existingCount` is the current
result's `screenshot_paths` length. A **local** console is strictly better than a remote MCP server for
clipboard capture (the local machine *is* the host), but clipboard is not required for this spec — browser
upload is the surface (FR-006).

---

## Summary of decisions

| # | Topic | Decision |
|---|---|---|
| R1 | HTTP transport | `System.Net.HttpListener` on `127.0.0.1`; not ASP.NET Core; not the MCP host |
| R2 | Detached lifecycle | Self-re-exec hidden worker + marker file; **prototype-gated (T001)**; fallback `CREATE_BREAKAWAY_FROM_JOB` |
| R3 | Write-back surface | 1:1 thin wrappers over existing `ExecutionEngine` methods (reuse `RunHandler` where practical) |
| R4 | Source of truth | SQLite via engine; browser holds no state; projection of `GetStatusAsync` |
| R5 | Live update | Poll `GET /current` on interval + after writes; no SSE/websocket |
| R6 | Styling | Borrow `ReportWriter` `:root` tokens into a new interactive page |
| R7 | Screenshots | One ingest endpoint → `ScreenshotService` + `AppendScreenshotPathAsync` |

**Sources** (R2 web research):
- [\[Process\]: Start detached process · dotnet/runtime #124334](https://github.com/dotnet/runtime/issues/124334)
- [Detached process on Windows dies when parent process ends · childprocess #99](https://github.com/enkessler/childprocess/issues/99)
- [Make Process.Start have an option to change handle inheritance · dotnet/runtime #13943](https://github.com/dotnet/runtime/issues/13943)
