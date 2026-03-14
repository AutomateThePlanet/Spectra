# Tasks: MCP Execution Server

**Input**: Design documents from `/specs/002-mcp-execution-server/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mcp-tools.md

**Tests**: Included per Development Workflow Compliance (xUnit tests for MCP tools, state machine, reports)

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Project initialization and Spectra.MCP project structure

- [X] T001 Create Spectra.MCP project in src/Spectra.MCP/Spectra.MCP.csproj with ASP.NET Core dependencies
- [X] T002 Add project reference from Spectra.MCP to Spectra.Core in Spectra.slnx
- [X] T003 Create Spectra.MCP.Tests project in tests/Spectra.MCP.Tests/Spectra.MCP.Tests.csproj
- [X] T004 [P] Create directory structure: Server/, Tools/, Execution/, Storage/, Reports/, Identity/, Infrastructure/
- [X] T005 [P] Configure .gitignore for .execution/ directory and reports/ if not already present

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

### Core Models (in Spectra.Core)

- [X] T006 [P] Create RunStatus enum in src/Spectra.Core/Models/Execution/RunStatus.cs
- [X] T007 [P] Create TestStatus enum in src/Spectra.Core/Models/Execution/TestStatus.cs
- [X] T008 [P] Create Run model in src/Spectra.Core/Models/Execution/Run.cs
- [X] T009 [P] Create RunFilters record in src/Spectra.Core/Models/Execution/RunFilters.cs
- [X] T010 [P] Create TestResult model in src/Spectra.Core/Models/Execution/TestResult.cs
- [X] T011 [P] Create TestHandle static class in src/Spectra.Core/Models/Execution/TestHandle.cs
- [X] T012 [P] Create QueuedTest record in src/Spectra.Core/Models/Execution/QueuedTest.cs
- [X] T013 [P] Create McpToolResponse wrapper in src/Spectra.Core/Models/Execution/McpToolResponse.cs
- [X] T014 [P] Create ErrorInfo record in src/Spectra.Core/Models/Execution/ErrorInfo.cs

### SQLite Storage Infrastructure

- [X] T015 Create ExecutionDb class with connection management in src/Spectra.MCP/Storage/ExecutionDb.cs
- [X] T016 Implement schema initialization (runs, test_results tables) in src/Spectra.MCP/Storage/ExecutionDb.cs
- [X] T017 [P] Create RunRepository with CRUD operations in src/Spectra.MCP/Storage/RunRepository.cs
- [X] T018 [P] Create ResultRepository with CRUD operations in src/Spectra.MCP/Storage/ResultRepository.cs

### State Machine

- [X] T019 Create StateMachine class with transition validation in src/Spectra.MCP/Execution/StateMachine.cs
- [X] T020 Implement RunStatus transitions (Created→Running→Paused→Completed/Cancelled) in StateMachine.cs
- [X] T021 Implement TestStatus transitions (Pending→InProgress→Passed/Failed/Skipped/Blocked) in StateMachine.cs

### MCP Server Infrastructure

- [X] T022 Create McpServer host with JSON-RPC handling in src/Spectra.MCP/Server/McpServer.cs
- [X] T023 Create McpProtocol for request/response parsing in src/Spectra.MCP/Server/McpProtocol.cs
- [X] T024 Create tool registration system in src/Spectra.MCP/Server/ToolRegistry.cs
- [X] T025 [P] Create UserIdentityResolver in src/Spectra.MCP/Identity/UserIdentityResolver.cs
- [X] T026 [P] Create McpConfig for configuration loading in src/Spectra.MCP/Infrastructure/McpConfig.cs
- [X] T027 [P] Create McpLogging with verbosity levels in src/Spectra.MCP/Infrastructure/McpLogging.cs

### Foundational Tests

- [X] T028 [P] Create StateMachineTests in tests/Spectra.MCP.Tests/Execution/StateMachineTests.cs
- [X] T029 [P] Create TestHandleTests in tests/Spectra.MCP.Tests/Models/TestHandleTests.cs
- [X] T030 [P] Create ExecutionDbTests in tests/Spectra.MCP.Tests/Storage/ExecutionDbTests.cs

**Checkpoint**: Foundation ready - user story implementation can begin

---

## Phase 3: User Story 1 - Execute a Test Suite (Priority: P1) MVP

**Goal**: Enable testers to start a run, execute tests one-by-one, record results, and finalize with report

**Independent Test**: Start a run, execute 3+ tests with mixed results (pass/fail), verify final report

### Tests for User Story 1

- [X] T031 [P] [US1] Contract test for start_execution_run in tests/Spectra.MCP.Tests/Tools/StartExecutionRunTests.cs
- [X] T032 [P] [US1] Contract test for get_test_case_details in tests/Spectra.MCP.Tests/Tools/GetTestCaseDetailsTests.cs
- [X] T033 [P] [US1] Contract test for advance_test_case in tests/Spectra.MCP.Tests/Tools/AdvanceTestCaseTests.cs
- [X] T034 [P] [US1] Contract test for finalize_execution_run in tests/Spectra.MCP.Tests/Tools/FinalizeExecutionRunTests.cs
- [X] T035 [P] [US1] Integration test for full execution flow in tests/Spectra.MCP.Tests/Integration/ExecutionFlowTests.cs

### Core Execution Engine

- [X] T036 [US1] Create ExecutionEngine orchestrator in src/Spectra.MCP/Execution/ExecutionEngine.cs
- [X] T037 [US1] Create TestQueue with ordering logic in src/Spectra.MCP/Execution/TestQueue.cs
- [X] T038 [US1] Implement dependency ordering in TestQueue (by depends_on, priority, ID)
- [X] T039 [US1] Create DependencyResolver for cascade blocking in src/Spectra.MCP/Execution/DependencyResolver.cs

### Run Management Tools (US1 subset)

- [X] T040 [US1] Implement start_execution_run tool in src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs
- [X] T041 [US1] Implement get_execution_status tool in src/Spectra.MCP/Tools/RunManagement/GetExecutionStatusTool.cs
- [X] T042 [US1] Implement finalize_execution_run tool in src/Spectra.MCP/Tools/RunManagement/FinalizeExecutionRunTool.cs

### Test Execution Tools (US1 subset)

- [X] T043 [US1] Implement get_test_case_details tool in src/Spectra.MCP/Tools/TestExecution/GetTestCaseDetailsTool.cs
- [X] T044 [US1] Implement advance_test_case tool in src/Spectra.MCP/Tools/TestExecution/AdvanceTestCaseTool.cs

### Report Generation

- [X] T045 [P] [US1] Create ExecutionReport model in src/Spectra.Core/Models/Execution/ExecutionReport.cs
- [X] T046 [P] [US1] Create ReportSummary record in src/Spectra.Core/Models/Execution/ReportSummary.cs
- [X] T047 [US1] Create ReportGenerator in src/Spectra.MCP/Reports/ReportGenerator.cs
- [X] T048 [US1] Create ReportWriter (JSON + Markdown) in src/Spectra.MCP/Reports/ReportWriter.cs
- [X] T049 [P] [US1] Create ReportGeneratorTests in tests/Spectra.MCP.Tests/Reports/ReportGeneratorTests.cs

**Checkpoint**: User Story 1 complete - basic test execution flow works end-to-end

---

## Phase 4: User Story 2 - Pause and Resume Execution (Priority: P1)

**Goal**: Enable testers to pause mid-suite and resume later without losing progress

**Independent Test**: Start run, execute 2 tests, pause, resume, verify next test is correct

### Tests for User Story 2

- [X] T050 [P] [US2] Contract test for pause_execution_run in tests/Spectra.MCP.Tests/Tools/PauseExecutionRunTests.cs
- [X] T051 [P] [US2] Contract test for resume_execution_run in tests/Spectra.MCP.Tests/Tools/ResumeExecutionRunTests.cs
- [X] T052 [P] [US2] Integration test for pause/resume flow in tests/Spectra.MCP.Tests/Integration/PauseResumeTests.cs

### Implementation

- [X] T053 [US2] Implement pause_execution_run tool in src/Spectra.MCP/Tools/RunManagement/PauseExecutionRunTool.cs
- [X] T054 [US2] Implement resume_execution_run tool in src/Spectra.MCP/Tools/RunManagement/ResumeExecutionRunTool.cs
- [X] T055 [US2] Add state preservation logic in ExecutionEngine for pause/resume
- [X] T056 [US2] Implement queue position tracking on resume in TestQueue.cs

**Checkpoint**: User Story 2 complete - pause/resume preserves progress

---

## Phase 5: User Story 3 - Skip and Block Tests (Priority: P1)

**Goal**: Enable testers to skip tests with reason; auto-block dependents

**Independent Test**: Skip a prerequisite test, verify dependents are blocked with correct reason chain

### Tests for User Story 3

- [X] T057 [P] [US3] Contract test for skip_test_case in tests/Spectra.MCP.Tests/Tools/SkipTestCaseTests.cs
- [X] T058 [P] [US3] Contract test for add_test_note in tests/Spectra.MCP.Tests/Tools/AddTestNoteTests.cs
- [X] T059 [P] [US3] Unit test for transitive blocking in tests/Spectra.MCP.Tests/Execution/DependencyResolverTests.cs
- [X] T060 [P] [US3] Integration test for cascade blocking in tests/Spectra.MCP.Tests/Integration/BlockingCascadeTests.cs

### Implementation

- [X] T061 [US3] Implement skip_test_case tool in src/Spectra.MCP/Tools/TestExecution/SkipTestCaseTool.cs
- [X] T062 [US3] Implement add_test_note tool in src/Spectra.MCP/Tools/TestExecution/AddTestNoteTool.cs
- [X] T063 [US3] Implement transitive cascade blocking in DependencyResolver.PropagateBlocks()
- [X] T064 [US3] Update advance_test_case to trigger blocking on failure
- [X] T065 [US3] Add blocked_by tracking in TestResult and reports

**Checkpoint**: User Story 3 complete - skip/block with transitive cascade works

---

## Phase 6: User Story 4 - Filter Tests Before Execution (Priority: P2)

**Goal**: Enable testers to run filtered subsets (by priority, tags, component, IDs)

**Independent Test**: Start filtered run with priority=high, verify only matching tests included

### Tests for User Story 4

- [X] T066 [P] [US4] Unit test for filter by priority in tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs
- [X] T067 [P] [US4] Unit test for filter by tags in tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs
- [X] T068 [P] [US4] Unit test for filter by component in tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs
- [X] T069 [P] [US4] Unit test for filter by test IDs in tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs
- [X] T070 [P] [US4] Integration test for filtered execution in tests/Spectra.MCP.Tests/Integration/FilteredExecutionTests.cs

### Implementation

- [X] T071 [US4] Implement priority filter in TestQueue.BuildQueue()
- [X] T072 [US4] Implement tags filter (AND logic) in TestQueue.BuildQueue()
- [X] T073 [US4] Implement component filter in TestQueue.BuildQueue()
- [X] T074 [US4] Implement test IDs filter with dependency inclusion in TestQueue.BuildQueue()
- [X] T075 [US4] Add NO_TESTS_MATCH error handling in start_execution_run

**Checkpoint**: User Story 4 complete - filtered runs work

---

## Phase 7: User Story 5 - View Available Suites (Priority: P2)

**Goal**: Enable testers to discover available suites before starting a run

**Independent Test**: List suites, verify all test directories appear with correct counts

### Tests for User Story 5

- [X] T076 [P] [US5] Contract test for list_available_suites in tests/Spectra.MCP.Tests/Tools/ListAvailableSuitesTests.cs

### Implementation

- [X] T077 [US5] Implement list_available_suites tool in src/Spectra.MCP/Tools/RunManagement/ListAvailableSuitesTool.cs
- [X] T078 [US5] Integrate with Spectra.Core index reading for suite discovery
- [X] T079 [US5] Add INDEX_STALE and NO_SUITES_FOUND error handling

**Checkpoint**: User Story 5 complete - suite discovery works

---

## Phase 8: User Story 6 - Retest Failed Tests (Priority: P2)

**Goal**: Enable testers to re-queue specific tests within an active run

**Independent Test**: Fail a test, retest it, verify it reappears with attempt=2

### Tests for User Story 6

- [X] T080 [P] [US6] Contract test for retest_test_case in tests/Spectra.MCP.Tests/Tools/RetestTestCaseTests.cs
- [X] T081 [P] [US6] Integration test for retest flow in tests/Spectra.MCP.Tests/Integration/RetestFlowTests.cs

### Implementation

- [X] T082 [US6] Implement retest_test_case tool in src/Spectra.MCP/Tools/TestExecution/RetestTestCaseTool.cs
- [X] T083 [US6] Implement Requeue operation in TestQueue with attempt increment
- [X] T084 [US6] Generate new handle for retested test
- [X] T085 [US6] Update report to show all attempts per test

**Checkpoint**: User Story 6 complete - retest functionality works

---

## Phase 9: User Story 7 - View Execution History (Priority: P3)

**Goal**: Enable testers to view past execution runs

**Independent Test**: Complete multiple runs, query history, verify all appear

### Tests for User Story 7

- [X] T086 [P] [US7] Contract test for get_run_history in tests/Spectra.MCP.Tests/Tools/GetRunHistoryTests.cs
- [X] T087 [P] [US7] Contract test for get_execution_summary in tests/Spectra.MCP.Tests/Tools/GetExecutionSummaryTests.cs

### Implementation

- [X] T088 [US7] Implement get_run_history tool in src/Spectra.MCP/Tools/Reporting/GetRunHistoryTool.cs
- [X] T089 [US7] Implement get_execution_summary tool in src/Spectra.MCP/Tools/Reporting/GetExecutionSummaryTool.cs
- [X] T090 [US7] Add user filter to get_run_history
- [X] T091 [US7] Add limit parameter handling in get_run_history

**Checkpoint**: User Story 7 complete - history and summary reporting works

---

## Phase 10: User Story 8 - Concurrent Execution by Multiple Users (Priority: P3)

**Goal**: Enable concurrent runs by different users; prevent same-user conflicts on same suite

**Independent Test**: Two users start runs on different suites, verify both proceed independently

### Tests for User Story 8

- [X] T092 [P] [US8] Unit test for active run check in tests/Spectra.MCP.Tests/Storage/RunRepositoryTests.cs
- [X] T093 [P] [US8] Contract test for cancel_execution_run in tests/Spectra.MCP.Tests/Tools/CancelExecutionRunTests.cs
- [X] T094 [P] [US8] Integration test for concurrent users in tests/Spectra.MCP.Tests/Integration/ConcurrentUsersTests.cs

### Implementation

- [X] T095 [US8] Implement cancel_execution_run tool in src/Spectra.MCP/Tools/RunManagement/CancelExecutionRunTool.cs
- [X] T096 [US8] Add GetActiveRunAsync(suite, user) to RunRepository
- [X] T097 [US8] Implement ACTIVE_RUN_EXISTS check in start_execution_run
- [X] T098 [US8] Add user validation (NOT_OWNER) to pause/resume/cancel tools
- [X] T099 [US8] Implement run timeout for ABANDONED status (72h default)

**Checkpoint**: User Story 8 complete - concurrent execution with user isolation works

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements across all user stories

- [X] T100 [P] Add XML documentation to all public APIs in src/Spectra.MCP/
- [X] T101 [P] Add XML documentation to new models in src/Spectra.Core/Models/Execution/
- [X] T102 Review and ensure all tools return self-contained responses with next_expected_action
- [X] T103 Verify all error codes match contracts/mcp-tools.md specification
- [X] T104 [P] Create Program.cs entry point in src/Spectra.MCP/Program.cs
- [X] T105 Performance: verify tool responses <100ms for local operations
- [X] T106 Run full test suite and ensure 80%+ coverage on Core, 60%+ on MCP
- [X] T107 Validate quickstart.md workflow works end-to-end with real MCP server

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories
- **User Stories (Phase 3-10)**: All depend on Foundational phase completion
  - P1 stories (US1, US2, US3) should complete before P2/P3
  - P2 stories (US4, US5, US6) can proceed after P1 or in parallel
  - P3 stories (US7, US8) can proceed after P2 or in parallel
- **Polish (Phase 11)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (Execute Suite)**: Foundational only - No other story dependencies - **MVP**
- **US2 (Pause/Resume)**: Depends on US1 (uses same run infrastructure)
- **US3 (Skip/Block)**: Depends on US1 (extends advance_test_case)
- **US4 (Filter)**: Depends on US1 (extends start_execution_run)
- **US5 (View Suites)**: Foundational only - Can run in parallel with US1
- **US6 (Retest)**: Depends on US1 (extends queue management)
- **US7 (History)**: Depends on US1 (requires completed runs)
- **US8 (Concurrent)**: Depends on US1 (extends user validation)

### Parallel Opportunities

Within each phase, tasks marked [P] can run in parallel:
- All model creation tasks (T006-T014)
- All repository tasks (T017-T018)
- All test file creation within a story

---

## Parallel Example: Foundational Phase

```bash
# Launch all models in parallel:
Task: "Create RunStatus enum in src/Spectra.Core/Models/Execution/RunStatus.cs"
Task: "Create TestStatus enum in src/Spectra.Core/Models/Execution/TestStatus.cs"
Task: "Create Run model in src/Spectra.Core/Models/Execution/Run.cs"
Task: "Create RunFilters record in src/Spectra.Core/Models/Execution/RunFilters.cs"
Task: "Create TestResult model in src/Spectra.Core/Models/Execution/TestResult.cs"
Task: "Create TestHandle static class in src/Spectra.Core/Models/Execution/TestHandle.cs"

# Then launch repositories in parallel:
Task: "Create RunRepository in src/Spectra.MCP/Storage/RunRepository.cs"
Task: "Create ResultRepository in src/Spectra.MCP/Storage/ResultRepository.cs"
```

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel:
Task: "Contract test for start_execution_run"
Task: "Contract test for get_test_case_details"
Task: "Contract test for advance_test_case"
Task: "Contract test for finalize_execution_run"
Task: "Integration test for full execution flow"

# Launch report models in parallel:
Task: "Create ExecutionReport model"
Task: "Create ReportSummary record"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test full execution flow end-to-end
5. Deploy/demo basic test execution

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add US1 (Execute) → Test independently → MVP!
3. Add US2 (Pause/Resume) → Real-world usability
4. Add US3 (Skip/Block) → Handle dependencies
5. Add US4-6 (P2) → Enhanced features
6. Add US7-8 (P3) → Team features

### Task Summary

| Phase | Tasks | Story |
|-------|-------|-------|
| Setup | T001-T005 (5) | - |
| Foundational | T006-T030 (25) | - |
| US1: Execute | T031-T049 (19) | P1 |
| US2: Pause/Resume | T050-T056 (7) | P1 |
| US3: Skip/Block | T057-T065 (9) | P1 |
| US4: Filter | T066-T075 (10) | P2 |
| US5: View Suites | T076-T079 (4) | P2 |
| US6: Retest | T080-T085 (6) | P2 |
| US7: History | T086-T091 (6) | P3 |
| US8: Concurrent | T092-T099 (8) | P3 |
| Polish | T100-T107 (8) | - |
| **Total** | **107 tasks** | |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after completion
- Tests are included per Development Workflow Compliance
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
