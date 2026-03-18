# Feature Specification: Conversational Test Generation

**Feature Branch**: `006-conversational-generation`
**Created**: 2026-03-18
**Status**: Draft
**Input**: User description: "Refactor SPECTRA test generation to be conversational by default with two modes: Direct and Interactive"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Direct Mode Test Generation (Priority: P1)

A QA engineer knows exactly what tests they need. They run the generate command with flags specifying suite and focus area. The system executes autonomously: loads existing tests, checks for duplicates, generates new tests, writes them directly to disk, updates the index, and shows remaining coverage gaps. No prompts or questions interrupt the flow.

**Why this priority**: Direct mode is the primary productivity path for experienced users and CI pipelines. It transforms verbose flag-based commands into a streamlined describe-and-execute workflow.

**Independent Test**: Can be fully tested by running `spectra ai generate --suite checkout --focus "negative scenarios"` and verifying tests are written to disk without any prompts.

**Acceptance Scenarios**:

1. **Given** a suite with existing tests, **When** user runs `spectra ai generate --suite checkout --focus "negative payment"`, **Then** system loads existing tests, generates new ones avoiding duplicates, writes to disk, and shows completion with gap summary.

2. **Given** flags `--suite` and `--focus` are provided, **When** command executes, **Then** no interactive prompts appear and all output is progress/status messages.

3. **Given** generation completes successfully, **When** results are displayed, **Then** user sees: count of generated tests, table with ID/title/priority/tags, file paths written, and list of uncovered areas.

4. **Given** `--no-interaction` flag is used, **When** running in CI environment, **Then** command completes with exit code 0 for success, non-zero for errors.

---

### User Story 2 - Interactive Mode Test Generation (Priority: P1)

A QA engineer wants guidance on what tests to create. They run the generate command without arguments. The system enters an interactive flow: shows available suites with test counts, asks what kind of tests to generate, shows existing coverage, identifies gaps, generates tests for selected areas, writes directly to disk, and offers to continue with remaining gaps.

**Why this priority**: Interactive mode makes test generation accessible to users unfamiliar with the codebase or documentation structure. It guides discovery while maintaining the same direct-write behavior.

**Independent Test**: Can be fully tested by running `spectra ai generate` and following the interactive prompts through suite selection, test type selection, and generation.

**Acceptance Scenarios**:

1. **Given** user runs `spectra ai generate` without arguments, **When** command starts, **Then** system displays a selectable list of suites with their test counts.

2. **Given** user selects a suite, **When** test type prompt appears, **Then** user can choose: full coverage, negative scenarios only, specific area (describe), or free description.

3. **Given** user describes their focus, **When** system analyzes coverage, **Then** it shows existing tests matching the area and lists uncovered scenarios.

4. **Given** tests are generated, **When** they complete, **Then** tests are written directly to disk (no accept/review step), index is updated, and system shows summary with remaining gaps.

5. **Given** gaps remain after generation, **When** summary is shown, **Then** user is offered to generate tests for uncovered areas or finish.

---

### User Story 3 - Direct Mode Test Update (Priority: P1)

A QA engineer wants to sync tests with documentation changes. They run the update command with a suite flag. The system compares all tests against current documentation, automatically updates outdated tests, marks orphaned tests with warning headers in their files, flags redundant tests in the index, and reports all changes.

**Why this priority**: Keeping tests in sync with documentation is critical for test validity. Direct update mode enables automation of maintenance workflows.

**Independent Test**: Can be tested by running `spectra ai update --suite checkout` after documentation changes and verifying test files are updated in place.

**Acceptance Scenarios**:

1. **Given** documentation has changed, **When** user runs `spectra ai update --suite checkout`, **Then** system compares all tests, updates outdated ones in place, marks orphans, and reports summary.

2. **Given** tests are classified, **When** update completes, **Then** outdated tests have updated content, orphaned tests have `status: orphaned` in frontmatter with reason, redundant tests are flagged.

3. **Given** update finishes, **When** results display, **Then** user sees counts: up-to-date, updated, orphaned, redundant, and paths to modified files.

---

### User Story 4 - Interactive Mode Test Update (Priority: P2)

A QA engineer wants to review and manage test maintenance. They run update without arguments, select a suite, see the comparison results, and get guidance on next steps. Changes are still written directly (no approval needed), but the flow is guided.

**Why this priority**: Interactive update helps users understand what changed and why. It surfaces orphaned and redundant tests for human decision-making via git.

**Independent Test**: Can be tested by running `spectra ai update` and following prompts through suite selection to completion.

**Acceptance Scenarios**:

1. **Given** user runs `spectra ai update` without arguments, **When** command starts, **Then** system shows suites with test counts and last-updated dates.

2. **Given** user selects a suite, **When** comparison runs, **Then** system displays summary: up-to-date count, outdated (updated), orphaned (marked), redundant (flagged).

3. **Given** orphaned tests exist, **When** results display, **Then** system lists them with their orphan reasons and suggests reviewing via git diff.

---

### User Story 5 - Suite Creation in Interactive Mode (Priority: P2)

A QA engineer wants to create tests for a new feature area. In interactive generation, they select "Create new suite", provide a name, and proceed with test generation. The suite directory is created automatically.

**Why this priority**: New suites are common as products evolve. Integrated suite creation removes friction from the generation workflow.

**Independent Test**: Can be tested by running `spectra ai generate`, selecting "Create new suite", naming it, and verifying the directory and initial tests are created.

**Acceptance Scenarios**:

1. **Given** interactive suite selection, **When** user selects "Create new suite", **Then** system prompts for suite name.

2. **Given** valid suite name provided, **When** confirmed, **Then** system creates `tests/{suite-name}/` directory and proceeds to test type selection.

---

### User Story 6 - CI Pipeline Integration (Priority: P2)

An automation engineer runs SPECTRA commands in CI/CD pipelines. Using `--no-interaction` with required flags ensures commands complete without prompts, output is machine-readable, and exit codes indicate success/failure.

**Why this priority**: CI integration enables automated test maintenance and generation as part of development workflows.

**Independent Test**: Can be tested by running commands with `--no-interaction` flag in a non-TTY environment and verifying no prompts and correct exit codes.

**Acceptance Scenarios**:

1. **Given** `--no-interaction` with `--suite` flag, **When** command runs in CI, **Then** completes without any interactive prompts.

2. **Given** command succeeds, **When** process exits, **Then** exit code is 0.

3. **Given** command fails (e.g., no documentation found), **When** process exits, **Then** exit code is non-zero with error message to stderr.

4. **Given** `--no-interaction` without required `--suite` flag, **When** command runs, **Then** exits with error explaining required flags.

---

### Edge Cases

- What happens when the terminal doesn't support interactive prompts (piped input)?
  - System detects non-TTY and behaves as if `--no-interaction` was passed with appropriate warnings.

- How does the system handle empty documentation folders?
  - System informs user no documentation was found and suggests adding docs or checking configuration.

- What happens if AI generation fails mid-way?
  - Tests already written remain on disk. Error is reported with count of successful writes. User can retry.

- What happens when all gaps are already covered?
  - System reports "No gaps identified for the requested area" and suggests other areas or full coverage.

- How are profile settings applied?
  - `spectra.profile.md` is loaded automatically if present. Interactive mode user intent is layered on top.

- What happens with network errors during AI calls?
  - Error displayed with suggestion to retry. Partial work (if any tests written) is preserved.

## Requirements *(mandatory)*

### Functional Requirements

**Mode Detection & Execution**
- **FR-001**: System MUST enter interactive mode when `spectra ai generate` is run without `--suite` argument
- **FR-002**: System MUST enter direct mode when `spectra ai generate` is run with `--suite` argument
- **FR-003**: System MUST skip all interactive prompts when `--no-interaction` flag is provided
- **FR-004**: System MUST require `--suite` flag when `--no-interaction` is used

**Generation Flow**
- **FR-005**: System MUST load and display existing tests matching the focus area before generating new tests
- **FR-006**: System MUST identify coverage gaps by comparing documentation against existing tests
- **FR-007**: System MUST write generated tests directly to disk without a review/accept step
- **FR-008**: System MUST update `_index.json` after writing tests
- **FR-009**: System MUST display coverage gaps remaining after generation
- **FR-010**: In interactive mode, system MUST offer to generate tests for remaining gaps

**Update Flow**
- **FR-011**: System MUST classify existing tests as: up-to-date, outdated, orphaned, or redundant
- **FR-012**: System MUST update outdated tests in place with new content
- **FR-013**: System MUST mark orphaned tests with `status: orphaned` and reason in frontmatter
- **FR-014**: System MUST flag redundant tests in the index
- **FR-015**: System MUST rebuild the index after updates

**UI & Feedback**
- **FR-016**: System MUST use consistent visual symbols: ◆ prompts, ◐ spinners, ✓ success, ✗ errors, ⚠ warnings, ℹ info
- **FR-017**: System MUST use color coding: green success, yellow warnings, red errors, cyan info
- **FR-018**: System MUST display test listings in table format with columns: ID, title, priority, tags
- **FR-019**: System MUST use visual grouping with box-drawing characters for related output

**Profile & Configuration**
- **FR-020**: System MUST automatically load `spectra.profile.md` if present
- **FR-021**: Interactive mode user selections MUST layer on top of profile settings
- **FR-022**: System MUST support `--focus` flag to describe the test generation focus area

**Suite Management**
- **FR-023**: Interactive mode MUST include "Create new suite" option in suite selection
- **FR-024**: System MUST create suite directory when new suite is selected

### Key Entities

- **Test Suite**: A collection of test cases for a feature area, stored in `tests/{suite-name}/` with an `_index.json` metadata file.

- **Coverage Gap**: An area of functionality identified from documentation that has no corresponding test. Includes: document path, section, description.

- **Test Classification**: The state of a test during update: UP_TO_DATE (matches docs), OUTDATED (docs changed), ORPHANED (docs removed), REDUNDANT (duplicates another test).

- **Generation Profile**: Settings from `spectra.profile.md` that influence test generation: detail level, priority defaults, tag conventions, domain-specific rules.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete a direct-mode generation (flags → tests written) in under 60 seconds for typical scenarios
- **SC-002**: Users can complete an interactive-mode generation (no args → tests written) in under 3 minutes for typical scenarios
- **SC-003**: System prevents 95% of duplicate tests through proactive coverage checking
- **SC-004**: Generated tests are written to disk within 5 seconds of generation completing
- **SC-005**: Coverage gap identification matches actual documentation gaps with 90% accuracy
- **SC-006**: Test update correctly classifies 95% of tests into the four categories
- **SC-007**: Interactive prompts respond to user input within 100ms
- **SC-008**: CI mode (--no-interaction) completes without any stdin reads

## Assumptions

- Existing `AgentRuntime` infrastructure supports the AI calls for generation and gap analysis
- `spectra.profile.md` format is stable from Phase 4 implementation
- Existing `Spectra.Core` index reading can load suite metadata efficiently
- Users review generated tests via IDE or git diff rather than in-CLI review
- Git provides the undo mechanism (git checkout) for unwanted generations
- Terminal environments support ANSI colors and Unicode symbols for rich output
