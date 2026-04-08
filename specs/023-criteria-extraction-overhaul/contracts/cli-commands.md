# CLI Command Contracts: Acceptance Criteria

**Feature**: 023-criteria-extraction-overhaul

## Extract Criteria

```
spectra ai analyze --extract-criteria [--force] [--dry-run] [--output-format json] [--verbosity quiet]
```

**Hidden alias**: `--extract-requirements`

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--extract-criteria` | bool | false | Trigger extraction |
| `--force` | bool | false | Re-extract all documents (ignore hashes) |
| `--dry-run` | bool | false | Preview without writing |

**Exit codes**:
- 0: All documents processed successfully
- 1: Fatal error (no documents processed)
- 2: Partial success (some documents failed, results written for successful ones)

**JSON output** (`ExtractCriteriaResult`):
```json
{
  "command": "analyze",
  "subcommand": "extract-criteria",
  "success": true,
  "documentsProcessed": 12,
  "documentsSkipped": 8,
  "documentsFailed": 0,
  "failedDocuments": [],
  "criteriaExtracted": 45,
  "criteriaNew": 12,
  "criteriaUpdated": 5,
  "criteriaUnchanged": 28,
  "orphanedCriteria": 2,
  "totalCriteria": 87,
  "indexFile": "docs/requirements/_criteria_index.yaml"
}
```

## Import Criteria

```
spectra ai analyze --import-criteria <path> [--merge|--replace] [--skip-splitting] [--dry-run] [--output-format json] [--no-interaction]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--import-criteria` | string | — | Path to YAML/CSV/JSON file |
| `--merge` | bool | true | Merge with existing (match by ID/source) |
| `--replace` | bool | false | Replace target import file only |
| `--skip-splitting` | bool | false | Skip AI splitting and normalization |

**Exit codes**:
- 0: Import successful
- 1: Error (file not found, unrecognized format, no text column in CSV)

**JSON output** (`ImportCriteriaResult`):
```json
{
  "command": "analyze",
  "subcommand": "import-criteria",
  "success": true,
  "imported": 25,
  "split": 8,
  "normalized": 18,
  "merged": 3,
  "new": 22,
  "totalCriteria": 87,
  "file": "docs/requirements/imported/jira-sprint-42.criteria.yaml",
  "sourceBreakdown": {
    "jira": 25,
    "document": 45,
    "manual": 5
  }
}
```

## List Criteria

```
spectra ai analyze --list-criteria [--source-type <type>] [--component <name>] [--priority <level>] [--output-format json]
```

| Flag | Type | Default | Description |
|------|------|---------|-------------|
| `--list-criteria` | bool | false | Trigger listing |
| `--source-type` | string? | null | Filter: jira, document, manual, ado, confluence |
| `--component` | string? | null | Filter by component/suite name |
| `--priority` | string? | null | Filter: high, medium, low |

**Exit codes**:
- 0: Success (including empty results)

**JSON output** (`ListCriteriaResult`):
```json
{
  "command": "analyze",
  "subcommand": "list-criteria",
  "success": true,
  "criteria": [
    {
      "id": "AC-CHECKOUT-001",
      "text": "System MUST validate IBAN format",
      "rfc2119": "MUST",
      "sourceType": "document",
      "sourceDoc": "docs/checkout.md",
      "component": "checkout",
      "priority": "high",
      "linkedTests": ["TC-CHECKOUT-015"],
      "covered": true
    }
  ],
  "total": 87,
  "covered": 64,
  "coveragePct": 73.6
}
```

## CSV Column Auto-Detection

Priority-ordered matching (case-insensitive, first match wins):

| SPECTRA Field | Accepted Column Names |
|---------------|----------------------|
| text | `text`, `summary`, `title`, `acceptance_criteria`, `acceptance criteria`, `description` |
| source | `source`, `key`, `id`, `work_item_id`, `work item id` |
| source_type | `source_type`, `source type`, `type` |
| component | `component`, `area_path`, `area path` |
| priority | `priority` |
| tags | `tags`, `labels` |

**Required**: At least one column matching `text` field. All others optional.
