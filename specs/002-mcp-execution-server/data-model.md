# Data Model: MCP Execution Server

**Feature**: 002-mcp-execution-server
**Date**: 2026-03-14

## Overview

This document defines the core entities for the MCP Execution Server. The models extend Spectra.Core where applicable and introduce new entities for execution state management.

---

## 1. Run

Represents a single test execution session.

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| RunId | string (UUID) | Yes | Unique identifier for the run |
| Suite | string | Yes | Name of the test suite being executed |
| Status | RunStatus | Yes | Current state of the run |
| StartedAt | DateTime | Yes | UTC timestamp when run was created |
| StartedBy | string | Yes | User identity who started the run |
| Environment | string? | No | Target environment (staging, uat, prod) |
| Filters | RunFilters? | No | Applied test filters |
| UpdatedAt | DateTime | Yes | UTC timestamp of last state change |
| CompletedAt | DateTime? | No | UTC timestamp when run was finalized |

### RunStatus Enum

```csharp
public enum RunStatus
{
    Created,    // Run initialized, not yet started
    Running,    // Tests being executed
    Paused,     // Temporarily stopped, can resume
    Completed,  // All tests done, report generated
    Cancelled,  // Manually stopped before completion
    Abandoned   // Auto-cancelled after timeout (72h default)
}
```

### RunFilters

```csharp
public sealed record RunFilters
{
    public Priority? Priority { get; init; }
    public List<string>? Tags { get; init; }
    public string? Component { get; init; }
    public List<string>? TestIds { get; init; }
}
```

### State Transitions

```
Created → Running       (start_execution_run)
Running → Paused        (pause_execution_run)
Running → Completed     (finalize_execution_run)
Running → Cancelled     (cancel_execution_run)
Paused → Running        (resume_execution_run)
Paused → Cancelled      (cancel_execution_run)
Paused → Abandoned      (timeout, default 72h)
```

### Validation Rules

- RunId must be a valid UUID
- Suite must match an existing test suite directory
- StartedBy must not be empty
- Status transitions must follow the state machine

---

## 2. TestResult

Represents the outcome of executing a single test.

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| RunId | string | Yes | FK to parent Run |
| TestId | string | Yes | Test case ID (e.g., TC-101) |
| TestHandle | string | Yes | Opaque reference for this test in this run |
| Status | TestStatus | Yes | Execution result |
| Notes | string? | No | Tester notes or observations |
| StartedAt | DateTime? | No | When test execution began |
| CompletedAt | DateTime? | No | When test execution ended |
| Attempt | int | Yes | Attempt number (1 for first, 2+ for retests) |
| BlockedBy | string? | No | Test ID that caused this test to be blocked |

### TestStatus Enum

```csharp
public enum TestStatus
{
    Pending,      // Not yet executed
    InProgress,   // Currently being executed
    Passed,       // Test passed
    Failed,       // Test failed
    Skipped,      // Manually skipped with reason
    Blocked       // Auto-blocked due to dependency failure
}
```

### State Transitions

```
Pending → InProgress    (get_test_case_details)
InProgress → Passed     (advance_test_case with status=passed)
InProgress → Failed     (advance_test_case with status=failed)
InProgress → Skipped    (skip_test_case)
Pending → Blocked       (dependency failed/skipped/blocked)
```

### Validation Rules

- TestHandle must be unique across all runs
- Attempt must be >= 1
- CompletedAt must be >= StartedAt when both present
- BlockedBy must reference a valid TestId when Status is Blocked

---

## 3. TestHandle

Opaque reference to prevent context manipulation.

### Format

```
{run_uuid_prefix}-{test_id}-{random_suffix}
```

### Examples

```
a3f7c291-TC104-x9k2
b5d8e412-TC205-m3p7
```

### Components

| Component | Length | Source |
|-----------|--------|--------|
| run_uuid_prefix | 8 chars | First 8 characters of RunId |
| test_id | variable | Test case ID from metadata |
| random_suffix | 4 chars | Cryptographic random |

### Generation

```csharp
public static string Generate(string runId, string testId)
{
    var prefix = runId[..8];
    var random = Convert.ToBase64String(RandomNumberGenerator.GetBytes(3))
        .Replace("+", "x").Replace("/", "k")[..4];
    return $"{prefix}-{testId}-{random}";
}
```

### Validation Rules

- Must contain exactly 2 hyphens (3 parts)
- First part must match current run's UUID prefix
- Second part must match expected test ID
- Handle must exist in test_results table

---

## 4. ExecutionQueue

In-memory representation of the test execution order.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| RunId | string | Associated run |
| Tests | List<QueuedTest> | Ordered list of tests to execute |
| CurrentIndex | int | Index of current test (0-based) |

### QueuedTest

```csharp
public sealed record QueuedTest
{
    public required string TestId { get; init; }
    public required string TestHandle { get; init; }
    public required string Title { get; init; }
    public required Priority Priority { get; init; }
    public string? DependsOn { get; init; }
    public TestStatus Status { get; init; } = TestStatus.Pending;
}
```

### Ordering Rules

1. Tests with no dependencies come first
2. Tests are then ordered by their dependency chain
3. Within same dependency level, ordered by Priority (high → medium → low)
4. Within same priority, ordered by TestId (deterministic)

### Queue Operations

| Operation | Description |
|-----------|-------------|
| BuildQueue | Create queue from _index.json applying filters |
| GetNext | Return next pending/blocked test |
| MarkCompleted | Update status, trigger dependency cascade if failed/skipped |
| Requeue | Add test back to queue with incremented attempt |

---

## 5. ExecutionReport

Generated when a run is finalized.

### Fields

| Field | Type | Description |
|-------|------|-------------|
| RunId | string | Associated run |
| Suite | string | Test suite name |
| Environment | string? | Target environment |
| StartedAt | DateTime | Run start time |
| CompletedAt | DateTime | Run completion time |
| ExecutedBy | string | User who executed |
| Status | RunStatus | Final run status |
| Summary | ReportSummary | Aggregate counts |
| Results | List<TestResultEntry> | Individual test results |

### ReportSummary

```csharp
public sealed record ReportSummary
{
    public required int Total { get; init; }
    public required int Passed { get; init; }
    public required int Failed { get; init; }
    public required int Skipped { get; init; }
    public required int Blocked { get; init; }
    public TimeSpan Duration => CompletedAt - StartedAt;
}
```

### TestResultEntry

```csharp
public sealed record TestResultEntry
{
    public required string TestId { get; init; }
    public required string Title { get; init; }
    public required TestStatus Status { get; init; }
    public required int Attempt { get; init; }
    public TimeSpan? Duration { get; init; }
    public string? Notes { get; init; }
    public string? BlockedBy { get; init; }
}
```

### File Output

```
reports/
├── {run_id}.json      # Machine-readable
└── {run_id}.md        # Human-readable
```

---

## 6. MCP Tool Response

Standard wrapper for all MCP tool responses.

### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Data | object | Yes | Tool-specific result data |
| RunStatus | RunStatus? | Conditional | Current run state (when run exists) |
| Progress | string? | Conditional | "completed/total" format |
| NextExpectedAction | string? | Conditional | Suggested next tool call |
| Error | ErrorInfo? | Conditional | Error details if failed |

### ErrorInfo

```csharp
public sealed record ErrorInfo
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}
```

### Error Codes

| Code | Description |
|------|-------------|
| INVALID_SUITE | Suite does not exist |
| RUN_NOT_FOUND | Run ID not found |
| INVALID_HANDLE | Test handle invalid or expired |
| INVALID_TRANSITION | State transition not allowed |
| ACTIVE_RUN_EXISTS | User already has active run on suite |
| TEST_NOT_IN_PROGRESS | Cannot record result for non-active test |

---

## 7. Relationships

```
Run (1) ←──────→ (N) TestResult
 │
 └── RunFilters (embedded)

TestResult ──→ TestCase (via TestId, read from _index.json)
 │
 └── TestHandle (generated per run)

ExecutionQueue (in-memory) ←──── Run + _index.json

ExecutionReport ←──── Run + TestResult[]
```

---

## 8. Database Schema

### runs Table

```sql
CREATE TABLE runs (
    run_id TEXT PRIMARY KEY,
    suite TEXT NOT NULL,
    status TEXT NOT NULL CHECK (status IN ('Created','Running','Paused','Completed','Cancelled','Abandoned')),
    started_at TEXT NOT NULL,
    started_by TEXT NOT NULL,
    environment TEXT,
    filters TEXT,  -- JSON
    updated_at TEXT NOT NULL,
    completed_at TEXT
);

CREATE INDEX idx_runs_suite_user ON runs(suite, started_by);
CREATE INDEX idx_runs_status ON runs(status);
```

### test_results Table

```sql
CREATE TABLE test_results (
    run_id TEXT NOT NULL,
    test_id TEXT NOT NULL,
    test_handle TEXT NOT NULL UNIQUE,
    status TEXT NOT NULL CHECK (status IN ('Pending','InProgress','Passed','Failed','Skipped','Blocked')),
    notes TEXT,
    started_at TEXT,
    completed_at TEXT,
    attempt INTEGER NOT NULL DEFAULT 1,
    blocked_by TEXT,
    PRIMARY KEY (run_id, test_id, attempt),
    FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
);

CREATE INDEX idx_results_run ON test_results(run_id);
CREATE INDEX idx_results_handle ON test_results(test_handle);
CREATE INDEX idx_results_status ON test_results(run_id, status);
```

---

## 9. Reused from Spectra.Core

The following existing models are reused without modification:

| Model | Location | Usage |
|-------|----------|-------|
| TestCase | Spectra.Core.Models | Test content from Markdown |
| TestIndexEntry | Spectra.Core.Models | Metadata from _index.json |
| MetadataIndex | Spectra.Core.Models | Suite index |
| Priority | Spectra.Core.Models | Priority enum |
| ValidationResult | Spectra.Core.Models | Validation outcomes |
