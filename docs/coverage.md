# Coverage Analysis

Three-dimensional coverage analysis: Documentation, Requirements, and Automation.

Related: [CLI Reference](cli-reference.md) | [Configuration](configuration.md) | [Test Format](test-format.md)

---

## Overview

SPECTRA produces a unified coverage report with three sections:

| Type | What it measures | Data source |
|------|-----------------|-------------|
| **Documentation** | Which docs have linked tests | `source_refs` field in test frontmatter matched against `docs/` |
| **Requirements** | Which requirements are covered | `requirements` field in test frontmatter + `_requirements.yaml` |
| **Automation** | Which tests have automation code | `automated_by` field in test frontmatter + code scanning |

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
    "details": [
      { "doc": "docs/auth.md", "test_count": 28, "covered": true, "test_ids": ["TC-001", "..."] },
      { "doc": "docs/admin.md", "test_count": 0, "covered": false, "test_ids": [] }
    ]
  },
  "requirements_coverage": {
    "total_requirements": 5,
    "covered_requirements": 3,
    "percentage": 60.00,
    "has_requirements_file": true,
    "details": [
      { "id": "REQ-042", "title": "Payment rejection", "tests": ["TC-134"], "covered": true },
      { "id": "REQ-043", "title": "Expired card", "tests": [], "covered": false }
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

Measures which documentation files have at least one test referencing them via `source_refs`.

For each doc in `docs/`, SPECTRA checks if any test file has it in its `source_refs` frontmatter field.

## Requirements Coverage

Measures which formal requirements are covered by tests.

### With `_requirements.yaml`

When a requirements file exists, SPECTRA cross-references the defined requirements with `requirements` fields in test frontmatter. This reveals which requirements have no tests.

### Without `_requirements.yaml`

When no requirements file exists, SPECTRA discovers requirements from test frontmatter only and reports them as a flat list. `has_requirements_file` is `false`.

### Requirements File Format

Create `docs/requirements/_requirements.yaml`:

```yaml
requirements:
  - id: REQ-001
    title: "User can log in with valid credentials"
    source: docs/authentication.md
    priority: high
  - id: REQ-002
    title: "System rejects invalid passwords"
    source: docs/authentication.md
    priority: high
  - id: REQ-003
    title: "Admin panel access control"
    source: docs/admin.md
    priority: high
```

The path to this file is configured via `coverage.requirements_file` in [spectra.config.json](configuration.md).

## Automation Coverage

Measures which tests have linked automation code (via `automated_by` field or code scanning).

Reports include:
- **By suite**: Per-suite automation percentages
- **Unlinked tests**: Tests with no automation reference
- **Orphaned automation**: Automation files referencing non-existent tests
- **Broken links**: `automated_by` paths pointing to missing files

## Auto-Link

The `--auto-link` flag scans your automation code for test ID references and writes `automated_by` back into test YAML frontmatter:

```bash
spectra ai analyze --coverage --auto-link
```

**How it works:**

1. Scans files matching `file_extensions` in `automation_dirs`
2. Matches test IDs using `scan_patterns` templates (e.g., `[TestCase("TC-001")]`)
3. For each match, updates the test file's `automated_by` frontmatter field

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

## Coverage Configuration

Full coverage settings in `spectra.config.json`:

```json
{
  "coverage": {
    "automation_dirs": ["tests", "test", "spec", "e2e"],
    "scan_patterns": ["[TestCase(\"{id}\")]", "@pytest.mark.manual_test(\"{id}\")"],
    "file_extensions": [".cs", ".java", ".py", ".ts"],
    "requirements_file": "docs/requirements/_requirements.yaml"
  }
}
```

See [Configuration Reference](configuration.md) for all coverage options.

## Dashboard Visualizations

The [dashboard](cli-reference.md#spectra-dashboard) Coverage tab provides four visualizations:

### Donut Chart

A test health distribution chart at the top of the Coverage tab:
- **Green** — Automated tests (have `automated_by`)
- **Yellow** — Manual-only tests (have `source_refs` but no `automated_by`)
- **Red** — Unlinked tests (neither `source_refs` nor `automated_by`)
- Center label shows total test count; hover segments for tooltips

### Progress Bars with Drill-Down

Three stacked cards — one per coverage type — with:
- Percentage and fill bar (green >= 80%, yellow >= 50%, red < 50%)
- "Show details" toggle that expands a per-item breakdown list
- Documentation: each doc file with test count and covered/uncovered icon
- Requirements: each requirement ID, title, linked test IDs
- Automation: per-suite breakdown (suite name, automated/total, percentage)

### Empty State Guidance

When coverage data is missing or unconfigured, cards show actionable messages:
- **Requirements**: "No requirements tracked yet" with setup instructions
- **Automation**: "No automation links detected" with `--auto-link` instructions
- **Documentation**: "All documents have test coverage!" success message when at 100%

### Treemap

A block visualization below the progress bars showing suites sized by test count and colored by automation coverage:
- **Green** — >= 50% automated
- **Yellow** — > 0% but < 50% automated
- **Red** — 0% automated
- Hover for suite details; click to navigate to suite test list
