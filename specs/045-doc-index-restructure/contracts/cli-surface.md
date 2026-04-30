# Contract — CLI surface for branch 045

## `spectra docs index`

### Existing flags (unchanged behavior)

| Flag | Type | Default | Notes |
|---|---|---|---|
| `--force` | bool | false | Full rebuild rather than incremental. |
| `--skip-criteria` | bool | false | Skip the auto-extract-criteria step. |
| `--dry-run` | bool | false | Report would-be changes without writing. |
| `--output-format` | enum | `human` | `human` \| `json`. |
| `--no-interaction` | bool | false | Suppress prompts. |
| `--verbosity` | enum | `normal` | `quiet` \| `normal` \| `detailed`. |

### New flags

| Flag | Type | Default | Notes |
|---|---|---|---|
| `--no-migrate` | bool | false | If a legacy `_index.md` is present, error instead of migrating. |
| `--include-archived` | bool | false | Include suites flagged `skip_analysis: true` in any auto-extracted criteria step. |
| `--suites <ids>` | string | (none) | Comma-separated list of suite IDs to re-index. Other suites' files are left untouched. Implies incremental. Phase 1+. |

### Exit codes

- `0` — success.
- `1` — error (config missing, parse failure, migration error, unknown).
- `2` — pre-flight budget violation (only when this command performs an analyzer call; reserved for future).

### JSON result

See `docs-index-result.json` for the full schema. New top-level fields:

- `manifest` — repo-relative path to `_manifest.yaml`.
- `suites[]` — per-suite breakdown (id, count, tokens, skip flag, file path).
- `migration` — present only when migration ran on this invocation.
- `skippedDocuments` — count of documents in skip-analysis suites.

### Phases (status values)

`scanning` → `grouping` → `writing-suites` → `writing-manifest` → `extracting-criteria` → `completed`.

Migration emits a `migrating` status before `scanning` if it runs.

## `spectra ai generate`

### New flags

| Flag | Type | Default | Notes |
|---|---|---|---|
| `--include-archived` | bool | false | Pass `--include-archived` through to the analyzer (loads skip-analysis suites). |

### Behavior changes

- `--suite <id>` now prefers a doc-suite match in the manifest. If `<id>` is unknown, emit a warning listing available suites and fall back to no-filter behavior.
- Pre-flight token check runs before any AI call. On budget violation, command exits with code `2` and a message of the form:

  ```
  Analyzer prompt would be ~187,234 tokens, exceeding the configured 96,000-token budget.
  Candidate suites (sorted by token cost):
    SM_GSG_Topics       10,877 tokens
    RD_Topics            9,911 tokens
    POS_UG_Topics        7,400 tokens
    ... 9 more ...
  Suggested:
    spectra ai generate --suite SM_GSG_Topics
    spectra ai generate --analyze-only
  Or raise ai.analysis.max_prompt_tokens in spectra.config.json.
  ```

### Unchanged

All existing flags (`--focus`, `--analyze-only`, `--count`, `--auto-complete`, `--from-suggestions`, `--from-description`, `--context`, `--no-interaction`, `--dry-run`, `--skip-critic`) keep current semantics.

## `spectra ai analyze`

### New flags

| Flag | Type | Default | Notes |
|---|---|---|---|
| `--include-archived` | bool | false | Honored by `--extract-criteria` and `--coverage` subcommands. |

### Behavior changes

- `--extract-criteria` skips documents in skip-analysis suites by default (User Story 3 acceptance scenario 1).
- `--coverage` continues to consider all documents (skip-analysis affects analyzer input, not coverage metrics).

## `spectra docs list-suites` (Phase 4)

```
spectra docs list-suites [--output-format json]
```

Lists every suite in the manifest with id, document count, token estimate, skip flag, and source path.

Human output is a table; JSON output is an array of `SuiteResultEntry` objects matching the result schema.

## `spectra docs show-suite <id>` (Phase 4)

```
spectra docs show-suite <suite-id>
```

Prints the contents of `<doc_index_dir>/groups/<id>.index.md` to stdout. Errors with exit code 1 if the suite is unknown.

## Configuration changes

### `spectra.config.json`

```json
{
  "source": {
    "doc_index_dir": "docs/_index",
    "group_overrides": {}
  },
  "coverage": {
    "analysis_exclude_patterns": [
      "**/Old/**",
      "**/old/**",
      "**/legacy/**",
      "**/archive/**",
      "**/release-notes/**",
      "**/CHANGELOG*",
      "**/SUMMARY.md"
    ],
    "max_suite_tokens": 80000
  },
  "ai": {
    "analysis": {
      "max_prompt_tokens": 96000
    }
  }
}
```

- `source.doc_index` (legacy single-file path) — kept as a hidden alias. Setting it logs a deprecation warning. Removed in a future major release.
- `coverage.analysis_exclude_patterns` — replace-not-merge with defaults. Setting `[]` disables all defaults.
- `ai.analysis.max_prompt_tokens` — pre-flight budget. Lowering it makes the budget check fire earlier.

## Frontmatter additions

Per-document YAML frontmatter recognized fields:

```yaml
---
suite: my-custom-suite
analyze: true
---
```

- `suite` — overrides the directory-based default. Validated per FR-008 (R-004).
- `analyze` — when `true`, forces inclusion in analyzer input even if the suite is `skip_analysis: true`. When `false`, excludes the document even if its suite is normally analyzed.

Both fields are optional; absent means default behavior.
