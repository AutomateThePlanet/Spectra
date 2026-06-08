# Quickstart: Coverage-Scoped Document Exclusions

**Feature**: 060-coverage-doc-exclusions

## What it does

Drops chosen documents (release notes, changelogs, summaries, archived material) from the
**documentation-coverage percentage** without removing them from generation, analysis, or indexing.
Excluded docs are reported with a distinct `excluded` status — never silently dropped.

## Configure

Add the new key to `spectra.config.json` under `coverage`:

```jsonc
{
  "coverage": {
    "coverage_exclude_patterns": [
      "docs/release-notes/**",
      "**/SUMMARY.md"
    ]
  }
}
```

Empty or absent → no change from today.

## Run

```bash
spectra ai analyze --coverage
spectra ai analyze --coverage --format json --output coverage.json
```

## Verify

- **Denominator shrank correctly**: `total_docs` now excludes the matched docs; `percentage` is
  computed over the remainder. `excluded_docs` shows how many were dropped.
- **Docs still present elsewhere**: run generation/analysis/index over the same docs — they are still
  processed (the exclusion is coverage-only).
- **Visibly reported**: each excluded doc appears in `details` with `"excluded": true` and
  `"excluded_by_pattern"`; the human-readable report shows an `Excluded` status, not `Yes`/`No`.
- **No-config safety**: remove the key and re-run — output is identical to before the feature.

## Don't confuse it with the other two

| Want to… | Use |
|----------|-----|
| Make a doc vanish from **everything** (gen, analysis, coverage) | `source.exclude_patterns` |
| Keep a doc indexed but skip **analysis** of it (`skip_analysis: true`) | `coverage.analysis_exclude_patterns` |
| Keep a doc everywhere but drop it from the **coverage %** only | `coverage.coverage_exclude_patterns` ← this feature |

## Test the feature locally

```bash
dotnet test --filter "FullyQualifiedName~Coverage"
```

Key tests: denominator filtering, excluded-status reporting, no-config equivalence, and the
three-concept disambiguation (a doc matched only by `analysis_exclude_patterns` still counts).
