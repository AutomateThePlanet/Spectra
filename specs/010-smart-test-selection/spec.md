# Feature Specification: Smart Test Selection

**Feature Branch**: `010-smart-test-selection`
**Created**: 2026-03-20
**Status**: Draft
**Input**: User description: "Smart cross-suite test selection that lets users describe WHAT they want to test instead of manually picking suites and filters. The intelligence lives in the agent prompt (orchestrator layer), while the MCP server provides deterministic filtering and data access tools."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cross-Suite Test Search and Filtering (Priority: P1)

A test executor wants to find tests across all suites by metadata — priority, tags, components, or keywords — without manually opening each suite's index. They invoke the `find_test_cases` tool with filter criteria and receive a consolidated, metadata-rich list of matching tests with estimated total duration.

**Why this priority**: This is the foundational capability that all other stories depend on. Without cross-suite search, users cannot discover or select tests outside a single suite.

**Independent Test**: Can be fully tested by calling `find_test_cases` with various filter combinations and verifying correct matches are returned with accurate metadata.

**Acceptance Scenarios**:

1. **Given** multiple suites with indexed test cases, **When** the user calls `find_test_cases` with `tags: ["payment"]`, **Then** all tests tagged "payment" across all suites are returned with their full metadata (id, suite, title, description, priority, tags, component, estimated_duration).
2. **Given** multiple filter types are provided (tags AND priorities), **When** `find_test_cases` is called with `tags: ["payment"], priorities: ["high"]`, **Then** only tests matching ALL filter types are returned (AND between types, OR within arrays).
3. **Given** a free-text query is provided, **When** `find_test_cases` is called with `query: "timeout retry"`, **Then** tests whose title, description, or tags contain ANY matching keyword are returned (OR logic, case-insensitive), ranked by number of keyword hits.
4. **Given** `max_results` is set to 10 and 25 tests match, **When** `find_test_cases` is called, **Then** only the top 10 results are returned (ordered by priority descending, then suite name, then original index order) along with `matched: 25` indicating total matches. When a free-text query is present, keyword hit count takes precedence as the primary sort.
5. **Given** `has_automation: true` is specified, **When** `find_test_cases` is called, **Then** only tests with a non-empty `automated_by` field are returned.
6. **Given** no filters match any tests, **When** `find_test_cases` is called, **Then** an empty result set is returned with `matched: 0` and `total_estimated_duration: "0m"`.

---

### User Story 2 - Start Execution Run with Custom Test IDs (Priority: P1)

A test executor has identified specific tests (from search results or their own knowledge) and wants to start an execution run with an arbitrary list of test IDs spanning multiple suites. They pass `test_ids` to `start_execution_run` and the system queues those specific tests in the given order.

**Why this priority**: This completes the core selection-to-execution flow. Without this, search results cannot be acted upon.

**Independent Test**: Can be tested by calling `start_execution_run` with a list of test IDs from different suites and verifying the run starts with those exact tests in order.

**Acceptance Scenarios**:

1. **Given** a list of valid test IDs from different suites, **When** `start_execution_run` is called with `test_ids: ["TC-134", "TC-100", "TC-201"]`, **Then** a new execution run is created with those tests queued in the specified order.
2. **Given** one or more test IDs do not exist in any suite index, **When** `start_execution_run` is called, **Then** the tool returns an error listing the invalid IDs before any run is started.
3. **Given** `test_ids` is provided along with `suite`, **When** `start_execution_run` is called, **Then** an error is returned indicating the parameters are mutually exclusive.
4. **Given** `test_ids` is provided without a `name`, **When** `start_execution_run` is called, **Then** an error is returned requiring a descriptive run name for custom selections.
5. **Given** a valid `test_ids` list, **When** the run starts, **Then** the run metadata records which suites the tests originated from.

---

### User Story 3 - Test Execution History for Prioritization (Priority: P2)

A test executor wants to see execution history for specific tests to make informed decisions about what to run. They call `get_test_execution_history` and receive per-test statistics including last execution date, pass/fail status, total runs, and pass rate.

**Why this priority**: Enables risk-based test selection but is not required for basic search-and-run workflows.

**Independent Test**: Can be tested by querying execution history for tests that have been run previously and verifying accurate statistics are returned.

**Acceptance Scenarios**:

1. **Given** specific test IDs that have execution history, **When** `get_test_execution_history` is called with those IDs, **Then** each test's last execution date, last status, total runs, pass rate, and last run ID are returned.
2. **Given** a test ID with no execution history, **When** `get_test_execution_history` is called, **Then** that test's entry shows `last_executed: null`, `total_runs: 0`, `pass_rate: null`.
3. **Given** no `test_ids` parameter is provided, **When** `get_test_execution_history` is called, **Then** history for all tests with at least one execution is returned.
4. **Given** a `limit` of 5 is specified, **When** history is retrieved, **Then** only the 5 most recent executions per test are considered for statistics.

---

### User Story 4 - Saved Selections in Configuration (Priority: P2)

A team lead defines reusable test selections (e.g., "smoke", "payment-regression", "pre-release") in the project configuration. Team members can list available selections and start runs using a selection name instead of manually specifying filters each time.

**Why this priority**: Reduces repetitive filter specification for common testing workflows. Valuable but not required for basic operation.

**Independent Test**: Can be tested by configuring saved selections in spectra.config.json, calling `list_saved_selections` to verify they appear, and calling `start_execution_run` with a selection name to verify the correct tests are queued.

**Acceptance Scenarios**:

1. **Given** spectra.config.json contains a `selections` section with entries, **When** `list_saved_selections` is called, **Then** all saved selections are returned with their name, description, filters, estimated test count, and estimated duration.
2. **Given** a saved selection named "smoke" exists, **When** `start_execution_run` is called with `selection: "smoke"`, **Then** the system applies the selection's filters across all suites and starts a run with the matching tests.
3. **Given** a selection name that does not exist in config, **When** `start_execution_run` is called with that selection, **Then** an error is returned listing available selection names.
4. **Given** `spectra init` is run on a new project, **Then** the generated spectra.config.json includes a sample `selections` section with a "smoke" example.

---

### User Story 5 - Test Description Field for Better Search (Priority: P3)

A test author wants to add a short description to test cases that provides more context than the title alone. This description is indexed and used by `find_test_cases` for better keyword matching.

**Why this priority**: Improves search quality but the system works without it. Title and tag matching provide adequate search for most cases.

**Independent Test**: Can be tested by adding a `description` field to test YAML frontmatter, rebuilding the index, and verifying `find_test_cases` matches on description keywords.

**Acceptance Scenarios**:

1. **Given** a test case with a `description` field in its YAML frontmatter, **When** `find_test_cases` is called with a query matching words only in the description, **Then** that test is included in results.
2. **Given** a test case without a `description` field, **When** it is parsed and indexed, **Then** parsing succeeds and the test is searchable by title and tags as before.
3. **Given** the `description` field is present, **When** the test index is rebuilt, **Then** the description appears in the `_index.json` entry for that test.

---

### User Story 6 - Agent-Driven Smart Selection via Prompts (Priority: P3)

An execution agent (AI orchestrator) interprets natural-language user requests like "run payment tests" or "what should I test before release?" and translates them into appropriate MCP tool calls. The agent prompt provides step-by-step guidance for intent parsing, saved selection matching, test discovery, grouped presentation, and risk-based recommendations using execution history.

**Why this priority**: The MCP tools are fully functional without agent prompt updates. This story enhances the user experience for AI-assisted workflows but is not required for programmatic or manual tool usage.

**Independent Test**: Can be tested by verifying the agent prompt file contains the documented selection workflow steps and example conversations.

**Acceptance Scenarios**:

1. **Given** an agent prompt file exists, **When** the smart selection feature is complete, **Then** the prompt includes step-by-step instructions for intent parsing, saved selection checking, test discovery, grouped result presentation, user adjustment, and run start.
2. **Given** the prompt includes risk-based recommendation guidance, **When** an agent follows it, **Then** it can call `find_test_cases` and `get_test_execution_history` and present tests grouped by risk category (never executed, last failed, not run recently, recently passed).

---

### Edge Cases

- What happens when a suite's `_index.json` is missing or malformed? The tool skips that suite and includes a warning in the response.
- What happens when `find_test_cases` is called with no filters at all? All tests across all suites are returned (up to `max_results`).
- What happens when a saved selection's filters match zero tests? The tool returns the selection metadata with `estimated_test_count: 0` and a message indicating no tests currently match.
- What happens when `test_ids` in `start_execution_run` contains duplicates? Duplicates are removed, preserving the first occurrence's position in the order.
- What happens when estimated_duration is missing from a test? That test contributes "0m" to the total, and the response notes the duration is estimated.
- What happens when the execution database is empty or missing? `get_test_execution_history` returns null/zero values for all requested tests without error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `find_test_cases` MCP tool that searches and filters test cases across all suites by query text, suite names, priorities, tags, components, and automation status.
- **FR-002**: System MUST apply AND logic between different filter types and OR logic within filter value arrays. Free-text query matching uses OR logic across keywords (case-insensitive), with results ranked by keyword hit count.
- **FR-003**: System MUST return matched tests with full metadata: id, suite, title, description, priority, tags, component, and estimated_duration. Default ordering is priority descending, then suite name, then original index order. When a free-text query is present, keyword hit count is the primary sort key.
- **FR-004**: System MUST provide a `get_test_execution_history` MCP tool that returns per-test execution statistics from the execution database.
- **FR-005**: System MUST provide a `list_saved_selections` MCP tool that reads saved test selections from spectra.config.json and returns them with estimated test counts.
- **FR-006**: System MUST extend `start_execution_run` to accept a `test_ids` parameter for cross-suite test execution with specified order.
- **FR-007**: System MUST extend `start_execution_run` to accept a `selection` parameter that resolves to test IDs via saved selection filters.
- **FR-008**: System MUST enforce mutual exclusivity between `suite`, `test_ids`, and `selection` parameters in `start_execution_run`.
- **FR-009**: System MUST validate all test IDs exist before starting a run and return errors listing any invalid IDs.
- **FR-010**: System MUST support an optional `description` field in test case YAML frontmatter that is indexed and searchable.
- **FR-011**: System MUST include a sample `selections` section in the default configuration generated by `spectra init`.
- **FR-012**: All new MCP tools MUST be purely deterministic with no AI/LLM calls — filtering, querying, and config reading only.
- **FR-013**: System MUST provide agent prompt documentation describing the smart selection workflow for AI orchestrators.

### Key Entities

- **TestSearchResult**: A test case matched by `find_test_cases`, containing full metadata (id, suite, title, description, priority, tags, component, estimated_duration).
- **TestExecutionHistory**: Per-test execution statistics (last_executed, last_status, total_runs, pass_rate, last_run_id).
- **SavedSelection**: A named, reusable set of test filters stored in configuration (name, description, filter criteria).
- **SelectionFilters**: The filter criteria for a saved selection (tags, priorities, components, has_automation).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can find relevant tests across all suites in a single operation, reducing multi-suite test discovery from multiple manual lookups to one tool call.
- **SC-002**: Users can start execution runs with arbitrary cross-suite test lists, removing the single-suite limitation for custom test plans.
- **SC-003**: Saved selections allow teams to execute common test plans (smoke, regression, pre-release) by name, reducing repeated filter specification to zero.
- **SC-004**: Execution history queries return accurate per-test statistics enabling data-driven prioritization of test runs.
- **SC-005**: All new MCP tools respond within 2 seconds for repositories with up to 500 test cases across 20 suites.
- **SC-006**: The `find_test_cases` tool returns zero false positives — every returned test matches the specified filter criteria.
- **SC-007**: 100% of existing test execution workflows (single-suite runs) continue working without modification after the update to `start_execution_run`.

## Clarifications

### Session 2026-03-20

- Q: When a multi-word free-text query is provided, should ALL keywords match (AND) or ANY keyword match (OR)? → A: OR matching — any keyword match returns the test, results ranked by number of keyword hits.
- Q: When max_results truncates and there is no free-text query, what ordering determines which tests are returned? → A: Priority descending, then suite name, then original index order. Free-text query keyword hit count takes precedence when present.

## Assumptions

- Test suites use `_index.json` files as the authoritative source of test metadata (existing Spectra convention).
- The SQLite execution database (`.execution/spectra.db`) is the source of execution history data.
- The `estimated_duration` field in test metadata uses a human-readable format (e.g., "5m", "1h 30m") and may be absent from older tests.
- The `description` field is optional and backward-compatible — tests without it parse and index correctly.
- Agent prompt files are Markdown documents that guide AI orchestrator behavior but do not affect MCP server functionality.
- Saved selections are team-level configuration, not per-user.

## Scope Boundaries

### In Scope
- Three new MCP tools: `find_test_cases`, `get_test_execution_history`, `list_saved_selections`
- Extension of `start_execution_run` with `test_ids` and `selection` parameters
- Optional `description` field in test YAML frontmatter and index
- Saved selections in spectra.config.json
- Agent prompt documentation for smart selection workflow

### Out of Scope
- AI/ML-based test prioritization algorithms in the MCP server
- Automatic test selection without user confirmation
- Integration with external test management tools
- Test flakiness detection or statistical analysis beyond pass rate
- UI/dashboard changes for test selection
