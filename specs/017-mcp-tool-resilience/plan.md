# Implementation Plan: MCP Tool Resilience for Weaker Models

**Branch**: `017-mcp-tool-resilience` | **Date**: 2026-03-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/017-mcp-tool-resilience/spec.md`

## Summary

Make all MCP tools resilient to weaker AI models (GPT-4.1, GPT-4o) that call tools with empty `{}` input by auto-resolving `run_id` and `test_handle` when omitted. Add `list_active_runs` and `cancel_all_active_runs` management tools. Enhance `get_run_history` with status filter and per-run summary counts.

The approach uses a shared `ActiveRunResolver` helper class that each tool calls during parameter validation — no middleware, no base class changes, minimal blast radius.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (serialization), Microsoft.Data.Sqlite (storage)
**Storage**: SQLite (`.execution/spectra.db`) — existing `runs` and `test_results` tables, no schema changes
**Testing**: xUnit, 306 existing MCP tests
**Target Platform**: Cross-platform CLI / MCP server
**Project Type**: MCP server (Spectra.MCP project)
**Performance Goals**: Auto-resolution adds at most 1 extra DB query per tool call
**Constraints**: Zero regressions on existing tests; backward-compatible for callers that provide explicit parameters
**Scale/Scope**: 13 tools need run_id changes, 7 need test_handle changes, 2 new tools, 1 enhanced tool

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | No changes to test files, docs, or config. Only MCP server runtime changes. |
| II. Deterministic Execution | PASS | Auto-resolution is deterministic: same DB state → same resolved parameters. State machine unchanged. |
| III. Orchestrator-Agnostic Design | PASS | This feature directly serves this principle — making tools work with weaker orchestrators. Responses remain self-contained with `next_expected_action`. |
| IV. CLI-First Interface | N/A | MCP tools are the interface here, not CLI commands. No CLI changes needed. |
| V. Simplicity (YAGNI) | PASS | Shared helper (not middleware/base class). No new abstractions beyond what's needed. No schema changes. |

**Post-Design Re-Check**: All gates still pass. The `ActiveRunResolver` is a single helper class, not a new abstraction layer. Two new repository methods are simple SQL queries. No new projects or dependencies.

## Project Structure

### Documentation (this feature)

```text
specs/017-mcp-tool-resilience/
├── plan.md              # This file
├── research.md          # Phase 0: research findings
├── data-model.md        # Phase 1: entity and query models
├── quickstart.md        # Phase 1: implementation guide
├── contracts/
│   └── mcp-tools.md     # Phase 1: MCP tool contract changes
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/Spectra.MCP/
├── Storage/
│   ├── RunRepository.cs              # MOD: +GetActiveRunsAsync(), +status filter in GetAllAsync()
│   └── ResultRepository.cs           # MOD: +GetInProgressTestsAsync()
├── Tools/
│   ├── ActiveRunResolver.cs          # NEW: shared auto-resolution helper
│   ├── RunManagement/
│   │   ├── GetExecutionStatusTool.cs     # MOD: optional run_id
│   │   ├── PauseExecutionRunTool.cs      # MOD: optional run_id
│   │   ├── ResumeExecutionRunTool.cs     # MOD: optional run_id
│   │   ├── CancelExecutionRunTool.cs     # MOD: optional run_id
│   │   ├── FinalizeExecutionRunTool.cs   # MOD: optional run_id
│   │   ├── ListActiveRunsTool.cs         # NEW
│   │   └── CancelAllActiveRunsTool.cs    # NEW
│   ├── TestExecution/
│   │   ├── GetTestCaseDetailsTool.cs     # MOD: optional run_id + test_handle
│   │   ├── AdvanceTestCaseTool.cs        # MOD: optional test_handle
│   │   ├── SkipTestCaseTool.cs           # MOD: optional test_handle
│   │   ├── BulkRecordResultsTool.cs      # MOD: optional run_id
│   │   ├── AddTestNoteTool.cs            # MOD: optional test_handle
│   │   ├── RetestTestCaseTool.cs         # MOD: optional run_id
│   │   └── SaveScreenshotTool.cs         # MOD: optional test_handle
│   └── Reporting/
│       ├── GetRunHistoryTool.cs          # MOD: +status filter, +summary counts
│       └── GetExecutionSummaryTool.cs    # MOD: optional run_id
├── Program.cs                            # MOD: register 2 new tools

tests/Spectra.MCP.Tests/
├── Tools/
│   ├── ActiveRunResolverTests.cs         # NEW
│   ├── ListActiveRunsToolTests.cs        # NEW
│   ├── CancelAllActiveRunsToolTests.cs   # NEW
│   └── GetRunHistoryToolTests.cs         # MOD: add filter tests
```

**Structure Decision**: All changes are within the existing `Spectra.MCP` project. No new projects needed. The `ActiveRunResolver` lives alongside tools in `Tools/` since it's consumed exclusively by tools.

## Complexity Tracking

No constitution violations. No complexity justifications needed.
