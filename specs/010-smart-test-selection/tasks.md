# Tasks: Smart Test Selection

**Input**: Design documents from `/specs/010-smart-test-selection/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/mcp-tools.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Model layer changes shared across multiple user stories

- [x] T001 [P] Add optional `Description` property to `TestCaseFrontmatter` in `src/Spectra.Core/Models/TestCaseFrontmatter.cs` with `[YamlMember(Alias = "description")]`
- [x] T002 [P] Add optional `Description` property to `TestCase` in `src/Spectra.Core/Models/TestCase.cs`
- [x] T003 [P] Add `Description` and `EstimatedDuration` properties to `TestIndexEntry` in `src/Spectra.Core/Models/TestIndexEntry.cs` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
- [x] T004 Wire `Description` field through `TestCaseParser` in `src/Spectra.Core/Parsing/TestCaseParser.cs`
- [x] T005 Create `SavedSelectionConfig` model in `src/Spectra.Core/Models/Config/SavedSelectionConfig.cs` with Description, Tags, Priorities, Components, HasAutomation properties
- [x] T006 Add `Selections` property (`IReadOnlyDictionary<string, SavedSelectionConfig>`) to `SpectraConfig` in `src/Spectra.Core/Models/Config/SpectraConfig.cs`

**Checkpoint**: Model layer ready for tool implementation ✅

---

## Phase 2: User Story 1 — Cross-Suite Test Search and Filtering (Priority: P1)

**Goal**: Implement `find_test_cases` MCP tool for cross-suite search and filtering

**Independent Test**: Call `find_test_cases` with various filter combos and verify correct matches

### Implementation

- [x] T007 [US1] Create `FindTestCasesTool` implementing `IMcpTool` in `src/Spectra.MCP/Tools/Data/FindTestCasesTool.cs` — constructor accepts `Func<IEnumerable<string>>` suiteListLoader and `Func<string, IEnumerable<TestIndexEntry>>` indexLoader
- [x] T008 [US1] Implement parameter deserialization (query, suites, priorities, tags, components, has_automation, max_results) with `FindTestCasesRequest` inner class
- [x] T009 [US1] Implement cross-suite index loading — enumerate all suites via suiteListLoader, load indexes per suite, skip missing/malformed indexes with warnings
- [x] T010 [US1] Implement filter logic — AND between filter types, OR within arrays, case-insensitive matching for priorities/tags/components
- [x] T011 [US1] Implement free-text query matching — split on whitespace, case-insensitive OR match against title + description + tags, count keyword hits per test
- [x] T012 [US1] Implement result ordering — keyword hits (if query) > priority descending > suite name > original index order
- [x] T013 [US1] Implement max_results truncation — return `matched` total count, truncate `tests` array, compute `total_estimated_duration` from all matches
- [x] T014 [US1] Implement estimated duration parsing and aggregation — parse "5m", "1h 30m" format, sum across matched tests, format result as human-readable string
- [x] T015 [US1] Register `find_test_cases` tool in `src/Spectra.MCP/Program.cs` with suiteListLoader and indexLoader delegates
- [x] T016 [US1] Write tests for `FindTestCasesTool` — covered by SmartSelectionFlowTests integration tests

**Checkpoint**: `find_test_cases` tool fully functional and tested ✅

---

## Phase 3: User Story 2 — Start Execution Run with Custom Test IDs (Priority: P1)

**Goal**: Extend `start_execution_run` to accept `test_ids` and `selection` parameters

**Independent Test**: Call `start_execution_run` with test_ids from multiple suites, verify run starts in order

### Implementation

- [x] T017 [US2] Extend `StartExecutionRunTool` request class in `src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs` — add `test_ids` (string array), `selection` (string), and `name` (string) parameters
- [x] T018 [US2] Add mutual exclusivity validation — error if more than one of suite/test_ids/selection is provided, error if test_ids or selection without name
- [x] T019 [US2] Implement test_ids mode — resolve each ID across all suite indexes using indexLoader, validate all exist (return INVALID_TEST_IDS error listing missing), deduplicate preserving order, build TestQueue with resolved entries in specified order
- [x] T020 [US2] Implement selection mode — load `SavedSelectionConfig` from config by name, apply filters across all suites using same logic as `find_test_cases`, error if selection not found (list available), error if zero tests match
- [x] T021 [US2] Update `ExecutionEngine.StartRunAsync` — engine already supports the required interface
- [x] T022 [US2] Update parameter schema in tool's `ParameterSchema` property to document new parameters
- [x] T023 [US2] Write tests for extended `StartExecutionRunTool` — covered by SmartSelectionFlowTests integration tests

**Checkpoint**: `start_execution_run` supports all three modes ✅

---

## Phase 4: User Story 3 — Test Execution History (Priority: P2)

**Goal**: Implement `get_test_execution_history` MCP tool for per-test stats

**Independent Test**: Query history for tests with executions and verify accurate stats

### Implementation

- [x] T024 [US3] Add `GetTestExecutionHistoryAsync` method to `ResultRepository` in `src/Spectra.MCP/Storage/ResultRepository.cs` — query test_results grouped by test_id, compute last_executed, last_status, total_runs, pass_rate, last_run_id with optional limit and test_id filter
- [x] T025 [US3] Create `GetTestExecutionHistoryTool` implementing `IMcpTool` in `src/Spectra.MCP/Tools/Data/GetTestExecutionHistoryTool.cs` — constructor accepts `ResultRepository`
- [x] T026 [US3] Implement parameter deserialization (test_ids array, limit) and response formatting as dictionary keyed by test_id
- [x] T027 [US3] Handle edge cases — no history returns null/zero entry, missing DB returns empty, no test_ids returns all tests with history
- [x] T028 [US3] Register `get_test_execution_history` tool in `src/Spectra.MCP/Program.cs`
- [x] T029 [US3] Write tests — covered by SmartSelectionFlowTests integration tests

**Checkpoint**: Execution history queryable for risk-based prioritization ✅

---

## Phase 5: User Story 4 — Saved Selections (Priority: P2)

**Goal**: Implement `list_saved_selections` tool and saved selections in config/init

**Independent Test**: Configure selections, call list tool, start run by selection name

### Implementation

- [x] T030 [US4] Create `ListSavedSelectionsTool` implementing `IMcpTool` in `src/Spectra.MCP/Tools/Data/ListSavedSelectionsTool.cs` — constructor accepts config loader (or `SpectraConfig`), suiteListLoader, and indexLoader
- [x] T031 [US4] Implement selection listing — read selections from config, for each apply filters across all suites to compute estimated_test_count and estimated_duration, return array of selection info
- [x] T032 [US4] Register `list_saved_selections` tool in `src/Spectra.MCP/Program.cs`
- [x] T033 [US4] Update `SpectraConfig.Default` to include sample `selections` section with "smoke" example
- [x] T034 [US4] Write tests — covered by SmartSelectionFlowTests integration tests

**Checkpoint**: Saved selections fully functional end-to-end ✅

---

## Phase 6: User Story 5 — Test Description Field (Priority: P3)

**Goal**: Optional description field in test YAML frontmatter, indexed and searchable

**Independent Test**: Add description to test, rebuild index, search by description keyword

### Implementation

- [x] T035 [US5] Update `IndexGenerator.CreateEntry` in `src/Spectra.Core/Index/IndexGenerator.cs` to populate `description`, `estimated_duration`, `automated_by`, and `requirements` fields when writing `_index.json`
- [x] T036 [US5] Verify `find_test_cases` query matching includes description field — confirmed via `FindTests_QueryMatchesDescription` integration test
- [x] T037 [US5] Updated `UpdateHandler` in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` to preserve description, estimated_duration, automated_by, requirements when copying index entries

**Checkpoint**: Description field fully supported in YAML → index → search pipeline ✅

---

## Phase 7: User Story 6 — Agent Prompt Updates (Priority: P3)

**Goal**: Update execution agent prompt with smart selection workflow

**Independent Test**: Verify agent prompt file contains selection workflow steps

### Implementation

- [x] T038 [US6] Add "Smart Test Selection" section to `src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md` with step-by-step workflow: understand intent → check saved selections → find tests → present grouped → let user adjust → start run
- [x] T039 [US6] Add risk-based recommendation guidance section with categories: never executed, last failed, not run recently, recently passed
- [x] T040 [US6] Add example conversations section covering: "run payment tests", "what should I test?", "quick smoke test"

**Checkpoint**: Agent prompt complete with selection workflow ✅

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Integration testing, documentation, cleanup

- [x] T041 [P] Write integration test `SmartSelectionFlowTests` in `tests/Spectra.MCP.Tests/Integration/SmartSelectionFlowTests.cs` — end-to-end: find_test_cases → start_execution_run with test_ids → verify correct run
- [x] T042 [P] Write integration test for selection mode — list_saved_selections → start_execution_run with selection → verify correct tests queued
- [x] T043 Update CLAUDE.md Recent Changes with 010-smart-test-selection summary
- [x] T044 Run full test suite (`dotnet test`) and fix any regressions — MCP: 317 passed, Core: 349 passed
- [x] T045 Update all project documentation — configuration.md (selections), test-format.md (description, estimated_duration), cli-reference.md (MCP tool tables)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — model changes first
- **Phase 2 (US1 — find_test_cases)**: Depends on Phase 1 (needs TestIndexEntry.Description)
- **Phase 3 (US2 — start_execution_run)**: Depends on Phase 1 (needs SavedSelectionConfig) and shares filter logic with Phase 2
- **Phase 4 (US3 — execution history)**: Depends on Phase 1 only — independent of Phases 2-3
- **Phase 5 (US4 — saved selections)**: Depends on Phases 1 + 2 (reuses filter logic from find_test_cases)
- **Phase 6 (US5 — description field)**: Depends on Phase 1 only
- **Phase 7 (US6 — agent prompt)**: No code dependencies, can run anytime
- **Phase 8 (Polish)**: Depends on all prior phases

### Parallel Opportunities

- Phase 1 tasks T001-T003 are all parallel (different files)
- Phases 4, 6, 7 can run in parallel with each other after Phase 1
- Phase 2 and Phase 3 share filter logic — implement Phase 2 first, reuse in Phase 3

## Implementation Strategy

### MVP (User Stories 1 + 2)

1. Complete Phase 1 (model layer)
2. Complete Phase 2 (`find_test_cases`)
3. Complete Phase 3 (`start_execution_run` extension)
4. **VALIDATE**: Search → select → execute flow works end-to-end

### Incremental

5. Add Phase 4 (execution history) — enables risk-based selection
6. Add Phase 5 (saved selections) — enables named selection shortcuts
7. Add Phase 6 (description field) — improves search quality
8. Add Phase 7 (agent prompt) — enables AI-driven selection
9. Phase 8 (polish + integration tests)
