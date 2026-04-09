# Feature Specification: Coverage Semantics Fix & Criteria-Generation Pipeline

**Feature Branch**: `028-coverage-criteria-fix`  
**Created**: 2026-04-09  
**Status**: Draft  
**Input**: User description: "Fix coverage calculation semantics and wire criteria into the generation pipeline"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Coverage Percentages Reflect Correct Semantics (Priority: P1)

A QA lead runs `spectra ai analyze --coverage` and sees accurate numbers for all three coverage dimensions. Documentation coverage shows which documents have test cases (not which have automation). Acceptance criteria coverage shows which criteria are referenced by test cases via the `criteria: []` frontmatter field. Automation coverage shows which test cases have `automated_by` links resolving to real files. The dashboard displays these same correct numbers.

**Why this priority**: Without correct coverage semantics, the entire coverage system is misleading. The dashboard currently shows 0% acceptance criteria coverage despite 259 test cases and 753 criteria, making the feature useless.

**Independent Test**: Run `spectra ai analyze --coverage` on a project with documents, criteria, and test cases. Verify documentation coverage counts docs with linked tests, criteria coverage counts criteria referenced by tests, and automation coverage counts tests with resolved `automated_by` links.

**Acceptance Scenarios**:

1. **Given** a document with a matching test suite (e.g., `checkout.md` and suite `checkout`), **When** running coverage analysis, **Then** the document shows as covered regardless of whether tests have automation.
2. **Given** an acceptance criterion `AC-001` and a test with `criteria: [AC-001]` in frontmatter, **When** running coverage analysis, **Then** the criterion shows as covered.
3. **Given** 10 criteria where 3 are referenced by tests, **When** running coverage analysis, **Then** acceptance criteria coverage shows 30%.
4. **Given** a test with `automated_by: [LoginTests.cs]` where the file exists in `automation_dirs`, **When** running coverage analysis, **Then** the test shows as automated.
5. **Given** a test with `automated_by: [MissingFile.cs]` where the file does not exist, **When** running coverage analysis, **Then** the test shows as not automated.
6. **Given** a document with tests but zero automation, **When** running coverage analysis, **Then** documentation coverage is 100% (tests exist) and automation coverage reflects the actual automation rate.

---

### User Story 2 - Generation Produces Criteria-Linked Tests (Priority: P1)

A QA engineer runs `spectra ai generate --suite checkout` on a project that has extracted acceptance criteria. The generation pipeline loads criteria relevant to the checkout suite, includes them in the AI prompt, and the generated test files contain `criteria: [AC-XXX]` fields in their YAML frontmatter linking tests to the criteria they verify.

**Why this priority**: Without criteria linkage in generation, criteria coverage will always be 0% because no test ever references a criterion ID. This is the pipeline that populates the data for User Story 1.

**Independent Test**: Generate tests for a suite that has associated `.criteria.yaml` files. Verify the generated test files contain `criteria: []` frontmatter fields with valid criterion IDs from the input criteria.

**Acceptance Scenarios**:

1. **Given** a suite `checkout` with docs/checkout.md and 5 acceptance criteria in `docs/criteria/checkout.criteria.yaml`, **When** generating tests, **Then** the AI prompt includes all 5 criteria IDs and text.
2. **Given** criteria with `component: checkout` in a different document's criteria file, **When** generating tests for suite `checkout`, **Then** those criteria are also loaded as context.
3. **Given** criteria context provided to the AI, **When** tests are generated, **Then** each test file contains a `criteria: []` field in YAML frontmatter listing the IDs of criteria it verifies.
4. **Given** generated test files with `criteria: [AC-XXX]`, **When** running coverage analysis, **Then** the referenced criteria show as covered.

---

### User Story 3 - Update Flow Detects Criteria Changes (Priority: P2)

A QA engineer modifies acceptance criteria (changes text or deletes criteria) and then runs `spectra ai update --suite checkout`. The update flow detects that some tests reference criteria that have changed (OUTDATED) or been deleted (ORPHANED), and reports uncovered criteria that need new tests.

**Why this priority**: Criteria drift over time as requirements change. Without update-flow awareness, stale test-to-criteria links accumulate silently.

**Independent Test**: Modify criteria text and delete a criterion, then run update and verify correct classification of affected tests.

**Acceptance Scenarios**:

1. **Given** a test referencing `AC-001` whose text has changed since generation, **When** running update, **Then** the test is classified as OUTDATED.
2. **Given** a test referencing `AC-001` which has been deleted from criteria files, **When** running update, **Then** the test is classified as ORPHANED.
3. **Given** 10 criteria where 3 are not referenced by any test, **When** running update, **Then** suggestions mention "3 uncovered criteria."

---

### User Story 4 - Dashboard Displays Correct Coverage (Priority: P2)

A team lead opens the SPECTRA dashboard and sees three coverage cards with correct percentages. The coverage gaps table shows gap types (No Tests, No Criteria Coverage, Low Automation). Expanding a criterion in the acceptance criteria drill-down shows which test IDs reference it.

**Why this priority**: The dashboard is the primary visibility tool for stakeholders. Incorrect numbers undermine trust.

**Independent Test**: Generate the dashboard after running coverage analysis and verify the HTML displays correct numbers matching the CLI output.

**Acceptance Scenarios**:

1. **Given** coverage data with 12 docs (6 covered), 753 criteria (180 covered), 259 tests (0 automated), **When** viewing the dashboard, **Then** cards show 50% doc coverage, 23.9% criteria coverage, 0% automation.
2. **Given** coverage gaps data, **When** viewing the gaps table, **Then** each gap shows its type (No Tests, No Criteria Coverage, Low Automation).
3. **Given** criteria coverage data, **When** expanding a criterion in the drill-down, **Then** linked test IDs are displayed.

---

### Edge Cases

- What happens when a test references a criterion ID that doesn't exist in any criteria file? The criterion is ignored in coverage calculation (only known criteria are counted), and the update flow flags the test as having an invalid reference.
- What happens when criteria files exist but no tests have `criteria: []` fields? Criteria coverage is 0%. The coverage report suggests running generation to populate criteria links.
- What happens when a test uses the legacy `requirements: []` field instead of `criteria: []`? Both fields are read for backward compatibility. The requirement IDs are matched against criteria IDs.
- What happens when no criteria files exist at all? Criteria coverage section shows "No criteria extracted. Run `spectra ai analyze --extract-criteria` first." with 0 total and 0 covered.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Documentation coverage MUST count a document as "covered" when at least one test case references it (via grounding source, suite name match, or source document field).
- **FR-002**: Documentation coverage MUST NOT use `automated_by` status to determine document coverage.
- **FR-003**: Acceptance criteria coverage MUST count a criterion as "covered" when at least one test case lists its ID in the `criteria: []` frontmatter field.
- **FR-004**: Acceptance criteria coverage MUST also read the legacy `requirements: []` frontmatter field for backward compatibility.
- **FR-005**: Automation coverage MUST count a test case as "automated" when its `automated_by: []` field contains at least one entry that resolves to an existing file in configured `automation_dirs`.
- **FR-006**: The test generation pipeline MUST load acceptance criteria relevant to the target suite before calling the AI model.
- **FR-007**: Criteria loading MUST match by document association (per-doc `.criteria.yaml` files) and by component field matching the suite name.
- **FR-008**: The AI generation prompt MUST include a section listing acceptance criteria IDs and text when criteria are available.
- **FR-009**: Generated test files MUST contain a `criteria: []` field in YAML frontmatter listing the IDs of criteria the test verifies.
- **FR-010**: The `TestCaseFrontmatter` model MUST have a `Criteria` property that serializes to/from the `criteria` YAML field.
- **FR-011**: The test update flow MUST classify tests as OUTDATED when referenced criteria text has changed.
- **FR-012**: The test update flow MUST classify tests as ORPHANED when referenced criteria have been deleted.
- **FR-013**: The update flow MUST report the count of uncovered criteria as a suggestion.
- **FR-014**: The dashboard MUST display correct percentages for all three coverage dimensions.
- **FR-015**: The coverage gaps table MUST include gap type labels (No Tests, No Criteria Coverage, Low Automation).
- **FR-016**: Coverage reports (JSON and markdown formats) MUST output correct numbers for all three dimensions.

### Key Entities

- **Document**: A markdown file in the docs directory. "Covered" when referenced by at least one test case.
- **Acceptance Criterion**: An item from `.criteria.yaml` files with a unique ID (e.g., AC-001). "Covered" when referenced by at least one test's `criteria: []` field.
- **Test Case**: A markdown file in `tests/{suite}/` with YAML frontmatter. Links to documents via `grounding.source`, to criteria via `criteria: []`, and to automation via `automated_by: []`.
- **Automation File**: A source code file in `automation_dirs` that implements automated testing. Linked from test cases via `automated_by: []`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After fix, documentation coverage reports non-zero coverage for projects with test suites matching document names (currently may report incorrect values).
- **SC-002**: After fix, acceptance criteria coverage reports non-zero coverage for projects where tests have `criteria: []` fields populated (currently reports 0%).
- **SC-003**: After fix, generating tests for a suite with criteria produces test files where 80%+ of files contain at least one criterion ID in the `criteria: []` field.
- **SC-004**: Coverage percentages on the dashboard match the CLI output exactly (no discrepancies between the two views).
- **SC-005**: All existing tests continue to pass after the fix (zero regressions).
- **SC-006**: New tests covering the three coverage analyzers, generation pipeline criteria loading, and update flow criteria detection total at least 20 new test cases.

## Assumptions

- The `criteria: []` field is the canonical way to link test cases to acceptance criteria. The legacy `requirements: []` field is supported for backward compatibility only.
- AI models (GPT-4o, DeepSeek V3, etc.) can follow instructions to include `criteria: []` fields in generated YAML frontmatter when given criteria context in the prompt.
- Suite names correspond to document names by convention (suite `checkout` -> `docs/checkout.md`). This is the existing convention and is not changed by this spec.
- The `.criteria.yaml` file format and `_criteria_index.yaml` index from spec 023 are stable and correctly populated.
- The `automated_by` auto-link mechanism (`--auto-link` flag) is out of scope and unchanged.
