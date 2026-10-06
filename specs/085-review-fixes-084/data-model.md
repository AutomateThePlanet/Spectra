# Data Model: Spec 085

The files do not change: `_source-baseline.json`, the snapshots and `.spectra/update-triage.json` keep their 084
shape. The changes are in types and in-memory data only.

## Core

| Type | Change |
|---|---|
| `MarkdownHeading(Level, Text, StartLine)` | + `CleanText` (attribute block removed, `TextNormalizer.Clean`, case kept), + `TextKey` (`AnchorKey.Make(CleanText)`), + `ExplicitIds` (keys of `id="…"` / `id='…'` and the `{#id}` ID). All are computed from `Text`, so equality is unchanged |
| `HeadingMatch` (new) | `record(int Index, MatchRule Rule, bool Ambiguous)`, with `enum MatchRule { Text, ExplicitId, DuplicateSuffix }` |
| `SectionExtractor` | + `static HeadingMatch? Match(IReadOnlyList<MarkdownHeading> headings, string anchor)`. `Extract` and `CoveredHeadings` use it. + `static string ComparisonText(string rawSection)`: normalised text with heading attribute blocks removed (FR-001a), used by `BaselineContext` |
| `TextNormalizer` | + `static string Clean(string? text)`: `Normalize` without the final lowercasing. `Normalize(x) == Clean(x).ToLowerInvariant()` |
| `ChangeClassifier.ModalWords` | the extended list (R4); membership on `Fold(word)` |

## CLI

| Type | Change |
|---|---|
| `SuiteScan` (new) | `record(IReadOnlyList<TestCase> Tests, IReadOnlySet<string> Ids, IReadOnlyList<string> UnparseableFiles)`, with `bool AllParsed => UnparseableFiles.Count == 0` |
| `BaselineStore` | + `ScanSuiteAsync`. `SaveSuiteAsync(testsPath, suite, baseline, SuiteScan scan, List<string> warnings, ct)` prunes only when `scan.AllParsed`, and otherwise adds the warning |
| `BaselineRecorder` | `Replace(baseline, TestCase test, computed, summary)` keeps dead fingerprints (R3). + `enum RecordMode { Rebuild, KeepBaseline }`. `RecordPersistedAsync(…, RecordMode mode, List<string> warnings)` scans after persisting |
| `BaselineWriteSummary.ToJson` | + `mode` (`"rebuild"` / `"keep"`) when a mode was used |
| `ConsoleEncoding` (new, Infrastructure) | `UseUtf8ForRedirectedStreams()` |

## Entry rules after 085

| Writer | Mode | Existing fingerprints | Dead refs/criteria still listed | Keys the test no longer lists | Prune |
|---|---|---|---|---|---|
| `ingest-tests` | Rebuild (new tests) | — | — | — | if all parsed |
| `ingest-update` | Rebuild | recomputed | previous fingerprint kept | dropped | if all parsed |
| `ingest-update --keep-baseline` | KeepBaseline | unchanged | unchanged | kept (stale) | if all parsed |
| `update-ack` | Rebuild (REMOVED refused) | recomputed | n/a | dropped | if all parsed |
| `source-baseline --init` | Complete | unchanged | unchanged | kept | if all parsed |
| `source-baseline --init --force` | Rebuild | recomputed | previous fingerprint kept | dropped | if all parsed |
| `update-triage` (auto-ack) | COSMETIC refs only | only COSMETIC refs refreshed | unchanged | kept | if all parsed |
