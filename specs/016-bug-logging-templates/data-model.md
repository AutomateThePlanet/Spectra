# Data Model: 016 Bug Logging, Templates, and Execution Agent Integration

**Date**: 2026-03-22
**Branch**: `016-bug-logging-templates`

## New Entities

### BugTrackingConfig

Configuration section added to `SpectraConfig`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `provider` | `string` | `"auto"` | Bug tracker: `"auto"`, `"azure-devops"`, `"jira"`, `"github"`, `"local"` |
| `template` | `string?` | `"templates/bug-report.md"` | Path to bug report template. `null` to disable |
| `default_severity` | `string` | `"medium"` | Default: `"critical"`, `"major"`, `"medium"`, `"minor"` |
| `auto_attach_screenshots` | `bool` | `true` | Auto-include execution screenshots |
| `auto_prompt_on_failure` | `bool` | `true` | Offer bug logging on every failure |

**Validation rules**:
- `provider` must be one of the allowed enum values
- `default_severity` must be one of the allowed enum values
- `template` path, if non-null, is relative to repo root

### BugReportContext

Runtime data object assembled when composing a bug report. Not persisted — used to populate template variables.

| Field | Type | Source |
|-------|------|--------|
| `TestId` | `string` | Test case frontmatter `id` |
| `TestTitle` | `string` | First heading of test case Markdown |
| `SuiteName` | `string` | Suite folder name from run |
| `Environment` | `string` | Execution run environment parameter |
| `Severity` | `string` | Derived from `priority` or config default |
| `RunId` | `string` | Current execution run UUID |
| `FailedSteps` | `string` | Steps up to and including the failing step, numbered |
| `ExpectedResult` | `string` | Expected Results section from test case |
| `Attachments` | `List<string>` | Screenshot paths from execution |
| `SourceRefs` | `List<string>` | From frontmatter `source_refs` |
| `Requirements` | `List<string>` | From frontmatter `requirements` |
| `Component` | `string?` | From frontmatter `component` |
| `ExistingBugs` | `List<string>` | From frontmatter `bugs` (for dedup) |

### Template Variable Map

Maps `BugReportContext` fields to template `{{variable}}` placeholders:

| Variable | Context Field | Formatting |
|----------|--------------|------------|
| `{{title}}` | Auto-generated | `"Bug: {TestTitle} - {FailedSteps summary}"` |
| `{{test_id}}` | `TestId` | As-is |
| `{{test_title}}` | `TestTitle` | As-is |
| `{{suite_name}}` | `SuiteName` | As-is |
| `{{environment}}` | `Environment` | As-is |
| `{{severity}}` | `Severity` | As-is |
| `{{run_id}}` | `RunId` | As-is |
| `{{failed_steps}}` | `FailedSteps` | Numbered list |
| `{{expected_result}}` | `ExpectedResult` | As-is |
| `{{attachments}}` | `Attachments` | Markdown image/link list |
| `{{source_refs}}` | `SourceRefs` | Comma-separated |
| `{{requirements}}` | `Requirements` | Comma-separated |
| `{{component}}` | `Component` | As-is or "N/A" |

## Modified Entities

### TestCaseFrontmatter (modified)

**New field**:

| Field | Type | YAML Key | Default |
|-------|------|----------|---------|
| `Bugs` | `List<string>` | `bugs` | `[]` |

Follows `automated_by` / `requirements` pattern. Contains bug IDs or URLs written back after bug creation.

### TestCase (modified)

**New field**:

| Field | Type | Default |
|-------|------|---------|
| `Bugs` | `IReadOnlyList<string>` | `[]` |

### TestIndexEntry (modified)

**New field**:

| Field | Type | JSON Key | Default |
|-------|------|----------|---------|
| `Bugs` | `IReadOnlyList<string>` | `bugs` | `[]` |

Uses `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` to omit when empty.

## Relationships

```
SpectraConfig
  └── BugTrackingConfig (1:1, new section)

TestCaseFrontmatter
  └── bugs: List<string> (0..N bug IDs/URLs)

ExecutionRun (existing)
  └── reports/{run_id}/bugs/ (0..N local bug files)
       └── attachments/ (0..N screenshot copies)
```

## State Transitions

Bug logging has no persistent state machine — it's a single-pass flow within the agent:

```
Test FAILED → Offer bug logging → Gather context → Check duplicates
  → [Duplicate found] → Show existing, offer link or new
  → [No duplicate] → Populate template → Show preview → Confirm
    → [Confirm] → Submit to tracker → Record bug ID in notes + frontmatter
    → [Edit] → User edits → Re-confirm
    → [Cancel] → Continue execution
```

## Severity Mapping

| Test Priority | Bug Severity |
|---------------|-------------|
| `high` | `critical` |
| `medium` | `major` |
| `low` | `minor` |
| (not set) | `default_severity` from config |
