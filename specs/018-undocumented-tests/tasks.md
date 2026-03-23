# Tasks: Undocumented Behavior Test Cases

**Input**: Design documents from `/specs/018-undocumented-tests/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included — the feature specification requires validation of all acceptance scenarios.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No project setup needed — this feature extends an existing codebase. Proceed directly to foundational changes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core schema extensions that ALL user stories depend on. These changes to `VerificationVerdict` and `GroundingFrontmatter` are required before any story-specific work can begin.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T001 [P] Add `Manual` value to `VerificationVerdict` enum in `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs`
- [x] T002 [P] Add `source` (string?, YAML alias `source`), `created_by` (string?, YAML alias `created_by`), and `note` (string?, YAML alias `note`) fields with YamlMember attributes to `GroundingFrontmatter` in `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`
- [x] T003 Update `TryParseVerdict()` in `GroundingFrontmatter` to accept `"manual"` string and map to `VerificationVerdict.Manual` in `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`
- [x] T004 Update `ToMetadata()` in `GroundingFrontmatter` to handle manual verdict — return `GroundingMetadata` with `Generator="user"`, `Critic="none"`, `Score=1.0`, `VerifiedAt=DateTimeOffset.UtcNow` when verdict is `"manual"` in `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`
- [x] T005 Add unit tests for `VerificationVerdict.Manual` parsing, `GroundingFrontmatter` new fields serialization/deserialization, and `ToMetadata()` manual verdict handling in `tests/Spectra.Core.Tests/`

**Checkpoint**: Foundation ready — `Manual` verdict exists in the type system and can be serialized/deserialized. All existing tests pass (zero regressions).

---

## Phase 3: User Story 1 — Describe Undocumented Behavior and Get a Test Case (Priority: P1) MVP

**Goal**: A tester can describe an undocumented behavior to the generation agent and get a properly structured test case with `grounding.verdict: "manual"`, `source: "user-described"`, empty `source_refs`, and correct `created_by`.

**Independent Test**: Describe a behavior to the generation agent → verify test case file is created with correct frontmatter and markdown structure → verify suite index is updated.

### Implementation for User Story 1

- [x] T006 [US1] Extend `GroundedPromptBuilder.BuildSystemPrompt()` to add "Creating Test Cases from Undocumented Behavior" section with step-by-step flow (understand behavior, generate test case, review, save) and output format specifying `source_refs: []`, `grounding.verdict: "manual"`, `grounding.source: "user-described"`, `grounding.created_by` in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
- [x] T007 [US1] Add "When NOT to use this flow" guidance to the system prompt — differentiate from doc-based generation (user provides doc/URL = normal flow, user describes behavior without docs = undocumented flow) in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
- [x] T008 [US1] Add clarifying questions guidance to the system prompt — ask only what's missing (screen/module, steps, expected result, preconditions, priority, suite); skip questions when description is detailed enough in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
- [x] T009 [US1] Add draft review and documentation reminder instructions to the system prompt — show complete draft for user confirmation before writing, display reminder after save ("This test has no documentation source. Consider updating docs.") in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
- [x] T010 [US1] Update `GenerateHandler.CreateTestWithGrounding()` to preserve existing `grounding` metadata on tests that already have `verdict: "manual"` set during generation (do not overwrite with critic results) in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`

**Checkpoint**: Generation agent can create undocumented test cases with correct metadata. Existing doc-based generation is unaffected.

---

## Phase 4: User Story 2 — Duplicate Detection Before Creating Undocumented Tests (Priority: P1)

**Goal**: The agent checks existing test cases for duplicates before creating a new undocumented test, showing similar matches and offering options when exact duplicates are found.

**Independent Test**: Describe a behavior that matches an existing test → verify agent shows the match and offers update/create/cancel options.

### Implementation for User Story 2

- [x] T011 [US2] Add duplicate detection instructions to the undocumented behavior section of the system prompt — instruct agent to call `find_test_cases` MCP tool with keywords from user description and component filter before generating in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`
- [x] T012 [US2] Add duplicate handling guidance — when similar test found: show it and proceed; when exact duplicate found: offer three options (update existing, create new, cancel); when no matches: proceed silently in `src/Spectra.CLI/Agent/GroundedPromptBuilder.cs`

**Checkpoint**: Agent performs duplicate check before creating undocumented tests. No new code files — this is prompt-level integration with existing `find_test_cases` MCP tool.

---

## Phase 5: User Story 3 — Critic Verification Bypass for Manual Tests (Priority: P1)

**Goal**: Tests with `grounding.verdict: "manual"` are automatically skipped during critic verification — no errors, no warnings.

**Independent Test**: Create a test with `grounding.verdict: "manual"` → run critic verification → verify test is skipped and included in write list.

### Implementation for User Story 3

- [x] T013 [US3] Update `VerifyTestsAsync()` in `GenerateHandler` to check each test's grounding verdict before calling `critic.VerifyTestAsync()` — if verdict is `Manual`, skip verification and add to results with a pass-through `VerificationResult` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T014 [US3] Ensure the verdict filter (`Verdict != Hallucinated`) in `GenerateHandler` correctly handles `Manual` verdict — manual tests must always pass through to the write list in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- [x] T015 [US3] Add unit tests for critic bypass: test with manual verdict is skipped in verification loop, test with manual verdict passes write filter, test with null verdict still goes through normal verification in `tests/Spectra.CLI.Tests/`

**Checkpoint**: Manual verdict tests flow through the generation pipeline without critic verification errors. All 984+ existing tests still pass.

---

## Phase 6: User Story 4 — Undocumented Tests in Coverage Analysis (Priority: P2)

**Goal**: Coverage analysis reports undocumented tests (empty `source_refs`) as a separate metric with count, percentage, and test IDs.

**Independent Test**: Run `spectra ai analyze --coverage` on a project with both documented and undocumented tests → verify separate undocumented metric in output.

### Implementation for User Story 4

- [x] T016 [P] [US4] Add `UndocumentedTestCount` (int) and `UndocumentedTestIds` (IReadOnlyList<string>) properties to `DocumentationCoverage` model in `src/Spectra.Core/Models/Coverage/DocumentationCoverage.cs`
- [x] T017 [US4] Update `DocumentationCoverageAnalyzer.Analyze()` to count tests where `SourceRefs` is empty or null — populate `UndocumentedTestCount` and `UndocumentedTestIds` on the returned `DocumentationCoverage` in `src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs`
- [x] T018 [US4] Update `UnifiedCoverageBuilder.Build()` to pass through undocumented test fields from `DocumentationCoverage` to `UnifiedCoverageReport` in `src/Spectra.Core/Coverage/UnifiedCoverageBuilder.cs`
- [x] T019 [US4] Update `CoverageReportWriter` to include undocumented test count and recommendation ("These may indicate documentation gaps") in both JSON and Markdown output formats in `src/Spectra.CLI/Coverage/CoverageReportWriter.cs`
- [x] T020 [US4] Add unit tests for `DocumentationCoverageAnalyzer` undocumented metric: project with mixed tests shows correct counts, project with no undocumented tests shows zero, empty source_refs correctly identified in `tests/Spectra.Core.Tests/Coverage/`
- [x] T021 [US4] Add unit tests for `CoverageReportWriter` undocumented section in JSON and Markdown outputs in `tests/Spectra.CLI.Tests/Coverage/`

**Checkpoint**: `spectra ai analyze --coverage` reports undocumented tests as a separate metric. Existing coverage calculations unchanged.

---

## Phase 7: User Story 5 — Dashboard Display of Undocumented Tests (Priority: P2)

**Goal**: Dashboard coverage visualization shows undocumented tests as an orange category with tooltip and filter toggle.

**Independent Test**: Generate dashboard for project with undocumented tests → verify orange category in coverage view with correct counts, tooltip, and working filter toggle.

### Implementation for User Story 5

- [x] T022 [P] [US5] Add `UndocumentedTestCount` (int) and `UndocumentedTestIds` (List<string>) properties with `[JsonPropertyName]` attributes to `DocumentationSectionData` in `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`
- [x] T023 [US5] Update `DataCollector.BuildCoverageSummaryAsync()` to populate undocumented test count and IDs in the documentation section — count tests where `SourceRefs` is empty in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T024 [P] [US5] Add `--cov-orange: #F97316` and `--cov-orange-bg: #fff7ed` CSS custom properties to the `:root` coverage variables section in `src/Spectra.CLI/Dashboard/Templates/styles/main.css`
- [x] T025 [US5] Update `renderThreeSectionCoverage()` or equivalent coverage rendering function in `app.js` to display undocumented tests as an orange segment in documentation coverage KPI card and donut chart in `src/Spectra.CLI/Dashboard/Templates/scripts/app.js`
- [x] T026 [US5] Add tooltip for orange segment: "Test created from user description — no documentation source" in the coverage visualization in `src/Spectra.CLI/Dashboard/Templates/scripts/app.js`
- [x] T027 [US5] Add filter toggle for undocumented tests in the coverage view — when toggled off, hide orange category and recalculate coverage percentages in `src/Spectra.CLI/Dashboard/Templates/scripts/app.js`
- [x] T028 [US5] Add unit tests for `DataCollector` undocumented metric population in `DocumentationSectionData` in `tests/Spectra.CLI.Tests/Dashboard/`

**Checkpoint**: Dashboard renders undocumented tests as a visually distinct orange category with functional filtering. Existing dashboard views unaffected.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final integration checks

- [x] T029 [P] Run full test suite (`dotnet test`) to verify zero regressions across all 984+ existing tests
- [x] T030 [P] Verify `GroundingMetadata.IsValid()` correctly handles `Manual` verdict (score=1.0, generator="user", critic="none" should pass validation) in `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs`
- [x] T031 Run `spectra validate` on a test file with manual verdict frontmatter to confirm schema validation passes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 2 (Foundational)**: No dependencies — start immediately
- **Phase 3 (US1 — Agent Prompt)**: Depends on Phase 2 (needs `Manual` verdict in type system)
- **Phase 4 (US2 — Duplicate Detection)**: Depends on Phase 3 (extends agent prompt from US1)
- **Phase 5 (US3 — Critic Bypass)**: Depends on Phase 2 only (needs `Manual` verdict enum)
- **Phase 6 (US4 — Coverage Analysis)**: Depends on Phase 2 only (needs model fields)
- **Phase 7 (US5 — Dashboard)**: Depends on Phase 6 (needs coverage model with undocumented fields)
- **Phase 8 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational — no other story dependencies
- **US2 (P1)**: Depends on US1 (extends the same agent prompt)
- **US3 (P1)**: Depends on Foundational only — can run in parallel with US1/US2
- **US4 (P2)**: Depends on Foundational only — can run in parallel with US1/US2/US3
- **US5 (P2)**: Depends on US4 (needs coverage model changes)

### Within Each User Story

- Models/schema before services
- Services before CLI output
- Core implementation before tests
- Story complete before checkpoint validation

### Parallel Opportunities

- **Phase 2**: T001 and T002 can run in parallel (different files)
- **Phase 5 + Phase 6**: US3 and US4 can run in parallel (independent file changes)
- **Phase 7**: T022 and T024 can run in parallel (C# model vs CSS file)
- **After Phase 2**: US1, US3, and US4 can all start in parallel

---

## Parallel Example: After Foundational Phase

```text
# These three can launch simultaneously after Phase 2:

# Stream A: Agent prompt (US1 → US2)
Task T006: Extend GroundedPromptBuilder with undocumented behavior section
Task T007: Add "when NOT to use" guidance
Task T008: Add clarifying questions guidance
Task T009: Add draft review instructions
Task T010: Update CreateTestWithGrounding for manual verdict

# Stream B: Critic bypass (US3)
Task T013: Update VerifyTestsAsync to skip manual verdict
Task T014: Ensure verdict filter handles Manual
Task T015: Add critic bypass tests

# Stream C: Coverage analysis (US4)
Task T016: Add UndocumentedTestCount to DocumentationCoverage model
Task T017: Update DocumentationCoverageAnalyzer.Analyze()
Task T018: Update UnifiedCoverageBuilder
Task T019: Update CoverageReportWriter
Task T020-T021: Coverage tests
```

---

## Implementation Strategy

### MVP First (User Story 1 + 3 Only)

1. Complete Phase 2: Foundational (T001-T005)
2. Complete Phase 3: US1 — Agent Prompt (T006-T010)
3. Complete Phase 5: US3 — Critic Bypass (T013-T015)
4. **STOP and VALIDATE**: Create an undocumented test case end-to-end, verify correct frontmatter and no critic errors
5. Deploy/demo if ready — core value proposition is complete

### Incremental Delivery

1. Foundational → Schema ready
2. US1 + US3 → Core flow works (MVP!)
3. US2 → Duplicate detection added
4. US4 → Coverage metrics available
5. US5 → Dashboard visualization complete
6. Polish → Full validation pass

### Parallel Team Strategy

With multiple developers after Foundational phase:
- Developer A: US1 → US2 (agent prompt, sequential)
- Developer B: US3 (critic bypass, independent)
- Developer C: US4 → US5 (coverage + dashboard, sequential)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- R3 from research.md confirms: empty `source_refs` already passes validation — no changes needed
- R7 from research.md confirms: duplicate detection uses existing `find_test_cases` MCP tool — no new code, prompt-level only
