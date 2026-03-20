# Implementation Plan: Smart Test Selection

**Branch**: `010-smart-test-selection` | **Date**: 2026-03-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-smart-test-selection/spec.md`

## Summary

Add cross-suite test discovery, execution history, and saved selections to the MCP server via three new deterministic tools (`find_test_cases`, `get_test_execution_history`, `list_saved_selections`) and extend `start_execution_run` with `test_ids` and `selection` modes. Add optional `description` field to test metadata. Update agent prompt for smart selection workflows. All MCP tools remain purely deterministic — intelligence lives in the orchestrator layer.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (serialization), Microsoft.Data.Sqlite (execution DB), YamlDotNet (frontmatter parsing)
**Storage**: SQLite (`.execution/spectra.db` for execution history), JSON files (`_index.json` per suite), `spectra.config.json`
**Testing**: xUnit with real SQLite databases in temp directories, JSON response parsing assertions
**Target Platform**: Cross-platform CLI / MCP stdio server
**Project Type**: CLI + MCP server (library + tools)
**Performance Goals**: < 2 seconds for 500 tests across 20 suites
**Constraints**: No AI/LLM calls in MCP tools, deterministic filtering only
**Scale/Scope**: Up to 500 test cases across 20 suites

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All data from `_index.json` files, `spectra.config.json`, and committed SQLite DB |
| II. Deterministic Execution | PASS | All new tools are pure C# filtering/querying — no AI calls. Same inputs produce same outputs. |
| III. Orchestrator-Agnostic Design | PASS | Tools return self-contained JSON responses. Smart selection logic in agent prompt, not tools. |
| IV. CLI-First Interface | PASS | MCP tools are the interface. No chat loops. Batch-compatible. |
| V. Simplicity (YAGNI) | PASS | Simple keyword matching (no stemming/fuzzy). Reuses existing patterns (IMcpTool, McpToolResponse, repository). No new abstractions. |

**Post-Design Re-Check**: All principles still satisfied. No violations detected.

## Project Structure

### Documentation (this feature)

```text
specs/010-smart-test-selection/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── mcp-tools.md
└── tasks.md
```

### Source Code (repository root)

```text
src/Spectra.MCP/
├── Tools/
│   └── Data/
│       ├── FindTestCasesTool.cs          # NEW — cross-suite search/filter
│       ├── GetTestExecutionHistoryTool.cs # NEW — per-test execution stats
│       └── ListSavedSelectionsTool.cs    # NEW — config-based selections
├── Tools/RunManagement/
│   └── StartExecutionRunTool.cs          # MODIFIED — add test_ids + selection modes
├── Storage/
│   └── ResultRepository.cs              # MODIFIED — add history aggregation queries
└── Program.cs                           # MODIFIED — register new tools, add loaders

src/Spectra.Core/
├── Models/
│   ├── TestCaseFrontmatter.cs           # MODIFIED — add Description field
│   ├── TestCase.cs                      # MODIFIED — add Description field
│   ├── TestIndexEntry.cs                # MODIFIED — add Description, EstimatedDuration fields
│   └── Config/
│       ├── SpectraConfig.cs             # MODIFIED — add Selections property
│       └── SavedSelectionConfig.cs      # NEW — selection config model
├── Parsing/
│   └── TestCaseParser.cs               # MODIFIED — wire Description field

src/Spectra.CLI/
├── Commands/Init/
│   └── InitHandler.cs                  # MODIFIED — add selections to default config
└── Agent/Resources/
    └── spectra-execution.agent.md      # MODIFIED — add smart selection section

tests/Spectra.MCP.Tests/
├── Tools/Data/
│   ├── FindTestCasesTests.cs            # NEW
│   ├── GetTestExecutionHistoryTests.cs  # NEW
│   └── ListSavedSelectionsTests.cs      # NEW
├── Tools/
│   └── StartExecutionRunTests.cs        # MODIFIED — add test_ids/selection tests
└── Integration/
    └── SmartSelectionFlowTests.cs       # NEW — end-to-end selection → execution
```

**Structure Decision**: Follows existing project structure. New tools go in `Tools/Data/` (alongside existing `ValidateTestsTool`, `RebuildIndexesTool`, `AnalyzeCoverageGapsTool`). Config model goes in `Spectra.Core/Models/Config/`. No new projects or abstractions.

## Complexity Tracking

No violations. All changes follow existing patterns.
