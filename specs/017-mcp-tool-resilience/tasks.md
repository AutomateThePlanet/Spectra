# Tasks: MCP Tool Resilience for Weaker Models

**Input**: Design documents from `/specs/017-mcp-tool-resilience/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/mcp-tools.md, quickstart.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new projects needed. Verify existing test suite passes before making changes.

- [x] T001 Run `dotnet test` to confirm all 306 existing MCP tests pass as baseline

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Repository queries and shared resolver that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Add `GetActiveRunsAsync()` method to `src/Spectra.MCP/Storage/RunRepository.cs` — query all runs WHERE status IN ('Created','Running','Paused') ORDER BY started_at DESC, returning `Task<IReadOnlyList<Run>>`
- [x] T003 [P] Add `GetInProgressTestsAsync(string runId)` method to `src/Spectra.MCP/Storage/ResultRepository.cs` — query test_results WHERE run_id = @runId AND status = 'InProgress', returning `Task<IReadOnlyList<TestResult>>`
- [x] T004 Create `ActiveRunResolver` in `src/Spectra.MCP/Tools/ActiveRunResolver.cs` — implement `ResolveRunIdAsync(string? runId, RunRepository repo)` returning `(string? resolvedRunId, string? errorJson)`. When runId is provided, return it directly. When null: query GetActiveRunsAsync, return the single run's ID if exactly 1, or return formatted error JSON (NO_ACTIVE_RUNS / MULTIPLE_ACTIVE_RUNS with run listing) if 0 or 2+. See contracts/mcp-tools.md for exact error response format.
- [x] T005 Add `ResolveTestHandleAsync(string? testHandle, string runId, ResultRepository repo)` to `src/Spectra.MCP/Tools/ActiveRunResolver.cs` — when testHandle is provided, return it directly. When null: query GetInProgressTestsAsync(runId), return the single test's handle if exactly 1, or return formatted error JSON (NO_TEST_IN_PROGRESS / MULTIPLE_TESTS_IN_PROGRESS with test listing) if 0 or 2+. See contracts/mcp-tools.md for exact error response format.
- [x] T006 [P] Write unit tests for `ActiveRunResolver` in `tests/Spectra.MCP.Tests/Tools/ActiveRunResolverTests.cs` — cover: explicit run_id bypasses resolution, 0 active runs returns NO_ACTIVE_RUNS error, 1 active run returns that run_id, 2+ active runs returns MULTIPLE_ACTIVE_RUNS error with listing, explicit test_handle bypasses resolution, 0 in-progress tests returns NO_TEST_IN_PROGRESS error, 1 in-progress test returns that handle, 2+ in-progress tests returns MULTIPLE_TESTS_IN_PROGRESS error with listing

**Checkpoint**: Foundation ready — repository queries and resolver tested, user story implementation can begin

---

## Phase 3: User Story 1 — Auto-Resolve Run ID When Omitted (Priority: P1) MVP

**Goal**: All 13 tools that accept `run_id` treat it as optional. When omitted with exactly 1 active run, auto-resolve. When 0 or 2+ active runs, return descriptive error.

**Independent Test**: Start a single run, call `get_execution_status` with empty `{}` input — it should return the run's status using auto-detection.

### Implementation for User Story 1

**Run Management tools (5 tools):**

- [x] T007 [US1] Update `GetExecutionStatusTool` in `src/Spectra.MCP/Tools/RunManagement/GetExecutionStatusTool.cs` — inject RunRepository into constructor, replace `run_id` null/empty check with `ActiveRunResolver.ResolveRunIdAsync()` call, remove `run_id` from `required` array in ParameterSchema, update description to mention auto-detection
- [x] T008 [P] [US1] Update `PauseExecutionRunTool` in `src/Spectra.MCP/Tools/RunManagement/PauseExecutionRunTool.cs` — same pattern as T007: inject RunRepository, replace validation with resolver call, update ParameterSchema
- [x] T009 [P] [US1] Update `ResumeExecutionRunTool` in `src/Spectra.MCP/Tools/RunManagement/ResumeExecutionRunTool.cs` — same pattern as T007
- [x] T010 [P] [US1] Update `CancelExecutionRunTool` in `src/Spectra.MCP/Tools/RunManagement/CancelExecutionRunTool.cs` — same pattern as T007
- [x] T011 [P] [US1] Update `FinalizeExecutionRunTool` in `src/Spectra.MCP/Tools/RunManagement/FinalizeExecutionRunTool.cs` — same pattern as T007

**Test Execution tools (5 tools needing run_id):**

- [x] T012 [P] [US1] Update `GetTestCaseDetailsTool` in `src/Spectra.MCP/Tools/TestExecution/GetTestCaseDetailsTool.cs` — inject RunRepository, replace run_id validation with resolver call, update ParameterSchema (run_id optional, test_handle stays required for now — US2 will make it optional)
- [x] T013 [P] [US1] Update `BulkRecordResultsTool` in `src/Spectra.MCP/Tools/TestExecution/BulkRecordResultsTool.cs` — inject RunRepository, replace run_id validation with resolver call, update ParameterSchema
- [x] T014 [P] [US1] Update `RetestTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/RetestTestCaseTool.cs` — inject RunRepository, replace run_id validation with resolver call, update ParameterSchema
- [x] T015 [P] [US1] Update `AdvanceTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs` — this tool uses test_handle (not explicit run_id), but needs RunRepository injected for US2 test_handle resolution. For now, no run_id change needed if tool doesn't take run_id directly. Verify and skip if no run_id param exists.
- [x] T016 [P] [US1] Update `SkipTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/SkipTestCaseTool.cs` — same as T015: verify if run_id param exists and update accordingly

**Reporting tools (1 tool):**

- [x] T017 [P] [US1] Update `GetExecutionSummaryTool` in `src/Spectra.MCP/Tools/Reporting/GetExecutionSummaryTool.cs` — inject RunRepository, replace run_id validation with resolver call, update ParameterSchema

**Registration update:**

- [x] T018 [US1] Update tool registration in `src/Spectra.MCP/Program.cs` — pass RunRepository instance to all tools that now require it in their constructor. Verify all 11+ tools compile with new constructor signatures.

**Tests:**

- [x] T019 [US1] Write integration tests for run_id auto-resolution in `tests/Spectra.MCP.Tests/Tools/RunIdAutoResolutionTests.cs` — test `GetExecutionStatusTool` with: (a) empty input + 1 active run → success, (b) empty input + 0 runs → NO_ACTIVE_RUNS error, (c) empty input + 2 runs → MULTIPLE_ACTIVE_RUNS error with listing, (d) explicit run_id → existing behavior unchanged
- [x] T020 [US1] Run `dotnet test` to verify all existing tests still pass (zero regressions) plus new tests pass

**Checkpoint**: US1 complete — any tool accepting run_id auto-resolves when omitted. Weaker models can now call run-level tools with `{}`.

---

## Phase 4: User Story 2 — Auto-Resolve Test Handle When Omitted (Priority: P1)

**Goal**: All 6 tools that accept `test_handle` treat it as optional. When omitted with exactly 1 in-progress test, auto-resolve. Requires run_id resolution from US1 for tools that need both.

**Independent Test**: Start a run, get test details (puts test in IN_PROGRESS), call `advance_test_case` with only `{"status": "PASSED"}` — it should find the in-progress test automatically.

### Implementation for User Story 2

- [x] T021 [P] [US2] Update `AdvanceTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs` — inject ResultRepository and RunRepository (if not already). Replace test_handle null/empty check with: (1) if test_handle provided, use it; (2) if not, resolve run_id first via ActiveRunResolver, then resolve test_handle via ActiveRunResolver.ResolveTestHandleAsync(). Remove `test_handle` from `required` array in ParameterSchema. Update description.
- [x] T022 [P] [US2] Update `SkipTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/SkipTestCaseTool.cs` — same pattern as T021
- [x] T023 [P] [US2] Update `GetTestCaseDetailsTool` in `src/Spectra.MCP/Tools/TestExecution/GetTestCaseDetailsTool.cs` — add test_handle auto-resolution (run_id resolution added in US1). Remove `test_handle` from `required` array. Note: this tool also accepts `run_id` which was made optional in US1.
- [x] T024 [P] [US2] Update `AddTestNoteTool` in `src/Spectra.MCP/Tools/TestExecution/AddTestNoteTool.cs` — inject RunRepository and ResultRepository. This tool only takes test_handle (no run_id), so resolution flow is: resolve run_id from active runs → resolve test_handle from in-progress tests. Remove `test_handle` from `required` array.
- [x] T025 [P] [US2] Update `SaveScreenshotTool` in `src/Spectra.MCP/Tools/TestExecution/SaveScreenshotTool.cs` — same pattern as T024 (test_handle only, needs run_id resolved first)
- [x] T026 [P] [US2] Update `RetestTestCaseTool` in `src/Spectra.MCP/Tools/TestExecution/RetestTestCaseTool.cs` — this tool uses `test_id` not `test_handle`. If it accepts test_handle, make it optional with auto-resolution. If it only uses test_id, add auto-resolution for test_id from in-progress test. Verify parameter shape and adjust.
- [x] T027 [US2] Write integration tests for test_handle auto-resolution in `tests/Spectra.MCP.Tests/Tools/TestHandleAutoResolutionTests.cs` — test `AdvanceTestCaseTool` with: (a) `{"status":"PASSED"}` + 1 in-progress test → success, (b) `{"status":"PASSED"}` + 0 in-progress tests → NO_TEST_IN_PROGRESS error, (c) explicit test_handle → existing behavior unchanged
- [x] T028 [US2] Run `dotnet test` to verify all tests pass

**Checkpoint**: US1+US2 complete — weaker models can call any tool with minimal or empty input and get auto-resolution or helpful errors.

---

## Phase 5: User Story 3 — List Active Runs (Priority: P2)

**Goal**: New `list_active_runs` tool returns all non-terminal runs with progress summaries.

**Independent Test**: Start 2 runs (one RUNNING, one PAUSED), call `list_active_runs` — both appear with correct details and progress.

### Implementation for User Story 3

- [x] T029 [US3] Create `ListActiveRunsTool` in `src/Spectra.MCP/Tools/RunManagement/ListActiveRunsTool.cs` — implement IMcpTool. No required parameters. Query RunRepository.GetActiveRunsAsync(), for each run get ResultRepository.GetStatusCountsAsync() to build progress string (e.g., "5/20 completed, 3 passed, 2 failed"). Return McpToolResponse with runs array including run_id, suite, status, started_at, started_by, environment, progress. If no active runs, include message "No active runs found." and next_expected_action: "start_execution_run". See contracts/mcp-tools.md for exact response format.
- [x] T030 [US3] Register `list_active_runs` in `src/Spectra.MCP/Program.cs` — add `registry.Register("list_active_runs", new ListActiveRunsTool(runRepo, resultRepo))` alongside existing run management tools
- [x] T031 [US3] Write tests for `ListActiveRunsTool` in `tests/Spectra.MCP.Tests/Tools/ListActiveRunsToolTests.cs` — cover: no active runs returns empty with message, 1 active run returns it with progress, mix of active and terminal runs returns only active, CREATED state runs are included, progress string format is correct
- [x] T032 [US3] Run `dotnet test` to verify all tests pass

**Checkpoint**: US3 complete — orchestrators can discover active runs before choosing which to interact with.

---

## Phase 6: User Story 4 — Cancel All Active Runs (Priority: P2)

**Goal**: New `cancel_all_active_runs` tool transitions all non-terminal runs to CANCELLED in one call.

**Independent Test**: Start 3 runs in different states (CREATED, RUNNING, PAUSED), call `cancel_all_active_runs` — all become CANCELLED, response lists each with previous state.

### Implementation for User Story 4

- [x] T033 [US4] Create `CancelAllActiveRunsTool` in `src/Spectra.MCP/Tools/RunManagement/CancelAllActiveRunsTool.cs` — implement IMcpTool. Optional `reason` parameter. Query RunRepository.GetActiveRunsAsync(), iterate and call ExecutionEngine.CancelRunAsync() for each. Collect successes (run_id, suite, previous_status) and failures (run_id, suite, reason). Return McpToolResponse with cancelled array, cancelled_count, failed array. If no active runs, include message "No active runs to cancel." See contracts/mcp-tools.md for exact response format.
- [x] T034 [US4] Register `cancel_all_active_runs` in `src/Spectra.MCP/Program.cs` — add `registry.Register("cancel_all_active_runs", new CancelAllActiveRunsTool(engine, runRepo))`
- [x] T035 [US4] Write tests for `CancelAllActiveRunsTool` in `tests/Spectra.MCP.Tests/Tools/CancelAllActiveRunsToolTests.cs` — cover: no active runs returns empty with message, 3 active runs all cancelled with correct previous_status listing, mix of active and terminal runs only cancels active, partial failure (if possible to simulate) reports both successes and failures
- [x] T036 [US4] Run `dotnet test` to verify all tests pass

**Checkpoint**: US4 complete — users can clean up all active runs in a single call.

---

## Phase 7: User Story 5 — Enhanced Run History with Filters (Priority: P3)

**Goal**: `get_run_history` gains status filter and per-run pass/fail/skip summary counts.

**Independent Test**: Create runs across multiple suites and statuses, call `get_run_history` with `{"status": "COMPLETED"}` — only completed runs returned, each with summary counts.

### Implementation for User Story 5

- [x] T037 [US5] Add `status` parameter support to `RunRepository.GetAllAsync()` in `src/Spectra.MCP/Storage/RunRepository.cs` — add optional `RunStatus? status` parameter, append `AND status = @status` to WHERE clause when provided
- [x] T038 [US5] Update `GetRunHistoryTool` in `src/Spectra.MCP/Tools/Reporting/GetRunHistoryTool.cs` — add `status` property to request class with `[JsonPropertyName("status")]`, parse as RunStatus enum, pass to RunRepository.GetAllAsync(). Add `status` to ParameterSchema properties. For each run in results, call ResultRepository.GetStatusCountsAsync(run.RunId) and include summary object (total, passed, failed, skipped, blocked) in response. See contracts/mcp-tools.md for exact response format.
- [x] T039 [US5] Write tests for enhanced `GetRunHistoryTool` in `tests/Spectra.MCP.Tests/Tools/GetRunHistoryToolTests.cs` — add new test methods: status filter returns only matching runs, status filter with invalid value returns error, summary counts are correct per run, no filters returns all runs with summaries, combined suite + status filter works
- [x] T040 [US5] Run `dotnet test` to verify all tests pass

**Checkpoint**: US5 complete — full run history with filtering and summary statistics.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and cleanup

- [x] T041 Update MCP tool descriptions in all modified tools to mention auto-detection behavior for omitted parameters — ensure consistency in wording across all 13+ tools (e.g., "If omitted, auto-detects when exactly one active run exists.")
- [x] T042 Run full `dotnet test` suite — confirm all existing 306+ tests pass plus all new tests (target: zero regressions)
- [x] T043 Run `dotnet build` with no warnings to confirm clean compilation
- [x] T044 Update CLAUDE.md Recent Changes section with 017-mcp-tool-resilience summary: list new tools (list_active_runs, cancel_all_active_runs), auto-resolution behavior, enhanced get_run_history filters

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — baseline verification
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (T002, T004) — needs GetActiveRunsAsync + ActiveRunResolver
- **US2 (Phase 4)**: Depends on US1 completion — test_handle resolution builds on run_id resolution
- **US3 (Phase 5)**: Depends on Foundational (T002) — can start in parallel with US1/US2
- **US4 (Phase 6)**: Depends on Foundational (T002) — can start in parallel with US1/US2
- **US5 (Phase 7)**: Depends on Foundational only — independent of US1-US4
- **Polish (Phase 8)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational only — no dependencies on other stories
- **US2 (P1)**: Depends on US1 — test_handle resolution requires run_id resolution to work first
- **US3 (P2)**: Independent of US1/US2 — uses same repository query but separate tool
- **US4 (P2)**: Independent of US1/US2 — uses same repository query but separate tool
- **US5 (P3)**: Fully independent — only touches GetRunHistoryTool and RunRepository

### Within Each User Story

- Repository queries before resolver logic
- Resolver before tool modifications
- Tool modifications before registration updates
- Tests after implementation
- Regression check last

### Parallel Opportunities

- **Phase 2**: T002 and T003 can run in parallel (different repository files)
- **Phase 2**: T006 can run in parallel with T002/T003 (test file vs source files)
- **Phase 3**: T008-T011 can all run in parallel (different tool files)
- **Phase 3**: T012-T017 can all run in parallel (different tool files)
- **Phase 4**: T021-T026 can all run in parallel (different tool files)
- **Phase 5+6**: US3 and US4 can run in parallel (different new tool files)
- **Phase 5+7**: US3 and US5 can run in parallel (no shared files)

---

## Parallel Example: User Story 1

```bash
# After T007 establishes the pattern, launch remaining run management tools in parallel:
Task: "Update PauseExecutionRunTool in src/Spectra.MCP/Tools/RunManagement/PauseExecutionRunTool.cs"
Task: "Update ResumeExecutionRunTool in src/Spectra.MCP/Tools/RunManagement/ResumeExecutionRunTool.cs"
Task: "Update CancelExecutionRunTool in src/Spectra.MCP/Tools/RunManagement/CancelExecutionRunTool.cs"
Task: "Update FinalizeExecutionRunTool in src/Spectra.MCP/Tools/RunManagement/FinalizeExecutionRunTool.cs"

# Then launch test execution tools in parallel:
Task: "Update GetTestCaseDetailsTool in src/Spectra.MCP/Tools/TestExecution/GetTestCaseDetailsTool.cs"
Task: "Update BulkRecordResultsTool in src/Spectra.MCP/Tools/TestExecution/BulkRecordResultsTool.cs"
Task: "Update RetestTestCaseTool in src/Spectra.MCP/Tools/TestExecution/RetestTestCaseTool.cs"
Task: "Update GetExecutionSummaryTool in src/Spectra.MCP/Tools/Reporting/GetExecutionSummaryTool.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (baseline)
2. Complete Phase 2: Foundational (repository + resolver)
3. Complete Phase 3: User Story 1 (run_id auto-resolution)
4. **STOP and VALIDATE**: Test with empty `{}` calls — run_id resolves correctly
5. This alone fixes the primary GPT-4.1 problem for run-level operations

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Run-level auto-resolution works → **MVP!**
3. Add US2 → Test-level auto-resolution works → Full auto-resolution complete
4. Add US3 + US4 → Management tools available → Session cleanup enabled
5. Add US5 → Enhanced history → Full feature complete
6. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 → then US2 (sequential dependency)
   - Developer B: US3 + US4 (independent, parallel)
   - Developer C: US5 (fully independent)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- US2 is the only story with a hard dependency on another story (US1)
- The ActiveRunResolver pattern established in T007 should be followed exactly for T008-T017 to maintain consistency
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
