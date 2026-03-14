# Feature Specification: MCP Execution Server

**Feature Branch**: `002-mcp-execution-server`
**Created**: 2026-03-14
**Status**: Draft
**Input**: MCP execution server for deterministic test execution through any LLM orchestrator

## Overview

The MCP Execution Server enables testers to execute manual test suites through any LLM-based assistant (GitHub Copilot, Claude, custom agents). It provides a deterministic, stateful execution engine that guides testers through test cases one at a time, tracks results, and generates execution reports.

This is Phase 2 of the Spectra project, building on the CLI (Phase 1) which generates and maintains test cases. The execution server consumes the test suites created by the CLI and provides a conversational interface for executing them.

## Clarifications

### Session 2026-03-14

- Q: Where does user identity come from for run ownership and validation? → A: Derived from environment (git config user.name, fallback to OS username)
- Q: What format should execution reports be generated in? → A: Both JSON and Markdown generated side-by-side
- Q: Should blocking cascade transitively (blocked tests block their dependents)? → A: Yes, cascade transitively
- Q: How long should execution history be retained? → A: Indefinitely (no automatic cleanup, manual purge available)
- Q: What observability/logging is required? → A: Structured logging to file with configurable verbosity levels

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Execute a Test Suite (Priority: P1)

A tester wants to execute a test suite through their preferred LLM assistant. They start a run, receive tests one at a time, record pass/fail results with notes, and complete the run with a summary report.

**Why this priority**: This is the core value proposition - enabling conversational test execution through any LLM. Without this, the MCP server has no purpose.

**Independent Test**: Can be fully tested by starting a run, executing 3+ tests with mixed results (pass/fail/skip), and verifying the final report contains accurate results.

**Acceptance Scenarios**:

1. **Given** a validated test suite exists, **When** I request to start an execution run, **Then** I receive confirmation with the first test case ready to execute
2. **Given** an active run with a test in progress, **When** I record the result as passed/failed with notes, **Then** the system advances to the next test automatically
3. **Given** all tests in a run are completed, **When** I finalize the run, **Then** I receive a summary report with pass/fail counts and all recorded notes

---

### User Story 2 - Pause and Resume Execution (Priority: P1)

A tester needs to pause mid-suite (meeting, lunch, end of day) and resume later without losing progress. The system preserves all state and picks up exactly where they left off.

**Why this priority**: Real-world test execution is rarely completed in one sitting. Without pause/resume, testers would lose progress and avoid using the tool.

**Independent Test**: Can be tested by starting a run, executing 2 tests, pausing, waiting, resuming, and verifying the next test is correct with prior results preserved.

**Acceptance Scenarios**:

1. **Given** an active run with some tests completed, **When** I pause the run, **Then** the system confirms pause and preserves all progress
2. **Given** a paused run, **When** I resume the run, **Then** I receive the next unexecuted test and can continue normally
3. **Given** a paused run, **When** I check status without resuming, **Then** I see current progress without affecting run state

---

### User Story 3 - Skip and Block Tests (Priority: P1)

A tester encounters a test they cannot execute (environment not ready, dependency failed, not applicable). They can skip with a reason, and the system automatically blocks dependent tests.

**Why this priority**: Test dependencies are common. Failing to handle skips/blocks means testers waste time on tests that cannot succeed.

**Independent Test**: Can be tested by creating tests with dependencies, skipping a prerequisite test, and verifying dependent tests are automatically blocked with appropriate reasons.

**Acceptance Scenarios**:

1. **Given** a test in progress, **When** I skip it with reason "environment not available", **Then** the test is marked skipped with my reason and the next test is presented
2. **Given** test B depends on test A, **When** test A fails or is skipped, **Then** test B is automatically marked as blocked with reference to test A
3. **Given** multiple tests depend on a failed test, **When** viewing the summary, **Then** all blocked tests show the chain of dependency that caused the block

---

### User Story 4 - Filter Tests Before Execution (Priority: P2)

A tester wants to run only specific tests - by priority, tags, component, or explicit test IDs. They can define filters when starting a run and only matching tests are included.

**Why this priority**: Full suite runs are rare. Most executions target specific areas (smoke tests, regression for a component, high-priority only).

**Independent Test**: Can be tested by starting a filtered run with priority=high, verifying only high-priority tests are included, and confirming the count matches expectations.

**Acceptance Scenarios**:

1. **Given** a suite with tests of various priorities, **When** I start a run filtered to priority=high, **Then** only high-priority tests are included in the run
2. **Given** a suite with tagged tests, **When** I start a run filtered to tags=["payments", "checkout"], **Then** only tests with matching tags are included
3. **Given** explicit test IDs, **When** I start a run with specific IDs, **Then** only those tests are included in dependency order

---

### User Story 5 - View Available Suites (Priority: P2)

A tester wants to see what test suites are available before starting a run. They can list all suites with test counts and select one to execute.

**Why this priority**: Discovery is essential for new team members and prevents errors from mistyped suite names.

**Independent Test**: Can be tested by listing suites and verifying all test directories appear with accurate test counts.

**Acceptance Scenarios**:

1. **Given** multiple test suites exist, **When** I request available suites, **Then** I receive a list with suite names and test counts
2. **Given** suite information is displayed, **When** I select a suite, **Then** I can start a run for that suite with optional filters
3. **Given** a suite has no tests, **When** I list suites, **Then** the empty suite appears with count=0

---

### User Story 6 - Retest Failed Tests (Priority: P2)

A tester fixed an issue and wants to retest only the tests that failed without re-running the entire suite. They can re-queue specific tests within an active run.

**Why this priority**: Bug fixes often need quick verification. Re-running entire suites wastes time.

**Independent Test**: Can be tested by failing a test, re-queuing it, and verifying it appears again in the execution queue with attempt number incremented.

**Acceptance Scenarios**:

1. **Given** a completed test that failed, **When** I request to retest it, **Then** the test is re-queued and appears again when reached
2. **Given** a retested test, **When** viewing results, **Then** I see all attempts with their individual results and notes
3. **Given** multiple failed tests, **When** I retest all failed, **Then** all failed tests are re-queued in dependency order

---

### User Story 7 - View Execution History (Priority: P3)

A tester or manager wants to see past execution runs - when they ran, who ran them, what the results were. Historical data supports trend analysis and audit requirements.

**Why this priority**: History is valuable but not required for core execution. Teams can function without it initially.

**Independent Test**: Can be tested by completing multiple runs, querying history, and verifying all runs appear with accurate metadata.

**Acceptance Scenarios**:

1. **Given** multiple runs have been completed, **When** I request run history for a suite, **Then** I receive a list of past runs with dates, user, and summary
2. **Given** run history, **When** I select a specific run, **Then** I can view detailed results from that run
3. **Given** runs from multiple users, **When** I filter history by user, **Then** only that user's runs appear

---

### User Story 8 - Concurrent Execution by Multiple Users (Priority: P3)

Multiple testers can execute different suites simultaneously. The same tester cannot start a new run on a suite they already have active (prevents confusion and data corruption).

**Why this priority**: Team testing is common, but conflicts are rare. Basic single-user execution works without this.

**Independent Test**: Can be tested by having two users start runs on different suites simultaneously, verifying both proceed independently.

**Acceptance Scenarios**:

1. **Given** user A has an active run on suite X, **When** user B starts a run on suite Y, **Then** both runs proceed independently
2. **Given** user A has an active run on suite X, **When** user A tries to start another run on suite X, **Then** the system rejects with "active run exists" and provides resume option
3. **Given** user A's run is paused, **When** user A starts a new run on the same suite, **Then** the system prompts to resume or cancel the existing run

---

### Edge Cases

- What happens when a tester loses connection mid-test? The test remains in progress; on reconnect, they can continue or re-mark it.
- How does the system handle corrupted state data? Validation on startup detects issues and reports them; runs can be cancelled and restarted.
- What if a test file is deleted during an active run? The test is marked as "unavailable" with explanation; run can continue with remaining tests.
- What happens when the test index is stale? The system validates index freshness and warns if tests have changed since run started.
- How are ties broken when multiple tests have no dependencies? Tests are ordered deterministically by ID to ensure consistent execution order.

## Requirements *(mandatory)*

### Functional Requirements

**Run Management**

- **FR-001**: System MUST allow users to list available test suites with test counts
- **FR-002**: System MUST allow users to start an execution run for a specific suite
- **FR-003**: System MUST support filtering runs by priority, tags, component, or explicit test IDs
- **FR-004**: System MUST allow users to pause an active run, preserving all state
- **FR-005**: System MUST allow users to resume a paused run from exactly where they left off
- **FR-006**: System MUST allow users to cancel a run, marking remaining tests as "not executed"
- **FR-007**: System MUST track which user started and is executing each run

**Test Execution**

- **FR-008**: System MUST present tests one at a time in dependency order
- **FR-009**: System MUST provide full test details (preconditions, steps, expected results) on request
- **FR-010**: System MUST accept test results: passed, failed, skipped (with reason), or blocked
- **FR-011**: System MUST automatically block tests when their dependencies fail, are skipped, or are blocked (transitive cascade)
- **FR-012**: System MUST allow notes to be attached to any test result
- **FR-013**: System MUST support retesting specific tests within an active run
- **FR-014**: System MUST track attempt number for retested tests

**State Management**

- **FR-015**: System MUST maintain execution state persistently (survives restarts)
- **FR-016**: System MUST prevent the same user from having multiple active runs on the same suite
- **FR-017**: System MUST support concurrent runs by different users on different suites
- **FR-018**: System MUST validate that test references are valid before allowing operations
- **FR-019**: System MUST enforce valid state transitions (cannot mark completed test as in-progress)

**Reporting**

- **FR-020**: System MUST generate a summary report in both JSON and Markdown formats when a run is finalized
- **FR-021**: System MUST include pass/fail/skip/blocked counts in reports
- **FR-022**: System MUST include all recorded notes in the final report
- **FR-023**: System MUST support viewing execution history for a suite (retained indefinitely, manual purge available)
- **FR-024**: System MUST track execution duration for runs and individual tests

**Security & Safety**

- **FR-025**: System MUST use opaque references for tests (not direct IDs) to prevent context manipulation
- **FR-026**: System MUST derive user identity from environment (git config user.name, fallback to OS username) and validate on each operation
- **FR-027**: System MUST reject operations on runs owned by other users
- **FR-028**: System MUST provide clear, self-contained responses that don't rely on conversation memory

**Observability**

- **FR-029**: System MUST provide structured logging to file with configurable verbosity levels (-v, -vv)

### Key Entities

- **Run**: A single execution session for a test suite, owned by one user, with status and progress tracking
- **Test Result**: The outcome of a single test execution, including status, notes, duration, and attempt number
- **Test Handle**: An opaque reference to a test within a run, used to prevent context forgery
- **Suite**: A collection of related test cases, sourced from the tests directory
- **Report**: A summary document generated when a run completes, containing all results and metadata

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Testers can complete a 20-test suite execution in under 30 minutes (excluding actual testing time)
- **SC-002**: System correctly handles pause/resume with zero data loss across 100 test runs
- **SC-003**: Dependency-based blocking works correctly for all test dependency configurations
- **SC-004**: System supports 5 concurrent users executing different suites without interference
- **SC-005**: 95% of testers successfully complete their first execution run without assistance
- **SC-006**: Reports accurately reflect all recorded results with zero discrepancies
- **SC-007**: System recovers gracefully from unexpected shutdowns with no corrupted state

## Assumptions

- Test suites are already created and validated by the Spectra CLI (Phase 1)
- `_index.json` metadata files exist and are current for each suite
- Users have appropriate access to the test repository
- The LLM orchestrator (Copilot, Claude, etc.) correctly forwards MCP tool calls
- Network connectivity is reliable during individual test operations (brief disconnections are recoverable)

## Dependencies

- Spectra.Core library (parsing, validation, models) from Phase 1
- Test suite files in `tests/{suite}/*.md` format
- Metadata index files `tests/{suite}/_index.json`

## Out of Scope

- Test case creation or modification (handled by Spectra CLI)
- Integration with external issue trackers (future Phase 3)
- Integration with notification systems (future Phase 3)
- Visual UI for test execution (future Phase 3)
- Automated test execution (this is for manual test execution only)
