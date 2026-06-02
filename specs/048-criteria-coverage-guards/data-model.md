# Phase 1 Data Model: Criteria Coverage Guards (Spec 048)

**Branch**: `048-criteria-coverage-guards`
**Date**: 2026-06-02

This spec adds **three additive fields and one internal record**. All on-disk and JSON additions are backward-compatible (optional, default-omittable, default value documented for legacy reads).

---

## On-disk: `CriteriaSource` (master criteria index entry)

**File**: `docs/criteria/_criteria_index.yaml` (existing — entries already persisted by Spec 040+).
**Type**: `Spectra.Core.Models.Coverage.CriteriaSource` — `src/Spectra.Core/Models/Coverage/CriteriaSource.cs`.

### Change

Add one new optional field:

```csharp
[YamlMember(Alias = "outcome")]
public string Outcome { get; set; } = "extracted";
```

### Field semantics

| Value | Meaning | Written when |
|---|---|---|
| `extracted` | Affirmed extraction outcome. The document was processed; the criteria list (possibly empty) is the genuine answer from the extractor. The record is cacheable. | Every path that writes a `CriteriaSource` today writes this value. Spec 047 guarantees only `Extracted` results reach the cache; that invariant is what makes this the only valid current write-value. |
| *(missing key)* | Legacy entry written before this spec. **Interpreted as `extracted`** by the C# default value. | Never written by this code — only read. |
| `empty_response`, `parse_failure` | Reserved for future use. | Never written today. Reserved so guards can express future relaxations of Spec 047's invariant without a schema version bump. |

### Backward compatibility (FR-002)

YamlDotNet's deserializer applies the C# property default for missing keys. A legacy entry like:

```yaml
- file: docs/criteria/login.criteria.yaml
  source_doc: docs/login.md
  source_type: document
  doc_hash: abc123
  criteria_count: 0
  last_extracted: 2026-04-12T10:00:00Z
```

deserializes with `Outcome == "extracted"` — no migration step required, no version bump.

### Forward compatibility

Adding new outcome values is non-breaking — they remain plain strings. Consumers that only recognize `"extracted"` continue to read the field; unknown values fail the "affirmed extracted" gate and a guard could legitimately warn on them.

---

## JSON result: `DocsIndexResult.CriteriaWarning`

**File**: `src/Spectra.CLI/Results/DocsIndexResult.cs`.
**Type**: `string?`, omitted from JSON when null.

### Shape

```csharp
[JsonPropertyName("criteria_warning")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? CriteriaWarning { get; init; }
```

### Semantics

- **Set to a non-null string** when, and only when, the corpus-zero condition holds (FR-005, FR-006): `documentsIndexed > 0 && criteriaExtractedTotal == 0` AND `--skip-criteria` was NOT passed AND `--dry-run` was NOT in effect.
- **Null/absent** otherwise. Consumers detect the state via presence check (`"criteria_warning" in result`).

### Value (D4)

When set, equal to the warning message string (constant up to the document-count interpolation):

```text
Indexed 12 document(s) but extracted 0 acceptance criteria. Test generation will not be able to link criteria. Run: spectra ai analyze --extract-criteria
```

### Existing field cross-reference

The existing `CriteriaExtracted` (`:53-55`) carries the count. When `CriteriaWarning` is present, `CriteriaExtracted == 0` (by construction) and `DocumentsIndexed > 0`. Tests can assert the cross-field invariant.

---

## JSON result: `GenerateResult.Notes`

**File**: `src/Spectra.CLI/Results/GenerateResult.cs`.
**Type**: `IReadOnlyList<string>?`, omitted from JSON when null. The collection is non-null and contains exactly one element when a no-match note applies; null otherwise.

### Shape

```csharp
[JsonPropertyName("notes")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public IReadOnlyList<string>? Notes { get; init; }
```

### Semantics

- **One-element list** when, and only when, `LoadCriteriaContextAsync(...).SuiteMatchedCount == 0` (FR-009) on a result built by `ExecuteDirectModeAsync` final-result or `ExecuteFromDescriptionAsync` (D5).
- **Null/absent** otherwise.

### Value (D4)

```text
No acceptance criteria matched suite 'checkout'. Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. Run 'spectra ai analyze --extract-criteria' if criteria are expected.
```

`{suite}` is interpolated from the run's suite name.

### Future shape

The collection (rather than scalar) is intentional. Future specs can add additional notes (e.g., from-description specific guidance) without changing the field type — just appending entries.

---

## Internal: `CriteriaContextResult` (in-process)

**File**: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` (private to handler).
**Type**: internal sealed record.

### Shape

```csharp
internal sealed record CriteriaContextResult(
    string? Context,
    int SuiteMatchedCount,
    int TotalCriteriaCount);
```

### Replaces

`LoadCriteriaContextAsync`'s current return type `Task<string?>` (D3). The existing return becomes `result.Context`; the new fields support FR-009.

### Field semantics

| Field | Meaning |
|---|---|
| `Context` | Formatted markdown context to inject into the prompt (today's return). `null` when no criteria files exist on disk. |
| `SuiteMatchedCount` | Number of `AcceptanceCriterion` entries that matched the suite via the component/source-doc/file-name filters in `LoadCriteriaContextAsync` (lines 2452-2482 in current code). **Does NOT include the last-resort "all criteria" fallback** (`:2485-2486`). Strictly zero when no criterion legitimately matched the suite — this is the value the no-match note keys on. |
| `TotalCriteriaCount` | Total criteria loaded from disk (informational; for future tuning). |

### Invariants

- `SuiteMatchedCount >= 0`
- `TotalCriteriaCount >= SuiteMatchedCount` (a matched criterion is by definition loaded)
- When `TotalCriteriaCount == 0`, `Context` is null and `SuiteMatchedCount == 0` (the early-return path on line 2449).
- When `SuiteMatchedCount > 0`, `Context` is non-null.
- When `SuiteMatchedCount == 0 && TotalCriteriaCount > 0`, `Context` may be either `null` (no matches and no fallback context produced) or non-null (last-resort fallback engaged) — the note fires either way.

---

## Type relationships (informal diagram)

```
docs/criteria/_criteria_index.yaml
└── CriteriaSource[]                          (Spectra.Core.Models.Coverage)
    ├── file, source_doc, source_type
    ├── doc_hash, criteria_count
    ├── last_extracted, imported_at
    └── outcome  ◀── NEW (default "extracted")
                            ▲
                            │ written by
                            │
AnalyzeHandler.RunExtractCriteriaAsync         (Spectra.CLI.Commands.Analyze)
└── upserts CriteriaSource with Outcome = "extracted"

DocsIndexHandler.TryExtractCriteriaAsync       (Spectra.CLI.Commands.Docs)
└── computes CriteriaWarning when zero-criteria condition holds
                            │
                            ▼
DocsIndexResult                                (Spectra.CLI.Results)
└── criteria_warning  ◀── NEW (nullable, WhenWritingNull)

GenerateHandler.LoadCriteriaContextAsync       (Spectra.CLI.Commands.Generate)
└── returns CriteriaContextResult              (internal record — NEW)
            ├── Context
            ├── SuiteMatchedCount  ◀── keys the no-match note
            └── TotalCriteriaCount

GenerateHandler.{ExecuteDirectMode|ExecuteFromDescription}Async
└── attaches Notes[0] when SuiteMatchedCount == 0
                            │
                            ▼
GenerateResult                                 (Spectra.CLI.Results)
└── notes  ◀── NEW (IReadOnlyList<string>?, WhenWritingNull)
```

## Validation rules summary

| Rule | Where | Test name |
|---|---|---|
| Legacy `CriteriaSource` deserializes with `Outcome == "extracted"` | `Spectra.Core` deserializer (no code change — YamlDotNet default) | `CriteriaSource_Roundtrip_PreservesOutcome` |
| Newly-written `CriteriaSource` serializes `outcome: extracted` | `AnalyzeHandler.RunExtractCriteriaAsync` upsert | `Extract_RealEmpty_RecordsOutcomeExtracted` |
| `criteria_warning` present iff `documentsIndexed > 0 && criteriaExtractedTotal == 0 && !skipCriteria` | `DocsIndexHandler.TryExtractCriteriaAsync` | `DocsIndex_ZeroCriteriaAcrossCorpus_WarnsNonBlocking`, `DocsIndex_CriteriaFound_NoWarning`, `DocsIndex_RealEmptyDocs_NoFalseWarning` |
| `notes` contains the no-match note iff `SuiteMatchedCount == 0` for the resolved suite | `GenerateHandler.ExecuteDirectModeAsync` + `ExecuteFromDescriptionAsync` | `Generate_Batch_NoMatchedCriteria_AddsNote`, `Generate_FromDescription_NoMatchedCriteria_AddsNote`, `Generate_WithMatchedCriteria_NoNote` |
| `notes` is present in JSON regardless of `--verbosity quiet` | `GenerateHandler.ExecuteDirectModeAsync` final result | `Generate_Note_PresentInJson_EvenWhenQuiet` |
