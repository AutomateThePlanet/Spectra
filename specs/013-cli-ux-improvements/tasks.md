# Tasks: CLI UX Improvements

**Input**: Design documents from `/specs/013-cli-ux-improvements/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are included — the project constitution requires tests for all public APIs and critical paths.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the shared hint helper used by all user stories

- [x] T001 Create `HintContext` record in `src/Spectra.CLI/Output/NextStepHints.cs` with fields: `hasAutoLink` (bool), `hasGaps` (bool), `suiteCount` (int), `errorCount` (int), `outputPath` (string?), `suiteName` (string?)
- [x] T002 Create `NextStepHints` static class in `src/Spectra.CLI/Output/NextStepHints.cs` with method `Print(string commandName, bool success, VerbosityLevel verbosity, HintContext? context = null)` — checks `verbosity >= Normal` and `!Console.IsOutputRedirected` before printing; outputs blank line then "Next steps:" header then 2-3 indented dimmed hints via `AnsiConsole.MarkupLine("[grey]    {hint}[/]")`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define hint content for all commands — MUST complete before integrating into handlers

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Implement hint content for `init` command in `NextStepHints` — on success: suggest `spectra ai generate` and `spectra init-profile`
- [x] T004 Implement hint content for `generate` command in `NextStepHints` — on success: suggest `spectra ai analyze --coverage` and `spectra ai generate` (interactive mode); include `suiteName` context
- [x] T005 Implement hint content for `analyze` command in `NextStepHints` — on success: suggest `spectra dashboard --output ./site`; if `!hasAutoLink` suggest `spectra ai analyze --coverage --auto-link`; if `hasGaps` suggest `spectra ai generate`
- [x] T006 Implement hint content for `dashboard` command in `NextStepHints` — on success: suggest `Open {outputPath}/index.html in your browser` and reference `docs/deployment/cloudflare-pages-setup.md`
- [x] T007 Implement hint content for `validate` command in `NextStepHints` — on success: suggest `spectra ai generate` and `spectra index`; on failure (`errorCount > 0`): suggest fixing errors then `spectra validate`
- [x] T008 Implement hint content for `docs-index` command in `NextStepHints` — on success: suggest `spectra ai generate`
- [x] T009 Implement hint content for `index` command in `NextStepHints` — on success: suggest `spectra validate` and `spectra ai generate`
- [x] T010 [P] Write unit tests for `NextStepHints` in `tests/Spectra.CLI.Tests/Output/NextStepHintsTests.cs` — test: quiet verbosity suppresses output, each command returns correct hints, context-aware hints (auto-link, gaps, errors), redirected output suppresses hints

**Checkpoint**: Hint helper is complete and tested — ready to integrate into handlers

---

## Phase 3: User Story 1 — Next-Step Hints (Priority: P1) 🎯 MVP

**Goal**: Every supported command displays contextual next-step hints after completion.

**Independent Test**: Run `spectra validate`, `spectra dashboard --output ./site`, and `spectra ai analyze --coverage`. Verify dimmed hint text appears after each. Run with `--verbosity quiet` and verify hints are suppressed.

### Implementation for User Story 1

- [x] T011 [US1] Add `NextStepHints.Print("init", true, verbosity)` call at end of successful path in `src/Spectra.CLI/Commands/Init/InitHandler.cs`
- [x] T012 [US1] Add `NextStepHints.Print("generate", true, verbosity, context)` call at end of `ExecuteDirectModeAsync` and `ExecuteInteractiveModeAsync` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` with `suiteName` in context
- [x] T013 [P] [US1] Add `NextStepHints.Print("analyze", true, verbosity, context)` call at end of `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` with `hasAutoLink` and `hasGaps` context
- [x] T014 [P] [US1] Add `NextStepHints.Print("dashboard", true, verbosity, context)` call at end of `src/Spectra.CLI/Commands/Dashboard/DashboardHandler.cs` with `outputPath` context
- [x] T015 [P] [US1] Add `NextStepHints.Print("validate", success, verbosity, context)` call at end of `src/Spectra.CLI/Commands/Validate/ValidateHandler.cs` with `errorCount` context
- [x] T016 [P] [US1] Add `NextStepHints.Print("docs-index", true, verbosity)` call at end of `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`
- [x] T017 [P] [US1] Add `NextStepHints.Print("index", true, verbosity)` call at end of `src/Spectra.CLI/Commands/Index/IndexHandler.cs`
- [x] T018 [US1] Verify hint suppression: confirm `--verbosity quiet` and piped output (`Console.IsOutputRedirected`) both suppress hints across all handlers

**Checkpoint**: User Story 1 complete — all commands show relevant hints

---

## Phase 4: User Story 2 — Init: Configure Automation Directories (Priority: P2)

**Goal**: Users configure automation code directories during init and manage them with config subcommands.

**Independent Test**: Run `spectra init`, enter comma-separated paths at the automation prompt, verify `coverage.automation_dirs` is updated. Run `spectra config add-automation-dir`, `list-automation-dirs`, `remove-automation-dir` and verify each works.

### Implementation for User Story 2

- [x] T019 [US2] Add automation directory prompt to `InitHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Init/InitHandler.cs` — after existing setup, before doc index build; use `AnsiConsole.Prompt(new TextPrompt<string>())` with allow-empty; parse comma-separated input; write to config `coverage.automation_dirs`; skip when not interactive
- [x] T020 [US2] Implement config JSON modification helper — create a private method in `ConfigHandler` (or a small utility) in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` that reads `spectra.config.json` as `JsonNode`, modifies a specific array field, and writes back preserving structure
- [x] T021 [US2] Implement `AddAutomationDirAsync(string path)` in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` — read config, check for duplicate, append to `coverage.automation_dirs`, write back, print confirmation
- [x] T022 [US2] Implement `RemoveAutomationDirAsync(string path)` in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` — read config, remove from `coverage.automation_dirs`, warn if not found, write back, print confirmation
- [x] T023 [US2] Implement `ListAutomationDirsAsync()` in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs` — read config, print each dir with `[exists]` or `[missing]` indicator based on `Directory.Exists()`
- [x] T024 [US2] Register `add-automation-dir`, `remove-automation-dir`, `list-automation-dirs` subcommands in `src/Spectra.CLI/Commands/Config/ConfigCommand.cs` using System.CommandLine `Command` with appropriate arguments and handlers
- [x] T025 [P] [US2] Write tests for automation dir init prompt in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs` — verify config file updated with entered dirs, verify defaults when skipped
- [x] T026 [P] [US2] Write tests for config subcommands in `tests/Spectra.CLI.Tests/Commands/ConfigCommandTests.cs` — add (success + duplicate), remove (success + not found), list (with existing + missing dirs)

**Checkpoint**: User Story 2 complete — automation directories configurable via init and config commands

---

## Phase 5: User Story 3 — Init: Configure Critic Model (Priority: P2)

**Goal**: Users enable grounding verification during init with guided provider selection.

**Independent Test**: Run `spectra init`, select "Yes" for grounding verification, pick a provider, verify `ai.critic` section in spectra.config.json has `enabled: true` with correct provider/model/api_key_env.

### Implementation for User Story 3

- [x] T027 [US3] Add critic configuration prompt to `InitHandler.ExecuteAsync()` in `src/Spectra.CLI/Commands/Init/InitHandler.cs` — after AI provider setup; show "Enable grounding verification?" prompt; skip if not interactive or if user skipped AI provider
- [x] T028 [US3] Implement critic provider selection in `InitHandler` — use `SelectionPrompt<string>` with options: google (recommended), anthropic, openai, same as primary; map selection to `CriticConfig` fields using existing `GetEffectiveModel()` and `GetDefaultApiKeyEnv()` methods
- [x] T029 [US3] Implement critic API key env var prompt in `InitHandler` — use `TextPrompt<string>` with default value from `CriticConfig.GetDefaultApiKeyEnv()`; handle "Same as primary" case (copy provider/model, skip API key prompt)
- [x] T030 [US3] Write `ai.critic` section to config in `InitHandler` — modify the config JSON to add/update the critic block with `enabled`, `provider`, `model`, `api_key_env`; print confirmation message with provider/model and `--skip-critic` reminder
- [x] T031 [P] [US3] Write tests for critic init prompt in `tests/Spectra.CLI.Tests/Commands/InitCommandTests.cs` — verify config updated with critic settings when enabled, verify critic absent/disabled when skipped, verify "same as primary" copies provider/model

**Checkpoint**: User Story 3 complete — critic model configurable through init

---

## Phase 6: User Story 4 — Interactive Mode Continuation (Priority: P3)

**Goal**: After generating tests for a suite, users can continue to other suites without restarting.

**Independent Test**: Run `spectra ai generate` in interactive mode, complete one suite, verify continuation menu appears, select "Switch to a different suite", verify generation starts for the new suite, select "Done", verify session summary prints.

### Implementation for User Story 4

- [x] T032 [US4] Wrap the existing interactive generation flow in `GenerateHandler.ExecuteInteractiveModeAsync()` in `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` in an outer `while (true)` loop with a continuation menu after session completion
- [x] T033 [US4] Implement continuation menu in `GenerateHandler` — use `SelectionPrompt<string>` with 4 options: "Generate more for {suite}", "Switch to a different suite", "Create a new suite", "Done — exit"; display remaining gaps before menu if any
- [x] T034 [US4] Implement "Generate more" option — reset the `InteractiveSession`, restart inner loop focusing on remaining gaps for the same suite
- [x] T035 [US4] Implement "Switch suite" option — show suite list via `SelectionPrompt` with test counts per suite, load selected suite's existing tests, restart generation flow
- [x] T036 [US4] Implement "Create new suite" option — prompt for suite name via `TextPrompt<string>`, create directory, restart generation flow for new empty suite
- [x] T037 [US4] Implement session tracking and summary — track list of suites worked on and total tests generated across all suites; on "Done — exit", print summary: "Session: {n} suites, {m} tests generated"
- [x] T038 [US4] Ensure `--no-interaction` skips continuation — verify existing behavior preserved: direct mode and `--no-interaction` exit after first suite without showing menu

**Checkpoint**: User Story 4 complete — multi-suite interactive sessions work

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, validation, and final quality

- [x] T039 Update CLAUDE.md Recent Changes — add 013-cli-ux-improvements entry documenting NextStepHints, automation dir config, critic init, interactive continuation
- [x] T040 Run quickstart.md validation — execute the quickstart scenarios end-to-end to verify all features work as documented
- [x] T041 Verify all handlers pass `--verbosity quiet` suppression — run each command with `-v quiet` and verify zero hint output

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T001-T002) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2)
- **User Story 2 (Phase 4)**: Depends on Phase 1 only — can start in parallel with Phase 2
- **User Story 3 (Phase 5)**: Depends on Phase 1 only — can start in parallel with Phase 2
- **User Story 4 (Phase 6)**: No dependency on hint infrastructure — can start after Phase 1
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Depends on Foundational (hint content must exist before integrating)
- **User Story 2 (P2)**: Independent — no dependency on hints or other stories
- **User Story 3 (P2)**: Independent — no dependency on hints or other stories
- **User Story 4 (P3)**: Independent — no dependency on hints (hints for generate are added in US1)

### Within Each User Story

- Infrastructure/models before handler integration
- Handler changes before tests
- Core implementation before polish

### Parallel Opportunities

- T013-T017 can all run in parallel (different handler files)
- T025, T026 can run in parallel (different test files)
- T031 can run in parallel with T025/T026
- US2, US3, US4 can all run in parallel after Phase 1 completes
- US1 must wait for Phase 2 (hint content) but US2/US3/US4 do not

---

## Parallel Example: User Story 1

```bash
# After Phase 2 completes, these can all run in parallel (different handler files):
Task T013: "Add hints to AnalyzeHandler.cs"
Task T014: "Add hints to DashboardHandler.cs"
Task T015: "Add hints to ValidateHandler.cs"
Task T016: "Add hints to DocsIndexHandler.cs"
Task T017: "Add hints to IndexHandler.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational — hint content (T003-T010)
3. Complete Phase 3: User Story 1 — integrate hints into all handlers (T011-T018)
4. **STOP and VALIDATE**: Run several commands and verify hints appear
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Hint helper ready
2. Add User Story 1 → Hints work everywhere → Deploy (MVP!)
3. Add User Story 2 → Automation dir config works → Deploy
4. Add User Story 3 → Critic init works → Deploy
5. Add User Story 4 → Interactive continuation works → Deploy
6. Polish → Documentation, final validation

### Recommended Order (Solo Developer)

P1 (hints) → P2 (automation dirs) → P2 (critic init) → P3 (interactive continuation) → Polish

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Total: 41 tasks across 7 phases
