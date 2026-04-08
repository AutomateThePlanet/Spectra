# Data Model: 024-docs-skill-coverage-fix

## Modified Entities

### DocsIndexResult (extend existing)

**File**: `src/Spectra.CLI/Results/DocsIndexResult.cs`

Current fields:
- `DocumentsIndexed` (int)
- `DocumentsUpdated` (int)
- `IndexPath` (string)

New fields:
- `DocumentsSkipped` (int) — documents unchanged (hash match)
- `DocumentsNew` (int) — documents added to index for the first time
- `DocumentsTotal` (int) — total documents found
- `CriteriaExtracted` (int?) — number of acceptance criteria extracted (null if skipped)
- `CriteriaFile` (string?) — path to `_criteria_index.yaml` (null if skipped)

Inherited from `CommandResult`:
- `Command` (string) = "docs-index"
- `Status` (string) = "completed" | "failed"
- `Timestamp` (DateTimeOffset)
- `Message` (string?)

### DocsIndexResult JSON Schema

```json
{
  "command": "docs-index",
  "status": "completed",
  "message": "Documentation index updated",
  "timestamp": "2026-04-08T14:30:00Z",
  "documentsIndexed": 12,
  "documentsUpdated": 4,
  "documentsSkipped": 3,
  "documentsNew": 2,
  "documentsTotal": 15,
  "indexPath": "docs/_index.md",
  "criteriaExtracted": 45,
  "criteriaFile": "docs/requirements/_criteria_index.yaml"
}
```

### In-Progress Result JSON (for `.spectra-result.json` during execution)

```json
{
  "command": "docs-index",
  "status": "indexing",
  "message": "Indexing checkout.md (3/15)...",
  "timestamp": "2026-04-08T14:30:00Z",
  "documentsIndexed": 2,
  "documentsTotal": 15,
  "indexPath": "docs/_index.md"
}
```

Status lifecycle: `scanning` → `indexing` → `extracting-criteria` → `completed` | `failed`

## Unchanged Entities (verified correct)

### CoverageSummaryData

Already uses correct field names post-spec-023:
- `DocumentationSectionData` (serialized as `documentation`)
- `AcceptanceCriteriaSectionData` (serialized as `acceptance_criteria`)
- `AutomationSectionData` (serialized as `automation`)

No changes needed to the model. Fix is in `DataCollector` to ensure non-null defaults.

### AcceptanceCriteriaSectionData

Fields already correct:
- `Covered` (int)
- `Total` (int)
- `Percentage` (decimal)
- `HasCriteriaFile` (bool)
- `Details` (list of `CriteriaCoverageDetail`)

## File Rename Behavior

### `_requirements.yaml` → `_requirements.yaml.bak`

**Trigger**: Any criteria reader access when `_criteria_index.yaml` does not exist but `_requirements.yaml` does.

**Actions**:
1. `File.Move("_requirements.yaml", "_requirements.yaml.bak")`
2. Log: "Renamed _requirements.yaml → _requirements.yaml.bak (legacy format)"
3. Return empty criteria set (next extraction will create fresh `_criteria_index.yaml`)

**No new models needed** for this behavior.
