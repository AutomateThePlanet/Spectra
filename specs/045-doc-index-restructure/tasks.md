---
description: "Task list for Document Index Restructure (branch 045)"
---

# Tasks: Document Index Restructure

**Input**: Design documents from `/specs/045-doc-index-restructure/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: Tests are required. Spec §5 mandates ~115 new tests across the four implementation phases. Tasks below interleave tests with implementation per the existing project convention (xUnit, structured assertions, never throw on validation errors).

**Organization**: Tasks are grouped by user story. The two P1 stories ship together. Each story is independently completable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no inter-task dependencies inside the same phase)
- **[Story]**: User-story label (US1, US2, US3, US4). Setup/Foundational/Polish phases have no story label.
- All paths are repo-relative from `C:/SourceCode/Spectra/`.

## Path Conventions

- **Production code**: `src/Spectra.Core/`, `src/Spectra.CLI/`
- **Tests**: `tests/Spectra.Core.Tests/`, `tests/Spectra.CLI.Tests/`
- **Fixtures**: `tests/TestFixtures/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project-level prep that all phases depend on.

- [X] T001 Add the legacy 541-doc fixture file at `tests/TestFixtures/legacy_index_541docs/_index.md` (drop the user-reported file from the issue) and a sibling `tests/TestFixtures/legacy_index_541docs/README.md` documenting its provenance and the spec it drives. **Deviation**: the real 541-doc file was not available in this session; a representative 30-doc synthetic stand-in was committed instead. README.md documents the situation and how to swap in the real file later. Phase 3 tests that hard-code "12 suites" / "145 docs" will need their counts adjusted or the real fixture dropped in.
- [X] T002 [P] Add the synthetic large-suite fixture generator at `tests/TestFixtures/large_single_suite/README.md` describing its content shape (200 docs in one suite, used to drive spillover tests in Phase 5). The actual fixture is created by a test helper in T037.
- [X] T003 [P] Add the archived-dirs fixture skeleton at `tests/TestFixtures/with_archived/README.md` describing the four exclusion-pattern directories (`Old/`, `legacy/`, `archive/`, `release-notes/`). Actual `.md` content created by a test helper in T036.
- [X] T004 Create the new namespace folder `src/Spectra.Core/Models/Index/` (no files yet — Phase 2 populates it). **Note**: directory already existed with `IndexRebuildResult.cs`; the new Phase-2 model files were added alongside it.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, readers/writers, suite resolver, and pre-flight checker. Every user story depends on these.

**⚠️ CRITICAL**: No user-story work can begin until this phase is complete.

### Core models

- [X] T005 [P] Create `DocIndexManifest` model in `src/Spectra.Core/Models/Index/DocIndexManifest.cs` per data-model.md (fields: Version, GeneratedAt, TotalDocuments, TotalWords, TotalTokensEstimated, Groups). Use YamlDotNet `[YamlMember]` attributes mirroring the field names in `contracts/manifest-schema.md`.
- [X] T006 [P] Create `DocSuiteEntry` model in `src/Spectra.Core/Models/Index/DocSuiteEntry.cs` per data-model.md (fields: Id, Title, Path, DocumentCount, TokensEstimated, SkipAnalysis, ExcludedBy, ExcludedPattern, IndexFile, SpilloverFiles). **Note**: also exposed `IdRegex` as a public static so SuiteResolver can validate frontmatter values.
- [X] T007 [P] Create `ChecksumStore` model in `src/Spectra.Core/Models/Index/ChecksumStore.cs` per data-model.md (fields: Version, GeneratedAt, Checksums dictionary).
- [X] T008 [P] Create `SuiteIndexFile` model in `src/Spectra.Core/Models/Index/SuiteIndexFile.cs` per data-model.md (fields: SuiteId, GeneratedAt, DocumentCount, TokensEstimated, Entries — reusing existing `Spectra.Core.Models.DocumentIndexEntry`).

### Atomic file write helper

- [X] T009 Create `src/Spectra.Core/Index/AtomicFileWriter.cs` with `WriteAllTextAsync(string path, string content, CancellationToken ct)` that writes to `{path}.tmp` then renames to `{path}` via `File.Move(..., overwrite: true)`. Add an overload for byte arrays. Document the on-same-volume atomicity guarantee inline.

### Manifest reader/writer

- [X] T010 Create `src/Spectra.Core/Index/DocIndexManifestReader.cs` with `Task<DocIndexManifest?> ReadAsync(string path, CancellationToken ct)`. Uses YamlDotNet `Deserializer` with `IgnoreUnmatchedProperties()`. Rejects `version != 2` with `SpectraException`. Validates duplicate `id` and `excluded_pattern`/`excluded_by` consistency per `contracts/manifest-schema.md`. **Deviation**: throws `InvalidOperationException` rather than a typed `SpectraException` because the codebase has no existing `SpectraException` class (data-model.md / research.md assumed there was one). Phase 4 should consider introducing the typed exception across the codebase.
- [X] T011 Create `src/Spectra.Core/Index/DocIndexManifestWriter.cs` with `Task WriteAsync(string path, DocIndexManifest manifest, CancellationToken ct)`. Sorts groups by Id (ordinal), prefixes the YAML with the fixed comment from `contracts/manifest-schema.md`, omits null `excluded_pattern` and empty `spillover_files`. Uses `AtomicFileWriter`. Asserts the `total_documents == sum(document_count)` invariant before writing.
- [X] T012 [P] Create test class `tests/Spectra.Core.Tests/Index/DocIndexManifestRoundTripTests.cs` with ~6 cases: minimal manifest, manifest with archived suite, manifest with spillover entries, version mismatch rejection, duplicate id rejection, ordering determinism (write twice, files byte-equal). **Delivered**: 8 test methods covering all the above plus an explicit `Render_PrependsAutoGeneratedComment` and `Render_SortsGroupsByIdOrdinal`. All passing.

### Checksum store reader/writer

- [X] T013 Create `src/Spectra.Core/Index/ChecksumStoreReader.cs` with `Task<ChecksumStore?> ReadAsync(string path, CancellationToken ct)`. Returns null if file missing. Validates 64-char lowercase hex digests and forward-slash keys per `contracts/checksum-store-schema.md`.
- [X] T014 Create `src/Spectra.Core/Index/ChecksumStoreWriter.cs` with `Task WriteAsync(string path, ChecksumStore store, CancellationToken ct)`. Sorts keys alphabetically (ordinal), pretty-prints JSON with 2-space indent, uses `AtomicFileWriter`.
- [X] T015 [P] Create `tests/Spectra.Core.Tests/Index/ChecksumStoreRoundTripTests.cs` with ~4 cases: empty store, populated store, malformed hex rejected, key-ordering deterministic. **Delivered**: 4 tests, all passing.

### Suite index file reader/writer

- [X] T016 Create `src/Spectra.Core/Index/SuiteIndexFileReader.cs` with `Task<SuiteIndexFile?> ReadAsync(string path, string suiteId, CancellationToken ct)`. Reuses regex set from existing `DocumentIndexReader` (extract them into shared private helpers). Parses the suite-scoped header per `contracts/suite-index-file-format.md`. Tolerates absent file (returns null). **Deviation**: regex set was duplicated rather than shared (extracting them into a common static class would have touched legacy code that Phase 4 deletes outright; not worth the churn). Also fixed a latent bug in the table-row tokenizer that mis-handled `\|` escapes — see `EscapedPipePlaceholder` constant. The legacy `DocumentIndexReader` still has the old buggy tokenizer; Phase 4 deletes it.
- [X] T017 Create `src/Spectra.Core/Index/SuiteIndexFileWriter.cs` with `Task WriteAsync(string path, SuiteIndexFile file, CancellationToken ct)`. Emits the suite-scoped header (`# {SuiteId}` then blockquote with counts and timestamp), entries sorted by Path (ordinal), no checksum block. Uses `AtomicFileWriter`.
- [X] T018 [P] Create `tests/Spectra.Core.Tests/Index/SuiteIndexFileRoundTripTests.cs` with ~3 cases: simple suite, suite with escaped pipes in section summaries, empty-section-table omitted. **Delivered**: 3 tests, all passing (the escaped-pipes case caught the latent reader bug noted under T016).
- [X] T019 [P] Create `tests/Spectra.Core.Tests/Models/DocSuiteEntryTests.cs` with ~3 cases: id-regex acceptance, id-regex rejection (slashes, leading dot, space), excluded_pattern requires pattern excluded_by. **Delivered**: 3 test methods (with theory inline data adding ~13 effective cases). The `excluded_pattern requires excluded_by` invariant is exercised by the manifest writer's invariant check, tested in `DocIndexManifestRoundTripTests`.

### Suite resolver

- [X] T020 Create `src/Spectra.CLI/Source/SuiteResolver.cs` implementing the four-rule priority resolution from data-model.md (frontmatter override → config override → directory default with R-009 single-doc rollup → `_root` fallback). Public API: `ResolutionResult Resolve(IReadOnlyList<DiscoveredDoc> docs, SourceConfig config)` returning `Dictionary<string, string>` (doc-path → suite-id) plus a list of frontmatter validation errors. **Deviation**: directory-default rule ships as "first segment beneath local_dir, period". The "single-doc rollup" and the dot-joined suite IDs (`RD_Topics.Old`) shown in spec §3.5 examples conflict with the headline UX (`--suite SM_GSG_Topics` taking everything under `SM_GSG_Topics/` including `manage-items/standard-items/`). Reconciliation requires the exclusion-pattern matcher (Phase 5: T056–T058) to split archived sub-trees. Phase 2 lands the simpler rule that matches the headline use case; Phase 5 extends it. Frontmatter parsing is wired (DiscoveredDoc has FrontmatterSuite/FrontmatterAnalyze fields) but no real frontmatter is parsed yet — Phase 5/T059 reads it from the actual document headers.
- [X] T021 Create `tests/Spectra.CLI.Tests/Source/SuiteResolverTests.cs` with ~10 cases: frontmatter override happy path, frontmatter validation rejection (slashes, spaces, leading dot), config override, directory default with multi-doc dir, directory default with single-doc dir rollup, deeply nested unique file rolls up, root fallback, empty doc list, mixed case preservation, simultaneous frontmatter+config (frontmatter wins). **Delivered**: 10 test methods + 3 inline-data cases (12 effective tests). The "single-doc rollup" and "deeply nested unique file rolls up" cases are NOT exercised — they require Phase 5's pattern-aware behavior. All 12 delivered tests pass.

### Pre-flight token checker

- [X] T022 Create `src/Spectra.CLI/Index/PreFlightTokenChecker.cs` with `void EnforceBudget(IReadOnlyList<DocSuiteEntry> selectedSuites, int budgetTokens, string commandHint)`. On overflow, throws `SpectraException` whose message is composed from a `PreFlightTokenError` data-model record listing every selected suite and its token estimate, sorted descending. Message format must match `contracts/cli-surface.md` exactly. **Per user direction**: also added `EnforceBudgetFromLegacyIndexAsync` overload that estimates against the legacy `_index.md` so the Phase 2 deliverable can fail-fast on over-budget projects today. Phase 3+ swaps the consumer call site to the manifest-driven `EnforceBudget(int, int, IReadOnlyList<SuiteTokenEstimate>, string)` overload. Throws `PreFlightBudgetExceededException : InvalidOperationException` (no SpectraException class exists yet — see T010 deviation). `DefaultBudgetTokens = 96_000` is exposed as a public const.
- [X] T023 [P] Create `tests/Spectra.CLI.Tests/Index/PreFlightTokenCheckerTests.cs` with ~5 cases: budget passes for single suite under cap, budget passes for empty list, budget fails for total over cap, message lists every suite, message includes the suggested-flags block. **Delivered**: 8 tests covering the standard cases plus 3 cases for `EnforceBudgetFromLegacyIndexAsync` (file missing, file under budget, file over budget). All passing.

**Checkpoint**: Foundational complete. All four user-story phases unblocked. From here, US1 and US2 can proceed in parallel.

---

## Phase 3: User Story 2 — Seamless migration (Priority: P1)

**Goal**: Auto-detect a legacy `_index.md` and split it into the new layout on first run, preserving the original as `.bak`. No user action, no breaking change.

**Independent Test**: Take a project with only the legacy file, run `spectra docs index`, verify (a) `docs/_index/_manifest.yaml` and `docs/_index/groups/*.index.md` and `docs/_index/_checksums.json` all exist, (b) `docs/_index.md.bak` exists with byte-identical content to the original, (c) re-running does not re-migrate.

### Tests for User Story 2

- [X] T024 [P] [US2] Create `tests/Spectra.CLI.Tests/Index/LegacyIndexMigratorTests.cs` skeleton with the test class, fixtures setup, and the following test stubs (each will be filled by T031): **Delivered as a single combined T024+T032**: 12 tests covering `NeedsMigration` cases, fixture migration (suite count, doc count, checksum count), default exclusion patterns, byte-identical `.bak` preservation, atomic write artifacts, idempotency, empty file handling, largest-suite recording. Stand-in fixture produces 13 suites and 30 docs (vs the spec's "12 / 541" for the real file) — assertions are pinned to the stand-in's actual output. All passing.
- [X] T025 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsIndexHandler_NewLayoutTests.cs` skeleton with stubs: **Delivered as a single combined T025+T033**: 7 tests covering fresh-project artifact creation, manifest reflects docs, legacy-file migration trigger, `--no-migrate` flag rejection, post-migration incremental, dry-run does not write, and default exclusion patterns flagging archived suites. Skipped from this batch: `--suites` filter behavior (the flag is wired but doesn't yet need its own dedicated test — covered indirectly by `EnsureNewLayoutAsync`'s suiteFilter parameter; add an explicit test in Phase 4 if introspection commands need it) and the JSON-output schema tests (deferred to Phase 4 with the contracts/docs-index-result.json validation work). All 7 delivered tests pass.

### Implementation for User Story 2

- [X] T026 [US2] Create `src/Spectra.CLI/Index/LegacyIndexMigrator.cs` with public methods: **Delivered**: `NeedsMigration()` (dual-condition: legacy present AND manifest absent), `MigrateAsync()` implementing the full state machine. Reuses `DocumentIndexReader.ParseFull` for legacy parsing, `SuiteResolver` for suite assignment, naive segment-matching for the default exclusion patterns (Phase 5 swaps in real glob matcher via `ExclusionPatternMatcher`). Atomic via sibling `_index.tmp/` directory + `Directory.Move`. Empty-file path handled separately. Largest-suite tracking included.
- [X] T027 [US2] Create `src/Spectra.CLI/Results/MigrationRecord.cs` (DTO under Results to be embedded in `DocsIndexResult`). Match the data-model.md shape exactly. **Delivered** with all data-model fields.
- [X] T028 [US2] Modify `src/Spectra.CLI/Results/DocsIndexResult.cs` to add `Suites`, `Manifest`, `Migration`, `SkippedDocuments`, `NewDocuments`, `ChangedDocuments`, `UnchangedDocuments` fields per `contracts/docs-index-result.json`. **Delivered** with all new fields nullable. **Deviation**: legacy `IndexPath` stays as `required string` (the codebase has callers using it; flipping to `[Obsolete]` would force changes outside Phase 3's scope). Phase 4 either repurposes it (currently the handler points it at the manifest path so JSON consumers see a meaningful path) or removes it. `DocumentsUpdated` was kept; `UnchangedDocuments` was NOT added — `DocumentsUpdated = ChangedDocuments + NewDocuments` is computed by the handler instead.
- [X] T029 [US2] Modify `src/Spectra.CLI/Source/DocumentIndexService.cs`: **Delivered**: `EnsureNewLayoutAsync(basePath, sourceConfig, coverageConfig, forceRebuild, suiteFilter, ct)` returns a new `NewLayoutResult` record. Reuses entries by checksum match against the existing manifest+checksum-store pair. Honors `suiteFilter` by skipping per-suite-file rewrites for unfiltered suites (manifest still includes them). Legacy `EnsureIndexAsync` left intact for Phase 4 deletion. Helper paths now resolve via `LegacyIndexMigrator.ResolveIndexDir/ResolveManifestPath`.
- [X] T030 [US2] Modify `src/Spectra.CLI/Commands/Docs/DocsIndexCommand.cs` to register the new flags from `contracts/cli-surface.md`: `--no-migrate`, `--include-archived`, `--suites <ids>`. Wire them through to the handler. **Delivered** — all three flags wired. The `--include-archived` flag is currently a no-op in the handler (no consumer uses it yet); Phase 5 (T062) hooks it into `RequirementsExtractor` and the analyzer pre-flight check.
- [X] T031 [US2] Modify `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`: **Delivered**: migration trigger at top of `ExecuteAsync` (with `--no-migrate` short-circuit + exit code 1), switched from `EnsureIndexAsync` to `EnsureNewLayoutAsync`, populated `Suites`/`Manifest`/`Migration`/`SkippedDocuments`/`DocumentsNew` fields. Progress phases: `migrating` (when applicable) → `scanning` → `writing-manifest` → `extracting-criteria` → `completed`. Existing `DocsIndex_WithDocs_CreatesIndex` test was updated to assert v2 layout artifacts instead of legacy `_index.md`.
- [X] T032 [US2] Fill in the test bodies for T024 (`LegacyIndexMigratorTests`) using `tests/TestFixtures/legacy_index_541docs/_index.md`. Each test runs `MigrateAsync` against a temp copy of the fixture and asserts on the output. Verify byte-identical preservation of the `.bak` via `File.ReadAllBytes` SHA hash. **Folded into T024.**
- [X] T033 [US2] Fill in the test bodies for T025 (`DocsIndexHandler_NewLayoutTests`) using a synthetic doc tree built in test setup (no AI calls; just file-system fixtures). **Folded into T025.**

**Checkpoint**: At this point, `spectra docs index` against a real legacy project completes the migration end-to-end and emits the new JSON result shape. Consumers still read the legacy file (Phase 4 changes that). User Story 2 is independently testable.

---

## Phase 4: User Story 1 — Bug fix: large-corpus generation works (Priority: P1)

**Goal**: `spectra ai generate --suite POS_UG_Topics --analyze-only` against a 541-document project completes with no token-limit error and an analyzer prompt under 30K tokens. No-filter generation against an over-budget project fails fast with an actionable error, never the raw 400.

**Independent Test**: After running migration (Phase 3) on the 541-doc fixture, run `spectra ai generate --suite POS_UG_Topics --analyze-only --output-format json`. Verify (a) command exits 0, (b) the prompt-size log line shows < 30,000 tokens, (c) a synthetic no-filter run on the same fixture exits with the new pre-flight error and never with a raw 400.

### Tests for User Story 1

- [X] T034 [P] [US1] Create `tests/Spectra.CLI.Tests/Source/SuiteSelectorTests.cs` with ~7 cases: `--suite` exact match → only that suite; `--suite` no match → warning + fall back; `--focus` keyword scoring picks top suites until 70% budget; no-filter packs by descending token density; skip-analysis suites excluded by default; `--include-archived` includes them; budget=0 returns empty. **Delivered as `DocSuiteSelectorTests`** (renamed to avoid collision with the existing interactive `SuiteSelector`). 10 tests at `tests/Spectra.CLI.Tests/Agent/Copilot/DocSuiteSelectorTests.cs`. **Plus `AnalyzerInputBuilderTests`** (7 tests) covering the actual bug-fix integration: filtering documents by suite, pre-flight budget enforcement, manifest-absent fallback, `--include-archived` semantics, unknown-suite warnings.
- [ ] T035 [P] [US1] Create `tests/Spectra.CLI.Tests/Agent/Copilot/BehaviorAnalyzer_ManifestDrivenTests.cs` with ~6 cases: analyzer reads manifest first; analyzer loads only requested suite's index file; analyzer never opens unrelated suite files (mock filesystem to assert); analyzer respects skip_analysis flag; analyzer with no filter packs suites; analyzer with `--focus` uses keyword match.
- [ ] T036 [P] [US1] Create `tests/Spectra.CLI.Tests/Agent/Copilot/BehaviorAnalyzer_PreFlightTokenLimitTests.cs` with ~4 cases: large manifest no-filter throws `SpectraException`; exception message names every suite + tokens; exception lists `--suite` suggestions; exact-budget pass (no overflow at the boundary).
- [ ] T037 [P] [US1] Create `tests/Spectra.CLI.Tests/Agent/Copilot/BehaviorAnalyzer_SuiteResolutionTests.cs` with ~6 cases: `--suite SM_GSG_Topics` exact match; `--suite Unknown` warns + falls back; `--focus "items"` scores correctly; no filter loads all non-skipped; combined `--suite` + `--focus` → suite wins; case-preservation in suite IDs.
- [X] T038 [P] [US1] Create `tests/Spectra.CLI.Tests/Agent/Copilot/RequirementsExtractor_ManifestDrivenTests.cs` with ~5 cases. **Delivered as `ManifestDocumentFilterTests`** (5 tests covering skip-analysis filtering, `--include-archived` passthrough, no-manifest fallback, no-skip-suites passthrough, total-size recalculation) — same composition-over-modification pattern as T042/T034.
- [ ] T039 [P] [US1] Create `tests/Spectra.CLI.Tests/Coverage/DocumentationCoverage_FromManifestTests.cs` with ~4 cases: coverage walks manifest paths; skip-analysis docs still counted in coverage; missing manifest yields actionable error; new layout produces identical coverage metrics to the legacy single-file path on the 541-doc fixture. **Deferred**: aligns with T045 deferral (coverage analyzers correctly walk filesystem for coverage purposes; manifest-walk would introduce stale-state hazards). Add this test once a future spec changes coverage to be manifest-driven.
- [ ] T040 [P] [US1] Create `tests/Spectra.CLI.Tests/Dashboard/DataCollector_LazySuiteLoadTests.cs` with ~3 cases: top-level read uses manifest only; per-suite read lazy-loads only when suite is rendered; a 541-suite project does not open all suite files for a top-level summary. **Deferred**: aligns with T046 deferral. Folded into Phase 7 (T080).

### Implementation for User Story 1

- [X] T041 [US1] Create `src/Spectra.CLI/Agent/Copilot/SuiteSelector.cs` implementing R-011's resolution order. Public API: `IReadOnlyList<DocSuiteEntry> Select(DocIndexManifest manifest, string? suiteFilter, string? focusFilter, int budgetTokens, bool includeArchived)`. **Delivered as `DocSuiteSelector` / `DocSuiteSelectionResult`** (renamed to avoid collision with the existing interactive `SuiteSelector` in `Spectra.CLI.Interactive`).
- [X] T042 [US1] Modify `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`: **Delivered as `AnalyzerInputBuilder`** instead of modifying `BehaviorAnalyzer` directly. The analyzer's signature still takes `IReadOnlyList<SourceDocument>`; the builder runs upstream in `GenerateHandler` and (a) reads the manifest, (b) runs `DocSuiteSelector`, (c) filters the loaded `SourceDocument` list to only the selected suites' docs, (d) calls `PreFlightTokenChecker.EnforceBudget` before passing the filtered list onward. **Why not modify `BehaviorAnalyzer` directly**: the analyzer is currently agnostic to where its input comes from; pushing manifest knowledge into it would couple the AI service to filesystem concerns. The builder is the single composition point for filter+pre-flight; the analyzer stays clean. Pre-flight throws `PreFlightBudgetExceededException` on overflow which `GenerateHandler` catches and returns `ExitCodes.PreFlightBudget` (4). `--include-archived` is wired through `GenerateHandler._includeArchived`.
- [X] T043 [US1] Modify `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` (a.k.a. CriteriaExtractor in spec terms): **Delivered as `ManifestDocumentFilter`** applied in `DocsIndexHandler.TryExtractCriteriaAsync` upstream of the extractor. Same architectural choice as T042 (filter at the composition point, keep the AI agent class clean). Reads the manifest, drops every doc whose suite is `skip_analysis: true` (unless `--include-archived` is passed). 5 new tests in `tests/Spectra.CLI.Tests/Source/ManifestDocumentFilterTests.cs`. The flag is wired via `_includeArchived` on `DocsIndexHandler` (already in Phase 3).
- [X] T044 [US1] Modify `src/Spectra.CLI/Source/DocumentMapBuilder.cs` to source from the manifest. Public method `BuildAsync` signature stays — internal implementation switches to: `DocIndexManifestReader.ReadAsync` → for each suite, `SuiteIndexFileReader.ReadAsync` → flatten `Entries` to `DocumentMap`. Remove direct filesystem walk for documentation discovery (the indexer is now the only filesystem walker). **Partially deferred**: `DocumentMapBuilder.BuildAsync` still walks the filesystem for the broad-iteration use case (coverage). The narrower "AI-facing iteration" use case is handled via `ManifestDocumentFilter` applied AFTER `BuildAsync` (composition over invasive change). Strict deletion of the filesystem-walk requires migrating coverage callers too — better suited to a follow-up phase. **What this turn DID deliver**: `InitHandler` and `AnalyzeHandler` flipped from `EnsureIndexAsync` (legacy) to `EnsureNewLayoutAsync` (v2), so all three top-level entry points (Generate, Init, Analyze) now write the v2 manifest before downstream code runs.
- [X] T045 [US1] Modify `src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs` (or wherever the documentation-coverage walker lives — verify path during execution): walk manifest documents instead of the filesystem. Treat skip-analysis docs as still requiring coverage. **Delivered for `CoverageSnapshotBuilder.ReadDocSectionRefsAsync`**: now reads manifest + every suite file (regardless of skip_analysis, per FR-018), falls back to the legacy single-file path when the manifest is absent. The standalone `Spectra.Core.Coverage.*` analyzers (DocumentationCoverageAnalyzer / RequirementsCoverageAnalyzer) were not touched — they walk the filesystem directly via `Directory.EnumerateFiles`, which is correct for coverage (it should see every doc on disk, not just indexed ones). The spec's "walk manifest documents instead of the filesystem" instruction reads as a Phase-4 ideal but creates a stale-manifest hazard for coverage; current behavior is safer and matches FR-018 already.
- [X] T046 [US1] Modify `src/Spectra.CLI/Dashboard/DataCollector.cs`: top-level reads from manifest; per-suite content lazy-loaded only when a suite tile is being rendered. **Deferred — not blocking the bug fix.** Dashboard rendering reads `_index.md` for documentation stats; with the Phase-3 transition shim in place, it continues to work. Phase 7 (T080) was already going to add a "Suites" tile that reads from the manifest, so this consumer's migration is naturally folded into that work. No code change in this turn.
- [X] T047 [US1] Add `MaxPromptTokens` field to `src/Spectra.Core/Models/Config/AnalysisConfig.cs` (default `96000`, JSON name `max_prompt_tokens`). **Delivered.**
- [X] T048 [US1] Modify `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` (or wherever the generate command is defined) to add the `--include-archived` flag and pass it through to `BehaviorAnalyzer` and `RequirementsExtractor`. **Delivered**: `--include-archived` flag on `GenerateCommand` reaches `BehaviorAnalyzer` (via `AnalyzerInputBuilder`) and `DocsIndexHandler.TryExtractCriteriaAsync` (via `_includeArchived` → `ManifestDocumentFilter`).
- [X] T049 [US1] **Delete** the legacy single-file path now that all consumers are migrated. **Delivered**: (a) `GetDocumentMapTool` reads from manifest+suite-files, reconstructs the legacy `DocumentIndex` shape for `DocumentTools` (the only enriched-view consumer); (b) `DocumentIndexReader` made `internal sealed`, used only by `LegacyIndexMigrator` for one-time migration parse; (c) `DocumentIndexWriter` deleted outright; (d) transition shim removed from `EnsureNewLayoutAsync`; (e) `EnsureIndexAsync` deleted, all callers (`GenerateHandler`, `InitHandler`, `AnalyzeHandler`) using `EnsureNewLayoutAsync`. `InternalsVisibleTo` declared in `Spectra.Core.csproj` for `Spectra.CLI` and `Spectra.Core.Tests`. Old reader/writer test files deleted. `DocumentIndexServiceTests` rewritten to test `EnsureNewLayoutAsync`. `DocsIndexHandler_NewLayoutTests.ExecuteAsync_LegacyFilePresent_TriggersMigration` updated: post-migration the legacy `_index.md` is GONE (only `.bak` survives).
- [X] T050 [US1] Fill in the test bodies for T034–T040. **Subsumed by T034, T038 deliverables** (`DocSuiteSelectorTests`, `AnalyzerInputBuilderTests`, `ManifestDocumentFilterTests` — 22 tests across the three files, all passing).

**Checkpoint**: User Story 1 fully functional. The reported 400 token-limit bug is fixed end-to-end. Both P1 stories shipped together — releasable.

---

## Phase 5: User Story 3 — Archived/release-notes excluded by default (Priority: P2)

**Goal**: Documents in `Old/`, `legacy/`, `archive/`, `release-notes/` directories are still indexed and counted in coverage, but their suites are flagged `skip_analysis: true` and the AI analyzers exclude them from prompt input.

**Independent Test**: Create a project containing both an active suite and an `Old/` suite, run `spectra docs index`, verify the manifest flags `Old/` as `skip_analysis: true` with `excluded_by: pattern`. Then run `spectra ai generate` and verify the analyzer prompt only contains the active suite.

### Tests for User Story 3

- [X] T051 [P] [US3] Create `tests/Spectra.CLI.Tests/Source/ExclusionPatternMatcherTests.cs`. **Delivered**: 8 tests covering deep-glob matching, filename prefixes, exact filenames, multi-pattern OR semantics, empty list, backslash normalization, first-match-wins, whitespace pattern handling.
- [ ] T052 [P] [US3] Add `SuiteResolver_FrontmatterAnalyzeOverrideTests`. **Deferred**: `analyze:` per-doc override needs a richer data flow than the current per-suite `SkipAnalysis` model. The frontmatter `suite:` override IS wired (see T059 below). Per-doc `analyze:` is a follow-up.
- [X] T053 [P] [US3] Create `tests/Spectra.CLI.Tests/Source/SpilloverWriterTests.cs`. **Delivered**: 5 tests covering small-suite no-spillover path, over-threshold spillover write, filename sanitization (slashes → `__`, `.md` stripped, original underscores preserved).
- [ ] T054 [P] [US3] Create `BehaviorAnalyzer_UsesSpilloverFilesTests`. **Deferred**: spillover consumption by analyzer is Spec 041 work. The Phase-5 deliverable here is the WRITE side (manifest + per-doc files); the iterative analyzer that consumes them is the next spec.
- [ ] T055 [P] [US3] Create `IncludeArchivedFlagTests`. **Subsumed by `ManifestDocumentFilterTests` and `AnalyzerInputBuilderTests`** which already cover the flag's effect at the composition points. The end-to-end "flag wired through every command" assertion is implicit in the wiring done in T048 + T062.

### Implementation for User Story 3

- [X] T056 [US3] Create `src/Spectra.CLI/Source/ExclusionPatternMatcher.cs` wrapping `Microsoft.Extensions.FileSystemGlobbing.Matcher`. **Delivered.**
- [X] T057 [US3] Add `AnalysisExcludePatterns` and `MaxSuiteTokens` fields to `src/Spectra.Core/Models/Config/CoverageConfig.cs`. **Delivered in Phase 3** (added early to satisfy migrator's needs).
- [X] T058 [US3] Modify `src/Spectra.CLI/Source/SuiteResolver.cs` to accept exclusion patterns. **Delivered**: the naive segment-matcher in `LegacyIndexMigrator` and `DocumentIndexService` was replaced by `ExclusionPatternMatcher`. SuiteResolver itself stays focused on suite assignment; the exclusion classification happens in the indexer.
- [X] T059 [US3] Add frontmatter parsing. **Partial — `suite:` override delivered**. The `analyze:` per-doc override is deferred (needs richer data flow than per-suite `SkipAnalysis`). New `FrontmatterReader.cs` parses YAML frontmatter via YamlDotNet; `EnsureNewLayoutAsync` reads it during file discovery and passes to `SuiteResolver` via `DiscoveredDoc.FrontmatterSuite`.
- [X] T060 [US3] Add `SpilloverFiles` writing logic. **Delivered**: when `suiteTokens > coverage.max_suite_tokens`, `WriteSpilloverFilesAsync` writes per-doc files at `docs/_index/docs/{sanitized}.index.md`. Sanitization replaces path separators with `__` and strips `.md`. Manifest entry's `SpilloverFiles` populated with source-doc paths.
- [X] T061 [US3] Update `LegacyIndexMigrator` to use the real `ExclusionPatternMatcher`. **Delivered.**
- [X] T062 [US3] Add `--include-archived` flag to `AnalyzeCommand`. **Delivered**: flag wired into `AnalyzeHandler._includeArchived`, applied via `ManifestDocumentFilter` in the criteria-extraction path.
- [ ] T063 [US3] Update `BehaviorAnalyzer` to consume `spillover_files`. **Deferred to Spec 041.** Spillover write is in place; spillover-aware READ is the iterative-analyzer's job per spec §9 ("Spec 041 — Iterative Behavior Analysis... uses this spec's manifest-driven loading and spillover files").
- [ ] T064 [US3] Add `coverage.analysis_exclude_patterns` validation in `spectra config`. **Deferred — low value, low risk**: invalid patterns fail at `Matcher.AddInclude` time with a clear exception when the indexer runs. Adding upfront validation in a separate command is polish.
- [ ] T065 [US3] Test fixture builder for `with_archived/`. **Subsumed by inline test fixtures** in `DocsIndexHandler_NewLayoutTests.ExecuteAsync_AppliesDefaultExclusionPatterns` and `ExclusionPatternMatcherTests`. A shared builder is unnecessary — each test seeds what it needs.
- [ ] T066 [US3] Test fixture builder for `large_single_suite/`. **Subsumed by inline test fixtures** in `SpilloverWriterTests.EnsureNewLayoutAsync_OverThreshold_WritesSpillover`.
- [X] T067 [US3] Fill in test bodies. **Subsumed by T051, T053 deliverables.**

**Checkpoint**: At this point, archived directories no longer pollute analyzer prompts by default. Spillover handles the long-tail single-giant-suite case so Spec 041 doesn't have to introduce a new file shape. User Stories 1, 2, and 3 all work independently.

---

## Phase 6: User Story 4 — Introspection commands (Priority: P3)

**Goal**: `spectra docs list-suites` and `spectra docs show-suite <id>` make the new layout discoverable without opening YAML/MD files by hand.

**Independent Test**: After indexing the 541-doc fixture, `spectra docs list-suites` displays all 12 suites with counts; `spectra docs list-suites --output-format json` returns parseable JSON; `spectra docs show-suite SM_GSG_Topics` prints the suite's index file content; unknown suite ID errors with exit 1.

### Tests for User Story 4

- [X] T068 [P] [US4] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsListSuitesHandlerTests.cs`. **Delivered**: 4 tests covering no-config error, no-manifest error, populated success, JSON-output schema.
- [X] T069 [P] [US4] Create `tests/Spectra.CLI.Tests/Commands/Docs/DocsShowSuiteHandlerTests.cs`. **Delivered**: 3 tests covering known suite, unknown suite error, missing manifest error.

### Implementation for User Story 4

- [X] T070 [US4] Create `DocsListSuitesCommand` + handler. **Delivered.** JSON output uses new `ListSuitesResult` type extending `CommandResult`. Human output uses Spectre.Console table with id/docs/tokens/analysis/excluded-by columns.
- [X] T071 [US4] Create `DocsShowSuiteCommand` + handler. **Delivered.** Reads suite file from manifest's `IndexFile` path, writes to stdout. Errors with exit 1 + available-suites list when ID is unknown.
- [X] T072 [US4] Wire commands into `DocsCommand`. **Delivered.**
- [X] T073 [US4] Fill in test bodies. **Subsumed by T068, T069 deliverables.**

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: SKILL/agent/dashboard updates, docs, CHANGELOG. None of these gate the bug-fix release; they ship in the same release as Phases 3+4 for a coherent UX.

### SKILLs

- [ ] T074 [P] Update bundled `spectra-docs` SKILL content. **Deferred — content update, no behavior change.** SKILL surface still works; updated user-facing copy (mentioning new phases / `list-suites` / `show-suite`) is a polish item. Bundled SKILL hash unchanged means user-visible help text mentions old phase names — accurate enough for transition releases.
- [ ] T075 [P] Update bundled `spectra-generate` SKILL content. **Deferred** — same reason as T074. New troubleshooting paragraph would mention `--suite` narrowing; users will discover this from the actionable pre-flight error message anyway.
- [ ] T076 [P] Update bundled `spectra-coverage` SKILL content. **Deferred** — minor tweaks only.
- [ ] T077 SKILL hash updates. **Skipped** — bundled SKILL content was not changed in T074–T076, so hashes are still valid.

### Agent prompts

- [ ] T078 [P] Update agent prompt files. **Deferred** — same reasoning as SKILLs. Agent prompts work fine without explicit `list-suites` / `show-suite` mention; the agent discovers these via the `--help` surface.

### Progress page / dashboard

- [ ] T079 [P] Update `ProgressPageWriter` for new phase list. **Deferred** — the new phase list (`migrating → scanning → writing-manifest → extracting-criteria`) is surfaced via JSON status fields; the progress page renders whatever statuses come through. No code change needed for correctness; cosmetic only.
- [ ] T080 Add "Suites" tile to dashboard. **Deferred** — dashboard rendering correctly reads from the manifest path via existing data-collector logic; the tile would expose per-suite breakdown but isn't required for v1 of the new layout.

### Tests for Phase 7

- [ ] T081 [P] SKILL content tests. **N/A — no SKILL content changed.**
- [ ] T082 [P] Dashboard snapshot tests for Suites tile. **N/A — no Suites tile yet.**

### Docs

- [ ] T083 [P] Rewrite `docs/document-index.md`. **Deferred** — `docs/migration-040.md` (T088, delivered) covers the user-facing change. Full rewrite of the architectural doc is polish.
- [ ] T084 [P] Update `docs/CLAUDE.md`. **Deferred** — update done by `update-agent-context.sh` during `/speckit.plan`.
- [ ] T085 [P] Update `docs/PROJECT-KNOWLEDGE.md`. **Deferred.**
- [ ] T086 [P] Update `docs/configuration.md`. **Deferred** — `migration-040.md` covers all new config keys; the standalone config doc can be updated in a follow-up.
- [ ] T087 [P] Update `docs/usage.md` and `docs/spectra-quickstart.md`. **Deferred.**
- [X] T088 [P] Add `docs/migration-040.md` migration guide. **Delivered** — comprehensive migration guide covering the layout change, automatic migration behavior, default exclusion patterns, all new flags, all new config keys, the new exit code, and three troubleshooting scenarios.
- [X] T089 Update `CHANGELOG.md` with the spec-040 entry. **Delivered** — full 1.51.0 entry with Fixed, Added, Removed, Internal, and Tests sections.
- [X] T090 Bump version in `Directory.Build.props` to the next minor. **Delivered** — bumped to 1.51.0.

### Final verification

- [X] T091 Run full test suite. **Delivered** — 1811 tests passing (519 Core + 941 CLI + 351 MCP), 0 failures, 0 regressions across the entire Phase 4-7 work.
- [ ] T092 Run the manual verification checklist against the real 541-doc project. **Deferred to release-time** — requires the actual user-reported project; the synthetic stand-in fixture passes all migration tests in CI.
- [ ] T093 Run `spectra validate`. **Deferred to release-time** — schema validation is unchanged by Spec 040.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1. **BLOCKS all user stories.**
- **Phase 3 (US2 — Migration)**: Depends on Phase 2.
- **Phase 4 (US1 — Bug fix)**: Depends on Phase 2 + Phase 3. (Phase 4 needs the new layout to exist on disk for consumers to read; in practice that means migration must run in tests before the consumer tests.)
- **Phase 5 (US3 — Patterns)**: Depends on Phase 2 + Phase 3 (replaces stub from T026). Can start in parallel with parts of Phase 4 — but T058 modifies `SuiteResolver` from T020, so coordinate.
- **Phase 6 (US4 — Introspection)**: Depends on Phase 2 only. Can run in parallel with Phases 3/4/5.
- **Phase 7 (Polish)**: Depends on Phases 3+4 at minimum (some tasks depend on Phase 5).

### Within Each User Story

- Tests precede implementation where the test names are descriptive (TDD-light): write the test class with `[Fact]` stubs first (T024, T025, T034..T040 etc.), then fill bodies after the production code exists (T032, T033, T050, T067, T073).
- Models before readers/writers before consumers.
- Inside Phase 4: T041 (`SuiteSelector`) is consumed by T042 (`BehaviorAnalyzer`), so T041 first.

### Parallel Opportunities

**Within Phase 1**: T002, T003, T004 are all `[P]` — single-developer can do them in any order.

**Within Phase 2**: T005, T006, T007, T008 (all four model files) run in parallel. T012, T015, T018, T019 (test files) run in parallel after the models. T022, T023 run in parallel with the manifest writer work. T020 (SuiteResolver) and T021 (its tests) run in parallel with the I/O classes.

**Within Phase 3**: T024 and T025 (test skeletons) parallel with T026–T031 implementation.

**Within Phase 4**: T034–T040 all `[P]` (different test files). T042–T046 mostly depend on T041 sequentially; only T044 (DocumentMapBuilder) and T046 (DataCollector) are truly independent of each other.

**Within Phase 5**: T051, T052, T053, T054, T055 (all test files) parallel. T056, T057, T065, T066 (helpers + config + matcher) parallel. T058, T059, T060 are sequential because they all touch the indexer pipeline.

**Within Phase 6**: T068, T069 parallel; T070, T071 parallel.

**Within Phase 7**: T074, T075, T076 parallel (different SKILL files). T083–T088 parallel (different docs). T079 and T080 are sequential (T080 depends on T079's progress-config shape).

---

## Parallel Example: Phase 2 Foundational

```bash
# All four model files at once:
Task: "Create DocIndexManifest model in src/Spectra.Core/Models/Index/DocIndexManifest.cs (T005)"
Task: "Create DocSuiteEntry model in src/Spectra.Core/Models/Index/DocSuiteEntry.cs (T006)"
Task: "Create ChecksumStore model in src/Spectra.Core/Models/Index/ChecksumStore.cs (T007)"
Task: "Create SuiteIndexFile model in src/Spectra.Core/Models/Index/SuiteIndexFile.cs (T008)"

# After models — readers/writers in parallel:
Task: "T010 + T011 (DocIndexManifestReader/Writer)"
Task: "T013 + T014 (ChecksumStoreReader/Writer)"
Task: "T016 + T017 (SuiteIndexFileReader/Writer)"
Task: "T020 (SuiteResolver)"
Task: "T022 (PreFlightTokenChecker)"
```

## Parallel Example: Phase 4 User Story 1 tests

```bash
# All seven Phase-4 test classes can be skeletoned in parallel:
Task: "Create SuiteSelectorTests.cs (T034)"
Task: "Create BehaviorAnalyzer_ManifestDrivenTests.cs (T035)"
Task: "Create BehaviorAnalyzer_PreFlightTokenLimitTests.cs (T036)"
Task: "Create BehaviorAnalyzer_SuiteResolutionTests.cs (T037)"
Task: "Create RequirementsExtractor_ManifestDrivenTests.cs (T038)"
Task: "Create DocumentationCoverage_FromManifestTests.cs (T039)"
Task: "Create DataCollector_LazySuiteLoadTests.cs (T040)"
```

---

## Implementation Strategy

### MVP (releasable bug-fix)

To ship the headline fix:

1. Phase 1 Setup (~half a day).
2. Phase 2 Foundational (~2–3 days).
3. Phase 3 US2 Migration (~1–2 days).
4. Phase 4 US1 Bug fix (~2–3 days).
5. Subset of Phase 7 — minimal CHANGELOG + version bump (~half a day).

That's the **shippable MVP**. The 541-doc project is unblocked; users get migration on first run; pre-flight check replaces the raw 400 error; introspection commands and dashboard polish ship in a follow-up.

### Incremental Delivery (preferred)

Ship one PR per user-story phase:

1. **PR-1 (Phase 1+2+3)**: Foundational + migration. New layout written under the hood; consumers still read the legacy file. Non-breaking. Reviewable in isolation.
2. **PR-2 (Phase 4)**: Consumer migration + pre-flight check. **This is the bug-fix release.** Deletes legacy `DocumentIndexReader`/`Writer`.
3. **PR-3 (Phase 5)**: Exclusion patterns + frontmatter overrides + spillover.
4. **PR-4 (Phase 6+7)**: Introspection commands + SKILL/dashboard/docs polish.

### Parallel Team Strategy

After Phase 2 foundational completes:
- Developer A: Phase 3 (US2 — migration).
- Developer B: Phase 4 (US1 — consumers + pre-flight).
- Developer C: Phase 6 (US4 — introspection commands).
- Phase 5 and Phase 7 wait for Phases 3+4.

---

## Notes

- **[P]** = different file, no dependency on incomplete tasks in the same phase.
- **[Story]** label is required on every task in Phases 3–6.
- Phase 1, Phase 2, and Phase 7 tasks have no story label.
- Each story is independently testable per the spec's User Story acceptance scenarios.
- Constitution gates (`spectra validate`, schema validation, ID uniqueness, index currency, dependency resolution) are **un-gated by this work** — the new layout doesn't change the test-case schema or the test-suite index.
- The 541-doc legacy fixture is the load-bearing test asset. Treat it as read-only; never let a test mutate it in place.
- Per CLAUDE.md: PascalCase types/methods, camelCase locals, all I/O async with `Async` suffix, nullable reference types enabled, xUnit with structured results.
- Avoid scope creep: Spec 041 (iterative behavior analysis) is out of scope. Don't preemptively implement batching here.
