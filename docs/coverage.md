---
title: Coverage Analysis
parent: User Guide
nav_order: 6
---

# Coverage Analysis

Three-dimensional coverage analysis: Documentation, Acceptance Criteria, and Automation.

Related: [CLI Reference](cli-reference.md) | [Configuration](configuration.md) | [Test Format](test-format.md)

---

## Overview

SPECTRA produces a unified coverage report with three sections:

| Type | What it measures | Data source |
|------|-----------------|-------------|
| **Documentation** | Which docs have linked test cases | `source_refs` field in test case frontmatter matched against `docs/` |
| **Acceptance Criteria** | Which criteria are covered | `criteria` field in test case frontmatter + `_criteria_index.yaml` |
| **Automation** | Which test cases have automation code | `automated_by` field in test case frontmatter + code scanning |

> **Spec 037 — boundary coverage from ISTQB techniques**: Test generation now
> applies six ISTQB test design techniques systematically (EP, BVA, DT, ST, EG,
> UC). Suites generated after spec 037 typically have 50%+ more test cases in the
> `boundary` and `negative` categories than pre-037 suites on the same docs.
> The analysis output exposes this via a `technique_breakdown` map alongside
> the existing category `breakdown`.
>
> **Spec 038 — algorithmic precision (optional)**: When the optional
> [Testimize integration](testimize-integration.md) is enabled, the AI replaces
> approximated boundary values with mathematically optimal ones from
> Testimize's BVA / EP / pairwise / ABC algorithms. Disabled by default.

## Run Coverage Analysis

```bash
# Console output (three sections)
spectra ai analyze --coverage

# JSON output (three top-level keys)
spectra ai analyze --coverage --format json --output coverage.json

# Markdown output
spectra ai analyze --coverage --format markdown --output coverage.md

# Detailed output
spectra ai analyze --coverage --verbosity detailed
```

### JSON Output Structure

```json
{
  "generated_at": "2026-03-20T10:00:00Z",
  "documentation_coverage": {
    "total_docs": 4,
    "covered_docs": 3,
    "percentage": 75.00,
    "excluded_docs": 1,
    "details": [
      { "doc": "docs/auth.md", "test_count": 28, "covered": true, "test_ids": ["TC-001", "..."] },
      { "doc": "docs/admin.md", "test_count": 0, "covered": false, "test_ids": [] },
      { "doc": "docs/release-notes/v1.md", "test_count": 0, "covered": false, "test_ids": [],
        "excluded": true, "excluded_by_pattern": "docs/release-notes/**" }
    ]
  },
  "acceptance_criteria_coverage": {
    "total": 5,
    "covered": 3,
    "percentage": 60.00,
    "has_criteria_file": true,
    "details": [
      { "id": "AC-042", "title": "Payment rejection", "tests": ["TC-134"], "covered": true },
      { "id": "AC-043", "title": "Expired card", "tests": [], "covered": false }
    ]
  },
  "automation_coverage": {
    "total_tests": 40,
    "automated": 12,
    "percentage": 30.00,
    "by_suite": [ ... ],
    "unlinked_tests": [ ... ],
    "orphaned_automation": [ ... ],
    "broken_links": [ ... ]
  }
}
```

## Documentation Coverage

Measures which documentation files have at least one test case referencing them via `source_refs`.

For each doc in `docs/`, SPECTRA checks if any test case file has it in its `source_refs` frontmatter field.

### Excluding docs from the coverage percentage (Spec 060)

Some documents — release notes, changelogs, summaries, archived material — should stay indexed and available to generation and analysis but should **not** drag down the documentation-coverage number, because nobody writes test cases against them. Configure `coverage.coverage_exclude_patterns` to drop matched docs from the coverage **denominator only**:

```json
{
  "coverage": {
    "coverage_exclude_patterns": ["docs/release-notes/**", "**/SUMMARY.md"]
  }
}
```

With this configured, a matched doc:

- is **removed from the coverage denominator** (`total_docs` counts only in-scope docs, and `percentage` is computed over them);
- is **counted in `excluded_docs`** and reported in `details` with `"excluded": true` and the matching `"excluded_by_pattern"` — it is **never silently dropped**;
- **remains fully present** in the document map for generation, analysis, and indexing.

The `excluded_docs`, `excluded`, and `excluded_by_pattern` fields appear only when at least one doc is excluded; with the default (empty) configuration, coverage output is identical to before Spec 060. In the markdown report the excluded docs show an `Excluded` status (distinct from `Yes`/`No`); in the compact/terminal report they show a `~` mark (distinct from covered `+` and uncovered `-`).

> This is one of **three independent exclusion mechanisms**. It is distinct from `source.exclude_patterns` (which removes a doc from *everything*) and `coverage.analysis_exclude_patterns` (which only skips AI analysis and **still counts** the doc in coverage). See [the three exclusion mechanisms](configuration.md#the-three-exclusion-mechanisms) in the configuration reference for the full comparison.

## Acceptance Criteria Coverage

Measures which acceptance criteria are covered by test cases.

### With `_criteria_index.yaml`

When a criteria index file exists, SPECTRA cross-references the defined criteria with `criteria` fields in test case frontmatter. This reveals which criteria have no test cases.

### Without `_criteria_index.yaml`

When no criteria file exists, SPECTRA discovers criteria from test case frontmatter only and reports them as a flat list. `has_criteria_file` is `false`.

### From-description tests and the two coverage axes (Spec 050)

Acceptance-criteria coverage and grounded coverage are tallied independently:

- **Acceptance-criteria coverage** counts a test when its `criteria` frontmatter field is populated.
- **Grounded coverage** counts a test when its `grounding.verdict` is `grounded` (or `partial`) — i.e. an independent critic verified it.

Tests created with `spectra ai generate --from-description "..."` have their `criteria` field populated (Spec 050 injects the matching criteria as the mandatory mapping instruction), so they **count toward acceptance-criteria coverage**. But from-description runs no critic, so their `grounding.verdict` stays `manual` and they are **excluded from grounded statistics**. This is intended, not an inconsistency: populating `criteria` records what the test claims to cover; it does not assert that an independent critic verified the test against the source documentation. See the [from-description note](skills-integration.md) for the rationale.

### Criteria File Format

Create `docs/criteria/_criteria_index.yaml` (or use `spectra ai analyze --extract-criteria` to auto-generate):

```yaml
criteria:
  - id: AC-001
    title: "User can log in with valid credentials"
    source: docs/authentication.md
    priority: high
  - id: AC-002
    title: "System rejects invalid passwords"
    source: docs/authentication.md
    priority: high
  - id: AC-003
    title: "Admin panel access control"
    source: docs/admin.md
    priority: high
```

The path to this file is configured via `coverage.criteria_file` in [spectra.config.json](configuration.md).

### Resilience (Spec 047)

`ai analyze --extract-criteria` runs per-document with bounded retries on inconclusive AI responses:

- A genuine empty result (the AI returns a valid empty list, or the source document is empty) is cached normally — the next run skips the document.
- An empty AI response or unparseable response triggers a single retry (1.5 s backoff). If still inconclusive, the document is reported under `failed_documents` and re-attempted on the next run — no cache hash is written, so the next non-`--force` run will not skip it.
- A thrown exception or per-document timeout is logged to `.spectra-errors.log` and the document is reported as failed; no retry is performed.
- `--force` bypasses the cache and re-extracts every document, regardless of any prior cache entry.

### Coverage guards (Spec 048)

Each persisted entry in `_criteria_index.yaml` carries an `outcome` field that distinguishes a genuine extraction outcome from inconclusive states. Today the only value written is `extracted` — Spec 047's cache-write gate guarantees only genuine extraction results are persisted. The field exists so future guards can distinguish affirmed-empty entries (`criteria_count: 0` with `outcome: extracted`, meaning the AI legitimately found nothing) from inconclusive ones without re-deriving the distinction heuristically. **An entry with `criteria_count: 0` and `outcome: extracted` is an affirmed empty — not a failure.** Legacy entries written before Spec 048 lack the field and deserialize as `extracted` by default; no migration step is required.

Two non-blocking guards surface coverage-linkage gaps:

- **`spectra docs index` zero-criteria warning** — when the run indexed at least one document but extracted zero acceptance criteria across the whole corpus, the command emits a prominent warning naming the recovery command (`spectra ai analyze --extract-criteria`) and writes a matching `criteria_warning` string into the JSON result. The command still exits success; the warning is suppressed when `--skip-criteria` is passed.
- **`spectra ai generate` no-match note** — when a generation run targets a suite for which no acceptance criteria match by component, source-doc, or file-name, the run completes normally but attaches a non-blocking note to the result's `notes` array (e.g. `"No acceptance criteria matched suite 'checkout' …"`). The note is present in the JSON regardless of console verbosity; only the human-facing console echo is suppressed under `--verbosity quiet`. No interactive prompts are introduced anywhere.

## Automation Coverage

Measures which test cases have linked automation code (via `automated_by` field or code scanning).

Reports include:
- **By suite**: Per-suite automation percentages
- **Unlinked test cases**: Test cases with no automation reference
- **Orphaned automation**: Automation files referencing non-existent test cases
- **Broken links**: `automated_by` paths pointing to missing files

## Auto-Link

The `--auto-link` flag scans your automation code for test ID references and writes `automated_by` back into test case YAML frontmatter:

```bash
spectra ai analyze --coverage --auto-link
```

**How it works:**

1. Scans files matching `file_extensions` in `automation_dirs`
2. Matches test IDs using `scan_patterns` templates (e.g., `[TestCase("TC-001")]`)
3. For each match, updates the test case file's `automated_by` frontmatter field

### Scan Patterns

Scan patterns are templates where `{id}` is replaced with the test ID regex. Examples:

```json
{
  "coverage": {
    "scan_patterns": [
      "[TestCase(\"{id}\")]",
      "[ManualTestCase(\"{id}\")]",
      "@pytest.mark.manual_test(\"{id}\")",
      "groups = {\"{id}\"}"
    ],
    "file_extensions": [".cs", ".java", ".py", ".ts"]
  }
}
```

If `scan_patterns` is empty, SPECTRA falls back to the legacy `attribute_patterns` regex list.

## Coverage-Aware Generation

When running `spectra ai generate`, the analysis step is coverage-aware for existing suites. Before identifying testable behaviors, the analyzer builds a coverage snapshot from:

- **`_index.json`**: Existing test titles, criteria links, and source refs
- **`.criteria.yaml` files**: All acceptance criteria, cross-referenced against tests
- **`docs/_index/_manifest.yaml` + `groups/{suite}.index.md`** (Spec 040 v2 layout): Documentation sections, cross-referenced against test source refs. The coverage path reads ALL suites regardless of `skip_analysis` flag — coverage considers every indexed document.

The AI receives this coverage context and only recommends tests for genuine gaps — uncovered criteria and undocumented sections. For a mature suite with 231 tests covering 38/41 criteria, the analysis recommends ~8 new tests (the actual gap) instead of 139.

For suites with more than 500 tests, the analyzer switches to summary mode to conserve prompt tokens: only coverage statistics and uncovered items are sent, not the full title list.

New suites with no `_index.json` or criteria files work exactly as before — the coverage context is simply omitted.

## Coverage Configuration

Full coverage settings in `spectra.config.json`:

```json
{
  "coverage": {
    "automation_dirs": ["tests", "test", "spec", "e2e"],
    "scan_patterns": ["[TestCase(\"{id}\")]", "@pytest.mark.manual_test(\"{id}\")"],
    "file_extensions": [".cs", ".java", ".py", ".ts"],
    "criteria_file": "docs/criteria/_criteria_index.yaml"
  }
}
```

See [Configuration Reference](configuration.md) for all coverage options.

## Dashboard Visualizations

The [dashboard](cli-reference.md#spectra-dashboard) Coverage tab provides four visualizations:

### Donut Chart

A test case health distribution chart at the top of the Coverage tab:
- **Green** — Automated test cases (have `automated_by`)
- **Yellow** — Manual-only test cases (have `source_refs` but no `automated_by`)
- **Red** — Unlinked test cases (neither `source_refs` nor `automated_by`)
- Center label shows total test case count; hover segments for tooltips

### Progress Bars with Drill-Down

Three stacked cards — one per coverage type — with:
- Percentage and fill bar (green >= 80%, yellow >= 50%, red < 50%)
- "Show details" toggle that expands a per-item breakdown list
- Documentation: each doc file with test case count and covered/uncovered icon
- Acceptance Criteria: each criterion ID, title, linked test case IDs
- Automation: per-suite breakdown (suite name, automated/total, percentage)

### Empty State Guidance

When coverage data is missing or unconfigured, cards show actionable messages:
- **Acceptance Criteria**: "No acceptance criteria tracked yet" with setup instructions
- **Automation**: "No automation links detected" with `--auto-link` instructions
- **Documentation**: "All documents have test case coverage!" success message when at 100%

### Treemap

A block visualization below the progress bars showing suites sized by test case count and colored by automation coverage:
- **Green** — >= 50% automated
- **Yellow** — > 0% but < 50% automated
- **Red** — 0% automated
- Hover for suite details; click to navigate to suite test case list
