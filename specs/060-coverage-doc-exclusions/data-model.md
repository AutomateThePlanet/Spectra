# Data Model: Coverage-Scoped Document Exclusions

**Feature**: 060-coverage-doc-exclusions | **Date**: 2026-06-08

All changes are additive. Existing fields and their JSON property names are unchanged to preserve
FR-005 (byte-for-byte equivalence when unconfigured).

## 1. `CoverageConfig` (modified) — `src/Spectra.Core/Models/Config/CoverageConfig.cs`

New property, sibling to the existing `AnalysisExcludePatterns`.

| Field | JSON key | Type | Default | Notes |
|-------|----------|------|---------|-------|
| `CoverageExcludePatterns` | `coverage_exclude_patterns` | `IReadOnlyList<string>` | `[]` (empty) | Glob patterns whose matched documents are dropped from the documentation-coverage **denominator only**. Empty = no exclusions (no behavior change). NOT merged with any defaults. |

**Validation rules**:
- Absent key → empty list (FR-007). No implicit defaults (FR-005).
- Blank/whitespace entries are ignored by the matcher (existing `ExclusionPatternMatcher` behavior).
- Patterns are repo-relative, forward-slash, full-glob (`**`, `*`, brace) — identical semantics to the
  other two exclusion lists.

**Independence (FR-006)**: Distinct field from `AnalysisExcludePatterns`. Matching one has no effect on
the other.

## 2. `DocumentCoverageDetail` (modified) — `src/Spectra.Core/Models/Coverage/DocumentationCoverage.cs`

| Field | JSON key | Type | Default | Notes |
|-------|----------|------|---------|-------|
| `Doc` | `doc` | `string` | (required) | unchanged |
| `TestCount` | `test_count` | `int` | (required) | unchanged |
| `Covered` | `covered` | `bool` | (required) | unchanged. `false` for excluded docs (they are not "covered"); the `Excluded` flag is the discriminator. |
| `TestIds` | `test_ids` | `string[]` | `[]` | unchanged |
| **`Excluded`** | `excluded` | `bool` | `false` | NEW. `true` when the doc matched a coverage-exclude pattern. Default `false` keeps unconfigured JSON identical except the key's presence — see note below. |
| **`ExcludedByPattern`** | `excluded_by_pattern` | `string?` | `null` | NEW. The first matching glob (for auditability, FR-003). `null` when not excluded. |

**Status precedence**: `Excluded` takes precedence over `Covered`. A doc that both matches an exclude
pattern and has linked tests is reported `excluded: true` (edge case in spec).

**FR-005 note**: To keep no-config JSON byte-for-byte identical, `excluded`/`excluded_by_pattern` are
emitted with `JsonIgnoreCondition.WhenWritingDefault` (omit `false`/`null`). When no patterns are
configured, every detail has the defaults, so the serialized output is unchanged. Human-readable output
shows the new status only when at least one doc is excluded.

## 3. `DocumentationCoverage` (aggregate, modified) — same file

| Field | JSON key | Type | Default | Notes |
|-------|----------|------|---------|-------|
| `TotalDocs` | `total_docs` | `int` | (required) | **Semantics refined**: counts only **non-excluded** docs (the denominator). When no exclusions, equals today's value. |
| `CoveredDocs` | `covered_docs` | `int` | (required) | unchanged (covered non-excluded docs) |
| `Percentage` | `percentage` | `decimal` | (required) | `CoveredDocs / TotalDocs`, existing zero-denominator rule (0 when `TotalDocs == 0`). |
| `UndocumentedTestCount` | `undocumented_test_count` | `int` | 0 | unchanged |
| `UndocumentedTestIds` | `undocumented_test_ids` | `string[]` | `[]` | unchanged |
| `Details` | `details` | `DocumentCoverageDetail[]` | `[]` | unchanged shape; now includes excluded docs flagged `excluded: true`. |
| **`ExcludedDocs`** | `excluded_docs` | `int` | `0` | NEW. Count of docs dropped from the denominator. `0` (omitted via WhenWritingDefault) when no exclusions → FR-005. |

**Invariant**: `TotalDocs + ExcludedDocs == Details.Count` (every mapped doc is either in the
denominator or excluded; none are dropped from `details`).

## 4. Legacy report model — `LegacyModels.DocumentCoverage` / `CoverageReport` (CLI)

The legacy path (`RunLegacyCoverageAsync`) builds its own model. Additive changes mirror the Core model:

| Model | New field | Type | Notes |
|-------|-----------|------|-------|
| `LegacyModels.DocumentCoverage` | `IsExcluded` (`is_excluded`) + `ExcludedByPattern` (`excluded_by_pattern`) | `bool` / `string?` | Excluded docs flagged; excluded from gap generation. |
| `LegacyModels.CoverageReport` | `ExcludedDocuments` (`excluded_documents`) | `int` | Excluded docs not counted in `CoveredDocuments`/`UncoveredDocuments`; `TotalDocuments` denominator excludes them. |

**Behavior**: An excluded doc does **not** produce a `CoverageGap` (it is intentionally out of scope),
and is not counted in `UncoveredDocuments`. With no patterns, all new fields are default → unchanged
output.

## 5. Relocated type — `ExclusionPatternMatcher`

Moves `src/Spectra.CLI/Source/ExclusionPatternMatcher.cs` →
`src/Spectra.Core/Source/ExclusionPatternMatcher.cs`. Namespace `Spectra.CLI.Source` →
`Spectra.Core.Source`. **No behavior change.** Public surface (`ctor(IReadOnlyList<string>)`,
`bool IsExcluded(string, out string?)`, `bool IsEmpty`) preserved. Existing consumers
(`DocumentIndexService`, `LegacyIndexMigrator`) update their `using`.

## Entity relationship summary

```
spectra.config.json
  └─ coverage.coverage_exclude_patterns: string[]   (NEW, empty default)
        │ feeds
        ▼
ExclusionPatternMatcher (relocated to Core)
        │ used by
        ├─► DocumentationCoverageAnalyzer.Analyze  → DocumentationCoverage { ExcludedDocs, Details[].Excluded }
        └─► RunLegacyCoverageAsync (inline)         → LegacyModels.CoverageReport { ExcludedDocuments, ... }
```
