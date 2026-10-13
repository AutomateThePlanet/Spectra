# Data Model: Plan Only Uncovered Behaviours

## Coverage of a behaviour

| Field | Rule |
|---|---|
| similarity | `1 − Levenshtein(norm(a), norm(b)) / max(len)`; `norm` = lowercase, letters/digits/whitespace only, single spaces (the duplicate detector's measure); empty title → 0 |
| covered | best similarity ≥ `generation.duplicate_threshold` (default 0.6; used as given) |
| `covered_by` | id of the most similar test; equal similarities → lowest id (ordinal); null when uncovered |

`already_covered` = covered behaviours; `recommended` = uncovered behaviours (after the focus filter). The breakdowns
cover all behaviours.

## Generation plan (094 format, additions in bold)

```json
{
  "version": 1,
  "suite": "storage",
  "focus": null,
  "batch_size": 8,
  **"duplicate_threshold": 0.6,**
  "recommended": 12,
  "planned_total": 12,
  "count_rule": "proportional",
  "behaviors": [
    {"id": "B1", "title": "…", "category": "…", "technique": "…", "source": "…", "ref": "…", "resolved": true, **"covered_by": "TC-104"**},
    {"id": "B2", "title": "…", "category": "…", "technique": "…", "source": "…", "ref": "…", "resolved": true, **"covered_by": null**}
  ],
  "sections": { "…refs used by batches only…": "sha256:…" },
  "batches": [ …uncovered behaviours only… ],
  "notes": []
}
```

- `duplicate_threshold` is present in every 095 plan. Its absence marks a 094 plan: coverage unknown, and every
  behaviour is treated as uncovered.
- `covered_by` is written for every behaviour (null when uncovered). It is optional on read, and missing means null.
- Batches, `unsourced` and counts use only behaviours with `covered_by: null`; `sections` lists only the refs those
  batches use.

## ingest-analysis JSON additions

```json
"covered": [{"id": "B1", "title": "Upload above 30 GB is refused", "covered_by": "TC-104"}]
```

Human summary: `covered: <title> (<covered_by>)` per covered behaviour (after the batch lines, before `unresolved:`).
`unresolved` (JSON and `unresolved:` lines) lists only uncovered behaviours whose source didn't resolve; a covered
behaviour appears only under `covered`.

Plan counts: each batch's `count` = its number of (uncovered) behaviours; Σ = `recommended`.

Threshold outside (0, 1]: one stderr `Warning:` line — "generation.duplicate_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered."

## compile-prompt --batch

On a 094 plan: an extra warning, "the generation plan predates per-behaviour coverage; re-run the analysis for
corrected counts".

## Agent / skill lines

```
analyst += ( "covered: " <title> " (" <covered_by> ")" )*        (verbatim from the CLI)
skill Step 4: "Already covered — not generated:" + the covered lines; Step 9: the same list
```
