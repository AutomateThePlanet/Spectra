# Contract: Coverage Context in Analysis Output

**Date**: 2026-04-13 | **Feature**: 044-coverage-aware-analysis

## JSON Output Contract (--output-format json)

When `spectra ai generate --analyze-only --output-format json` runs on a suite with existing tests, the `analysis` object in the result JSON includes coverage fields:

### New fields in `analysis` object

```json
{
  "status": "success",
  "analysis": {
    "total_behaviors": 8,
    "already_covered": 233,
    "recommended": 8,
    "breakdown": { "boundary": 2, "negative": 2, "happy_path": 3, "edge_case": 1 },
    "technique_breakdown": { "BVA": 3, "EP": 2, "EG": 1, "UC": 2 },
    "existing_test_count": 231,
    "total_criteria": 41,
    "covered_criteria": 38,
    "uncovered_criteria": 3,
    "uncovered_criteria_ids": ["AC-039", "AC-040", "AC-041"]
  }
}
```

### Backward compatibility

- All new fields have defaults: `existing_test_count: 0`, `total_criteria: 0`, `covered_criteria: 0`, `uncovered_criteria: 0`, `uncovered_criteria_ids: []`
- Old consumers that don't read these fields are unaffected
- When no coverage data is available (new suite), all new fields are 0/empty

## Prompt Template Contract

### New placeholder: `{{coverage_context}}`

Added to `behavior-analysis.md` template. Resolves to a markdown block or empty string.

**Full mode** (tests <= 500): Includes covered criteria IDs, uncovered criteria details, covered/uncovered source refs, and truncated test titles.

**Summary mode** (tests > 500): Includes only coverage statistics and uncovered items. No title list.

**Empty** (no coverage data): Placeholder resolves to empty string. Template behavior is identical to pre-feature.

### User customization

Users can override `behavior-analysis.md` via `.spectra/prompts/behavior-analysis.md`. If their template omits `{{coverage_context}}`, coverage data is simply not injected — no error, no crash.
