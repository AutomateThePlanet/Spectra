# MCP Tool Contracts: 017-mcp-tool-resilience

## Contract Changes to Existing Tools

### Parameter Schema Change: run_id becomes optional

Applies to all 13 tools that accept `run_id`. The `required` array in the JSON Schema no longer includes `run_id`.

**Before:**
```json
{
  "type": "object",
  "properties": {
    "run_id": { "type": "string", "description": "Run identifier" }
  },
  "required": ["run_id"]
}
```

**After:**
```json
{
  "type": "object",
  "properties": {
    "run_id": { "type": "string", "description": "Run identifier. If omitted, auto-detects when exactly one active run exists." }
  }
}
```

**Affected tools**: get_execution_status, pause_execution_run, resume_execution_run, cancel_execution_run, finalize_execution_run, get_test_case_details, advance_test_case, skip_test_case, bulk_record_results, retest_test_case, get_execution_summary

**Note**: `add_test_note` and `save_screenshot` use `test_handle` only (no explicit `run_id`). Their auto-resolution resolves via the active run first, then finds the in-progress test.

### Parameter Schema Change: test_handle becomes optional

Applies to all 7 tools that accept `test_handle`.

**Before:**
```json
{
  "type": "object",
  "properties": {
    "test_handle": { "type": "string", "description": "Test handle from get_test_case_details" }
  },
  "required": ["test_handle"]
}
```

**After:**
```json
{
  "type": "object",
  "properties": {
    "test_handle": { "type": "string", "description": "Test handle. If omitted, auto-detects the current in-progress test." }
  }
}
```

**Affected tools**: get_test_case_details, advance_test_case, skip_test_case, add_test_note, retest_test_case, save_screenshot

**Note**: `bulk_record_results` uses `test_ids` array or `remaining` flag, not a single `test_handle`, so its auto-resolution only applies to `run_id`.

---

## Auto-Resolution Error Responses

### No Active Runs (run_id omitted, 0 active runs)
```json
{
  "error": {
    "code": "NO_ACTIVE_RUNS",
    "message": "No active runs found. Use start_execution_run to begin a new execution."
  },
  "next_expected_action": "start_execution_run"
}
```

### Multiple Active Runs (run_id omitted, 2+ active runs)
```json
{
  "error": {
    "code": "MULTIPLE_ACTIVE_RUNS",
    "message": "Multiple active runs found. Please specify run_id:\n- abc123 | suite: checkout | status: RUNNING | started: 2026-03-22T10:00:00Z\n- def456 | suite: auth | status: PAUSED | started: 2026-03-21T15:30:00Z"
  },
  "next_expected_action": "list_active_runs"
}
```

### No In-Progress Test (test_handle omitted, 0 in-progress tests)
```json
{
  "error": {
    "code": "NO_TEST_IN_PROGRESS",
    "message": "No test currently in progress. Use get_execution_status to see the next test."
  },
  "next_expected_action": "get_execution_status"
}
```

### Multiple In-Progress Tests (test_handle omitted, 2+ in-progress tests)
```json
{
  "error": {
    "code": "MULTIPLE_TESTS_IN_PROGRESS",
    "message": "Multiple tests in progress. Please specify test_handle:\n- handle1 | TC-001: Login flow\n- handle2 | TC-002: Checkout flow"
  }
}
```

---

## New Tool: list_active_runs

### Schema
```json
{
  "name": "list_active_runs",
  "description": "List all active (non-terminal) execution runs. Returns runs in CREATED, RUNNING, or PAUSED state.",
  "inputSchema": {
    "type": "object",
    "properties": {}
  }
}
```

### Success Response
```json
{
  "data": {
    "runs": [
      {
        "run_id": "abc-123",
        "suite": "checkout",
        "status": "RUNNING",
        "started_at": "2026-03-22T10:00:00Z",
        "started_by": "alice",
        "environment": "staging",
        "progress": "5/20 completed, 3 passed, 2 failed"
      }
    ],
    "count": 1
  },
  "next_expected_action": "get_execution_status"
}
```

### Empty Response
```json
{
  "data": {
    "runs": [],
    "count": 0,
    "message": "No active runs found."
  },
  "next_expected_action": "start_execution_run"
}
```

---

## New Tool: cancel_all_active_runs

### Schema
```json
{
  "name": "cancel_all_active_runs",
  "description": "Cancel all active execution runs at once. Transitions all CREATED, RUNNING, and PAUSED runs to CANCELLED.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "reason": {
        "type": "string",
        "description": "Optional reason for cancellation"
      }
    }
  }
}
```

### Success Response
```json
{
  "data": {
    "cancelled": [
      {
        "run_id": "abc-123",
        "suite": "checkout",
        "previous_status": "RUNNING"
      },
      {
        "run_id": "def-456",
        "suite": "auth",
        "previous_status": "PAUSED"
      }
    ],
    "cancelled_count": 2,
    "failed": []
  },
  "next_expected_action": "start_execution_run"
}
```

### Empty Response (no active runs)
```json
{
  "data": {
    "cancelled": [],
    "cancelled_count": 0,
    "failed": [],
    "message": "No active runs to cancel."
  },
  "next_expected_action": "start_execution_run"
}
```

---

## Enhanced Tool: get_run_history

### Schema (additions in bold)
```json
{
  "name": "get_run_history",
  "description": "Get execution run history with optional filters",
  "inputSchema": {
    "type": "object",
    "properties": {
      "suite": { "type": "string", "description": "Filter by suite name" },
      "user": { "type": "string", "description": "Filter by user who started the run" },
      "status": { "type": "string", "description": "Filter by run status (CREATED, RUNNING, PAUSED, COMPLETED, CANCELLED, ABANDONED)" },
      "limit": { "type": "integer", "description": "Maximum number of runs (default: 50)" }
    }
  }
}
```

### Enhanced Response (summary field added per run)
```json
{
  "data": {
    "runs": [
      {
        "run_id": "abc-123",
        "suite": "checkout",
        "status": "COMPLETED",
        "started_at": "2026-03-22T10:00:00Z",
        "started_by": "alice",
        "completed_at": "2026-03-22T11:30:00Z",
        "environment": "staging",
        "summary": {
          "total": 20,
          "passed": 18,
          "failed": 1,
          "skipped": 1,
          "blocked": 0
        }
      }
    ],
    "count": 1
  },
  "next_expected_action": "get_execution_summary"
}
```
