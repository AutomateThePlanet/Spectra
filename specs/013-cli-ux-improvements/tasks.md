# Tasks: CLI UX Improvements

**Input**: Design documents from `/specs/013-cli-ux-improvements/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included as the plan explicitly requires unit and integration tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No project initialization needed — all code lives within the existing `Spectra.CLI` project. This phase is empty.

**Checkpoint**: Existing project compiles and all 984 tests pass (`dotnet test`).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core utility that multiple user stories depend on — the NextStepHints helper is foundational because US1 needs it directly, and US2-US4 will also call it from their command handlers.

- [ ] T001 Create `NextStepHint` record and `HintSet` record in `src/Spectra.CLI/Output/NextStepHints.cs` — hint has `Command` (string) and `Comment` (string); HintSet has `OnSuccess` (NextStepHint[]) and `OnFailure` (NextStepHint[])
- [ ] T002 Create static `NextStepHints` class in `src/Spectra.CLI/Output/NextStepHints.cs` with `Dictionary<string, HintSet>` registry containing static hint mappings for all commands: `init`, `init-profile`, `ai generate` (success/failure), `ai update` (success/failure), `ai analyze` (success with/without auto-link), `dashboard` (success), `validate` (success/failure), `docs index` (success), `config` (success)
- [ ] T003 Add `Render(string commandName, bool success, VerbosityLevel verbosity, IAnsiConsole console)` method to `NextStepHints` in `src/Spectra.CLI/Output/NextStepHints.cs` — return early if `verbosity < VerbosityLevel.Normal`; print blank line, then `[dim]  Next steps:[/]` header, then each hint as `[dim]    {command}  # {comment}[/]`

**Checkpoint**: NextStepHints utility compiles. Hint registry contains entries for all commands. Render method respects verbosity.

---

## Phase 3: User Story 1 — Next-Step Hints After Commands (Priority: P1) 🎯 MVP

**Goal**: Every Spectra command displays 2-3 contextual next-step suggestions in dimmed text after completion. Suppressed when `--verbosity quiet`.

**Independent Test**: Run `spectra init` and verify hints appear. Run `spectra dashboard --verbosity quiet` and verify no hints appear.

### Tests for User Story 1

- [ ] T004 [P] [US1] Create `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs` — test that `Render("init", true, VerbosityLevel.Normal, console)` writes "Next steps:" and at least 2 hint lines to the test console
- [ ] T005 [P] [US1] Add test in `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs` — verify `Render("init", true, VerbosityLevel.Quiet, console)` writes nothing to the test console
- [ ] T006 [P] [US1] Add tests in `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs` — verify distinct hint sets exist for success vs failure outcomes of `ai generate`, `validate`, and `ai analyze`
- [ ] T007 [P] [US1] Add test in `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs` — verify `Render` with an unknown command name does not throw (graceful no-op)

### Implementation for User Story 1

- [ ] T008 [US1] Wire `NextStepHints.Render("init", success, verbosity, AnsiConsole.Console)` into `InitHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Init/InitHandler.cs` before returning exit code
- [ ] T009 [P] [US1] Wire `NextStepHints.Render("ai generate", success, verbosity, AnsiConsole.Console)` into `GenerateHandler.ExecuteDirectModeAsync()` and `ExecuteInteractiveModeAsync()` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` before returning exit code
- [ ] T010 [P] [US1] Wire `NextStepHints.Render("ai update", success, verbosity, AnsiConsole.Console)` into `UpdateHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` before returning exit code
- [ ] T011 [P] [US1] Wire `NextStepHints.Render("ai analyze", success, verbosity, AnsiConsole.Console)` into `AnalyzeHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` — use `"ai analyze --auto-link"` key when auto-link flag was used, `"ai analyze"` otherwise
- [ ] T012 [P] [US1] Wire `NextStepHints.Render("dashboard", success, verbosity, AnsiConsole.Console)` into `DashboardHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs` before returning exit code
- [ ] T013 [P] [US1] Wire `NextStepHints.Render("validate", success, verbosity, AnsiConsole.Console)` into the validate command handler before returning exit code
- [ ] T014 [P] [US1] Wire `NextStepHints.Render("docs index", success, verbosity, AnsiConsole.Console)` into `DocsIndexHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` before returning exit code
- [ ] T015 [US1] Run full test suite (`dotnet test`) and verify all existing tests still pass plus new NextStepHints tests pass

**Checkpoint**: All commands show contextual hints after completion. Hints are suppressed with `--verbosity quiet`. US1 is complete and independently testable.

---

## Phase 4: User Story 2 — Init: Configure Automation Directories (Priority: P2)

**Goal**: Users can configure automation code directories during `spectra init` and manage them afterward via `spectra config add-automation-dir`, `remove-automation-dir`, and `list-automation-dirs`.

**Independent Test**: Run `spectra config add-automation-dir ../tests` and verify the path appears in `spectra.config.json` under `coverage.automation_dirs`. Run `spectra config list-automation-dirs` to confirm.

### Tests for User Story 2

- [ ] T016 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Config/AutomationDirCommandTests.cs` — test `add-automation-dir` writes path to `coverage.automation_dirs` array in a temp `spectra.config.json`
- [ ] T017 [P] [US2] Add test in `AutomationDirCommandTests.cs` — verify `add-automation-dir` is idempotent (adding existing path prints notice, does not duplicate)
- [ ] T018 [P] [US2] Add test in `AutomationDirCommandTests.cs` — verify `remove-automation-dir` removes an existing path from the array
- [ ] T019 [P] [US2] Add test in `AutomationDirCommandTests.cs` — verify `remove-automation-dir` with nonexistent path returns warning
- [ ] T020 [P] [US2] Add test in `AutomationDirCommandTests.cs` — verify `list-automation-dirs` outputs all configured paths; verify empty state message when no dirs configured
- [ ] T021 [P] [US2] Add test in `AutomationDirCommandTests.cs` — verify all three subcommands return error when `spectra.config.json` does not exist

### Implementation for User Story 2

- [ ] T022 [P] [US2] Create `AddAutomationDirCommand` class in `src/Spectra.CLI/Commands/Config/AddAutomationDirCommand.cs` — subcommand `add-automation-dir` with required `path` argument; handler reads config JSON, navigates to `coverage.automation_dirs` array, checks for duplicate (idempotent no-op with notice), appends path, writes back
- [ ] T023 [P] [US2] Create `RemoveAutomationDirCommand` class in `src/Spectra.CLI/Commands/Config/RemoveAutomationDirCommand.cs` — subcommand `remove-automation-dir` with required `path` argument; handler reads config JSON, removes path from array (warning if not found), writes back
- [ ] T024 [P] [US2] Create `ListAutomationDirsCommand` class in `src/Spectra.CLI/Commands/Config/ListAutomationDirsCommand.cs` — subcommand `list-automation-dirs` with no arguments; handler reads config JSON, prints each path prefixed with `  - `, or "No automation directories configured" if empty/missing
- [ ] T025 [US2] Register all three subcommands in `ConfigCommand` constructor in `src/Spectra.CLI/Commands/Config/ConfigCommand.cs` — add via `AddCommand(new AddAutomationDirCommand())`, etc.
- [ ] T026 [US2] Add automation directory prompt to `InitHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Init/InitHandler.cs` — after docs/tests directory setup and before AI provider setup: `TextPrompt` asking for comma-separated paths with examples, skip if `isInteractive == false`, parse and trim paths, write to `coverage.automation_dirs` in config JSON
- [ ] T027 [US2] Wire `NextStepHints.Render("config", success, verbosity, AnsiConsole.Console)` into the three new config subcommand handlers
- [ ] T028 [US2] Run full test suite (`dotnet test`) and verify all existing + new tests pass

**Checkpoint**: Config subcommands work. Init prompts for automation dirs. Non-interactive mode skips the prompt. US2 is complete and independently testable.

---

## Phase 5: User Story 3 — Init: Configure Critic Model (Priority: P3)

**Goal**: Users can configure the grounding verification (critic) model during `spectra init` with a guided wizard. Critic pipeline is confirmed functional.

**Independent Test**: Run `spectra init`, select "Yes" for grounding verification, choose Google provider, verify `ai.critic` section in `spectra.config.json` has `enabled: true`, `provider: "google"`, `model: "gemini-2.0-flash"`.

### Tests for User Story 3

- [ ] T029 [P] [US3] Add test in `tests/Spectra.CLI.Tests/Commands/Init/InitCriticSetupTests.cs` — verify critic prompt writes correct `ai.critic` config for each provider option (google, anthropic, openai, same-as-primary)
- [ ] T030 [P] [US3] Add test in `InitCriticSetupTests.cs` — verify selecting "No" for grounding verification results in `ai.critic.enabled: false` or section omitted
- [ ] T031 [P] [US3] Add test in `InitCriticSetupTests.cs` — verify non-interactive mode skips critic prompt entirely

### Implementation for User Story 3

- [ ] T032 [US3] Add critic setup prompts to `InitHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Init/InitHandler.cs` — after AI provider setup: (1) `SelectionPrompt` "Enable grounding verification?" with Yes/No, (2) if Yes: `SelectionPrompt` for provider (google/anthropic/openai/same-as-primary) with descriptions, (3) `TextPrompt` for API key env var with default from `CriticConfig.GetDefaultApiKeyEnv()`, (4) check `Environment.GetEnvironmentVariable()` and warn if not set, (5) write `ai.critic` section to config JSON. Skip all if `isInteractive == false`.
- [ ] T033 [US3] Verify critic pipeline end-to-end: trace `GenerateHandler.ShouldVerify()` → `VerifyTestsAsync()` → `CriticFactory.TryCreate()` → `CopilotCritic.VerifyTestAsync()` with a valid critic config. Confirm grounding verdicts appear in console output. Document findings and fix any issues found in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` or `src/Spectra.CLI/Agent/Critic/CriticFactory.cs`
- [ ] T034 [US3] Run full test suite (`dotnet test`) and verify all existing + new tests pass

**Checkpoint**: Init wizard includes critic configuration. Critic pipeline confirmed functional during generation. US3 is complete and independently testable.

---

## Phase 6: User Story 4 — Interactive Mode: Continue to Other Suites (Priority: P4)

**Goal**: After completing test generation in interactive mode, users can generate more for the same suite, switch to a different suite, create a new suite, or exit — without restarting the CLI.

**Independent Test**: Run `spectra ai generate` in interactive mode, complete generation for one suite, verify the continuation menu appears with all four options. Select "Switch to a different suite", pick another suite, verify generation flow restarts.

### Tests for User Story 4

- [ ] T035 [P] [US4] Create `tests/Spectra.CLI.Tests/Interactive/ContinuationSelectorTests.cs` — test that `Prompt()` returns `ContinuationResult` with `Action = Exit` when user selects "Done — exit"
- [ ] T036 [P] [US4] Add test in `ContinuationSelectorTests.cs` — verify `Prompt()` returns `ContinuationResult` with `Action = SwitchSuite` and populated `SuiteName`/`SuitePath` when user selects "Switch to a different suite" and picks a suite
- [ ] T037 [P] [US4] Add test in `ContinuationSelectorTests.cs` — verify `Prompt()` returns `ContinuationResult` with `Action = CreateSuite` and populated `SuiteName` when user enters a valid new suite name
- [ ] T038 [P] [US4] Add test in `ContinuationSelectorTests.cs` — verify creating a suite with a name that already exists shows warning and returns `Action = GenerateMore` with existing suite

### Implementation for User Story 4

- [ ] T039 [US4] Create `ContinuationAction` enum (`GenerateMore`, `SwitchSuite`, `CreateSuite`, `Exit`) and `ContinuationResult` record (`Action`, `SuiteName?`, `SuitePath?`) in `src/Spectra.CLI/Interactive/ContinuationSelector.cs`
- [ ] T040 [US4] Create `ContinuationSelector` class in `src/Spectra.CLI/Interactive/ContinuationSelector.cs` — inject `IAnsiConsole` (pattern from `GapSelector`); `Prompt(string currentSuite, IReadOnlyList<SuiteInfo> availableSuites)` method: `SelectionPrompt` with 4 choices; if SwitchSuite → `SuiteSelector`-style suite picker; if CreateSuite → `TextPrompt` for name with validation (no special chars, check if exists → warn and offer existing), create directory; if GenerateMore → return current suite; if Exit → return Exit action
- [ ] T041 [US4] Modify `GenerateHandler.ExecuteInteractiveModeAsync()` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` — wrap the existing interactive flow (suite selection through session completion) in a `do { ... } while (continuation.Action != ContinuationAction.Exit)` outer loop. After `session.Complete()`, call `ContinuationSelector.Prompt()`. On GenerateMore: create new session with same suite, re-enter loop. On SwitchSuite: create new session with selected suite (skip suite selection step), re-enter. On CreateSuite: create new session with new suite, re-enter. On Exit: break.
- [ ] T042 [US4] Run full test suite (`dotnet test`) and verify all existing + new tests pass

**Checkpoint**: Interactive mode loops with continuation menu. All four options work correctly. US4 is complete and independently testable.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all user stories.

- [ ] T043 Verify hint text content is accurate and helpful for each command — review all entries in the `NextStepHints` registry in `src/Spectra.CLI/Output/NextStepHints.cs` against the examples in spec.md
- [ ] T044 Run full test suite (`dotnet test`) — confirm all tests pass (existing 984 + new tests)
- [ ] T045 Update `CLAUDE.md` Recent Changes section with 013-cli-ux-improvements summary

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Empty — existing project is ready
- **Foundational (Phase 2)**: T001-T003 must complete before any user story — NextStepHints utility is used by all stories
- **US1 (Phase 3)**: Depends on Phase 2 — wires hints into all command handlers
- **US2 (Phase 4)**: Depends on Phase 2 — new config subcommands + init prompt
- **US3 (Phase 5)**: Depends on Phase 2 — init critic prompt + pipeline verification
- **US4 (Phase 6)**: Depends on Phase 2 — continuation selector + handler modification
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Depends on Phase 2 only — no dependencies on other stories
- **User Story 2 (P2)**: Depends on Phase 2 only — no dependencies on other stories
- **User Story 3 (P3)**: Depends on Phase 2 only — no dependencies on other stories
- **User Story 4 (P4)**: Depends on Phase 2 only — no dependencies on other stories

All four user stories are fully independent and can be implemented in parallel after Phase 2.

### Within Each User Story

- Tests should be written first (T004-T007, T016-T021, T029-T031, T035-T038)
- Implementation follows tests
- Integration/wiring tasks last within each story
- Full test suite run at story completion

### Parallel Opportunities

- **Phase 2**: T001-T003 are sequential (same file)
- **Phase 3 (US1)**: T004-T007 tests in parallel; T009-T014 handler wiring in parallel (different files)
- **Phase 4 (US2)**: T016-T021 tests in parallel; T022-T024 command classes in parallel (different files)
- **Phase 5 (US3)**: T029-T031 tests in parallel
- **Phase 6 (US4)**: T035-T038 tests in parallel
- **Cross-story**: US1, US2, US3, US4 can all run in parallel after Phase 2

---

## Parallel Example: User Story 2

```text
# Launch all tests for US2 together:
Task T016: "Test add-automation-dir writes path to config"
Task T017: "Test add-automation-dir idempotent behavior"
Task T018: "Test remove-automation-dir removes path"
Task T019: "Test remove-automation-dir warning on nonexistent"
Task T020: "Test list-automation-dirs output"
Task T021: "Test error when config missing"

# Launch all command implementations together:
Task T022: "Create AddAutomationDirCommand"
Task T023: "Create RemoveAutomationDirCommand"
Task T024: "Create ListAutomationDirsCommand"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001-T003) — NextStepHints utility
2. Complete Phase 3: User Story 1 (T004-T015) — Wire hints into all handlers
3. **STOP and VALIDATE**: Run any command and verify hints appear. Run with `--verbosity quiet` and verify suppression.
4. Deploy/demo if ready — immediate user value from every command invocation.

### Incremental Delivery

1. Phase 2 → NextStepHints utility ready
2. US1 → Hints on all commands (MVP!)
3. US2 → Automation dir config during init + config subcommands
4. US3 → Critic model config during init + pipeline verification
5. US4 → Interactive continuation menu
6. Polish → Final validation and CLAUDE.md update

### Parallel Team Strategy

With multiple developers after Phase 2:
- Developer A: User Story 1 (hints wiring — touches many files but only adds one line each)
- Developer B: User Story 2 (config subcommands — self-contained new files)
- Developer C: User Story 3 (init critic prompt — contained in InitHandler)
- Developer D: User Story 4 (continuation selector — contained in Interactive/ + GenerateHandler)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Design decision D1: Use `--verbosity quiet` instead of new `--quiet` flag
- Design decision D3: Continuation menu is handler-level outer loop, not new session states
- Design decision D4: Hints are static dictionary, no file I/O
- Design decision D5: Config modification via JsonDocument read-modify-write
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
