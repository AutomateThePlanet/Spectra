# Implementation Plan: Execution Console Infrastructure

**Branch**: `066-execution-console-infrastructure` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/066-execution-console-infrastructure/spec.md`

## Summary

Add a `spectra run console` subcommand that starts a **detached, local HTTP server** serving one
ephemeral, gitignored web page from which a QA engineer drives a manual run — current test, PASS / FAIL /
BLOCKED, comment, screenshot — with **SQLite (via the existing `ExecutionEngine`) as the single source of
truth**. The console is a *third transport* alongside the CLI `RunHandler` and the MCP tools: it
constructs **one long-lived `RunServices`** and dispatches each HTTP endpoint 1:1 to existing engine
methods. The browser is a view + write-back caller — no browser store; page state is always a projection
of `GetStatusAsync`, refetched by polling. Guardrails (`STATUS_REQUIRED` / `NOTES_REQUIRED`) are
replicated at the HTTP boundary verbatim from `RunHandler.AdvanceAsync`. Screenshots reuse
`ScreenshotService.EncodeAndSaveAsync` + `ResultRepository.AppendScreenshotPathAsync` behind one
browser→bytes ingest endpoint. The page borrows `ReportWriter`'s `:root` design tokens (navy + status
palette). The HTTP layer is `System.Net.HttpListener` (BCL — no new framework dependency, satisfies
YAGNI). The one genuine unknown — a Windows detached-process pattern that survives the launching agent
session — is resolved by a **prototype gate (T001)** before the lifecycle work is locked.

## Technical Context

**Language/Version**: C# 12, .NET 8  
**Primary Dependencies**: `System.Net.HttpListener` (BCL HTTP transport — no ASP.NET Core added to the CLI tool); `System.CommandLine` (existing CLI host); the existing `Spectra.Execution` library (`ExecutionEngine`, `RunRepository`/`ResultRepository`/`QueueSnapshotRepository`, `ScreenshotService`, `ReportWriter` styling); `SixLabors.ImageSharp` (already pulled transitively by `ScreenshotService`)  
**Storage**: SQLite at `.execution/spectra.db` (WAL + `busy_timeout=5000` already set by `ExecutionDb`) — reused unchanged; the console adds **no** schema and **no** new store  
**Testing**: xUnit — new `tests/Spectra.CLI.Tests/Commands/Run/WebConsole/` (`ConsoleParityTests`, `ConsoleGuardrailTests`, `ConsoleScreenshotTests`, `ConsoleLifecycleTests`)  
**Target Platform**: Windows (primary), macOS (secondary); localhost only  
**Project Type**: CLI subcommand + embedded static web asset (single project — `Spectra.CLI`)  
**Performance Goals**: Single local human; poll interval ~1.5–2 s; page projection of `GetStatusAsync` returns well under perceptible latency for a typical suite (≤ a few hundred tests)  
**Constraints**: Localhost-only bind (`127.0.0.1`); one human at a time; browser holds **no** authoritative state; engine/storage/screenshot/report code reused **unmodified**; the regression net (C13) stays byte-unchanged green  
**Scale/Scope**: One run, one engineer, one machine; ~7 HTTP endpoints + one static page + a pid/port marker file

### Resolved unknowns (see research.md)

1. **HTTP transport** → `System.Net.HttpListener` (BCL), not ASP.NET Core, not the MCP host.
2. **Detached lifecycle on Windows** → self-re-exec a hidden worker process; **prototype-gated** (T001) because Windows job-object kill-on-close can reap children of an agent-tracked background task. Marker file (`.execution/console.json`: pid + port + url) enables discovery and `--stop`.
3. **Live update** → polling `GET /current` on an interval and after every write (no SSE/websocket).
4. **Styling** → borrow `ReportWriter.cs:413-431` `:root` tokens + card/status CSS as a shared inline stylesheet on a new interactive page (not an extension of `ReportWriter`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|---|---|---|
| **I. GitHub as Source of Truth** | Console adds no source-of-truth store. Test definitions stay in `test-cases/`; run state stays in `.execution/spectra.db` (already the execution store, gitignored). The page + marker are ephemeral, gitignored runtime artifacts (FR-011). | ✅ Pass |
| **II. Deterministic Execution** | The console is a transport; it calls the SAME deterministic `ExecutionEngine` and never manages state. Every write goes through the engine's state machine; page state is a projection of `GetStatusAsync`. No state lives in the browser or the HTTP layer. | ✅ Pass |
| **III. Orchestrator-Agnostic** | Console is independent of any LLM orchestrator; it is a human transport over the same engine the MCP tools use. No new orchestrator coupling. | ✅ Pass |
| **IV. CLI-First** | The console is launched and stopped by explicit CLI commands (`spectra run console`, `… --stop`) with deterministic exit codes. The web UI sits *on top of* the CLI/engine foundation, exactly as the constitution prescribes ("GUIs can be built on top of a solid CLI"). | ✅ Pass |
| **V. Simplicity (YAGNI)** | BCL `HttpListener` over adding ASP.NET Core to the tool; polling over SSE; reuse `ScreenshotService`/`ReportWriter`/`ExecutionEngine` verbatim; no new abstraction, no new storage model, no feature flags. The only net-new infra is the HTTP listener + one ingest endpoint + the detached-launch shim. | ✅ Pass |

**Quality Gates**: `spectra validate` semantics unaffected (no test-file or index changes). New code is additive; regression net (C13) must stay green unchanged — this is itself an explicit success criterion (SC-006).

**Result**: PASS — no violations, Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/066-execution-console-infrastructure/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── console-http.md  # HTTP endpoint contract (request/response/guardrails)
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Commands/Run/
│   ├── RunCommand.cs            # MODIFY: AddCommand(BuildConsole()) — `console` subcommand + --stop, --port
│   ├── RunServices.cs           # REUSE unchanged (the console constructs ONE long-lived instance)
│   ├── RunHandler.cs            # REUSE unchanged (guardrail logic is the reference the console replicates)
│   └── WebConsole/                 # NEW — the console transport (sibling of RunHandler)
│       ├── ConsoleCommand.cs        # launch / --stop / status wiring + detached re-exec + marker I/O
│       ├── ConsoleServer.cs         # HttpListener host: route table → ConsoleEndpoints; lifecycle
│       ├── ConsoleEndpoints.cs      # endpoint handlers → ExecutionEngine (A2 mapping); guardrails (FR-005)
│       ├── ConsoleMarker.cs         # .execution/console.json read/write (pid, port, url); stale detection
│       └── ConsolePage.cs           # builds the interactive HTML (borrowed :root tokens) + inline JS
└── Spectra.CLI.csproj          # (no new PackageReference; HttpListener is BCL)

tests/Spectra.CLI.Tests/Commands/Run/WebConsole/   # NEW
├── ConsoleParityTests.cs        # write-back leaves same DB state as engine-direct/RunHandler
├── ConsoleGuardrailTests.cs     # STATUS_REQUIRED / NOTES_REQUIRED at the HTTP boundary
├── ConsoleScreenshotTests.cs    # upload → ScreenshotService → screenshot_paths; no browser store
└── ConsoleLifecycleTests.cs     # marker read/write, stale-marker handling, --stop, detached survival
```

**Structure Decision**: Single project (`Spectra.CLI`), matching the existing `spectra run` surface
(Spec 065). The console lives in a new `Commands/Run/WebConsole/` folder as a **sibling transport** to
`RunHandler` and the MCP tools — it constructs one long-lived `RunServices` and dispatches to the same
`ExecutionEngine`. No changes to `Spectra.Execution`, `Spectra.Core`, or the MCP projects. The web asset
is generated server-side (no separate frontend project — YAGNI for one local page).

## Complexity Tracking

> No Constitution violations — table intentionally omitted.
