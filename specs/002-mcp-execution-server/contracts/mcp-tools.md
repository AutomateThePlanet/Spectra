# MCP Tool Contracts: Execution Server

**Feature**: 002-mcp-execution-server
**Date**: 2026-03-14
**Protocol**: MCP (Model Context Protocol) over JSON-RPC 2.0

## Overview

The MCP Execution Server exposes 14 tools across 3 categories:
- **Run Management** (7 tools): Suite discovery, run lifecycle
- **Test Execution** (5 tools): Test progression, result recording
- **Reporting** (2 tools): Status queries, history

All responses include self-contained context per Constitution Principle III.

---

## Standard Response Envelope

Every successful response includes:

```json
{
  "data": { },                    // Tool-specific payload
  "run_status": "RUNNING",        // Current run state (when applicable)
  "progress": "8/15",             // "completed/total" (when applicable)
  "next_expected_action": "..."   // Suggested next tool call
}
```

Error responses:

```json
{
  "error": {
    "code": "INVALID_TRANSITION",
    "message": "Human-readable explanation"
  },
  "current_run_status": "PAUSED",
  "next_expected_action": "resume_execution_run"
}
```

---

# Run Management Tools

## 1. list_available_suites

Returns all test suites with metadata.

### Request

```json
{
  "method": "list_available_suites",
  "params": {}
}
```

### Response

```json
{
  "data": {
    "suites": [
      {
        "name": "checkout",
        "test_count": 42,
        "last_updated": "2026-03-14T10:00:00Z"
      },
      {
        "name": "authentication",
        "test_count": 18,
        "last_updated": "2026-03-13T15:30:00Z"
      }
    ]
  },
  "next_expected_action": "start_execution_run"
}
```

### Errors

| Code | Condition |
|------|-----------|
| NO_SUITES_FOUND | No test directories exist |
| INDEX_STALE | _index.json missing or invalid |

---

## 2. start_execution_run

Creates a new execution run for a suite.

### Request

```json
{
  "method": "start_execution_run",
  "params": {
    "suite": "checkout",
    "environment": "staging",
    "filters": {
      "priority": "high",
      "tags": ["smoke", "payments"],
      "component": "cart",
      "test_ids": ["TC-101", "TC-102"]
    }
  }
}
```

### Parameters

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| suite | string | Yes | Suite name |
| environment | string | No | Target environment |
| filters | object | No | Test selection filters |

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "suite": "checkout",
    "test_count": 15,
    "first_test": {
      "test_handle": "a3f7c291-TC101-x9k2",
      "test_id": "TC-101",
      "title": "Checkout with valid Visa card"
    }
  },
  "run_status": "RUNNING",
  "progress": "0/15",
  "next_expected_action": "get_test_case_details"
}
```

### Errors

| Code | Condition |
|------|-----------|
| INVALID_SUITE | Suite does not exist |
| ACTIVE_RUN_EXISTS | User has active run on this suite |
| NO_TESTS_MATCH | Filters exclude all tests |

---

## 3. resume_execution_run

Resumes a paused run.

### Request

```json
{
  "method": "resume_execution_run",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c"
  }
}
```

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "next_test": {
      "test_handle": "a3f7c291-TC105-m3p7",
      "test_id": "TC-105",
      "title": "Checkout with insufficient funds"
    }
  },
  "run_status": "RUNNING",
  "progress": "4/15",
  "next_expected_action": "get_test_case_details"
}
```

### Errors

| Code | Condition |
|------|-----------|
| RUN_NOT_FOUND | Run ID not found |
| INVALID_TRANSITION | Run is not paused |
| NOT_OWNER | Run belongs to different user |

---

## 4. pause_execution_run

Pauses an active run.

### Request

```json
{
  "method": "pause_execution_run",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c"
  }
}
```

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "paused_at": "2026-03-14T11:30:00Z"
  },
  "run_status": "PAUSED",
  "progress": "8/15",
  "next_expected_action": "resume_execution_run"
}
```

### Errors

| Code | Condition |
|------|-----------|
| RUN_NOT_FOUND | Run ID not found |
| INVALID_TRANSITION | Run is not running |
| NOT_OWNER | Run belongs to different user |

---

## 5. cancel_execution_run

Cancels a run, preserving partial results.

### Request

```json
{
  "method": "cancel_execution_run",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "reason": "Environment unavailable"
  }
}
```

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "cancelled_at": "2026-03-14T11:35:00Z",
    "completed_tests": 8,
    "cancelled_tests": 7
  },
  "run_status": "CANCELLED",
  "progress": "8/15",
  "next_expected_action": "start_execution_run"
}
```

---

## 6. get_execution_status

Returns current run state and progress.

### Request

```json
{
  "method": "get_execution_status",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c"
  }
}
```

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "suite": "checkout",
    "started_at": "2026-03-14T10:00:00Z",
    "started_by": "user@example.com",
    "current_test": {
      "test_handle": "a3f7c291-TC105-m3p7",
      "test_id": "TC-105",
      "title": "Checkout with insufficient funds"
    },
    "summary": {
      "total": 15,
      "passed": 6,
      "failed": 1,
      "skipped": 1,
      "blocked": 0,
      "pending": 7
    }
  },
  "run_status": "RUNNING",
  "progress": "8/15",
  "next_expected_action": "get_test_case_details"
}
```

---

## 7. finalize_execution_run

Completes the run and generates reports.

### Request

```json
{
  "method": "finalize_execution_run",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "force": false
  }
}
```

### Parameters

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| run_id | string | Yes | Run to finalize |
| force | boolean | No | Allow finalize with pending tests |

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "completed_at": "2026-03-14T11:30:00Z",
    "report_path": "reports/a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c.json",
    "summary": {
      "total": 15,
      "passed": 12,
      "failed": 2,
      "skipped": 1,
      "blocked": 0
    }
  },
  "run_status": "COMPLETED",
  "progress": "15/15",
  "next_expected_action": "start_execution_run"
}
```

### Errors

| Code | Condition |
|------|-----------|
| TESTS_PENDING | Tests remain pending and force=false |

---

# Test Execution Tools

## 8. get_test_case_details

Returns full test content for execution.

### Request

```json
{
  "method": "get_test_case_details",
  "params": {
    "test_handle": "a3f7c291-TC104-x9k2"
  }
}
```

### Response

```json
{
  "data": {
    "test_handle": "a3f7c291-TC104-x9k2",
    "test_id": "TC-104",
    "title": "Checkout with expired card",
    "priority": "high",
    "tags": ["payments", "negative"],
    "component": "checkout",
    "preconditions": "User is logged in, cart has items",
    "step_count": 3,
    "steps": [
      { "number": 1, "action": "Navigate to checkout" },
      { "number": 2, "action": "Enter expired card details (exp: 01/2020)" },
      { "number": 3, "action": "Click 'Pay Now'" }
    ],
    "expected_result": "Payment is rejected, error message displays: card expired, user remains on checkout page",
    "test_data": "Card: 4111111111111111, Expiry: 01/2020"
  },
  "run_status": "RUNNING",
  "progress": "7/15",
  "next_expected_action": "advance_test_case"
}
```

### Errors

| Code | Condition |
|------|-----------|
| INVALID_HANDLE | Handle not found or invalid |
| TEST_NOT_PENDING | Test already completed |

---

## 9. advance_test_case

Records result and returns next test.

### Request

```json
{
  "method": "advance_test_case",
  "params": {
    "test_handle": "a3f7c291-TC104-x9k2",
    "status": "FAILED",
    "notes": "Error message shows 'Invalid card' instead of 'Card expired'"
  }
}
```

### Parameters

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| test_handle | string | Yes | Current test handle |
| status | string | Yes | PASSED or FAILED |
| notes | string | No | Observations |

### Response

```json
{
  "data": {
    "recorded": {
      "test_id": "TC-104",
      "status": "FAILED"
    },
    "blocked_tests": ["TC-108", "TC-109"],
    "next": {
      "test_handle": "a3f7c291-TC105-m3p7",
      "test_id": "TC-105",
      "title": "Checkout with insufficient funds"
    }
  },
  "run_status": "RUNNING",
  "progress": "8/15",
  "next_expected_action": "get_test_case_details"
}
```

When no more tests:

```json
{
  "data": {
    "recorded": {
      "test_id": "TC-119",
      "status": "PASSED"
    },
    "blocked_tests": [],
    "next": null
  },
  "run_status": "RUNNING",
  "progress": "15/15",
  "next_expected_action": "finalize_execution_run"
}
```

### Errors

| Code | Condition |
|------|-----------|
| INVALID_HANDLE | Handle not found |
| TEST_NOT_IN_PROGRESS | Test not currently active |
| INVALID_STATUS | Status not PASSED or FAILED |

---

## 10. skip_test_case

Skips current test with reason.

### Request

```json
{
  "method": "skip_test_case",
  "params": {
    "test_handle": "a3f7c291-TC104-x9k2",
    "reason": "Payment gateway not available in staging"
  }
}
```

### Response

```json
{
  "data": {
    "skipped": {
      "test_id": "TC-104",
      "reason": "Payment gateway not available in staging"
    },
    "blocked_tests": ["TC-108", "TC-109"],
    "next": {
      "test_handle": "a3f7c291-TC105-m3p7",
      "test_id": "TC-105",
      "title": "Checkout with insufficient funds"
    }
  },
  "run_status": "RUNNING",
  "progress": "8/15",
  "next_expected_action": "get_test_case_details"
}
```

---

## 11. retest_test_case

Re-queues a completed test for another attempt.

### Request

```json
{
  "method": "retest_test_case",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "test_id": "TC-104"
  }
}
```

### Response

```json
{
  "data": {
    "requeued": {
      "test_id": "TC-104",
      "new_attempt": 2,
      "new_handle": "a3f7c291-TC104-p7q2"
    }
  },
  "run_status": "RUNNING",
  "progress": "14/16",
  "next_expected_action": "get_test_case_details"
}
```

### Errors

| Code | Condition |
|------|-----------|
| TEST_NOT_COMPLETED | Test hasn't been executed yet |
| RUN_NOT_RUNNING | Run is paused or completed |

---

## 12. add_test_note

Adds a note without changing status.

### Request

```json
{
  "method": "add_test_note",
  "params": {
    "test_handle": "a3f7c291-TC104-x9k2",
    "note": "UI shows spinner but payment processes in background"
  }
}
```

### Response

```json
{
  "data": {
    "test_id": "TC-104",
    "note_added": true,
    "total_notes": 2
  },
  "run_status": "RUNNING",
  "progress": "7/15",
  "next_expected_action": "advance_test_case"
}
```

---

# Reporting Tools

## 13. get_execution_summary

Returns progress statistics for active run.

### Request

```json
{
  "method": "get_execution_summary",
  "params": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c"
  }
}
```

### Response

```json
{
  "data": {
    "run_id": "a3f7c291-4b8e-4f12-9a5d-1c2e3f4a5b6c",
    "suite": "checkout",
    "started_at": "2026-03-14T10:00:00Z",
    "duration_minutes": 45,
    "summary": {
      "total": 15,
      "passed": 10,
      "failed": 2,
      "skipped": 1,
      "blocked": 2,
      "pending": 0
    },
    "failed_tests": [
      { "test_id": "TC-104", "title": "Checkout with expired card" },
      { "test_id": "TC-107", "title": "Checkout with invalid CVV" }
    ]
  },
  "run_status": "RUNNING",
  "progress": "15/15",
  "next_expected_action": "finalize_execution_run"
}
```

---

## 14. get_run_history

Returns past runs for a suite.

### Request

```json
{
  "method": "get_run_history",
  "params": {
    "suite": "checkout",
    "limit": 10,
    "user": null
  }
}
```

### Parameters

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| suite | string | Yes | Suite name |
| limit | int | No | Max runs to return (default: 10) |
| user | string | No | Filter by user |

### Response

```json
{
  "data": {
    "suite": "checkout",
    "runs": [
      {
        "run_id": "a3f7c291-...",
        "started_at": "2026-03-14T10:00:00Z",
        "completed_at": "2026-03-14T11:30:00Z",
        "started_by": "user@example.com",
        "status": "COMPLETED",
        "summary": {
          "total": 15,
          "passed": 12,
          "failed": 2,
          "skipped": 1,
          "blocked": 0
        }
      }
    ]
  },
  "next_expected_action": "start_execution_run"
}
```

---

## Tool Availability Matrix

| Tool | Run State Required | Modifies State |
|------|-------------------|----------------|
| list_available_suites | None | No |
| start_execution_run | None | Yes |
| resume_execution_run | PAUSED | Yes |
| pause_execution_run | RUNNING | Yes |
| cancel_execution_run | RUNNING/PAUSED | Yes |
| get_execution_status | Any | No |
| finalize_execution_run | RUNNING | Yes |
| get_test_case_details | RUNNING | Yes (marks IN_PROGRESS) |
| advance_test_case | RUNNING | Yes |
| skip_test_case | RUNNING | Yes |
| retest_test_case | RUNNING | Yes |
| add_test_note | RUNNING | Yes |
| get_execution_summary | Any | No |
| get_run_history | None | No |
