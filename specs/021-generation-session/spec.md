# Feature Specification: Generation Session Flow

**Feature Branch**: `021-generation-session`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "Implement the SPECTRA generation session flow"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iterative Test Generation Session (Priority: P1)

A tester runs the generate command and enters a multi-phase session: the system first analyzes documentation to count testable behaviors, then generates test cases, then suggests additional tests for uncovered areas, and loops until the tester is satisfied.

**Why this priority**: This is the core interactive experience that replaces the current single-shot generation. Without the session loop, testers must manually re-run commands to achieve comprehensive coverage.

**Independent Test**: Can be tested by running the generate command in interactive mode, completing one full cycle through analysis, generation, suggestions, and exit, verifying the session summary displays correct totals.

**Acceptance Scenarios**:

1. **Given** a tester runs the generate command for a suite, **When** analysis completes, **Then** the system shows testable behavior count, breakdown by category, existing coverage, and a recommended count
2. **Given** generation completes with remaining uncovered areas, **When** Phase 3 begins, **Then** the system suggests specific additional test cases with titles and categories
3. **Given** suggestions are displayed, **When** the tester selects specific suggestions, **Then** only those are generated and the loop restarts with updated gap analysis
4. **Given** the tester chooses to exit, **Then** a session summary shows totals by phase (from docs, from suggestions, from descriptions)

---

### User Story 2 - User-Described Test Cases (Priority: P1)

A tester describes a behavior they want to test in plain language, and the system creates a structured test case from that description. The generated test is marked with a "manual" grounding verdict since it has no documentation source.

**Why this priority**: Enables testers to capture undocumented behaviors, edge cases, and domain knowledge that AI cannot discover from documentation alone.

**Independent Test**: Can be tested by choosing "describe your own" in the session menu, entering a behavior description, and verifying the generated test has correct grounding metadata.

**Acceptance Scenarios**:

1. **Given** a tester selects "describe your own test case" in the session menu, **When** they enter a behavior description and optional context, **Then** the system generates a structured test case with steps and expected results
2. **Given** a user-described test is created, **When** it is written to disk, **Then** its grounding metadata shows `verdict: manual` and `source: user-described`
3. **Given** a user-described test draft is shown, **When** the tester chooses "edit", **Then** they can modify the test before saving

---

### User Story 3 - Non-Interactive Session for CI/SKILL (Priority: P2)

A CI pipeline or SKILL file invokes generation session phases individually via CLI flags without any prompts, receiving structured JSON output.

**Why this priority**: Enables automated workflows to use the same session phases that interactive users get, but via explicit CLI arguments.

**Independent Test**: Can be tested by running `--from-suggestions` and `--from-description` flags with `--output-format json`, verifying valid JSON output and no prompts.

**Acceptance Scenarios**:

1. **Given** a previous session generated suggestions, **When** `--from-suggestions` is passed, **Then** the system generates tests from those saved suggestions without prompts
2. **Given** `--from-description "behavior text"` is passed, **When** the command runs, **Then** a test case is created from the description and output as JSON
3. **Given** `--auto-complete` is passed, **When** the command runs, **Then** all phases execute without prompts: analyze, generate, accept all suggestions, finalize

---

### User Story 4 - Duplicate Detection Before Creation (Priority: P2)

Before creating any test case (from docs, suggestions, or description), the system checks existing tests for similar ones using fuzzy title matching, and warns the tester or includes a warning in JSON output.

**Why this priority**: Prevents duplicate test cases from accumulating, improving test suite quality.

**Independent Test**: Can be tested by creating a test with a title very similar to an existing one and verifying the duplicate warning appears.

**Acceptance Scenarios**:

1. **Given** an existing test has a similar title (>80% match), **When** a new test is about to be created in interactive mode, **Then** the user sees a warning with the similar test ID and can choose to create anyway or skip
2. **Given** an existing test has a similar title, **When** creating in non-interactive mode, **Then** the test is created but the JSON output includes a `duplicate_warning` field

---

### Edge Cases

- What happens when a session expires mid-flow? The next command starts a fresh session.
- What happens when `--from-suggestions` is used but no previous session exists? Returns an error with exit code 1.
- What happens when all behaviors are already covered? Phase 2 is skipped, and the session goes directly to Phase 3 (suggestions/user-described).
- What happens when the user describes a test but generation fails? The description is preserved in session state so they can retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a four-phase generation session: Analysis, Generation, Suggestions, User-Described
- **FR-002**: System MUST persist session state between phases so that suggestions and context carry forward
- **FR-003**: Session state MUST expire after 1 hour or when a new session starts for the same suite
- **FR-004**: System MUST provide `--from-suggestions` flag to generate tests from the previous session's suggestions
- **FR-005**: System MUST provide `--from-description` and `--context` flags to create a test from a plain-language description
- **FR-006**: System MUST provide `--auto-complete` flag that runs all phases without prompts
- **FR-007**: User-described tests MUST be marked with `grounding.verdict: manual` and `source: user-described`
- **FR-008**: System MUST check for duplicate tests using fuzzy title matching (>80% similarity) before creating any test
- **FR-009**: In interactive mode, duplicate detection MUST prompt the user to create, skip, or update the existing test
- **FR-010**: In non-interactive mode, duplicate detection MUST include a `duplicate_warning` field in JSON output
- **FR-011**: The suggestions phase MUST allow the user to select all, pick specific suggestions by index, describe their own, or exit
- **FR-012**: System MUST display a session summary at exit showing test counts by source (docs, suggestions, user-described)
- **FR-013**: `--from-suggestions` with specific indices (e.g., `1,3`) MUST generate only those selected suggestions

### Key Entities

- **Generation Session**: A time-bounded context tracking analysis results, generated tests, pending suggestions, and user-described tests for a specific suite
- **Suggestion**: A proposed test case identified by gap analysis, with title, category, and status (pending/generated/skipped)
- **User-Described Test**: A test case created from a tester's plain-language description, bypassing critic verification

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A tester can complete a full generation session (analyze, generate, review suggestions, describe custom test, exit) in a single command invocation
- **SC-002**: Session state persists between separate command invocations for up to 1 hour, enabling `--from-suggestions` to work after initial generation
- **SC-003**: User-described tests are created within 5 seconds of the user submitting their description
- **SC-004**: Duplicate detection catches tests with >80% title similarity and warns the user before creation
- **SC-005**: `--auto-complete` runs all phases and exits with zero interactive prompts
- **SC-006**: Session summary accurately reports total tests by source category with zero miscounts

## Assumptions

- The behavior analysis system (Phase 1) already exists from the smart test count feature — this spec extends it into the session flow
- Suggestions are derived from gap analysis comparing generated tests against identified behaviors
- Fuzzy matching uses normalized string similarity — the specific algorithm is an implementation detail
- Session state is a local file, not shared across machines — this is a single-user CLI tool
- The `--from-suggestions` flag reads from the most recent session for the specified suite
