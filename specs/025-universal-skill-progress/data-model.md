# Data Model: Universal Progress/Result

**Date**: 2026-04-08  
**Branch**: `025-universal-skill-progress`

## New Entities

### ProgressManager

Shared service encapsulating progress/result file lifecycle.

| Field | Type | Description |
|-------|------|-------------|
| Command | string | Command identifier (e.g., "generate", "docs-index", "coverage") |
| Phases | string[] | Ordered phase names for the phase stepper |
| Title | string | Display title for progress page header |
| ResultPath | string | Path to `.spectra-result.json` (workspace root) |
| ProgressPath | string | Path to `.spectra-progress.html` (workspace root) |

**Lifecycle**: Reset → Start → Update(n) → Complete | Fail

**Methods**:
- `Reset()` — Delete stale files
- `StartAsync(title)` — Create initial progress HTML
- `UpdateAsync(phase, message, summaryData?)` — Update both files
- `CompleteAsync(result)` — Write final result, remove auto-refresh
- `FailAsync(error, partialResult?)` — Write error state

### ProgressPhases (static)

Phase sequences per command type.

| Command | Phases |
|---------|--------|
| Generate | Analyzing → Analyzed → Generating → Completed |
| Update | Classifying → Updating → Verifying → Completed |
| DocsIndex | Scanning → Indexing → Extracting Criteria → Completed |
| Coverage | Scanning Tests → Analyzing Docs → Analyzing Criteria → Analyzing Automation → Completed |
| ExtractCriteria | Scanning Docs → Extracting → Building Index → Completed |
| Dashboard | Collecting Data → Generating HTML → Completed |

## Modified Entities

### AnalyzeCoverageResult (rename field)

| Field | Old Name | New Name | Type |
|-------|----------|----------|------|
| Acceptance criteria section | `Requirements` | `AcceptanceCriteria` | CoverageSection |

JSON output changes: `"requirements": {...}` → `"acceptanceCriteria": {...}`

## File Artifacts

### .spectra-result.json

Written by all SKILL-wrapped commands. Schema varies by command but always includes base fields:

```json
{
  "command": "string",
  "status": "string (analyzing|generating|completed|failed|...)",
  "timestamp": "ISO 8601 UTC",
  "message": "string (optional)"
}
```

### .spectra-progress.html

Written by long-running commands only. Self-contained HTML with:
- Auto-refresh meta tag (2s interval, removed on completion/failure)
- Phase stepper showing progress through command phases
- Summary cards with command-specific data
- Status badge with current operation message
- Error card (on failure)
- VS Code file links for generated artifacts

## State Transitions

```
[Reset] → files deleted
   ↓
[Start] → progress HTML created (phase 1 active, auto-refresh ON)
   ↓
[Update]* → progress HTML updated (phase N active), result JSON updated
   ↓
[Complete] → final result JSON, auto-refresh OFF, last phase marked done
   OR
[Fail] → error result JSON, auto-refresh OFF, current phase marked failed
```

## Relationships

- `ProgressManager` → uses `ProgressPageWriter` (existing static class)
- `ProgressManager` → uses `JsonResultWriter` serialization options
- Each command handler → owns one `ProgressManager` instance
- `ProgressPageWriter` → reads `ProgressPhases` to render correct stepper
