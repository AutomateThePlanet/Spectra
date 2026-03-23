# Implementation Plan: Smart Test Count Recommendation

**Branch**: `019-smart-test-count` | **Date**: 2026-03-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/019-smart-test-count/spec.md`

## Summary

When `--count` is omitted from `spectra ai generate`, the system performs a lightweight AI-powered pre-scan of source documentation to identify and categorize all distinct testable behaviors. It displays a categorized breakdown (happy path, negative, edge case, security, performance), deducts already-covered tests, and either auto-generates all (non-interactive) or presents an interactive selection menu. Post-generation, a gap notification shows remaining uncovered behaviors.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK (AI runtime), Spectre.Console (terminal UX), System.Text.Json (JSON parsing)
**Storage**: File system (test markdown files, `_index.json`)
**Testing**: xUnit with structured results
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool
**Performance Goals**: Analysis step completes within 5 seconds (single lightweight AI call)
**Constraints**: Analysis uses same AI provider as generation; no additional API keys or dependencies
**Scale/Scope**: Affects `spectra ai generate` command only — both direct and interactive modes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Analysis reads from docs/ and tests/ in Git repo; no external storage |
| II. Deterministic Execution | PASS | Same docs + same tests = same analysis breakdown; count is deterministic input to generation |
| III. Orchestrator-Agnostic | PASS | Uses same Copilot SDK provider chain; no new vendor dependency |
| IV. CLI-First Interface | PASS | `--count` preserves existing behavior; analysis is CLI-native with `--no-interaction` support |
| V. Simplicity (YAGNI) | PASS | Single new service (BehaviorAnalyzer) with one AI call; reuses existing SourceDocumentLoader, DuplicateDetector, and Spectre.Console patterns |

**Quality Gates**: No new quality gates needed. Existing `spectra validate` gates unaffected.

## Project Structure

### Documentation (this feature)

```text
specs/019-smart-test-count/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/Generate/
│   │   ├── GenerateCommand.cs          # No changes (--count already optional)
│   │   └── GenerateHandler.cs          # Modified: add analysis step when count is null
│   ├── Agent/
│   │   ├── Copilot/
│   │   │   └── BehaviorAnalyzer.cs     # NEW: AI-powered behavior analysis
│   │   └── Analysis/
│   │       ├── BehaviorAnalysisResult.cs # NEW: Analysis result model
│   │       └── IdentifiedBehavior.cs    # NEW: Single behavior model
│   ├── Interactive/
│   │   └── CountSelector.cs            # NEW: Interactive count selection menu
│   └── Output/
│       └── AnalysisPresenter.cs        # NEW: Spectre.Console breakdown display
└── Spectra.Core/
    └── Models/
        └── BehaviorCategory.cs         # NEW: Category enum

tests/
├── Spectra.CLI.Tests/
│   ├── Agent/
│   │   └── BehaviorAnalyzerTests.cs    # NEW: Analysis parsing and dedup tests
│   ├── Interactive/
│   │   └── CountSelectorTests.cs       # NEW: Menu option generation tests
│   └── Output/
│       └── AnalysisPresenterTests.cs   # NEW: Display formatting tests
```

**Structure Decision**: Follows existing CLI project structure. New files placed in existing directories matching their responsibility (Agent/ for AI calls, Interactive/ for UX, Output/ for display). One new model in Core for the shared enum.

## Complexity Tracking

No constitution violations — no entries needed.
