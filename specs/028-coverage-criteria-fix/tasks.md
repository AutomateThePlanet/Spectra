# Tasks: Coverage Semantics Fix & Criteria-Generation Pipeline

**Input**: Design documents from `/specs/028-coverage-criteria-fix/`
**Prerequisites**: plan.md (required), spec.md (required)

**Tests**: Included — the spec requires at least 20 new test cases.

**Organization**: Tasks grouped by user story. US1 and US2 are both P1 but US2 depends on the generation pipeline being wired (which feeds data for US1 coverage).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Foundational — Model & Writer Fixes

**Purpose**: Ensure the data model and file writer support criteria correctly before wiring the pipeline.

- [x] T001 Audit `TestCaseParser` to verify `Criteria` field is propagated from frontmatter to `TestCase` — if missing, add the assignment in `src/Spectra.Core/Parsing/TestCaseParser.cs`
- [x] T002 Fix `TestFileWriter` to always write `criteria: []` field in YAML frontmatter (even when empty) so the field is visible and editable in `src/Spectra.CLI/IO/TestFileWriter.cs`

**Checkpoint**: TestCaseFrontmatter → TestCase → file roundtrip preserves `criteria` field.

---

## Phase 2: User Story 2 — Generation Produces Criteria-Linked Tests (Priority: P1) 🎯 MVP

**Goal**: Wire criteria loading into the generation pipeline so generated tests contain `criteria: [AC-XXX]` in frontmatter.

**Independent Test**: Generate tests for a suite with `.criteria.yaml` files and verify output files contain `criteria:` field with valid IDs.

### Implementation for User Story 2

- [x] T003 [US2] Add criteria loading to `GenerateHandler`: before calling AI agent, load criteria from per-doc `.criteria.yaml` files matching suite documents, plus criteria with matching `component` field, in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T004 [US2] Format loaded criteria as context string (ID, RFC 2119 level, text, component) for AI prompt in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T005 [US2] Add `criteriaContext` parameter to `GenerateTestsAsync()` method and pass it through to `BuildFullPrompt()` in `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`
- [x] T006 [US2] Verify AI response parser extracts `criteria` field from generated test JSON — confirm `criteria` list is mapped to `TestCase.Criteria` in `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`
- [x] T007 [US2] Run `dotnet test` to verify all existing tests pass with the new parameter added to `GenerateTestsAsync`

**Checkpoint**: `spectra ai generate --suite X` on a project with criteria files produces test files containing `criteria: [AC-XXX]`.

---

## Phase 3: User Story 1 — Coverage Percentages Reflect Correct Semantics (Priority: P1)

**Goal**: Verify and add regression tests proving the three coverage analyzers use correct semantics.

**Independent Test**: Run coverage analysis on test fixtures with known criteria links and verify correct percentages.

### Implementation for User Story 1

- [x] T008 [P] [US1] Add test: document with matching test suite (no automation) is covered in `tests/Spectra.Core.Tests/Coverage/DocumentationCoverageAnalyzerTests.cs`
- [x] T009 [P] [US1] Add test: document with grounding.source reference is covered in `tests/Spectra.Core.Tests/Coverage/DocumentationCoverageAnalyzerTests.cs`
- [x] T010 [P] [US1] Add test: document with no tests is not covered in `tests/Spectra.Core.Tests/Coverage/DocumentationCoverageAnalyzerTests.cs`
- [x] T011 [P] [US1] Add test: criterion referenced in test `criteria: []` is covered in `tests/Spectra.Core.Tests/Coverage/` (AcceptanceCriteriaCoverageAnalyzer test file)
- [x] T012 [P] [US1] Add test: criterion referenced via legacy `requirements: []` is covered (backward compat) in `tests/Spectra.Core.Tests/Coverage/`
- [x] T013 [P] [US1] Add test: criterion not referenced by any test is not covered in `tests/Spectra.Core.Tests/Coverage/`
- [x] T014 [P] [US1] Add test: criterion referenced by multiple tests shows correct linked test count in `tests/Spectra.Core.Tests/Coverage/`
- [x] T015 [P] [US1] Add test: coverage percentage calculation with mixed covered/uncovered criteria in `tests/Spectra.Core.Tests/Coverage/`
- [x] T016 [P] [US1] Add test: automation coverage — test with `automated_by` resolving to existing file is automated in `tests/Spectra.Core.Tests/Coverage/AutomationScannerTests.cs`
- [x] T017 [P] [US1] Add test: automation coverage — test with `automated_by` pointing to missing file is not automated in `tests/Spectra.Core.Tests/Coverage/AutomationScannerTests.cs`

**Checkpoint**: All three coverage dimensions have regression tests proving correct semantics.

---

## Phase 4: User Story 3 — Update Flow Detects Criteria Changes (Priority: P2)

**Goal**: Verify TestClassifier handles orphaned/outdated criteria and UpdateHandler passes criteria data.

**Independent Test**: Run update with changed/deleted criteria and verify correct classification.

### Implementation for User Story 3

- [x] T018 [US3] Verify `UpdateHandler` loads criteria data and passes it to `TestClassifier` — if not, add criteria loading in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`
- [x] T019 [P] [US3] Add test: test referencing deleted criterion is classified as ORPHANED in `tests/Spectra.Core.Tests/Update/` or `tests/Spectra.CLI.Tests/`
- [x] T020 [P] [US3] Add test: test referencing criterion with changed text is classified as OUTDATED in `tests/Spectra.Core.Tests/Update/`
- [x] T021 [P] [US3] Add test: update suggestions include count of uncovered criteria in `tests/Spectra.CLI.Tests/`

**Checkpoint**: `spectra ai update` correctly detects criteria-related test staleness.

---

## Phase 5: User Story 4 — Dashboard Displays Correct Coverage (Priority: P2)

**Goal**: Verify dashboard displays correct percentages and gap types.

**Independent Test**: Generate dashboard and verify HTML shows correct numbers matching CLI output.

### Implementation for User Story 4

- [x] T022 [US4] Verify `DataCollector` populates criteria coverage data correctly with `AcceptanceCriteriaCoverageAnalyzer` — read and confirm in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T023 [US4] Verify `app.js` reads coverage JSON correctly and displays three coverage cards with correct labels in `dashboard-site/scripts/app.js`
- [x] T024 [P] [US4] Add test: DataCollector produces correct criteria coverage when tests have `criteria: []` fields in `tests/Spectra.CLI.Tests/Dashboard/`
- [x] T025 [P] [US4] Add test: DataCollector produces 0% criteria coverage when no tests have criteria links in `tests/Spectra.CLI.Tests/Dashboard/`

**Checkpoint**: Dashboard shows correct coverage for all three dimensions.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates and final validation.

- [x] T026 Run full test suite (`dotnet test`) to verify zero regressions
- [x] T027 [P] Update `CLAUDE.md`: add 028 entry to Recent Changes with coverage fix description
- [x] T028 [P] Update `PROJECT-KNOWLEDGE.md`: clarify coverage semantics (doc=test existence, criteria=criteria field, automation=automated_by)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately
- **Phase 2 (US2 — Generation Pipeline)**: Depends on Phase 1 (model/writer fixes)
- **Phase 3 (US1 — Coverage Tests)**: No dependency on Phase 2 (tests verify existing analyzers)
- **Phase 4 (US3 — Update Flow)**: Can start after Phase 1
- **Phase 5 (US4 — Dashboard)**: Can start after Phase 1
- **Phase 6 (Polish)**: Depends on all phases complete

### User Story Dependencies

- **US1 (Coverage Semantics)**: Independent — tests verify existing analyzers
- **US2 (Generation Pipeline)**: Depends on Phase 1 foundational fixes
- **US3 (Update Flow)**: Independent — verifies existing TestClassifier
- **US4 (Dashboard)**: Independent — verifies existing DataCollector

### Parallel Opportunities

- **Phase 1**: T001 and T002 are sequential (T002 depends on T001 verification)
- **Phase 2**: T003-T005 are sequential (call chain wiring)
- **Phase 3**: All 10 tests (T008-T017) can run in parallel — different test files
- **Phase 4**: T019-T021 can run in parallel after T018
- **Phase 5**: T024-T025 can run in parallel after T022-T023

---

## Implementation Strategy

### MVP First (US2 — Generation Pipeline)

1. Phase 1: Fix model/writer (T001-T002)
2. Phase 2: Wire criteria into generation (T003-T007)
3. **STOP and VALIDATE**: Generate tests, verify `criteria: []` in output
4. Run `dotnet test` — all existing tests pass

### Full Delivery

1. Phase 1 → Phase 2 (MVP)
2. Phase 3: Coverage semantic tests (parallel)
3. Phase 4-5: Update flow + dashboard verification
4. Phase 6: Documentation
5. Final `dotnet test` — all tests pass

---

## Notes

- Total tasks: 28
- Phase 1: 2 tasks
- Phase 2 (US2): 5 tasks
- Phase 3 (US1): 10 tasks (all parallel)
- Phase 4 (US3): 4 tasks
- Phase 5 (US4): 4 tasks
- Phase 6: 3 tasks
- Most coverage analyzers are already correct — the main code change is wiring criteria into GenerateHandler
