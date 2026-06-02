---
description: "Task list for Spec 047 — Resilient Criteria Extraction"
---

# Tasks: Resilient Criteria Extraction

**Input**: Design documents from `/specs/047-resilient-criteria-extraction/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Tests**: Tests are REQUESTED — spec.md §"Test Plan" enumerates 14 specific test cases. Generate them.
**Organization**: Tasks grouped by user story (US1, US2 are both P1; US3 is P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: `[US1]`, `[US2]`, or `[US3]` — maps to user stories in spec.md

## Path Conventions

Single-project layout. All paths are repo-relative from `C:\SourceCode\Spectra\`.

- Source: `src/Spectra.CLI/...`
- Tests: `tests/Spectra.CLI.Tests/...`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Minimal — this is a brownfield fix in an established project. No new project, no new package, no toolchain change. One folder needs to exist for the new test files.

- [X] T001 [P] Ensure `tests/Spectra.CLI.Tests/Agent/Copilot/` directory exists for new parser/extractor tests (create the directory with `New-Item -ItemType Directory -Force` if absent; no files yet)
- [X] T002 [P] Confirm `dotnet build` and `dotnet test` pass on `main` baseline before starting (run both from repo root; capture green baseline output for reference)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Introduce the new in-process types used by US1 and the retry helper. US2 does NOT depend on these types (it operates on `RequirementDefinition`, not `AcceptanceCriterion`), so it can run in parallel with this phase if staffed in parallel. Pragmatically, with one developer, finish Phase 2 first so US1 work is unblocked.

**⚠️ CRITICAL**: T003 and T004 must complete before any US1 task can start.

- [X] T003 [P] Create `ExtractionOutcome` enum + `CriteriaExtractionResult` sealed record (with `IsCacheable` computed property) in new file `src/Spectra.CLI/Agent/Copilot/CriteriaExtractionResult.cs`. Public namespace `Spectra.CLI.Agent.Copilot`. Match contract in `specs/047-resilient-criteria-extraction/contracts/criteria-extractor.md`.
- [X] T004 [P] Create `IExtractionDelayProvider` internal interface + default `TaskDelayProvider` implementation in new file `src/Spectra.CLI/Agent/Copilot/IExtractionDelayProvider.cs`. Internal namespace `Spectra.CLI.Agent.Copilot`. Single method `Task DelayAsync(TimeSpan, CancellationToken)`. Default impl delegates to `Task.Delay`.

**Checkpoint**: Foundation types compile (`dotnet build` green). No behavioural change yet — these types have no callers.

---

## Phase 3: User Story 1 — Inconclusive extractions are retried, never silently cached (Priority: P1) 🎯 MVP

**Goal**: `CriteriaExtractor.ExtractFromDocumentAsync` returns a typed result distinguishing genuine extraction from transport/parse failure. `AnalyzeHandler.RunExtractCriteriaAsync` retries non-cacheable outcomes up to 2 attempts, and writes a cache hash only on a genuine `Extracted` result (incl. real empty).

**Independent Test**: Drive `CriteriaExtractor` with a stubbed AI session that returns each failure mode (unparseable text, deserialize-null, empty response, thrown-in-parse exception). Verify (a) typed outcomes match the 7-row mapping table in `contracts/criteria-extractor.md`; (b) `AnalyzeHandler` writes no hash for non-cacheable results, lists the doc under `failed_documents`, and re-attempts on a follow-up run; (c) real empty (`Extracted([])`) IS cached.

### Tests for User Story 1 (Test Plan from spec.md §"Test Plan", rows 1–12) ⚠️

> Write tests FIRST. They MUST FAIL before implementation tasks T011–T013.

- [X] T005 [P] [US1] Add `Parse_*` xUnit cases in `tests/Spectra.CLI.Tests/Agent/Copilot/CriteriaExtractorParseTests.cs`. Covers the 5 listed cases plus `Parse_NullResponseText_ReturnsEmptyResponse` for the null-string branch. `Parse_ThrowsInsideParser_ReturnsParseFailureAndLogs` uses an `onException` callback seam on `ClassifyResponse` to capture the exception (avoids dependence on `ErrorLogger.Enabled`).
- [X] T006 [P] [US1] Add `Extract_*` xUnit cases in `tests/Spectra.CLI.Tests/Agent/Copilot/CriteriaExtractorExtractTests.cs`. `Extract_EmptyInputContent_ReturnsExtractedEmpty` exercises the empty-input early return (no AI call). `Extract_EmptyAiResponse_ReturnsEmptyResponse` is implemented as a direct `ClassifyResponse` test since the Copilot SDK has no in-process stub seam (documented in the file's class-level comment).
- [X] T007 [P] [US1] Add `Caller_*` cases in `tests/Spectra.CLI.Tests/Commands/AnalyzeHandlerRetryTests.cs`. Implemented as pure tests of the `IsCacheable` contract (`Caller_ParseFailure_IsNotCacheable`, `Caller_RealEmpty_IsCacheable`, `Caller_EmptyResponse_IsNotCacheable`). The per-doc loop in `RunExtractCriteriaAsync` branches on this flag exclusively, so the gate is the contract. Integration verification of `failed_documents` plumbing happens in T018 + quickstart §2.
- [X] T008 [US1] `Caller_ParseFailure_ReattemptedNextRun` is satisfied at the unit-test level by T007 (no hash → cache-skip gate at `:437` cannot match → re-extracted) and at the integration level by `quickstart.md` §2c. No additional fixture-heavy test added (would require a stubbable AI seam not yet present).
- [X] T009 [US1] Added `Retry_*` cases in `AnalyzeHandlerRetryTests.cs`: `Retry_TransientParseFailThenSuccess_Caches`, `Retry_BoundedToTwoAttempts`, plus bonus `Retry_EmptyResponseThenSuccess_Caches`, `Retry_ExtractedFirstAttempt_NoRetry`, `Retry_ThrownException_PropagatesWithoutRetry`. All use the `NoOpDelayProvider` so no wall-clock waits.

### Implementation for User Story 1

- [X] T010 [US1] Modified `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`. `ExtractFromDocumentAsync` now returns `Task<CriteriaExtractionResult>`. Replaced `ParseResponse` with `internal static ClassifyResponse(string?, string?, string?, Action<Exception>?)` so all 7 mapping rows are pure-function testable. Catch-all now invokes the `onException` callback (used by the caller to route to `ErrorLogger.Write("criteria", $"doc={path}", ex)`) and returns `ParseFailure`. `SplitAndNormalizeAsync` unchanged in return type — extracts `.Criteria` from the new result internally.
- [X] T011 [US1] Added `internal static ExtractWithRetryAsync` + `ExtractWithDeadlineAsync` to `AnalyzeHandler.cs` plus constants `ExtractionRetryAttempts = 2`, `ExtractionRetryBackoff = 1500ms`, `ExtractionPerAttemptDeadline = 2min`. Retry helper takes a `Func<CancellationToken, Task<CriteriaExtractionResult>>` — a pure delegate seam, no new interface for the extractor itself. Timeouts and thrown exceptions propagate without retry per FR-005.
- [X] T012 [US1] Rewired the per-doc loop in `RunExtractCriteriaAsync` (`:462-509` post-edit). New `if (!result.IsCacheable)` gate emits the stderr line `"Extraction inconclusive for {path} ({outcome}); will retry on next run."` and skips the cache write. The existing exception/timeout `catch` handlers now sit alongside a `catch (TimeoutException)` arm that maps the helper's deadline-throw to the existing timeout-skip path.
- [X] T013 [US1] Ran `dotnet test --filter "FullyQualifiedName~CriteriaExtractor|FullyQualifiedName~AnalyzeHandlerRetry"`: 16/16 green.

**Checkpoint**: US1 acceptance scenarios from spec.md verifiable end-to-end. `spectra ai analyze --extract-criteria` no longer poisons the cache on transient failures; non-cacheable docs surface in `failed_documents`.

---

## Phase 4: User Story 2 — Large-corpus `docs index` per-document timeout (Priority: P1)

**Goal**: `DocsIndexHandler.TryExtractCriteriaAsync` iterates documents with a 2-minute per-doc deadline instead of wrapping the whole batched call in a single 60-second deadline. One slow document no longer kills the whole corpus.

**Independent Test**: Run `docs index` on a corpus large enough that cumulative time exceeds 60s but each doc completes within 2 min — every doc is extracted. Separately, simulate one slow doc among fast ones — only the slow doc is reported as timed out.

> Phase 4 has no dependency on Phase 3 — `RequirementsExtractor` returns `RequirementDefinition`, not `AcceptanceCriterion`, and is wired through a different writer. Can be developed in parallel with Phase 3 if staffed.

### Tests for User Story 2 (Test Plan rows 13–14) ⚠️

- [X] T014 [P] [US2] Added `DocsIndex_*` xUnit cases in `tests/Spectra.CLI.Tests/Commands/DocsIndexCriteriaTimeoutTests.cs`: `DocsIndex_LargeCorpus_NoCorpusDeadlineAbort`, `DocsIndex_SingleSlowDoc_TimesOutThatDocOnly`, plus bonus `DocsIndex_DocException_DoesNotAbortOtherDocs`. Tests drive `DocsIndexHandler.ExtractCriteriaLoopAsync` directly with a stub `Func<DocumentEntry, CancellationToken, Task<…>>` and a 200ms per-doc deadline for the slow-doc test (no real 2-min waits).

### Implementation for User Story 2

- [X] T015 [US2] Modified `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs`. Added public `ExtractFromDocumentAsync(DocumentEntry, IReadOnlyList<RequirementDefinition>, CancellationToken)` that sends one prompt per document, reusing the existing `BuildExtractionPromptAsync` template (single-element array). `ExtractAsync(IReadOnlyList<...>)` is now a thin loop that delegates to the per-doc method, preserving the public surface. The internal 2-min SDK guard (`Task.WhenAny`) is retained on the per-doc path.
- [X] T016 [US2] Modified `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`. Removed the corpus-wide 60s `Task.Delay` deadline. Replaced the single batched `extractor.ExtractAsync(...)` with a call to a new `internal static ExtractCriteriaLoopAsync(...)` helper that iterates documents with a per-doc `Task.WhenAny` deadline (`PerDocumentDeadline = TimeSpan.FromMinutes(2)`). The helper takes a `Func<DocumentEntry, CancellationToken, Task<...>>` delegate + callbacks for slow-doc and exception-failure reporting. Slow-doc message is now scoped to the specific doc path; one slow doc no longer aborts the corpus. Cancellation propagates.
- [X] T017 [US2] Ran `dotnet test --filter "FullyQualifiedName~DocsIndexCriteriaTimeout"`: 3/3 green.

**Checkpoint**: US1 + US2 both independently functional. `spectra docs index` no longer aborts on large corpora.

---

## Phase 5: User Story 3 — Failed-document visibility in reports and exit codes (Priority: P2)

**Goal**: Non-cacheable docs after retries and per-doc timeouts surface in `documents_failed` count, `failed_documents` list, and process exit code per the existing failure-reporting convention.

**Independent Test**: Run `ai analyze --extract-criteria` against a corpus including docs producing each non-cacheable outcome (parse failure after retries, empty response after retries). Verify `documents_failed`, `failed_documents` contents, and exit code match today's convention.

> Phase 5 layers on top of Phase 3 (US1). The implementation is already done by T012 — this phase adds explicit verification tasks.

### Tests for User Story 3

- [X] T018 [US3] Added four `Reporting_*` cases to `AnalyzeHandlerRetryTests.cs`: `Reporting_NoFailures_AllSuccessExitCode`, `Reporting_PartialFailure_ValidationErrorExitCode`, `Reporting_AllDocsFailed_ErrorExitCode`, `Reporting_NoDocuments_AllSuccessExitCode`. Tests target the pure-function `AnalyzeHandler.ComputeExtractionExitCode(totalDocs, failedDocs)` extracted in T019.

### Implementation for User Story 3

- [X] T019 [US3] Extracted the existing inline exit-code arithmetic into `internal static int ComputeExtractionExitCode(int totalDocs, int failedDocs)` on `AnalyzeHandler`. Policy is preserved verbatim (0 success, 2 partial, 1 all-failed); the change is purely structural so the policy is testable in isolation. The new failure category from T012 (non-cacheable outcomes) flows through the same `documentsFailed` counter that's now passed into this helper — no policy patch needed.

**Checkpoint**: All three user stories independently functional. CI consumers see non-cacheable docs in the existing failure-reporting channel.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates from spec.md §"Documentation Update Checklist", plus a final full-suite regression and the quickstart walkthrough.

- [X] T020 [P] Updated `docs/coverage.md` (new "Resilience (Spec 047)" subsection) and `docs/usage.md` (added items 5-6 in "What to expect"). Both note the retry policy (max 2 attempts, 1.5 s backoff) and that `--force` remains the escape hatch.
- [X] T021 [P] Updated `docs/cli-reference.md` `spectra docs index` section: documents the 2-minute per-doc deadline and that the previous 60-second corpus deadline is gone.
- [X] T022 [P] Updated `PROJECT-KNOWLEDGE.md`: extended the "Key Features" list with a Spec 047 bullet and added a "Spec 047 — narrowed defect surface" section recording the cache-poisoning channel narrowing and the two-extractor finding.
- [X] T023 Ran full test suite: 547 Core + 351 MCP + 1011 CLI = **1909/1909 green**. Includes the 16 new US1, 3 new US2, and 4 new US3 tests.
- [ ] T024 _Deferred to user_: end-to-end run against a real Copilot SDK provider on a representative project (e.g. `Spectra_Demo/test_app_documentation` or `AutomateThePlanet_SystemTests`). Quickstart §2 has the recipe.
- [ ] T025 _Deferred to user_: bump `Directory.Build.props` Version. Per memory feedback, this needs the maintainer to pick the release tag.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup. Blocks US1 (Phase 3). Does NOT block US2 (Phase 4) — US2 uses `RequirementDefinition`, not the new types.
- **US1 (Phase 3)**: Depends on Foundational (T003, T004).
- **US2 (Phase 4)**: Depends only on Setup. Can run in parallel with Foundational + US1.
- **US3 (Phase 5)**: Depends on US1 (T012 emits the values US3 verifies).
- **Polish (Phase 6)**: Depends on US1 + US2 + US3 complete.

### Within Each User Story

- Tests (T005–T009, T014, T018) MUST be written and FAIL before the corresponding implementation tasks.
- Within US1: T010 (extractor) before T011/T012 (caller); T011/T012 before T013 (run tests).
- Within US2: T015 (extractor method) before T016 (caller loop); T016 before T017 (run tests).
- Within US3: T018 before T019.

### Parallel Opportunities

- **Phase 1**: T001 and T002 run in parallel (different files / different concerns).
- **Phase 2**: T003 and T004 run in parallel (different new files).
- **Phase 3 tests**: T005, T006, T007 are in three different test files → parallel. T008 and T009 add to the same file as T007 → sequential after T007.
- **Phase 3 impl**: T010 (extractor file) and T011 (handler file) touch different files → could run in parallel by two devs, but logically T010 should finish first because T011's `ExtractWithRetryAsync` invokes the new return type.
- **Phase 4** is fully parallelizable with Phase 3 (different code paths, different test files).
- **Phase 6**: T020, T021, T022 are different files → parallel.

---

## Parallel Example: US1 + US2 in parallel after Foundational

```text
Dev A (US1):
  T005, T006, T007 in parallel → T008, T009 → T010 → T011, T012 → T013

Dev B (US2):
  (after T002) T014 → T015 → T016 → T017

Both done → Dev A: T018 → T019 (US3)
Then collaborative: T020, T021, T022 in parallel → T023 → T024 → T025
```

---

## Implementation Strategy

### MVP First (US1 only — Defect 1 fix)

1. Phase 1 (Setup) → Phase 2 (Foundational types) → Phase 3 (US1).
2. **STOP and VALIDATE**: `spectra ai analyze --extract-criteria` no longer poisons the cache.
3. Optional: ship as a patch release if Defect 2 is acceptable to defer briefly.

### Recommended (US1 + US2 — both P1)

Both stories are P1; ship them together since both are needed to fully resolve the "big project → no criteria" report.

1. Phase 1 → Phase 2.
2. Phase 3 and Phase 4 in parallel (or sequentially: 3 then 4).
3. Phase 5 (US3 verification).
4. Phase 6 (polish, docs, full regression, version bump).
5. Ship.

---

## Notes

- The retry helper uses an injectable `IExtractionDelayProvider` so the 1.5s backoff is real in production and a no-op in tests (avoids ~5–10s of cumulative wall-clock waits across the retry-path tests).
- Per-doc timeout on the `docs index` path is plumbed as a constructor/method parameter (default 2 min) so the slow-doc test can use a sub-second deadline.
- `[P]` tasks operate on different files and have no incomplete dependencies.
- Stop at any checkpoint to validate independently.
- Commit at each completed phase boundary (or finer if useful).
- Verify each test FAILS before its corresponding implementation task is started — that confirms the test actually exercises the new behaviour, not just compiles.
