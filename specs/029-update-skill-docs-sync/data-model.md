# Data Model: SPECTRA Update SKILL + Documentation Sync

**Date**: 2026-04-10 | **Branch**: `029-update-skill-docs-sync`

## Entities

### UpdateResult (extended)

Existing model at `src/Spectra.CLI/Results/UpdateResult.cs`. Extends `CommandResult`.

**Existing fields** (no change):
| Field | Type | JSON Key | Description |
|-------|------|----------|-------------|
| Command | string | command | "update" |
| Status | string | status | "completed" / "failed" |
| Timestamp | string | timestamp | ISO 8601 |
| Message | string? | message | Optional status message |
| Suite | string? | suite | Target suite name |
| TestsUpdated | int | testsUpdated | Count of rewritten tests |
| TestsRemoved | int | testsRemoved | Count of removed tests |
| TestsUnchanged | int | testsUnchanged | Count of unchanged tests |
| Classification | UpdateClassificationCounts? | classification | Breakdown by classification type |
| FilesModified | IReadOnlyList<string>? | filesModified | List of modified file paths |
| FilesDeleted | IReadOnlyList<string>? | filesDeleted | List of deleted file paths |

**New fields** (to add):
| Field | Type | JSON Key | Description |
|-------|------|----------|-------------|
| Success | bool | success | Whether command completed without errors |
| TotalTests | int | totalTests | Total tests analyzed (sum of classifications) |
| TestsFlagged | int | testsFlagged | Count of tests flagged for review (orphaned + redundant) |
| FlaggedTests | IReadOnlyList<FlaggedTestEntry>? | flaggedTests | Details of flagged tests |
| Duration | string? | duration | Formatted duration "HH:mm:ss" |

### FlaggedTestEntry (new)

New model class for detailed flagged test information.

| Field | Type | JSON Key | Description |
|-------|------|----------|-------------|
| Id | string | id | Test case ID (e.g., "TC-107") |
| Title | string | title | Test case title |
| Classification | string | classification | "ORPHANED" or "REDUNDANT" |
| Reason | string | reason | Human-readable explanation |

### UpdateClassificationCounts (no change)

Already complete with `UpToDate`, `Outdated`, `Orphaned`, `Redundant` fields.

## SKILL Content Entity

The SKILL file is a Markdown document with YAML frontmatter, stored as an embedded resource.

**Frontmatter schema** (standard across all SKILLs):
| Field | Value |
|-------|-------|
| name | spectra-update |
| description | Update existing test cases after documentation or acceptance criteria changes. |
| tools | [{{READONLY_TOOLS}}] |
| model | GPT-4o |
| disable-model-invocation | true |

## State Transitions

No new state transitions. The SKILL delegates to the existing `UpdateHandler` which manages its own state machine via `ProgressManager`:

```
classifying → updating → verifying → completed
```

These phases are already defined in `ProgressPhases.Update`.
