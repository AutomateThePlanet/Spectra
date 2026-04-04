# Tasks: Generation Session Flow

**Input**: Design documents from `/specs/021-generation-session/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks grouped by user story for independent implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New models, session store, and duplicate detection shared by all stories

- [x] T001 Create GenerationSessionState, AnalysisSnapshot, Suggestion, and SuggestionStatus models in src/Spectra.CLI/Session/GenerationSession.cs
- [x] T002 Create SessionStore (read/write/expire .spectra/session.json) in src/Spectra.CLI/Session/SessionStore.cs
- [x] T003 Create DuplicateDetector with normalized Levenshtein similarity in src/Spectra.CLI/Validation/DuplicateDetector.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add new CLI flags and extend GenerateResult for session data

- [x] T004 Add --from-suggestions, --from-description, --context, --auto-complete options to GenerateCommand in src/Spectra.CLI/Commands/Generate/GenerateCommand.cs
- [x] T005 Extend GenerateResult with suggestions, duplicate_warnings, and session summary fields in src/Spectra.CLI/Results/GenerateResult.cs

**Checkpoint**: New flags registered, result models extended

---

## Phase 3: User Story 1 - Iterative Test Generation Session (Priority: P1) MVP

**Goal**: Multi-phase session flow (analyze → generate → suggest → loop → exit with summary)

**Independent Test**: Run generate in interactive mode, complete full cycle, verify session summary

### Implementation for US1

- [x] T006 [US1] Create SuggestionPresenter for displaying suggestions menu (generate all, pick specific, describe own, done) in src/Spectra.CLI/Output/SuggestionPresenter.cs
- [x] T007 [US1] Create SessionSummary presenter for exit summary (counts by source: docs, suggestions, descriptions) in src/Spectra.CLI/Session/SessionSummary.cs
- [x] T008 [US1] Extract suggestion derivation logic: after generation, diff BehaviorAnalysisResult against generated tests to produce Suggestion list in src/Spectra.CLI/Session/SuggestionBuilder.cs
- [x] T009 [US1] Refactor GenerateHandler.ExecuteDirectModeAsync to use session flow: after Phase 2 generation, save session state, show suggestions, loop Phases 3-4 until user exits in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T010 [US1] Update GenerateHandler.ExecuteInteractiveModeAsync to use session flow with suggestion loop and session summary in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T011 [US1] Wire SessionStore into GenerateHandler: create session on start, update after each phase, save suggestions for later --from-suggestions use in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs

**Checkpoint**: Interactive session works end-to-end with suggestions loop and summary

---

## Phase 4: User Story 2 - User-Described Test Cases (Priority: P1)

**Goal**: Create test cases from tester's plain-language descriptions with manual grounding verdict

**Independent Test**: Choose "describe your own" in session, enter description, verify test has verdict:manual

### Implementation for US2

- [x] T012 [US2] Create UserDescribedGenerator: takes description + context, uses Copilot SDK to generate structured TestCase with grounding.verdict=manual in src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs
- [x] T013 [US2] Create UserDescriptionPrompt for interactive input (description + optional context) in src/Spectra.CLI/Interactive/UserDescriptionPrompt.cs
- [x] T014 [US2] Integrate user-described flow into suggestion loop: option (c) in SuggestionPresenter triggers UserDescribedGenerator, shows draft, allows save/edit/cancel in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T015 [US2] Update SessionStore to track user_described test IDs in session state in src/Spectra.CLI/Session/SessionStore.cs

**Checkpoint**: User can describe a test, get a structured result with manual grounding

---

## Phase 5: User Story 3 - Non-Interactive Session for CI/SKILL (Priority: P2)

**Goal**: --from-suggestions, --from-description, --context, --auto-complete work without prompts

**Independent Test**: Run --auto-complete with --output-format json, verify all phases execute and valid JSON output

### Implementation for US3

- [x] T016 [US3] Implement --from-suggestions handler: load session, generate tests from pending suggestions (all or by index), output JSON in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T017 [US3] Implement --from-description handler: create test from description+context, skip critic, output JSON in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T018 [US3] Implement --auto-complete handler: run analyze → generate all → generate all suggestions → save session → output JSON summary in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T019 [US3] Handle error cases: --from-suggestions with no session (exit 1), expired session (exit 1, clear message) in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs

**Checkpoint**: All non-interactive flags work with --output-format json

---

## Phase 6: User Story 4 - Duplicate Detection (Priority: P2)

**Goal**: Fuzzy title matching warns before creating duplicate tests

**Independent Test**: Create test with similar title to existing one, verify duplicate warning

### Implementation for US4

- [x] T020 [US4] Integrate DuplicateDetector into GenerateHandler: check each generated test against existing tests before writing in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T021 [US4] Interactive mode: show duplicate warning with similar test ID, prompt create/skip/update in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T022 [US4] Non-interactive mode: add duplicate_warnings to GenerateResult JSON output in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs

**Checkpoint**: Duplicates detected and reported in both modes

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T023 [P] Add unit tests for DuplicateDetector (Levenshtein similarity, threshold, edge cases) in tests/Spectra.CLI.Tests/Validation/DuplicateDetectorTests.cs
- [x] T024 [P] Add unit tests for SessionStore (create, read, expire, overwrite) in tests/Spectra.CLI.Tests/Session/SessionStoreTests.cs
- [x] T025 [P] Add unit tests for SuggestionBuilder (derive suggestions from analysis minus generated) in tests/Spectra.CLI.Tests/Session/SuggestionBuilderTests.cs
- [x] T026 Verify all existing tests still pass (dotnet test)
- [x] T027 Run quickstart.md validation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup
- **US1 (Phase 3)**: Depends on Foundational — core session flow
- **US2 (Phase 4)**: Depends on US1 — integrates into suggestion loop
- **US3 (Phase 5)**: Depends on US1 + US2 — non-interactive wrappers
- **US4 (Phase 6)**: Depends on US1 — can run after core session works
- **Polish (Phase 7)**: Depends on all stories

### Parallel Opportunities

- T001-T003 (Setup): All parallel (different files)
- T006-T008 (US1 models): All parallel (different files)
- T023-T025 (Tests): All parallel (different test files)

---

## Implementation Strategy

### MVP First (US1 + US2)

1. Complete Setup (T001-T003)
2. Complete Foundational (T004-T005)
3. Complete US1 (T006-T011) — session flow works
4. Complete US2 (T012-T015) — user-described tests work
5. **VALIDATE**: Full interactive session end-to-end
6. Continue to US3, US4, Polish

---

## Notes

- GenerateHandler.cs is modified by multiple stories — tasks must be sequential within that file
- Session state file (.spectra/session.json) should be added to .gitignore
- Existing BehaviorAnalyzer and gap analysis infrastructure is reused, not duplicated
