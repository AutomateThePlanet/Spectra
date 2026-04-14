# Tasks: Coverage-Aware Behavior Analysis

**Input**: Design documents from `/specs/044-coverage-aware-analysis/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — the spec explicitly defines a test plan with 17 tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Create new files and scaffolding for coverage-aware analysis

- [x] T001 Create `CoverageSnapshot` model and `UncoveredCriterion` record in `src/Spectra.CLI/Agent/Analysis/CoverageSnapshot.cs`
- [x] T002 [P] Create `CoverageSnapshotBuilder` class with `BuildAsync` method in `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs`
- [x] T003 [P] Create `CoverageContextFormatter` static class with `Format(CoverageSnapshot, CoverageContextMode)` method in `src/Spectra.CLI/Agent/Analysis/CoverageContextFormatter.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core snapshot building logic that all user stories depend on

**CoverageSnapshotBuilder** must read three independent data sources and produce a `CoverageSnapshot`:

- [x] T004 Implement `ReadSuiteIndexAsync` in `CoverageSnapshotBuilder` — read `_index.json` via `IndexWriter.ReadAsync()`, extract test titles, criteria IDs, and source_refs from `TestIndexEntry` fields. Return empty data if file missing. File: `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs`
- [x] T005 Implement `ReadCriteriaAsync` in `CoverageSnapshotBuilder` — read `.criteria.yaml` files via `CriteriaFileReader.ReadAsync()` using `CriteriaIndexReader` to locate files. Cross-reference criteria IDs against covered set. Return uncovered criteria list. File: `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs`
- [x] T006 Implement `ReadDocSectionRefsAsync` in `CoverageSnapshotBuilder` — read `docs/_index.md` via `DocumentIndexReader`, extract section headings as source refs. Cross-reference against covered source_refs. Return uncovered refs. File: `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs`
- [x] T007 Implement `CoverageContextFormatter.Format()` — format `CoverageSnapshot` as a markdown block for prompt injection. Full mode: include covered criteria IDs, uncovered criteria details, covered/uncovered source refs, truncated test titles (80 char cap). Summary mode (>500 tests): omit title list, include only stats and uncovered items. Empty snapshot: return empty string. File: `src/Spectra.CLI/Agent/Analysis/CoverageContextFormatter.cs`

**Checkpoint**: Snapshot builder and formatter are complete — can build and format coverage data from any suite

---

## Phase 3: User Story 1 - Gap-Only Analysis for Mature Suites (Priority: P1) MVP

**Goal**: Make `BehaviorAnalyzer` and `GenerateHandler` coverage-aware so analysis only recommends tests for genuine gaps.

**Independent Test**: Run `spectra ai generate --analyze-only` on a suite with known coverage. Verify recommended count reflects actual gap.

### Tests for User Story 1

- [x] T008 [P] [US1] Unit test `CoverageSnapshotBuilder_EmptySuite_ReturnsZeros` — new suite with no `_index.json` returns all-zero snapshot. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T009 [P] [US1] Unit test `CoverageSnapshotBuilder_WithTests_CountsCorrectly` — suite with 231 test entries returns `ExistingTestCount=231` and correct title list. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T010 [P] [US1] Unit test `CoverageSnapshotBuilder_CrossRefsCriteria` — 41 criteria with 38 covered by tests returns 3 uncovered criteria with correct IDs. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T011 [P] [US1] Unit test `CoverageSnapshotBuilder_CoveredSourceRefs` — tests with source_refs cross-referenced against doc index returns correct covered/uncovered sets. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T012 [P] [US1] Unit test `CoverageContextFormatter_FullMode_IncludesTitlesAndCriteria` — snapshot with <500 tests formats full coverage block with all sections. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageContextFormatterTests.cs`
- [x] T013 [P] [US1] Unit test `CoverageContextFormatter_EmptySnapshot_ReturnsEmpty` — empty snapshot produces empty string (no coverage section). File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageContextFormatterTests.cs`
- [x] T014 [P] [US1] Unit test `CoverageContextFormatter_TruncatesTitlesAt80Chars` — title with 120 chars is truncated to 80 in formatted output. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageContextFormatterTests.cs`

### Implementation for User Story 1

- [x] T015 [US1] Add `{{coverage_context}}` placeholder to `behavior-analysis.md` template — insert between distribution guidelines and `{{#if testimize_enabled}}` block. Add placeholder metadata to YAML frontmatter. File: `src/Spectra.CLI/Prompts/Content/behavior-analysis.md`
- [x] T016 [US1] Modify `BehaviorAnalyzer.AnalyzeAsync` — add optional `CoverageSnapshot? snapshot = null` parameter. When snapshot provided and has data, use `CoverageContextFormatter.Format()` to build coverage context string and pass as `coverage_context` placeholder value to template resolution. File: `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`
- [x] T017 [US1] Modify `BehaviorAnalyzer.BuildAnalysisPrompt` — add `coverageContext` parameter. Include in placeholder values dict as `coverage_context`. File: `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`
- [x] T018 [US1] Modify `GenerateHandler.ExecuteDirectModeAsync` — after loading existing tests and before calling `BehaviorAnalyzer.AnalyzeAsync`, build `CoverageSnapshot` via `CoverageSnapshotBuilder.BuildAsync`. Pass snapshot to `AnalyzeAsync`. Update all 3 call sites. File: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T019 [US1] When `CoverageSnapshot` is available, set `AlreadyCovered` from snapshot's `ExistingTestCount` (accurate count from index) instead of `CountCoveredBehaviors` title-similarity heuristic. Keep heuristic as fallback when no snapshot. File: `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`

**Checkpoint**: Analysis on mature suites now recommends only gap tests. New suites work exactly as before.

---

## Phase 4: User Story 2 - Graceful Degradation for New Suites (Priority: P1)

**Goal**: Ensure new suites with missing data sources work identically to current behavior.

**Independent Test**: Run `spectra ai generate` on a new suite with no index/criteria/docs. Verify analysis works as before.

### Tests for User Story 2

- [x] T020 [P] [US2] Unit test `CoverageSnapshotBuilder_MissingIndex_GracefulFallback` — no `_index.json` file returns empty snapshot (no crash). File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T021 [P] [US2] Unit test `CoverageSnapshotBuilder_MissingCriteria_PartialSnapshot` — no criteria files returns snapshot with test titles and source_refs but empty criteria. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`
- [x] T022 [P] [US2] Unit test `CoverageSnapshotBuilder_MissingDocIndex_PartialSnapshot` — no `docs/_index.md` returns snapshot with titles and criteria but empty source_refs. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageSnapshotBuilderTests.cs`

### Implementation for User Story 2

- [x] T023 [US2] Add try/catch with graceful fallback in each `Read*Async` method of `CoverageSnapshotBuilder` — file not found, parse errors, or empty files return empty collections (not exceptions). Log warnings for parse failures. File: `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs`
- [x] T024 [US2] Ensure `GenerateHandler` passes `null` snapshot (or empty snapshot) to `BehaviorAnalyzer` when snapshot has zero data — analyzer falls back to existing behavior. File: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`

**Checkpoint**: New suites produce identical results to pre-feature behavior.

---

## Phase 5: User Story 3 - Coverage Summary in Analysis Output (Priority: P2)

**Goal**: Display structured coverage summary in analysis output (terminal and JSON).

**Independent Test**: Run analysis with `--output-format json` and verify new fields are present.

### Tests for User Story 3

- [x] T025 [P] [US3] Unit test `GenerateAnalysis_NewFields_Serialize` — JSON round-trip includes `existing_test_count`, `total_criteria`, `covered_criteria`, `uncovered_criteria`, `uncovered_criteria_ids`. File: `tests/Spectra.CLI.Tests/Results/GenerateAnalysisCoverageTests.cs`
- [x] T026 [P] [US3] Unit test `GenerateAnalysis_BackwardCompat` — JSON without new fields deserializes with defaults (0, []). File: `tests/Spectra.CLI.Tests/Results/GenerateAnalysisCoverageTests.cs`
- [x] T027 [P] [US3] Unit test `AnalysisPresenter_ShowsCoverageSummary` — output includes "existing tests", criteria coverage ratio, doc coverage ratio when snapshot data present. File: `tests/Spectra.CLI.Tests/Output/AnalysisPresenterCoverageTests.cs`
- [x] T028 [P] [US3] Unit test `AnalysisPresenter_ZeroGap` — 0 uncovered criteria/sections shows "Suite fully covered" message. File: `tests/Spectra.CLI.Tests/Output/AnalysisPresenterCoverageTests.cs`

### Implementation for User Story 3

- [x] T029 [US3] Add new fields to `GenerateAnalysis` class — `ExistingTestCount`, `TotalCriteria`, `CoveredCriteria`, `UncoveredCriteria` (int, default 0), `UncoveredCriteriaIds` (List\<string\>, default []). Add `[JsonPropertyName]` attributes. File: `src/Spectra.CLI/Results/GenerateResult.cs`
- [x] T030 [US3] Populate `GenerateAnalysis` coverage fields from `CoverageSnapshot` in `GenerateHandler` where `GenerateAnalysis` is constructed from `BehaviorAnalysisResult`. File: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T031 [US3] Modify `AnalysisPresenter.DisplayBreakdown` — when coverage data is available (ExistingTestCount > 0), render coverage summary section: existing test count, criteria coverage ratio, doc section coverage ratio, gap-only recommendation. File: `src/Spectra.CLI/Output/AnalysisPresenter.cs`
- [x] T032 [US3] Add `AnalysisPresenter.DisplayAllCovered` enhancement — when 0 uncovered criteria and 0 uncovered source_refs, show "Suite fully covered" message. File: `src/Spectra.CLI/Output/AnalysisPresenter.cs`

**Checkpoint**: Analysis output shows accurate coverage summary in both human and JSON formats.

---

## Phase 6: User Story 4 - Token Budget Management for Large Suites (Priority: P2)

**Goal**: Switch to summary mode for suites with >500 tests to stay within token budget.

**Independent Test**: Build snapshot for a suite with 600 entries and verify prompt omits title list.

### Tests for User Story 4

- [x] T033 [P] [US4] Unit test `CoverageContextFormatter_SummaryMode_Over500Tests` — snapshot with 600 tests produces summary mode (no title list, includes stats). File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageContextFormatterTests.cs`
- [x] T034 [P] [US4] Unit test `CoverageContextFormatter_SummaryMode_StillIncludesUncovered` — summary mode still includes uncovered criteria and source refs. File: `tests/Spectra.CLI.Tests/Agent/Analysis/CoverageContextFormatterTests.cs`

### Implementation for User Story 4

- [x] T035 [US4] Add `CoverageContextMode` enum (`Full`, `Summary`) to `CoverageSnapshot.cs`. Add `Mode` computed property: `ExistingTestCount > 500 ? Summary : Full`. File: `src/Spectra.CLI/Agent/Analysis/CoverageSnapshot.cs`
- [x] T036 [US4] Update `CoverageContextFormatter.Format()` to check `Mode` — in Summary mode, omit `### Existing Test Titles` section and `### Covered Acceptance Criteria` list. Add note about title list omission. File: `src/Spectra.CLI/Agent/Analysis/CoverageContextFormatter.cs`

**Checkpoint**: Large suites get accurate analysis without exceeding token budget.

---

## Phase 7: User Story 5 - Progress Page Coverage Snapshot (Priority: P3)

**Goal**: Show coverage snapshot in `.spectra-progress.html` during analysis phase.

**Independent Test**: Generate with progress page and verify HTML contains coverage elements.

### Tests for User Story 5

- [x] T037 [P] [US5] Unit test `ProgressPage_ShowsCoverageSnapshot` — progress JSON with coverage data produces HTML with existing test count badge and criteria coverage bar. File: `tests/Spectra.CLI.Tests/Progress/ProgressPageCoverageTests.cs`

### Implementation for User Story 5

- [x] T038 [US5] Add coverage snapshot fields to `ProgressSnapshot` model (or pass via JSON data) — `existing_test_count`, `criteria_coverage` (e.g., "38/41"), `mode` ("gap-only"). File: `src/Spectra.CLI/Progress/ProgressPageWriter.cs`
- [x] T039 [US5] Update `ProgressPageWriter.WriteProgressPage` HTML template — during analysis phase, render existing test count badge, criteria coverage mini-bar, and "Gap-only analysis" label when coverage data present. File: `src/Spectra.CLI/Progress/ProgressPageWriter.cs`

**Checkpoint**: Progress page shows coverage context during generation.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, SKILL updates, and template validation

- [x] T040 [P] Update `behavior-analysis.md` template frontmatter — add `coverage_context` to placeholders list with description. File: `src/Spectra.CLI/Prompts/Content/behavior-analysis.md`
- [x] T041 [P] Unit test `PromptTemplate_CoverageContextPlaceholder` — validate `behavior-analysis.md` template resolves `{{coverage_context}}` without errors. File: `tests/Spectra.CLI.Tests/Prompts/PromptTemplateCoverageTests.cs`
- [x] T042 [P] Update coverage documentation — add "Coverage-Aware Generation" section explaining the analysis step now considers existing tests. File: `docs/coverage.md`
- [x] T043 [P] Update CLI reference documentation — note that `spectra ai generate` analysis is now coverage-aware. File: `docs/cli-reference.md`
- [x] T044 [P] Update customization documentation — note `behavior-analysis.md` template has new `{{coverage_context}}` placeholder. File: `docs/customization.md`
- [x] T045 Run `dotnet build` and `dotnet test` to verify all tests pass and no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on T001 (model) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 completion — core feature
- **US2 (Phase 4)**: Depends on Phase 2 completion — can run in parallel with US1
- **US3 (Phase 5)**: Depends on US1 (needs snapshot wiring in GenerateHandler)
- **US4 (Phase 6)**: Depends on Phase 2 (formatter exists) — can run in parallel with US1/US3
- **US5 (Phase 7)**: Depends on US3 (needs progress snapshot fields)
- **Polish (Phase 8)**: Depends on US1-US4 completion

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational — no story dependencies
- **US2 (P1)**: Depends on Foundational — no story dependencies (can parallelize with US1)
- **US3 (P2)**: Depends on US1 (GenerateAnalysis fields populated from snapshot)
- **US4 (P2)**: Depends on Foundational only (formatter changes)
- **US5 (P3)**: Depends on US3 (progress snapshot uses same data)

### Within Each User Story

- Tests marked [P] can run in parallel
- Implementation tasks are sequential within a story (file dependencies)

### Parallel Opportunities

- T001, T002, T003 can all run in parallel (different files)
- T004, T005, T006 are independent reads — implement in parallel
- All test tasks within a story marked [P] can run in parallel
- US1 and US2 can be developed in parallel after Phase 2
- US4 can run in parallel with US1/US3 (different file: formatter)
- All Polish [P] tasks can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together:
Task: "T008 CoverageSnapshotBuilder_EmptySuite_ReturnsZeros"
Task: "T009 CoverageSnapshotBuilder_WithTests_CountsCorrectly"
Task: "T010 CoverageSnapshotBuilder_CrossRefsCriteria"
Task: "T011 CoverageSnapshotBuilder_CoveredSourceRefs"
Task: "T012 CoverageContextFormatter_FullMode_IncludesTitlesAndCriteria"
Task: "T013 CoverageContextFormatter_EmptySnapshot_ReturnsEmpty"
Task: "T014 CoverageContextFormatter_TruncatesTitlesAt80Chars"

# Then implement sequentially:
Task: "T015 Add {{coverage_context}} placeholder to template"
Task: "T016 Modify BehaviorAnalyzer.AnalyzeAsync"
Task: "T017 Modify BehaviorAnalyzer.BuildAnalysisPrompt"
Task: "T018 Modify GenerateHandler wiring"
Task: "T019 Set AlreadyCovered from snapshot"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T007)
3. Complete Phase 3: User Story 1 (T008-T019)
4. **STOP and VALIDATE**: Run `spectra ai generate --analyze-only` on a suite with known tests
5. Verify recommended count reflects actual gap

### Incremental Delivery

1. Setup + Foundational → Model and builder ready
2. Add US1 → Gap-only analysis works → Core value delivered (MVP!)
3. Add US2 → Graceful degradation verified → Safe for all suites
4. Add US3 → Coverage summary in output → User-facing improvement
5. Add US4 → Large suite support → Scalability
6. Add US5 → Progress page → Visual polish
7. Polish → Docs and template validation → Ship-ready

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Tests are included per the spec's explicit test plan
- 45 total tasks across 8 phases
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
