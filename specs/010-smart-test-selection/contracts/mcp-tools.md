# MCP Tool Contracts: Smart Test Selection

## find_test_cases

**Method**: `find_test_cases`
**Category**: Data
**Description**: Search and filter test cases across all suites by metadata

### Parameters

```json
{
  "type": "object",
  "properties": {
    "query": {
      "type": "string",
      "description": "Free-text search — OR across keywords, case-insensitive, matches title + description + tags"
    },
    "suites": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Limit search to these suite names"
    },
    "priorities": {
      "type": "array",
      "items": { "type": "string", "enum": ["high", "medium", "low"] },
      "description": "Filter by priority (OR within array)"
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Filter by tags (OR within array)"
    },
    "components": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Filter by component (OR within array)"
    },
    "has_automation": {
      "type": "boolean",
      "description": "Filter by automation status"
    },
    "max_results": {
      "type": "integer",
      "description": "Maximum results to return (default: 50)"
    }
  }
}
```

### Response (success)

```json
{
  "data": {
    "matched": 25,
    "total_estimated_duration": "1h 45m",
    "tests": [
      {
        "id": "TC-134",
        "suite": "payment-processing",
        "title": "Payment timeout handling",
        "description": "Verifies payment gateway timeouts trigger retry",
        "priority": "high",
        "tags": ["payment", "timeout"],
        "component": "payment-processing",
        "estimated_duration": "5m",
        "has_automation": false
      }
    ],
    "warnings": ["Skipped suite 'legacy' — _index.json not found"]
  }
}
```

### Filter Logic

- **Between filter types**: AND (test must match ALL specified filter types)
- **Within filter arrays**: OR (test must match ANY value in the array)
- **Free-text query**: OR across keywords, ranked by hit count
- **Default ordering**: keyword hits (if query) > priority descending > suite name > index order
- **Truncation**: Returns up to `max_results`, `matched` shows total before truncation

### Error Cases

| Code | Condition |
|------|-----------|
| No error | No filters → returns all tests up to max_results |
| INVALID_PARAMS | max_results < 1 |

---

## get_test_execution_history

**Method**: `get_test_execution_history`
**Category**: Data
**Description**: Get execution history and statistics for specific tests

### Parameters

```json
{
  "type": "object",
  "properties": {
    "test_ids": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Test IDs to query. If omitted, returns all tests with history."
    },
    "limit": {
      "type": "integer",
      "description": "Max recent executions per test for statistics (default: 10)"
    }
  }
}
```

### Response (success)

```json
{
  "data": {
    "TC-100": {
      "last_executed": "2026-03-20T14:30:00Z",
      "last_status": "PASSED",
      "total_runs": 3,
      "pass_rate": 100.0,
      "last_run_id": "fb556e29-..."
    },
    "TC-134": {
      "last_executed": null,
      "last_status": null,
      "total_runs": 0,
      "pass_rate": null,
      "last_run_id": null
    }
  }
}
```

### Error Cases

| Code | Condition |
|------|-----------|
| No error | Empty test_ids → returns all tests with history |
| No error | Unknown test_ids → returns null/zero entry |

---

## list_saved_selections

**Method**: `list_saved_selections`
**Category**: Data
**Description**: List saved test selections from configuration

### Parameters

```json
{
  "type": "object",
  "properties": {}
}
```

### Response (success)

```json
{
  "data": {
    "selections": [
      {
        "name": "smoke",
        "description": "Quick smoke test — high priority tests only",
        "filters": { "priorities": ["high"], "tags": ["smoke"] },
        "estimated_test_count": 12,
        "estimated_duration": "20m"
      }
    ]
  }
}
```

### Error Cases

| Code | Condition |
|------|-----------|
| No error | No selections configured → returns empty array |

---

## start_execution_run (extended)

**Method**: `start_execution_run`
**Category**: RunManagement
**Description**: Start a new test execution run (existing tool, extended)

### New Parameters (added to existing)

```json
{
  "properties": {
    "suite": { "type": "string", "description": "Run full suite (existing)" },
    "test_ids": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Run specific tests from any suites (new, mutually exclusive with suite/selection)"
    },
    "selection": {
      "type": "string",
      "description": "Run a saved selection by name (new, mutually exclusive with suite/test_ids)"
    },
    "name": {
      "type": "string",
      "description": "Run name (required for test_ids and selection modes)"
    },
    "environment": { "type": "string" },
    "filters": { "type": "object", "description": "Additional filters (existing)" }
  }
}
```

### Three Modes (mutually exclusive)

1. **suite** — existing behavior, unchanged
2. **test_ids** — resolve IDs across all suite indexes, queue in given order, require `name`
3. **selection** — load selection from config, apply filters, queue matching tests, require `name`

### New Error Cases

| Code | Condition |
|------|-----------|
| INVALID_PARAMS | Multiple of suite/test_ids/selection provided |
| INVALID_PARAMS | test_ids or selection without name |
| INVALID_TEST_IDS | One or more test_ids not found (lists invalid IDs) |
| SELECTION_NOT_FOUND | Named selection not in config (lists available names) |
| NO_TESTS_MATCHED | Selection filters match zero tests |
