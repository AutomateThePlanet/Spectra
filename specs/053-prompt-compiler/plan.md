# Implementation Plan: Prompt-compiler + generation handoff inversion

**Branch**: `053-prompt-compiler` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/053-prompt-compiler/spec.md`
**Conceptual ID**: Spec 052 (series 052–057 foundation; dir is 053 because 052 was taken)

## Summary

Invert the generation handoff. Today the CLI owns the whole generation act — it compiles a grounded prompt, calls the model itself at `GenerationAgent.cs:239`, then parses/validates/persists. This plan extracts the two model-free halves of that file into standalone, deterministic, unit-testable artifacts and exposes each as a CLI command:

1. **`PromptCompiler`** — relocates `BuildFullPrompt` (`GenerationAgent.cs:448`) into a model-free class that *refuses to emit* when a required input is missing (FR-002/003/004). Exposed as `spectra ai compile-prompt`.
2. **`GeneratedTestIngestor`** — relocates the parse pipeline (`ParseTestsFromResponse`/`ParseTestCase`, `:537`/`:678`) into a *fail-loud* boundary that validates agent content with the existing `TestValidator`, **drops the silent truncation-salvage** (`TryRepairTruncatedArray`, `:619`), and persists valid content through the unchanged `TestPersistenceService` (FR-005/006/008). Exposed as `spectra ai ingest-tests`.

The existing `BuildFullPrompt` becomes a thin delegate to `PromptCompiler` so there is a single source of prompt truth (proving the relocation while keeping the existing test corpus green). The bounded retry (FR-007) is **skill/agent choreography, not C#** — its only C# contract is that the ingestor returns a specific, machine-readable error a skill can act on; that contract is what we build and test here.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine (CLI), System.Text.Json (parse), existing `Spectra.Core` (validation/index), `TestPersistenceService` (persist). No new dependencies.
**Storage**: File-based — `test-cases/{suite}/*.md` + `_index.json`, unchanged. Compiler writes nothing; ingestor writes only via `TestPersistenceService`.
**Testing**: xUnit. Net-new tests are token-free (no model, no network) — pure-function compiler + ingestor tests.
**Target Platform**: cross-platform CLI (win32 primary dev).
**Project Type**: single-solution CLI + shared core library.
**Performance Goals**: not latency-bound; the guarantees are categorical (zero model calls, byte-identical determinism, zero persistence on invalid input).
**Constraints**: MUST NOT modify `Spectra.Core` tests or `TestPersistenceService` tests (hard regression net). Compiler MUST be deterministic (no timestamps/random in the prompt). Ingestor MUST persist nothing on any validation failure.
**Scale/Scope**: 2 new production classes + result types, 2 new CLI commands, 1 delegation edit to `GenerationAgent`, ~3 new test files. No data-model change.

### Scoping decision (recorded — read before implementing)

**FR-001 (delete the `:239` model call + rewire the three handler entry paths) is delivered as relocation + delegation, NOT as call-site deletion in this pass.** Rationale:
- `GenerateHandler.cs` (~2500 lines) drives three generation entry paths (`ExecuteDirectModeAsync`, `ExecuteInteractiveModeAsync`, from-description) plus critic integration (critic = Spec 054, out of scope). Deleting `GenerateTestsAsync` outright forces rewriting CLI integration tests that the spec's regression discipline says to treat with care.
- The orchestration skill that *invokes* the new compile→ingest path is **Spec 055**, not this spec ("this spec delivers the CLI surface the skill will call, not the skill itself").
- Constitution §V (Simplicity / don't break working code) + the spec's hard regression rule both favour an additive, green increment over a risky big-bang deletion.

What this pass DOES guarantee for FR-001: the **new** path (`compile-prompt` → agent → `ingest-tests`) contains **zero** model calls, and prompt compilation has a **single** model-free source of truth (`PromptCompiler`, which `BuildFullPrompt` now delegates to). The literal deletion of `:239` and the handler rewire is captured as the final, explicitly-flagged task and is the natural seam Spec 055 completes when it wires the skill. This is called out in the final report, not buried.

## Constitution Check

*GATE: evaluated against `.specify/memory/constitution.md` v1.1.0.*

| Principle | Assessment |
|-----------|------------|
| I. GitHub as source of truth | ✅ No new storage. Tests still land as `.md` + `_index.json` via `TestPersistenceService`. |
| II. Deterministic execution | ✅ Strengthened. Compiler is deterministic by FR-003; ingestor is fail-loud, no silent repair. |
| III. Orchestrator-agnostic | ✅ Improved. Removing the in-CLI model call makes generation orchestrator-driven (the skill); the CLI surface is plain commands with deterministic exit codes. |
| IV. CLI-first | ✅ Both new capabilities are named CLI commands with explicit params and exit codes; the agent never writes files directly — persistence goes through the validated `TestPersistenceService`. |
| V. Simplicity (YAGNI) | ✅ Relocation, not rewrite. No new abstractions beyond the two result records the contract needs. The scoping decision above explicitly avoids premature big-bang deletion. |

**Quality gates**: `spectra validate` semantics unchanged — the ingestor validates with the same `TestValidator` before persisting, so the gates (schema, ID uniqueness via persistence/index, priority enum) are upheld at the boundary. No violations to record in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/053-prompt-compiler/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   ├── compile-prompt.md   # CLI command contract
│   └── ingest-tests.md     # CLI command contract
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Generation/                        # NEW — model-free generation seam
│   ├── PromptCompiler.cs              # NEW — relocated BuildFullPrompt + refuse-to-emit
│   ├── PromptCompileResult.cs         # NEW — success(prompt) | failure(missing input)
│   ├── GeneratedTestIngestor.cs       # NEW — relocated parse pipeline, fail-loud + persist
│   └── IngestResult.cs                # NEW — success(persisted) | failure(specific error)
├── Commands/Generate/
│   ├── CompilePromptCommand.cs        # NEW — `spectra ai compile-prompt`
│   ├── IngestTestsCommand.cs          # NEW — `spectra ai ingest-tests`
│   └── GenerateHandler.cs             # (later task) call-site removal / rewire
├── Commands/Ai/AiCommand.cs           # EDIT — register the two new commands
└── Agent/Copilot/GenerationAgent.cs   # EDIT — BuildFullPrompt delegates to PromptCompiler

tests/Spectra.CLI.Tests/Generation/
├── PromptCompilerTests.cs             # NEW — determinism + refuse-to-emit (token-free)
├── GeneratedTestIngestorTests.cs      # NEW — fail-loud + zero-persistence + happy path
└── CompileIngestCommandTests.cs       # NEW — CLI exit-code contract (optional, if time)
```

**Structure Decision**: Single-project CLI. New code lives in a new `src/Spectra.CLI/Generation/` namespace — deliberately *outside* `Agent/Copilot/` so the model-free seam no longer sits next to the SDK-coupled code (directly addresses investigation finding `03` F-2: "`BuildFullPrompt` is model-free but Copilot-coupled by location"). Reused-verbatim machinery (`TestPersistenceService`, `Spectra.Core` validators/index/parsers) is referenced, never modified.

## Complexity Tracking

No constitution violations. Table intentionally empty.
