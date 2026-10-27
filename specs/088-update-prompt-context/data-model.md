# Data Model: Spec 088

No file format changes: baselines, snapshots, the triage detail and test files are as in 084–087. Staging files keep
their path and content (a JSON array with one edited test); they are now deleted when consumed.

## New (Spectra.CLI.Generation)

| Type | Shape | Notes |
|---|---|---|
| `SectionState` | `Unchanged`, `Changed`, `NotBaselined`, `Removed`, `Relink` | |
| `SectionBlock` | `(string Label /* S1… */, string Ref, IReadOnlyList<string> Tests, SectionState State, string? Bucket /* LOAD/LIGHT/COSMETIC */, string? Current, string? Previous, string? PreviousUnavailableReason, IReadOnlyList<string> Why, IReadOnlyList<DiffRun> Diff, string? Reason /* unresolved */)` | One per distinct (ref, baseline fingerprint) |
| `CriterionState` (prompt) | `Unchanged`, `ChangedSinceBaseline`, `Removed` | Distinct from Core's `CriterionState` (named `LinkedCriterionState`) |
| `CriterionBlock` | `(string Id, string? Text, LinkedCriterionState State)` | Linked criteria only, by ID |
| `TestBlock` | `(TestCase Test, IReadOnlyList<string> SectionLabels, IReadOnlyList<string> CriterionIds)` | By test ID |
| `UpdateContext` | `(IReadOnlyList<TestBlock> Tests, IReadOnlyList<SectionBlock> Sections, IReadOnlyList<CriterionBlock> Criteria, IReadOnlyList<string> NothingToReconcile)` | |
| `UpdateContextBuilder` | `static Task<UpdateContext> BuildAsync(IReadOnlyList<TestCase> tests, SourceBaseline baseline, BaselineContext context, int lightThreshold, CancellationToken)` | |
| `UpdatePromptRenderer` | `static string SourceDiff(UpdateContext)`, `static string Criteria(UpdateContext)`, `static string Tests(UpdateContext)` | LF, deterministic |

## New (Spectra.CLI.Baseline)

| Type | Shape | Notes |
|---|---|---|
| `TriageGroupResolver` | `static Task<List<TestCase>> ResolveAsync(string root, string suite, string group, IReadOnlyDictionary<string, TestCase> byId, BaselineContext context, CancellationToken)` → bucket via `out`/record | Extracted from `UpdateAckHandler`; throws `BaselineStoreException` with `TRIAGE_MISSING` / `GROUP_NOT_FOUND` / `TRIAGE_STALE` |

## Changed

| Type | Change |
|---|---|
| `BaselineContext` | + `GetRawSectionAsync(string ref)` (raw section text, or null when missing); + `GetCriterionText(string id)` |
| `UpdatePromptCompiler` | + `CompileContext(UpdateContext, PromptTemplate, string profileFormat, bool group)` → `(string? Prompt, string? Warning, string? RefusalCode, string? Message)`; + `IsOutdated(PromptTemplate)`; legacy `Compile`/`Assemble` kept |
| `UpdatedTestIngestor` | + `static bool IsUnchanged(TestCase original, TestCase candidate)` (CRLF/CR → LF, trailing whitespace trimmed per line and per field, comparison only); `IngestAsync` returns `IngestResult` with `Unchanged = true` (no persist) |
| `IngestResult` | + `bool Unchanged` |
| `IngestUpdateCommand` | single: `unchanged` in the output, baseline rebuilt unless `--keep-baseline`; batch: `StagedOutcome` per file, deletion of consumed files, `unchanged` + `results` in the output |
| `CompileUpdatePromptCommand` | `--group`; `ConfigLoader.LoadForReadAsync(Source|Tests)`; shared failure shape; new refusals |
| `UpdateAckHandler` | uses `TriageGroupResolver` |
| `test-update.md` | placeholders `source_diff`, `acceptance_criteria`, `tests`, `profile_format` (+ legacy `test_case`, `current_source`) |

## Outcomes

| Command | Outcome | Effect |
|---|---|---|
| `ingest-update --all` (per file) | `written` | Test persisted, index updated, baseline rebuilt (or kept with `--keep-baseline`), staging file deleted |
| | `unchanged` | Nothing written; baseline rebuilt from current content (none with `--keep-baseline`); staging file deleted |
| | `failed` | Nothing written; staging file kept; `error_code` + `errors` reported |
| `ingest-update --test-id` | `unchanged: true` | As above, no file write |
