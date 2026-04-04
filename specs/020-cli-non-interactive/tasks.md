# Tasks: CLI Non-Interactive Mode and Structured Output

**Input**: Design documents from `/specs/020-cli-non-interactive/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: New types, enums, and utilities that all user stories depend on

- [x] T001 Create OutputFormat enum (Human, Json) in src/Spectra.CLI/Infrastructure/OutputFormat.cs
- [x] T002 Add MissingArguments = 3 constant to src/Spectra.CLI/Infrastructure/ExitCodes.cs
- [x] T003 Create base CommandResult record and ErrorResult in src/Spectra.CLI/Results/CommandResult.cs
- [x] T004 Create JsonResultWriter utility (serialize any CommandResult to stdout as JSON, camelCase, enums as strings, omit nulls) in src/Spectra.CLI/Output/JsonResultWriter.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Register global options and wire OutputFormat into existing output infrastructure

**CRITICAL**: No user story work can begin until this phase is complete

- [x] T005 Add --output-format and --no-interaction as global options in src/Spectra.CLI/Options/GlobalOptions.cs (using existing AddTo pattern)
- [x] T006 Update ProgressReporter to accept OutputFormat; all output methods become no-ops when Json in src/Spectra.CLI/Output/ProgressReporter.cs
- [x] T007 Update ResultPresenter to accept OutputFormat; all display methods become no-ops when Json in src/Spectra.CLI/Output/ResultPresenter.cs
- [x] T008 Update NextStepHints.Print to suppress when OutputFormat.Json in src/Spectra.CLI/Output/NextStepHints.cs
- [x] T009 [P] Update VerificationPresenter to accept OutputFormat; suppress when Json in src/Spectra.CLI/Output/VerificationPresenter.cs
- [x] T010 [P] Update AnalysisPresenter to accept OutputFormat; suppress when Json in src/Spectra.CLI/Output/AnalysisPresenter.cs

**Checkpoint**: Foundation ready - global options registered, output infrastructure JSON-aware

---

## Phase 3: User Story 1 - SKILL-Based Automated CLI Invocation (Priority: P1) MVP

**Goal**: All CLI commands produce structured JSON output when --output-format json is used, with no interactive prompts when all args are provided

**Independent Test**: Run any command with all required args and --output-format json, verify valid JSON output with no prompts

### Result Models for US1

- [x] T011 [P] [US1] Create GenerateResult record in src/Spectra.CLI/Results/GenerateResult.cs
- [x] T012 [P] [US1] Create AnalyzeCoverageResult record in src/Spectra.CLI/Results/AnalyzeCoverageResult.cs
- [x] T013 [P] [US1] Create ValidateResult record in src/Spectra.CLI/Results/ValidateResult.cs
- [x] T014 [P] [US1] Create DashboardResult record in src/Spectra.CLI/Results/DashboardResult.cs
- [x] T015 [P] [US1] Create ListResult record in src/Spectra.CLI/Results/ListResult.cs
- [x] T016 [P] [US1] Create ShowResult record in src/Spectra.CLI/Results/ShowResult.cs
- [x] T017 [P] [US1] Create InitResult record in src/Spectra.CLI/Results/InitResult.cs
- [x] T018 [P] [US1] Create DocsIndexResult record in src/Spectra.CLI/Results/DocsIndexResult.cs

### Command Handler Updates for US1

- [x] T019 [US1] Update GenerateCommand to remove local --no-interaction (use global) and pass OutputFormat to handler in src/Spectra.CLI/Commands/Generate/GenerateCommand.cs and GenerateHandler.cs
- [x] T020 [US1] Update GenerateHandler to build GenerateResult and output JSON when OutputFormat.Json in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T021 [US1] Update AnalyzeCommand to pass OutputFormat to handler and AnalyzeHandler to build AnalyzeCoverageResult for JSON mode in src/Spectra.CLI/Commands/Analyze/AnalyzeCommand.cs and AnalyzeHandler.cs
- [x] T022 [US1] Update UpdateCommand/UpdateHandler to pass OutputFormat, build result, output JSON in src/Spectra.CLI/Commands/Update/UpdateCommand.cs and UpdateHandler.cs
- [x] T023 [US1] Update ValidateCommand/ValidateHandler to pass OutputFormat, build ValidateResult, output JSON in src/Spectra.CLI/Commands/Validate/ValidateCommand.cs and ValidateHandler.cs
- [x] T024 [US1] Update InitCommand/InitHandler to pass OutputFormat, build InitResult, output JSON in src/Spectra.CLI/Commands/Init/InitCommand.cs and InitHandler.cs
- [x] T025 [US1] Update DashboardCommand/DashboardHandler to pass OutputFormat, build DashboardResult, output JSON in src/Spectra.CLI/Commands/Dashboard/DashboardCommand.cs and DashboardHandler.cs
- [x] T026 [US1] Update DocsIndexCommand/DocsIndexHandler to pass OutputFormat, build DocsIndexResult, output JSON in src/Spectra.CLI/Commands/Docs/DocsIndexCommand.cs and DocsIndexHandler.cs
- [x] T027 [US1] Update ListCommand/ListHandler to pass OutputFormat, build ListResult, output JSON in src/Spectra.CLI/Commands/List/ files
- [x] T028 [US1] Update ShowCommand/ShowHandler to pass OutputFormat, build ShowResult, output JSON in src/Spectra.CLI/Commands/Show/ files
- [x] T029 [US1] Update IndexCommand/IndexHandler to pass OutputFormat, output JSON in src/Spectra.CLI/Commands/Index/ files
- [x] T030 [US1] Update ConfigCommand and subcommand handlers to pass OutputFormat in src/Spectra.CLI/Commands/Config/ files

**Checkpoint**: All commands produce JSON output when --output-format json is used

---

## Phase 4: User Story 2 - CI Pipeline Integration (Priority: P2)

**Goal**: --no-interaction flag fails with exit code 3 when required args are missing, with structured error output

**Independent Test**: Run command with --no-interaction but missing required arg, verify exit code 3 and error listing missing args

### Implementation for US2

- [x] T031 [US2] Update GenerateHandler to check --no-interaction globally and return exit code 3 with ErrorResult when suite arg missing in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T032 [US2] Update UpdateHandler for --no-interaction missing arg check (suite required) in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T033 [US2] Update DashboardHandler for --no-interaction missing arg check (--output required) in src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs
- [x] T034 [US2] Update ShowHandler for --no-interaction missing arg check (test-id required) in src/Spectra.CLI/Commands/Show/ files
- [x] T035 [US2] Ensure all handlers output ErrorResult as JSON when --output-format json and command fails (runtime errors, not just missing args) across all handler files

**Checkpoint**: CI pipelines get exit code 3 for missing args, structured JSON errors for all failures

---

## Phase 5: User Story 3 - Human Interactive Usage Preserved (Priority: P2)

**Goal**: Default behavior (no new flags) is identical to current behavior — interactive prompts, human-formatted output

**Independent Test**: Run commands without --no-interaction and without --output-format, verify interactive prompts appear as before

### Implementation for US3

- [x] T036 [US3] Verify GenerateHandler interactive mode still works: suite selector, count selector, focus descriptor prompts when no suite arg in src/Spectra.CLI/Commands/Generate/GenerateHandler.cs
- [x] T037 [US3] Verify UpdateHandler interactive mode still works: suite selector prompts when no suite arg in src/Spectra.CLI/Commands/Update/UpdateHandler.cs
- [x] T038 [US3] Verify all presenters (ProgressReporter, ResultPresenter, VerificationPresenter, AnalysisPresenter) produce full Spectre.Console output when OutputFormat.Human in src/Spectra.CLI/Output/ files

**Checkpoint**: Existing human workflows work identically to before

---

## Phase 6: User Story 4 - Verbosity Control (Priority: P3)

**Goal**: --verbosity flag controls output detail; verbose logs go to stderr when JSON mode active

**Independent Test**: Run any command with --verbosity quiet, verify only final result appears

### Implementation for US4

- [x] T039 [US4] Ensure ProgressReporter respects verbosity levels: quiet suppresses spinners/progress, verbose adds step-by-step logging in src/Spectra.CLI/Output/ProgressReporter.cs
- [x] T040 [US4] When OutputFormat.Json and verbosity >= Detailed, send verbose logs to Console.Error (stderr) so stdout stays clean JSON in src/Spectra.CLI/Output/ProgressReporter.cs

**Checkpoint**: Verbosity control works across all commands, stderr logging in JSON mode

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Tests, validation, and cleanup

- [x] T041 [P] Add unit tests for JsonResultWriter serialization (camelCase, enums as strings, null omission) in tests/Spectra.CLI.Tests/Output/JsonResultWriterTests.cs
- [x] T042 [P] Add unit tests for CommandResult and ErrorResult construction in tests/Spectra.CLI.Tests/Results/CommandResultTests.cs
- [x] T043 [P] Add tests for --no-interaction exit code 3 behavior on GenerateHandler in tests/Spectra.CLI.Tests/Commands/
- [x] T044 Verify all existing tests still pass (dotnet test)
- [x] T045 Run quickstart.md validation — verify JSON output examples match actual output

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) - result models + handler updates
- **US2 (Phase 4)**: Depends on US1 (needs ErrorResult and JSON output infrastructure)
- **US3 (Phase 5)**: Can run after Foundational (Phase 2) - verification only
- **US4 (Phase 6)**: Can run after Foundational (Phase 2) - extends existing verbosity
- **Polish (Phase 7)**: Depends on all user stories being complete

### Within Each User Story

- Result models before handler updates
- Handler updates are sequential per command (same files)
- Different command handlers can be updated in parallel

### Parallel Opportunities

- T001-T004 (Setup): T001 and T002 are parallel (different files)
- T011-T018 (Result models): All 8 are parallel (different files)
- T009-T010 (Presenter updates): Both parallel (different files)
- T041-T043 (Tests): All parallel (different test files)

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T004)
2. Complete Phase 2: Foundational (T005-T010)
3. Complete Phase 3: User Story 1 (T011-T030)
4. **STOP and VALIDATE**: Run dotnet test, verify JSON output on all commands
5. Proceed to remaining stories

### Incremental Delivery

1. Setup + Foundational → Infrastructure ready
2. Add US1 → JSON output works → MVP
3. Add US2 → CI exit codes work → CI-ready
4. Add US3 → Backward compat verified
5. Add US4 → Verbosity polished
6. Polish → Tests + validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- The existing --verbosity global option already exists; US4 only extends its behavior
- The existing --no-interaction on GenerateCommand is replaced by the global option
- Keep AnalyzeCommand's --format flag for file output format (separate concern from --output-format)
