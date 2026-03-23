# Feature Specification: Undocumented Behavior Test Cases

**Feature Branch**: `018-undocumented-tests`
**Created**: 2026-03-23
**Status**: Draft
**Input**: User description: "Add ability to create test cases from undocumented behavior descriptions via the generation agent"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Describe Undocumented Behavior and Get a Test Case (Priority: P1)

A tester knows about a behavior that isn't documented — perhaps discovered during a production incident, learned from tribal knowledge, or communicated verbally by a stakeholder. The tester describes this behavior conversationally to the generation agent, which asks targeted clarifying questions (only what's missing), checks for duplicate test cases, generates a properly structured test case with special metadata marking it as user-described, and shows a draft for review before saving.

**Why this priority**: This is the core value proposition. Without this flow, undocumented behaviors cannot be captured in Spectra without first writing formal documentation — a friction point that causes knowledge loss.

**Independent Test**: Can be fully tested by describing a behavior to the generation agent and verifying a complete test case file is created with correct frontmatter (`grounding.verdict: "manual"`, `source: "user-described"`, empty `source_refs`), proper markdown structure, and updated suite index.

**Acceptance Scenarios**:

1. **Given** a tester is interacting with the generation agent, **When** they describe an undocumented behavior with sufficient detail (component, steps, expected result, priority, suite), **Then** the agent generates a complete test case without asking unnecessary clarifying questions, shows the draft for confirmation, and saves it to the correct suite folder upon approval.
2. **Given** a tester describes an undocumented behavior with incomplete details (e.g., missing component or priority), **When** the agent processes the description, **Then** it asks only the missing clarifying questions (not all possible questions) before generating the test case.
3. **Given** the agent has generated a draft test case, **When** the tester requests changes, **Then** the agent updates the draft and shows the revised version for re-approval before saving.
4. **Given** a test case is saved from an undocumented behavior description, **When** the file is written, **Then** the frontmatter contains `source_refs: []`, `grounding.verdict: "manual"`, `grounding.source: "user-described"`, `grounding.created_by` with the current user identity, and an optional `grounding.note`.

---

### User Story 2 - Duplicate Detection Before Creating Undocumented Tests (Priority: P1)

Before creating a new test case from an undocumented behavior, the agent searches existing test cases for similar scenarios. If a similar (but not identical) test exists, the agent informs the tester and proceeds with creation. If an exact duplicate exists, the agent offers choices: update the existing test, create a new one anyway, or cancel.

**Why this priority**: Without duplicate detection, testers would unknowingly create redundant test cases, increasing maintenance burden and cluttering test suites.

**Independent Test**: Can be tested by describing a behavior that matches an existing test case and verifying the agent shows the match and offers appropriate options.

**Acceptance Scenarios**:

1. **Given** existing test cases in a suite, **When** the tester describes a behavior that partially overlaps with an existing test, **Then** the agent shows the similar test case with an explanation of the overlap and proceeds to create the new test.
2. **Given** existing test cases in a suite, **When** the tester describes a behavior that exactly matches an existing test, **Then** the agent shows the duplicate and offers three options: update the existing test, create a new one anyway, or cancel.
3. **Given** no similar test cases exist, **When** the agent performs the duplicate check, **Then** it proceeds directly to test case generation without interrupting the user.

---

### User Story 3 - Critic Verification Bypass for Manual Tests (Priority: P1)

Tests created from undocumented behaviors have no documentation to verify against. When the critic verification step runs during test generation, tests with `grounding.verdict: "manual"` are automatically skipped — they are not flagged as ungrounded or invalid.

**Why this priority**: Without this bypass, every undocumented test would fail critic verification (since there's no source document), making the feature unusable in workflows that include critic verification.

**Independent Test**: Can be tested by creating a test with `grounding.verdict: "manual"` and running critic verification — the test should be skipped without errors.

**Acceptance Scenarios**:

1. **Given** a test case with `grounding.verdict: "manual"`, **When** critic verification runs, **Then** the test is skipped with no error or warning.
2. **Given** a test case with `grounding.verdict: "manual"` and empty `source_refs: []`, **When** schema validation runs, **Then** no validation error is raised for the empty source references.

---

### User Story 4 - Undocumented Tests in Coverage Analysis (Priority: P2)

Coverage analysis reports undocumented tests (those with empty `source_refs`) as a separate metric. This highlights documentation gaps without penalizing overall test coverage scores.

**Why this priority**: Provides visibility into which behaviors are tested but not documented, helping teams prioritize documentation work. Lower priority because the core test creation flow works without this.

**Independent Test**: Can be tested by running `spectra ai analyze --coverage` on a project containing both documented and undocumented tests and verifying the report shows the separate metric.

**Acceptance Scenarios**:

1. **Given** a project with both documented and undocumented tests, **When** coverage analysis runs, **Then** the report shows documented and undocumented test counts with percentages as a separate section.
2. **Given** a project with no undocumented tests, **When** coverage analysis runs, **Then** the undocumented tests metric shows zero or is omitted entirely.
3. **Given** the coverage report includes undocumented tests, **When** the user views the report, **Then** a recommendation is shown suggesting these may indicate documentation gaps.

---

### User Story 5 - Dashboard Display of Undocumented Tests (Priority: P2)

The dashboard coverage visualization displays undocumented tests as a distinct orange category, separate from documented and uncovered tests. Users can filter the view to show or hide undocumented tests.

**Why this priority**: Visual distinction in the dashboard helps stakeholders quickly identify documentation gaps and understand coverage composition. Depends on coverage analysis changes.

**Independent Test**: Can be tested by generating a dashboard for a project with undocumented tests and verifying the orange category appears in the coverage visualization with correct counts and tooltip.

**Acceptance Scenarios**:

1. **Given** a project with undocumented tests, **When** the dashboard is generated, **Then** undocumented tests appear as an orange category in the coverage visualization.
2. **Given** the dashboard shows undocumented tests, **When** a user hovers over the orange segment, **Then** a tooltip displays "Test created from user description — no documentation source."
3. **Given** the dashboard shows undocumented tests, **When** a user toggles the undocumented test filter, **Then** the orange category is hidden or shown accordingly, and coverage percentages recalculate.

---

### Edge Cases

- What happens when the user describes a behavior that spans multiple components or suites? The agent asks which suite to place it in, or suggests the most relevant one.
- What happens when the user provides an extremely vague description (e.g., "test the login")? The agent asks for specifics rather than generating a low-quality test.
- What happens when duplicate detection finds matches across multiple suites? The agent shows all matches with their suite context.
- What happens when the user cancels after the draft is shown? No file is written, and no index is updated.
- What happens when the user identity cannot be determined for `created_by`? The agent uses a fallback value (e.g., "unknown") and notes this in the output.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The generation agent MUST support a conversational flow for creating test cases from user-described behaviors that are not in documentation.
- **FR-002**: The agent MUST ask clarifying questions only for missing information — if the user's description is sufficiently detailed, unnecessary questions MUST be skipped.
- **FR-003**: The agent MUST perform a duplicate check against existing test cases before creating a new one, using similarity matching on title, steps, and component.
- **FR-004**: When an exact duplicate is found, the agent MUST offer the user three options: update the existing test, create a new test anyway, or cancel.
- **FR-005**: The agent MUST show a complete draft of the test case for user review and wait for explicit confirmation before writing to disk.
- **FR-006**: Test cases created from undocumented behaviors MUST include frontmatter with `source_refs: []`, `grounding.verdict: "manual"`, `grounding.source: "user-described"`, and `grounding.created_by` set to the current user identity.
- **FR-007**: Tests with `grounding.verdict: "manual"` MUST be excluded from critic verification — they MUST NOT be flagged as ungrounded or cause verification errors.
- **FR-008**: Schema validation MUST accept empty `source_refs: []` as valid when `grounding.verdict` is `"manual"`.
- **FR-009**: Coverage analysis MUST report undocumented tests (empty `source_refs`) as a separate metric distinct from documented test coverage.
- **FR-010**: The dashboard coverage visualization MUST display undocumented tests as an orange category with a descriptive tooltip.
- **FR-011**: The dashboard MUST allow filtering undocumented tests in the coverage view, with coverage percentages recalculating when the filter is toggled.
- **FR-012**: After saving an undocumented test, the agent MUST display a reminder suggesting the user consider updating documentation to include the described behavior.
- **FR-013**: The generation agent prompt MUST include guidance on when to use the undocumented behavior flow versus normal doc-based generation.

### Key Entities

- **Undocumented Test Case**: A test case created from a user's verbal description rather than from documentation. Distinguished by `grounding.verdict: "manual"` and `grounding.source: "user-described"` in its frontmatter. Has empty `source_refs`.
- **Grounding Metadata**: Extended frontmatter section containing `verdict`, `source`, `created_by`, and optional `note` fields. The `verdict: "manual"` value signals that this test was not generated from documentation and should skip critic verification.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a properly structured test case from an undocumented behavior description in under 3 minutes, including the clarifying question exchange and review step.
- **SC-002**: Duplicate detection identifies existing tests with similar coverage at least 80% of the time, preventing redundant test creation.
- **SC-003**: 100% of tests with `grounding.verdict: "manual"` pass through the generation pipeline without critic verification errors.
- **SC-004**: Coverage analysis accurately reports the ratio of documented to undocumented tests, matching the actual count of tests with empty `source_refs`.
- **SC-005**: Dashboard correctly renders undocumented tests as a visually distinct category, identifiable without reading individual test files.
- **SC-006**: The undocumented test creation flow requires no changes to existing documented test cases — it is fully additive with zero regression risk to current functionality.

## Assumptions

- The generation agent is already capable of conversational interaction (established in 006-conversational-generation).
- User identity is available via the existing `UserIdentityResolver` in Spectra.MCP.
- The `find_test_cases` MCP tool (from 010-smart-test-selection) provides sufficient search capability for duplicate detection.
- Generation profiles do not affect undocumented test creation — the user's description takes priority over profile preferences.
- The orange color for undocumented tests in the dashboard does not conflict with existing color categories.
