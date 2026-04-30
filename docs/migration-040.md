---
layout: default
title: "Migration: Spec 040 — Document Index Restructure"
parent: Migration Guides
nav_order: 40
---

# Migration: Spec 040 — Document Index Restructure

**Affects releases:** 1.51.0 and later
**Breaking change:** No (auto-migration on first run)
**Backed-up:** Yes (legacy `_index.md` preserved as `_index.md.bak`)

## What changed

The single-file `docs/_index.md` has been replaced with a per-suite layout
under `docs/_index/`:

```
docs/_index/
├── _manifest.yaml          # always loaded into AI prompts (~2-5K tokens)
├── _checksums.json         # never sent to AI; used for incremental detection
└── groups/
    ├── checkout.index.md   # lazy-loaded; one per suite
    ├── payments.index.md
    └── ...
```

### Why

On large projects (500+ docs), the legacy single-file index grew to ~46K
tokens — over a third of the model's 128K context window. Combined with the
analyzer's per-doc previews, the prompt routinely exceeded 200K tokens and
hit a hard `400 prompt token count exceeds the limit of 128000` error from
the model. The new layout is per-suite, so `spectra ai generate --suite X`
loads only suite X's entries.

A new pre-flight token-budget check fails fast with an actionable error
listing the available suites and their token costs, rather than letting the
model overflow.

## What you need to do

### Existing users

**Nothing.** Run `spectra docs index` once after upgrading and you'll see:

```
Migrated 541 docs across 12 suites. Largest suite: SM_GSG_Topics
(145 docs, ~10,877 tokens). Legacy index preserved as
docs/_index.md.bak — safe to delete after verification.
```

The original `docs/_index.md` is renamed to `docs/_index.md.bak`. The new
layout is written under `docs/_index/`. Subsequent runs do an incremental
update.

### Optional cleanup

After verifying the new layout works (`spectra docs list-suites`), you can
delete `docs/_index.md.bak`. Add `docs/_index/` to source control as you
would any generated artifact.

### Reviewing default exclusion patterns

Spec 040 introduces `coverage.analysis_exclude_patterns` (defaults below).
Documents matching these globs are still indexed and counted in coverage,
but their suites are flagged `skip_analysis: true` and the AI analyzer
excludes them from prompt input by default.

```json
{
  "coverage": {
    "analysis_exclude_patterns": [
      "**/Old/**",
      "**/old/**",
      "**/legacy/**",
      "**/archive/**",
      "**/release-notes/**",
      "**/CHANGELOG*",
      "**/SUMMARY.md"
    ]
  }
}
```

Override per-document via frontmatter:

```yaml
---
suite: my-custom-suite
analyze: true
---
```

Or globally by editing `coverage.analysis_exclude_patterns` in
`spectra.config.json`. Setting it to `[]` disables all default exclusions.

### New CLI flags

| Flag | Affects | Behavior |
|---|---|---|
| `--include-archived` | `spectra docs index`, `spectra ai generate`, `spectra ai analyze` | Includes skip-analysis suites in the AI input |
| `--no-migrate` | `spectra docs index` | Errors out instead of auto-migrating a legacy file |
| `--suites <ids>` | `spectra docs index` | Re-indexes only the named suites |

### New introspection commands

```bash
# List every suite with document count + token estimate + skip status
spectra docs list-suites
spectra docs list-suites --output-format json

# Print one suite's index file
spectra docs show-suite SM_GSG_Topics
```

### New configuration keys

```json
{
  "ai": {
    "analysis": {
      "max_prompt_tokens": 96000  // pre-flight budget; 0 to disable
    }
  },
  "coverage": {
    "analysis_exclude_patterns": [...],
    "max_suite_tokens": 80000     // spillover threshold
  },
  "source": {
    "doc_index_dir": "docs/_index",   // v2 layout root
    "group_overrides": {}             // per-path suite override
  }
}
```

### New exit code

| Code | Meaning |
|---|---|
| `4` | Pre-flight budget exceeded — narrow with `--suite` or raise `ai.analysis.max_prompt_tokens` |

## Troubleshooting

### "Analyzer prompt would be ~187K tokens, exceeding..."

That's the new pre-flight check. Pick a suite from
`spectra docs list-suites` and re-run with `--suite <id>`. If you genuinely
need to load the entire corpus, raise
`ai.analysis.max_prompt_tokens` in `spectra.config.json` (within the
model's actual capacity).

### "No doc-suite '<x>' in manifest"

The CLI passes `--suite` as both a test-suite name and a doc-suite filter.
If they don't match, you'll see a warning and the analyzer falls back to
loading all non-archived suites (still subject to the budget check). To
reconcile, either rename the test suite to match the doc-suite, or add
a config override:

```json
{
  "source": {
    "group_overrides": {
      "docs/some/path.md": "my-suite"
    }
  }
}
```

### Migration didn't run on a project I expected

The migrator only runs when (a) `docs/_index.md` exists AND (b)
`docs/_index/_manifest.yaml` does not. If both are present, migration is
skipped. To force a re-migration, delete `docs/_index/` and re-run.

## Related specs

- Spec 010 (the original Document Index — superseded in part)
- Spec 023 (Criteria Extraction Overhaul — same migration ergonomics)
- Spec 026 (Criteria Folder Rename — same single-version cutover pattern)
- Spec 041 (Iterative Behavior Analysis — out of scope here; consumes the
  spillover format defined in this spec)
