# Tasks: Universal Progress/Result for SKILL-Wrapped Commands

**Input**: Design documents from `/specs/025-universal-skill-progress/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included per the constitution's Test-Required Discipline (integration tests for CLI command workflows, unit tests for core services).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Create new files and establish shared infrastructure skeleton

- [x] T001 Create `src/Spectra.CLI/Progress/ProgressPhases.cs` with static phase arrays for all 6 command types (Generate, Update, DocsIndex, Coverage, ExtractCriteria, Dashboard) per data-model.md phase definitions
- [x] T002 Create `src/Spectra.CLI/Progress/ProgressManager.cs` implementing Reset(), StartAsync(), UpdateAsync(), CompleteAsync(), FailAsync() methods. Extract the file I/O pattern from GenerateHandler lines 1696-1703 (FlushWriteFile with FileStream.Flush(true)). Use ProgressPageWriter.WriteProgressPage() for HTML updates. Catch and log I/O exceptions without failing the command (FR-015). Constructor takes command name, phases array, and optional title string.
- [x] T003 Create `src/Spectra.CLI/Results/UpdateResult.cs` with fields: suite, testsUpdated, testsRemoved, testsUnchanged, classification (upToDate, outdated, orphaned, redundant), filesModified, filesDeleted. Inherit from CommandResult.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Refactor existing handlers to use ProgressManager, proving the abstraction works before adding it to new handlers

**CRITICAL**: No user story work (Phase 3+) can begin until this phase validates ProgressManager works correctly with existing behavior.

- [x] T004 Refactor `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` to use ProgressManager. Replace inline FlushWriteFile(), WriteResultFile(), WriteInProgressResultFile(), WriteErrorResultFile(), and DeleteResultFile() methods (lines 1682-1811) with ProgressManager calls. The GenerateHandler must create a ProgressManager with ProgressPhases.Generate phases. All existing behavior must be preserved — this is a pure internal refactor.
- [x] T005 Refactor `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` to use ProgressManager. Replace inline WriteProgressResult(), WriteResultFile(), FlushWriteFile(), and DeleteResultFile() methods (lines 236-327) with ProgressManager calls. Use ProgressPhases.DocsIndex phases. All existing behavior must be preserved.
- [x] T006 Extend `src/Spectra.CLI/Progress/ProgressPageWriter.cs` to support new phase stepper configs. Currently BuildStepper() handles "analyzing/analyzed/generating" and "scanning/indexing/extracting-criteria" status values. Add cases for: "classifying/updating/verifying" (update), "scanning-tests/analyzing-docs/analyzing-criteria/analyzing-automation" (coverage), "scanning-docs/extracting/building-index" (extract-criteria), "collecting-data/generating-html" (dashboard). Add BuildSummaryCards() variants for each new command type's summary data shape.
- [x] T007 Add title parameter support to ProgressPageWriter. Currently the page header is hardcoded or inferred from command type. Make it accept a dynamic title string from ProgressManager so it renders "SPECTRA — Documentation Index", "SPECTRA — Coverage Analysis", "SPECTRA — Test Update", "SPECTRA — Dashboard Generation" etc. (FR-014).
- [x] T008 Run full test suite (`dotnet test`) to verify GenerateHandler and DocsIndexHandler refactors introduce zero regressions. All ~1279 existing tests must pass unchanged.

**Checkpoint**: ProgressManager proven with two existing handlers. Foundation ready for new integrations.

---

## Phase 3: User Story 1 — SKILL Reads Structured Results (Priority: P1) MVP

**Goal**: Every SKILL-wrapped command writes `.spectra-result.json` with typed result data on completion and failure.

**Independent Test**: Run each SKILL-wrapped command and verify `.spectra-result.json` exists with correct typed data.

### Tests for User Story 1

- [x] T009 [P] [US1] Create `tests/Spectra.CLI.Tests/Progress/ProgressManagerTests.cs` with tests: Reset_DeletesExistingFiles, StartAsync_CreatesProgressHtml, UpdateAsync_WritesResultJsonWithCurrentPhase, CompleteAsync_WritesResultJsonWithFinalData, FailAsync_WritesErrorResult, IOExceptions_DoNotFailCommand. Use temp directory for file isolation.

### Implementation for User Story 1

- [x] T010 [US1] Add ProgressManager result-file-only integration to `src/Spectra.CLI/Commands/Validate/ValidateHandler.cs`. After validation completes, write ValidateResult to `.spectra-result.json` using ProgressManager. No progress HTML needed (fast command). Delete stale result file at start.
- [x] T011 [P] [US1] Add ProgressManager result-file-only integration to `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` for the `--import-criteria` sub-command path. After import completes, write ImportCriteriaResult to `.spectra-result.json`. No progress HTML.
- [x] T012 [P] [US1] Add ProgressManager result-file-only integration to `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` for the `--list-criteria` sub-command path. After list completes, write ListCriteriaResult to `.spectra-result.json`. No progress HTML.
- [x] T013 [US1] Add ProgressManager to `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` for the `--coverage` sub-command path. Create ProgressManager with ProgressPhases.Coverage. Call UpdateAsync at each phase (Scanning Tests, Analyzing Docs, Analyzing Criteria, Analyzing Automation). Write AnalyzeCoverageResult on completion.
- [x] T014 [US1] Add ProgressManager to `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` for the `--extract-criteria` sub-command path. Create ProgressManager with ProgressPhases.ExtractCriteria. Call UpdateAsync at each phase (Scanning Docs, Extracting per-document with progress counts, Building Index). Write ExtractCriteriaResult on completion.
- [x] T015 [US1] Add ProgressManager to `src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs`. Create ProgressManager with ProgressPhases.Dashboard. Call UpdateAsync for Collecting Data and Generating HTML phases. Write DashboardResult on completion.
- [x] T016 [US1] Add ProgressManager to `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`. Create ProgressManager with ProgressPhases.Update. Call UpdateAsync for Classifying, Updating, Verifying phases. Write UpdateResult on completion. Handle both direct and interactive modes.
- [ ] T017 [US1] Write integration tests in `tests/Spectra.CLI.Tests/Commands/` verifying result file creation: ValidateHandler_WritesResultJson, CoverageAnalysis_WritesResultJson, ExtractCriteria_WritesResultJson, Dashboard_WritesResultJson, UpdateHandler_WritesResultJson, ImportCriteria_WritesResultJson, ListCriteria_WritesResultJson. Each test runs the handler and asserts `.spectra-result.json` exists with correct command name and status.

**Checkpoint**: All 9 SKILL-wrapped commands write `.spectra-result.json`. SC-001 met.

---

## Phase 4: User Story 2 — Live Progress Page for Long-Running Commands (Priority: P1)

**Goal**: All 6 long-running commands write `.spectra-progress.html` with auto-refreshing phases.

**Independent Test**: Run each long-running command and verify progress HTML updates through all phases with auto-refresh removed on completion.

### Implementation for User Story 2

- [ ] T018 [US2] Verify ProgressManager integration in UpdateHandler (T016) creates and updates `.spectra-progress.html` during Classifying/Updating/Verifying phases. Add progress page assertions to UpdateHandler integration test. Verify auto-refresh meta tag is present during execution and removed on completion.
- [ ] T019 [US2] Verify ProgressManager integration in AnalyzeHandler coverage path (T013) creates and updates `.spectra-progress.html` during all 4 coverage phases. Add progress page assertions to coverage integration test.
- [ ] T020 [US2] Verify ProgressManager integration in AnalyzeHandler extract-criteria path (T014) creates and updates `.spectra-progress.html` during extraction phases with per-document progress messages. Add progress page assertions.
- [ ] T021 [US2] Verify ProgressManager integration in DashboardHandler (T015) creates and updates `.spectra-progress.html` during data collection and HTML generation. Add progress page assertions.
- [x] T022 [US2] Write test in `tests/Spectra.CLI.Tests/Progress/ProgressManagerTests.cs`: CompleteAsync_RemovesAutoRefreshFromHtml, FailAsync_RemovesAutoRefreshFromHtml, UpdateAsync_HtmlHasAutoRefreshTag. Verify the auto-refresh meta tag lifecycle.
- [x] T023 [US2] Write test: ProgressManager_DeletesStaleFilesAtStart — verify that when a ProgressManager is created and Reset() is called, both `.spectra-result.json` and `.spectra-progress.html` from a previous run are deleted.

**Checkpoint**: All 6 long-running commands have live progress pages. SC-002 and SC-007 met.

---

## Phase 5: User Story 3 — Shared ProgressManager Infrastructure (Priority: P1)

**Goal**: GenerateHandler refactor introduces no regressions. (Already implemented in Phase 2; this phase validates.)

**Independent Test**: Run full test suite — all existing generate and docs-index tests pass without modification.

### Implementation for User Story 3

- [x] T024 [US3] Run full generate-specific test suite and verify all GenerateHandler tests pass after Phase 2 refactor. Document any test adjustments needed (should be zero). If any tests reference internal methods that were removed (FlushWriteFile, WriteResultFile, etc.), update them to test via ProgressManager public API instead.
- [x] T025 [US3] Run full docs-index-specific test suite and verify all DocsIndexHandler tests pass after Phase 2 refactor. Document any test adjustments needed.

**Checkpoint**: SC-005 met — GenerateHandler refactor is regression-free.

---

## Phase 6: User Story 4 — Dashboard Coverage Tab Fix (Priority: P2)

**Goal**: Dashboard Coverage tab handles null data gracefully and DataCollector never returns null sections.

**Independent Test**: Generate a dashboard with and without coverage data; verify Coverage tab renders correctly in both cases.

### Implementation for User Story 4

- [x] T026 [US4] Rename `Requirements` property to `AcceptanceCriteria` in `src/Spectra.CLI/Results/AnalyzeCoverageResult.cs`. Add `[JsonPropertyName("acceptanceCriteria")]` attribute. Update all code that references `AnalyzeCoverageResult.Requirements` to use the new name.
- [x] T027 [US4] Add null-coalescing at individual section level in `src/Spectra.CLI/Dashboard/DataCollector.cs` method `BuildCoverageSummaryAsync()`. If documentation, acceptance criteria, or automation analyzer returns null for a section, replace with zero-state default (0 covered, 0 total, 0%, empty details array) instead of propagating null.
- [ ] T028 [US4] Write tests in `tests/Spectra.CLI.Tests/Dashboard/`: DataCollector_NeverReturnsNullCoverageSection (mock each analyzer to return null individually, verify zero-state fallback), DataCollector_HandlesAllNullSections (all three null, verify complete zero-state CoverageSummaryData).

**Checkpoint**: Dashboard Coverage tab renders without crashes. SC-003 met.

---

## Phase 7: User Story 5 — Terminology Rename Completion (Priority: P2)

**Goal**: Zero user-facing strings contain legacy "requirement(s)" terminology.

**Independent Test**: Run `grep -rn "requirement" --include="*.cs" src/Spectra.CLI/` and verify only backward-compat aliases and directory paths remain.

### Implementation for User Story 5

- [x] T029 [US5] Audit all `.cs` files in `src/Spectra.CLI/` for user-facing string literals containing "requirement" (excluding: `--extract-requirements` hidden alias in AnalyzeCommand.cs which is intentional backward compat, `docs/requirements/` directory path references which are actual paths). Fix any remaining user-facing strings to use "acceptance criteria". Known target: verify AnalyzeCoverageResult rename from T026 is complete.
- [x] T030 [US5] Write an audit test in `tests/Spectra.CLI.Tests/`: UserFacingStrings_DoNotContain_OldRequirementTerminology — scan string literals in Spectra.CLI assembly for "requirement" in user-facing contexts (excluding known exceptions). This ensures future regressions are caught.

**Checkpoint**: SC-004 met — terminology is consistent.

---

## Phase 8: User Story 7 — Updated SKILLs Use Progress Flow (Priority: P3)

**Goal**: All existing SKILLs follow the universal progress/result pattern.

**Independent Test**: Inspect each SKILL file and verify it includes `--no-interaction --output-format json --verbosity quiet` flags and the progress page flow.

Note: Story 6 (spectra-docs SKILL) is already complete from spec 024. Skipping to Story 7.

### Implementation for User Story 7

- [x] T031 [P] [US7] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-coverage.md` to include the 5-step progress flow: open `.spectra-progress.html` preview, run command with `--no-interaction --output-format json --verbosity quiet`, wait, read `.spectra-result.json`, present results. Ensure all command strings include the three standard flags.
- [x] T032 [P] [US7] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md` to include progress page flow for `--extract-criteria` (long-running). For `--list-criteria` and `--import-criteria` (fast), include result file reading only (no progress page). Ensure all command strings include standard flags.
- [x] T033 [P] [US7] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-dashboard.md` to include progress page flow: open preview, run with standard flags, wait, read result, then additionally open generated dashboard HTML. Ensure standard flags present.
- [x] T034 [P] [US7] Update `src/Spectra.CLI/Skills/Content/Skills/spectra-validate.md` to include result file reading after command completion (no progress page). Ensure standard flags present.
- [x] T035 [US7] Update `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md` and `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` to add universal progress/result instructions: always open `.spectra-progress.html` for long-running commands, always include `--no-interaction --output-format json --verbosity quiet`, always read `.spectra-result.json` after completion.
- [x] T036 [US7] Update SKILL hashes in `src/Spectra.CLI/Skills/SkillsManifest.cs` — recalculate SHA-256 hashes for all modified SKILL files and update the manifest's default hash dictionary.
- [x] T037 [US7] Write tests in `tests/Spectra.CLI.Tests/Skills/`: AllSkills_IncludeNoInteractionFlag (load each SKILL content string and assert `--no-interaction` is present in every command line), AllSkills_IncludeOutputFormatJson (assert `--output-format json` present), AllSkills_IncludeVerbosityQuiet (assert `--verbosity quiet` present).

**Checkpoint**: SC-006 met — all SKILLs follow universal progress pattern.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, CLAUDE.md update, and full test run

- [x] T038 Update `CLAUDE.md` Recent Changes section with spec 025 summary: ProgressManager shared infrastructure, progress/result for all SKILL commands, AnalyzeCoverageResult rename, SKILL updates, DataCollector hardening. Include new test count.
- [x] T039 Run full test suite (`dotnet test`) to verify all tests pass. Report total test count and any failures.
- [x] T040 Run `dotnet build` with no warnings to verify clean compilation across all projects.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 (ProgressManager must exist and be proven)
- **User Story 2 (Phase 4)**: Depends on Phase 3 (result file integration must exist before verifying progress pages)
- **User Story 3 (Phase 5)**: Depends on Phase 2 (validates refactor done in Phase 2)
- **User Story 4 (Phase 6)**: Can start after Phase 2 — independent of Phases 3-5
- **User Story 5 (Phase 7)**: Can start after Phase 6 (rename in T026 must happen first)
- **User Story 7 (Phase 8)**: Can start after Phase 2 — independent (static markdown changes)
- **Polish (Phase 9)**: Depends on all previous phases

### User Story Dependencies

- **US1 (Results)**: Depends on Foundational only — MVP story
- **US2 (Progress Pages)**: Depends on US1 (result integration provides the ProgressManager hookup points)
- **US3 (Infrastructure Validation)**: Depends on Foundational only — pure validation
- **US4 (Dashboard Fix)**: Independent after Foundational — different files than US1/US2
- **US5 (Rename)**: Depends on US4 (T026 does the field rename)
- **US7 (SKILL Updates)**: Independent after Foundational — static markdown files

### Within Each User Story

- Tests can be written in parallel with implementation (not strict TDD since integration tests need running handlers)
- Result file integration before progress page verification
- Single-handler changes before cross-handler validation

### Parallel Opportunities

- T001, T002, T003 (Phase 1) can all run in parallel — different files
- T004, T005 (Phase 2) can run in parallel — different handler files
- T006, T007 (Phase 2) can run in parallel — different aspects of ProgressPageWriter
- T010, T011, T012 (Phase 3) can run in parallel — different handler files
- T031, T032, T033, T034 (Phase 8) can all run in parallel — different SKILL files
- Phase 6 (US4) and Phase 8 (US7) can run in parallel — completely independent file sets

---

## Parallel Example: Phase 1 (Setup)

```bash
# Launch all setup tasks together (different files):
Task: "Create ProgressPhases.cs in src/Spectra.CLI/Progress/"
Task: "Create ProgressManager.cs in src/Spectra.CLI/Progress/"
Task: "Create UpdateResult.cs in src/Spectra.CLI/Results/"
```

## Parallel Example: Phase 3 (US1 Result Integration)

```bash
# Launch fast-command result integrations together (different handler files):
Task: "Add result file to ValidateHandler"
Task: "Add result file to AnalyzeHandler --import-criteria path"
Task: "Add result file to AnalyzeHandler --list-criteria path"
```

## Parallel Example: Phase 8 (US7 SKILL Updates)

```bash
# Launch all SKILL file updates together (different markdown files):
Task: "Update spectra-coverage.md"
Task: "Update spectra-criteria.md"
Task: "Update spectra-dashboard.md"
Task: "Update spectra-validate.md"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 3)

1. Complete Phase 1: Setup (create ProgressManager, ProgressPhases, UpdateResult)
2. Complete Phase 2: Foundational (refactor GenerateHandler + DocsIndexHandler)
3. Complete Phase 3: US1 — all 9 commands write result files
4. Complete Phase 5: US3 — validate refactors are regression-free
5. **STOP and VALIDATE**: Run full test suite, verify all result files written correctly
6. This delivers the core SKILL integration value

### Incremental Delivery

1. Setup + Foundational → ProgressManager ready
2. Add US1 (Results) → All SKILLs can read structured output (MVP!)
3. Add US2 (Progress Pages) → Live progress for long-running commands
4. Add US4 + US5 (Dashboard + Rename) → Fix regressions
5. Add US7 (SKILL Updates) → Consistent SKILL experience
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With two developers:

1. Both complete Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (Results) → US2 (Progress Pages)
   - Developer B: US4 (Dashboard) → US5 (Rename) → US7 (SKILLs)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Research found that spectra-docs SKILL (US6) already exists — skipped
- Research found dashboard JS already correct — US4 reduced to DataCollector hardening + field rename
- AnalyzeHandler has 3 sub-command paths (coverage, extract-criteria, import/list) requiring separate integrations
