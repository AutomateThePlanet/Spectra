# Research: Performance and Cleanup (Phase 6, Part 4)

All facts verified on `c24d23e` (the branch point). File paths are under `src/Spectra.CLI/` unless they say otherwise.

## R0 — Golden-output check (FR-001/FR-002, SC-001)

**Decision.** One test class, `GoldenOutputTests` (CLI tests, `CriteriaCommands` collection, in-process through
`BaselineTestWorkspace.RunAsync`), drives a fixture workspace made by the real `spectra init` through a fixed sequence:

0. help output: `spectra --help`, `spectra docs --help`, `spectra ai --help`, `spectra docs apply-move --help`,
   `spectra docs retire --help` (F6 must not change command names);
1. `init` (real template, skills installed);
2. `docs index`, `source-baseline --init --all-suites`, then `update-ack --suite payments --group G1` before any triage
   (refusal `TRIAGE_MISSING`, exit 4), over 2 fixture docs (headings with `{#id}`, duplicates, a fenced
   block) and 1 suite of 6 tests (one with an unknown frontmatter key and a comment, one CRLF, one unparseable file);
3. `ingest-tests` (2 new tests from a fixture JSON);
4. doc edit → `update-triage` (stdout + `.spectra/update-triage.json`), `update-ack --group G1 --dry-run`;
5. `compile-update-prompt --group G99` (refusal `GROUP_NOT_FOUND`, exit 4), then `--group G1` and `--test-id` (stdout);
6. `ingest-update --test-id --from` (one edit), `ingest-update --all` with 3 staged files: one edit, one
   `MULTIPLE_TESTS`, one `ID_MISMATCH` (exit 8, `failed-<id>.json`);
7. `ingest-verdict --suite --all`, `ingest-grounding --all --ids`, `audit-grounding`;
8. `ingest-analysis` (generation plan) and `compile-prompt --batch B1`;
9. read-only: `validate`, `show`, `docs list-suites`, `ai analyze --coverage --format json`, `dashboard --dry-run`.

After each step the test records the exit code, stdout and stderr, and at the end every file under the workspace
(path + bytes). The record is one text file per step under `tests/Spectra.CLI.Tests/Golden/099/`.

**Capture.** `SPECTRA_GOLDEN_RECORD=1` makes the test write the golden files instead of comparing. The capture runs the
same test source in a `git worktree` at `c24d23e` (the test only uses public commands and the existing fixture helpers,
so it compiles there unchanged); the golden files are copied back and committed with the test. Capture runs twice on
`c24d23e`: the second run is compared with the first to find the fields that vary between runs.

**Masking.** Only fields that differ between the two `c24d23e` runs are masked, each with a named rule (expected:
the temp root path, `verified_at`, `compiled_at`, `generated_at`, `recorded_at`, durations, file write times quoted in
JSON). The mask list and the two-run evidence go in the Notes of tasks.md. Nothing is masked because 099 changed it.

**Pre-099 workspace (FR-024).** `init` is part of the compared sequence; byte-identical init output means a workspace
created by `c24d23e` and one created by 099 are the same bytes, so the sequence covers both. A second row runs steps
2–9 on the captured post-init tree of `c24d23e` (copied from the golden files) to make that explicit.

**Alternatives.** A shell script diffing two CLI builds (not part of the suite, so it would rot); comparing only JSON
(misses file bytes, which is what 078's lossless writer and F3 must preserve).

## R1 — One suite parse per ingest (F1)

**Facts.**
- `IngestTestsCommand.LoadExistingTestsAsync` (:188-209) and `IngestUpdateCommand.LoadExistingTestsAsync` (:666-687) are
  identical: `Directory.GetFiles(suitePath, "*.md")` (not recursive, OS order), `_`-prefixed files skipped, parse with
  `Path.GetRelativePath(suitePath, file)`, **unparseable files dropped silently**. They run before the write and outside
  the baseline lock (ingest-tests :94, `--test-id` :142, `--all` :363).
- `BaselineRecorder.RecordPersistedAsync` (:182-212) → `BaselineStore.ScanSuiteAsync` (:209; BaselineStore.cs:116-142,
  same file set, ordinal file order, unparseable files as repo-relative `/` paths) → `SaveSuiteAsync(…, scan, warnings)`.
- `SaveSuiteAsync` reads only `scan.AllParsed`, `scan.Ids` (ordinal set) and `scan.UnparseableFiles` (whose order is in the
  warning text). It never reads `scan.Tests`. The recorder reads `Id`, `SourceRefs`, `Criteria` of the **in-memory**
  persisted tests, never the scan's.
- The scan runs once per successful non-dry-run ingest that records (`--dry-run` never scans; `--keep-baseline` with only
  unchanged edits records nothing and never scans).
- Writes go to `{id}.md` (`TestFileWriter.GetFilePath`), not to `tc.FilePath`: an update of a test stored as `foo.md`
  creates `TC-1.md` beside it (existing behaviour, unchanged here).

**Decision.**
- New `Baseline/SuiteScanBuilder` (static, pure): `FromFiles(repoRoot, suitePath, parsed, unparseable)` builds the same
  `SuiteScan` the store would build; `WithWritten(scan, writtenPaths, writtenTests, comparer)` applies the call's writes:
  for each written path, any scanned entry for the same path (compared with `PathComparer` = OrdinalIgnoreCase on
  Windows, Ordinal elsewhere — the precedent in `TriageEngine.cs:60`/`TestDeletionService.cs:67`) is replaced by the
  written test (removing an unparseable entry or a different id the file held), then ids are recomputed and the lists
  re-sorted (tests by id, unparseable files ordinally) exactly as `ScanSuiteAsync` sorts.
- The two loaders become one shared `IngestSuiteLoader.LoadAsync(repoRoot, testsPath, suite, captureLayout, ct)` that
  returns the existing tests **and** the per-file outcome (path → test | unparseable). The existing-tests list the
  commands use is unchanged (same tests, same order).
- `BaselineRecorder.RecordPersistedAsync` gains an optional `SuiteScan? scan`; when given, it skips `ScanSuiteAsync`.
  The ingest commands pass `WithWritten(load, written…)`. Other callers pass nothing (unchanged).
- Window: the load happens before the lock; the old scan ran inside it after the write. Both see the files this call
  wrote; another process writing test files of the same suite between the load and the record is the only difference,
  and such a writer (another ingest) takes the same baseline lock for its own record, so its baseline write is
  serialised as before. Recorded as a spec assumption.
- **Counter:** `ParseCounter.FullParses` (R8), counted in `TestCaseParser.Parse` itself, so it sees every test-file
  parse in the call — the loader, the baseline scan, and also `PendingChangeCheck` / `TriageEngine.TriageTestAsync`
  (089) in `ingest-update`. Target N for a suite of N test files (each parse attempt counts, an unparseable file
  included). The guard is written first (Phase 2 wiring) and records today's number per variant; F1 removes every
  duplicate it can without behaviour change; a parse that remains beyond N is pinned at its real number and listed
  in the Notes with the reason.

**Alternatives.** Moving the load inside the lock (changes the order of `LOCK_TIMEOUT` vs IO errors — visible);
re-reading only the written files (still extra parses, and they're already in memory).

**Found, not in scope.** `SourceBaselineHandler.cs:160` re-scans every suite already loaded at :110 only to feed
`SubmoduleNotesAsync` — the same class of double parse, outside the ingest commands. Recorded for a later spec.

## R2 — Cached headings (F2)

**Facts.** In `Baseline/BaselineContext.cs` the whole-document heading scans are:
- `GetSectionAsync` :158 — `SectionExtractor.Extract(text, anchor)` (scans; result cached per ref string);
- `GetRawSectionAsync` :199 — `Extract` again on every call (not cached; callers `UpdateContext.cs:193`,
  `CompileBatchPrompt.cs:205`);
- `GetHeadingsAsync` :210 — `SectionExtractor.Headings(text)` on every call (callers `TriageEngine.cs:316`,
  `CompileBatchPrompt.cs:175/183` — once per criterion via `BatchCriteriaSelector.cs:67` —, `PlanSectionResolver.cs:47`).
Text cache `_files` is `Dictionary<string,string?>(StringComparer.Ordinal)` keyed by the path given to `ReadFileAsync`
(usually `ResolvedSourceRef.FullPath`). `Headings` is a pure function of its input (normalises internally, static
generated regexes); `MarkdownHeading` is an immutable record; callers only index and pass the list on. All context use
is sequential (no parallel callers).

**Decision.**
- `SectionExtractor` gains `internal static SectionLookup Extract(string normalisedText, IReadOnlyList<MarkdownHeading>
  headings, string? anchor)`; the public `Extract(text, anchor)` becomes normalise + `Headings` + that overload
  (identical result).
- `BaselineContext` gains `_headings` (`Dictionary<string, IReadOnlyList<MarkdownHeading>>`, Ordinal, keyed by the same
  path as `_files`) and caches the normalised text alongside; :158, :199 and :210 use it. The one non-normalised key
  (`CompileBatchPrompt.cs:183`, `Path.Combine(root, doc)`) keeps its behaviour — it is cached under its own key as the
  text already is.
- **Counter:** `ParseCounter.DocumentHeadingScans` (R8), incremented by `BaselineContext` each time it scans a whole
  document's headings (directly or through `Extract`).
  Section-level scans (`CurrentSection.FromRaw` → `HeadingComparisonTexts`, ChangeClassifier) work on one section's text
  and are not document scans; not counted, not changed.
- `HistoryBootstrap.cs:134` (`Extract` on historic blobs, one per ref, no context) is untouched: its texts differ per
  commit, and no ref repeats within one blob lookup.

## R3 — Layout capture only when writing (F3)

**Facts.** `TestCaseParser.Parse` (:44-124) builds the test from the typed frontmatter + body parse;
`AttachLayout` (:132-161) then re-splits the raw text, walks the YAML event stream (`CaptureFrontmatterEntries`), scans
the body (`CaptureBodySegments`), detects the newline and makes a `with` copy. The typed parse doesn't depend on it.
`TestCase.Layout` is `[JsonIgnore]/[YamlIgnore]`, part of record equality (reference-compared lists); no production code
compares tests by value (`IngestUpdateCommand.cs:544` uses `ReferenceEquals`). Only `TestFileWriter` and
`TestRefRewriter.EditInPlace` (:72) read the layout.

The writer writes a layout-less test canonically — the path new generated tests take (`IngestTestsCommand` →
`TestPersistenceService.cs:51`, tests built from model JSON).

Parse callers, classified:

| Caller | Class | Why |
|---|---|---|
| `IO/GroundingWriteBackService.cs:66` | writer | `original with {Grounding}` → writer :113 |
| `Commands/Review/ReviewFlaggedHandler.cs:98` (accept) | writer | `tc with {Grounding}` → :108 |
| `Baseline/TestRefRewriter.cs:65` | writer | re-parse of edited text, layout used |
| `Commands/Generate/IngestUpdateCommand.cs:681` | mixed → layout | the `original` is edited and written |
| `Commands/Delete/TestDeletionService.cs:384` | mixed → layout | scalar dependents written at :251 |
| `Agent/Tools/BatchReadTestsTool.cs:68` | mixed → layout | `UpdateHandler` orphan marking writes at :684 |
| `Baseline/BaselineStore.cs:128` (`ScanSuiteAsync`) | mixed → parameter | relink (`RelinkSourcesHandler.cs:67` → `TestRefRewriter`) needs layout; every other caller is read-only |
| `ReviewFlaggedHandler.cs:57/74` (list) | read-only | copies fields |
| `IngestTestsCommand.cs:207` | read-only | index merge only; new tests come from JSON |
| `IngestGroundingCommand.cs:182/350` | read-only | the write re-parses in the write-back service |
| `Verification/VerdictFreshness.cs:179`, `Commands/Index/IndexHandler.cs:200/240`, `AuditGroundingHandler:103/143`, `RunServices:83`, `ShowHandler:141`, `ValidateHandler:133`, `Dashboard/DataCollector:302`, `CompileCriticPrompt:311`, `CompileRepairPrompt:94`, `CompileRepairBatch:119/128`, `CompileAnalysisPrompt:207`, `CompilePrompt:194`, `IngestAnalysis:318`, `IngestVerdict:211`, `Interactive/SuiteScanner.cs:87` | read-only | no writer reference |

**Decision.**
- `TestCaseParser` gets an immutable mode: `new TestCaseParser(captureLayout: false)` — a constructor argument (default
  **true**, as today), because `IndexHandler` shares one parser across `Parallel.ForEachAsync`. Every read-only caller
  in the table passes `false`; writer and mixed callers keep the default. The safe default means a new writer caller
  can't regress and a missed read-only caller only costs time; the existing round-trip tests (`TestCaseLayoutTests`,
  `TestFileWriterRoundTripTests`, …) are untouched.
- `TestCase` gains `[JsonIgnore, YamlIgnore] internal bool LayoutOmitted { get; init; }` (Core `InternalsVisibleTo`
  CLI). A parse without capture sets it; `with` copies carry it.
- `TestFileWriter.FormatTestCase` throws `InvalidOperationException("Test {id} was parsed without layout; a writer path
  must parse with captureLayout: true")` when `LayoutOmitted` is set; `Layout == null && !LayoutOmitted` (a new test)
  still writes canonically.
- `BaselineStore.ScanSuiteAsync(…, bool captureLayout = false)`; `BaselineCommandIO.LoadSuiteTestsAsync` passes it
  through; `RelinkSourcesHandler` passes `true`.
- **Guards:** (1) a writer-path matrix test runs each writer path of the table (update ingest, grounding write-back,
  review accept, delete's `depends_on` cleanup, relink, legacy `ai update` orphan marking) on a fixture file with an
  unknown key, a comment and CRLF and asserts lossless output (the existing 078/080/081 rows stay); (2) a writer unit
  test: a layout-less parse → throws, nothing written; a new in-memory test → canonical; (3) a parser-mode test per
  read-only command family asserting it parses without capture (`ParseCounter.LayoutCaptures` = 0 within the
  command's scope, R8).
- Removing the layout makes two parses of one file record-equal; nothing in production depends on inequality.

**Alternatives.** Lazy capture (keeps the raw text on every test — memory for every read path, and the writer still
needs a second parse); a separate `TestCaseReader` type (duplicates the parser).

## R4 — One JSON parse per staged file (F4)

**Facts.** Per staged file, `ExtractJson` + `JsonDocument.Parse` run in `IngestUpdateCommand.cs:487`
(`GeneratedTestIngestor.ArrayObjects`), `UpdatedTestIngestor.cs:78` (`ArrayObjects` again) and `:82`
(`ParseAndValidate` → `TryParseArray` :83). Check order in `--all`: no id in name → skip; `NO_ORIGINAL`; `UNREADABLE`;
`MULTIPLE_TESTS` (`MultipleTestsMessage`); `ID_MISMATCH`; then the ingestor (`EMPTY_CONTENT` ×2 messages, `TRUNCATED`,
`MALFORMED_JSON`, `NO_TESTS`, `SCHEMA_INVALID`), `DRIFT_DETECTED`, no-change, persist (`PERSIST_FAILED`). No message
quotes the content; the `failed-<id>.json` rename uses the path only. `--test-id` runs calls 2 and 3.

**Decision.**
- `GeneratedTestIngestor` gains `internal sealed record ExtractedContent(bool Blank, string Json, JsonElement? Array)`
  and `internal static ExtractedContent Extract(string? content)`; `ArrayObjects(ExtractedContent)` and
  `ParseAndValidate(ExtractedContent, TestValidator)` overloads; the public string overloads delegate (existing tests
  compile unchanged).
- `UpdatedTestIngestor.IngestAsync(ExtractedContent, …)` (internal); the string version delegates.
- `--all` extracts once and passes the result down; `--test-id` extracts once in the string overload (2 → 1).
- **Counter:** `ParseCounter.JsonParses` (R8) incremented in `Extract`; K staged files → K.
- **String overloads (constitution V).** At implement time, a string overload left with 0 production callers is
  deleted (the F5 rule) and its tests move to the `ExtractedContent` overload; the decision per overload goes in the
  Notes.
- `TryParseArray` keeps its `Clone()` of the root (unchanged lifetime).

## R5 — Dead code (F5)

**Rule.** Delete a type or member only when (a) it has **0 references from `src/`** (comments and `<see cref>` don't
count; checked: no reflection, `[JsonSerializable]`, `Activator` or name-based DI in `src/`), and (b) git history ties
its last production use to 058/059/069/070 (or it is the 095 item named in the spec). Tests that only exercise deleted
code are deleted with it (listed). Items with 0 references whose origin is older are **recorded, not deleted**
(scope). Kept by rule: retired-key detection (`CriticConfigConverter`, deprecated-key notice), `AiConfig.GenerationBatchSize`,
everything in `Spectra.Execution`.

| Candidate | src refs | Test files removed with it | Verdict |
|---|---|---|---|
| `AnalysisRecommendationBuilder.Build` `snapshot` param | 1 caller passing `null`, never read | — (`AnalysisRecommendationBuilderTests` :66/:90 drop the argument) | delete param |
| `AnalysisRecommendationBuilder.CountCoveredBehaviors` | 0 | 2 methods of `Agent/BehaviorAnalyzerTests.cs` | delete |
| `Output/AnalysisPresenter` | 0 | AnalysisPresenterTests, AnalysisPresenterCoverageTests | delete |
| `Agent/IAgentRuntime` + `GenerationResult` | 0 | — | delete |
| `Agent/Skills/SkillLoader` + `SkillDefinition` | 0 | — | delete |
| `GroundedPromptBuilder.BuildSystemPrompt` / `BuildUserPrompt`×2 / `FormatCriteriaContext` / `BuildExistingTestsSummary` | 0 (keep `LoadRelatedCriteriaAsync`, used by `UpdateHandler.cs:292`) | rows that only test them | delete members |
| `Agent/Analysis/BehaviorAnalysisResult` | only dead users | rows using it only for dead code | delete (chain) |
| `Interactive/CountSelector` (+`CountSelection`, `MenuOption`), `GapSelector`, `InteractiveSession`, `TestTypeSelector` (+`FocusDescriptor`), `UserDescriptionPrompt` | 0 (keep `SuiteSelector`, `SuiteScanner`) | CountSelector tests | delete |
| `Session/` (`SessionStore`, `SuggestionBuilder`, `GenerationSessionState`, `AnalysisSnapshot`, `SessionSuggestion`, `SessionSummary`), `Output/SuggestionPresenter` | 0 / chain | SessionStoreTests, SuggestionBuilder tests | delete folder |
| `Review/TestReviewer` → `PendingTestQueue`, `ReviewPresenter`, `TestEditor`; `Review/StreamingRenderer` (+`IStreamingOutput`, outputs) | 0 / chain (keep `UpdateReviewer`, `DiffPresenter`) | — | delete |
| `Coverage/GapAnalyzer`, `GapPresenter`; `Output/ResultPresenter`; `Output/VerificationPresenter` | 0 | GapAnalyzer tests, VerificationPresenter tests | delete |
| Testimize chain: `TestimizeRunner`, `FieldSpecAnalysisTools`, `PromptCompiler` `testimizeData` params + `FormatTestimizeDataset`, Core `TestimizeDataset`/`Row`/`Cell`, `FieldSpec` | 0 / chain (all 3 `PromptCompiler` callers pass `null`) | TestimizeRunnerTests, AnalyzeFieldSpecTests | delete (keep the `Testimize` package — `TestimizeCheckHandler` uses `TestimizeEngine`; keep `TestimizeConfig`, `testimize check`) |
| `TokenUsageTracker.Record`, `TokenUsageReport.FromTracker`, `Services/CostEstimator` | 0 / chain (tracker class kept: `UpdateHandler.cs:567`) | rows of the 4 tracker test files that call `Record`; CostEstimator tests | delete members |
| `Agent/GitHubCliTokenProvider` + `AuthResult`; `Services/CopilotUsageObserver` | 0 | CopilotUsageObserverTests | delete |
| `Analysis/DocumentTools`, `Analysis/TestIndexTools`, `Agent/Tools/ToolRegistry` (+defs), `GetDocumentMapTool`, `LoadSourceDocumentTool`, `SearchSourceDocsTool`, `ReadTestIndexTool`, `GetNextTestIdsTool`, `CheckDuplicatesBatchTool`, `BatchWriteTestsTool` (+ result records) | 0 / chain (keep `BatchReadTestsTool`, `BatchProposeUpdatesTool`, `UpdateProposalItem`) | the `BatchWriteTestsTool` row of `WriterPathPreservationTests.cs:169` | delete |
| `Source/DocumentSearcher`, `Source/SourceDocumentReader` (+records) | chain | — | delete |
| Core `GlobalIdScanner.CreateGlobalAllocatorAsync`, Core `TestIdAllocator` | chain (keep `GlobalIdScanner`) | TestIdAllocatorTests | delete |
| `Provider/RecoverableErrorDetector` | 0 | — | delete folder |
| `Agent/Critic/CriticResponseParser` | 0 (cref only) | CriticResponseParserTests, the parser rows of VerificationIntegrationTests | delete |
| `Microsoft.Extensions.AI.Abstractions` package | used only by deleted tools | — | remove reference |
| Core `Models/Execution/McpToolResponse` (070), Core `SuiteInfo` (070), `Results/ExtractCriteriaResult` (069), Core `RequirementsWriter` (069) | 0 | RequirementsWriter tests | delete |
| `GenerateResult` | 1 (`ProgressManager` type pattern) | — | keep (has a reference) |
| `CriticPromptBuilder`, `CoverageSnapshot`/`Builder`/`ContextFormatter`, `AnalyzerInputBuilder`, `DocSuiteSelector`, `DocumentMapBuilder` | live | — | keep |
| `IO/LockManager` (+types), `PathSanitizer`, `Results/InitResult`, `Results/ExtractRequirementsResult`, Core `BugReportTemplateEngine`, `BugReportWriter`, `RequirementsCoverageAnalyzer`, `IndexRebuildResult` | 0, but no tie to 058–070 (never used, or last use removed before 058) | — | **recorded, not deleted** (later cleanup) |

Each deletion is re-counted at implement time (`grep -rw` over `src/`, plus a clean build); a count that is no longer 0
moves the row to "keep". Test rows removed are listed per file in tasks.md Notes with their method names.
`ConfigCommand.cs:17` help example `'ai.defaultProvider'` is stale but is CLI help text (a visible change) → recorded for 100.

## R6 — Build warnings (F6)

**Facts.** A clean build at `c24d23e` shows exactly:
- `DocsApplyMoveCommand.cs(21,26)` / `DocsRetireCommand.cs(21,26)` CS0108: `private const string Name` hides
  `IdentifierSymbol.Name` (System.CommandLine). The const is the JSON `command` value (`docs-apply-move` / `docs-retire`).
- `CriteriaLayoutMigrator.cs(30,29)` CS8714 / `(31,13)` CS8621: `ToDictionary(s => s, …, ReferenceEqualityComparer.Instance)`
  infers `TKey = object?` from the comparer (`IEqualityComparer<object?>`).

`Directory.Build.props` already has `TreatWarningsAsErrors` **only for Release**, `NoWarn` CA1062/CS8602, analyzers
`Microsoft.CodeAnalysis.NetAnalyzers` `10.*` (floating).

**Decision.**
- Rename the consts to `CommandName` (no `new` — hiding a public member with a private const is confusing); values unchanged.
- `ToDictionary<CriteriaSource, CriteriaSource, string>(…, ReferenceEqualityComparer.Instance)` — explicit type arguments;
  `ReferenceEqualityComparer` is contravariant-compatible as `IEqualityComparer<CriteriaSource>`.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Spectra.CLI.csproj` and `Spectra.Core.csproj` (all
  configurations), with `<WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>`: a NuGet vulnerability
  advisory published after the fact must not break a build with no code change.
  This is not hypothetical: on `c24d23e`, `dotnet restore --force-evaluate` reports NU1902/NU1903 for
  `SixLabors.ImageSharp` 3.1.12 in `Spectra.Execution` (warnings in Debug), and **`dotnet build -c Release` fails at
  restore today** because `Directory.Build.props` promotes them in Release. CLI and Core report none (NuGet audit is
  direct-only for net8), but the CLI references Execution, so a transitive audit would reach it. The upgrade is out of
  099's scope (a dependency change) and was raised as a separate task.
- The floating analyzer version (`10.*`, resolved `10.0.401` here) is a similar risk: a new analyzer release can add
  warnings that would now fail Debug builds of CLI/Core. Pinning it is a dependency change outside 099's scope; it is
  recorded as a risk in the plan, and `AnalysisLevel` stays.
- Check: `dotnet build --no-incremental` (Debug) shows 0 warnings for CLI and Core (Execution's restore advisories
  remain until the separate upgrade); a guard test reads both csproj files and asserts the property (as
  `RepositoryAttributesTests` guards `.gitattributes`). A Release build is not part of 099's gate while the
  ImageSharp advisory blocks its restore.

## R7 — Test-suite duration (F7)

**Facts** (survey + the `c24d23e` measurement below).
- No `xunit.runner.json`; xUnit 2 defaults (a collection per class, parallel up to 16 cores here). Serial collections run
  after the parallel ones, one class at a time: `CriteriaCommands` 96 classes / 572 methods + 145 rows,
  `WorkingDirectory` 46 / 332, `GitEnvironment` 1 / 3, `DebugLogger` 1 / 17, `ErrorLogger` 1 / 10. ≈ 1,100 test cases
  run single-threaded — the main cost.
- They are serial because `BaselineTestWorkspace.RunAsync`, `CriteriaTestWorkspace.RunAsync`, `UpdateWorkspace.RunAsync`
  set the current directory and swap `Console`.
- Real-init fixtures run per test: `UpdatePromptWorkspace.InitWithRealTemplateAsync` (≈ 20 classes / ≈ 110 cases) and
  `ManifestWorkspace.InitWithSkillsAsync` (≈ 5 classes / ≈ 55 cases, writes ≈ 30 skill files); `QuickstartWorkflowTests`
  runs full init per test (9).
- Git fixtures: `GitFixture.Init` 5 processes, `Commit` 3 (add, commit, rev-parse), `AddSubmodule` 4, `CloneInto` 4;
  ≈ 1,000–1,400 git processes, all in `CriteriaCommands` (serial). `SubmoduleNotCheckedOutTests` rebuilds the same
  ≈ 25-process setup for each of its 9 tests.
- One class launches the CLI (`Utf8StdoutTests`, 2 launches) — process-level by subject, kept.
- Sleeps total ≈ 3–4 s; not a target.

**Decision — allowed techniques** (in order of expected gain; each change keeps every test and assertion):
1. **Leave the serial collection** when the class touches no process-global state: no `RunAsync` that sets the current
   directory or `Console`, no `Directory.SetCurrentDirectory`, no `Console.Set*`, no static override
   (`LockTimeoutOverride`, `GitAccess.RunnerOverride`), no singleton
   (`CancellationManager.Instance`). Candidates: `DeletePerformanceTests`, `SkillsManifestTests` (73),
   `InitClaudeSettingsTests`, `InitNoMcpEmissionTests`, `InitHandlerBugTrackingTests`, `ClaudeSkillsInstallTests`,
   `SessionStoreTests` (deleted by R5), `AnalyzeHandlerMigrationTests` — each verified by reading the class before moving.
   *This technique is not listed in spec FR-020; the spec is amended to allow it (it removes no coverage).*
2. **Init once, copy per test.** `TemplateWorkspaces` (test project): a process-wide `Lazy<Task<string>>` per template
   (real init without skills; with skills) under a temp root; `UpdatePromptWorkspace`/`ManifestWorkspace` copy that tree
   into the test's folder instead of running init. Guard: `TemplateWorkspaceTests` compares a copied tree with a fresh
   init byte for byte (paths + contents), so a template that embeds its own location fails loudly.
3. **Fewer git processes in fixtures.** `GitFixture.Init` = `git init` + one write of the identity/autocrlf/gpgsign
   settings to `.git/config` (1 process instead of 5); `Commit` returns the hash from `git commit`'s output only when
   asked (`CommitAsync(…, wantHash: false)` default for callers that discard it) — or keeps `rev-parse` where the hash is
   used. `SubmoduleNotCheckedOutTests` and other classes that build an identical repo per test build it once per class
   (`IClassFixture`) and copy the folder per test (relative `gitdir:` links survive a copy; the clone's `origin` points at
   the class template, which lives as long as the class).
4. **Process launches.** None to replace (the only launcher tests process-level behaviour).

**Measurement.** `dotnet test tests/Spectra.CLI.Tests --no-build --logger trx` on this machine, idle; per-class time =
sum of test durations from the TRX (serial classes: that is their wall time; parallel classes: CPU-ish, recorded for
comparison), plus the run's wall time. The 20 slowest classes before/after and the suite wall time go in tasks.md Notes.

### Baseline measurement on `c24d23e`

Idle machine (16 cores), `dotnet test tests/Spectra.CLI.Tests --no-build --logger trx`: **597 s wall** (9 m 57 s),
2,326 passed. Sum of test durations 786 s, split by collection:

| Collection | Test cases | Σ duration | Runs |
|---|---|---|---|
| `CriteriaCommands` | 787 | 495.8 s | serial |
| `WorkingDirectory` | 345 | 48.8 s | serial |
| `GitEnvironment` | 5 | 7.4 s | serial |
| `DebugLogger` / `ErrorLogger` | 27 | 0.7 s | serial |
| parallel (no collection) | 1,162 | 233.5 s | parallel (16 cores) |

The wall time ≈ the serial sum (552 s). Git classes alone: 317.6 s, nearly all in `CriteriaCommands`. The ≈ 7 min
target (≈ 420 s) therefore needs ≈ 180 s off the serial part: git fixture processes, per-class git templates and
copied init templates are the levers; leaving a serial collection only helps for classes that don't run commands
in-process (few, and mostly small).

20 slowest classes (Σ test duration):

| # | Class | Tests | Σ s |
|---|---|---|---|
| 1 | Commands.Git.RelinkPerformanceTests | 1 | 81.3 |
| 2 | Commands.Git.SubmoduleNotCheckedOutTests | 9 | 58.1 |
| 3 | Commands.Git.RenameTests | 14 | 44.1 |
| 4 | Commands.Baseline.UpdateTriagePerformanceTests | 1 | 39.3 |
| 5 | Commands.InitCommandTests | 23 | 33.3 |
| 6 | Commands.Git.RelinkSourcesTests | 13 | 30.0 |
| 7 | Commands.Git.TopologyTests | 3 | 27.6 |
| 8 | Commands.Git.GitReaderIntegrationTests | 1 | 21.0 |
| 9 | Commands.Git.HistoryBootstrapTests | 16 | 20.4 |
| 10 | Commands.Delete.DeletePerformanceTests | 2 | 18.9 |
| 11 | Dashboard.DataCollectorTests | 30 | 13.6 |
| 12 | Dashboard.DashboardGeneratorTests | 16 | 13.6 |
| 13 | Config.LegacyCriticConfigTests | 29 | 11.7 |
| 14 | Index.LegacyIndexMigratorTests | 12 | 11.5 |
| 15 | Commands.Git.UpdateAckRelinkTests | 5 | 8.3 |
| 16 | Skills.CriticModelTests | 19 | 7.6 |
| 17 | Commands.Git.BootstrapThresholdTests | 5 | 7.4 |
| 18 | Commands.Git.GitEnvironmentIsolationTests | 5 | 7.4 |
| 19 | Integration.QuickstartWorkflowTests | 9 | 6.4 |
| 20 | IO.TestPersistenceServiceTests | 11 | 6.3 |

A first run on the same commit while four research agents loaded the machine took 833 s and failed one wall-clock
assertion (`UpdateTriagePerformanceTests`: setup 60.66 s > 60 s; 35 s alone, idle). Measurements are idle-only; the
assertion is not relaxed (spec).

**Not in 099.** The serial collections exist because commands read the current directory and write to the global
`Console`; passing the workspace root and an output writer into the handlers would let them run in parallel (the
largest possible gain) but changes the product's command plumbing — recorded for a later spec.

## R8 — Scoped parse counter (FR-006, FR-008, FR-014, FR-022)

**Decision.** `src/Spectra.Core/Diagnostics/ParseCounter.cs` (internal):

```csharp
internal static class ParseCounter
{
    private static readonly AsyncLocal<Scope?> Current = new();
    public static Scope Begin() { var s = new Scope(Current.Value); Current.Value = s; return s; }
    internal static void FullParse() { if (Current.Value is { } s) Interlocked.Increment(ref s.full); }
    // LayoutCapture(), DocumentHeadingScan(), JsonParse() likewise
    internal sealed class Scope : IDisposable { /* FullParses, LayoutCaptures, DocumentHeadingScans, JsonParses; Dispose restores the parent */ }
}
```

- Increments go to the innermost current scope only; `Interlocked`, because `AsyncLocal` flows into
  `Parallel.ForEachAsync` / `Task.Run` (e.g. `IndexHandler`), which share the scope object.
- No scope (production) → a null check, nothing else.
- Each test opens its own scope around the command it measures, so parallel tests never see each other's counts; no
  serial collection is needed for the counters, and no static state is read.
- Sites: `TestCaseParser.Parse` (`FullParse`, every attempt), `TestCaseParser.AttachLayout` (`LayoutCapture`),
  `BaselineContext` whole-document heading scans (`DocumentHeadingScan`), `GeneratedTestIngestor` JSON parse
  (`JsonParse`; today only inside `TryParseArray`, the single `JsonDocument.Parse` site — `ArrayObjects` counts through
  it, so today's number is 3 per staged file; after F4 the count moves to the one `Extract` call).
- Core gains `<InternalsVisibleTo Include="Spectra.CLI.Tests" />` (beside `Spectra.CLI` and `Spectra.Core.Tests`);
  the CLI already grants it.
- The counters are wired in Phase 2, before any fix: every guard first records today's number (2N, 3K, scans per
  ref, layout captures in read-only commands), which is the proof that the guard fails on the old behaviour.

**Alternatives.** Plain statics (shared across parallel tests → serial collection or flaky); per-instance counters
(the ingest commands are static, and the parser is created in many places).
