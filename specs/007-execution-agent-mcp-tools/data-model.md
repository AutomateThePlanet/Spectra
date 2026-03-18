# Data Model: Bundled Execution Agent & MCP Data Tools

**Branch**: `007-execution-agent-mcp-tools` | **Date**: 2026-03-19 | **Status**: Draft

## Entities

### 1. ValidationError

Represents a single validation error found in a test file.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Code | string | Yes | Error code (e.g., "MISSING_ID", "INVALID_PRIORITY") |
| Message | string | Yes | Human-readable error description |
| FilePath | string | Yes | Relative path to the test file |
| LineNumber | int? | No | Line number where error occurred (if determinable) |
| FieldName | string? | No | Field name that caused the error |
| TestId | string? | No | Test ID if parseable |

### 2. ValidationResult

Aggregated result of validating one or more test files.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| IsValid | bool | Yes | True if no errors found |
| TotalFiles | int | Yes | Number of files validated |
| ValidFiles | int | Yes | Number of files that passed validation |
| Errors | List\<ValidationError\> | Yes | All errors found |
| Warnings | List\<ValidationWarning\> | Yes | All warnings found |

### 3. ValidationWarning

Represents a non-blocking issue found in a test file.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Code | string | Yes | Warning code (e.g., "NO_STEPS", "LONG_TITLE") |
| Message | string | Yes | Human-readable warning description |
| FilePath | string | Yes | Relative path to the test file |
| TestId | string? | No | Test ID if parseable |

### 4. IndexRebuildResult

Result of rebuilding index files.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| SuitesProcessed | int | Yes | Number of suites processed |
| TestsIndexed | int | Yes | Total tests added to indexes |
| FilesAdded | int | Yes | New test files discovered |
| FilesRemoved | int | Yes | Orphaned entries removed |
| IndexPaths | List\<string\> | Yes | Paths to updated index files |

### 5. CoverageGap

Represents an uncovered documentation area.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| DocumentPath | string | Yes | Relative path to documentation file |
| DocumentTitle | string | Yes | Title extracted from document (H1) |
| Severity | GapSeverity | Yes | Priority based on document size/complexity |
| SizeKb | int | Yes | Document size in kilobytes |
| HeadingCount | int | Yes | Number of headings in document |

### 6. GapSeverity (enum)

Priority level for coverage gaps.

| Value | Criteria |
|-------|----------|
| High | Document > 10KB or > 5 headings |
| Medium | Document > 5KB or > 2 headings |
| Low | Default for smaller documents |

### 7. CoverageAnalysisResult

Result of coverage gap analysis.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| DocsScanned | int | Yes | Total documentation files scanned |
| DocsCovered | int | Yes | Documents with at least one test reference |
| Gaps | List\<CoverageGap\> | Yes | Uncovered documentation areas |
| CoveragePercent | int | Yes | Percentage of docs with test coverage |

### 8. AgentPrompt

Metadata for bundled agent prompt files.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | Yes | Agent identifier (e.g., "spectra-execution") |
| Description | string | Yes | One-line description for tool discovery |
| Content | string | Yes | Full markdown prompt content |
| TargetPath | string | Yes | Installation path relative to repo root |

## Relationships

```
ValidationResult
    └── Errors → ValidationError[]
    └── Warnings → ValidationWarning[]

IndexRebuildResult
    └── IndexPaths → string[] (paths to _index.json files)

CoverageAnalysisResult
    └── Gaps → CoverageGap[]

TestCase (existing)
    └── SourceRefs → string[] (paths to docs for coverage mapping)
```

## Validation Rules

### ValidationError
- Code must be SCREAMING_SNAKE_CASE
- FilePath must be relative to tests/ directory
- LineNumber must be > 0 if present

### CoverageGap
- DocumentPath must be relative to docs/ directory
- DocumentTitle extracted from first H1 or filename if no H1
- Severity calculated from SizeKb and HeadingCount

### IndexRebuildResult
- TestsIndexed must equal sum of all index entry counts
- FilesAdded + FilesRemoved indicates delta from previous state

## MCP Tool Response Formats

All tools use `McpToolResponse<T>` wrapper:

```json
{
  "data": { /* tool-specific result */ },
  "error": null
}
```

On error:
```json
{
  "data": null,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable message"
  }
}
```
