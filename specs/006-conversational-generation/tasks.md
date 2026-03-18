# Tasks: Conversational Test Generation

**Input**: Design documents from `/specs/006-conversational-generation/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in spec. Tests are optional per project convention.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Based on plan.md structure:
- CLI source: `src/Spectra.CLI/`
- Core models: `src/Spectra.Core/Models/`
- Tests: `tests/Spectra.CLI.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure and foundational models

- [x] T001 [P] Create TestClassification enum in src/Spectra.Core/Models/TestClassification.cs (EXISTING: UpdateClassification)
- [x] T002 [P] Create SessionMode enum in src/Spectra.Core/Models/SessionMode.cs
- [x] T003 [P] Create SessionState enum in src/Spectra.Core/Models/SessionState.cs
- [x] T004 [P] Create TestTypeSelection enum in src/Spectra.Core/Models/TestTypeSelection.cs
- [x] T005 [P] Create GapSeverity enum in src/Spectra.Core/Models/GapSeverity.cs (EXISTING: CoverageReport.cs)
- [x] T006 Create CoverageGap model in src/Spectra.Core/Models/CoverageGap.cs (EXISTING: CoverageReport.cs)
- [x] T007 Create ClassifiedTest model in src/Spectra.Core/Models/ClassifiedTest.cs
- [x] T008 Create SuiteSummary model in src/Spectra.Core/Models/SuiteSummary.cs
- [x] T009 Create UpdateResult model in src/Spectra.Core/Models/UpdateResult.cs
- [x] T010 Extend GenerationResult with CoverageGapsRemaining property in src/Spectra.CLI/Agent/IAgentRuntime.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared output and coverage infrastructure used by ALL user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T011 Create OutputSymbols static class with Unicode symbols (◆◐✓✗⚠ℹ) in src/Spectra.CLI/Output/OutputSymbols.cs
- [x] T012 Create ProgressReporter with Spectre.Console spinners and status in src/Spectra.CLI/Output/ProgressReporter.cs
- [x] T013 Create ResultPresenter with table formatting for test listings in src/Spectra.CLI/Output/ResultPresenter.cs
- [x] T014 Create GapAnalyzer to compare docs against source_refs in src/Spectra.CLI/Coverage/GapAnalyzer.cs
- [x] T015 Create GapPresenter to display coverage gaps with symbols in src/Spectra.CLI/Coverage/GapPresenter.cs
- [x] T016 Create SuiteScanner to enumerate suites with test counts in src/Spectra.CLI/Interactive/SuiteScanner.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Direct Mode Test Generation (Priority: P1) 🎯 MVP

**Goal**: Users can run `spectra ai generate --suite X --focus Y` for autonomous test generation

**Independent Test**: Run `spectra ai generate --suite checkout --focus "negative scenarios"` and verify tests written to disk without prompts

### Implementation for User Story 1

- [x] T017 [US1] Modify GenerateCommand to make suite argument optional with Arity.ZeroOrOne in src/Spectra.CLI/Commands/Ai/Generate/GenerateCommand.cs
- [x] T018 [US1] Add isDirectMode detection logic (suite provided = direct mode) in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T019 [US1] Implement direct mode flow: load suite → check duplicates → generate → write in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T020 [US1] Add progress output with ProgressReporter (◐ Loading, ◐ Generating) in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T021 [US1] Display generated tests in table format using ResultPresenter in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T022 [US1] Call GapAnalyzer to identify remaining gaps after generation in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T023 [US1] Display remaining gaps with GapPresenter (ℹ Gaps still uncovered) in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T024 [US1] Remove review/accept step - write tests immediately to disk in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs

**Checkpoint**: Direct mode generation fully functional and testable independently

---

## Phase 4: User Story 2 - Interactive Mode Test Generation (Priority: P1)

**Goal**: Users can run `spectra ai generate` (no args) for guided test generation

**Independent Test**: Run `spectra ai generate` and follow prompts through suite selection → type selection → generation

### Implementation for User Story 2

- [x] T025 [P] [US2] Create SuiteSelector with Spectre.Console SelectionPrompt in src/Spectra.CLI/Interactive/SuiteSelector.cs
- [x] T026 [P] [US2] Create TestTypeSelector (Full/Negative/Specific/Free) in src/Spectra.CLI/Interactive/TestTypeSelector.cs
- [x] T027 [P] [US2] Create FocusDescriptor with TextPrompt for focus input in src/Spectra.CLI/Interactive/FocusDescriptor.cs
- [x] T028 [P] [US2] Create GapSelector for selecting remaining gaps in src/Spectra.CLI/Interactive/GapSelector.cs
- [x] T029 [US2] Create InteractiveSession state machine per data-model.md in src/Spectra.CLI/Interactive/InteractiveSession.cs
- [x] T030 [US2] Add isInteractiveMode detection (no suite + not CI) in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T031 [US2] Implement interactive flow: suite selection → type → focus → gap analysis → generate in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T032 [US2] Display existing tests matching focus area before generating in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs
- [x] T033 [US2] Add gap selection loop (generate more or finish) in src/Spectra.CLI/Commands/Ai/Generate/GenerateHandler.cs

**Checkpoint**: Interactive mode generation fully functional and testable independently

---

## Phase 5: User Story 3 - Direct Mode Test Update (Priority: P1)

**Goal**: Users can run `spectra ai update --suite X` for autonomous test sync

**Independent Test**: Run `spectra ai update --suite checkout` after doc changes and verify tests updated/marked

### Implementation for User Story 3

- [x] T034 [P] [US3] Create TestClassifier with classification logic in src/Spectra.Core/Update/TestClassifier.cs (already existed)
- [x] T035 [P] [US3] Create ClassificationPresenter for update results display in src/Spectra.CLI/Classification/ClassificationPresenter.cs
- [x] T036 [US3] Modify UpdateCommand to make suite argument optional in src/Spectra.CLI/Commands/Update/UpdateCommand.cs
- [x] T037 [US3] Add isDirectMode detection in UpdateHandler in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T038 [US3] Implement test classification: UP_TO_DATE, OUTDATED, ORPHANED, REDUNDANT in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T039 [US3] Update outdated tests in place with new AI-generated content in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T040 [US3] Mark orphaned tests with status: orphaned, orphaned_reason, orphaned_date in frontmatter in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T041 [US3] Flag redundant tests in _index.json with redundant_of and redundant_reason in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T042 [US3] Display update summary with ClassificationPresenter in src/Spectra.CLI/Commands/Update/UpdateHandler.cs

**Checkpoint**: Direct mode update fully functional and testable independently

---

## Phase 6: User Story 4 - Interactive Mode Test Update (Priority: P2)

**Goal**: Users can run `spectra ai update` (no args) for guided test maintenance

**Independent Test**: Run `spectra ai update` and follow prompts through suite selection to completion

### Implementation for User Story 4

- [x] T043 [US4] Add suite selection for update (with last-updated dates) in src/Spectra.CLI/Interactive/SuiteSelector.cs
- [x] T044 [US4] Add isInteractiveMode detection in UpdateHandler in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T045 [US4] Implement interactive update flow: suite select → classify → update → summary in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T046 [US4] Display orphaned tests with reasons and git diff suggestion in src/Spectra.CLI/Classification/ClassificationPresenter.cs
- [x] T047 [US4] Display redundant tests with similarity info in src/Spectra.CLI/Classification/ClassificationPresenter.cs

**Checkpoint**: Interactive mode update fully functional and testable independently

---

## Phase 7: User Story 5 - Suite Creation in Interactive Mode (Priority: P2)

**Goal**: Users can create new suites during interactive generation flow

**Independent Test**: Run `spectra ai generate`, select "Create new suite", provide name, verify directory created

### Implementation for User Story 5

- [x] T048 [US5] Add "Create new suite" option to SuiteSelector in src/Spectra.CLI/Interactive/SuiteSelector.cs
- [x] T049 [US5] Create SuiteCreator to prompt for name and create directory in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs (inline)
- [x] T050 [US5] Integrate suite creation into InteractiveSession flow in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T051 [US5] Validate suite name (no special chars, not duplicate) in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs (sanitizes name)

**Checkpoint**: Suite creation fully functional and testable independently

---

## Phase 8: User Story 6 - CI Pipeline Integration (Priority: P2)

**Goal**: Commands work in CI with --no-interaction, proper exit codes, no stdin reads

**Independent Test**: Run with --no-interaction in non-TTY, verify no prompts and correct exit codes

### Implementation for User Story 6

- [x] T052 [US6] Add --no-interaction option in src/Spectra.CLI/Commands/Generate/GenerateCommand.cs and UpdateCommand.cs
- [x] T053 [US6] Implement non-TTY auto-detection (Console.IsInputRedirected) in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T054 [US6] Add non-TTY auto-detection to UpdateHandler in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T055 [US6] Validate --suite required when --no-interaction in GenerateHandler in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T056 [US6] Validate --suite required when --no-interaction in UpdateHandler in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T057 [US6] Return exit code 1 for errors with message to stderr in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T058 [US6] Return exit code 1 for errors in UpdateHandler in src/Spectra.CLI/Commands/Update/UpdateHandler.cs

**Checkpoint**: CI integration fully functional and testable independently

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, error handling, and refinements across all stories

- [x] T059 [P] Handle empty documentation folders with helpful message in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T060 [P] Handle AI generation failure mid-way (preserve partial work) in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T061 [P] Handle "all gaps covered" scenario with suggestion in src/Spectra.CLI/Coverage/GapPresenter.cs
- [x] T062 [P] Handle network errors with retry suggestion in src/Spectra.CLI/Agent/*.cs (all 3 agents updated)
- [x] T063 Ensure profile auto-loading per FR-020 in GenerateHandler in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T064 Profile auto-loading not needed in UpdateHandler (profiles only affect generation, not update)
- [x] T065 Run quickstart.md validation scenarios (implementation verified against quickstart examples)
- [x] T066 Verify all output matches contracts/cli-commands.md format (symbols, error messages, orphaned status, redundant flagging)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-8)**: All depend on Foundational phase completion
  - US1-US3 are P1 priority (implement first)
  - US4-US6 are P2 priority (implement after P1 stories)
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational - Shares interactive components
- **User Story 3 (P1)**: Can start after Foundational - Introduces classification
- **User Story 4 (P2)**: Depends on US3 classification infrastructure
- **User Story 5 (P2)**: Depends on US2 interactive infrastructure
- **User Story 6 (P2)**: Can start after Foundational - CI concerns cut across all

### Within Each User Story

- Models/enums before services
- Infrastructure (selectors, reporters) before handlers
- Core flow before edge cases
- Integration points after core implementation

### Parallel Opportunities

- All Phase 1 enum tasks (T001-T005) can run in parallel
- Phase 2 output tasks (T011-T013) can run in parallel with coverage tasks (T014-T016)
- US2 interactive component tasks (T025-T028) can run in parallel
- US3 classification tasks (T034-T035) can run in parallel
- All US6 validation tasks can run in parallel after handler setup
- All Phase 9 edge case tasks can run in parallel

---

## Parallel Example: Phase 1 Setup

```bash
# Launch all enum creation tasks together:
Task: "Create TestClassification enum in src/Spectra.Core/Models/TestClassification.cs"
Task: "Create SessionMode enum in src/Spectra.Core/Models/SessionMode.cs"
Task: "Create SessionState enum in src/Spectra.Core/Models/SessionState.cs"
Task: "Create TestTypeSelection enum in src/Spectra.Core/Models/TestTypeSelection.cs"
Task: "Create GapSeverity enum in src/Spectra.Core/Models/GapSeverity.cs"
```

## Parallel Example: User Story 2 Interactive Components

```bash
# Launch all interactive selectors together:
Task: "Create SuiteSelector with Spectre.Console SelectionPrompt in src/Spectra.CLI/Interactive/SuiteSelector.cs"
Task: "Create TestTypeSelector (Full/Negative/Specific/Free) in src/Spectra.CLI/Interactive/TestTypeSelector.cs"
Task: "Create FocusDescriptor with TextPrompt for focus input in src/Spectra.CLI/Interactive/FocusDescriptor.cs"
Task: "Create GapSelector for selecting remaining gaps in src/Spectra.CLI/Interactive/GapSelector.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (enums and models)
2. Complete Phase 2: Foundational (output and gap infrastructure)
3. Complete Phase 3: User Story 1 (direct mode generate)
4. **STOP and VALIDATE**: Test `spectra ai generate --suite X --focus Y` independently
5. Deploy/demo if ready - users can generate tests without prompts

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test direct generate → Deploy (MVP!)
3. Add User Story 2 → Test interactive generate → Deploy
4. Add User Story 3 → Test direct update → Deploy
5. Add User Stories 4-6 → Test remaining features → Deploy
6. Add Polish → Full feature complete

### Parallel Team Strategy

With multiple developers after Foundational completes:
- Developer A: User Story 1 (direct generate)
- Developer B: User Story 2 (interactive generate)
- Developer C: User Story 3 (direct update)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Key principle: No review step - tests written directly, git is the review tool
- Reuse existing Spectre.Console patterns from ReviewPresenter.cs
