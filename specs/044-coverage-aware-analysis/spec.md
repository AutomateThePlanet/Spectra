# Feature Specification: Coverage-Aware Behavior Analysis

**Feature Branch**: `044-coverage-aware-analysis`  
**Created**: 2026-04-13  
**Status**: Draft  
**Input**: User description: "Make the behavior analysis step in `spectra ai generate` coverage-aware so it only recommends tests for genuine gaps instead of treating documentation as a blank slate"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gap-Only Analysis for Mature Suites (Priority: P1)

A test engineer runs `spectra ai generate` on a mature suite with 231 existing tests covering 41 acceptance criteria. Instead of the AI reporting 142 behaviors and recommending 139 new tests, the analysis step loads the existing coverage data (test index, criteria coverage, doc section coverage) and only identifies uncovered behaviors. The engineer sees an accurate gap report: "3 uncovered acceptance criteria, recommend 8 new tests."

**Why this priority**: This is the core value proposition. Without accurate gap detection, the generate command produces misleading recommendations that waste time and erode trust.

**Independent Test**: Run `spectra ai generate --analyze-only` on a suite with known coverage. Verify the recommended count matches the actual gap (uncovered criteria + uncovered doc sections), not the total testable behaviors.

**Acceptance Scenarios**:

1. **Given** a suite with 231 tests covering 38/41 criteria, **When** the user runs `spectra ai generate --analyze-only`, **Then** the analysis reports ~3 uncovered criteria and recommends a number of new tests proportional to the actual gap (not 139).
2. **Given** a suite with full criteria coverage (41/41), **When** the user runs `spectra ai generate --analyze-only`, **Then** the analysis reports 0 uncovered criteria and may recommend 0 new tests or a small number from uncovered doc sections.
3. **Given** a suite with partial doc section coverage (14/16 sections linked), **When** the user runs `spectra ai generate --analyze-only`, **Then** the analysis identifies 2 uncovered doc sections and looks for testable behaviors in those sections.

---

### User Story 2 - Graceful Degradation for New Suites (Priority: P1)

A test engineer runs `spectra ai generate` on a brand-new suite with no existing tests, no criteria index, and no doc index. The system behaves exactly as it does today: full behavior analysis without coverage context.

**Why this priority**: New suite generation must not break. The coverage-aware feature is additive; the absence of coverage data must not cause errors or empty results.

**Independent Test**: Run `spectra ai generate` on a new suite with no `_index.json`, no `_criteria_index.yaml`, and no `docs/_index.md`. Verify the analysis produces results identical to the current behavior.

**Acceptance Scenarios**:

1. **Given** a new suite with no `_index.json`, **When** the user runs `spectra ai generate`, **Then** the analysis runs without coverage context and produces full behavior analysis.
2. **Given** a suite with `_index.json` but no `_criteria_index.yaml`, **When** the user runs analysis, **Then** the coverage snapshot includes test titles and source refs but omits criteria coverage — partial context is still useful.
3. **Given** a suite with `_index.json` and `_criteria_index.yaml` but no `docs/_index.md`, **When** the user runs analysis, **Then** the snapshot includes test titles and criteria coverage but omits doc section coverage.

---

### User Story 3 - Coverage Summary in Analysis Output (Priority: P2)

After analysis completes, the engineer sees a structured coverage summary before the behavior breakdown: existing test count, criteria coverage ratio (e.g., "38/41 covered, 92.7%"), doc section coverage ratio, and a gap-only behavior list. This replaces the current misleading "142 behaviors found, 3 already covered."

**Why this priority**: Accurate presentation builds trust in the tool's recommendations and helps engineers make informed decisions about whether to proceed with generation.

**Independent Test**: Run `spectra ai generate` on a suite with known coverage and verify the terminal output includes the coverage summary section with correct numbers.

**Acceptance Scenarios**:

1. **Given** a completed analysis with coverage data, **When** results are presented to the user, **Then** the output shows existing test count, criteria coverage ratio, doc section coverage ratio, and gap-only recommended count.
2. **Given** a completed analysis in JSON output mode, **When** results are serialized, **Then** the JSON includes `existing_test_count`, `total_criteria`, `covered_criteria`, `uncovered_criteria`, and `uncovered_criteria_ids` fields.

---

### User Story 4 - Token Budget Management for Large Suites (Priority: P2)

A test engineer runs analysis on a very large suite with 600+ tests. The system switches to "summary mode" — sending only coverage statistics and uncovered items to the AI prompt, not the full title list — to stay within token budget while still providing accurate gap analysis.

**Why this priority**: Without token management, large suites could exceed prompt limits or degrade AI response quality.

**Independent Test**: Run analysis on a suite with 600 test entries in `_index.json` and verify the prompt does not include full title list but does include coverage statistics.

**Acceptance Scenarios**:

1. **Given** a suite with fewer than 500 tests, **When** coverage context is built, **Then** full mode is used: all titles, criteria, and source refs are included in the prompt.
2. **Given** a suite with more than 500 tests, **When** coverage context is built, **Then** summary mode is used: only coverage statistics and uncovered items are sent, full title list is omitted.
3. **Given** summary mode is active, **When** the AI analyzes behaviors, **Then** the results are still accurate (uncovered criteria and doc sections are still provided for gap detection).

---

### User Story 5 - Progress Page Coverage Snapshot (Priority: P3)

When the `.spectra-progress.html` page is open during generation, the analysis phase shows a coverage snapshot: existing test count badge, criteria coverage mini-bar, and a "Gap-only analysis" label.

**Why this priority**: Visual feedback during generation helps the engineer understand what the tool is doing, but is not essential for core functionality.

**Independent Test**: Run `spectra ai generate` with the progress page open and verify the HTML contains coverage snapshot elements during the analysis phase.

**Acceptance Scenarios**:

1. **Given** the progress page is open during analysis, **When** coverage snapshot is computed, **Then** the page displays existing test count, criteria coverage bar, and "Gap-only analysis" label.

---

### Edge Cases

- What happens when `_index.json` exists but is empty or malformed? System treats it as "no index" and returns empty snapshot gracefully.
- What happens when criteria IDs in test cases don't match any IDs in `_criteria_index.yaml`? Those test criteria count as covered but won't affect the uncovered criteria list.
- What happens when test titles exceed 80 characters? They are truncated at 80 chars in the prompt context to manage token budget.
- What happens when `--focus` is used alongside coverage awareness? Focus is additive — the coverage context still tells the AI what's covered; focus narrows the documentation area to analyze.
- What happens when `--from-description` or `--from-suggestions` is used? These flows skip behavior analysis entirely, so coverage awareness does not apply.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST build a coverage snapshot from `_index.json`, `_criteria_index.yaml`, and `docs/_index.md` before running behavior analysis on an existing suite.
- **FR-002**: System MUST inject the coverage snapshot into the behavior analysis prompt so the AI only identifies uncovered behaviors.
- **FR-003**: System MUST include existing test titles in the prompt for deduplication, truncated to 80 characters each.
- **FR-004**: System MUST cross-reference criteria IDs from test cases against `_criteria_index.yaml` to identify uncovered criteria.
- **FR-005**: System MUST cross-reference `source_refs` from test cases against the doc index to identify uncovered documentation sections.
- **FR-006**: System MUST switch to summary mode when the suite has more than 500 tests, omitting the full title list but preserving coverage statistics and uncovered items.
- **FR-007**: System MUST gracefully degrade when any coverage data source is missing — each source (index, criteria, doc index) is independent.
- **FR-008**: System MUST preserve existing behavior for new suites with no coverage data — no errors, no empty results.
- **FR-009**: System MUST extend the analysis result model with coverage fields (`existing_test_count`, `total_criteria`, `covered_criteria`, `uncovered_criteria`, `uncovered_criteria_ids`) defaulting to 0/empty for backward compatibility.
- **FR-010**: System MUST present a structured coverage summary in the analysis output (terminal and JSON) showing criteria coverage ratio, doc section coverage ratio, and gap-only recommendations.
- **FR-011**: System MUST update the progress page to display a coverage snapshot during the analysis phase.
- **FR-012**: System MUST not affect the `--from-description` or `--from-suggestions` flows, which bypass behavior analysis.

### Key Entities

- **CoverageSnapshot**: Aggregated view of what's already tested — existing test count, titles, covered/uncovered criteria IDs, covered/uncovered source refs.
- **UncoveredCriterion**: A single acceptance criterion with zero linked tests — includes ID, title, source document, and priority.
- **GenerateAnalysis (extended)**: The behavior analysis result, now enriched with coverage fields for accurate gap reporting.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a suite with 231 tests and 41 criteria where 38 are covered, the analysis recommends fewer than 15 new tests (not 139).
- **SC-002**: For a fully covered suite (all criteria and doc sections linked), the analysis recommends 0-5 new tests.
- **SC-003**: For a new suite with no existing data, the analysis produces results identical to current behavior (no regression).
- **SC-004**: Coverage snapshot computation completes in under 2 seconds for suites with up to 500 tests.
- **SC-005**: Token overhead from coverage context stays under 5,000 tokens for suites with up to 500 tests.
- **SC-006**: All existing `spectra ai generate` flows (`--from-description`, `--from-suggestions`, `--auto-complete`) continue to work without modification.
- **SC-007**: JSON output from `--output-format json` includes the new coverage fields and is backward-compatible (old consumers ignore new fields).

## Assumptions

- The `_index.json` file follows the existing schema where each test entry has `criteria` (list of criteria IDs) and `source_refs` (list of doc section references).
- The `_criteria_index.yaml` file contains a flat list of criteria entries with `id`, `title`, `source`, and `priority` fields.
- The `docs/_index.md` doc index can be parsed for section references using the existing `DocIndexExtractor`.
- The behavior analysis prompt template supports placeholder injection via the existing `PlaceholderResolver` / `TemplateLoader` system.
- The 500-test threshold for summary mode is a reasonable default; it can be adjusted later based on real-world token usage.
