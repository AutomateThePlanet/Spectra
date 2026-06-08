# Implementation Plan: Execution Surface Consolidation (CLI run + MCP-as-adapter)

**Branch**: `065-execution-surface-consolidation` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/065-execution-surface-consolidation/spec.md`

## Summary

Relocate the deterministic execution engine and its SQLite storage out of the `Spectra.MCP` executable into a new transport-neutral `Spectra.Execution` class library, referenced by both the CLI and MCP. Add a `spectra run` command group whose subcommands are thin adapters over the same `ExecutionEngine` the MCP tools already call, so a full manual-execution loop is drivable from the one `spectra` global tool with no MCP server and no per-client MCP config. Keep `Spectra.MCP` as a thin adapter (its tool handlers unchanged). Harden SQLite for short-lived processes (WAL + busy_timeout at connection open). Ship a `spectra-execute` SKILL and re-point the execution agent at `spectra run …`, preserving the present→wait-for-verdict→advance discipline.

**Key architectural decision (decisive, see research.md R1)**: the relocated types **keep their existing namespaces** (`Spectra.MCP.Execution`, `Spectra.MCP.Storage`, `Spectra.MCP.Identity`, `Spectra.MCP.Infrastructure`). Every MCP source file and **every protected test** references those namespaces; preserving them means the move is *file relocation + project-reference rewiring only* — zero `using` edits, so the ~14 transport tests and the integration/tool test corpus compile byte-unchanged. That untouched-and-green corpus is the proof the extraction preserved behavior (SC-003/SC-004). A future spec may rename namespaces cosmetically; out of scope here (Constitution V / YAGNI).

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine (CLI surface), Spectre.Console (CLI rendering), Microsoft.Data.Sqlite (storage), SixLabors.ImageSharp (screenshot encode), xUnit (tests)
**Storage**: SQLite at `.execution/spectra.db` (single file; `runs`, `test_results`, `queue_snapshot` tables — schema owned by `ExecutionDb`)
**Testing**: xUnit across `Spectra.Core.Tests`, `Spectra.CLI.Tests`, `Spectra.MCP.Tests`, `Spectra.Integration.Tests`; new `Spectra.Execution.Tests` for the relocated assembly
**Target Platform**: Cross-platform .NET global tool (`dotnet tool install -g`)
**Project Type**: Multi-project solution — class libraries (`Spectra.Core`, new `Spectra.Execution`) + two executables (`Spectra.CLI`, `Spectra.MCP`)
**Performance Goals**: Human-paced interactive loop; managed-CLI cold start (low-hundreds-of-ms) is acceptable — no AOT/ReadyToRun in scope
**Constraints**: Behavior-preserving extraction (transport tests must stay green untouched); short-lived-process SQLite safety under single-user contention; no new MCP tools; no change to engine public surface
**Scale/Scope**: Single-user manual execution; one active run per user per suite; suites of tens–hundreds of tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| **I. GitHub as Source of Truth** | ✅ No change. Test cases/docs/config stay file-based in Git; execution state stays in `.execution/spectra.db` (already git-ignored runtime state). |
| **II. Deterministic Execution** | ✅ Strengthened. The same `ExecutionEngine`/`StateMachine` now drives both surfaces; the CLI path inherits Spec 064's lossless reconstruction, so determinism is identical long-lived vs short-lived. No state moves into the orchestrator. |
| **III. Orchestrator-Agnostic Design** | ✅ Strengthened. MCP is retained for any MCP orchestrator; the CLI adds a second orchestrator-agnostic surface over the same engine. Self-contained per-invocation operations (each CLI call reconstructs from the DB). |
| **IV. CLI-First Interface** | ✅ Directly advances this principle — execution becomes a first-class CLI surface (`spectra run …`) with deterministic exit codes; named commands, no chat loop in the engine; the agent drives the CLI, never writes files directly. |
| **V. Simplicity (YAGNI)** | ✅ Namespace-preservation avoids a mass rename; no feature flags/compat shims; reuse the existing engine, tool handlers, loaders, and CLI command/handler/JSON-writer pattern. One new project is justified (a shared library is the minimal way for two executables to share one engine without one referencing the other). |

**Gate result: PASS** — no violations; Complexity Tracking table not required.

## Project Structure

### Documentation (this feature)

```text
specs/065-execution-surface-consolidation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── run-cli.md        # spectra run subcommand ⟷ MCP tool ⟷ engine mapping
├── checklists/
│   └── requirements.md  # spec quality checklist (already created by /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
  Spectra.Core/                      # unchanged
  Spectra.Execution/                 # NEW class library (refs Core + Microsoft.Data.Sqlite + ImageSharp)
    Execution/                       # MOVED from Spectra.MCP (namespaces preserved)
      ExecutionEngine.cs
      TestQueue.cs
      DependencyResolver.cs
      StateMachine.cs
      QueueReconstructionException.cs
    Storage/                         # MOVED from Spectra.MCP (namespaces preserved)
      ExecutionDb.cs                 # + WAL/busy_timeout PRAGMA at open
      RunRepository.cs
      ResultRepository.cs
      QueueSnapshotRepository.cs
    Identity/
      UserIdentityResolver.cs        # MOVED (namespace Spectra.MCP.Identity preserved)
    Infrastructure/
      McpConfig.cs                   # MOVED (namespace Spectra.MCP.Infrastructure preserved); McpLogging stays in MCP
    Screenshots/
      ScreenshotService.cs           # NEW shared service (encode + OS clipboard capture) — see research R5
  Spectra.MCP/                       # retained thin adapter — refs Spectra.Execution; tool handlers UNCHANGED
    Program.cs                       # unchanged wiring (types resolve from referenced assembly)
    Tools/...                        # screenshot tools refactored to delegate to ScreenshotService (behavior-preserving)
    Infrastructure/McpLogging.cs     # stays
  Spectra.CLI/                       # refs Spectra.Execution (added)
    Commands/Run/
      RunCommand.cs                  # the `run` command group
      RunServices.cs                 # builds engine + loaders (mirrors MCP Program.cs:38–130)
      *Handler.cs                    # one handler per subcommand (DeleteHandler-style: verbosity/outputFormat)
    Skills/Content/Skills/
      spectra-execute.md             # NEW CLI execution-loop SKILL
    Skills/Content/Agents/
      spectra-execution.agent.md     # re-pointed at `spectra run …` (CLI), MCP table retained as optional

tests/
  Spectra.Execution.Tests/          # NEW — relocated-assembly smoke + WAL/concurrency (FR-004)
  Spectra.CLI.Tests/Commands/Run/   # NEW — parity vs engine, guardrails, single-install smoke
  Spectra.MCP.Tests/                # UNCHANGED (proves behavior preserved)
```

**Structure Decision**: Multi-project. A new `Spectra.Execution` **class library** is the structural change: it is the only way for two executables (`Spectra.CLI`, `Spectra.MCP`) to share one engine without either referencing the other's executable. The relocation is mechanical (move files, preserve namespaces, add project references); the CLI gains a thin command group; MCP is rewired by reference only.

## Complexity Tracking

> No Constitution violations — table intentionally omitted.
