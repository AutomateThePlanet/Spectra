# Data Model: Conversational Test Generation

**Branch**: `006-conversational-generation` | **Date**: 2026-03-18

## Entities

### 1. InteractiveSession

Tracks the state of an interactive generation or update flow.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | string | Yes | Unique session identifier (GUID) |
| Mode | SessionMode | Yes | Generate or Update |
| Suite | string? | No | Selected suite name (null until selected) |
| Focus | string? | No | User-provided focus description |
| TestType | TestTypeSelection? | No | Type of tests to generate |
| State | SessionState | Yes | Current state in the flow |
| SelectedGaps | List\<string\> | No | Gap IDs selected for generation |
| GeneratedTests | List\<TestCase\> | No | Tests generated in this session |
| StartedAt | DateTimeOffset | Yes | Session start time |

**State Transitions**:
```
[Start] → SuiteSelection → TestTypeSelection → FocusInput →
         GapAnalysis → Generating → Results → [GapSelection | End]
```

### 2. SessionMode

Enum for session type.

| Value | Description |
|-------|-------------|
| Generate | Creating new tests |
| Update | Updating existing tests |

### 3. SessionState

Enum for interactive flow states.

| Value | Description |
|-------|-------------|
| SuiteSelection | Showing suite picker |
| TestTypeSelection | Showing test type options |
| FocusInput | Accepting focus description |
| GapAnalysis | Analyzing coverage gaps |
| Generating | AI generating tests |
| Results | Showing generation results |
| GapSelection | Offering to generate for remaining gaps |
| Complete | Session finished |

### 4. TestTypeSelection

Enum for test generation type (interactive mode).

| Value | Description |
|-------|-------------|
| FullCoverage | Happy path + negative + boundary |
| NegativeOnly | Error and failure scenarios only |
| SpecificArea | User will describe the area |
| FreeDescription | Open-ended description |

### 5. CoverageGap

Represents an uncovered area in documentation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | string | Yes | Gap identifier (hash of path + section) |
| DocumentPath | string | Yes | Path to documentation file |
| DocumentTitle | string | Yes | Title of the document |
| Section | string? | No | Specific section if applicable |
| Severity | GapSeverity | Yes | High/Medium/Low priority |
| Description | string | Yes | What is not covered |

### 6. GapSeverity

Enum for coverage gap priority.

| Value | Criteria |
|-------|----------|
| High | Document > 10KB or > 5 headings |
| Medium | Document > 5KB or > 2 headings |
| Low | Default |

### 7. TestClassification

Enum for test state during updates.

| Value | Description | Action |
|-------|-------------|--------|
| UpToDate | Source refs exist and content matches | None |
| Outdated | Source refs exist but docs changed | Update in place |
| Orphaned | Source refs point to deleted docs | Mark in frontmatter |
| Redundant | > 90% similarity to another test | Flag in index |

### 8. ClassifiedTest

Test with its classification for update operations.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Test | TestCase | Yes | The test case |
| Classification | TestClassification | Yes | Classification result |
| Reason | string? | No | Why classified this way |
| RelatedTest | string? | No | For redundant: ID of similar test |
| UpdatedContent | TestCase? | No | For outdated: new content |

### 9. GenerationResult (existing, extended)

Result of AI test generation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Tests | List\<TestCase\> | Yes | Generated test cases |
| SkippedDuplicates | List\<string\> | No | Test titles skipped as duplicates |
| Errors | List\<string\> | No | Generation errors |
| TokenUsage | TokenUsage | No | AI token consumption |
| CoverageGapsRemaining | List\<CoverageGap\> | No | Gaps still uncovered |

### 10. UpdateResult

Result of test update operation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| UpToDate | int | Yes | Count of unchanged tests |
| Updated | int | Yes | Count of updated tests |
| Orphaned | int | Yes | Count of orphaned tests |
| Redundant | int | Yes | Count of redundant tests |
| ModifiedFiles | List\<string\> | Yes | Paths to modified files |
| OrphanedTests | List\<ClassifiedTest\> | Yes | Details of orphaned tests |
| RedundantTests | List\<ClassifiedTest\> | Yes | Details of redundant tests |

### 11. SuiteSummary

Summary information for suite selection display.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | Yes | Suite name |
| Path | string | Yes | Suite directory path |
| TestCount | int | Yes | Number of tests in suite |
| LastUpdated | DateTimeOffset? | No | Most recent test modification |
| CoveragePercent | int? | No | Estimated doc coverage |

## Relationships

```
InteractiveSession
    └── Suite (selected) → SuiteSummary
    └── Focus (entered) → string
    └── CoverageGaps (analyzed) → CoverageGap[]
    └── GeneratedTests (created) → TestCase[]

TestCase
    └── SourceRefs → Documentation files
    └── DependsOn → TestCase (optional)

ClassifiedTest
    └── Test → TestCase
    └── RelatedTest → TestCase (for redundant)
    └── UpdatedContent → TestCase (for outdated)

CoverageGap
    └── DocumentPath → Documentation file
```

## Validation Rules

### InteractiveSession
- Id must be non-empty GUID
- State transitions must follow valid paths
- Suite must be set before GapAnalysis state
- Focus must be set before Generating state (if TestType is SpecificArea)

### CoverageGap
- DocumentPath must exist in docs/
- Severity must match calculation criteria
- Id must be deterministic (same inputs = same ID)

### TestClassification
- Orphaned requires non-existent source_refs
- Redundant requires RelatedTest to be set
- Outdated requires UpdatedContent to be set

### GenerationResult
- Tests must have valid IDs (TC-XXX format)
- No duplicate IDs within result
- CoverageGapsRemaining must exclude gaps covered by Tests

## State Transitions

### InteractiveSession State Machine

```
┌─────────────────────────────────────────────────────────────────┐
│                    InteractiveSession                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐                                            │
│  │ SuiteSelection  │ ◄── Start (no --suite arg)                │
│  └────────┬────────┘                                            │
│           │ user selects suite                                  │
│           ▼                                                      │
│  ┌─────────────────┐                                            │
│  │TestTypeSelection│                                            │
│  └────────┬────────┘                                            │
│           │ user selects type                                   │
│           ▼                                                      │
│  ┌─────────────────┐                                            │
│  │   FocusInput    │ (if SpecificArea or FreeDescription)      │
│  └────────┬────────┘                                            │
│           │ user enters focus                                   │
│           ▼                                                      │
│  ┌─────────────────┐                                            │
│  │   GapAnalysis   │                                            │
│  └────────┬────────┘                                            │
│           │ gaps identified                                     │
│           ▼                                                      │
│  ┌─────────────────┐                                            │
│  │   Generating    │ ◄── Direct mode enters here               │
│  └────────┬────────┘                                            │
│           │ tests written to disk                               │
│           ▼                                                      │
│  ┌─────────────────┐                                            │
│  │    Results      │                                            │
│  └────────┬────────┘                                            │
│           │                                                      │
│     ┌─────┴─────┐                                               │
│     ▼           ▼                                               │
│ (gaps remain) (no gaps)                                         │
│     │           │                                               │
│     ▼           ▼                                               │
│  ┌─────────┐  ┌──────────┐                                      │
│  │GapSelect│  │ Complete │                                      │
│  └────┬────┘  └──────────┘                                      │
│       │                                                          │
│       │ user selects gaps                                       │
│       └──────────► Generating (loop)                            │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### TestClassification Assignment

```
For each test in suite:
  1. Check source_refs exist
     └── None exist → ORPHANED (reason: "No source references")
     └── Some missing → ORPHANED (reason: "Missing: {paths}")

  2. Compare test content against source docs
     └── Docs unchanged → UP_TO_DATE
     └── Docs changed → OUTDATED

  3. Check similarity against other tests
     └── > 90% match found → REDUNDANT (relatedTest: matched ID)
```
