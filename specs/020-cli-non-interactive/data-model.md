# Data Model: CLI Non-Interactive Mode and Structured Output

## Enums

### OutputFormat
Controls how command results are rendered to stdout.

| Value | Description |
|-------|-------------|
| Human | Default. Spectre.Console formatted output with colors, spinners, tables |
| Json | Structured JSON on stdout, no ANSI, no spinners, no progress |

### ExitCodes (extended)

| Code | Name | Description |
|------|------|-------------|
| 0 | Success | Command completed successfully |
| 1 | Error | Command failed (runtime error, missing config) |
| 2 | ValidationError | Validation errors found (validate command) |
| 3 | MissingArguments | Required arguments missing in non-interactive mode |
| 130 | Cancelled | Operation cancelled by user |

## Result Models

### CommandResult (base)
Common fields for all command JSON output.

| Field | Type | Description |
|-------|------|-------------|
| command | string | Command name (e.g., "generate", "validate") |
| status | string | Outcome: "completed", "errors_found", "failed" |
| timestamp | string (ISO 8601) | UTC timestamp of completion |

### GenerateResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| suite | string | Target suite name |
| analysis | object? | Behavior analysis breakdown (total, covered, recommended, by category) |
| generation | object | Tests generated, written, rejected, grounding verdicts |
| suggestions | array | Suggested additional tests (title + category) |
| files_created | array | Paths of created test files |

### AnalyzeCoverageResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| documentation | object | percentage, covered count, total count |
| requirements | object | percentage, covered count, total count |
| automation | object | percentage, linked count, total count |
| undocumented_tests | int | Tests not linked to any doc |
| uncovered_areas | array | Items with no coverage (doc or requirement + reason) |

### ValidateResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| total_files | int | Total files validated |
| valid | int | Files passing validation |
| errors | array | Error items (file, line, message) |

### DashboardResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| output_path | string | Dashboard output directory |
| pages_generated | int | Number of HTML pages |
| suites_included | int | Number of suites in dashboard |
| tests_included | int | Total tests across suites |
| runs_included | int | Execution runs included |

### ListResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| suites | array | Suite items (name, test_count, last_modified) |

### ShowResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| test | object | Full test details (id, title, priority, suite, component, tags, source_refs, steps, expected_results) |

### InitResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| created | array | List of created file/directory paths |

### DocsIndexResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| documents_indexed | int | Number of documents processed |
| documents_updated | int | Documents that changed since last index |
| index_path | string | Path to the generated index file |

### ErrorResult : CommandResult
| Field | Type | Description |
|-------|------|-------------|
| error | string | Error message |
| missing_arguments | array? | List of missing argument names (for exit code 3) |

## Relationships

- Every command handler builds its specific `*Result` record
- `JsonResultWriter` serializes any result to stdout when `OutputFormat.Json`
- `ProgressReporter` and presenters check `OutputFormat` and become no-ops for `Json`
- Error paths build `ErrorResult` with appropriate details
