# Contract: `DocsIndexHandler` zero-criteria corpus warning

**File**: `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs`

## What's changing

When `docs index` finishes its criteria-extraction phase, the handler computes a corpus-zero condition and surfaces a non-blocking warning when it holds.

### Existing signature

```csharp
private async Task<(int? criteriaCount, string? criteriaFile)> TryExtractCriteriaAsync(
    string currentDir, SpectraConfig config, CancellationToken ct)
```

### New signature

```csharp
private async Task<(int? criteriaCount, string? criteriaFile, string? criteriaWarning)> TryExtractCriteriaAsync(
    string currentDir, SpectraConfig config, CancellationToken ct)
```

The third tuple element carries the warning string when the corpus-zero condition holds, `null` otherwise. The caller at `:199` destructures all three and threads the warning into `DocsIndexResult.CriteriaWarning`.

## Behavioural contract

| Condition (at end of extraction phase) | `criteriaWarning` value | `_progress.Warning` emitted? | Exit code |
|---|---|---|---|
| `--skip-criteria` passed (method not called) | n/a — caller assigns `null` | No | `Success` |
| `--dry-run` (early-return inside `TryExtractCriteriaAsync` before the gate) | `null` | No | `Success` |
| `documentMap.Documents.Count == 0` (no documents to extract from) | `null` | No | `Success` |
| `documentMap.Documents.Count > 0` AND `extracted.Count == 0` (the zero condition) | The warning string (D4) | Yes (suppressed under `--verbosity quiet`) | `Success` |
| `documentMap.Documents.Count > 0` AND `extracted.Count > 0` (criteria found) | `null` | No | `Success` |
| Extraction threw (caught in existing `try/catch`) | `null` (return path returns `(null, null, null)`) | Existing failure warning unchanged | `Success` |

Key invariants:

1. The warning is **never** the only signal of failure — it's an informational nudge. Exit code does not change.
2. The warning is **scoped to the run** — it does not consult the on-disk `_criteria_index.yaml`. A run that extracts zero criteria on a corpus where some `.criteria.yaml` files already exist still triggers the warning, because *this run* produced nothing. Re-running with `--skip-criteria` would not trigger it.
3. The warning is **independent of `outcome` field state** — the per-record outcome distinction (Contract: criteria-source) is a per-record concern; the docs-index warning is a per-run corpus-level concern.

## Message format (D4)

```text
Indexed {N} document(s) but extracted 0 acceptance criteria. Test generation will not be able to link criteria. Run: spectra ai analyze --extract-criteria
```

`{N}` is interpolated from `documentMap.Documents.Count` (NOT `newLayout.Manifest.TotalDocuments` — the former is the count actually subjected to extraction after manifest filtering; the latter includes filtered-out docs).

The same string is:
- written via `_progress.Warning(...)` for human-facing output (gated by `_verbosity`)
- assigned to `DocsIndexResult.CriteriaWarning` for the JSON channel

## JSON shape

```jsonc
{
  "command": "docs-index",
  "status": "completed",
  "documents_indexed": 12,
  "documents_total": 12,
  "criteria_extracted": 0,
  "criteria_warning": "Indexed 12 document(s) but extracted 0 acceptance criteria. Test generation will not be able to link criteria. Run: spectra ai analyze --extract-criteria",
  /* … remaining fields unchanged … */
}
```

When the condition does not hold, the `criteria_warning` key is **absent** from the JSON (not `null`, not `""`) — enforced by `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`.

## Test contract

| Test | Setup | Asserts |
|---|---|---|
| `DocsIndex_ZeroCriteriaAcrossCorpus_WarnsNonBlocking` | Stub extractor returns zero criteria for every doc; >=1 doc in the manifest; `--skip-criteria` NOT passed | `criteria_warning` present in JSON; message contains "spectra ai analyze --extract-criteria"; exit code is success; `_progress.Warning` was called once |
| `DocsIndex_CriteriaFound_NoWarning` | Stub extractor returns >=1 criterion for at least one doc | `criteria_warning` key absent from JSON; `_progress.Warning` not called for this condition |
| `DocsIndex_RealEmptyDocs_NoFalseWarning` (modified per Edge Cases) | Stub extractor returns 0 criteria for some docs and >=1 for others — corpus total > 0 | `criteria_warning` key absent from JSON |
| `DocsIndex_SkipCriteria_SuppressesWarning` | `--skip-criteria` passed | `criteria_warning` key absent from JSON regardless of any criteria state |

All tests drive the loop via the existing `ExtractCriteriaLoopAsync` internal static seam (already in place from 047) — no real AI call.
