# Feature Specification: Bundled Execution Agent & MCP Data Tools

**Feature Branch**: `007-execution-agent-mcp-tools`
**Created**: 2026-03-19
**Status**: Draft
**Input**: User description: "Create bundled SPECTRA execution agent prompts and add MCP data tools for test validation, index rebuilding, and coverage gap analysis."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Tests with AI Assistant (Priority: P1)

A QA engineer wants to execute manual tests interactively using an AI assistant (Copilot Chat, Claude, or any MCP-compatible client). They invoke the execution agent, select a test suite, and the assistant guides them through each test one at a time, collecting results and showing progress.

**Why this priority**: This is the core value proposition - enabling conversational test execution across multiple AI orchestrators without manual agent prompt setup.

**Independent Test**: Can be fully tested by initializing a repo with `spectra init`, opening Copilot Chat, invoking `@spectra-execution`, and executing a test suite end-to-end.

**Acceptance Scenarios**:

1. **Given** a repository initialized with SPECTRA, **When** the user invokes the execution agent in Copilot Chat, **Then** the agent presents available test suites and guides the user through test execution.
2. **Given** a test is presented, **When** the user says "it passed" or "failed, button was greyed out", **Then** the agent correctly interprets the natural language result and records the appropriate status.
3. **Given** a test suite execution is in progress, **When** the user completes a test, **Then** the agent shows progress (e.g., "Test 5/15 - 4 passed, 1 failed") before presenting the next test.

---

### User Story 2 - Validate Test Files (Priority: P1)

A developer modifies test files and wants to verify they conform to the SPECTRA test schema before committing. They use an MCP client (or CLI calling the MCP tool) to validate all test files and receive structured error messages for any issues.

**Why this priority**: Validation prevents broken test suites and is essential for CI integration and editor tooling.

**Independent Test**: Can be fully tested by creating test files with intentional schema violations and verifying the tool returns specific, actionable errors.

**Acceptance Scenarios**:

1. **Given** a suite with valid test files, **When** `validate_tests` is called, **Then** the tool returns success with count of validated files.
2. **Given** a suite with invalid test files (missing required fields, invalid frontmatter), **When** `validate_tests` is called, **Then** the tool returns structured errors listing file path, line number, and specific validation failure.
3. **Given** no test files exist, **When** `validate_tests` is called, **Then** the tool returns success with zero files validated.

---

### User Story 3 - Rebuild Index Files (Priority: P2)

A developer manually edits or adds test files outside the CLI workflow. They need to regenerate the `_index.json` files to ensure the indexes reflect the actual test files on disk.

**Why this priority**: Index consistency is required for test execution and other tools to function correctly, but is less frequently needed than validation.

**Independent Test**: Can be fully tested by manually adding a test file, calling `rebuild_indexes`, and verifying the new test appears in `_index.json`.

**Acceptance Scenarios**:

1. **Given** test files exist that are not in the index, **When** `rebuild_indexes` is called, **Then** the index is updated to include all test files.
2. **Given** index entries exist for deleted test files, **When** `rebuild_indexes` is called, **Then** the orphaned entries are removed from the index.
3. **Given** multiple suites exist, **When** `rebuild_indexes` is called with a suite filter, **Then** only the specified suite's index is rebuilt.

---

### User Story 4 - Analyze Coverage Gaps (Priority: P2)

A QA lead wants to identify which documentation areas lack test coverage. They call the coverage analysis tool to compare the docs folder against existing tests and receive a list of uncovered areas.

**Why this priority**: Coverage analysis enables test planning and prioritization but is not required for core test execution workflows.

**Independent Test**: Can be fully tested by creating documentation files, some with matching tests (via `source_refs`) and some without, then verifying the tool correctly identifies uncovered documents.

**Acceptance Scenarios**:

1. **Given** documentation files exist with no corresponding tests, **When** `analyze_coverage_gaps` is called, **Then** the tool returns those documents as uncovered areas.
2. **Given** tests exist with `source_refs` pointing to documentation, **When** `analyze_coverage_gaps` is called, **Then** those documents are excluded from the uncovered list.
3. **Given** a specific suite is requested, **When** `analyze_coverage_gaps` is called with the suite filter, **Then** only coverage for that suite's scope is analyzed.

---

### User Story 5 - Initialize Repository with Agent Files (Priority: P2)

A developer sets up SPECTRA in a new repository and wants the execution agent prompt files installed automatically. Running `spectra init` creates the agent files in the standard locations for Copilot and other orchestrators.

**Why this priority**: Setup is a one-time operation but enables all agent-based workflows.

**Independent Test**: Can be fully tested by running `spectra init` in a fresh repository and verifying agent files are created at expected paths.

**Acceptance Scenarios**:

1. **Given** a repository without SPECTRA agent files, **When** `spectra init` is run, **Then** agent files are created at `.github/agents/spectra-execution.agent.md` and `.github/skills/spectra-execution/SKILL.md`.
2. **Given** agent files already exist, **When** `spectra init` is run, **Then** existing files are preserved (not overwritten) unless `--force` flag is used.
3. **Given** the `--force` flag is provided, **When** `spectra init` is run, **Then** agent files are overwritten with the latest version.

---

### User Story 6 - Log Bugs from Failed Tests (Priority: P3)

A QA engineer executes tests and discovers failures. When a test fails, the execution agent offers to create a bug in Azure DevOps (if Azure DevOps MCP is connected) with pre-populated details from the test case and failure information.

**Why this priority**: Bug logging integration is valuable but requires external MCP server (Azure DevOps) and is not core to test execution.

**Independent Test**: Can be tested by failing a test during execution, accepting the bug creation prompt, and verifying the bug is created with correct details in Azure DevOps.

**Acceptance Scenarios**:

1. **Given** a test fails and Azure DevOps MCP is connected, **When** the user confirms bug creation, **Then** a work item is created with title "[SPECTRA] {test_title} - {failure_summary}", test ID, steps, expected vs actual, and user comment.
2. **Given** a test fails and no bug tracker MCP is connected, **When** the execution completes, **Then** the agent suggests manual bug logging and provides copyable details.

---

### Edge Cases

- What happens when a test file has YAML frontmatter syntax errors? The validation tool returns a specific parse error with file path and line number.
- What happens when documentation folder doesn't exist? The coverage analysis tool returns an informative message indicating no documentation was found.
- What happens when the user is in a non-interactive environment (CI)? The agent files are designed for interactive use; CI pipelines should use CLI commands directly.
- What happens when `source_refs` in tests point to non-existent documents? The coverage analysis ignores invalid references and logs a warning.

## Requirements *(mandatory)*

### Functional Requirements

**Agent Prompt Files**

- **FR-001**: System MUST provide a bundled execution agent prompt at `.github/agents/spectra-execution.agent.md`
- **FR-002**: System MUST provide the same prompt content as a Copilot Skill at `.github/skills/spectra-execution/SKILL.md`
- **FR-003**: Agent prompt MUST define the execution workflow: list suites, start run, present test, collect result, advance, show progress, finalize
- **FR-004**: Agent prompt MUST specify presentation rules: one test at a time, numbered steps with action verbs, progress shown after each result
- **FR-005**: Agent prompt MUST define natural language result mapping: "it passed" to PASS, "failed, X was wrong" to FAIL with comment, "staging is down" to BLOCKED, "not applicable" to SKIP
- **FR-006**: Agent prompt MUST include bug logging integration via Azure DevOps MCP with title format "[SPECTRA] {test_title} - {failure_summary}"

**Init Command**

- **FR-007**: `spectra init` command MUST install agent prompt files into the target repository
- **FR-008**: `spectra init` MUST NOT overwrite existing agent files unless `--force` flag is provided
- **FR-009**: Installed agent files MUST be identical to the bundled source files

**MCP Data Tools**

- **FR-010**: System MUST provide `validate_tests` MCP tool that validates test files against the SPECTRA schema
- **FR-011**: `validate_tests` MUST return structured errors with file path, line number (when available), field name, and error description
- **FR-012**: `validate_tests` MUST accept optional `suite` parameter to validate a specific suite only
- **FR-013**: System MUST provide `rebuild_indexes` MCP tool that regenerates `_index.json` files from test files on disk
- **FR-014**: `rebuild_indexes` MUST add new files, update changed files, and remove entries for deleted files
- **FR-015**: `rebuild_indexes` MUST accept optional `suite` parameter to rebuild a specific suite only
- **FR-016**: System MUST provide `analyze_coverage_gaps` MCP tool that compares documentation against test `source_refs`
- **FR-017**: `analyze_coverage_gaps` MUST return uncovered documentation files with path and title
- **FR-018**: `analyze_coverage_gaps` MUST accept optional `suite` parameter to analyze coverage for a specific suite
- **FR-019**: All MCP data tools MUST be deterministic (no AI, no model dependency)
- **FR-020**: All MCP data tools MUST use the standard `McpToolResponse<T>` format

**Documentation**

- **FR-021**: System MUST document how to use the execution agent with GitHub Copilot Chat in VS Code
- **FR-022**: System MUST document how to use the execution agent with GitHub Copilot CLI
- **FR-023**: System MUST document how to use the execution agent with Claude
- **FR-024**: System MUST document how to use the execution agent with generic MCP clients

### Key Entities

- **Agent Prompt**: Markdown file with YAML frontmatter (name, description) and prompt content defining workflow, presentation rules, and result mapping
- **Validation Error**: Structured object containing file path, line number, field name, error code, and human-readable message
- **Coverage Gap**: Uncovered documentation area identified by document path, title, and severity (based on document size/complexity)
- **Index Entry**: Test metadata extracted from test file frontmatter (id, title, priority, tags, component, source_refs)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can execute test suites interactively via Copilot Chat or Claude within 2 minutes of initializing a repository
- **SC-002**: Validation tool identifies 100% of schema violations in test files with actionable error messages
- **SC-003**: Index rebuild accurately reflects all test files on disk with zero orphaned or missing entries
- **SC-004**: Coverage analysis correctly identifies all documentation files not referenced by any test's `source_refs`
- **SC-005**: Agent prompt works without modification across Copilot Chat (VS Code), Copilot CLI, and Claude
- **SC-006**: All three MCP data tools execute in under 5 seconds for repositories with up to 500 test files
- **SC-007**: Documentation enables users to configure and use the execution agent within 10 minutes

## Assumptions

- Azure DevOps MCP server is a separate, optional dependency for bug logging integration
- Existing MCP infrastructure (protocol, server, tool registry) supports adding new tools without architectural changes
- Test file schema is well-defined and validation rules can be codified
- Documentation files are in the `docs/` folder with `.md` extension (standard SPECTRA convention)
- Users are familiar with their chosen AI orchestrator (Copilot, Claude) and can invoke agents/skills

## Dependencies

- Existing Spectra.MCP project structure and `IMcpTool` interface
- Existing `IndexWriter` for reading/writing `_index.json` files
- Existing test file parser for extracting frontmatter metadata
- Existing document scanner for enumerating documentation files
