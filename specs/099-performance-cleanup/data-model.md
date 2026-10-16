# Data Model: Performance and Cleanup (Phase 6, Part 4)

No persisted format changes. Everything below is in-memory or test-only.

## Suite scan (F1)

`SuiteScan(Tests, Ids, UnparseableFiles)` — unchanged record (085). New way to build it:

| Step | Input | Output |
|---|---|---|
| `IngestSuiteLoader.LoadAsync` | suite folder | per file (ordinal path order): `(path, TestCase)` or `(path, unparseable)`; the existing-tests list (same tests, same order as the old loaders) |
| `SuiteScanBuilder.FromFiles` | loader result | `SuiteScan` as `BaselineStore.ScanSuiteAsync` would build it on the same disk state |
| `SuiteScanBuilder.WithWritten` | scan, written `(path, TestCase)` pairs, `PathComparer` | written entries replace any entry with the same path (OS case rule); ids recomputed; tests sorted by id (ordinal), unparseable files sorted ordinally (repo-relative, `/`) |

Rules:
- Same file set as the store's scan: top-level `*.md`, `_`-prefixed skipped.
- An overwritten unparseable file leaves the unparseable list; an overwritten file that held another id loses that id.
- A test updated from `foo.md` is written as `{id}.md`: `foo.md` stays in the scan (as today on disk).
- `BaselineRecorder.RecordPersistedAsync(…, SuiteScan? scan = null)`: given → no disk scan.

Counter: `ParseCounter` scope `FullParses` (counted in `TestCaseParser.Parse`, every parse in the call) — target N per
ingest of a suite of N test files; remaining extras pinned and explained in Notes.

## Heading cache (F2)

`BaselineContext`:

| Field | Key | Value |
|---|---|---|
| `_files` (existing) | path as given (Ordinal) | raw text or null |
| `_normalised` (new) | same | `NormalizeNewlines(text)` |
| `_headings` (new) | same | `IReadOnlyList<MarkdownHeading>` from one `SectionExtractor.Headings` call |
| (counter) | — | each whole-document scan calls `ParseCounter.DocumentHeadingScan()` |

`SectionExtractor.Extract(text, anchor)` = `Extract(normalised, Headings(normalised), anchor)` (new internal overload);
identical result.

## Parse mode and writer guard (F3)

| `TestCase` state | `Layout` | `LayoutOmitted` | `TestFileWriter` |
|---|---|---|---|
| parsed with capture (default) | set | false | lossless (078) |
| parsed with `captureLayout: false` | null | true | **throws** `InvalidOperationException` naming the test; nothing written |
| built in memory (new test from JSON) | null | false | canonical (as today) |

- `LayoutOmitted`: `internal`, `[JsonIgnore, YamlIgnore]`, `init`; `with` copies carry it.
- `TestCaseParser(bool captureLayout = true)`; immutable; `AttachLayout` calls `ParseCounter.LayoutCapture()`.
- `BaselineStore.ScanSuiteAsync(…, bool captureLayout = false)` — relink passes `true`.

Read-only callers (pass `false`) and writer/mixed callers (default) are listed in research R3.

## Extracted JSON (F4)

`GeneratedTestIngestor.ExtractedContent(bool Blank, string Json, JsonElement? Array)` (internal record):

| Field | Meaning | Used by |
|---|---|---|
| `Blank` | `IsNullOrWhiteSpace(content)` | `EMPTY_CONTENT` "Agent returned no content." |
| `Json` | `ExtractJson(content)` | `EMPTY_CONTENT` "No JSON array found…", `TRUNCATED` |
| `Array` | `TryParseArray(Json)` (cloned root) | `ArrayObjects`, `MALFORMED_JSON`, `NO_TESTS`, schema validation |

Counter: `ParseCounter.JsonParse()` — once per `Extract`.

## ParseCounter scope (R8)

| Member | Incremented by |
|---|---|
| `FullParses` | every `TestCaseParser.Parse` attempt |
| `LayoutCaptures` | every `AttachLayout` |
| `DocumentHeadingScans` | every whole-document heading scan in `BaselineContext` |
| `JsonParses` | every JSON array parse in `GeneratedTestIngestor` |

`ParseCounter.Begin()` returns a scope (the innermost receives increments; `AsyncLocal`, flows into child tasks;
`Interlocked`); `Dispose` restores the parent. Internal to Core; visible to `Spectra.CLI` and `Spectra.CLI.Tests`.

## Dead-code candidate (F5)

`{ name, file, src_refs, test_files, origin_spec, verdict: delete | keep | recorded }` — the R5 table; re-counted at
implement time.

## Golden step record (FR-002)

One text file per step under `tests/Spectra.CLI.Tests/Golden/099/`:

```text
# step: <n> <command line>
# exit: <code>
## stdout
<masked stdout>
## stderr
<masked stderr>
```

plus `files.txt` at the end: one block per workspace file (`### <relative path>` + masked content; binary files as a
sha256). Masks: named rules, each replacing a value with `<mask:name>`; the list is in tasks.md Notes.

## Class timing (F7)

`{ class, collection, tests, before_s, after_s, technique }` — the Notes table; plus the suite wall time before/after.
