# Tasks: Performance and Cleanup (Phase 6, Part 4)

**Input**: Design documents from `/specs/099-performance-cleanup/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/contracts.md, quickstart.md

**Tests**: required by the spec — the golden-output check (FR-002), a deterministic count guard per performance change
(FR-006/008/014, FR-022), the writer guard (FR-011/012) and the warnings-as-errors guard (FR-018). No wall-clock
assertion is added; timings go to Notes only.

"Notes" in a task means the Notes section at the end of specs/099-performance-cleanup/tasks.md; "golden" means
tests/Spectra.CLI.Tests/Golden/GoldenOutputTests.cs; "full suite" means `dotnet test` over all four test projects;
"scope" means a `ParseCounter` scope (research R8) opened by the test around the measured call.

**Rule for every phase after US1**: the golden check and the full suite stay green after the phase; a red golden check
is a behaviour change → fix the code, never the golden files or masks.

## Phase 1: Setup

- [X] T001 Copy the `c24d23e` baseline into Notes: test counts (Core 1253 / CLI 2326 / Execution 228 / Integration 8), the 4 build warnings, the idle CLI wall time (597 s) and the 20-slowest table from specs/099-performance-cleanup/research.md R7; save the TRX aggregation script used there as specs/099-performance-cleanup/trx-classes.py (reads a `.trx` + optional collection map, prints Σ duration per class and per collection)
- [X] T002 Create a detached worktree at `c24d23e` in the scratchpad (`git worktree add --detach <scratch>/c24d23e c24d23e`) for golden capture; never commit in it; remove it in T064

## Phase 2: Foundational (blocking)

- [X] T003 Fix CS0108: rename `private const string Name` → `CommandName` (value unchanged) in src/Spectra.CLI/Commands/Docs/DocsApplyMoveCommand.cs and src/Spectra.CLI/Commands/Docs/DocsRetireCommand.cs (all uses in each file)
- [X] T004 Fix CS8714/CS8621: explicit `ToDictionary<CriteriaSource, CriteriaSource, string>(…, ReferenceEqualityComparer.Instance)` at line 30 of src/Spectra.CLI/Criteria/CriteriaLayoutMigrator.cs
- [X] T005 Add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>` (unconditional PropertyGroup) to src/Spectra.CLI/Spectra.CLI.csproj and src/Spectra.Core/Spectra.Core.csproj; `dotnet build --no-incremental` → 0 warnings from CLI and Core (record Execution's ImageSharp restore advisories as known, out of scope)
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Infrastructure/WarningsAsErrorsGuardTests.cs: parse both csproj files (walk up from the test assembly to the repo root as `RepositoryAttributesTests` does) and assert `TreatWarningsAsErrors` = true without a `Condition`, and `WarningsNotAsErrors` contains NU1901–NU1904
- [X] T007 Verify by hand that a deliberate unused local in a CLI file fails `dotnet build` (revert); note in Notes
- [X] T008 Grant Core internals to the CLI tests: add `<InternalsVisibleTo Include="Spectra.CLI.Tests" />` beside the existing entries in src/Spectra.Core/Spectra.Core.csproj (needed by `ParseCounter`, `TestCase.LayoutOmitted`, the internal `SectionExtractor.Extract` overload)
- [X] T009 Create src/Spectra.Core/Diagnostics/ParseCounter.cs per research R8 (internal static class; `AsyncLocal<Scope?>`; `Begin()` returns a disposable scope that restores its parent; `FullParse()`, `LayoutCapture()`, `DocumentHeadingScan()`, `JsonParse()` increment only the innermost current scope with `Interlocked`; no scope = null check only) and tests/Spectra.Core.Tests/Diagnostics/ParseCounterTests.cs (no scope → no throw; nested scopes; two parallel tasks with their own scopes see only their own counts; increments from inside `Parallel.ForEachAsync` reach the caller's scope; dispose restores the parent)
- [X] T010 Wire the counter into today's code (counting only, no behaviour change): `FullParse()` at the start of every `TestCaseParser.Parse` attempt and `LayoutCapture()` in `AttachLayout` (src/Spectra.Core/Parsing/TestCaseParser.cs); `DocumentHeadingScan()` at each whole-document scan in src/Spectra.CLI/Baseline/BaselineContext.cs (the `Extract` calls at :158/:199 and `Headings` at :210); `JsonParse()` only inside `TryParseArray` in src/Spectra.CLI/Generation/GeneratedTestIngestor.cs (the single `JsonDocument.Parse` site, once per real parse; `ArrayObjects` counts through it, so today's number is 3 per staged file)

## Phase 3: User Story 1 — nothing a user sees changes (P1) 🎯 MVP, blocks US2–US6

**Goal**: a golden-output check over a real-init fixture, captured on `c24d23e`.
**Independent test**: `dotnet test tests/Spectra.CLI.Tests --filter FullyQualifiedName~GoldenOutputTests` green on 099.

- [X] T011 [US1] Create the fixture inputs under tests/Spectra.CLI.Tests/Golden/099/inputs/: 2 docs (`docs/payments/checkout.md` with `{#id}` headings, a duplicate heading, a fenced block with `#` lines; `docs/payments/refunds.md`), suite `payments` of 6 tests (one with an unknown frontmatter key + column-0 comment, one CRLF, one `depends_on` scalar, one unparseable `.md`), `new-tests.json` (2 tests), `edit-TC-*.json` (one edit), staged files for `--all` (one valid edit, one `MULTIPLE_TESTS`, one `ID_MISMATCH`), verdict JSONs (grounded/partial/hallucinated), `analysis.json` for `ingest-analysis`, and the doc edit applied before triage; mark the folder as content (`CopyToOutputDirectory`) in tests/Spectra.CLI.Tests/Spectra.CLI.Tests.csproj
- [X] T012 [US1] Write tests/Spectra.CLI.Tests/Golden/GoldenOutputTests.cs (`[Collection(CriteriaCommandsCollection.Name)]`, uses `BaselineTestWorkspace.RunAsync` and the real init). Step sequence: `--help`, `docs --help`, `ai --help`, `docs apply-move --help`, `docs retire --help` → init → docs index → source-baseline --init --all-suites → `update-ack --suite payments --group G1` before any triage (exit 4 `TRIAGE_MISSING`) → ingest-tests → doc edit → update-triage → `update-ack --group G1 --dry-run` → `compile-update-prompt --suite payments --group G99` (exit 4 `GROUP_NOT_FOUND`) → compile-update-prompt `--group G1` / `--test-id` → ingest-update `--test-id --from` → ingest-update `--all` (exit 8) → ingest-verdict `--all` → ingest-grounding `--all --ids` → audit-grounding → ingest-analysis → compile-prompt `--batch B1` → validate → show → docs list-suites → ai analyze `--coverage --format json` → dashboard `--dry-run`. Per step write the record of data-model "Golden step record"; at the end `files.txt` (all workspace files sorted ordinally, text inline, binary as sha256); `SPECTRA_GOLDEN_RECORD=1` → write to the golden folder, else compare record by record with a unified diff in the failure message
- [X] T013 [US1] Add the masking helper tests/Spectra.CLI.Tests/Golden/GoldenMasks.cs: named rules (start with the workspace temp root only); every rule replaces a value with `<mask:name>`, never removes lines
- [X] T014 [US1] Copy T011–T013 into the `c24d23e` worktree (T002) — the test must compile there without the Phase 2 code (it uses no `ParseCounter`) — and run with `SPECTRA_GOLDEN_RECORD=1` twice into two folders; diff them; add a mask rule for each field that differs (expected candidates: `verified_at`, `compiled_at`, `generated_at`, `recorded_at`, durations, quoted file times) and re-capture until two captures are identical after masking; during the final capture also save the post-init workspace tree to tests/Spectra.CLI.Tests/Golden/099/pre099-workspace/; record each mask with the two-run evidence in Notes
- [X] T015 [US1] Copy the final golden files and `pre099-workspace/` from the worktree into tests/Spectra.CLI.Tests/Golden/099/ and run the golden check on the 099 branch (Phase 2 applied) → green; if red, Phase 2 altered output → fix it
- [X] T016 [US1] Add a second `[Fact]` to tests/Spectra.CLI.Tests/Golden/GoldenOutputTests.cs that runs the steps after init on a copy of tests/Spectra.CLI.Tests/Golden/099/pre099-workspace/ instead of a fresh init — the 083 pre-099 workspace row

**Checkpoint**: golden check green; from here on every phase ends with it green.

## Phase 4: User Story 2 — an ingest reads the suite once (P1, F1)

**Goal**: N parses per ingest call for a suite of N test files (was 2N); baseline byte-identical.
**Independent test**: `IngestParseCountTests` + golden check.

- [X] T017 [US2] Write tests/Spectra.CLI.Tests/Baseline/SuiteScanBuilderTests.cs: for each case build the disk state, compare `SuiteScanBuilder.WithWritten(FromFiles(load), writes)` with `BaselineStore.ScanSuiteAsync` on the post-write disk (tests, ids, unparseable files, order): new test added; existing test rewritten; an unparseable `TC-9.md` overwritten by a valid TC-9; a file holding another id overwritten; test stored as `foo.md` updated (→ `{id}.md` beside it); case-variant overwrite (`TC-1.md` / `tc-1`, Windows rule via `PathComparer`); empty/missing suite folder
- [X] T018 [US2] Write tests/Spectra.CLI.Tests/Baseline/IngestParseCountTests.cs: suite of N test files (incl. 1 unparseable); inside a scope, `FullParses` for one `ingest-tests` call, one `ingest-update --test-id` call and one `ingest-update --all` call, each with and without `--keep-baseline`, plus `--dry-run` for `ingest-update` only (single and `--all`; `ingest-tests` has no `--dry-run`); run on the branch with only Phase 2 applied first and record today's numbers in Notes (expected 2N plus any parse from `PendingChangeCheck` / `TriageEngine.TriageTestAsync`); then assert the target N per call, or the pinned real number with its reason in Notes for any extra that can't be removed without behaviour change; baseline file bytes equal to a run with the scan forced (`BaselineRecorder` without a scan)
- [X] T019 [US2] Create src/Spectra.CLI/Commands/Generate/IngestSuiteLoader.cs: `LoadAsync(repoRoot, testsPath, suite, bool captureLayout, ct)` → existing tests (same tests and order as the two old `LoadExistingTestsAsync`) + per-file outcomes (ordinal path order; unparseable as repo-relative `/` paths)
- [X] T020 [US2] Create src/Spectra.CLI/Baseline/SuiteScanBuilder.cs: `FromFiles(...)` and `WithWritten(scan, IReadOnlyList<(string Path, TestCase Test)> written, StringComparer pathComparer)` per data-model "Suite scan"
- [X] T021 [US2] `BaselineRecorder.RecordPersistedAsync(…, SuiteScan? scan = null)`: given → skip `ScanSuiteAsync` in src/Spectra.CLI/Baseline/BaselineRecorder.cs
- [X] T022 [US2] `IngestTestsCommand`: replace `LoadExistingTestsAsync` with `IngestSuiteLoader.LoadAsync(…, captureLayout: false)`; pass `WithWritten(load, persisted at TestFileWriter.GetFilePath(...))` to the recorder in src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs
- [X] T023 [US2] `IngestUpdateCommand` (`--test-id` and `--all`): same, `captureLayout: true` (the original is written); written paths = `GetFilePath(testsPath, suite, id)` for every Written outcome in src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs; delete both old loaders; where `PendingChangeCheck` / `TriageTestAsync` re-parse a test already loaded, pass the loaded test instead if that changes no output (golden + their existing rows), else leave it and pin it (T018)
- [X] T024 [US2] Run T017/T018 + golden + full suite; record before/after counts per variant in Notes

## Phase 5: User Story 3 — triage resolves headings once per document (P2, F2)

**Goal**: one document heading scan per document per `BaselineContext`.
**Independent test**: `HeadingScanCountTests` + golden check (triage detail, prompts).

- [X] T025 [P] [US3] Write tests/Spectra.CLI.Tests/Baseline/HeadingScanCountTests.cs (inside a scope): one context resolving 50 refs into 1 doc → `DocumentHeadingScans == 1`; refs into 3 docs + `GetHeadingsAsync` + `GetRawSectionAsync` repeats → 3; a missing doc → 0 scans and the same missing reason; two contexts → 1 each; section results equal to `SectionExtractor.Extract(text, anchor)` for every ref (ambiguous, `{#id}`, image heading, duplicate `-n` anchors); record today's numbers (Phase 2 only) in Notes first
- [X] T026 [US3] Add `internal static SectionLookup Extract(string normalisedText, IReadOnlyList<MarkdownHeading> headings, string? anchor)`; public `Extract(text, anchor)` = normalise + `Headings` + overload (early return without anchor kept) in src/Spectra.Core/Baseline/SectionExtractor.cs
- [X] T027 [US3] `BaselineContext`: `_normalised` + `_headings` caches keyed like `_files` (Ordinal, same path string); scan (and count) once per path; use them at `GetSectionAsync` (:158), `GetRawSectionAsync` (:199), `GetHeadingsAsync` (:210) in src/Spectra.CLI/Baseline/BaselineContext.cs
- [X] T028 [US3] Run T025 + Core baseline rows (085/098 matcher, 084 determinism) + golden + full suite; Notes

## Phase 6: User Story 4 — read-only commands skip the write-only layout (P2, F3)

**Goal**: read-only callers parse without layout; the writer refuses such a test; writer paths unchanged.
**Independent test**: `LayoutModeTests` + golden check.

- [X] T029 [US4] Write tests/Spectra.CLI.Tests/IO/LayoutModeTests.cs: (a) writer unit rows — `TestCaseParser(captureLayout: false)` parse → `FormatTestCase`/`WriteAsync` throw `InvalidOperationException` naming the id, file untouched; `with` copy still refused (`LayoutOmitted` carried); in-memory new test → canonical (as today); (b) writer-path matrix — fixture test with unknown key, comment, CRLF, unknown body section run through ingest-update edit, `ingest-grounding --all`, review-flagged accept, `spectra delete` of a scalar dependency, `relink-sources`, legacy `ai update --no-review` orphan marking → hand edits survive byte-for-byte outside the changed keys; (c) read-only rows — `LayoutCaptures` = 0 inside a scope for `validate`, `show`, `docs index`, `update-triage --dry-run`, `audit-grounding`, `ai analyze --coverage`, `dashboard --dry-run`, `compile-critic-prompt`, `compile-repair-batch`, `compile-prompt --batch`, `ingest-analysis`, `review-flagged` list; record today's (c) numbers (Phase 2 only, > 0) in Notes first
- [X] T030 [US4] `TestCase.LayoutOmitted` (`internal`, `init`, `[JsonIgnore]`, `[YamlIgnore]`) in src/Spectra.Core/Models/TestCase.cs
- [X] T031 [US4] `TestCaseParser(bool captureLayout = true)` (readonly field); without capture skip `AttachLayout` and set `LayoutOmitted = true` in src/Spectra.Core/Parsing/TestCaseParser.cs
- [X] T032 [US4] Writer guard: throw when `LayoutOmitted` (message per contracts C2) before writing anything in src/Spectra.CLI/IO/TestFileWriter.cs
- [X] T033 [US4] `BaselineStore.ScanSuiteAsync(…, bool captureLayout = false)` and `BaselineCommandIO.LoadSuiteTestsAsync(…, captureLayout = false)` pass-through; `RelinkSourcesHandler` passes `true` (src/Spectra.CLI/Baseline/BaselineStore.cs, src/Spectra.CLI/Baseline/BaselineCommandIO.cs, src/Spectra.CLI/Commands/Baseline/RelinkSourcesHandler.cs)
- [X] T034 [US4] Read-only callers pass `captureLayout: false` (research R3 table): src/Spectra.CLI/Verification/VerdictFreshness.cs, Commands/Index/IndexHandler.cs (both sites), Commands/Review/AuditGroundingHandler.cs, Commands/Run/RunServices.cs, Commands/Show/ShowHandler.cs, Commands/Validate/ValidateHandler.cs, Dashboard/DataCollector.cs, Commands/Generate/{CompileCriticPromptCommand,CompileRepairPromptCommand,CompileRepairBatchCommand,CompileAnalysisPromptCommand,CompilePromptCommand,IngestAnalysisCommand,IngestVerdictCommand,IngestGroundingCommand}.cs (pre-checks only), Commands/Review/ReviewFlaggedHandler.cs (list only, not accept), Interactive/SuiteScanner.cs — re-check each file for a writer reference before changing it; writer/mixed callers (GroundingWriteBackService, ReviewFlaggedHandler accept, TestRefRewriter, TestDeletionService, BatchReadTestsTool, IngestUpdateCommand loader) keep the default
- [X] T035 [US4] Run T029 + 078/080/081 lossless rows (`TestFileWriterRoundTripTests`, `TestCaseLayoutTests`, `WriterPathPreservationTests`, …) + golden + full suite; Notes

## Phase 7: User Story 5 — a staged update file is read once (P3, F4)

**Goal**: K JSON parses for K staged files (was 3K); codes and messages unchanged.
**Independent test**: `StagedJsonParseCountTests` + golden check (its `--all` step has a `MULTIPLE_TESTS` and an `ID_MISMATCH` file).

- [X] T036 [P] [US5] Write tests/Spectra.CLI.Tests/Generate/StagedJsonParseCountTests.cs (inside a scope): K staged files → `JsonParses` = K for `ingest-update --all`; 1 for `--test-id --from`; per-file outcome/code/message/`failed-<id>.json`/exit 8 identical to the expected strings for blank, no array, truncated, malformed, two tests, id mismatch, schema-invalid, drift; record today's numbers (Phase 2 only, 3K) in Notes first
- [X] T037 [US5] `ExtractedContent` record, `Extract(string?)` (counts `JsonParse()` once), `ArrayObjects(ExtractedContent)`, `ParseAndValidate(ExtractedContent, TestValidator)`; string overloads delegate in src/Spectra.CLI/Generation/GeneratedTestIngestor.cs; move the Phase 2 `JsonParse()` call from `TryParseArray` into `Extract`
- [X] T038 [US5] `UpdatedTestIngestor.IngestAsync(ExtractedContent, …)` (internal); string overload extracts once and delegates; its own `MULTIPLE_TESTS` check uses the extracted array in src/Spectra.CLI/Generation/UpdatedTestIngestor.cs
- [X] T039 [US5] `--all`: extract once per file (after `UNREADABLE`, where `ArrayObjects` ran), pass it to the `MULTIPLE_TESTS`/`ID_MISMATCH` checks and to the ingestor in src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs
- [X] T040 [US5] String overloads (constitution V): for each string overload of `ArrayObjects`, `ParseAndValidate` and `UpdatedTestIngestor.IngestAsync`, count production callers; delete those with 0 (point their tests at the `ExtractedContent` overload) in src/Spectra.CLI/Generation/GeneratedTestIngestor.cs and src/Spectra.CLI/Generation/UpdatedTestIngestor.cs; record the decision per overload in Notes
- [X] T041 [US5] Run T036 + `UpdatedTestIngestorTests`, `MultipleTestsTests`, `IngestUpdateStagingTests`, `IngestUpdateExitCodeTests` + golden + full suite; Notes

## Phase 8: User Story 6 — a smaller, warning-free codebase (P3, F5; F6 done in Phase 2)

**Goal**: dead code from 058/059/069/070 and 095 removed at 0 production references.
**Independent test**: clean build with warnings as errors; `git grep -w <name> -- src` empty for every deleted row; golden check.

- [X] T042 [US6] Re-count every row of research R5 on the current branch (`git grep -nw <name> -- src`, excluding the declaring file, comments and `<see cref>`; also type-name strings and embedded resources); write the final table (name, src refs, test files, verdict) into Notes; a row whose count is no longer 0 becomes "keep"
- [X] T043 [US6] Remove the `snapshot` parameter from `AnalysisRecommendationBuilder.Build` and delete `CountCoveredBehaviors` in src/Spectra.CLI/Generation/AnalysisRecommendationBuilder.cs; drop the argument at src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs:120 and in tests/Spectra.CLI.Tests (AnalysisRecommendationBuilderTests :66/:90); delete the two `CountCoveredBehaviors_*` methods in tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs (list in Notes)
- [X] T044 [US6] Delete the agent tool chain: src/Spectra.CLI/Analysis/DocumentTools.cs, Analysis/TestIndexTools.cs, Agent/Tools/ToolRegistry.cs, Agent/Tools/{GetDocumentMapTool,LoadSourceDocumentTool,SearchSourceDocsTool,ReadTestIndexTool,GetNextTestIdsTool,CheckDuplicatesBatchTool,BatchWriteTestsTool}.cs, Source/DocumentSearcher.cs, Source/SourceDocumentReader.cs, Core `GlobalIdScanner.CreateGlobalAllocatorAsync` and src/Spectra.Core/Index/TestIdAllocator.cs (keep `BatchReadTestsTool`, `BatchProposeUpdatesTool`, `GlobalIdScanner`); delete TestIdAllocatorTests and the `BatchWriteTestsTool` row of tests/Spectra.CLI.Tests/IO/WriterPathPreservationTests.cs; remove the `Microsoft.Extensions.AI.Abstractions` PackageReference from src/Spectra.CLI/Spectra.CLI.csproj once nothing uses it
- [X] T045 [US6] Delete the Testimize generation chain: src/Spectra.CLI/Agent/Testimize/{TestimizeRunner,FieldSpecAnalysisTools}.cs, `testimizeData` params + `FormatTestimizeDataset` in src/Spectra.CLI/Generation/PromptCompiler.cs (and the 3 `null` arguments in CompilePromptCommand.cs/CompileBatchPrompt.cs), src/Spectra.Core/Models/Testimize/{TestimizeDataset,FieldSpec,…}.cs; delete TestimizeRunnerTests, AnalyzeFieldSpecTests (keep the `Testimize` package, `TestimizeConfig`, `testimize check`)
- [X] T046 [US6] Delete the 059 generator leftovers: src/Spectra.CLI/Agent/IAgentRuntime.cs, Agent/Skills/SkillLoader.cs, the dead `GroundedPromptBuilder` members (keep `LoadRelatedCriteriaAsync`), Agent/Analysis/BehaviorAnalysisResult.cs, Interactive/{CountSelector,GapSelector,InteractiveSession,TestTypeSelector,UserDescriptionPrompt}.cs, Session/ folder, Output/{AnalysisPresenter,SuggestionPresenter,ResultPresenter,VerificationPresenter}.cs, Review/{TestReviewer,PendingTestQueue,ReviewPresenter,TestEditor,StreamingRenderer}.cs, Coverage/{GapAnalyzer,GapPresenter}.cs; delete their test files (AnalysisPresenterTests, AnalysisPresenterCoverageTests, CountSelector/SessionStore/SuggestionBuilder/GapAnalyzer/VerificationPresenter tests); move any test that uses a deleted type only as a helper onto a local copy
- [X] T047 [US6] Delete the 058/069/070 leftovers: src/Spectra.CLI/Agent/Critic/CriticResponseParser.cs, Agent/GitHubCliTokenProvider.cs, Agent/AuthResult.cs, Services/CopilotUsageObserver.cs, Services/CostEstimator.cs, `TokenUsageTracker.Record`, `TokenUsageReport.FromTracker`, Provider/ folder, Results/ExtractCriteriaResult.cs, Core Models/Execution/McpToolResponse.cs, Core `SuiteInfo`, Core `RequirementsWriter`; delete or trim their tests (CriticResponseParserTests, parser rows of VerificationIntegrationTests, CopilotUsageObserverTests, CostEstimator tests, tracker `Record` rows, RequirementsWriter tests) and fix the crefs that pointed at them
- [X] T048 [US6] After each of T043–T047: `dotnet build --no-incremental` (0 warnings), golden check, full suite; list every deleted test method/file in Notes with the deleted code it covered
- [X] T049 [US6] Notes: the "recorded, not deleted" rows (LockManager, PathSanitizer, InitResult, ExtractRequirementsResult, BugReportTemplateEngine, BugReportWriter, RequirementsCoverageAnalyzer, IndexRebuildResult), `SourceBaselineHandler.cs:160` double scan, `ConfigCommand.cs:17` stale help example (for 100), and the ImageSharp task

## Phase 9: User Story 7 — the test suite runs faster (P3, F7)

**Goal**: shorter CLI suite, same tests and assertions; reference 420 s on this machine (baseline 597 s idle), measured and recorded, not asserted.
**Independent test**: per-class test counts unchanged; TRX before/after in Notes.

- [X] T050 [US7] Re-measure on the branch after US2–US6 (idle, `--logger trx`, T001 script) to separate product speed-ups from test changes; Notes
- [X] T051 [P] [US7] Write tests/Spectra.CLI.Tests/TestFixtures/TemplateWorkspaces.cs: process-wide `Lazy<Task<string>>` real-init templates (with and without skills) under one temp root; `CopyInto(string dir)`; plus tests/Spectra.CLI.Tests/TestFixtures/TemplateWorkspaceTests.cs comparing a copied tree with a fresh init byte for byte (paths + contents)
- [X] T052 [US7] Use the templates in tests/Spectra.CLI.Tests/TestFixtures/UpdatePromptWorkspace.cs (`InitWithRealTemplateAsync`) and ManifestWorkspace.cs (`InitWithSkillsAsync`); same for the inline init helpers in the classes listed in research R7 where they call the same init; per-class test count unchanged
- [X] T053 [US7] Fewer git processes in tests/Spectra.CLI.Tests/TestFixtures/GitFixture.cs: `Init` = `git init` + one append of the identity/autocrlf/gpgsign settings to `.git/config`; `Commit` skips `rev-parse` when the caller discards the hash (overload or optional flag); keep every helper's observable repo state identical (same config values, same commits)
- [X] T054 [US7] Per-class git templates: `IClassFixture` that builds the class's repo once and a copy per test, for tests/Spectra.CLI.Tests/Commands/Git/SubmoduleNotCheckedOutTests.cs first, then other classes in research R7 whose tests build an identical repo; only where every test of the class passes unchanged on the copy
- [X] T055 [US7] Leave the serial collection only for classes verified free of process-global state (research R7 list: DeletePerformanceTests, SkillsManifestTests, InitClaudeSettingsTests, InitNoMcpEmissionTests, InitHandlerBugTrackingTests, ClaudeSkillsInstallTests, AnalyzeHandlerMigrationTests) — read each class for `RunAsync`, `SetCurrentDirectory`, `Console.Set*`, `LockTimeoutOverride`, `GitAccess.RunnerOverride`, `CancellationManager.Instance` before removing its `[Collection]` (the `ParseCounter` scopes are per async flow and need no serial collection)
- [X] T056 [US7] Run the CLI suite 3× idle; record per changed class: collection, tests before/after, Σ s before/after, technique; the 20-slowest table after; suite wall time against the 420 s reference (measured, not asserted); any flake → revert that change

## Phase 10: Polish

- [X] T057 [P] CHANGELOG.md entry (099: no behaviour change; performance counts; dead code removed incl. the package reference; warnings as errors for CLI/Core; test duration before/after)
- [X] T058 [P] Short CLAUDE.md Recent Changes entry (099) and update the test counts
- [X] T059 Full `dotnet build --no-incremental` (0 CLI/Core warnings) + `dotnet test` (Core / CLI / Execution / Integration, 0 failures); record counts in Notes
- [X] T060 Built-CLI quickstart (specs/099-performance-cleanup/quickstart.md steps 3–6) on a real-init workspace and, with the `c24d23e` build from T002, on a copy of the same folder; diff; Notes
- [X] T061 Fill the remaining Notes: mask list with evidence, counts before/after (2N→N, 3K→K, scans, layout captures), deleted tests, timings
- [ ] T062 Ask the user before committing and merging into `fix/doc-change-update-workflow`; never push
- [ ] T063 After approval: commit + merge per the local-only workflow
- [ ] T064 Remove the `c24d23e` worktree (`git worktree remove`)

## Dependencies

- Phase 1 → Phase 2 (warnings, Core internals for CLI tests, `ParseCounter` wired) → **US1 (golden) blocks US2–US6**:
  every later phase is checked against it.
- US2 (F1) before US4 (F3): F3 sets `captureLayout` on the shared loader created in T019.
- Order F1 → F2 → F3 → F4 (as in plan.md); US3 (F2) and US5 (F4) touch different files and may swap or run alongside.
- US6 (F5) after US2–US5 (fewer moving files while deleting).
- US7 (F7) last: it measures the product changes first (T050), then changes only test fixtures.
- Within a story: count test → record today's number (Phase 2 wiring) → implementation → golden + full run.

## Parallel examples

- T006 with T003–T005 (test file vs. product files).
- T025 (US3 test) and T036 (US5 test) can be written in parallel with US2 work.
- T051 in parallel with T053 (different fixture files); T057 with T058.

## Implementation strategy

US1 first — it is the safety net for everything else (MVP: a committed golden check that is green on `c24d23e`'s
behaviour). Then the P1 performance fix (US2), the P2 fixes (US3, US4), US5, the cleanup (US6) and finally the test-suite
work (US7). Each story leaves the golden check and the full suite green and is a complete increment.

## Notes

(filled during implementation)

- **T001 baseline (`c24d23e`):** Core 1253 / CLI 2326 / Execution 228 / Integration 8 (CLAUDE.md 098 entry; CLI
  re-run here: 2326 passed). Build warnings (Debug, `--no-incremental`): exactly 4 — `DocsApplyMoveCommand.cs(21,26)`
  and `DocsRetireCommand.cs(21,26)` CS0108, `CriteriaLayoutMigrator.cs(30,29)` CS8714, `(31,13)` CS8621. CLI suite
  idle: **597 s wall**, Σ test durations 786 s (CriteriaCommands 495.8 s serial, WorkingDirectory 48.8 s serial,
  GitEnvironment 7.4 s, parallel 233.5 s). 20-slowest table: research.md R7. Script: `trx-classes.py`.
- **T002:** worktree `<scratchpad>/c24d23e` (detached at `c24d23e`).

- Analysis remediation (before implement): U1 Core internals for `Spectra.CLI.Tests` → T008 (moved to Phase 2, since
  `ParseCounter` is used from US2 on); U2 scoped `ParseCounter` (R8) → T009/T010, all count tests use scopes; U3 →
  T018; I1 → T014/T016; G1/G2 → T012; I2 → plan.md order; T1 → FR-006/SC-002; C1 → T040; A1 → SC-008, Phase 9 goal.
- Second analysis remediation: N1 → T010/T037 + research R8 (JSON parses counted only in `TryParseArray`); N2 → spec
  F1 decision + US2 test/scenarios; L1 → FR-002 + research R0; L2 → plan Risks; L3 → plan Scale/Scope + quickstart.
- **L2 grep (2026-10-14, `c24d23e` sources):** `git grep -nE "new Thread\b|new Thread\(|ThreadPool\.UnsafeQueueUserWorkItem|SuppressFlow|UnsafeStart" -- src`
  → no matches. Every counted path flows the async context; re-run before T059 in case F1–F5 add one.
- **US1 golden (T011–T016).** Deviations from the task text: the fixture inputs are inline constants in
  `Golden/GoldenOutputTests.cs` (no `inputs/` folder, no csproj content entry; the golden folder is found from the repo
  root, so record mode writes straight into the worktree); `init --skip-skills` (skills are static embedded content 099
  doesn't touch, and the committed pre-099 tree stays 15 files); the receipts doc got a criterion so
  `compile-prompt --batch 1` compiles in `batch` mode instead of refusing `MISSING_INPUT`; the golden files hold Windows
  newlines, so both facts are `[WindowsFact]` and `Golden/099/** -text` is in `.gitattributes`. Steps: 27 records +
  `files.txt` (text inline, masked; binary as sha256). Exit codes covered: 0, 1 (`validate`, unparseable TC-106), 4
  (`TRIAGE_MISSING`, `GROUP_NOT_FOUND`), 8 (`ingest-update --all` with `MULTIPLE_TESTS` + `ID_MISMATCH`).
- **Masks (T013/T014), each shown to vary between two `c24d23e` captures (14:31:15 vs 14:31:23 runs):**
  `workspace-root` / `workspace-name` (fresh temp folder per run); `timestamp` — every ISO date-time in the capture
  (14 of 15 occurrences differed; the 15th, `date: 2026-10-14T00:00:00`, is the day part of the manifest clock object);
  `clock` — the docs manifest writes `generated_at` as a YAML object (date_time, ticks, millisecond, …), all values
  masked; `local-time` — the progress page's `Last updated: HH:MM:SS`; `file-date` — `**Last Modified:** <date>` in the
  docs index group file (the fixture docs' write date = the run's day); `size` — the byte count of a file whose text a
  mask changed (the manifest was 859 vs 858 bytes); `run-status-file` — `.spectra-progress.html` and
  `.spectra-result.json` reflect earlier commands in the same process (DashboardHandler's static progress manager):
  running the pre-099 fact after the fresh one gave title "Coverage Analysis" instead of "Dashboard Generation"; listed
  but not compared, as `VerificationWorkspace.Tree` does. With these masks two `c24d23e` captures are byte-identical.
- **T015:** golden green on the 099 branch with Phase 2 applied (both facts, together and alone).
- **US2 (F1).** Today's numbers (Phase 2 wiring only, suite of N = 5 test files incl. 1 unparseable): `ingest-tests`
  **12** (5 + a 7-file rescan after writing 2 new tests); `ingest-update --test-id` **10** (with and without
  `--keep-baseline`); unchanged edit (runs the 089 pending-change check) **10** — the check adds no parse;
  `ingest-update --all` **10** (both modes); `--dry-run` **5** (no rescan). After F1: **5 for every variant** — no
  extra parse left to pin. Deviation: no separate `IngestSuiteLoader` file — `Baseline/SuiteScanBuilder` holds the read
  (`ReadAsync`, file-system order, so `existingTests` keeps its old order), the scan (`Build`, used by
  `BaselineStore.ScanSuiteAsync` too, so both scans come from the same code) and `Tests`; the two duplicated
  `LoadExistingTestsAsync` copies are gone. `SuiteScanBuilderTests` compares tests by ID (a scan's `FilePath` is never
  read by the baseline, and an update keeps its in-memory `FilePath`). The "baseline bytes equal to a forced rescan" row
  is covered by `SuiteScanBuilderTests` (same scan) and the golden check (same baseline bytes as `c24d23e`).
- **Checkpoint after US2:** Core 1258 / Execution 228 / Integration 8; CLI 2349 passed, 3 failed — the three US3 count
  rows (`HeadingScanCountTests`, written before the US3 fix, built into that run). One Core failure in that run did not
  reproduce in 4 reruns (1258/1258 each); its name was not captured — checkpoints now write a TRX for Core too.
- **US3 (F2).** Today's numbers: 50 refs into one doc (with raw-section repeats) **55** document heading scans; 3 docs
  with 3 heading lookups + 3 raw-section lookups each **21**; two contexts × 2 refs **4**. After F2: **1 / 3 / 2**.
  Edge kept exact: a document starting with two BOMs after reading (three on disk) was scanned by `GetHeadingsAsync`
  with one BOM removed and by section extraction with both — the lookup keeps its old reading (`DoubleBomDocument` row).
  Core baseline rows (314) and the triage / update-context / batch-compile / plan-section / golden CLI rows (86) green.
- **Checkpoint after US3:** Core 1258 / Execution 228 / Integration 8 / CLI 2353, 0 failures (CLI 433 s wall).
- **US4 (F3).** Today's numbers (parser forced to capture, i.e. pre-US4 behaviour, one temporary line reverted
  after): layout captures per read-only command = every test it parsed — `validate` 3, `show` 1, `index` 3,
  `update-triage --dry-run` 3, `audit-grounding` 3, `ai analyze --coverage` 3, `dashboard --dry-run` 3,
  `compile-critic-prompt` 1, `compile-repair-batch` 2, `compile-prompt --from-description` (captured; row added after
  `--batch`/`--count` were found to parse no test), `compile-analysis-prompt` (captured), `ingest-analysis` 3,
  `ingest-verdict --all` 1, `review-flagged` list 3. After F3: **0** for each, with `FullParses > 0` asserted (a row
  that parses nothing proves nothing). The parser default stays "capture" (R3); read-only callers opt out (17 sites +
  `BatchReadTestsTool(captureLayout: false)` in `AnalyzeHandler` — the coverage row found it; `UpdateHandler` keeps the
  default, it writes orphan marks; auto-link edits text via `FrontmatterUpdater`, not the writer). `TestFileWriter`
  formats before creating the directory, so a refused test leaves nothing behind. The fixture IDs became TC-101…103
  (ingest validates `^TC-\d{3,}$`). Writer matrix in LayoutModeTests: ingest-update edit, `ingest-grounding --all`,
  `delete` of a scalar dependency; relink, orphan marking, review accept and grounding write-back keep their own lossless
  rows (they now fail loudly on a layout-less parse). 622 related CLI tests green.
- **US5 (F4).** Today's numbers: `ingest-update --all` with 3 staged files **9** JSON parses (3K); `--test-id` **2**.
  After F4: **3** and **1**. `GeneratedTestIngestor.Extract` parses once (blank content: no parse; `TryParseArray` is
  the single counted site and now runs only from `Extract`). Failure codes unchanged (EMPTY_CONTENT ×2 messages,
  TRUNCATED, MALFORMED_JSON, MULTIPLE_TESTS, ID_MISMATCH, DRIFT_DETECTED, exit 8). **T040 (constitution V):**
  `ArrayObjects(string)` had 0 production callers left → deleted, its 5 rows in `UpdatedTestIngestorTests` read
  `ArrayObjects(Extract(...))`; `ParseAndValidate(string)` kept (production: `GeneratedTestIngestor.IngestAsync`);
  `UpdatedTestIngestor.IngestAsync(string)` kept (production: `ingest-update --test-id`).

- **US6 (F5) — re-count (T042), production references outside the declaring file (code lines only; comments and
  `cref`s excluded; no reflection / `[JsonSerializable]` / `Activator` in `src/`):** 0 for every R5 delete row, except
  chains whose only users were deleted with them: `BehaviorAnalysisResult` 5 (CountSelector, AnalysisPresenter ×2,
  SuggestionBuilder — all deleted), `CostEstimator` 1 (`TokenUsageReport.FromTracker`). `GroundedPromptBuilder`:
  `BuildSystemPrompt`/`BuildUserPrompt` hits were `CriticPromptBuilder`'s own methods; only `LoadRelatedCriteriaAsync`
  is used (UpdateHandler) — the class keeps that one member.
- **Deleted (T043–T047):** `AnalysisRecommendationBuilder.Build`'s `snapshot` parameter and `CountCoveredBehaviors`;
  the agent tool chain (DocumentTools, TestIndexTools, ToolRegistry, GetDocumentMap/LoadSourceDocument/SearchSourceDocs/
  ReadTestIndex/GetNextTestIds/CheckDuplicatesBatch/BatchWriteTests tools, DocumentSearcher, SourceDocumentReader, Core
  TestIdAllocator + `GlobalIdScanner.CreateGlobalAllocatorAsync`); the `Microsoft.Extensions.AI.Abstractions` package;
  the Testimize generation chain (TestimizeRunner, FieldSpecAnalysisTools, Core FieldSpec/TestimizeDataset,
  `PromptCompiler`'s `testimizeData` parameters + `FormatTestimizeDataset` and its two helpers — the template's
  `testimize_dataset`/`testimize_strategy_name` placeholders are still filled with "", so prompts are byte-identical);
  IAgentRuntime, SkillLoader, BehaviorAnalysisResult, the 5 dead Interactive selectors + FocusDescriptor, the Session/
  folder, Analysis/Suggestion/Result/Verification presenters, TestReviewer + PendingTestQueue/ReviewPresenter/TestEditor,
  StreamingRenderer, GapAnalyzer/GapPresenter, the dead GroundedPromptBuilder members; CriticResponseParser,
  GitHubCliTokenProvider + AuthResult, CopilotUsageObserver, the Provider/ folder, ExtractCriteriaResult, Core
  McpToolResponse (070), SuiteInfo (070), RequirementsWriter (069).
- **Tests removed with the deleted code (they exercised nothing else):** AnalysisPresenterTests,
  AnalysisPresenterCoverageTests, BehaviorAnalysisResultTechniqueTests, CountSelectorTests, SessionStoreTests,
  SuggestionBuilderTests, VerificationPresenterTests, GapAnalyzerTests, TestimizeRunnerTests, AnalyzeFieldSpecTests,
  CriticResponseParserTests, CopilotUsageObserverTests, Core TestIdAllocatorTests, Core RequirementsWriterTests;
  `AnalysisRecommendationBuilderTests.Build_ASnapshot_NoLongerDecidesTheCoveredCount` (only exercised the removed
  parameter; its remaining assertions duplicate the `Build_*` rows); the six `CriticResponseParser` rows of
  `VerificationIntegrationTests` (ParsesGrounded/Partial/HallucinatedVerdict, HandlesMarkdownWrappedJson,
  HandlesMalformedResponse, EndToEnd_GroundedFlow — production verdict parsing is `VerdictIngestor`, with its own tests).
- **Retargeted, not removed:** the two `CountCoveredBehaviors` rows → `Build(...).AlreadyCovered` (renamed
  `CoveredCount_*`); `Build_FiftyTests_…_WithAndWithoutASnapshot` → one call (renamed `…RecommendsTwelve`);
  `WriterPathPreservationTests.BatchWriteTool_NewTest_…` → `PersistenceService_NewTest_RoundTripsEveryTypedField`
  (new tests reach the writer through the persistence service); every `Build(…, snapshot: null, …)` call.
- **Kept (decision):** `TokenUsageTracker.Record`, `TokenUsageReport.FromTracker`, `CostEstimator` — 0 production
  callers, but entangled with live types (`RunSummaryDebugFormatter` formats the tracker; `GenerateResult` /
  `TokenUsageReport` are referenced): removing them needs a decision about the whole token-usage path, not a mechanical
  delete. Recorded for a later spec with the "recorded, not deleted" rows (T049). `GenerateResult` (1 reference).
- **T049 — recorded for a later spec, not deleted:** 0 production references but no tie to 058/059/069/070 (never used,
  or last use removed before 058): `IO/LockManager` (+LockHandle, LockInfo, SuiteLockedException), `PathSanitizer`,
  `Results/InitResult`, `Results/ExtractRequirementsResult`, Core `BugReportTemplateEngine`, `BugReportWriter`,
  `RequirementsCoverageAnalyzer`, `IndexRebuildResult`; the token-usage path (`TokenUsageTracker.Record`,
  `TokenUsageReport.FromTracker`, `CostEstimator`, see above). Also found: `SourceBaselineHandler.cs:160` re-scans every
  suite it loaded at :110 (a double parse outside the ingest commands); `ConfigCommand.cs:17` help example
  `'ai.defaultProvider'` is stale (CLI help text → 100); `SixLabors.ImageSharp` 3.1.12 advisories in Spectra.Execution
  (separate task; blocks `-c Release` restore). Stale doc comments that named deleted types were reworded
  (BoundaryGap, TokenEstimator, PersistentTestIdAllocator, VerdictIngestor, the CLI csproj Testimize comment).
- **Checkpoints:** after US4+US5 — Core 1258 / Execution 228 / Integration 8 / CLI 2376, 0 failures; after US6 — Core
  1228 / Execution 228 / Integration 8 / CLI 2277, 0 failures (`dotnet build --no-incremental`: 0 CLI/Core warnings).
- **T050 (after US2–US6, before F7 test changes):** Σ test durations 628 s (was 786 s); CLI 546 s wall (light editing
  in parallel, so only indicative).
- **US7 (F7) changes:** `GitFixture.Init` = `git init` + one append of the identity/LF/signing settings to
  `.git/config` (1 process instead of 5); `GitFixture.Commit` reads the new hash from the loose ref (no `rev-parse`;
  falls back to it); `CloneInto` appends its three settings in one write (it never set `commit.gpgsign`, still doesn't);
  `TemplateWorkspaces`: the real `spectra init` (with/without skills) once per process, copied into a workspace still in
  its initial state (a workspace that isn't runs the real init — `TemplateWorkspaceTests` proves copy == fresh init,
  byte for byte, and that a non-initial workspace gets the real init's refusal); `UpdatePromptWorkspace` /
  `ManifestWorkspace` use it; `SubmoduleNotCheckedOutTests` builds its default setup once per process and copies the
  clone (6 of 9 tests; the 3 custom setups build their own); `DeletePerformanceTests` left `CriteriaCommands` (explicit
  root, no console / current directory / static override). Kept serial: the Init* classes (InitHandler writes to the
  global Console/AnsiConsole), everything using `RunAsync`. SkillsManifestTests / AnalyzeHandlerMigrationTests were
  candidates but take < 0.5 s together — not moved. RelinkPerformanceTests' fixture is already one `git mv`; its time
  is the product work on 2,500 tests (not changed — no test weakened).
- **T056 final measurement (idle, Debug, 16 cores), CLI suite 3 runs:** **445 s / 418 s / 426 s** wall (median
  **426 s ≈ 7 min 6 s**), vs **597 s** on `c24d23e` (−29 %). Reference 420 s: one run under, the median 6 s over —
  reported, not asserted. Σ test durations 549 / 532 / 520 s (was 786 s); serial `CriteriaCommands` 366 s (was 496 s),
  `WorkingDirectory` 28 s (was 49 s), parallel 134 s (was 234 s). The `UpdateTriagePerformanceTests` 60 s setup bound
  is unchanged and now takes 23 s.

  | Class (20 slowest on c24d23e) | Tests before → after | Σ s before → after |
  |---|---|---|
  | Commands.Git.RelinkPerformanceTests | 1 → 1 | 81.3 → 61.8 |
  | Commands.Git.SubmoduleNotCheckedOutTests | 9 → 9 | 58.1 → 30.4 |
  | Commands.Git.RenameTests | 14 → 14 | 44.1 → 26.4 |
  | Commands.Baseline.UpdateTriagePerformanceTests | 1 → 1 | 39.3 → 23.0 |
  | Commands.InitCommandTests | 23 → 23 | 33.3 → 16.8 |
  | Commands.Git.RelinkSourcesTests | 13 → 13 | 30.0 → 17.7 |
  | Commands.Git.TopologyTests | 3 → 3 | 27.6 → 21.8 |
  | Commands.Git.GitReaderIntegrationTests | 1 → 1 | 21.0 → 7.3 |
  | Commands.Git.HistoryBootstrapTests | 16 → 16 | 20.4 → 16.1 |
  | Commands.Delete.DeletePerformanceTests | 2 → 2 | 18.9 → 20.9 (now parallel) |
  | Dashboard.DataCollectorTests | 30 → 30 | 13.6 → 3.6 |
  | Dashboard.DashboardGeneratorTests | 16 → 16 | 13.6 → 4.9 |
  | Config.LegacyCriticConfigTests | 29 → 29 | 11.7 → 8.4 |
  | Index.LegacyIndexMigratorTests | 12 → 12 | 11.5 → 4.6 |
  | Commands.Git.UpdateAckRelinkTests | 5 → 5 | 8.3 → 6.3 |
  | Skills.CriticModelTests | 19 → 19 | 7.6 → 9.7 (first user builds the skills template) |
  | Commands.Git.BootstrapThresholdTests | 5 → 5 | 7.4 → 5.9 |
  | Commands.Git.GitEnvironmentIsolationTests | 5 → 5 | 7.4 → 3.9 |
  | Integration.QuickstartWorkflowTests | 9 → 9 | 6.4 → 3.3 |
  | IO.TestPersistenceServiceTests | 11 → 11 | 6.3 → 4.5 |

  Part of the drop in classes without fixture changes (DataCollector, LegacyIndexMigrator, …) comes from the product
  changes (no layout capture on read paths) and from less contention in the serial phase.
- **T059 final:** `dotnet build --no-incremental` — 0 warnings from CLI and Core (Execution's ImageSharp restore
  advisories remain, separate task). Core **1228** / CLI **2280** / Execution **228** / Integration **8**, 0 failures,
  CLI three times. Test-count deltas: Core −30 (TestIdAllocatorTests, RequirementsWriterTests) +5 (ParseCounterTests)
  = 1253 → 1228; CLI 2326 → 2280 (+ golden 2, warnings guard 2, SuiteScanBuilder 9, IngestParseCount 8, HeadingScanCount
  6, LayoutMode 20, StagedJsonParseCount 3, TemplateWorkspace 3 = +53; − 99 test cases of deleted code, listed above).
  L2 grep re-run on the final tree: no `new Thread`, `ThreadPool.UnsafeQueueUserWorkItem`, `SuppressFlow`.
- **T060 built-CLI quickstart:** the built `Spectra.CLI.dll` of 099 and of `c24d23e` (worktree) on two identical
  workspaces (doc + hand-edited test with a comment and an unknown key): `init --skip-skills`, `index`, `docs index`,
  `ai source-baseline --init --all-suites`, `validate`, `show TC-101`, `ai update-triage --all-suites`,
  `ai audit-grounding --suite storage`, `dashboard --dry-run`, all `--output-format json`: stdout and exit codes
  identical (after the timestamp and folder-name masks), 9 × exit 0; file trees identical except three clock values
  (`generated_at` in `_checksums.json` and `_index.json`, `Last indexed` in the group index file).

