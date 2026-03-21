# Data Model: Automatic Requirements Extraction

**Feature**: 015-auto-requirements-extraction
**Date**: 2026-03-21

## Existing Entities (No Changes)

### RequirementDefinition

Already defined in `Spectra.Core/Models/Coverage/RequirementDefinition.cs`. No schema changes needed.

| Field    | Type    | Required | Description                                   |
|----------|---------|----------|-----------------------------------------------|
| id       | string  | Yes      | Unique identifier (REQ-001, REQ-002, ...)     |
| title    | string  | Yes      | Short description of the testable behavior    |
| source   | string  | No       | Document path the requirement was extracted from |
| priority | string  | No       | high / medium / low (default: medium)         |

### RequirementsDocument

Already defined in `Spectra.Core/Models/Coverage/RequirementDefinition.cs`. Wraps the YAML root.

| Field        | Type                       | Required | Description                |
|--------------|----------------------------|----------|----------------------------|
| requirements | List\<RequirementDefinition\> | Yes      | All known requirements     |

### TestCaseFrontmatter (existing field)

The `requirements` field already exists in `TestCaseFrontmatter`:

| Field        | Type          | Description                                |
|--------------|---------------|--------------------------------------------|
| requirements | List\<string\> | Requirement IDs this test covers (e.g., ["REQ-001", "REQ-003"]) |

### CoverageConfig (existing field)

The `requirements_file` field already exists:

| Field             | Type   | Default                               | Description                    |
|-------------------|--------|---------------------------------------|--------------------------------|
| requirements_file | string | docs/requirements/_requirements.yaml  | Path to requirements YAML file |

## New Entities

### ExtractionResult

Returned by the extraction service after processing documents. Used internally, not persisted.

| Field          | Type                          | Description                                      |
|----------------|-------------------------------|--------------------------------------------------|
| Extracted      | List\<RequirementDefinition\> | Newly extracted requirements (before dedup)       |
| Merged         | List\<RequirementDefinition\> | New requirements after dedup (ready to write)     |
| Duplicates     | List\<DuplicateMatch\>        | Detected duplicates with match details            |
| SkippedCount   | int                           | Count of duplicates not added                     |
| TotalInFile    | int                           | Total requirements after merge                    |
| SourceDocCount | int                           | Number of documents scanned                       |

### DuplicateMatch

Details about a detected duplicate requirement.

| Field           | Type   | Description                                          |
|-----------------|--------|------------------------------------------------------|
| NewTitle        | string | Title of the newly extracted requirement              |
| ExistingId      | string | ID of the matching existing requirement               |
| ExistingTitle   | string | Title of the matching existing requirement            |
| Source          | string | Document the new requirement was extracted from       |

## YAML File Format

The requirements file (`_requirements.yaml`) format is already established. No changes to the schema:

```yaml
requirements:
  - id: REQ-001
    title: "User can log in with valid credentials"
    source: docs/authentication.md
    priority: high
  - id: REQ-002
    title: "System rejects invalid passwords after 5 attempts"
    source: docs/authentication.md
    priority: high
  - id: REQ-003
    title: "Password reset email sent within 30 seconds"
    source: docs/authentication.md
    priority: medium
```

## ID Allocation Rules

1. Read existing requirements file (if present)
2. Find the highest numeric suffix among all existing IDs (e.g., REQ-012 → 12)
3. Allocate new IDs starting from highest + 1
4. Zero-pad to 3 digits minimum (REQ-001), expanding as needed (REQ-1000)
5. Never reuse IDs from gaps (deleted REQ-005 doesn't free that ID)

## State Transitions

No stateful entities — requirements are append-only. The file grows incrementally:

```
Empty File → First Extraction → File with REQ-001..N
File with REQ-001..N → Subsequent Extraction → File with REQ-001..N + REQ-(N+1)..M
```

Existing entries are never modified or reordered by extraction. Manual edits by users are preserved.
