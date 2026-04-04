# Implementation Plan: CLI Non-Interactive Mode and Structured Output

**Branch**: `020-cli-non-interactive` | **Date**: 2026-04-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/020-cli-non-interactive/spec.md`

## Summary

Refactor all SPECTRA CLI commands to support non-interactive execution and structured JSON output. Add `--output-format` and `--no-interaction` global options, standardize exit codes, and introduce a `CommandResult<T>` pattern so every handler can produce either human-friendly Spectre.Console output or clean JSON on stdout. Existing interactive behavior is preserved as the default.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (terminal UX), System.Text.Json (serialization)
**Storage**: N/A (no storage changes)
**Testing**: xUnit (existing 1048+ tests across 3 test projects)
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool
**Performance Goals**: Exit code 3 returned within 1 second for missing args
**Constraints**: Backward compatibility — all existing interactive workflows must continue unchanged
**Scale/Scope**: ~12 command handlers to update

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | No storage changes |
| II. Deterministic Execution | PASS | Same inputs → same outputs; JSON output is deterministic |
| III. Orchestrator-Agnostic Design | PASS | JSON output enables any orchestrator to parse results |
| IV. CLI-First Interface | PASS | This feature directly strengthens CLI-first by adding CI-friendly exit codes and structured output |
| V. Simplicity (YAGNI) | PASS | Leveraging existing VerbosityLevel enum, existing ExitCodes class, existing GlobalOptions pattern |

All gates pass. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/020-cli-non-interactive/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (JSON result models)
├── contracts/           # Phase 1 output (JSON schemas per command)
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Options/
│   └── GlobalOptions.cs          # MODIFY: Add --output-format, --no-interaction global options
├── Infrastructure/
│   ├── ExitCodes.cs              # MODIFY: Add MissingArguments = 3
│   ├── VerbosityLevel.cs         # EXISTS: Already has Quiet/Normal levels (keep as-is)
│   └── OutputFormat.cs           # NEW: OutputFormat enum (Human, Json)
├── Output/
│   ├── ProgressReporter.cs       # MODIFY: Suppress output when OutputFormat.Json
│   ├── ResultPresenter.cs        # MODIFY: Suppress output when OutputFormat.Json
│   ├── JsonResultWriter.cs       # NEW: Serialize CommandResult<T> to stdout as JSON
│   └── NextStepHints.cs          # MODIFY: Suppress when OutputFormat.Json
├── Results/                      # NEW: Typed result models per command
│   ├── CommandResult.cs          # Base result with command, status, timestamp
│   ├── GenerateResult.cs         # Generate-specific fields
│   ├── AnalyzeCoverageResult.cs  # Coverage analysis fields
│   ├── ValidateResult.cs         # Validation result fields
│   ├── DashboardResult.cs        # Dashboard generation fields
│   ├── ListResult.cs             # Suite listing fields
│   ├── ShowResult.cs             # Test detail fields
│   ├── InitResult.cs             # Init result fields
│   └── DocsIndexResult.cs        # Docs index result fields
├── Commands/
│   ├── Ai/Generate/
│   │   ├── GenerateCommand.cs    # MODIFY: Remove local --no-interaction, use global
│   │   └── GenerateHandler.cs    # MODIFY: Build GenerateResult, conditionally output JSON
│   ├── Ai/Analyze/
│   │   ├── AnalyzeCommand.cs     # MODIFY: Remove local --format, use global --output-format
│   │   └── AnalyzeHandler.cs     # MODIFY: Build AnalyzeCoverageResult for JSON mode
│   ├── Ai/Update/
│   │   └── UpdateHandler.cs      # MODIFY: Build result, JSON output
│   ├── Init/
│   │   └── InitHandler.cs        # MODIFY: Build InitResult, JSON output
│   ├── Validate/
│   │   └── ValidateHandler.cs    # MODIFY: Build ValidateResult, JSON output
│   ├── Dashboard/
│   │   └── DashboardHandler.cs   # MODIFY: Build DashboardResult, JSON output
│   ├── Docs/
│   │   └── DocsIndexHandler.cs   # MODIFY: Build DocsIndexResult, JSON output
│   └── [List, Show, Config, Index, Auth, Profile handlers] # MODIFY: Similar pattern
```

**Structure Decision**: Extends existing project structure. New `Results/` directory for typed result models. New `JsonResultWriter` utility in `Output/`. All changes within `Spectra.CLI` project — no new projects needed.

## Complexity Tracking

No constitution violations — table not needed.
