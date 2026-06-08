# Contract: `spectra ai ingest-analysis` (new)

New Spec 059 command mirroring the 053/054/055 ingest-* pattern. Fail-loud, no model call. Takes the agent's behavior JSON, runs deterministic accounting, and emits the recommendation the skill presents for approval.

## Synopsis

```
spectra ai ingest-analysis --suite <suite> [--from <file>] [--output-format json]
# or pipe: cat behaviors.json | spectra ai ingest-analysis --suite <suite> --output-format json
```

## Options

| Option | Type | Default | Notes |
|--------|------|---------|-------|
| `--suite, -s` | string | (required) | Suite the analysis targets (for coverage dedup). |
| `--from` | string? | null | File with agent JSON; omit to read **stdin**. |
| `--output-format` | enum | human | `json` for the structured recommendation. |

## Input

Agent JSON: an array of identified behaviors, each carrying at least a title/description, a category, and an ISTQB technique tag (may be wrapped in a markdown code block, like the other ingest commands).

## Behavior (deterministic — relocated from `BehaviorAnalyzer` lines ~158–172)

1. Parse the behavior JSON (fail-loud on empty/malformed/missing-fields).
2. Dedup against existing coverage: use `CoverageSnapshot` when available for an accurate `AlreadyCovered`, else title-similarity heuristic.
3. Compute `Breakdown` (per category) and `TechniqueBreakdown` (per ISTQB technique) by grouping/counting.
4. Compute `RecommendedCount = max(0, TotalBehaviors − AlreadyCovered)`.
5. Emit the `AnalysisRecommendation`.

## Output (success, exit 0)

JSON shape consumed by the skill (same field names it renders today):

```json
{
  "outcome": "Recommendation",
  "already_covered": 12,
  "recommended": 18,
  "breakdown": { "happy_path": 6, "negative": 5, "edge_case": 4, "...": 0 },
  "technique_breakdown": { "BVA": 4, "EP": 6, "DT": 2, "ST": 1, "EG": 3, "UC": 2 },
  "documents_analyzed": 3
}
```

Human form prints the same recommendation lines the skill's Step 4 shows.

## Exit codes (mirror ingest-tests / ingest-criteria / ingest-verdict)

| Code | Meaning |
|------|---------|
| 0 | Recommendation produced. |
| 5 | `EmptyResponse` — no behaviors / empty content; no recommendation. |
| 6 | `ParseFailure` — missing/unparseable required fields (damage). |
| 1 | File I/O or config error. |

Persists nothing — the recommendation is advisory; the skill STOPs for user approval before any generation.

## Acceptance mapping

- FR-001/FR-003 (analyze-only on the seam), FR-002 (fail-loud surface for the choreography), US3 AS1/AS2/AS3.
