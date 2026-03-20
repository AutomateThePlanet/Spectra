# Data Model: Coverage Dashboard Visualizations

**Feature**: 009-coverage-dashboard-viz | **Date**: 2026-03-20

## Entities

### CoverageSummaryData (existing — modify)

Root container embedded in dashboard JSON payload.

| Field | Type | Description |
|-------|------|-------------|
| documentation | DocumentationSectionData | Documentation coverage section |
| requirements | RequirementsSectionData | Requirements coverage section |
| automation | AutomationSectionData | Automation coverage section |

### DocumentationSectionData (new — replaces generic CoverageSectionData for docs)

| Field | Type | Description |
|-------|------|-------------|
| covered | int | Number of documents with at least one linked test |
| total | int | Total documents in docs/ |
| percentage | decimal | Coverage percentage |
| details | DocumentationCoverageDetail[] | Per-document breakdown |

### DocumentationCoverageDetail (new)

| Field | Type | Description |
|-------|------|-------------|
| doc | string | Document file path (e.g., "docs/auth.md") |
| test_count | int | Number of tests referencing this doc |
| covered | bool | Whether any test references this doc |
| test_ids | string[] | IDs of tests referencing this doc |

### RequirementsSectionData (new — replaces generic CoverageSectionData for reqs)

| Field | Type | Description |
|-------|------|-------------|
| covered | int | Number of requirements with at least one linked test |
| total | int | Total requirements (from file or discovered) |
| percentage | decimal | Coverage percentage |
| has_requirements_file | bool | Whether _requirements.yaml was found |
| details | RequirementCoverageDetail[] | Per-requirement breakdown |

### RequirementCoverageDetail (new)

| Field | Type | Description |
|-------|------|-------------|
| id | string | Requirement ID (e.g., "REQ-042") |
| title | string | Requirement title (from YAML or empty) |
| tests | string[] | Test IDs covering this requirement |
| covered | bool | Whether any test covers this requirement |

### AutomationSectionData (new — replaces generic CoverageSectionData for automation)

| Field | Type | Description |
|-------|------|-------------|
| covered | int | Number of tests with automation links |
| total | int | Total manual tests |
| percentage | decimal | Automation coverage percentage |
| details | AutomationSuiteDetail[] | Per-suite breakdown |
| unlinked_tests | UnlinkedTestDetail[] | Tests without automated_by |

### AutomationSuiteDetail (new)

| Field | Type | Description |
|-------|------|-------------|
| suite | string | Suite name (directory name) |
| total | int | Total tests in suite |
| automated | int | Tests with automated_by in suite |
| percentage | decimal | Suite automation percentage |

### UnlinkedTestDetail (new)

| Field | Type | Description |
|-------|------|-------------|
| test_id | string | Test ID |
| suite | string | Suite name |
| title | string | Test title |
| priority | string | Test priority |

## Relationships

```
CoverageSummaryData
├── DocumentationSectionData
│   └── DocumentationCoverageDetail[] (one per doc file)
├── RequirementsSectionData
│   └── RequirementCoverageDetail[] (one per requirement)
└── AutomationSectionData
    ├── AutomationSuiteDetail[] (one per suite)
    └── UnlinkedTestDetail[] (tests without automation)
```

## Dashboard Donut Chart Data (derived, not persisted)

Computed client-side from `data.tests` array:

| Category | Criteria | Color |
|----------|----------|-------|
| Automated | Test has `automated_by` (non-empty) | Green |
| Manual-only | Test has `source_refs` but no `automated_by` | Yellow |
| Unlinked | Test has neither `source_refs` nor `automated_by` | Red |

## Dashboard Treemap Data (derived, not persisted)

Computed client-side from `data.suites` + `data.coverage_summary.automation.details`:

| Field | Source | Purpose |
|-------|--------|---------|
| Suite name | `data.suites[].name` | Block label |
| Test count | `data.suites[].test_count` | Block size |
| Automation % | `automation.details[].percentage` | Block color |

## Validation Rules

- `percentage` is always `Math.Round((covered / total) * 100, 2)` when total > 0, else 0
- `details` arrays are sorted: docs by path, requirements by ID, suites by name
- `test_ids` and `tests` arrays are sorted numerically by TC number
- `has_requirements_file` is false when file is missing or malformed
