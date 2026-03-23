# Research: MCP Tool Resilience for Weaker Models

**Date**: 2026-03-23 | **Feature**: 017-mcp-tool-resilience

## R1: Current Tool Architecture and Parameter Handling

### Decision: Auto-resolution as shared helper, not middleware

**Rationale**: All 22 MCP tools implement `IMcpTool` with `ExecuteAsync(JsonElement? parameters)`. Each tool manually deserializes parameters via `McpProtocol.DeserializeParams<T>()` and validates individually. There is no shared validation layer or middleware pipeline. Adding a centralized auto-resolver as a static helper method that tools call is the simplest approach — it avoids introducing a middleware pattern that doesn't exist yet and keeps each tool in control of its own flow.

**Alternatives Considered**:
- **Middleware/decorator pattern**: Would require wrapping all tool registrations and changing the ToolRegistry dispatch. Over-engineered for this use case — violates Constitution Principle V (YAGNI).
- **Base class with template method**: Would require changing all 22 tools to inherit from a base. Too invasive for a cross-cutting parameter default.

### Findings: Tools Requiring run_id (13 tools)
1. `get_execution_status` — RunManagement
2. `pause_execution_run` — RunManagement
3. `resume_execution_run` — RunManagement
4. `cancel_execution_run` — RunManagement
5. `finalize_execution_run` — RunManagement
6. `get_test_case_details` — TestExecution
7. `advance_test_case` — TestExecution
8. `skip_test_case` — TestExecution
9. `bulk_record_results` — TestExecution
10. `add_test_note` — TestExecution (uses test_handle which encodes run_id)
11. `retest_test_case` — TestExecution
12. `save_screenshot` — TestExecution
13. `get_execution_summary` — Reporting

### Findings: Tools Requiring test_handle (7 tools)
1. `get_test_case_details` — also needs run_id
2. `advance_test_case` — also needs run_id (implicit via handle)
3. `skip_test_case` — also needs run_id (implicit via handle)
4. `add_test_note` — test_handle only
5. `retest_test_case` — needs run_id + test_id (not test_handle)
6. `save_screenshot` — test_handle only
7. `bulk_record_results` — uses test_handles array or `remaining` flag

Note: `add_test_note` and `save_screenshot` only take `test_handle` (no explicit `run_id`). The run is implicit in the handle. For auto-resolution, these need run_id resolved first to find in-progress tests.

### Findings: Current Validation Pattern
```csharp
var request = McpProtocol.DeserializeParams<RequestType>(parameters);
if (request is null || string.IsNullOrEmpty(request.RunId))
{
    return JsonSerializer.Serialize(McpToolResponse<object>.Failure(
        "INVALID_PARAMS", "run_id is required"));
}
```
This pattern will change to: resolve run_id if missing, then proceed.

---

## R2: Repository Query Capabilities for Auto-Resolution

### Decision: Add `GetActiveRunsAsync()` to RunRepository

**Rationale**: Current methods `GetActiveRunAsync(suite, user)` and `GetActiveRunByUserAsync(user)` return a single run. Auto-resolution needs ALL active runs (any suite, any user) to determine if exactly one exists. A new method `GetActiveRunsAsync()` is needed.

**Alternatives Considered**:
- **Reuse `GetAllAsync` with status filter**: `GetAllAsync` doesn't filter by status, only suite/user/limit. Adding a status parameter is possible but changes existing API semantics.
- **Query directly in the helper**: Violates repository pattern; storage access should go through RunRepository.

### Decision: Add `GetInProgressTestsAsync(runId)` to ResultRepository

**Rationale**: Currently, finding in-progress tests requires `GetByRunIdAsync(runId)` which returns ALL test results, then filtering client-side. A dedicated query is more efficient and explicit.

**Alternatives Considered**:
- **Use TestQueue.GetNext()**: Requires having a TestQueue instance which needs the full ExecutionEngine. Too heavy for a simple lookup.
- **Client-side filter on GetByRunIdAsync**: Works but wasteful for runs with many tests.

---

## R3: Existing get_run_history Tool Analysis

### Decision: Enhance existing `get_run_history` with status filter

**Rationale**: The tool exists with `suite`, `user`, and `limit` parameters. It returns run_id, suite, status, started_at, started_by, completed_at, environment. Missing: status filter, pass/fail/skip counts. Enhancement is straightforward:
1. Add `status` parameter to filter by RunStatus
2. Add summary counts per run (requires joining with test_results or a separate query)

**Current Query Path**: `GetRunHistoryTool` → `RunRepository.GetAllAsync(suite, user, limit)` → raw SQL `SELECT * FROM runs` with optional WHERE clauses.

**Alternatives Considered**:
- **New `list_runs` tool**: Would duplicate `get_run_history`. Enhancement is cleaner.

---

## R4: State Machine Terminal States

### Decision: Use existing `StateMachine.IsTerminal()` for active run detection

**Rationale**: `StateMachine` already has `IsTerminal(RunStatus)` returning true for Completed, Cancelled, Abandoned. Active = NOT terminal. This is the authoritative definition and aligns with the spec.

---

## R5: Error Message Format for Multi-Run Listing

### Decision: Structured text listing with pipe-separated fields

**Rationale**: MCP tool responses are consumed by AI orchestrators that parse natural language. A structured but human-readable format gives orchestrators enough context to self-correct:
```
Multiple active runs found. Please specify run_id:
- abc123 | suite: checkout | status: RUNNING | started: 2026-03-22T10:00:00Z
- def456 | suite: auth | status: PAUSED | started: 2026-03-21T15:30:00Z
```

**Alternatives Considered**:
- **JSON array in error message**: Harder for weaker models to parse from error context.
- **Separate `data` field in error response**: McpToolResponse error responses don't carry data — would require schema change.

---

## R6: Progress Summary Format

### Decision: Human-readable string from status counts

**Rationale**: `ResultRepository.GetStatusCountsAsync(runId)` already returns counts by status. Format as: `"5/20 completed, 3 passed, 1 failed, 1 skipped"`. This reuses existing infrastructure.

---

## R7: Schema Parameter Changes (required → optional)

### Decision: Remove run_id/test_handle from `required` array in ParameterSchema

**Rationale**: MCP protocol uses JSON Schema for parameter validation. Currently `run_id` is in the `required` array. Removing it from `required` while keeping the property definition makes it optional at the schema level. Some MCP clients may validate against the schema before calling — if `run_id` is required in schema, the call may never reach the tool.

**Risk**: None. Optional schema parameters are standard. Tools that always need the parameter (like `start_execution_run` mode selection) are unaffected since they don't use `run_id`.
