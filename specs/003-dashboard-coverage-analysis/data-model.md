# Data Model: Dashboard and Coverage Analysis

**Feature**: 003-dashboard-coverage-analysis
**Date**: 2026-03-15

## Entities

### Dashboard Domain

#### DashboardData

The root data structure containing all information needed to render the dashboard.

| Field | Type | Description |
|-------|------|-------------|
| generated_at | DateTime | When the dashboard was generated |
| repository | string | Repository name/path |
| suites | SuiteStats[] | Statistics for each test suite |
| runs | RunSummary[] | Execution run history |
| coverage | CoverageData | Coverage visualization data |
| tests | TestEntry[] | All test entries (denormalized for client-side filtering) |

#### SuiteStats

Aggregated statistics for a test suite.

| Field | Type | Description |
|-------|------|-------------|
| name | string | Suite name (folder name) |
| test_count | int | Total number of tests |
| by_priority | Dictionary<string, int> | Count by priority (high/medium/low) |
| by_component | Dictionary<string, int> | Count by component |
| tags | string[] | All unique tags in suite |
| last_run | RunSummary? | Most recent execution run |
| automation_coverage | decimal | Percentage of tests with automation |

#### TestEntry

Denormalized test information for dashboard display.

| Field | Type | Description |
|-------|------|-------------|
| id | string | Test ID (e.g., "TC-101") |
| suite | string | Suite name |
| title | string | Test title |
| file | string | Source file path |
| priority | string | high/medium/low |
| tags | string[] | Test tags |
| component | string? | Component under test |
| source_refs | string[] | Referenced documentation |
| automated_by | string? | Automation file path |
| has_automation | bool | Whether any automation link exists |

#### RunSummary

Summary of an execution run for history display.

| Field | Type | Description |
|-------|------|-------------|
| run_id | string | Unique run identifier |
| suite | string | Suite that was executed |
| status | string | Completed/Cancelled/Abandoned |
| started_at | DateTime | When run started |
| completed_at | DateTime? | When run ended |
| started_by | string | User who executed |
| duration_seconds | int? | Total duration |
| total | int | Total tests in run |
| passed | int | Tests that passed |
| failed | int | Tests that failed |
| skipped | int | Tests that were skipped |
| blocked | int | Tests that were blocked |

#### RunDetail

Detailed results for a specific run (drill-down view).

| Field | Type | Description |
|-------|------|-------------|
| run_id | string | Unique run identifier |
| suite | string | Suite name |
| environment | string? | Execution environment |
| filters | RunFilters? | Filters applied to run |
| results | TestResultEntry[] | Individual test results |

### Coverage Domain

#### CoverageData

Root structure for coverage visualization and analysis.

| Field | Type | Description |
|-------|------|-------------|
| documents | CoverageNode[] | Document nodes (root level) |
| tests | CoverageNode[] | Test nodes |
| automation | CoverageNode[] | Automation nodes |
| links | CoverageLink[] | Relationships between nodes |

#### CoverageNode

A node in the coverage visualization graph.

| Field | Type | Description |
|-------|------|-------------|
| id | string | Unique node identifier |
| type | NodeType | document/test/automation |
| name | string | Display name |
| path | string | File path |
| status | CoverageStatus | covered/partial/uncovered |
| children | string[]? | Child node IDs (for hierarchy) |

#### NodeType (enum)

```
Document    # Documentation file
Test        # Manual test case
Automation  # Automated test file
```

#### CoverageStatus (enum)

```
Covered     # Has both test and automation (green)
Partial     # Has test but no automation (yellow)
Uncovered   # No test coverage (red)
```

#### CoverageLink

A relationship between coverage nodes.

| Field | Type | Description |
|-------|------|-------------|
| source | string | Source node ID |
| target | string | Target node ID |
| type | LinkType | Relationship type |
| status | LinkStatus | Link health status |

#### LinkType (enum)

```
DocumentToTest      # source_refs relationship
TestToAutomation    # automated_by relationship
AutomationToTest    # Attribute reference back to test
```

#### LinkStatus (enum)

```
Valid       # Both endpoints exist and are consistent
Broken      # Target doesn't exist
Mismatch    # Link only exists in one direction
Orphaned    # Source has no corresponding target
```

### Coverage Report Domain

#### CoverageReport

Output of coverage analysis command.

| Field | Type | Description |
|-------|------|-------------|
| generated_at | DateTime | When analysis was run |
| summary | CoverageSummary | Aggregate statistics |
| by_suite | SuiteCoverage[] | Coverage per suite |
| by_component | ComponentCoverage[] | Coverage per component |
| unlinked_tests | UnlinkedTest[] | Tests without automation |
| orphaned_automation | OrphanedAutomation[] | Automation without tests |
| broken_links | BrokenLink[] | Invalid references |
| mismatches | LinkMismatch[] | Inconsistent bidirectional links |

#### CoverageSummary

Aggregate coverage statistics.

| Field | Type | Description |
|-------|------|-------------|
| total_tests | int | Total manual tests |
| automated | int | Tests with automation |
| manual_only | int | Tests without automation |
| coverage_percentage | decimal | (automated / total) * 100 |
| broken_links | int | Count of broken links |
| orphaned_automation | int | Count of orphaned files |
| mismatches | int | Count of mismatched links |

#### SuiteCoverage

Coverage statistics for a single suite.

| Field | Type | Description |
|-------|------|-------------|
| suite | string | Suite name |
| total | int | Total tests |
| automated | int | Automated tests |
| coverage_percentage | decimal | Coverage % |

#### ComponentCoverage

Coverage statistics for a component.

| Field | Type | Description |
|-------|------|-------------|
| component | string | Component name |
| total | int | Total tests |
| automated | int | Automated tests |
| coverage_percentage | decimal | Coverage % |

#### UnlinkedTest

A manual test without automation.

| Field | Type | Description |
|-------|------|-------------|
| test_id | string | Test ID |
| suite | string | Suite name |
| title | string | Test title |
| priority | string | Test priority |

#### OrphanedAutomation

An automation file referencing non-existent tests.

| Field | Type | Description |
|-------|------|-------------|
| file | string | Automation file path |
| referenced_ids | string[] | Test IDs referenced |
| line_numbers | int[] | Lines where references found |

#### BrokenLink

A reference to a non-existent file.

| Field | Type | Description |
|-------|------|-------------|
| test_id | string | Test with broken link |
| automated_by | string | Referenced path |
| reason | string | "File not found" |

#### LinkMismatch

Inconsistent bidirectional link.

| Field | Type | Description |
|-------|------|-------------|
| test_id | string | Test ID |
| test_automated_by | string? | automated_by value in test |
| automation_file | string? | File referencing test |
| issue | string | Description of mismatch |

## State Transitions

### CoverageStatus Flow

```
Document Created → Uncovered (red)
  ↓ (test added with source_ref)
Document → Partial (yellow)
  ↓ (test gets automated_by)
Document → Covered (green)
```

### LinkStatus Flow

```
Link Created → Valid
  ↓ (target file deleted)
Link → Broken
  ↓ (target restored)
Link → Valid
```

## Validation Rules

1. **Test ID Format**: Must match `^TC-\d{3,}$` pattern
2. **Node IDs**: Must be unique within type
3. **Link References**: source and target must exist in nodes
4. **Coverage Calculation**: Only counts tests with valid indexes
5. **Automation Pattern**: Must be valid regex with `{id}` placeholder

## Relationships

```
Document 1 ←──────── N source_refs ──────── N Test
Test 1 ←──────── 0..1 automated_by ────── 0..1 Automation
Automation 1 ←──────── N attributes ────── N Test (reverse lookup)
Suite 1 ←──────── N contains ──────────── N Test
Run 1 ←──────────── N has ─────────────── N TestResult
```
