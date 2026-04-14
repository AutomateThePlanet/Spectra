# Implementation Plan: Coverage-Aware Behavior Analysis

**Branch**: `044-coverage-aware-analysis` | **Date**: 2026-04-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/044-coverage-aware-analysis/spec.md`

## Summary

The behavior analysis step in `spectra ai generate` currently treats documentation as a blank slate, identifying all testable behaviors and applying a weak title-similarity heuristic for deduplication. For mature suites this produces wildly inaccurate recommendations (139 new tests when 8 are actually needed). This plan adds a coverage snapshot that feeds existing test index, criteria coverage, and doc section coverage into the analysis prompt so the AI only recommends tests for genuine gaps.

## Technical Context

**Language/Version**: C# 12, .NET 8+  
**Primary Dependencies**: GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet  
**Storage**: File-based (Markdown/YAML/JSON in `test-cases/`, `docs/criteria/`, `_index.json`, `_criteria_index.yaml`)  
**Testing**: xUnit (~1279 tests across 3 test projects)  
**Target Platform**: Windows/macOS/Linux CLI  
**Project Type**: CLI tool with MCP server  
**Performance Goals**: Coverage snapshot computation < 2s for 500 tests  
**Constraints**: Prompt token overhead < 5,000 tokens; switch to summary mode > 500 tests  
**Scale/Scope**: Suites up to 500+ tests, 50+ criteria, 20+ doc sections

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All data read from committed files (`_index.json`, `.criteria.yaml`, `_index.md`) |
| II. Deterministic Execution | PASS | Same coverage data + same docs = same snapshot; no randomness in snapshot building |
| III. Orchestrator-Agnostic Design | PASS | Coverage context is injected into the prompt template, not tied to a specific LLM |
| IV. CLI-First Interface | PASS | Feature is CLI-only; no new UI required; existing flags preserved |
| V. Simplicity (YAGNI) | PASS | One new model + one builder service + prompt template changes. No new abstractions beyond what's needed. Reuses existing `IndexWriter`, `CriteriaFileReader`, `DocumentIndexReader` |

**Quality Gates**: All existing gates preserved. No new validation rules needed.

## Project Structure

### Documentation (this feature)

```text
specs/044-coverage-aware-analysis/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── coverage-context-contract.md
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
  Spectra.CLI/
    Agent/
      Analysis/
        BehaviorAnalysisResult.cs      # Existing
        IdentifiedBehavior.cs          # Existing
        CoverageSnapshot.cs            # NEW: model + UncoveredCriterion record
        CoverageSnapshotBuilder.cs     # NEW: builds snapshot from index/criteria/docs
        CoverageContextFormatter.cs    # NEW: formats snapshot for prompt injection
      Copilot/
        BehaviorAnalyzer.cs            # MODIFIED: accept snapshot, inject context
    Commands/
      Generate/
        GenerateHandler.cs             # MODIFIED: build snapshot before analysis
    Output/
        AnalysisPresenter.cs           # MODIFIED: show coverage summary
    Progress/
        ProgressPageWriter.cs          # MODIFIED: coverage snapshot in HTML
    Prompts/
      Content/
        behavior-analysis.md           # MODIFIED: add {{coverage_context}} placeholder
    Results/
        GenerateResult.cs              # MODIFIED: add coverage fields to GenerateAnalysis

  Spectra.Core/
    # No changes — read existing data via existing readers

tests/
  Spectra.CLI.Tests/
    Agent/
      Analysis/
        CoverageSnapshotBuilderTests.cs  # NEW: 8 tests
        CoverageContextFormatterTests.cs # NEW: 5 tests
    Commands/
      Generate/
        GenerateHandlerCoverageTests.cs  # NEW: 2 integration tests
    Output/
        AnalysisPresenterCoverageTests.cs # NEW: 3 tests
    Results/
        GenerateAnalysisCoverageTests.cs  # NEW: 3 tests
```

**Structure Decision**: All new files go within the existing `Agent/Analysis/` directory (model + builder + formatter). No new project or directory structure needed — follows existing patterns exactly.

## Complexity Tracking

> No violations — all gates pass. No complexity justification needed.
