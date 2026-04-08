# Data Model: Acceptance Criteria Import & Extraction Overhaul

**Feature**: 023-criteria-extraction-overhaul  
**Date**: 2026-04-07

## Entities

### AcceptanceCriterion

Replaces `RequirementDefinition`. A single testable statement extracted or imported.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Id | string | yes | — | Unique ID (e.g., `AC-CHECKOUT-001`, `AC-PROJ-1234-1`) |
| Text | string | yes | — | The criterion statement, normalized to RFC 2119 |
| Rfc2119 | string? | no | null | Primary RFC 2119 keyword: MUST, SHOULD, MAY, etc. |
| Source | string? | no | null | External reference (Jira key, ADO ID, doc path) |
| SourceType | string | no | "document" | One of: document, jira, ado, confluence, manual |
| SourceDoc | string? | no | null | Source document path (for extracted criteria) |
| SourceSection | string? | no | null | Section heading in source document |
| Component | string? | no | null | Maps to test suite name |
| Priority | string | no | "medium" | high, medium, low |
| Tags | List\<string\> | no | [] | Categorization tags |
| LinkedTestIds | List\<string\> | no | [] | Test IDs linked via coverage (populated by auto-link) |

**Identity**: `Id` is unique within a criteria file. Globally unique across all criteria files by convention (prefix ensures uniqueness).

**Serialization**: YAML with underscore naming convention (matching existing YamlDotNet config).

### CriteriaSource

An entry in the master index representing one criteria file.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| File | string | yes | — | Relative path to `.criteria.yaml` file |
| SourceDoc | string? | no | null | Source document path (for extracted) |
| SourceType | string | yes | — | document, jira, ado, confluence, manual |
| DocHash | string? | no | null | SHA-256 hash of source document (for extracted) |
| CriteriaCount | int | yes | — | Number of criteria in the file |
| LastExtracted | DateTime? | no | null | When criteria were last extracted |
| ImportedAt | DateTime? | no | null | When criteria were imported |

### CriteriaIndex

The master index file (`_criteria_index.yaml`).

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Version | int | yes | 1 | Schema version |
| TotalCriteria | int | yes | — | Sum of all criteria across all sources |
| Sources | List\<CriteriaSource\> | yes | [] | List of criteria file entries |

### AcceptanceCriteriaCoverage

Replaces `RequirementsCoverage`. Coverage analysis result.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| TotalCriteria | int | yes | — | Total criteria count |
| CoveredCriteria | int | yes | — | Criteria with linked tests |
| Percentage | double | yes | — | Coverage percentage |
| HasCriteriaFile | bool | yes | — | Whether criteria index exists |
| Details | List\<CriteriaCoverageDetail\> | yes | — | Per-criterion coverage |
| SourceBreakdown | Dictionary\<string, SourceCoverageStats\> | no | — | Per-source-type breakdown |

### SourceCoverageStats

Per-source-type coverage breakdown for dashboard.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| SourceType | string | yes | — | document, jira, ado, etc. |
| Total | int | yes | — | Total criteria from this source |
| Covered | int | yes | — | Covered criteria from this source |
| Percentage | double | yes | — | Coverage percentage |

## Relationships

```
CriteriaIndex (1) ──contains──> (N) CriteriaSource
CriteriaSource (1) ──points-to──> (1) CriteriaFile
CriteriaFile (1) ──contains──> (N) AcceptanceCriterion
AcceptanceCriterion (N) ──linked-to──> (N) TestCase (via criteria/requirements frontmatter)
AcceptanceCriterion (1) ──extracted-from──> (0..1) Document (via SourceDoc)
```

## State Transitions

### Criterion Lifecycle

```
                ┌─────────┐
                │  (none) │
                └────┬────┘
                     │ extract / import
                     ▼
              ┌──────────────┐
              │    Active     │
              └──────┬───────┘
                     │
          ┌──────────┼──────────┐
          │          │          │
          ▼          ▼          ▼
    ┌──────────┐ ┌────────┐ ┌──────────┐
    │  Updated  │ │Orphaned│ │ Replaced │
    │(re-extract│ │(source │ │(--replace│
    │ /merge)   │ │deleted)│ │  import) │
    └──────────┘ └────────┘ └──────────┘
```

- **Active**: Normal state after extraction or import
- **Updated**: Criterion text changed during re-extraction or merge import
- **Orphaned**: Source document deleted; criteria file preserved with warning
- **Replaced**: Target file overwritten by `--replace` import

## File Format Specifications

### Per-document criteria file (`{name}.criteria.yaml`)

```yaml
# Extracted from: docs/checkout.md
# Doc hash: sha256:abc123...
# Extracted at: 2026-04-07T14:30:00Z
criteria:
  - id: AC-CHECKOUT-001
    text: "System MUST validate IBAN format before submitting payment"
    rfc2119: MUST
    source_doc: docs/checkout.md
    source_section: "Payment Validation"
    component: checkout
    priority: high
    tags: [payment, validation]
```

### Master index (`_criteria_index.yaml`)

```yaml
version: 1
total_criteria: 40
sources:
  - file: checkout.criteria.yaml
    source_doc: docs/checkout.md
    source_type: document
    doc_hash: "sha256:abc123..."
    criteria_count: 15
    last_extracted: "2026-04-07T14:30:00Z"
  - file: imported/jira-sprint-42.criteria.yaml
    source_type: jira
    criteria_count: 25
    imported_at: "2026-04-06T10:00:00Z"
```

## Config Schema Changes

```json
{
  "coverage": {
    "criteria_file": "docs/requirements/_criteria_index.yaml",
    "criteria_dir": "docs/requirements",
    "criteria_import": {
      "default_source_type": "manual",
      "auto_split": true,
      "normalize_rfc2119": true,
      "id_prefix": "AC"
    }
  }
}
```

Old `requirements_file` key is ignored (no migration). New keys used exclusively.

## Test Frontmatter Addition

```yaml
---
id: TC-CHECKOUT-015
title: "Verify IBAN validation rejects invalid checksum"
criteria: [AC-CHECKOUT-001, AC-PROJ-1234-2]   # NEW field
requirements: [REQ-001]                         # EXISTING, deprecated but still read
priority: high
---
```

Coverage analysis reads both `criteria` and `requirements` fields. New tests only populate `criteria`.
