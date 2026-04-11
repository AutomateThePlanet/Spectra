# Implementation Plan: Token Usage Tracking & Model/Provider Logging

**Branch**: `040-token-usage-tracking` | **Date**: 2026-04-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/040-token-usage-tracking/spec.md`

## Summary

Add cost/token visibility to Spectra. Three deliverables: (1) per-call token + model/provider data captured into a thread-safe `TokenUsageTracker` across all five AI phases (analysis, generation, critic, update, criteria); (2) a Run Summary panel rendered after `generate` and `update`, plus matching `run_summary` and `token_usage` fields in `--output-format json` and `.spectra-result.json`, plus a Token Usage section in `.spectra-progress.html`; (3) a new opt-in `debug` config section that turns off `.spectra-debug.log` by default and a `--verbosity diagnostic` flag that force-enables it. Cost estimation uses a hardcoded model→rate dictionary; `github-models` always reports "included in Copilot plan".

Technical approach: extend the existing static `DebugLogger` with an `Enabled` gate driven by the new `DebugConfig`, add `model`/`provider`/`tokens_in`/`tokens_out` to every AI debug line, introduce a new `TokenUsageTracker` singleton in `Spectra.CLI/Services/`, plumb it (plus elapsed `Stopwatch`) through `BehaviorAnalyzer`, `GenerationAgent`, `GroundingAgent` (critic), `UpdateHandler`/`TestUpdater`, and `CriteriaExtractor`. Render via Spectre.Console matching the existing technique-breakdown table style. Serialize via `System.Text.Json` records added to `Spectra.Core.Models` and surfaced through `Spectra.CLI/Results/GenerateResult`/`UpdateResult`. ProgressManager already writes `.spectra-result.json` incrementally; we extend its payload.

## Technical Context

**Language/Version**: C# 12 / .NET 8+
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json, GitHub Copilot SDK (sole AI runtime)
**Storage**: File system (`.spectra-debug.log`, `.spectra-result.json`, `.spectra-progress.html`); no DB
**Testing**: xUnit (`Spectra.Core.Tests`, `Spectra.CLI.Tests`)
**Target Platform**: Cross-platform CLI (Windows / macOS / Linux)
**Project Type**: Multi-project .NET solution (Spectra.Core, Spectra.CLI, Spectra.MCP)
**Performance Goals**: Token recording overhead must be negligible vs AI call latency (sub-millisecond per call). Debug log writes are append-only and best-effort; never block AI calls.
**Constraints**: Backwards-compatible — existing configs without a `debug` section must load; existing tests must keep passing without modification; no breaking changes to public CLI flags. Token tracking is purely additive.
**Scale/Scope**: ~20 files touched across Core + CLI + tests. ~16 new tests. One new config section, one new service, one new presenter, six handler integration points.

## Constitution Check

| Principle | Compliance |
|-----------|------------|
| I. GitHub as Source of Truth | PASS — no new external state. New `debug` config lives in `spectra.config.json` checked into Git. |
| II. Deterministic Execution | PASS — token tracking is observation-only; does not influence execution state. |
| III. Orchestrator-Agnostic Design | PASS — token capture works for every Copilot SDK provider. Cost rates are provider-aware (github-models special-cased). |
| IV. CLI-First Interface | PASS — feature is fully exposed via existing `generate`/`update` commands and `--verbosity`/`--output-format` flags. No GUI dependency. |
| V. Simplicity (YAGNI) | PASS — single tracker, single presenter, hardcoded rate table. Out-of-scope items (history DB, budget alerts, live pricing API) explicitly deferred. |

**Result**: All gates pass. No complexity-tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/040-token-usage-tracking/
├── plan.md              # This file
├── spec.md              # Feature spec
├── research.md          # Phase 0 — research notes
├── data-model.md        # Phase 1 — entities
├── quickstart.md        # Phase 1 — verify the feature end-to-end
├── contracts/
│   ├── debug-config.schema.json     # spectra.config.json `debug` section schema
│   ├── token-usage.schema.json      # token_usage JSON shape (output)
│   └── debug-log-format.md          # debug log line grammar (AI vs non-AI)
├── checklists/
│   └── requirements.md  # /speckit.specify checklist
└── tasks.md             # /speckit.tasks output
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/
│       ├── Config/
│       │   ├── SpectraConfig.cs              # +Debug property
│       │   └── DebugConfig.cs                # NEW
│       └── TokenUsage.cs                     # NEW (record: PromptTokens, CompletionTokens)
└── Spectra.CLI/
    ├── Infrastructure/
    │   └── DebugLogger.cs                    # MODIFY: default off, model/provider/tokens fields
    ├── Services/
    │   ├── TokenUsageTracker.cs              # NEW
    │   └── CostEstimator.cs                  # NEW (hardcoded rate table)
    ├── Agent/
    │   ├── IAgentRuntime.cs                  # MODIFY: TokenUsage record reuse / re-export
    │   └── Copilot/
    │       ├── BehaviorAnalyzer.cs           # MODIFY: record analysis tokens
    │       ├── GenerationAgent.cs            # MODIFY: record generation tokens per batch
    │       ├── GroundingAgent.cs             # MODIFY: record critic tokens per verdict
    │       ├── CriteriaExtractor.cs          # MODIFY: record criteria tokens
    │       └── ProviderMapping.cs            # MODIFY (if needed): expose canonical provider name
    ├── Commands/
    │   ├── Generate/GenerateHandler.cs       # MODIFY: build RunSummary, attach to result
    │   └── Update/UpdateHandler.cs           # MODIFY: build RunSummary, attach to result
    ├── Output/
    │   ├── RunSummaryPresenter.cs            # NEW (Spectre.Console panel + table)
    │   └── ProgressManager.cs                # MODIFY: write token_usage incrementally
    └── Results/
        ├── GenerateResult.cs                 # MODIFY: +RunSummary, +TokenUsageReport
        ├── UpdateResult.cs                   # MODIFY: +RunSummary, +TokenUsageReport
        ├── RunSummary.cs                     # NEW (run-context block)
        └── TokenUsageReport.cs               # NEW (phases + total + cost)

dashboard-site/
└── (no changes — progress page is .spectra-progress.html template inside CLI)

tests/
├── Spectra.Core.Tests/
│   └── Config/
│       └── DebugConfigTests.cs               # NEW
└── Spectra.CLI.Tests/
    ├── Infrastructure/
    │   └── DebugLoggerTests.cs               # NEW
    ├── Services/
    │   ├── TokenUsageTrackerTests.cs         # NEW
    │   └── CostEstimatorTests.cs             # NEW
    ├── Output/
    │   └── RunSummaryPresenterTests.cs       # NEW
    └── Results/
        └── GenerateResultTokenUsageTests.cs  # NEW
```

**Structure Decision**: Reuse the existing 3-project layout (Spectra.Core, Spectra.CLI, Spectra.MCP). All token tracking is CLI-side because the Copilot SDK and handlers live there; only the configuration record (`DebugConfig`) and the value type (`TokenUsage`) live in Core for sharing. No Spectra.MCP changes — MCP execution doesn't make AI calls.

## Complexity Tracking

No constitutional violations. Section intentionally empty.
