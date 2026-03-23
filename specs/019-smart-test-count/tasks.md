# Tasks: Smart Test Count Recommendation

**Input**: Design documents from `/specs/019-smart-test-count/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are included — constitution requires tests for all public APIs and critical paths.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new files and shared models needed by all user stories

- [x] T001 [P] Create BehaviorCategory enum with HappyPath, Negative, EdgeCase, Security, Performance values and snake_case JSON serialization in src/Spectra.Core/Models/BehaviorCategory.cs
- [x] T002 [P] Create IdentifiedBehavior record with Category (BehaviorCategory), Title (string), and Source (string) properties in src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs
- [x] T003 [P] Create BehaviorAnalysisResult record with TotalBehaviors, Breakdown (Dictionary<BehaviorCategory, int>), Behaviors (IReadOnlyList<IdentifiedBehavior>), AlreadyCovered, RecommendedCount, DocumentsAnalyzed, and TotalWords properties in src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core services that MUST be complete before any user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create BehaviorAnalyzer service in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs with AnalyzeAsync(IReadOnlyList<SourceDocument> documents, IReadOnlyList<TestCase> existingTests, string? focusArea, EffectiveProfile? profile, CancellationToken ct) method. Implementation: (1) Build document summaries from SourceDocument list (title + section headings + first 200 chars per section), (2) construct structured analysis prompt requesting JSON response with categorized testable behaviors, (3) send to Copilot SDK using same provider as generation via CopilotService, (4) parse JSON response into BehaviorAnalysisResult, (5) run dedup against existingTests using DuplicateDetector (Jaccard similarity, 0.6 threshold) to compute AlreadyCovered count, (6) return result with RecommendedCount = TotalBehaviors - AlreadyCovered. Handle parse failures by returning null (fallback handled by caller).
- [x] T005 Create AnalysisPresenter in src/Spectra.CLI/Output/AnalysisPresenter.cs with two methods: (1) DisplayBreakdown(BehaviorAnalysisResult result) — uses Spectre.Console to render document count, word total, categorized behavior counts with bullet points, existing coverage deduction, and recommended count; (2) DisplayGapNotification(BehaviorAnalysisResult analysis, int generatedCount, string suiteName) — shows remaining uncovered behaviors by category and next-step command. Both respect --quiet flag and piped output (check AnsiConsole.Profile.Out.IsTerminal).
- [x] T006 Write unit tests for BehaviorAnalyzer JSON parsing in tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs: test valid JSON response parsing, test malformed JSON returns null, test empty behaviors array, test dedup correctly reduces RecommendedCount, test category mapping from snake_case strings to BehaviorCategory enum, test document summary construction (title + sections + 200 char truncation)
- [x] T007 Write unit tests for AnalysisPresenter in tests/Spectra.CLI.Tests/Output/AnalysisPresenterTests.cs: test DisplayBreakdown renders all categories with correct counts, test DisplayBreakdown omits zero-count categories, test DisplayGapNotification shows remaining by category, test DisplayGapNotification is suppressed when generatedCount equals total, test next-step hint includes correct suite name

**Checkpoint**: Foundation ready — BehaviorAnalyzer can analyze docs and AnalysisPresenter can display results

---

## Phase 3: User Story 1 — Automatic Test Count Recommendation (Priority: P1) MVP

**Goal**: When `--count` is omitted, analyze docs and auto-generate all identified behaviors (non-interactive) or use default count (direct mode).

**Independent Test**: Run `spectra ai generate --suite <suite>` without `--count` and verify analysis output appears before generation.

### Implementation for User Story 1

- [x] T008 [US1] Modify GenerateHandler.ExecuteDirectModeAsync in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs: when count parameter is null, call BehaviorAnalyzer.AnalyzeAsync() after document loading. If analysis succeeds, call AnalysisPresenter.DisplayBreakdown(), set count to analysis.RecommendedCount. If analysis returns null (failure), log warning and fall back to existing default (20). When --no-interaction or no TTY, auto-use RecommendedCount without prompting. Pass resolved count to existing agent.GenerateTestsAsync(). After generation, call AnalysisPresenter.DisplayGapNotification() if analysis was performed and generatedCount < analysis.TotalBehaviors.
- [x] T009 [US1] Wire BehaviorAnalyzer into GenerateHandler constructor via dependency injection — add BehaviorAnalyzer as a constructor parameter, instantiate it with CopilotService and DuplicateDetector instances in the command setup (GenerateCommand.cs). No changes to IAgentRuntime interface.
- [x] T010 [US1] Write integration tests in tests/Spectra.CLI.Tests/Commands/Generate/SmartCountTests.cs: test that when count is null and analysis succeeds, RecommendedCount is used; test that when count is null and analysis fails, default 20 is used; test that when --count is explicitly provided, BehaviorAnalyzer is not called; test that --no-interaction mode auto-generates without prompting; test that post-generation gap notification is displayed when fewer tests generated than total behaviors; test that gap notification is suppressed when all behaviors are generated

**Checkpoint**: User Story 1 fully functional — `spectra ai generate --suite X` without `--count` performs analysis and auto-generates

---

## Phase 4: User Story 2 — Interactive Count Selection (Priority: P2)

**Goal**: In interactive mode, present a selection menu after analysis so the user can choose count by category or custom number.

**Independent Test**: Run `spectra ai generate` in interactive mode, select a suite, and verify the count selection menu appears with options matching the analysis breakdown.

### Implementation for User Story 2

- [x] T011 [P] [US2] Create CountSelector in src/Spectra.CLI/Interactive/CountSelector.cs with SelectAsync(BehaviorAnalysisResult analysis, CancellationToken ct) method returning CountSelection record (Count int, SelectedCategories IReadOnlyList<BehaviorCategory>?, FreeTextDescription string?). Build dynamic Spectre.Console SelectionPrompt with options: "All N — full coverage", per-category options (e.g., "8 — happy paths only"), cumulative category options (e.g., "14 — happy paths + negative"), "Custom number", "Let me describe what I want". For custom number, use TextPrompt<int> with validation (1 to TotalBehaviors). For free-text, use TextPrompt<string>.
- [x] T012 [US2] Modify GenerateHandler.ExecuteInteractiveModeAsync in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs: after suite selection and document loading, when count is null, call BehaviorAnalyzer.AnalyzeAsync(), call AnalysisPresenter.DisplayBreakdown(), then call CountSelector.SelectAsync() to get user's choice. Use returned CountSelection.Count as the generation count. If SelectedCategories is set, pass category names as focus to the generation prompt. If FreeTextDescription is set, append to focus. After generation, call AnalysisPresenter.DisplayGapNotification().
- [x] T013 [P] [US2] Write unit tests for CountSelector in tests/Spectra.CLI.Tests/Interactive/CountSelectorTests.cs: test menu options are generated correctly from analysis breakdown (correct labels and counts), test cumulative category options are ordered by breakdown size, test zero-count categories are excluded from options, test CountSelection record correctly populates Count and SelectedCategories fields

**Checkpoint**: Interactive mode users can select count from analysis-driven menu

---

## Phase 5: User Story 3 — Post-Generation Gap Notification (Priority: P2)

**Goal**: After partial generation, display remaining uncovered behaviors by category with actionable next-step command.

**Independent Test**: Generate fewer tests than identified behaviors and verify gap notification with correct per-category counts.

### Implementation for User Story 3

- [x] T014 [US3] Enhance AnalysisPresenter.DisplayGapNotification in src/Spectra.CLI/Output/AnalysisPresenter.cs to compute remaining behaviors per category: subtract generated count proportionally or by selected categories, display each remaining category with count, include `spectra ai generate --suite {suite}` next-step hint. Integrate with existing NextStepHints pattern (dimmed text, suppressed by --quiet).
- [x] T015 [US3] Write tests for gap notification accuracy in tests/Spectra.CLI.Tests/Output/AnalysisPresenterTests.cs: test remaining category counts are correct when user generated happy-paths-only (other categories fully remaining), test remaining is zero when all generated, test next-step command includes correct suite name, test output is suppressed when --quiet is active

**Checkpoint**: Gap notifications work correctly after any partial generation

---

## Phase 6: User Story 4 — Focus Flag Integration (Priority: P3)

**Goal**: When `--focus` is specified without `--count`, analysis runs but generation scopes to matching categories.

**Independent Test**: Run `spectra ai generate --suite X --focus "negative scenarios"` and verify only negative behaviors are generated.

### Implementation for User Story 4

- [x] T016 [US4] Modify BehaviorAnalyzer.AnalyzeAsync in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs to accept focusArea parameter. When focusArea is set, filter analysis result behaviors to matching categories (fuzzy match category display names against focus string, e.g., "negative" matches Negative, "edge" matches EdgeCase). Recompute Breakdown and TotalBehaviors from filtered list. Display "Focus: {focusArea} ({n} identified)" in AnalysisPresenter.
- [x] T017 [US4] Write tests for focus filtering in tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs: test focus "negative" filters to only Negative category, test focus "edge cases" filters to EdgeCase, test focus with no matching category returns empty result, test focus is case-insensitive, test partial match works (e.g., "sec" matches Security)

**Checkpoint**: Focus flag correctly scopes analysis to matching categories

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Integration validation and cleanup

- [x] T018 [P] Add BehaviorAnalyzer edge case handling in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs: handle empty document list (return null with warning), handle AI timeout (5-second timeout for analysis call, return null on timeout), handle all-covered scenario (RecommendedCount = 0, display "All N behaviors already covered" message and suggest `spectra ai update`)
- [x] T019 [P] Ensure profile integration in src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs: when EffectiveProfile has exclusions (e.g., "Security", "Performance"), filter those categories from the analysis prompt or result so excluded categories don't inflate the count
- [x] T020 Run full test suite (`dotnet test`) and verify all existing 1148+ tests still pass plus new tests
- [x] T021 Run quickstart.md validation — verify all examples from specs/019-smart-test-count/quickstart.md work end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (models must exist for BehaviorAnalyzer)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (needs BehaviorAnalyzer and AnalysisPresenter)
- **User Story 2 (Phase 4)**: Depends on Phase 2 (needs BehaviorAnalyzer and AnalysisPresenter). Independent of US1.
- **User Story 3 (Phase 5)**: Depends on Phase 2 (enhances AnalysisPresenter). Can run parallel with US1/US2.
- **User Story 4 (Phase 6)**: Depends on Phase 2 (modifies BehaviorAnalyzer). Can run parallel with US1/US2/US3.
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependencies on other stories
- **US2 (P2)**: After Foundational — no dependencies on other stories (independent interactive path)
- **US3 (P2)**: After Foundational — no dependencies on other stories (enhances display)
- **US4 (P3)**: After Foundational — no dependencies on other stories (adds filter to analyzer)

### Within Each User Story

- Models before services (Phase 1 before Phase 2)
- Services before handler integration
- Tests alongside implementation

### Parallel Opportunities

- T001, T002, T003 can all run in parallel (independent new files)
- T006, T007 can run in parallel with T004, T005 (test files vs implementation files)
- T011, T013 can run in parallel (CountSelector + its tests are independent files)
- US1, US2, US3, US4 can proceed in parallel after Phase 2 (different handler methods and files)
- T018, T019 can run in parallel (different concerns in same file but non-overlapping methods)

---

## Parallel Example: Phase 1

```bash
# Launch all model creation tasks together:
Task: "Create BehaviorCategory enum in src/Spectra.Core/Models/BehaviorCategory.cs"
Task: "Create IdentifiedBehavior record in src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs"
Task: "Create BehaviorAnalysisResult record in src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs"
```

## Parallel Example: User Story 2

```bash
# Launch CountSelector and its tests together:
Task: "Create CountSelector in src/Spectra.CLI/Interactive/CountSelector.cs"
Task: "Write CountSelector tests in tests/Spectra.CLI.Tests/Interactive/CountSelectorTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (3 tasks)
2. Complete Phase 2: Foundational (4 tasks)
3. Complete Phase 3: User Story 1 (3 tasks)
4. **STOP and VALIDATE**: Run `spectra ai generate --suite X` without --count — verify analysis + auto-generation works
5. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → MVP!
3. Add User Story 2 → Interactive count selection works
4. Add User Story 3 → Gap notifications work
5. Add User Story 4 → Focus flag integration works
6. Polish → Full integration validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- BehaviorAnalyzer is the single most critical component — get it right in Phase 2
