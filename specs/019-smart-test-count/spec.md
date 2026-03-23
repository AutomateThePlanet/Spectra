# Feature Specification: Smart Test Count Recommendation

**Feature Branch**: `019-smart-test-count`
**Created**: 2026-03-23
**Status**: Draft
**Input**: User description: "Add smart test count recommendation to spectra ai generate. When --count is not specified, the AI analyzes source documentation and proposes how many test cases to generate, broken down by category (happy path, negative, edge cases, security). User can accept, adjust, or specify a custom number."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Automatic Test Count Recommendation (Priority: P1)

A test author runs `spectra ai generate --suite checkout` without specifying `--count`. The system analyzes the suite's source documentation, identifies all distinct testable behaviors, categorizes them (happy path, negative, edge cases, security), checks for existing test coverage, and displays a categorized breakdown with a recommended count. In non-interactive mode, all identified behaviors are generated automatically. In interactive mode, the user chooses how many to generate.

**Why this priority**: This is the core value proposition. Without it, users must guess how many tests to generate, leading to gaps or duplicates. This story eliminates the guesswork entirely.

**Independent Test**: Can be fully tested by running `spectra ai generate --suite <suite>` without `--count` and verifying the analysis output appears with categorized breakdown before generation proceeds.

**Acceptance Scenarios**:

1. **Given** a suite with source documentation and no `--count` flag, **When** the user runs `spectra ai generate --suite checkout`, **Then** the system displays a categorized breakdown of testable behaviors before generating tests.
2. **Given** a suite with 18 identified testable behaviors and 10 existing tests, **When** analysis completes, **Then** the system reports 8 remaining behaviors to cover and recommends generating 8 new tests.
3. **Given** `--count 15` is specified, **When** the user runs the generate command, **Then** the analysis step is skipped entirely and exactly 15 tests are generated (unchanged behavior).
4. **Given** `--no-interaction` mode, **When** analysis identifies 12 testable behaviors, **Then** all 12 are generated automatically without prompting the user.

---

### User Story 2 - Interactive Count Selection (Priority: P2)

In interactive mode, after the analysis displays a breakdown, the user is presented with menu options to choose: generate all identified behaviors, generate only a specific category (e.g., happy paths only), generate a combination of categories, enter a custom number, or describe what they want in natural language.

**Why this priority**: Builds on P1 analysis to give users fine-grained control over what gets generated. Without this, interactive users would have no way to adjust the recommendation.

**Independent Test**: Can be tested by running `spectra ai generate` in interactive mode, completing suite selection, and verifying the count selection menu appears with correct options based on the analysis breakdown.

**Acceptance Scenarios**:

1. **Given** analysis identifies 18 behaviors (8 happy path, 6 negative, 3 edge case, 1 security), **When** the interactive menu is shown, **Then** the user sees options for "All 18", "8 happy paths only", "14 happy paths + negative", a custom number option, and a free-text description option.
2. **Given** the user selects "happy paths only", **When** generation proceeds, **Then** exactly 8 happy path test cases are generated.
3. **Given** the user enters a custom number of 5, **When** generation proceeds, **Then** 5 test cases are generated.

---

### User Story 3 - Post-Generation Gap Notification (Priority: P2)

After generation completes, if fewer tests were generated than the total identified behaviors, the system displays a summary of uncovered behaviors by category and suggests the next command to generate remaining tests.

**Why this priority**: Ensures users are aware of coverage gaps and have a clear path to close them. Complements P1 by providing actionable follow-up guidance.

**Independent Test**: Can be tested by generating fewer tests than identified (e.g., happy paths only when 18 behaviors exist) and verifying the gap notification appears with correct remaining counts.

**Acceptance Scenarios**:

1. **Given** 18 behaviors identified and user generates 8 (happy paths only), **When** generation completes, **Then** the system displays "10 testable behaviors not yet covered" with a per-category breakdown.
2. **Given** all identified behaviors were generated, **When** generation completes, **Then** no gap notification is shown.
3. **Given** a gap notification is displayed, **Then** it includes a `spectra ai generate --suite <suite>` next-step command to generate remaining tests.

---

### User Story 4 - Focus Flag Integration (Priority: P3)

When `--focus` is specified alongside the smart count (no `--count`), the analysis still runs but generation is scoped to behaviors matching the focus area.

**Why this priority**: Enhances the smart count feature for users who want targeted generation. Lower priority because `--focus` is an existing power-user feature.

**Independent Test**: Can be tested by running `spectra ai generate --suite checkout --focus "negative scenarios"` and verifying only negative scenario tests are generated.

**Acceptance Scenarios**:

1. **Given** `--focus "negative scenarios"` and 18 total behaviors with 6 negative, **When** the command runs, **Then** the system reports "Focus: negative scenarios (6 identified)" and generates 6 tests.
2. **Given** `--focus` with a category that has 0 matching behaviors, **When** the command runs, **Then** the system reports no matching behaviors and exits gracefully with a message.

---

### Edge Cases

- What happens when source documentation is empty or missing for the suite? The system displays a warning and falls back to requiring `--count` or prompting the user for a number.
- What happens when the AI analysis returns zero testable behaviors? The system reports "No testable behaviors identified" and suggests checking the documentation.
- What happens when all identified behaviors are already covered by existing tests? The system reports "All N behaviors already covered" and suggests using `spectra ai update` instead.
- What happens when the analysis call fails or times out? The system falls back gracefully, warns the user, and prompts for a manual count.
- What happens when the user specifies both `--count` and the system would normally analyze? `--count` takes precedence; no analysis is performed.
- What happens when the suite has no source document references? The system warns that no documentation is linked and falls back to manual count entry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST analyze source documentation for testable behaviors when `--count` is not specified.
- **FR-002**: System MUST categorize identified behaviors into: happy path, negative/error, edge case, security, and performance categories.
- **FR-003**: System MUST display a categorized breakdown showing total count and per-category counts before generation begins.
- **FR-004**: System MUST check existing tests in the target suite and subtract already-covered behaviors from the recommendation.
- **FR-005**: System MUST preserve existing behavior when `--count` is explicitly provided (no analysis step).
- **FR-006**: System MUST auto-generate all identified behaviors in non-interactive mode (`--no-interaction` or no TTY).
- **FR-007**: System MUST present an interactive selection menu in interactive mode with options for all, by category, custom number, and free-text description.
- **FR-008**: System MUST display a post-generation gap notification when fewer tests are generated than total identified behaviors.
- **FR-009**: System MUST integrate with the `--focus` flag to scope generation to the focused category.
- **FR-010**: System MUST fall back to prompting for a manual count if the analysis step fails.
- **FR-011**: System MUST respect active generation profiles when performing the analysis.
- **FR-012**: System MUST NOT bypass the grounding verification (critic) step after generation.

### Key Entities

- **BehaviorAnalysis**: The result of analyzing source documentation — contains total count, per-category breakdown, and list of identified behaviors.
- **IdentifiedBehavior**: A single testable behavior with category, title, and source document reference.
- **CountRecommendation**: The recommended count after dedup — includes original total, already-covered count, and net new count.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users who omit `--count` receive a categorized behavior breakdown within 5 seconds of initiating the command.
- **SC-002**: The recommended test count matches the number of distinct testable behaviors in the documentation, validated by comparing analysis output against manual review on sample suites.
- **SC-003**: Existing tests are correctly identified and subtracted, so the recommended count reflects only net-new behaviors.
- **SC-004**: 100% of existing `--count N` workflows continue to function identically with no behavior change.
- **SC-005**: Gap notifications after partial generation accurately reflect the remaining uncovered behaviors by category.
- **SC-006**: Interactive mode users can select a count option and proceed to generation within 3 interactions (analysis display, selection, confirmation).
