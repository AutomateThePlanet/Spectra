---

description: "Task list for Spec 040 — Token Usage Tracking & Model/Provider Logging"
---

# Tasks: Token Usage Tracking & Model/Provider Logging

**Input**: Design documents from `/specs/040-token-usage-tracking/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are REQUESTED in spec.md ("Test Plan" section). Test tasks are included for each user story and the cross-cutting tracker/cost services.

**Organization**: Tasks are grouped by user story. P1 stories (US1/US2/US3) are mostly independent and could be parallelized after foundational phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file, safe to run in parallel
- **[Story]**: User story label (US1/US2/US3/US4) or unlabeled for setup/foundation/polish
- All paths are absolute from repo root: `C:/SourceCode/Spectra/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new project scaffolding required — feature lives in existing Spectra.Core / Spectra.CLI projects.

- [X] T001 Verify branch `040-token-usage-tracking` is checked out and `dotnet build` succeeds against current `main` baseline (no source changes).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types and config plumbing that every user story below depends on. **No user story work can begin until this phase is complete.**

- [X] T002 [P] Create `DebugConfig` model at `src/Spectra.Core/Models/Config/DebugConfig.cs` with `Enabled` (bool, default false) and `LogFile` (string, default `.spectra-debug.log`) per data-model.md.
- [X] T003 Add `Debug` property to `src/Spectra.Core/Models/Config/SpectraConfig.cs` (`[JsonPropertyName("debug")] public DebugConfig Debug { get; init; } = new();`). Remove obsolete `AiConfig.DebugLogEnabled` field if present (keep deserializer tolerant of stray field).
- [X] T004 [P] Create `TokenUsage` record at `src/Spectra.Core/Models/TokenUsage.cs` with `PromptTokens`/`CompletionTokens` and computed `TotalTokens`. Delete the old record from `src/Spectra.CLI/Agent/IAgentRuntime.cs` and update its `using` and the `TokenUsage = null` assignment in `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs` to use the Core type.
- [X] T005 [P] Create `PhaseUsage` record at `src/Spectra.CLI/Services/PhaseUsage.cs` per data-model.md.
- [X] T006 Create `TokenUsageTracker` class at `src/Spectra.CLI/Services/TokenUsageTracker.cs` with thread-safe `Record(phase, model, provider, tokensIn?, tokensOut?, elapsed)`, `GetSummary()` aggregating by `(phase, model, provider)`, and `GetTotal()`.
- [X] T007 [P] Create `CostEstimator` static class at `src/Spectra.CLI/Services/CostEstimator.cs` with the hardcoded rate dictionary from data-model.md and the `Estimate(phases) → (decimal? amount, string display)` method implementing FR-026 to FR-029 (case-insensitive lookup, github-models special case, unknown-model fallback).
- [X] T008 Modify `src/Spectra.CLI/Infrastructure/DebugLogger.cs`: change default `Enabled` from `true` to `false`; add `AppendAi(component, message, model, provider, tokensIn, tokensOut)` overload that appends `model=… provider=… tokens_in=… tokens_out=…` (with `?` placeholders when null) per `contracts/debug-log-format.md`. Keep existing `Append(component, message)` for non-AI lines.
- [X] T009 Wire `DebugLogger.Enabled` initialization at CLI startup: in `src/Spectra.CLI/Program.cs` (or the existing config-load entrypoint), set `DebugLogger.Enabled = config.Debug.Enabled || verbosity == "diagnostic";` after parsing config and global flags. Replace any prior code that read `config.Ai.DebugLogEnabled`.
- [X] T010 Add `diagnostic` value to the verbosity enum/parser used by global CLI options (search `--verbosity` parsing in `src/Spectra.CLI/`); accept `quiet|normal|diagnostic` with `normal` as default.

**Checkpoint**: Foundation ready — all user story phases below can begin.

---

## Phase 3: User Story 1 — See cost and token usage after a generation run (Priority: P1) 🎯 MVP

**Goal**: After every `spectra ai generate` and `spectra ai update` run, the user sees a Run Summary panel with per-phase token counts, model/provider, elapsed AI time, and a cost estimate (or "included in Copilot plan"). JSON output and `.spectra-result.json` carry the same data.

**Independent Test**: Run `spectra ai generate <suite> --count 4` against a configured project. Confirm the Run Summary panel renders before the final status line and includes a non-empty TOTAL row plus a cost line. Run again with `--output-format json` and confirm the JSON contains `run_summary` and `token_usage`.

### Tests for User Story 1

- [X] T011 [P] [US1] Add `tests/Spectra.CLI.Tests/Services/TokenUsageTrackerTests.cs` covering: `Record_SingleCall_TracksCorrectly`, `Record_MultipleCalls_AggregatesByPhase`, `Record_DifferentModels_SeparateEntries`, `GetTotal_SumsAllPhases`, `ThreadSafety_ConcurrentRecords` (parallel `Record` calls under `Parallel.For`), `Record_NullTokens_StillIncrementsCalls`.
- [X] T012 [P] [US1] Add `tests/Spectra.CLI.Tests/Services/CostEstimatorTests.cs` covering: `EstimateCost_KnownModel_ReturnsValue` (e.g. gpt-4.1-mini on azure-openai), `EstimateCost_UnknownModel_ReturnsNull`, `EstimateCost_GitHubModels_ReturnsNull`, `EstimateCost_MultiplePhases_SumsCorrectly`, `EstimateCost_CaseInsensitiveModelLookup`.
- [X] T013 [P] [US1] Add `tests/Spectra.CLI.Tests/Output/RunSummaryPresenterTests.cs` covering: `Render_FormatsTable` (snapshot or contains-checks for column headers), `Render_QuietVerbosity_Suppressed`, `Render_GenerateContext_IncludesAllFields`, `Render_UpdateContext_IncludesAllFields`, `Render_GitHubModels_ShowsCopilotPlanMessage`, `Render_AzureOpenAi_ShowsDollarAmount`.
- [X] T014 [P] [US1] Add `tests/Spectra.CLI.Tests/Results/GenerateResultTokenUsageTests.cs` covering JSON round-trip serialization of `GenerateResult.RunSummary` and `GenerateResult.TokenUsage` matching `contracts/token-usage.schema.json` field names (`tokens_in`, `tokens_out`, `total_tokens`, `elapsed_seconds`, `estimated_cost_usd`, `cost_display`).

### Implementation for User Story 1

- [X] T015 [P] [US1] Create `RunSummary` DTO at `src/Spectra.CLI/Results/RunSummary.cs` with the generate-specific and update-specific nullable fields plus `DurationSeconds` per data-model.md, with `JsonPropertyName` snake_case attributes.
- [X] T016 [P] [US1] Create `TokenUsageReport` and `PhaseUsageDto` at `src/Spectra.CLI/Results/TokenUsageReport.cs` with snake_case JSON property names matching `contracts/token-usage.schema.json`.
- [X] T017 [US1] Create `TokenUsageReportBuilder` static helper at `src/Spectra.CLI/Services/TokenUsageReportBuilder.cs` that takes a `TokenUsageTracker` plus optional provider/model context and returns a `TokenUsageReport` (calls `CostEstimator.Estimate`).
- [X] T018 [US1] Create `RunSummaryPresenter` at `src/Spectra.CLI/Output/RunSummaryPresenter.cs` using Spectre.Console `Panel` + key/value `Grid` (run-context) + `Table` (per-phase rows + TOTAL) + final `Markup` line ("Estimated cost: …"). Honor verbosity (`quiet` → no-op, `diagnostic`/`normal` → render). Match the style of the existing technique-breakdown table (search for `TechniqueBreakdownPresenter` or equivalent).
- [X] T019 [US1] Modify `src/Spectra.CLI/Results/GenerateResult.cs` and `src/Spectra.CLI/Results/UpdateResult.cs` to add `RunSummary? RunSummary` and `TokenUsageReport? TokenUsage` properties with snake_case JSON attributes.
- [X] T020 [US1] Modify `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`: accept a `TokenUsageTracker` (constructor injection or method parameter), wrap each AI call with `Stopwatch`, after each call read `usage` from the Copilot SDK response and call `tracker.Record("analysis", model, provider, promptTokens, completionTokens, sw.Elapsed)`. Replace existing `DebugLogger.Append` AI lines with `DebugLogger.AppendAi(...)`.
- [X] T021 [US1] Modify `src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs`: same treatment per batch — wrap each batch with `Stopwatch`, call `tracker.Record("generation", …)`, switch debug log lines (`BATCH OK`, `BATCH TIMEOUT`) to `AppendAi`.
- [X] T022 [US1] Modify `src/Spectra.CLI/Agent/Copilot/GroundingAgent.cs` (critic): wrap each `VerifyAsync` call with `Stopwatch`, call `tracker.Record("critic", criticModel, criticProvider, …)`, switch `CRITIC OK`/`CRITIC ERROR`/`CRITIC TIMEOUT` debug log lines to `AppendAi`.
- [X] T023 [US1] Modify `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`: instantiate a `TokenUsageTracker` at handler start, pass it down to `BehaviorAnalyzer`, `GenerationAgent`, `GroundingAgent`. After all phases complete (success or failure), build the `RunSummary` (documents processed, behaviors identified, tests generated + verdict breakdown, batch size + count, wall-clock duration via `Stopwatch` from handler entry) and `TokenUsageReport` and attach to `GenerateResult`. Render via `RunSummaryPresenter` immediately before the final status line (skip when `--output-format json`).
- [X] T024 [US1] In `GenerateHandler`, ensure the JSON code path (`--output-format json`) writes `run_summary` and `token_usage` into the structured result via `JsonResultWriter` and suppresses the terminal panel.

**Checkpoint**: User Story 1 fully functional — terminal Run Summary, JSON output, and cost message all working for `spectra ai generate`. Update command and progress page still pending.

---

## Phase 4: User Story 2 — Identify which model handled each AI call from the debug log (Priority: P1)

**Goal**: Every AI debug log line ends with `model=<name> provider=<name> tokens_in=<n> tokens_out=<n>`, with `?` placeholders when usage is missing. Non-AI lines are unchanged.

**Independent Test**: With `debug.enabled: true`, run any AI command and confirm `grep "BATCH OK\|CRITIC OK\|ANALYSIS OK\|UPDATE OK\|CRITERIA OK" .spectra-debug.log` shows every line ending with the four new key=value pairs. Confirm `grep "TESTIMIZE" .spectra-debug.log` shows lines unchanged.

### Tests for User Story 2

- [X] T025 [P] [US2] Add `tests/Spectra.CLI.Tests/Infrastructure/DebugLogModelProviderTests.cs` covering: `AppendAi_KnownTokens_LineEndsWithAllFields`, `AppendAi_NullTokens_RendersQuestionMarks`, `AppendAi_Disabled_NoFileWritten`, `Append_NonAiMessage_UnchangedFormat`.

### Implementation for User Story 2

- [X] T026 [US2] Verify (and complete if missing from T020-T022) that `BehaviorAnalyzer`, `GenerationAgent`, `GroundingAgent` each call `DebugLogger.AppendAi(...)` instead of plain `Append(...)` for: ANALYSIS OK, ANALYSIS PARSE_FAIL, BATCH OK, BATCH TIMEOUT, CRITIC OK, CRITIC ERROR, CRITIC TIMEOUT.
- [X] T027 [P] [US2] Modify `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs` (and `RequirementsExtractor.cs` if it still drives any AI calls): wrap calls with `Stopwatch`, call `tracker.Record("criteria", …)`, switch debug log lines to `AppendAi`.
- [X] T028 [US2] Audit `src/Spectra.CLI/` for any remaining `DebugLogger.Append(` calls that emit AI lines (search the codebase) and convert them to `AppendAi`. Leave testimize/file-IO calls as `Append`.

**Checkpoint**: Every AI line in `.spectra-debug.log` is now self-describing.

---

## Phase 5: User Story 3 — Opt in/out of debug logging without code changes (Priority: P1)

**Goal**: Default Spectra runs produce no `.spectra-debug.log`. `debug.enabled: true` in config writes it. `--verbosity diagnostic` force-enables it for one run.

**Independent Test**: With no `debug` section in config, run any AI command and confirm `.spectra-debug.log` is not created. Set `debug.enabled: true`, run again, confirm file exists. Set back to false, run with `--verbosity diagnostic`, confirm file exists for that run.

### Tests for User Story 3

- [X] T029 [P] [US3] Add `tests/Spectra.Core.Tests/Config/DebugConfigTests.cs` covering: `Default_DisabledByDefault`, `Default_LogFilePath`, `Deserialization_FromJson_RoundTrip`, `Deserialization_MissingDebugSection_DefaultsToDisabled` (full SpectraConfig parse without `debug` block), `Deserialization_PartialDebugSection_FillsDefaults`.
- [X] T030 [P] [US3] Add `tests/Spectra.CLI.Tests/Infrastructure/DebugLoggerTests.cs` covering: `Disabled_NoFileWritten` (tempdir + Enabled=false + Append/AppendAi → assert file absent), `Enabled_FileWritten`, `DiagnosticVerbosity_OverridesConfig` (config disabled + setter set true → file created).

### Implementation for User Story 3

- [X] T031 [US3] Update the `spectra init` embedded config template (search for the JSON template in `src/Spectra.CLI/Commands/` — likely `Init` command or `Config` namespace) to include a `"debug": { "enabled": false, "log_file": ".spectra-debug.log" }` block.
- [X] T032 [US3] Verify the `Program.cs` wiring added in T009 correctly merges config + verbosity flag for the diagnostic override; add a unit/integration test if not covered by T030.
- [X] T033 [P] [US3] Update `docs/configuration.md` with the new `debug` section reference (document `enabled`, `log_file`, default values, and the `--verbosity diagnostic` override).

**Checkpoint**: Debug log is fully opt-in and override-able.

---

## Phase 6: User Story 4 — SKILLs and progress page report token usage (Priority: P2)

**Goal**: `.spectra-result.json` contains `run_summary` and `token_usage` (live during run, final after completion). `.spectra-progress.html` renders a Token Usage section that updates as the file is rewritten.

**Independent Test**: Start a long generation, mid-run open `.spectra-progress.html` and confirm Token Usage section shows non-zero running totals. After completion, parse `.spectra-result.json` and confirm `run_summary` + `token_usage.phases[]` + `token_usage.total` + `token_usage.estimated_cost_usd` are all present.

### Tests for User Story 4

- [X] T034 [P] [US4] Extend `tests/Spectra.CLI.Tests/Output/` (or wherever ProgressManager tests live — search `ProgressManager`) with a test verifying `.spectra-result.json` contains `run_summary` and `token_usage` after a simulated generate run with a fake tracker.

### Implementation for User Story 4

- [X] T035 [US4] Modify `src/Spectra.CLI/Output/ProgressManager.cs` (or the equivalent that writes `.spectra-result.json`) to accept a `TokenUsageReport` and serialize it under the `token_usage` key on every flush; also include `run_summary` once it's known. Add a `WriteTokenUsage(TokenUsageReport report)` helper invoked from handlers after each `Record(...)` (or at phase boundaries — see plan R9).
- [X] T036 [US4] Update `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` (and any update sub-handler) the same way as T023: instantiate tracker, pass to update AI runtime, build `RunSummary` (tests scanned, tests updated + classification breakdown, tests unchanged, chunks, duration), build `TokenUsageReport`, attach to `UpdateResult`, render via `RunSummaryPresenter`, write to `.spectra-result.json`. Switch update debug lines to `AppendAi`.
- [X] T037 [US4] Update `.spectra-progress.html` template (search `progress.html` or `ProgressTemplate` in `src/Spectra.CLI/`) to add a "Token Usage" section that reads `token_usage.total` and `token_usage.estimated_cost_usd` / `cost_display` and renders running totals and final cost. Use the existing JS that polls `.spectra-result.json`.
- [X] T038 [P] [US4] Update `.github/skills/spectra-generate/SKILL.md` (and `spectra-update/SKILL.md`) to read `run_summary` and `token_usage` from `.spectra-result.json` and include them in the user-facing summary message ("Generated N tests from M docs (K behaviors). Token usage: X tokens in Y. Cost: …").

**Checkpoint**: SKILLs and progress page now surface token data.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T039 [P] Update `CHANGELOG.md` with a Spec 040 entry summarizing token tracking, debug log opt-in, model/provider in debug log, and `--verbosity diagnostic`.
- [X] T040 [P] Update `docs/cli-reference.md` to mention the Run Summary panel and `--verbosity diagnostic` for `generate`/`update`.
- [X] T041 [P] Update `docs/grounding-verification.md` with a one-liner about critic token tracking.
- [X] T042 [P] Update `CLAUDE.md` Recent Changes section with a v1.44.0 (or next-version) entry summarizing Spec 040.
- [X] T043 Run `dotnet build` from repo root and fix any warnings/errors introduced.
- [X] T044 Run `dotnet test` from repo root and confirm all existing tests still pass and all new tests pass (FR-032 regression check).
- [X] T045 Execute the manual checks in `specs/040-token-usage-tracking/quickstart.md` steps 1–10 against a test project to validate end-to-end behavior.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** → **Phase 2 (Foundational)** → **Phases 3–6 (User Stories)** → **Phase 7 (Polish)**
- T002 must complete before T003 (DebugConfig file before SpectraConfig wiring).
- T004 (TokenUsage in Core) must complete before T020/T021/T022/T027 (agent integration uses it).
- T005 (PhaseUsage) → T006 (TokenUsageTracker) → T007/T017 (CostEstimator + ReportBuilder).
- T008 (DebugLogger.AppendAi) must complete before T020/T021/T022/T026/T027.
- T009 + T010 (verbosity wiring) must complete before T032.

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2. Blocks nothing further.
- **US2 (P1)**: Depends on Phase 2. Most of US2 work overlaps with US1 (T020/T021/T022 already switch to `AppendAi`); T026 just verifies; T027 covers the criteria-extractor seam not already touched in US1.
- **US3 (P1)**: Depends on Phase 2. Independent of US1/US2.
- **US4 (P2)**: Depends on US1 (uses `TokenUsageReport` type) and US2 (debug-line format). Builds on top of both.

### Within Each User Story

- All test tasks `[P]` for a story can run in parallel against each other (different files).
- Tests should be authored before implementation per spec.md "test plan" intent.
- Within US1, T015/T016 are independent files and can be parallel; T017/T018 depend on them; T019 is a separate file change.
- T020/T021/T022 touch different files and can run in parallel after T008 + T006.

---

## Parallel Example: User Story 1

```bash
# After Phase 2 completes, launch tests in parallel:
Task: "T011 TokenUsageTrackerTests"
Task: "T012 CostEstimatorTests"
Task: "T013 RunSummaryPresenterTests"
Task: "T014 GenerateResultTokenUsageTests"

# Result/DTO files (different paths) in parallel:
Task: "T015 RunSummary DTO"
Task: "T016 TokenUsageReport DTO"

# Agent integration (different files) in parallel:
Task: "T020 BehaviorAnalyzer integration"
Task: "T021 GenerationAgent integration"
Task: "T022 GroundingAgent integration"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (T001) → Phase 2 (T002–T010) → Phase 3 (T011–T024).
2. STOP and validate: run `spectra ai generate <suite> --count 4` against a real project; confirm Run Summary panel appears with non-zero tokens.
3. Demo / merge as MVP.

### Incremental Delivery

- After US1: ship token tracking + Run Summary + JSON for `generate` (the highest-value slice).
- Add US2: complete model/provider coverage in debug log + criteria phase.
- Add US3: ship debug-log opt-in (visible behavior change for all users).
- Add US4: SKILL/progress page integration (secondary surface).
- Polish: docs, changelog, full regression run.

### Suggested MVP scope

User Story 1 alone delivers the headline feature ("see cost after a run"). US2 and US3 are small follow-ups. US4 is the SKILL/UX layer on top.

---

## Notes

- All file paths are absolute under `C:/SourceCode/Spectra/`.
- `[P]` means different file with no dependency on an in-flight task.
- Token tracking is purely additive — no existing tests should break (FR-032 / SC-008).
- The Copilot SDK `usage` object shape is verified during T020 implementation (see research.md R3); if a provider omits it, the `?` fallback path applies.
- Removing `AiConfig.DebugLogEnabled` is intentional (research.md R10); System.Text.Json silently ignores unknown fields so old configs still load.
