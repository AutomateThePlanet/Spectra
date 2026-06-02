# Contract: `CriteriaSource.Outcome` (YAML schema additive)

**File**: `src/Spectra.Core/Models/Coverage/CriteriaSource.cs`
**Format**: `docs/criteria/_criteria_index.yaml`

## What's changing

Add one optional field to each entry in `_criteria_index.yaml`'s `sources:` list.

### Before

```yaml
sources:
  - file: docs/criteria/login.criteria.yaml
    source_doc: docs/login.md
    source_type: document
    doc_hash: c0ffee
    criteria_count: 3
    last_extracted: 2026-04-12T10:00:00Z
```

### After

```yaml
sources:
  - file: docs/criteria/login.criteria.yaml
    source_doc: docs/login.md
    source_type: document
    doc_hash: c0ffee
    criteria_count: 3
    last_extracted: 2026-04-12T10:00:00Z
    outcome: extracted   #  ◀── NEW
```

## Field

| Property | Type | Required | Default | YAML alias |
|---|---|---|---|---|
| `Outcome` | `string` | No (additive) | `"extracted"` | `outcome` |

## Valid values

| Value | Status | Written by |
|---|---|---|
| `extracted` | **Active.** Affirmed extraction. The record is cacheable; the `criteria_count` is the genuine answer (including a genuine `0`). | All current code paths that upsert a `CriteriaSource` (Spec 047 invariant). |
| *(field missing)* | **Legacy.** Pre-Spec-048 entries that don't carry the field. Interpreted as `extracted` by the property default. | Read-only — never re-written. |
| `empty_response`, `parse_failure` | **Reserved.** Not written today (Spec 047 prevents these outcomes from reaching the cache). Reserved so future relaxations of the invariant don't need a schema version bump. | Reserved. |

Unknown values are tolerated on read (the field is a plain string) but should be treated by guards as "not affirmed extracted" — i.e., guards that would normally suppress a warning on `extracted` should not suppress on unknown values.

## Round-trip behaviour

| Scenario | Behaviour |
|---|---|
| Legacy entry (no `outcome` key) → read → re-write | Written back with `outcome: extracted` (the default materializes during the upsert path). |
| New entry written by Spec 048+ code | Always carries `outcome: extracted`. |
| Manual user edit that sets `outcome:` to something unusual | Preserved on read; no migration; guards may surface it. |

## Backward compatibility

- **Pre-048 readers**: ignore the unknown field. YamlDotNet's default deserializer skips unmapped keys (no `IgnoreUnmatchedProperties` change needed — that's already the default in this codebase). Verified by attempting to round-trip a `_criteria_index.yaml` containing `outcome:` through the pre-048 `CriteriaSource` shape in a regression test (out of scope for spec 048 itself but trivial to confirm).
- **Pre-048 writers**: continue to omit the field. Spec 048+ readers fill in `extracted` via the property default — FR-002.
- **No file-format version bump.** The change is additive and the contract preserves it that way going forward.

## Consumer guidance for guards

When a guard reads a `CriteriaSource` and considers warning on `criteria_count == 0`:

| `outcome` value | Guard behaviour |
|---|---|
| `extracted` (or missing — interpreted as same) | **Suppress** the warning. The empty is affirmed. |
| Anything else (including reserved future values) | **Allow** the warning. The empty is not affirmed. |

In Spec 048 specifically, the only consumer is the corpus-level zero check in `DocsIndexHandler` (D2 in research.md). That check sums extracted criteria across the current run rather than reading the index — so it bypasses the per-record outcome distinction at the corpus level. The outcome field is forward-prep for record-level guards in future specs.

## Test contract

| Test | Asserts |
|---|---|
| `CriteriaSource_Roundtrip_PreservesOutcome` | A `CriteriaSource` with `Outcome = "extracted"` serializes with `outcome: extracted` and deserializes back to `Outcome == "extracted"`. A YAML payload without an `outcome:` key deserializes with `Outcome == "extracted"` by default. |
| `Extract_RealEmpty_RecordsOutcomeExtracted` | Driving the analyze handler with a stub extractor that returns `Extracted` + empty criteria list writes a `CriteriaSource` entry with `criteria_count: 0` AND `outcome: extracted`. |
