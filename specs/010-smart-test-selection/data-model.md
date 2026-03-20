# Data Model: Smart Test Selection

## New Entities

### TestSearchResult

Returned by `find_test_cases` for each matched test.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| id | string | TestIndexEntry.Id | Test case identifier |
| suite | string | MetadataIndex.Suite | Suite the test belongs to |
| title | string | TestIndexEntry.Title | Test title |
| description | string? | TestIndexEntry.Description | Optional one-liner (new field) |
| priority | string | TestIndexEntry.Priority | "high", "medium", "low" |
| tags | string[] | TestIndexEntry.Tags | Tag list |
| component | string? | TestIndexEntry.Component | Component name |
| estimated_duration | string? | TestIndexEntry.EstimatedDuration | Human-readable (new field) |
| has_automation | bool | Computed | True if AutomatedBy is non-empty |

### TestSearchResponse

Top-level response from `find_test_cases`.

| Field | Type | Notes |
|-------|------|-------|
| matched | int | Total matching tests (before max_results truncation) |
| total_estimated_duration | string | Sum of matched tests' durations |
| tests | TestSearchResult[] | Up to max_results entries, ordered per spec |
| warnings | string[]? | Skipped suites, missing indexes |

### TestExecutionHistoryEntry

Returned per test by `get_test_execution_history`.

| Field | Type | Source | Notes |
|-------|------|--------|-------|
| last_executed | DateTime? | test_results.completed_at | Most recent completion |
| last_status | string? | test_results.status | Most recent terminal status |
| total_runs | int | COUNT(*) | Total execution attempts |
| pass_rate | decimal? | Computed | PASSED / total * 100, null if 0 runs |
| last_run_id | string? | test_results.run_id | Run containing last execution |

### SavedSelectionConfig

Config model for saved selections in spectra.config.json.

| Field | Type | JSON Key | Notes |
|-------|------|----------|-------|
| description | string? | description | Human-readable purpose |
| tags | string[]? | tags | Tag filter (OR within) |
| priorities | string[]? | priorities | Priority filter (OR within) |
| components | string[]? | components | Component filter (OR within) |
| has_automation | bool? | has_automation | Automation status filter |

### SavedSelectionInfo

Returned by `list_saved_selections` per selection.

| Field | Type | Notes |
|-------|------|-------|
| name | string | Selection key from config |
| description | string? | From config |
| filters | object | The filter criteria |
| estimated_test_count | int | Tests matching filters now |
| estimated_duration | string | Sum of matching tests' durations |

## Modified Entities

### TestIndexEntry (existing)

New fields:

| Field | Type | JSON Key | Ignore Condition |
|-------|------|----------|-----------------|
| description | string? | description | WhenWritingNull |
| estimated_duration | string? | estimated_duration | WhenWritingNull |

### TestCaseFrontmatter (existing)

New fields:

| Field | Type | YAML Alias |
|-------|------|------------|
| Description | string? | description |
| EstimatedDuration already exists | — | estimated_duration |

### TestCase (existing)

New field:

| Field | Type | Notes |
|-------|------|-------|
| Description | string? | Parsed from frontmatter |

Note: `EstimatedDuration` already exists as `TimeSpan?` on TestCase.

### SpectraConfig (existing)

New property:

| Field | Type | JSON Key |
|-------|------|----------|
| Selections | IReadOnlyDictionary<string, SavedSelectionConfig> | selections |

## Relationships

```
SpectraConfig
  └── Selections: Dictionary<string, SavedSelectionConfig>

MetadataIndex
  └── Tests: TestIndexEntry[]  (+ description, estimated_duration)

find_test_cases
  ├── reads: MetadataIndex[] (all suites)
  ├── applies: SelectionFilters
  └── returns: TestSearchResponse

get_test_execution_history
  ├── reads: test_results table (SQLite)
  └── returns: Dictionary<string, TestExecutionHistoryEntry>

list_saved_selections
  ├── reads: SpectraConfig.Selections
  ├── evaluates: find_test_cases logic per selection
  └── returns: SavedSelectionInfo[]

start_execution_run
  ├── mode 1 (suite): existing behavior
  ├── mode 2 (test_ids): resolves across all suite indexes
  └── mode 3 (selection): loads config → applies filters → resolves IDs
```
