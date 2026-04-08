# Data Model: Criteria Folder Rename, Index Exclusion & Coverage Fix

No new entities or data model changes. This is a bugfix spec that modifies paths and logic only.

## Affected Data Paths

| Entity | Old Path | New Path |
|--------|----------|----------|
| Criteria Directory | `docs/requirements/` | `docs/criteria/` |
| Master Index | `docs/requirements/_criteria_index.yaml` | `docs/criteria/_criteria_index.yaml` |
| Per-document files | `docs/requirements/*.criteria.yaml` | `docs/criteria/*.criteria.yaml` |
| Imported criteria | `docs/requirements/imported/` | `docs/criteria/imported/` |
| Legacy requirements | `docs/requirements/_requirements.yaml` | `docs/criteria/_requirements.yaml.bak` (existing migration) |

## Config Schema (unchanged structure, new defaults)

```yaml
coverage:
  criteria_dir: "docs/criteria"           # was "docs/requirements"
  criteria_file: "docs/criteria/_criteria_index.yaml"  # was "docs/requirements/_criteria_index.yaml"
  requirements_file: "docs/requirements/_requirements.yaml"  # legacy, unchanged
```

## Coverage Data Flow (fixed)

```
Per-document .criteria.yaml files
    → AcceptanceCriteriaCoverageAnalyzer.AnalyzeFromDirectoryAsync()
    → Enumerate *.criteria.yaml (exclude _criteria_index.yaml, _index.criteria.yaml)
    → Parse each file → flat list of AcceptanceCriterion
    → Match criterion IDs against test frontmatter criteria: field
    → AcceptanceCriteriaCoverage { Total, Covered, Uncovered, Details[] }
    → DataCollector → DashboardData.Coverage.AcceptanceCriteria
    → JSON serialization → app.js renders section
```
