# Tasks: Generation & Verification Progress Bars

**Feature**: 041-progress-bars
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Data Model**: [data-model.md](./data-model.md)

## Phase 1 — Setup

(No project init needed; existing single-project CLI.)

- [X] T001 Confirm `Spectre.Console` is referenced by `src/Spectra.CLI/Spectra.CLI.csproj` (it already is — this is a verification task only; no code change).

## Phase 2 — Foundational (blocks all user stories)

These tasks introduce the shared `ProgressSnapshot` data type and wire suppression rules into the existing `ProgressReporter`. Every user story below depends on them.

- [X] T002 Create `ProgressSnapshot` record at `src/Spectra.CLI/Progress/ProgressSnapshot.cs` with the fields and `ProgressPhase` enum (`Generating`, `Verifying`, `Updating`) per `data-model.md`.
- [X] T003 Add optional `Progress` property of type `ProgressSnapshot?` to `src/Spectra.CLI/Results/GenerateResult.cs` (camelCase JSON, omit when null).
- [X] T004 Add optional `Progress` property of type `ProgressSnapshot?` to `src/Spectra.CLI/Results/UpdateResult.cs` (camelCase JSON, omit when null).
- [X] T005 Modify `src/Spectra.CLI/Progress/ProgressManager.cs` so `Complete()` and `Fail()` clear `result.Progress` to null before the final write (around lines 136–172). Verify `FlushWriteFile` still serializes correctly with the field omitted.
- [X] T006 Extend the suppression check inside `src/Spectra.CLI/Output/ProgressReporter.cs` `ProgressAsync` (lines 64–91) to ALSO suppress when `AnsiConsole.Profile.Capabilities.Interactive == false` OR when `_verbosity == VerbosityLevel.Quiet`. Keep existing JSON-mode short-circuit. Add an internal helper `bool ShouldSuppressProgress()` shared with the new entry points added in T010/T015/T019.
- [X] T007 Add a new overload `ProgressReporter.ProgressTwoTaskAsync(string genDescription, string verifyDescription, int total, Func<IProgressTaskHandle, IProgressTaskHandle, CancellationToken, Task> action, CancellationToken ct)` to `src/Spectra.CLI/Output/ProgressReporter.cs` that creates two sequential Spectre tasks (gen + verify) inside one `AnsiConsole.Progress()` block. The two tasks expose `Increment(double)` and `SetDescription(string)` via a small `IProgressTaskHandle` wrapper interface (also new in this file) so handlers don't depend directly on Spectre `ProgressTask`. Suppression returns a no-op pair of handles.

## Phase 3 — User Story 1: Terminal generation + verification progress bar (P1)

**Story Goal**: A user running `spectra ai generate <suite> --count 40+` sees a live terminal progress bar advancing per batch during generation, then a second bar advancing per critic call during verification.

**Independent Test** (from spec): Run `spectra ai generate <suite> --count 40` in a normal terminal. Confirm the generation bar advances per batch, the verification bar advances per critic call with TC-id + verdict, and both clear cleanly on completion.

- [X] T008 [P] [US1] Add `Progress/ProgressBarTests.cs` to `tests/Spectra.CLI.Tests/Progress/` with the four checklist tests: `Generation_IncrementsPerBatch`, `Critic_IncrementsPerTest`, `QuietVerbosity_NoAnsiOutput`, `JsonFormat_NoAnsiOutput`. Use a fake `IAnsiConsole` (Spectre's `TestConsole`) and the new `ProgressTwoTaskAsync` API so handlers don't need to be invoked end-to-end.
- [X] T009 [P] [US1] Add `Results/ResultFileProgressTests.cs` to `tests/Spectra.CLI.Tests/Results/` with `Progress_WrittenAfterEachBatch`, `Progress_WrittenAfterEachCritic`, `Progress_RemovedOnCompletion` — drive `ProgressManager` directly with a fake `GenerateResult`.
- [X] T010 [US1] In `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`, wrap the existing batch loop (lines 564–680) and the `VerifyTestsAsync` per-test critic loop (lines 1794–1832) inside a single `_progress.ProgressTwoTaskAsync(...)` call. Pass `effectiveCount` as `total`. The two `IProgressTaskHandle` instances are captured in local variables and increment as: gen handle by `batch.RequestedCount` after each batch completes; verify handle by `1` after each `critic.VerifyTestAsync` returns.
- [X] T011 [US1] In the same handler, build a `ProgressSnapshot` at the start of generation (`Phase=Generating`, `TestsTarget=effectiveCount`, `TotalBatches=ceil(effectiveCount/configuredBatchSize)`, `CurrentBatch=1`) and assign it to `result.Progress`. After each batch completes, increment `TestsGenerated += batch.RequestedCount`, `CurrentBatch++`, set `LastTestId = lastTestInBatch?.Id`, then call the existing `UpdateProgress()` (lines 1968–1990). No new write site — reuses the existing per-batch flush.
- [X] T012 [US1] Add a new `WriteCriticProgress(snapshot, test, verdict)` step inside the `VerifyTestsAsync` loop body in `GenerateHandler.cs` (line 1823 area, in the existing `onTestVerified` callback): mutate `snapshot.Phase = Verifying`, `snapshot.TestsVerified++`, `snapshot.LastTestId = test.Id`, `snapshot.LastVerdict = verdict.Verdict.ToString().ToLowerInvariant()`, then call `_progressManager.Update(result)` so the per-critic write hits `.spectra-result.json`.
- [X] T013 [US1] When `--skip-critic` is set, skip the verification phase tracking entirely so no idle verification bar is shown (the second `IProgressTaskHandle` is not added in that path; `ProgressTwoTaskAsync` overload accepts a flag `bool includeVerifyTask = true`).
- [X] T014 [US1] Update the description string of the verify handle inside the loop to `$"Verifying tests  {test.Id} {verdict}"` when `_verbosity >= VerbosityLevel.Normal`; leave the description as the static `"Verifying tests"` when `_verbosity == VerbosityLevel.Minimal` (per FR-004).

**Checkpoint**: After Phase 3, US1 is complete and independently verifiable via the quickstart.md "Terminal generation + verification" recipe.

## Phase 4 — User Story 2: Browser progress page reflects per-test progress (P2)

**Story Goal**: The browser progress page (`.spectra-progress.html`) renders new generation + verification progress bars driven by the `progress` field in `.spectra-result.json`.

**Independent Test**: Run `spectra ai generate <suite> --count 40 --output-format json > /dev/null`, then open `.spectra-progress.html`. The page shows a generating bar that advances; after generation, a second verifying bar becomes active and advances; after completion, the in-flight bars are gone.

- [X] T015 [P] [US2] Add `Progress/ProgressPageProgressBarTests.cs` to `tests/Spectra.CLI.Tests/Progress/` with `ProgressSection_RendersBarDuringGeneration` and `ProgressSection_ShowsVerifyingPhase`. Drive `ProgressPageWriter` with a synthesized `GenerateResult.Progress` and assert the rendered HTML string contains the expected bar widths and section classes.
- [X] T016 [US2] In `src/Spectra.CLI/Progress/ProgressPageWriter.cs` (the `$$"""..."""` template, lines 56–340+), add a new `progress-section` HTML block immediately below the existing `.stepper` div (around line 126). It contains two `<div class="progress-section">` blocks — one for generation, one for verification. Each has `progress-label`, `progress-bar-track > progress-bar-fill`, and `progress-detail` children, all per the spec example.
- [X] T017 [US2] In the same file, add CSS rules for `.progress-section`, `.progress-label`, `.progress-bar-track`, `.progress-bar-fill`, `.progress-detail`, and `.progress-section.dimmed` to the existing inline `<style>` block. Use the existing navy/teal palette CSS variables (from spec 012). The dimmed variant uses `opacity: 0.3`.
- [X] T018 [US2] Add a private helper `string RenderProgressBars(ProgressSnapshot? snapshot)` to `ProgressPageWriter.cs` that returns the HTML string for both bars based on the snapshot, computing `width: {percent}%` for each fill. Returns empty string when `snapshot == null`. Inject the result into the template via a new interpolated placeholder. Verification section is `dimmed` while `snapshot.Phase == Generating`, active when `snapshot.Phase == Verifying`. Include a "Batch N of M" or "TC-XXX grounded" detail line per phase.

**Checkpoint**: After Phase 4, US2 is complete. Quickstart "Progress page" recipe should pass.

## Phase 5 — User Story 3: Update command shows per-proposal progress (P3)

**Story Goal**: `spectra ai update <suite>` displays a progress bar that advances per applied proposal.

**Independent Test**: Run `spectra ai update <suite>` against a suite with multiple update proposals. Confirm an "Updating tests" bar appears, advances per proposal, and clears on completion.

- [X] T019 [P] [US3] Add `Progress/UpdateProgressTests.cs` to `tests/Spectra.CLI.Tests/Progress/` with `Proposals_IncrementCorrectly` (renamed from `Chunks_IncrementCorrectly` since the actual unit is proposals — see plan §"Surprises").
- [X] T020 [US3] In `src/Spectra.CLI/Commands/Update/UpdateHandler.cs`, wrap the proposal apply loop (lines 579–631) in a single-task `_progress.ProgressAsync("Updating tests", proposals.Count, async ctx => {...})` block. Reuse the existing single-task `ProgressAsync` overload (no need for the two-task variant added in T007).
- [X] T021 [US3] In the same handler, build a `ProgressSnapshot` (`Phase=Updating`, `TestsTarget=proposals.Count`, `TotalBatches=proposals.Count`, `CurrentBatch=1`, `TestsGenerated=0`, `TestsVerified=0`) and assign it to `result.Progress`. After each proposal applies, increment `CurrentBatch`, set `LastTestId = proposal.TestId`, and call `_progressManager.Update(result)`.
- [X] T022 [US3] Extend `ProgressPageWriter.RenderProgressBars` (added in T018) to handle `Phase=Updating`: render a single bar labeled "Updating tests" with no second/dimmed verification bar.

**Checkpoint**: After Phase 5, US3 is complete. All three user stories are independently verifiable.

## Phase 6 — Polish & Cross-Cutting

- [X] T023 [P] Update `docs/cli-reference.md` to note that `spectra ai generate` and `spectra ai update` now display progress bars at default verbosity, and that progress bars are suppressed under `--output-format json`, `--verbosity quiet`, and non-TTY stdout.
- [X] T024 [P] Add a `CHANGELOG.md` entry under a new `v1.47.0` section summarizing the feature, citing this spec, and noting that `--output-format json` / `--verbosity quiet` behavior is unchanged for SKILLs and CI.
- [X] T025 Run the full test suite (`dotnet test`) and confirm all new tests pass and no existing tests regress.
- [X] T026 Manually walk through `specs/041-progress-bars/quickstart.md` recipes 1, 2, 3, 4, 5, 6, 7. Confirm SC-001 through SC-006 visually.
- [X] T027 Sanity-check `.spectra-result.json` after a real run: while in flight, `progress` is present and updates; after completion, `progress` is absent and `runSummary` is present (SC-005).

## Dependencies

```
Phase 1 (T001)
  ↓
Phase 2 Foundational (T002 → T003, T004, T005, T006 → T007)
  ↓
Phase 3 US1 (T008, T009 [P]) → T010 → T011 → T012 → T013, T014
  ↓ (independently shippable)
Phase 4 US2 (T015 [P]) → T016 → T017 → T018
  ↓ (independently shippable)
Phase 5 US3 (T019 [P]) → T020 → T021 → T022
  ↓
Phase 6 Polish (T023, T024 [P]) → T025 → T026 → T027
```

**User-story independence**: US2 depends on the same `ProgressSnapshot` writes that US1 produces, but the writes themselves come from Phase 3. If you ship only US1, the page still works against the existing phase-stepper UI without the new bars. If you skip US3, generate-only UX is fully covered. So US1 = MVP.

## Parallel Execution Opportunities

- **T002, T003, T004** can run in parallel (different files: new `ProgressSnapshot.cs`, modified `GenerateResult.cs`, modified `UpdateResult.cs`).
- **T008, T009** in parallel (different test files).
- **T015** runs in parallel with the rest of US2 setup since it only needs the data-model contract.
- **T019** in parallel with US3 implementation.
- **T023, T024** in parallel (different doc files).

## Implementation Strategy

**MVP (US1 only)**: Phases 1 → 2 → 3. After Phase 3 ships, terminal users see progress bars during generation+verification. Browser progress page still works using the existing phase stepper. This is the minimum viable slice.

**Incremental delivery**:
1. Land US1 as MVP — solves the core "is it stuck?" pain.
2. Land US2 — gives SKILL/CI/remote users the same feedback in the browser.
3. Land US3 — extends the pattern to the update command.
4. Polish phase ties documentation and verification.
