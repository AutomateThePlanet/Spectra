# Implementation Plan: Conversational Test Generation

**Branch**: `006-conversational-generation` | **Date**: 2026-03-18 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/006-conversational-generation/spec.md`

## Summary

Refactor SPECTRA test generation to be conversational by default with two modes: **Direct** (describe what you want via flags, AI executes autonomously) and **Interactive** (AI guides you step by step). Both modes write tests directly to disk without a review/accept step. Git is the review tool.

Key changes:
- `spectra ai generate` enters interactive mode when no `--suite` argument
- `spectra ai generate --suite X --focus Y` runs in direct mode
- `spectra ai update` follows the same pattern
- Rich terminal UX with Spectre.Console (symbols, colors, tables, spinners)
- Coverage gap analysis before and after generation
- Test classification (up-to-date, outdated, orphaned, redundant) for updates

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (terminal UX), Microsoft.Extensions.AI (AI tools)
**Storage**: File system (tests/{suite}/*.md), JSON indexes (_index.json)
**Testing**: xUnit with test fixtures
**Target Platform**: Cross-platform CLI (Windows, macOS, Linux)
**Project Type**: CLI application
**Performance Goals**: Direct mode < 60s, Interactive mode < 3 min, prompt response < 100ms
**Constraints**: CI mode must complete without stdin reads, exit codes for automation
**Scale/Scope**: Typical suite has 10-100 tests, typical documentation set has 5-20 files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | ✅ PASS | Tests written directly to disk as Markdown files, indexes committed to Git |
| II. Deterministic Execution | ✅ PASS | Same inputs (suite + focus) produce same generation flow; no hidden state |
| III. Orchestrator-Agnostic Design | ✅ PASS | CLI-based, works with any AI provider via existing agent chain |
| IV. CLI-First Interface | ✅ PASS | All functionality via CLI commands with explicit parameters; `--no-interaction` for CI |
| V. Simplicity (YAGNI) | ✅ PASS | Two modes (direct/interactive) with clear triggers; no unnecessary abstractions |

**Post-Design Verification (Phase 1)**: ✅ All principles still satisfied. Design artifacts (data-model.md, contracts/, quickstart.md) maintain:
- File-based storage with Git as source of truth
- CLI-first interface with clear command contracts
- No unnecessary abstractions (reuses existing Spectre.Console patterns)

**Quality Gates:**
- Schema Validation: Tests will have valid YAML frontmatter (enforced by existing TestFileWriter)
- ID Uniqueness: Duplicate checking before generation (existing CheckDuplicatesBatchTool)
- Index Currency: Index updated after each generation (existing IndexGenerator)
- Dependency Resolution: depends_on validated (existing validation)
- Priority Enum: Enforced by TestCase model

## Project Structure

### Documentation (this feature)

```text
specs/006-conversational-generation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Ai/
│   │   │   ├── AiCommand.cs           # Parent AI command (exists)
│   │   │   └── Generate/
│   │   │       ├── GenerateCommand.cs  # Modified: optional suite arg
│   │   │       └── GenerateHandler.cs  # Modified: mode detection + flows
│   │   │   └── Update/
│   │   │       ├── UpdateCommand.cs    # Modified: optional suite arg
│   │   │       └── UpdateHandler.cs    # New/Modified: update flow
│   ├── Interactive/                    # NEW: Interactive mode components
│   │   ├── SuiteSelector.cs           # Suite selection prompt
│   │   ├── TestTypeSelector.cs        # Test type selection prompt
│   │   ├── FocusDescriptor.cs         # Free-text focus input
│   │   ├── GapSelector.cs             # Gap selection for follow-up
│   │   └── InteractiveSession.cs      # State machine for flow
│   ├── Coverage/                       # NEW/Modified: Gap analysis
│   │   ├── GapAnalyzer.cs             # Coverage gap detection
│   │   └── GapPresenter.cs            # Gap display formatting
│   ├── Classification/                 # NEW: Test classification
│   │   ├── TestClassifier.cs          # up-to-date/outdated/orphaned/redundant
│   │   └── ClassificationPresenter.cs # Classification display
│   ├── Output/                         # NEW: Rich output formatting
│   │   ├── ProgressReporter.cs        # Spinners, progress
│   │   └── ResultPresenter.cs         # Tables, summaries
│   └── Review/                         # Exists: reuse patterns
│       └── ReviewPresenter.cs          # Reference for Spectre.Console
├── Spectra.Core/
│   └── Models/
│       └── CoverageGap.cs             # Gap model (may exist)
│       └── TestClassification.cs      # NEW: Classification enum

tests/
├── Spectra.CLI.Tests/
│   ├── Commands/Generate/             # New tests for modes
│   ├── Interactive/                   # Interactive component tests
│   └── Classification/                # Classification tests
```

**Structure Decision**: Extends existing CLI structure with new Interactive/, Coverage/, Classification/, and Output/ namespaces for clean separation. Reuses existing patterns from Review/ namespace.

## Complexity Tracking

> No Constitution violations. Feature aligns with all principles.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | N/A | N/A |
