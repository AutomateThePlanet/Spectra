# Phase 1 Data Model: Boundary-coverage gap detection

## Entity: `BoundaryGap` (NEW)

`Spectra.CLI.Agent.Analysis.BoundaryGap` — a single uncovered boundary condition surfaced by the in-session analysis. Deserialized from one element of the top-level `boundary_gaps` JSON array. Advisory; never persisted.

| Field | JSON key | Type | Required for validity | Notes |
|-------|----------|------|-----------------------|-------|
| `Field` | `field` | string | **Yes** (non-blank) | The parameter/field or behavior the boundary concerns, e.g. `"username"`, `"order total"`. |
| `Kind` | `kind` | string | **Yes** (non-blank) | Free-form, expected vocabulary: `min-max`, `off-by-one`, `empty-null`, `overflow`, `timeout`, `max-length`. Free string (not enum) to avoid rejecting valid-but-novel kinds. |
| `Description` | `description` | string | **Yes** (non-blank) | Short description of the missing edge, e.g. `"max length boundary (20) has no test"`. |
| `Source` | `source` | string | No (defaults `""`) | Document/criterion that implies the boundary. Empty is allowed (model may infer from combined context) — empty source is NOT malformed. |

### Validation rules (fail-loud — FR-003)

Applied in `AnalysisRecommendationBuilder` after the behaviors parse:

1. **Absent key** → `BoundaryGaps = []`, outcome stays `Recommendation`. (Backward compatible: legacy output.)
2. **Present but not a JSON array** → `ParseFailure` with message `"boundary_gaps must be a JSON array."`
3. **Present, array, but an element is not an object OR has blank `field`/`kind`/`description`** → `ParseFailure` with message naming the offending element index and the missing field, e.g. `"boundary_gaps[2] is missing required field 'kind'."`
4. **Well-formed** → `BoundaryGaps` carries every parsed gap, outcome `Recommendation`.

Malformed boundary gaps route through the **existing** `AnalysisIngestOutcome.ParseFailure` → exit code **6**, with the boundary-gap-specific message in `Errors`. No new enum value, no new exit code.

## Entity: `AnalysisRecommendation` (MODIFIED — additive)

`Spectra.CLI.Generation.AnalysisRecommendation` gains one additive field; all existing fields/factories unchanged in meaning.

| New member | Type | Notes |
|------------|------|-------|
| `BoundaryGaps` | `IReadOnlyList<BoundaryGap>` | Defaults to `[]`. Populated only on the `Recommendation` outcome. |

- `Recommendation(...)` factory gains a `boundaryGaps` parameter (defaulted to empty for call-site safety, or overloaded — see tasks). Damage factories (`Empty`, `ParseFail`) leave it `[]`.
- No change to `Outcome`, `IsSuccess`, `TotalBehaviors`, `AlreadyCovered`, `RecommendedCount`, `Breakdown`, `TechniqueBreakdown`, `DocumentsAnalyzed`, `Errors`.

## Relationships

```
agent JSON object
├── behaviors[]      → List<IdentifiedBehavior>   (existing, tolerant parse)
├── field_specs[]    → List<FieldSpec>            (existing, Testimize-gated)
└── boundary_gaps[]  → List<BoundaryGap>          (NEW, strict parse, fail-loud)

AnalysisRecommendation
├── TechniqueBreakdown : IReadOnlyDictionary<string,int>   (existing)
└── BoundaryGaps       : IReadOnlyList<BoundaryGap>        (NEW, sibling/advisory)
```

`boundary_gaps` is **independent** of the `behaviors`/`technique_breakdown` accounting: it does not change `TotalBehaviors`, `RecommendedCount`, or any breakdown count (FR-005 — advisory, non-mutating).

## Ingest output additions (`IngestAnalysisCommand`)

JSON success object gains `boundary_gaps` (array of `{field, kind, description, source}`):

```json
{
  "outcome": "Recommendation",
  "already_covered": 12,
  "recommended": 18,
  "breakdown": { "happy_path": 6, "negative": 5, "edge_case": 4 },
  "technique_breakdown": { "BVA": 4, "EP": 6, "DT": 2 },
  "documents_analyzed": 3,
  "boundary_gaps": [
    { "field": "username", "kind": "max-length", "description": "21-char input (max 20) untested", "source": "docs/signup.md" }
  ]
}
```

Human output gains a `Boundary gaps:` section listing each gap (`{kind}` · `{field}` — `{description}`), printed only when the list is non-empty. Absent/empty → section omitted (no noise, FR-004).
