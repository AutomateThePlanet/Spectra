# Data Model: Spec 084 — Source Change Baseline

## Files

### `{tests_dir}/{suite}/_source-baseline.json` (committed)

```json
{
  "version": 1,
  "tests": {
    "TC-101": {
      "refs": {
        "docs/storage.md#Quotas": "sha256:3f5a…(64 hex)",
        "docs/storage.md": "sha256:91c0…"
      },
      "criteria": {
        "AC-STORAGE-003": "sha256:0b7e…"
      }
    },
    "TC-102": { "refs": {}, "criteria": {} }
  }
}
```

| Rule | Detail |
|---|---|
| Keys | `tests` keys are test IDs, sorted ordinally. `refs` keys are the refs exactly as written in the test, sorted ordinally. `criteria` keys are criterion IDs, sorted ordinally. |
| Values | `^sha256:[0-9a-f]{64}$` |
| Encoding | UTF-8 without BOM; 2-space indent; LF line endings; one trailing newline; relaxed escaping (no `\u00xx` for `#`, `→` or non-ASCII). |
| Never stored | The suite name (rename-safe), timestamps, absolute paths. |
| Absent file | An empty baseline: every test is UNBASELINED. |
| Invalid file | `BASELINE_UNREADABLE`. Never treated as empty. |
| Pruning | A write drops entries whose test ID is not in the suite now. |

### `.spectra/source-snapshots/{hex}.txt` (committed)

- `{hex}` is the 64 hex digits of a ref fingerprint, and the content is `TextNormalizer.Normalize(raw section)`.
- UTF-8 without BOM, with no trailing newline.
- The file is immutable once written, and never pruned automatically.

### `.spectra/update-triage.json` (gitignored scratch)

```json
{
  "version": 1,
  "scope": {"suite": "storage"},
  "config": {"group_size": 10, "light_threshold_words": 12},
  "tests": [
    {"suite": "storage", "id": "TC-101", "bucket": "LOAD", "partial": false,
     "refs": [{"ref": "docs/storage.md#Quotas", "state": "LOAD", "baseline": "sha256:…", "current": "sha256:…",
               "why": ["numbers: 10gb→50gb"],
               "diff": [{"removed": ["10gb"]}, {"added": ["50gb"]}]}],
     "criteria": [{"id": "AC-STORAGE-003", "state": "UNCHANGED", "baseline": "sha256:…", "current": "sha256:…"}],
     "unbaselined_refs": [], "unbaselined_criteria": []}
  ],
  "groups": [{"id": "G1", "suite": "storage", "bucket": "LOAD", "tests": ["TC-101"],
              "items": ["ref:docs/storage.md#Quotas=LOAD"]}],
  "unbaselined": {"storage": {"full": [], "partial": []}},
  "notes": []
}
```

- The JSON is compact and deterministic: tests ordered by suite then ID, arrays in fixed order.
- `current` is what `update-ack --group` compares against for `TRIAGE_STALE`.
- UNCHANGED tests are included with their states, but without `why` or `diff`.

### `.spectra/baseline.lock` (gitignored)

A cross-process lock file, created on the first non-dry-run write.

## Types (new)

### `Spectra.Core.Baseline` (model-free, pure)

| Type | Shape | Notes |
|---|---|---|
| `MarkdownHeading` | `record(int Level, string Text, int StartLine)` | ATX only, outside fences |
| `SectionLookup` | `record(SectionOutcome Outcome, string? RawText, bool Ambiguous)`; `SectionOutcome { WholeFile, Found, AnchorNotFound }` | |
| `SectionExtractor` | `static IReadOnlyList<MarkdownHeading> Headings(string text)`; `static SectionLookup Extract(string text, string? anchor)`; `static string? AnchorOf(string sourceRef)` | Text is LF-normalised first |
| `AnchorKey` | `static string Make(string text)` | lowercase invariant, letters and digits only; `Uri.UnescapeDataString` for anchors |
| `SourceFingerprint` | `static string OfSection(string raw)`; `static string OfCriterion(string text)`; `static string Hex(string fingerprint)`; `static bool IsValid(string)` | `sha256:` prefix |
| `WordDiff` | `static WordDiffResult Compute(string oldNormalised, string newNormalised, int cap = 5000)` → `record(bool Computed, string? Reason, IReadOnlyList<DiffRun> Runs, int Removed, int Added)`; `record DiffRun(bool Added, IReadOnlyList<string> Words)` | Trim prefix/suffix + bounded LCS |
| `RefState` | `enum { Unchanged, Cosmetic, Light, Load, Removed }` | |
| `CriterionState` | `enum { Unchanged, Changed, Removed }` | |
| `Bucket` | `enum { Unchanged, Cosmetic, Light, CriteriaChanged, Load, CriteriaRemoved, Removed }` | Numeric order = precedence; `Unbaselined` is not a bucket |
| `RefVerdict` | `record(string Ref, RefState State, string Baseline, string? Current, IReadOnlyList<string> Why, IReadOnlyList<DiffRun> Diff)` | |
| `ChangeClassifier` | `static RefVerdict ClassifyRef(string sourceRef, string baseline, CurrentSection current, string? snapshot, int lightThreshold)` | `CurrentSection` = `record(bool Missing, string? MissingReason, string? Fingerprint, string? Normalised)` |
| `BucketRules` | `static Bucket Of(IEnumerable<RefState>, IEnumerable<CriterionState>)` | |
| `TriageGrouper` | `static IReadOnlyList<TriageGroup> Group(IEnumerable<TestVerdict>, int groupSize)` → `record TriageGroup(string Id, string Suite, Bucket Bucket, IReadOnlyList<string> Items, IReadOnlyList<string> Tests, IReadOnlyList<string> Why)` | `G1…` across the run |
| `SourceBaseline` / `BaselineEntry` | `SortedDictionary` models (ordinal) | |
| `SourceBaselineSerializer` | `static string Serialize(SourceBaseline)`; `static SourceBaseline Parse(string json, string fileForMessages)` (throws `BaselineFormatException`) | Deterministic writer, strict reader |

### `Spectra.CLI.Baseline`

| Type | Responsibility |
|---|---|
| `BaselineStore` | `WithLockAsync`, `LoadSuiteAsync` (strict), `SaveSuiteAsync` (prune, skip identical, atomic), `EnsureSnapshotAsync`, `ReadSnapshotAsync`. Errors are `BaselineStoreException(code)` with `LOCK_TIMEOUT` / `BASELINE_UNREADABLE`. |
| `BaselineContext` | A per-run cache: `SourceRefResolver`, file text by full path, sections by (file, anchor), criteria by ID (from `CriteriaStore.ScanFilesAsync`, first wins), snapshot text by hex. It collects notes (ambiguous anchors, duplicate criteria). |
| `BaselineRecorder` | `ComputeEntryAsync(TestCase, BaselineContext)` → `(BaselineEntry, pendingSnapshots)`, plus the counters (created, updated, unchanged, snapshots). |
| `TriageEngine` | Loads suites and tests, computes `TestVerdict`s, auto-acks COSMETIC, groups, builds `no_tests`, and builds the stdout and detail documents. |
| `TriagePresenter` | The human summary table (stderr in JSON mode, stdout otherwise). |
| `BaselineWriteSummary` | `record(int Created, int Updated, int Unchanged, int AutoAcked, int SnapshotsWritten)`, emitted by every writer. |

### Changed types

| Type | Change |
|---|---|
| `UpdateConfig` | `+ int? GroupSize` (`group_size`), `+ int? LightThresholdWords` (`light_threshold_words`), `+ EffectiveGroupSize`, `+ EffectiveLightThresholdWords` |
| `ConfigLoader` | `CheckedSections += ("update", None)`; `Validate` adds the range errors |
| `InitHandler` | gitignore patterns `+ .spectra/baseline.lock`, `+ .spectra/update-triage.json` |
| `IngestTestsCommand`, `IngestUpdateCommand` | Lock → strict baseline load → persist → record. The success JSON gains `baseline`. |
| `AiCommand` | Registers `update-triage`, `update-ack` and `source-baseline` |

## State transitions (per ref, per test)

```
            (writer records)            (doc edited)                 (ack / ingest-update / auto-ack)
UNBASELINED ───────────────► UNCHANGED ──────────────► COSMETIC/LIGHT/LOAD/REMOVED ──────────────► UNCHANGED
                                 ▲                         │ (not acknowledged: stays, D5)
                                 └─────────────────────────┘  reappears on every triage
```

- COSMETIC goes back to UNCHANGED on the next non-dry-run triage, unless `--no-auto-ack` is given.
- REMOVED goes back to UNCHANGED once the test no longer lists that ref. That happens through a hand edit followed
  by `update-ack --ids` (or a triage, since stale keys are ignored), through an `ingest-update` that changes the refs,
  or later through `relink-sources` (Phase 2b). If the feature itself was removed, the user deletes the test
  (`spectra delete`); its entry becomes stale and is pruned on the next write. `update-ack` refuses a test while it
  still lists the ref
  (`REF_REMOVED`), and the same goes for CRITERIA_REMOVED (`CRITERION_REMOVED`).
- Stale keys (refs or criteria the test no longer lists) are dropped only when the whole entry is recomputed:
  ingest-tests, ingest-update, update-ack, or `source-baseline --init --force`. Auto-ack and `--init` without
  `--force` keep them (spec FR-026a).
