# Feature Specification: Coverage Analysis & Dashboard Visualizations

**Feature Branch**: `009-coverage-dashboard-viz`
**Created**: 2026-03-20
**Status**: Draft
**Input**: User description: "Three-type coverage analysis (documentation, requirements, automation) with --auto-link flag and dashboard visualizations (progress bars, donut chart, treemap, empty states)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Unified Coverage Analysis (Priority: P1)

A QA lead wants to understand their project's test health across three dimensions: which documentation has tests, which requirements are covered, and which manual tests have automation. They run a single command and get a unified report showing all three coverage types with percentages and details.

**Why this priority**: Coverage analysis is the core value of this feature. Without it, the dashboard has nothing to display, and auto-link has nothing to report against. A user can get value from coverage analysis alone, even without the dashboard.

**Independent Test**: Can be fully tested by running `spectra ai analyze --coverage` against a project with docs, tests, and automation code, and verifying the console output shows three coverage sections with correct percentages.

**Acceptance Scenarios**:

1. **Given** a project with 4 documentation files and tests referencing 3 of them via `source_refs`, **When** the user runs `spectra ai analyze --coverage`, **Then** the documentation coverage section shows "3/4 documents covered (75%)" with a detail list showing which doc is uncovered.
2. **Given** a project with a `_requirements.yaml` defining 5 requirements and tests covering 3 of them via `requirements` field, **When** the user runs `spectra ai analyze --coverage`, **Then** the requirements coverage section shows "3/5 requirements covered (60%)" with uncovered requirements listed.
3. **Given** a project with 40 manual tests and 12 having `automated_by` fields, **When** the user runs `spectra ai analyze --coverage`, **Then** the automation coverage section shows "12/40 tests automated (30%)" with per-suite breakdown.
4. **Given** a project with no `_requirements.yaml` file but tests referencing requirements in frontmatter, **When** the user runs `spectra ai analyze --coverage`, **Then** the requirements section shows `has_requirements_file: false` and lists requirements discovered from tests only.
5. **Given** the user runs `spectra ai analyze --coverage --format json --output coverage.json`, **Then** the output file contains three top-level keys: `documentation_coverage`, `requirements_coverage`, `automation_coverage` with the specified JSON structure.
6. **Given** the user runs `spectra ai analyze --coverage --format markdown --output coverage.md`, **Then** a readable Markdown report is generated with three sections.

---

### User Story 2 - Auto-Link Tests to Automation Code (Priority: P2)

A developer has written automation code that references manual test IDs (e.g., `[TestCase("TC-100")]`) but the test Markdown files don't have `automated_by` fields yet. They run a single command to scan automation code and automatically update test files with the correct links.

**Why this priority**: Auto-link is the primary write action of this feature — it modifies test files to establish traceability. It depends on the coverage scanning infrastructure from P1 but adds significant value by eliminating manual maintenance of `automated_by` fields.

**Independent Test**: Can be tested by creating test files without `automated_by` and automation files referencing test IDs, running `--auto-link`, and verifying the test files are updated.

**Acceptance Scenarios**:

1. **Given** `LoginTests.cs` contains `[TestCase("TC-100")]` and `TC-100.md` has no `automated_by` field, **When** the user runs `spectra ai analyze --coverage --auto-link`, **Then** `TC-100.md` frontmatter is updated to include `automated_by: ["tests/automation/LoginTests.cs"]`.
2. **Given** `TC-100.md` already has `automated_by: ["old/path.cs"]`, **When** auto-link finds `TC-100` referenced only in `NewTests.cs`, **Then** `automated_by` is replaced with `["NewTests.cs"]` (scan results are authoritative; stale entries are removed).
3. **Given** automation code references `TC-999` which does not exist as a test file, **When** auto-link runs, **Then** it reports this as orphaned automation (not an error, just a warning).
4. **Given** `TC-100.md` has `automated_by: ["tests/Removed.cs"]` but `Removed.cs` no longer exists, **When** coverage analysis runs, **Then** it reports this as a broken link.
5. **Given** `spectra ai analyze --coverage` is run WITHOUT `--auto-link`, **When** scan finds matches, **Then** no test files are modified — results are reported only.
6. **Given** auto-link updates 12 test files, **When** the command completes, **Then** it reports "Auto-linked 12 tests to automation code" with a summary of changes.

---

### User Story 3 - View Coverage in Dashboard with Progress Bars (Priority: P3)

A project manager opens the SPECTRA dashboard and wants to see coverage health at a glance. They see three stacked progress bar cards — one per coverage type — with percentages, fill bars, and expandable detail lists showing exactly which items are covered or uncovered.

**Why this priority**: The dashboard is the primary consumption point for non-CLI users. Progress bars provide the most intuitive and information-dense view of coverage. However, it requires P1 to generate the data, so it cannot be delivered first.

**Independent Test**: Can be tested by generating a dashboard with coverage data present and verifying the three progress bar cards render with correct percentages, colors, and expandable details.

**Acceptance Scenarios**:

1. **Given** a project with documentation coverage at 75%, **When** the dashboard Coverage tab is opened, **Then** a progress bar card shows "Documentation Coverage 75%" with a green-filled bar at 75% and "3/4 docs" label.
2. **Given** requirements coverage is at 60%, **When** the detail section is expanded, **Then** each requirement is listed with its title, linked test IDs, and a covered/uncovered indicator.
3. **Given** automation coverage is at 30%, **When** the detail section is expanded, **Then** per-suite breakdown is shown (e.g., "authentication: 5/28 (18%)") and unlinked tests are listed.
4. **Given** a progress bar card is collapsed, **When** the user clicks "Show details", **Then** the detail list expands below the progress bar with animation.
5. **Given** coverage data changes across dashboard regenerations, **When** the dashboard is regenerated, **Then** progress bars reflect the updated percentages.

---

### User Story 4 - View Donut Chart Summary (Priority: P4)

A stakeholder viewing the dashboard wants a high-level summary of test health as a single visual. A donut chart shows the distribution of tests: automated (green), manual-only (yellow), and unlinked (red), with the total test count in the center.

**Why this priority**: The donut chart provides a quick "health snapshot" but is less actionable than the progress bars. It enhances the dashboard but is not essential for coverage analysis value.

**Independent Test**: Can be tested by generating a dashboard with known test distributions and verifying the donut chart renders correct segments with proper colors and center label.

**Acceptance Scenarios**:

1. **Given** 40 total tests: 12 automated, 20 manual-only (have `source_refs`), 8 unlinked, **When** the dashboard renders, **Then** the donut chart shows three segments (green 30%, yellow 50%, red 20%) with "40 tests" in the center.
2. **Given** all tests are automated, **When** the dashboard renders, **Then** the donut is fully green with "40 tests" in the center.
3. **Given** hovering over a donut segment, **When** the user hovers, **Then** a tooltip shows the count and percentage for that category.

---

### User Story 5 - View Treemap by Component (Priority: P5)

A team lead wants to identify which suites need the most testing attention. A treemap visualization shows suites as blocks sized by test count and colored by automation coverage, making it immediately obvious where gaps exist.

**Why this priority**: The treemap is an advanced visualization that provides component-level insight. It depends on both coverage data (P1) and dashboard infrastructure (P3). Valuable for large projects but not essential for core coverage functionality.

**Independent Test**: Can be tested by generating a dashboard with multi-suite test data and verifying the treemap renders blocks of correct relative sizes with appropriate color coding.

**Acceptance Scenarios**:

1. **Given** three suites: authentication (28 tests, 18% automated), citizen-registration (6 tests, 0%), payment-processing (6 tests, 0%), **When** the treemap renders, **Then** authentication has the largest block, and all blocks are yellow (< 50% automated).
2. **Given** a suite with > 50% automation coverage, **When** the treemap renders, **Then** its block is green.
3. **Given** the user clicks a treemap block, **When** clicked, **Then** the test list for that component is shown (test IDs, titles, automation status).

---

### User Story 6 - See Helpful Empty States (Priority: P3)

A new user opens the dashboard before configuring coverage and sees helpful guidance instead of confusing "0%" bars. Each empty coverage section explains what's needed and how to set it up, reducing the learning curve.

**Why this priority**: Same as P3 because empty states are part of the progress bar implementation — they must be handled when progress bars are built. Poor empty states create a bad first impression and support burden.

**Independent Test**: Can be tested by generating a dashboard with no requirements configured and no automation links, and verifying the guidance text appears instead of bare zero-percent bars.

**Acceptance Scenarios**:

1. **Given** no tests have `requirements` fields and no `_requirements.yaml` exists, **When** the requirements coverage card renders, **Then** it shows "No requirements tracked yet" with instructions to add `requirements` to test frontmatter or create `_requirements.yaml`.
2. **Given** no tests have `automated_by` fields and no automation code references tests, **When** the automation coverage card renders, **Then** it shows "No automation links detected" with instructions to run `--auto-link` and configure `automation_dirs`.
3. **Given** all documents have test coverage, **When** the documentation coverage card renders, **Then** it shows a success message "All documents have test coverage!" with a checkmark.

---

### User Story 7 - Initialize Coverage Configuration (Priority: P1)

When a user runs `spectra init` for the first time, the coverage infrastructure is bootstrapped: a `_requirements.yaml` template with commented examples is created, and the `coverage` section with sensible defaults appears in `spectra.config.json`. This ensures users discover coverage features without manual config editing.

**Why this priority**: Same as P1 because init is the entry point for all new users. Without it, coverage features are invisible and undiscoverable. It's also trivial to implement alongside the core analysis.

**Independent Test**: Can be tested by running `spectra init` in an empty directory and verifying both the requirements template and coverage config section are created.

**Acceptance Scenarios**:

1. **Given** an empty directory, **When** the user runs `spectra init`, **Then** `docs/requirements/_requirements.yaml` is created with a commented example showing the expected YAML structure.
2. **Given** an empty directory, **When** the user runs `spectra init`, **Then** `spectra.config.json` includes a `coverage` section with default `automation_dirs`, `scan_patterns`, `file_extensions`, and `requirements_file`.
3. **Given** a project already initialized, **When** the user runs `spectra init` again, **Then** existing coverage config is not overwritten.

---

### Edge Cases

- **Test references nonexistent documentation**: A test has `source_refs: ["docs/deleted.md"]` — should be reported as a broken source reference in documentation coverage details.
- **Multiple automation references**: File A and File B both reference TC-001 — auto-link should set `automated_by` to both files. If only File A references TC-001 on a subsequent run, File B is removed (scan results are authoritative).
- **Very large automation codebase**: Hundreds of automation files with thousands of pattern matches — coverage scanning must complete within a reasonable time (linear scaling with file count).
- **Malformed `_requirements.yaml`**: File exists but has invalid YAML or missing required fields — report a warning and fall back to discovering requirements from tests only.
- **Mixed scan pattern formats**: Some patterns match, some don't compile as valid regex after `{id}` substitution — report invalid patterns as warnings, continue with valid ones.
- **Tests moved between suites**: A test's ID remains globally unique but its suite changes — coverage analysis finds it by ID regardless of suite location.
- **Empty project**: No docs, no tests, no automation code — all three coverage types report 0/0 with appropriate empty state messaging.
- **Unicode in file paths and test IDs**: Documentation paths and automation file paths containing non-ASCII characters must be handled correctly.

## Clarifications

### Session 2026-03-20

- Q: Should the treemap group tests by suite (directory name) or by the `component` frontmatter field? → A: Group by suite (directory name) — always available, every test maps to exactly one block.
- Q: When `--auto-link` runs, should it only add newly discovered references or also remove stale entries? → A: Replace with scan results — `automated_by` is set to exactly what the scan finds, acting as the authoritative source of truth. Stale references are automatically cleaned.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST analyze documentation coverage by cross-referencing `source_refs` fields in test frontmatter against documentation files in the configured `local_dir`.
- **FR-002**: System MUST analyze requirements coverage by reading `requirements` fields from test YAML frontmatter and optionally cross-referencing against `_requirements.yaml`.
- **FR-003**: System MUST analyze automation coverage by scanning configured `automation_dirs` for files matching `file_extensions` and searching for test ID patterns defined in `scan_patterns`.
- **FR-004**: System MUST produce a unified coverage report combining all three coverage types in a single output (console, JSON, or Markdown).
- **FR-005**: System MUST support `--auto-link` flag that scans automation code and replaces `automated_by` entries in test file YAML frontmatter with the current scan results (authoritative source of truth — stale references are automatically removed).
- **FR-006**: System MUST NOT modify any test files when `--auto-link` is not specified — analysis is read-only by default.
- **FR-007**: System MUST report broken links (test references nonexistent automation file), orphaned automation (code references nonexistent test ID), and unlinked tests (no `automated_by` field).
- **FR-008**: System MUST support `--format json` output with three top-level keys: `documentation_coverage`, `requirements_coverage`, `automation_coverage`.
- **FR-009**: System MUST support `--format markdown` output with three clearly labeled sections.
- **FR-010**: System MUST bootstrap coverage configuration during `spectra init` including a commented `_requirements.yaml` template and default `coverage` config section.
- **FR-011**: Dashboard MUST display three stacked progress bar cards (one per coverage type) with percentage, fill bar, count label, and expandable detail list.
- **FR-012**: Dashboard MUST display a donut chart showing overall test distribution: automated (green), manual-only (yellow), unlinked (red), with total count in center.
- **FR-013**: Dashboard MUST display a treemap visualization showing suites as blocks sized by test count and colored by automation coverage percentage.
- **FR-014**: Dashboard MUST show helpful empty state guidance (not bare "0%" bars) when coverage data is missing or unconfigured.
- **FR-015**: Scan patterns MUST support template syntax where `{id}` is replaced with the test ID regex pattern for matching.
- **FR-016**: System MUST gracefully handle missing or malformed `_requirements.yaml` by falling back to discovering requirements from test frontmatter only.
- **FR-017**: Progress bar color coding MUST use green for >= 80%, yellow for >= 50%, red for < 50%.

### Key Entities

- **Coverage Report**: The unified output containing three coverage sections plus generation timestamp. Produced by analysis, consumed by CLI output formatters and dashboard.
- **Documentation Coverage Detail**: Per-document record with document path, linked test count, coverage status, and linked test IDs.
- **Requirement Coverage Detail**: Per-requirement record with ID, title, linked tests, and coverage status. Sourced from `_requirements.yaml` or discovered from test frontmatter.
- **Automation Coverage Detail**: Per-suite breakdown with total/automated counts, plus lists of unlinked tests, orphaned automation references, and broken links.
- **Scan Pattern**: A template string (e.g., `[TestCase("{id}")]`) that is compiled into a regex by escaping special characters and replacing `{id}` with the test ID pattern.
- **Auto-Link Result**: A record pairing a test ID with an automation file path, produced by scanning and consumed by the frontmatter updater.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can generate a unified three-section coverage report with a single command and immediately identify which documents, requirements, and tests lack coverage.
- **SC-002**: Auto-link correctly discovers and writes automation references for 100% of test IDs found in scanned automation code, with zero false positives (no incorrect file associations).
- **SC-003**: Coverage analysis completes within 10 seconds for projects with up to 500 test files and 100 automation files.
- **SC-004**: Dashboard coverage tab renders all three progress bar cards with accurate percentages within 1 second of page load.
- **SC-005**: New users running `spectra init` discover coverage features through the created template files and config defaults, without needing to read documentation first.
- **SC-006**: Dashboard empty states provide actionable next steps — a new user seeing an empty state can follow the guidance to configure that coverage type without consulting external documentation.
- **SC-007**: Coverage JSON output is parseable by standard JSON tools and can be consumed by CI pipelines for quality gates (e.g., "fail if documentation coverage drops below 80%").
- **SC-008**: Treemap visualization correctly sizes blocks proportional to test count and applies color coding based on automation percentage thresholds.
