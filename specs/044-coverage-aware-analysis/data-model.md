# Data Model: Coverage-Aware Behavior Analysis

**Date**: 2026-04-13 | **Feature**: 044-coverage-aware-analysis

## New Entities

### CoverageSnapshot

Aggregated view of what's already tested in a suite. Built from three independent data sources.

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| ExistingTestCount | int | `_index.json` | Total test cases in the suite |
| ExistingTestTitles | string[] | `_index.json` | Test titles for dedup (truncated to 80 chars) |
| CoveredCriteriaIds | HashSet\<string\> | `_index.json` tests → criteria fields | Criteria IDs with at least one linked test |
| UncoveredCriteria | UncoveredCriterion[] | `_criteria_index.yaml` - CoveredCriteriaIds | Criteria with zero linked tests |
| CoveredSourceRefs | HashSet\<string\> | `_index.json` tests → source_refs fields | Doc sections with at least one linked test |
| UncoveredSourceRefs | string[] | `docs/_index.md` - CoveredSourceRefs | Doc sections with no linked tests |
| TotalCriteriaCount | int | `_criteria_index.yaml` | Total criteria across all sources |
| Mode | Full \| Summary | Computed | Full if tests <= 500, Summary otherwise |

**Lifecycle**: Created per analysis run. Not persisted — derived from committed files.

**Validation**:
- Empty/missing data sources produce zero values (not errors)
- CoveredCriteriaIds uses case-insensitive comparison
- CoveredSourceRefs uses case-insensitive comparison

### UncoveredCriterion

A single acceptance criterion that has no linked tests.

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| Id | string | `.criteria.yaml` | Criterion identifier (e.g., "AC-001") |
| Text | string | `.criteria.yaml` | Full criterion text |
| Source | string? | `.criteria.yaml` | Source document path |
| Priority | string | `.criteria.yaml` | Priority level (high/medium/low) |

## Modified Entities

### GenerateAnalysis (in GenerateResult.cs)

New fields added alongside existing ones:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| ExistingTestCount | int | 0 | Total tests in suite (from snapshot) |
| TotalCriteria | int | 0 | Total acceptance criteria count |
| CoveredCriteria | int | 0 | Criteria with linked tests |
| UncoveredCriteria | int | 0 | Criteria without linked tests |
| UncoveredCriteriaIds | string[] | [] | IDs of uncovered criteria |

**Backward compatibility**: All new fields default to 0 or empty. Old JSON consumers ignore unknown fields. Old JSON without new fields deserializes with defaults.

### BehaviorAnalysisResult (unchanged schema)

No structural changes. The `AlreadyCovered` field now uses the snapshot count (when available) instead of title-similarity heuristic. `RecommendedCount` derives from `TotalBehaviors - AlreadyCovered` as before.

## Data Flow

```
_index.json ──────┐
                   │
.criteria.yaml ────┤──→ CoverageSnapshotBuilder.BuildAsync() ──→ CoverageSnapshot
                   │
docs/_index.md ────┘
                                    │
                                    ▼
                   CoverageContextFormatter.Format(snapshot) ──→ string (markdown block)
                                    │
                                    ▼
                   PlaceholderResolver.Resolve(template, {coverage_context: block})
                                    │
                                    ▼
                   BehaviorAnalyzer.AnalyzeAsync() ──→ BehaviorAnalysisResult
                                    │
                                    ▼
                   GenerateAnalysis (enriched with snapshot stats)
```

## Relationships to Existing Models

- **TestIndexEntry** (`_index.json`): Read `Criteria` (string[]) and `SourceRefs` (string[]) fields
- **AcceptanceCriterion** (`.criteria.yaml`): Read `Id`, `Text`, `Source`/`SourceDoc`, `Priority` fields
- **DocumentIndexEntry** (`docs/_index.md`): Read `Path` and `Sections[].Heading` for source ref matching
- **CriteriaIndex** (`_criteria_index.yaml`): Read `Sources[]` to locate `.criteria.yaml` files
