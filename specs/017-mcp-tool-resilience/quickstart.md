# Quickstart: 017-mcp-tool-resilience

## Overview

This feature makes MCP tools resilient to weaker AI models (GPT-4.1, GPT-4o) that omit required parameters, and adds run management tools.

## Implementation Order

### Phase 1: Infrastructure (do first)
1. Add `GetActiveRunsAsync()` to `RunRepository`
2. Add `GetInProgressTestsAsync(runId)` to `ResultRepository`
3. Create `ActiveRunResolver` helper in `Spectra.MCP/Tools/`
4. Unit test the resolver with 0, 1, 2+ active runs

### Phase 2: Auto-Resolution (core feature)
5. Update all 13 run_id tools: replace validation with resolver call
6. Update all 7 test_handle tools: add resolver call after run resolution
7. Remove `run_id`/`test_handle` from `required` arrays in ParameterSchema
8. Update tool descriptions to mention auto-detection

### Phase 3: New Tools
9. Create `ListActiveRunsTool`
10. Create `CancelAllActiveRunsTool`
11. Register both in `Program.cs`

### Phase 4: Enhanced History
12. Add `status` parameter to `GetRunHistoryTool`
13. Add per-run summary counts to history response
14. Add `status` filter to `RunRepository.GetAllAsync()`

## Key Files to Modify

| File | Change |
|------|--------|
| `src/Spectra.MCP/Storage/RunRepository.cs` | Add `GetActiveRunsAsync()`, add status filter to `GetAllAsync()` |
| `src/Spectra.MCP/Storage/ResultRepository.cs` | Add `GetInProgressTestsAsync()` |
| `src/Spectra.MCP/Tools/ActiveRunResolver.cs` | **NEW** — shared auto-resolution helper |
| `src/Spectra.MCP/Tools/RunManagement/*.cs` | Update 6 tools for optional run_id |
| `src/Spectra.MCP/Tools/TestExecution/*.cs` | Update 7 tools for optional run_id/test_handle |
| `src/Spectra.MCP/Tools/Reporting/*.cs` | Update 2 tools for optional run_id; enhance history |
| `src/Spectra.MCP/Tools/RunManagement/ListActiveRunsTool.cs` | **NEW** |
| `src/Spectra.MCP/Tools/RunManagement/CancelAllActiveRunsTool.cs` | **NEW** |
| `src/Spectra.MCP/Program.cs` | Register new tools |

## Testing Strategy

- **Unit tests for ActiveRunResolver**: 0/1/2+ active runs, 0/1/2+ in-progress tests, explicit params bypass
- **Unit tests for new tools**: list_active_runs, cancel_all_active_runs
- **Integration tests**: end-to-end flow with omitted parameters
- **Regression**: all 306 existing MCP tests must pass unchanged
