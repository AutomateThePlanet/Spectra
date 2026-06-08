# Contract: `spectra ai ingest-analysis` output (updated for Spec 062)

Extends the Spec 059 contract additively. No flags change; no exit codes change.

## Input

Agent behavior JSON (from `--from <file>` or stdin), one object:

```json
{
  "behaviors": [ { "category": "...", "title": "...", "source": "...", "technique": "..." } ],
  "boundary_gaps": [ { "field": "...", "kind": "...", "description": "...", "source": "..." } ]
}
```

`boundary_gaps` is **optional**. `field_specs` (Testimize) may also be present and is unaffected by this feature.

## Behavior

| Condition | Outcome | Exit | `boundary_gaps` in output |
|-----------|---------|------|---------------------------|
| `boundary_gaps` absent | `Recommendation` | 0 | `[]` |
| `boundary_gaps` present, well-formed | `Recommendation` | 0 | carries every gap |
| `boundary_gaps` present, not an array | `ParseFailure` | 6 | n/a — error `"boundary_gaps must be a JSON array."` |
| `boundary_gaps` element missing `field`/`kind`/`description` | `ParseFailure` | 6 | n/a — error names index + missing field |
| content empty/whitespace | `EmptyResponse` | 5 | n/a |
| zero behaviors parsed | `ParseFailure` | 6 | n/a |

Boundary gaps **never** alter `recommended`, `already_covered`, `breakdown`, or `technique_breakdown` (advisory, non-mutating — FR-005).

## Output (success, `--output-format json`)

```json
{
  "outcome": "Recommendation",
  "already_covered": 12,
  "recommended": 18,
  "breakdown": { "happy_path": 6, "negative": 5 },
  "technique_breakdown": { "BVA": 4, "EP": 6 },
  "documents_analyzed": 3,
  "boundary_gaps": [
    { "field": "username", "kind": "max-length", "description": "21-char input (max 20) untested", "source": "docs/signup.md" }
  ]
}
```

`boundary_gaps` is always present on success (possibly `[]`).

## Output (success, human)

Existing sections unchanged. When `boundary_gaps` is non-empty, append:

```
Boundary gaps:
  max-length · username — 21-char input (max 20) untested  [docs/signup.md]
```

Omitted entirely when empty (no "no gaps" noise — FR-004).

## Output (failure, malformed boundary gaps)

`--output-format json` → stderr:

```json
{ "outcome": "ParseFailure", "errors": ["boundary_gaps[2] is missing required field 'kind'."] }
```

Human → stderr:

```
Analysis ingest failed [ParseFailure] — no recommendation produced:
  - boundary_gaps[2] is missing required field 'kind'.
```

## Untouched

`spectra ai compile-critic-prompt` / `ingest-verdict` and the `VerdictIngestor` verdict vocabulary (`grounded`/`partial`/`hallucinated`/`manual`) are **not** modified by this contract.
