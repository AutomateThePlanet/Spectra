# Tasks: Parallel Critic Verification & Error Log

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md)
**Branch**: `042-parallel-critic-error-log`

Tasks are ordered by dependency. Each `[P]` tag means the task can run in parallel with the previous `[P]`-tagged task (independent files).

## Phase 1 — Config surface

- **T001** · Add `MaxConcurrent` int property to `src/Spectra.Core/Models/Config/CriticConfig.cs` (JSON name `max_concurrent`, default 1). Add `GetEffectiveMaxConcurrent()` method clamping to [1, 20].
- **T002 [P]** · Add `ErrorLogFile` string property to `src/Spectra.Core/Models/Config/DebugConfig.cs` (JSON name `error_log_file`, default `.spectra-errors.log`).
- **T003** · Update `src/Spectra.CLI/Config/Templates/spectra.config.json` to include the new fields (commented/default values).

## Phase 2 — Error logging + counters

- **T004** · Create `src/Spectra.CLI/Infrastructure/ErrorLogger.cs` — static class mirroring `DebugLogger` (`Enabled`, `LogFile`, `Mode`, `BeginRun`, `Write`). Lazy-creates file on first write; thread-safe via lock; graceful stderr warning on file failure (flips `Enabled=false`).
- **T005 [P]** · Create `src/Spectra.CLI/Services/RunErrorTracker.cs` — instance class with thread-safe `IncrementError()`, `IncrementRateLimit()`, `Errors`, `RateLimits` properties.
- **T006** · Add `DebugLogger.AppendError(component, message)` helper that auto-appends `see=<ErrorLogger.LogFile>` suffix when `ErrorLogger.Enabled`.

## Phase 3 — Parallel critic verification

- **T007** · Refactor `GenerateHandler.VerifyTestsAsync` (src/Spectra.CLI/Commands/Generate/GenerateHandler.cs:1804–1900):
  - Read `criticConfig.GetEffectiveMaxConcurrent()`.
  - Build pre-sized `(TestCase, VerificationResult)?[]` array.
  - Create per-test tasks using `SemaphoreSlim` throttle and `Task.WhenAll`.
  - Preserve manual-verdict short-circuit.
  - On exception: write to `ErrorLogger`, increment `RunErrorTracker.Errors` (and `RateLimits` if classified), create `Unverified` verdict.
  - Return results in original order.
- **T008** · Wire `RunErrorTracker` into `GenerateHandler` constructor/fields and pass to `CopilotCritic`/`CriticFactory` (new optional param).
- **T009** · Update `CopilotCritic.VerifyTestAsync` (src/Spectra.CLI/Agent/Copilot/GroundingAgent.cs:122–160) to classify rate-limit exceptions and call the injected `RunErrorTracker`. Add `ErrorLogger.Write("critic", test.Id, ex, ...)` in catch blocks.

## Phase 4 — Error capture across other phases

- **T010** · Wire `ErrorLogger.Write("analyze", ..., ex)` into `BehaviorAnalyzer.cs:176–194` catch blocks; increment tracker.
- **T011 [P]** · Wire `ErrorLogger.Write("generate", batch=<n>, ex)` into `CopilotGenerationAgent.cs:351–411` catch blocks; increment tracker.
- **T012 [P]** · Wire `ErrorLogger.Write("update", ..., ex)` into `UpdateHandler.cs:125–133` catch blocks; increment tracker.
- **T013 [P]** · Wire `ErrorLogger.Write("criteria", ..., ex)` into `CriteriaExtractor` catch sites; increment tracker.

## Phase 5 — Run summary surfacing

- **T014** · Extend `RunSummaryDebugFormatter.FormatRunTotal` to accept `RunErrorTracker` (or counts) and append ` rate_limits=<n> errors=<n>` to the formatted line.
- **T015** · Extend `RunSummaryPresenter.Render` to add three new rows in the grid: `Critic concurrency`, `Errors`, `Rate limits`. When `Rate limits > 0`, append the `(consider reducing ai.critic.max_concurrent)` hint.
- **T016** · Update call sites of `FormatRunTotal` and `Render` in `GenerateHandler` and `UpdateHandler` to pass the tracker and effective critic concurrency value.

## Phase 6 — Startup wiring

- **T017** · In `GenerateHandler.HandleAsync` startup (lines 208–214 region): initialize `ErrorLogger.Enabled`/`LogFile`/`Mode` from `config.Debug` and call `BeginRun()`. Mirror in `UpdateHandler`.
- **T018** · In the same region, emit stderr warning when raw `MaxConcurrent > 20` (clamped) or `> 10` (risky).

## Phase 7 — Tests

- **T019** · `tests/Spectra.Core.Tests/Config/CriticConfigClampTests.cs` — `GetEffectiveMaxConcurrent` clamps values ≤0 → 1, >20 → 20, passthrough in range.
- **T020 [P]** · `tests/Spectra.CLI.Tests/Infrastructure/ErrorLoggerTests.cs` — `Write_CreatesFile`, `Disabled_NoFileCreated`, `CapturesStackTrace`, `CapturesResponseBody` (truncation at 500 chars), `ConcurrentWrites_NoCorruption`.
- **T021 [P]** · `tests/Spectra.CLI.Tests/Services/RunErrorTrackerTests.cs` — counter increments are thread-safe.
- **T022 [P]** · `tests/Spectra.CLI.Tests/Services/RunSummaryDebugFormatterTests.cs` — `RUN TOTAL` line includes `rate_limits=N errors=N` even on zero.
- **T023** · `tests/Spectra.CLI.Tests/Commands/Generate/ParallelCriticTests.cs`:
  - `MaxConcurrent1_RunsSequentially` — use a fake critic that records call order; assert sequential.
  - `MaxConcurrent5_RunsInParallel` — fake critic with a 50ms delay; 10 tests finish in <200ms.
  - `ResultsPreserveOrder` — out-of-order completion still returns input order.
  - `PartialFailure_OtherTestsContinue` — one task throws; others complete and the failed one gets `Unverified`.

## Phase 8 — Build + verify

- **T024** · `dotnet build` — resolve all errors.
- **T025** · `dotnet test` — all tests pass.

## Phase 9 — Docs

- **T026** · Update `docs/configuration.md` — document `ai.critic.max_concurrent` and `debug.error_log_file` with tuning guidance.
- **T027 [P]** · Update `docs/grounding-verification.md` — note parallel critic option and performance table.
- **T028 [P]** · Update `docs/cli-reference.md` — troubleshooting section references `.spectra-errors.log`.
- **T029** · Update `CHANGELOG.md` — v1.48.0 entry summarizing spec 043.
- **T030** · Update `CLAUDE.md` Recent Changes section with the feature summary.

## Dependency Notes

- T004 must land before T006 (debug-error helper references `ErrorLogger.Enabled`).
- T007 blocks T008/T009 (tracker wiring depends on the refactored method signature).
- T014–T016 depend on T005 (tracker type must exist).
- T019–T023 depend on the relevant source changes in Phases 1–6.
- T024–T025 block T026–T030 (no docs updates on broken code).
