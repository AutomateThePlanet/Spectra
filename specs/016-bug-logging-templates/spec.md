# Feature Specification: Bug Logging, Templates, and Execution Agent Integration

**Feature Branch**: `016-bug-logging-templates`
**Created**: 2026-03-22
**Status**: Draft
**Input**: User description: "Add integrated bug logging to SPECTRA execution workflow with customizable templates and tracker integration"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Log a Bug During Test Execution (Priority: P1)

A tester is executing test cases through the MCP execution engine. When they mark a test as FAILED, the execution agent offers to log a bug. The agent pre-fills a bug report with test case details (ID, title, steps to reproduce, expected result, screenshots, environment, traceability) using a customizable Markdown template. The tester reviews the draft, optionally edits it, and confirms submission. The bug is created in the connected tracker and its reference is recorded in the test execution notes.

**Why this priority**: This is the core value proposition. Without this, testers must manually switch to their bug tracker, re-type context, and lose execution flow. This single story eliminates that friction.

**Independent Test**: Can be fully tested by running an execution, failing a test, and verifying the agent offers bug logging, pre-fills data correctly, and records the bug reference. Delivers immediate value even without tracker integration (local fallback).

**Acceptance Scenarios**:

1. **Given** a test case is marked as FAILED during execution, **When** the agent asks "Would you like to log a bug?" and the tester says yes, **Then** the agent gathers test case details and shows a pre-filled bug report for review.
2. **Given** a pre-filled bug report is shown, **When** the tester confirms submission, **Then** the bug is created in the configured tracker (or saved locally) and the bug ID/URL is recorded in the test execution notes via `add_test_note`.
3. **Given** a test case is marked as FAILED, **When** the tester declines to log a bug, **Then** execution continues to the next test without interruption.
4. **Given** screenshots were attached during execution of the failed test, **When** the bug report is generated, **Then** screenshots are automatically included in the report.

---

### User Story 2 - Bug Report Template Initialization and Customization (Priority: P2)

A tester runs `spectra init` to set up their project. The init process creates a default bug report template at `templates/bug-report.md` with placeholder variables and adds a `bug_tracking` configuration section to `spectra.config.json`. The tester can later customize the template to match their team's bug reporting standards, or delete it entirely without breaking anything.

**Why this priority**: The template provides structure and consistency to bug reports. However, the agent can compose reports without a template, so this enhances but doesn't gate the core flow.

**Independent Test**: Can be tested by running `spectra init` and verifying the template file and config section are created with correct defaults. Template customization can be tested by editing the template and verifying the agent uses the modified format.

**Acceptance Scenarios**:

1. **Given** a user runs `spectra init` in a new project, **When** initialization completes, **Then** `templates/bug-report.md` exists with the default template content and `spectra.config.json` includes a `bug_tracking` section with default values.
2. **Given** the user has customized `templates/bug-report.md` with additional sections, **When** a bug is logged, **Then** the agent uses the customized template and populates the standard `{{variable}}` placeholders.
3. **Given** the user has deleted `templates/bug-report.md`, **When** a bug is logged, **Then** the agent composes the bug report directly from test case data without error.

---

### User Story 3 - Bug Tracker Auto-Detection and Configuration (Priority: P2)

A tester has connected their project to a bug tracker via MCP (Azure DevOps, Jira, or GitHub Issues). When logging a bug, the agent automatically detects the available tracker and submits the bug to it. The tester can also explicitly configure a preferred tracker in `spectra.config.json` or force local-only mode.

**Why this priority**: Tracker integration is what closes the loop from test failure to bug ticket. It shares P2 with template customization because both enhance the core logging flow.

**Independent Test**: Can be tested by configuring different `bug_tracking.provider` values and verifying the agent routes bug reports to the correct tracker, or falls back to local files when no tracker is available.

**Acceptance Scenarios**:

1. **Given** `bug_tracking.provider` is set to `"auto"` and Azure DevOps MCP tools are available, **When** a bug is logged, **Then** the agent creates a Work Item of type Bug in Azure DevOps.
2. **Given** `bug_tracking.provider` is set to `"auto"` and only GitHub MCP tools are available, **When** a bug is logged, **Then** the agent creates a GitHub Issue with the `bug` label.
3. **Given** `bug_tracking.provider` is set to `"jira"`, **When** a bug is logged, **Then** the agent creates a Jira Issue of type Bug regardless of other available MCPs.
4. **Given** no bug tracker MCP is available, **When** a bug is logged, **Then** the agent saves the bug report as a Markdown file in `reports/{run_id}/bugs/` with attachments in a subdirectory.
5. **Given** `bug_tracking.provider` is set to `"local"`, **When** a bug is logged, **Then** the agent saves locally even if tracker MCPs are available.

---

### User Story 4 - Execution Agent Prompt Updates (Priority: P3)

The bundled execution agent prompt at `.github/agents/spectra-execution.agent.md` is updated with a Bug Logging section that instructs the agent on the full bug logging workflow. Optionally, a Copilot Chat skill is created at `.github/skills/spectra-bug-logging/SKILL.md` for teams using Copilot Agent Skills.

**Why this priority**: The agent prompt is the mechanism that enables the behavior, but it's an authoring/configuration artifact rather than user-facing functionality. The core behavior is defined by the flow; the prompt encodes it.

**Independent Test**: Can be tested by reviewing the agent prompt for completeness and by running an end-to-end execution with the updated agent to verify it follows the bug logging instructions.

**Acceptance Scenarios**:

1. **Given** the execution agent prompt is updated, **When** a test fails during execution, **Then** the agent follows the bug logging flow as specified in the prompt.
2. **Given** a Copilot Chat skill is created, **When** a user asks Copilot to log a bug for a failed test, **Then** the skill reads test details, populates the template, and creates the issue.

---

### User Story 5 - Suppress Bug Prompts (Priority: P3)

A tester running a large regression suite does not want to be asked about bug logging after every failure. They set `bug_tracking.auto_prompt_on_failure` to `false` in config. The agent only logs bugs when the tester explicitly requests it during execution.

**Why this priority**: This is a workflow refinement for power users running large suites. Important for usability but not for initial delivery.

**Independent Test**: Can be tested by setting `auto_prompt_on_failure` to `false`, failing a test, and verifying the agent does not prompt for bug logging. Then explicitly requesting a bug log and verifying it works.

**Acceptance Scenarios**:

1. **Given** `auto_prompt_on_failure` is `false`, **When** a test is marked as FAILED, **Then** the agent records the failure and moves to the next test without offering bug logging.
2. **Given** `auto_prompt_on_failure` is `false`, **When** the tester explicitly asks the agent to log a bug for a previously failed test, **Then** the agent gathers the test details and follows the standard bug logging flow.

---

### Edge Cases

- What happens when the template contains custom `{{variables}}` not recognized by the agent? The agent leaves unrecognized placeholders as-is for the user to fill in manually.
- What happens when a test fails but has no steps defined (e.g., a minimal test case)? The agent populates available fields and marks missing sections with placeholder text.
- What happens when the bug tracker MCP is available but the API call to create the issue fails? The agent reports the error to the tester and offers to save the bug report locally as a fallback.
- What happens when multiple bug tracker MCPs are available and provider is set to `"auto"`? The agent uses the priority order: Azure DevOps > Jira > GitHub Issues, and informs the tester which tracker was selected.
- What happens when screenshots are referenced but the files no longer exist on disk? The agent notes the missing attachments in the bug report and continues without them.
- What happens when the tester marks multiple tests as FAILED in rapid succession (e.g., bulk record)? Each failure that was individually advanced triggers the bug prompt. Bulk-recorded failures do not trigger individual prompts; instead the agent presents a consolidated selection prompt listing all failures, letting the tester choose which ones get bugs. Each selected failure produces an individual bug (one bug per test, not one consolidated ticket).
- What happens when the same test case has an existing open bug in the tracker? The agent detects the existing bug, shows it to the tester, and offers to link to it rather than creating a duplicate. The tester can still choose to create a new bug if the failure is a different issue.

## Clarifications

### Session 2026-03-22

- Q: Should the agent check for existing open bugs for the same test case before creating a new one? → A: Yes. Check for existing open bugs matching the test ID before creating; link to existing bug if found. The agent proactively offers bug logging on failure (not only when user asks).
- Q: Should created bugs be linked back to test case frontmatter? → A: Yes. Add a `bugs` field to test case frontmatter; write bug IDs back after creation, following the `automated_by`/`requirements` pattern.
- Q: For bulk-recorded failures, should the agent create one bug per failed test or one consolidated bug? → A: One bug per failed test. The consolidated prompt lets the tester select which failures get bugs, then creates individual bugs for each selected failure.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST offer to log a bug when a test case is marked as FAILED via `advance_test_case`, unless `auto_prompt_on_failure` is `false`.
- **FR-002**: System MUST pre-fill bug reports with data from the test case frontmatter and execution context (test ID, title, suite, environment, steps, expected result, run ID, screenshots, source refs, requirements, component).
- **FR-003**: System MUST support a customizable Markdown template at a configurable path (default: `templates/bug-report.md`) with `{{variable}}` placeholder substitution.
- **FR-004**: System MUST gracefully degrade when the template file does not exist by composing the bug report directly from test case data.
- **FR-005**: System MUST detect available bug tracker MCPs and route bug creation accordingly when `provider` is set to `"auto"`.
- **FR-006**: System MUST support explicit provider configuration (`"azure-devops"`, `"jira"`, `"github"`, `"local"`) to override auto-detection.
- **FR-007**: System MUST save bug reports as local Markdown files in `reports/{run_id}/bugs/` when no bug tracker is available or when `provider` is `"local"`.
- **FR-008**: System MUST record the created bug ID or URL in the test execution notes via `add_test_note` after successful bug creation.
- **FR-009**: System MUST automatically attach screenshots captured during test execution to the bug report when `auto_attach_screenshots` is `true`.
- **FR-010**: System MUST create the default bug report template and `bug_tracking` configuration section during `spectra init`.
- **FR-011**: System MUST derive bug severity from the test case `priority` field (high to critical, medium to major, low to minor) when not specified interactively.
- **FR-012**: System MUST show the populated bug report to the tester for review and confirmation before submitting to the tracker.
- **FR-013**: System MUST update the execution agent prompt (`.github/agents/spectra-execution.agent.md`) with bug logging instructions.
- **FR-014**: System MUST fall back to local file storage when a bug tracker API call fails, after notifying the tester of the error.
- **FR-015**: System MUST check the configured bug tracker for existing open bugs matching the failed test case ID before creating a new bug. If a match is found, the agent MUST present the existing bug to the tester and offer to link to it instead of creating a duplicate.
- **FR-016**: System MUST write the created bug ID (or URL) back to the test case frontmatter in a `bugs` field (list of strings) after successful bug creation, following the established `automated_by` and `requirements` linkage pattern. This enables duplicate detection in local-only mode without querying a tracker.

### Key Entities

- **Bug Report**: A structured document capturing failure details — title, test case reference, steps to reproduce, expected vs. actual result, screenshots, severity, environment, and traceability links. Created from a template or composed directly.
- **Bug Report Template**: A Markdown file with `{{variable}}` placeholders that defines the structure and content of bug reports. Optional, customizable, created by `spectra init`.
- **Bug Tracking Configuration**: Settings in `spectra.config.json` controlling provider selection, template path, default severity, screenshot attachment, and auto-prompting behavior.
- **Local Bug File**: A Markdown file saved in `reports/{run_id}/bugs/` when no external tracker is available, containing the full bug report content with attachments in a subdirectory.
- **Test Case `bugs` Field**: A list of bug IDs or URLs in the test case YAML frontmatter, written back after bug creation. Enables traceability and local duplicate detection without querying the tracker.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Testers can log a bug from a failed test without leaving the execution flow, completing the entire process (failure to bug ticket) in under 30 seconds.
- **SC-002**: Bug reports contain all available test case context (ID, title, steps, expected result, screenshots, traceability) with zero manual re-entry of data already captured in the test case.
- **SC-003**: The system works in all template states (default, customized, deleted) without errors or loss of functionality.
- **SC-004**: Bug reports are successfully created in at least three external trackers (Azure DevOps, Jira, GitHub Issues) when their respective MCPs are available.
- **SC-005**: When no external tracker is available, bug reports are saved locally in a structured format that can be committed to version control and converted to issues later.
- **SC-006**: Testers running large suites can suppress automatic bug prompts and log bugs on demand without impacting execution throughput.
- **SC-007**: `spectra init` creates the template and configuration with correct defaults in a single operation, requiring no additional setup for basic bug logging.

## Assumptions

- Bug tracker MCPs (Azure DevOps, Jira, GitHub) follow standard MCP tool patterns and are available as separate MCP server configurations the user connects independently.
- The execution agent has access to all MCP tools registered in the session, including both Spectra MCP tools and any connected bug tracker MCP tools.
- Screenshots are stored on the local file system during execution and are accessible by path when composing bug reports.
- The `advance_test_case` MCP tool is the primary mechanism for recording test failures; `bulk_record_results` handles batch failures differently (one consolidated prompt rather than per-test prompts).
- Template variable syntax uses double curly braces (`{{variable}}`) which is unlikely to conflict with test case content.
- Local bug files in `reports/{run_id}/bugs/` follow the same report output conventions as existing execution reports.
