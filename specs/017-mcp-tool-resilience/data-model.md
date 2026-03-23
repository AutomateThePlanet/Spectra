# Data Model: MCP Tool Resilience

**Date**: 2026-03-23 | **Feature**: 017-mcp-tool-resilience

## Existing Entities (No Schema Changes)

### Run
No changes to the `runs` table or `Run` model. All fields already exist:
- `run_id`, `suite`, `status`, `started_at`, `started_by`, `environment`, `filters`, `updated_at`, `completed_at`

### TestResult
No changes to the `test_results` table or `TestResult` model. All fields already exist:
- `run_id`, `test_id`, `test_handle`, `status`, `notes`, `started_at`, `completed_at`, `attempt`, `blocked_by`, `screenshot_paths`

### RunStatus Enum (Existing)
`Created`, `Running`, `Paused`, `Completed`, `Cancelled`, `Abandoned`

**Terminal states**: Completed, Cancelled, Abandoned
**Active states**: Created, Running, Paused

### TestStatus Enum (Existing)
`Pending`, `InProgress`, `Passed`, `Failed`, `Skipped`, `Blocked`

---

## New Service: ActiveRunResolver

Shared helper for auto-resolving `run_id` and `test_handle` when omitted by callers.

### Inputs
- `run_id` (string?, from tool parameters)
- `test_handle` (string?, from tool parameters)

### Resolution Results

```
ActiveRunResolveResult
├── RunId: string              — resolved run ID (from parameter or auto-detected)
├── IsAutoResolved: bool       — true if run_id was auto-detected
└── Error: string?             — null on success, descriptive message on failure

TestHandleResolveResult
├── TestHandle: string         — resolved test handle
├── IsAutoResolved: bool       — true if test_handle was auto-detected
└── Error: string?             — null on success, descriptive message on failure
```

### State Transitions
None. This feature does not introduce new states or transitions. It only changes how existing parameters are resolved before the existing state machine processes them.

---

## New Query: GetActiveRunsAsync

**Repository**: RunRepository
**SQL**: `SELECT * FROM runs WHERE status IN ('Created', 'Running', 'Paused') ORDER BY started_at DESC`
**Returns**: `IReadOnlyList<Run>`

---

## New Query: GetInProgressTestsAsync

**Repository**: ResultRepository
**SQL**: `SELECT * FROM test_results WHERE run_id = @runId AND status = 'InProgress'`
**Returns**: `IReadOnlyList<TestResult>`

---

## Response Models for New Tools

### ListActiveRunsResponse
```
runs: [
  {
    run_id: string
    suite: string
    status: RunStatus
    started_at: DateTime
    started_by: string
    environment: string?
    progress: string          — e.g., "5/20 completed, 3 passed, 2 failed"
  }
]
count: int
```

### CancelAllActiveRunsResponse
```
cancelled: [
  {
    run_id: string
    suite: string
    previous_status: RunStatus
  }
]
cancelled_count: int
failed: [                     — runs that could not be cancelled
  {
    run_id: string
    suite: string
    reason: string
  }
]
```

### Enhanced GetRunHistoryResponse (additions)
```
runs: [
  {
    ... existing fields ...
    summary: {                — NEW: per-run test counts
      total: int
      passed: int
      failed: int
      skipped: int
      blocked: int
    }
  }
]
```
New parameter: `status` (string?, optional) — filter by RunStatus value
