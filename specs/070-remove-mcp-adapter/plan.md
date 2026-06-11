# Implementation Plan: Remove the Spectra.MCP Execution Adapter

**Branch**: `070-remove-mcp-adapter` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/070-remove-mcp-adapter/spec.md`

## Summary

Remove SPECTRA's own MCP execution adapter (`Spectra.MCP`) and converge on the single CLI execution
surface (`spectra run`). The execution engine (`Spectra.Execution`) is already transport-neutral and is
**not touched**. Work is: delete the `Spectra.MCP` project + its packaging + its solution entry; stop
`spectra init` emitting MCP wiring (`.vscode/mcp.json`, `mcp__spectra__*` allowlist); re-home the
execution skill/agent to `spectra run` only; **preserve all behavior coverage** by relocating the
engine-level tests that currently live under `tests/Spectra.MCP.Tests/` into `Spectra.Execution.Tests`
and porting the engine-substance of the adapter/integration tests to engine/CLI tests before retiring the
transport-only ones; re-point `tests/Spectra.Integration.Tests` off the MCP tools; and update the two
`ARCHITECTURE-v2.md` copies. The canary (`Spectra.Core`, `TestPersistenceService`, `Spectra.Execution`
engine tests) stays green and unmodified throughout.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, Spectre.Console, Microsoft.Data.Sqlite, SixLabors.ImageSharp (screenshot encode, lives in `Spectra.Execution`), xUnit
**Storage**: SQLite (`.execution/spectra.db`, WAL) — unchanged; files (test-cases/, docs/, configs) — unchanged
**Testing**: xUnit across `Spectra.Core.Tests`, `Spectra.CLI.Tests`, `Spectra.Execution.Tests`, `Spectra.Integration.Tests` (and the soon-removed `Spectra.MCP.Tests`)
**Target Platform**: Cross-platform .NET CLI tool (`spectra`); primary dev OS Windows 11
**Project Type**: Multi-project .NET solution (CLI app + Core/Execution libraries + test projects)
**Performance Goals**: N/A — this is a transport removal; no runtime perf target changes
**Constraints**: No change to engine behavior, state model, or SQLite schema. No coverage regression. Canary projects unmodified.
**Scale/Scope**: 1 project deleted (`Spectra.MCP`, ~40 source files), 1 solution edit, 2 init installers removed, 1 skill + 1 agent edited, ~50 test files triaged (relocate / port / retire), 1 integration-test harness re-pointed, 2 docs updated.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|-----------|---------|-------|
| I. GitHub as Source of Truth | ✅ Unaffected | No change to file-based storage / indexes. |
| II. Deterministic Execution | ✅ Upheld + amended | The deterministic state-machine engine is **unchanged**. The principle's MCP-specific wording was amended (constitution 1.1.0 → 2.0.0) to describe the engine + `spectra run` CLI with durable, reconstructable state. The determinism guarantee is preserved by the untouched `Spectra.Execution` engine. |
| III. Orchestrator-Agnostic Design | ✅ Upheld + amended | Redefined (constitution 2.0.0) around the model-free, command-driven CLI surface — the MCP-API and provider-chain/BYOK clauses (already overtaken by 058/059/069 and removed here) were retired. The agnostic intent survives: any orchestrator can shell out to `spectra …`. |
| IV. CLI-First Interface | ✅ Strongly reinforced | Converging on a single CLI execution surface is the purest expression of CLI-First. |
| V. Simplicity (YAGNI) | ✅ Strongly reinforced | Removes a redundant second surface and the "no backwards-compat shims when code can change directly" principle directly endorses deleting the adapter. |

**Gate result**: PASS. No blocking violations. The stale MCP/provider wording in Principles II/III was
amended in this work (constitution 1.1.0 → 2.0.0) rather than deferred. No new project, dependency, or
abstraction is introduced — the opposite: one project is removed.

## Project Structure

### Documentation (this feature)

```text
specs/070-remove-mcp-adapter/
├── plan.md              # This file
├── research.md          # Phase 0 — test-mapping table + re-point strategy + decisions
├── data-model.md        # Phase 1 — entity/artifact inventory (no runtime data model change)
├── quickstart.md        # Phase 1 — manual verification (clean lifecycle + init check)
├── contracts/
│   └── removed-mcp-tools.md   # The 25 MCP tool schemas being retired + their CLI/engine successor
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created by /speckit.specify)
└── tasks.md             # Phase 2 — /speckit.tasks output
```

### Source Code (repository root)

```text
src/
├── Spectra.MCP/                      # DELETED in full (Server/, Tools/, Program.cs, Infrastructure/McpLogging.cs, .csproj)
├── Spectra.Execution/                # UNTOUCHED — engine, storage, reports, screenshots, McpConfig (cosmetic ns)
├── Spectra.Core/                     # UNTOUCHED
└── Spectra.CLI/
    ├── Commands/Init/InitHandler.cs          # EDIT — drop .vscode/mcp.json + allowlist emission + log lines
    ├── Skills/VsCodeMcpConfigInstaller.cs    # DELETED
    ├── Skills/ClaudeSettingsInstaller.cs     # DELETED (MCP allowlist installer) — verify no non-MCP use
    ├── Skills/Content/Skills/spectra-execute.md          # EDIT — CLI-only, no MCP path
    └── Skills/Content/Agents/spectra-execution.agent.md  # EDIT — remove MCP fallback note (keep SUT-MCP)

tests/
├── Spectra.MCP.Tests/                # DELETED after triage (relocate/port/retire all files; remove from slnx)
├── Spectra.Execution.Tests/          # GROWS — receives relocated engine tests + ported engine-flow tests
├── Spectra.Integration.Tests/        # EDIT — re-point IntegrationWorkspace off MCP tools to engine/CLI
└── Spectra.CLI.Tests/                # EDIT — skill/agent assertions for "no MCP path"; receive any CLI-level ports

Spectra.slnx                          # EDIT — remove Spectra.MCP and Spectra.MCP.Tests entries
docs/architecture/ARCHITECTURE-v2.md  # EDIT — drop "Execution → MCP, only here" / "Do not unify"
docs/specs/ARCHITECTURE-v2.md         # EDIT — same (duplicate copy)
```

**Structure Decision**: No new projects. `Spectra.MCP` and `Spectra.MCP.Tests` are removed from the
solution. The engine-level tests under `Spectra.MCP.Tests` move into the existing `Spectra.Execution.Tests`
(their `using Spectra.MCP.Execution/.Storage/...` directives keep resolving because those cosmetic
namespaces live inside `Spectra.Execution` — zero `using` edits on relocation). The cosmetic
`Spectra.MCP.*` namespace rename is **out of scope** (FR-017).

## Test Strategy (the heart of this feature)

Disposition rules (see `research.md` for the full per-file table that satisfies FR-011):

1. **RELOCATE** — files that touch only `Spectra.Execution` types (no `Spectra.MCP.Tools.*` / `Spectra.MCP.Server.*`): move verbatim into `Spectra.Execution.Tests`. (`Execution/`, `Storage/`, `Models/`, `Helpers/`, `Reports/`, and the two engine-direct `Integration/` files.)
2. **PORT** — adapter/integration tests whose *substance* is engine behavior not otherwise covered (skip-cascade, bulk-record, pause/resume, cancel, cancel-all, finalize+report, status, details, history, summary, list-active/suites, smart-selection flow, retest-across-process): rewrite to call `ExecutionEngine`/repositories directly (drop the `*Tool.ExecuteAsync(json)` + JSON-shape layer) and place in `Spectra.Execution.Tests`. A handful with a natural CLI home (`spectra run` subcommands) may port to `Spectra.CLI.Tests/Commands/Run/` instead.
3. **RETIRE** — tests asserting only transport concerns that vanish with the adapter: `McpProtocolStrictTests` (JSON unknown-field strictness), `ReconstructionErrorSurfaceTests` (engine→`RECONSTRUCTION_FAILED` *error-code* mapping in `ToolRegistry`; the underlying fail-loud is covered by the relocated `ReconstructionFailLoudTests`), `FindTestCasesActionableErrorTests` (tool param validation), `ActiveRunResolverTests` (helper class being deleted). Each retired file gets a mapping-table row with its justification + the surviving equivalent.
4. **Data-tool tests** (`Tools/Data/*`): triage individually — port the operation-level behavior to its CLI command test (`spectra validate`, index rebuild, `ai analyze --coverage`, find) where the behavior is real and uncovered; retire the parts that only assert tool-JSON/param shape.

Deletion of any `Spectra.MCP.Tests` file is permitted only once its mapping-table row names a relocated /
ported / existing equivalent (or a justified transport-only retirement).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Constitution Principles II/III amended within this feature (1.1.0 → 2.0.0) | The MCP/provider wording was directly falsified by this removal and the v2 series; leaving it stale would let the governing document contradict the shipped code | Deferring to a separate PR was the initial plan, but the maintainer chose to amend now so the constitution and code land consistent. Amendment Procedure followed: MAJOR bump (principle redefinition), Last Amended updated, dependent templates re-checked (Compatible). |
