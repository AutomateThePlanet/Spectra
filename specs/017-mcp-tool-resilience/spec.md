# Feature Specification: MCP Tool Resilience for Weaker Models

**Feature Branch**: `017-mcp-tool-resilience`
**Created**: 2026-03-23
**Status**: Draft
**Input**: User description: "Make SPECTRA MCP tools more resilient for weaker models (GPT-4.1, GPT-4o) and add missing management tools"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Auto-Resolve Run ID When Omitted (Priority: P1)

An AI orchestrator powered by a weaker model (e.g., GPT-4.1, GPT-4o) calls an MCP tool such as `finalize_execution_run` or `get_execution_status` without providing the required `run_id` parameter. Instead of failing with a cryptic parameter error, the system checks for active runs and auto-resolves the run when unambiguous.

**Why this priority**: This is the core problem. Without this fix, weaker models cannot complete test execution workflows at all, making the MCP server unusable for a significant segment of AI orchestrators.

**Independent Test**: Can be fully tested by starting a single run, then calling any run-scoped tool with an empty input. The tool should succeed by auto-detecting the active run.

**Acceptance Scenarios**:

1. **Given** exactly one active run exists, **When** a tool requiring `run_id` is called without `run_id`, **Then** the system uses that single active run automatically and the tool executes normally.
2. **Given** no active runs exist, **When** a tool requiring `run_id` is called without `run_id`, **Then** the system returns a helpful error: "No active runs found. Use start_execution_run to begin a new execution."
3. **Given** two or more active runs exist, **When** a tool requiring `run_id` is called without `run_id`, **Then** the system returns an error listing all active runs with their ID, suite name, status, and start time, asking the caller to specify which run.
4. **Given** a valid `run_id` is provided, **When** any tool is called, **Then** existing behavior is preserved exactly.

---

### User Story 2 - Auto-Resolve Test Handle When Omitted (Priority: P1)

An AI orchestrator calls a test-scoped tool (e.g., `advance_test_case`, `add_test_note`) without providing `test_handle`. The system checks for the current in-progress test within the resolved run and auto-resolves when unambiguous.

**Why this priority**: Same root cause as Story 1. Weaker models also omit `test_handle`. Without auto-resolution, test-level operations fail even after run-level auto-resolution is working.

**Independent Test**: Can be tested by starting a run, advancing to a test, then calling `advance_test_case` with only a status and no `test_handle`.

**Acceptance Scenarios**:

1. **Given** exactly one test is in IN_PROGRESS state in the current run, **When** a test-scoped tool is called without `test_handle`, **Then** the system uses that test's handle automatically.
2. **Given** no tests are in IN_PROGRESS state, **When** a test-scoped tool is called without `test_handle`, **Then** the system returns a helpful error: "No test currently in progress. Use get_execution_status to see the next test."
3. **Given** multiple tests are in IN_PROGRESS state, **When** a test-scoped tool is called without `test_handle`, **Then** the system returns an error listing the in-progress tests with their handles, titles, and status.

---

### User Story 3 - List Active Runs (Priority: P2)

A user or orchestrator wants to see all currently active (non-terminal) runs to understand what's in progress before starting new work or to select a run to interact with.

**Why this priority**: Supports the auto-resolution feature by giving orchestrators a way to discover runs, and provides essential visibility for multi-run scenarios.

**Independent Test**: Can be tested by starting multiple runs, calling `list_active_runs`, and verifying all non-terminal runs appear with correct details.

**Acceptance Scenarios**:

1. **Given** three runs exist (one RUNNING, one PAUSED, one COMPLETED), **When** `list_active_runs` is called, **Then** only the RUNNING and PAUSED runs are returned with their run_id, suite name, status, start time, started_by, environment, and progress summary.
2. **Given** no active runs exist, **When** `list_active_runs` is called, **Then** the response is: "No active runs found."
3. **Given** a run is in CREATED state (not yet started), **When** `list_active_runs` is called, **Then** that run is included in the results.

---

### User Story 4 - Cancel All Active Runs (Priority: P2)

A user wants to clean up all active runs at once, for example after an interrupted session or environment reset. They call `cancel_all_active_runs` which transitions all non-terminal runs to CANCELLED.

**Why this priority**: Important for session cleanup and error recovery, but less critical than the core auto-resolution and discovery features.

**Independent Test**: Can be tested by starting multiple runs in different states, calling `cancel_all_active_runs`, and verifying all are cancelled with a summary.

**Acceptance Scenarios**:

1. **Given** three active runs exist (RUNNING, PAUSED, CREATED), **When** `cancel_all_active_runs` is called, **Then** all three transition to CANCELLED and the response lists each with its previous state.
2. **Given** no active runs exist, **When** `cancel_all_active_runs` is called, **Then** the response is: "No active runs to cancel."
3. **Given** a mix of active and terminal runs exist, **When** `cancel_all_active_runs` is called, **Then** only the active runs are cancelled; terminal runs are not affected.

---

### User Story 5 - Enhanced Run History with Filters (Priority: P3)

A user or orchestrator queries run history and can filter by status, suite name, or limit the number of results. The response includes comprehensive details for each run.

**Why this priority**: Improves discoverability and management but is not required for the core resilience fixes.

**Independent Test**: Can be tested by creating runs across multiple suites and statuses, then querying with different filter combinations.

**Acceptance Scenarios**:

1. **Given** runs exist across multiple suites and statuses, **When** `get_run_history` is called with a status filter of "COMPLETED", **Then** only completed runs are returned.
2. **Given** runs exist, **When** `get_run_history` is called with a suite filter of "checkout", **Then** only runs for the checkout suite are returned.
3. **Given** many runs exist, **When** `get_run_history` is called with a limit of 5, **Then** at most 5 runs are returned, ordered by most recent first.
4. **Given** runs exist, **When** `get_run_history` is called with no filters, **Then** all runs are returned with run_id, suite name, status, start/completion timestamps, environment, and pass/fail/skip summary.

---

### Edge Cases

- What happens when an active run's suite file has been deleted since the run started? The run should still appear in listings with whatever metadata was captured at creation time.
- What happens when `cancel_all_active_runs` encounters a run that fails to transition (e.g., database lock)? The tool should cancel as many as possible and report both successes and failures.
- What happens when two tools are called concurrently and both try to auto-resolve the same single active run? Each call should resolve independently; the underlying run state machine handles concurrency.
- What happens when a run transitions between the auto-resolution check and the actual tool execution? The tool should handle stale state gracefully and return an appropriate error (e.g., "Run {id} is no longer active").

## Requirements *(mandatory)*

### Functional Requirements

#### Auto-Resolution

- **FR-001**: All MCP tools that accept `run_id` MUST treat it as optional. When omitted, the system MUST auto-resolve using active run detection.
- **FR-002**: Auto-resolution MUST use a single active run when exactly one exists, return a descriptive error when none exist, and return a listing of all active runs when multiple exist.
- **FR-003**: All MCP tools that accept `test_handle` MUST treat it as optional within a resolved run. When omitted, the system MUST auto-resolve using the current in-progress test.
- **FR-004**: Auto-resolution for `test_handle` MUST use the single in-progress test when exactly one exists, and return descriptive errors otherwise.
- **FR-005**: When both `run_id` and `test_handle` are omitted, the system MUST resolve `run_id` first, then resolve `test_handle` within that run.
- **FR-006**: Auto-resolution MUST NOT change behavior when parameters are explicitly provided. Existing callers MUST NOT be affected.

#### New Management Tools

- **FR-007**: The system MUST provide a `list_active_runs` tool that returns all runs not in a terminal state (COMPLETED, CANCELLED, ABANDONED), including run_id, suite name, status, start time, started_by identity, environment, and a progress summary.
- **FR-008**: The system MUST provide a `cancel_all_active_runs` tool that transitions all non-terminal runs to CANCELLED and returns a summary listing each cancelled run with its previous state.
- **FR-009**: The `cancel_all_active_runs` tool MUST process all eligible runs even if individual transitions fail, and report both successes and failures.

#### Enhanced Run History

- **FR-010**: The existing run history tool MUST support optional filters for status, suite name, and result limit.
- **FR-011**: Run history results MUST include run_id, suite name, status, start and completion timestamps, environment, and pass/fail/skip counts.

### Key Entities

- **Active Run**: A run in any non-terminal state (CREATED, RUNNING, PAUSED). Used for auto-resolution and listing.
- **Terminal State**: COMPLETED, CANCELLED, or ABANDONED. Runs in these states are excluded from auto-resolution.
- **In-Progress Test**: A test within a run that currently has IN_PROGRESS status. Used for test handle auto-resolution.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Orchestrators that omit `run_id` complete test execution workflows end-to-end when a single run is active, with zero parameter-related failures.
- **SC-002**: Orchestrators that omit `test_handle` successfully record test results when a single test is in progress, with zero parameter-related failures.
- **SC-003**: Error messages for ambiguous resolution (0 or 2+ matches) include enough context for the orchestrator to self-correct on the next call.
- **SC-004**: All existing callers that provide explicit `run_id` and `test_handle` experience no change in behavior (zero regressions).
- **SC-005**: Users can discover and cancel all active runs in a single tool call, reducing multi-run cleanup from N calls to 1.
- **SC-006**: All existing tests continue to pass after changes.

## Assumptions

- "Active run" is defined as any run NOT in COMPLETED, CANCELLED, or ABANDONED state. This aligns with the existing state machine.
- The `started_by` field in run listings uses the existing `UserIdentityResolver` mechanism. If no identity is available, the field is omitted or shows "unknown".
- The progress summary format (e.g., "5/20 completed, 3 passed, 2 failed") is a human-readable string, not structured data, since it is primarily consumed by AI orchestrators rendering text to users.
- Auto-resolution logic is implemented as shared infrastructure (e.g., a helper method) rather than duplicated in each tool, to ensure consistent behavior.
- The `get_run_history` tool already exists and will be enhanced with filters rather than creating a new tool.
