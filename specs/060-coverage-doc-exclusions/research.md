# Research: Coverage-Scoped Document Exclusions

**Feature**: 060-coverage-doc-exclusions | **Date**: 2026-06-08

No open `NEEDS CLARIFICATION` items existed in the spec. This document records the design decisions
that resolve the few degrees of freedom the spec deliberately left to planning.

## Decision 1 — Config key name & location

**Decision**: Add `coverage_exclude_patterns` to the existing `coverage` config section
(`CoverageConfig`), as a sibling of `analysis_exclude_patterns`.

**Rationale**: FR-001 requires a name that makes the three mechanisms non-confusable. The three keys
become:
- `source.exclude_patterns` — discovery (total removal)
- `coverage.analysis_exclude_patterns` — analysis-skip (`skip_analysis: true`, index path only)
- `coverage.coverage_exclude_patterns` — coverage denominator only (this feature)

Placing the new key under `coverage` groups it with the analysis-skip key it must be distinguished
from, and the `coverage_exclude_*` prefix names the scope it affects. The word "coverage" appearing in
both the section and the key is intentional reinforcement of scope.

**Alternatives considered**:
- A top-level `coverage_exclude_patterns` — rejected; loses the grouping that aids the disambiguation
  table.
- Reusing/overloading `analysis_exclude_patterns` for coverage — rejected; explicitly forbidden by
  FR-006 (the two must stay independent), and would silently change existing coverage numbers.

## Decision 2 — Empty default (no implicit patterns)

**Decision**: Default value is an empty list `[]`. No default patterns.

**Rationale**: FR-005/FR-007 require byte-for-byte unchanged output for unconfigured workspaces. Unlike
`analysis_exclude_patterns` (which ships a default `**/release-notes/**`, `**/CHANGELOG*`, etc.), this
list must start empty or every existing workspace's coverage number would shift on upgrade — a silent
regression. Users opt in explicitly.

**Alternatives considered**: Seeding the same defaults as `analysis_exclude_patterns` — rejected;
violates FR-005 (would move the denominator for everyone on upgrade).

## Decision 3 — Reuse + relocate `ExclusionPatternMatcher`

**Decision**: Move `ExclusionPatternMatcher` from `src/Spectra.CLI/Source/` to
`src/Spectra.Core/Source/`, preserving the type name and behavior. Update the two existing CLI
consumers (`DocumentIndexService`, `LegacyIndexMigrator`) to the new namespace.

**Rationale**: FR-004 mandates reuse, not duplication. The matcher currently lives in CLI, but the
denominator filtering belongs in `DocumentationCoverageAnalyzer` (Core). A Core analyzer cannot
reference a CLI type, so the matcher must move down to Core. CLI references Core, so all existing
consumers keep compiling after a `using` update. The class is dependency-light (only
`Microsoft.Extensions.FileSystemGlobbing`, already available transitively/in Core), making the move
clean. This is the "third consumer" that justifies relocation under Constitution Principle V.

**Behavior preservation**: The relocation is a pure move — no logic change. Existing matcher tests (if
any) move with it; new tests assert identical match results for representative globs.

**Alternatives considered**:
- Duplicate a matcher in Core — rejected by FR-004.
- Invert the call (have CLI pass a delegate into Core) — rejected; more indirection than a file move,
  and Core would still need the glob types.

## Decision 4 — Where exclusion is applied (both coverage paths)

**Decision**: Apply the exclusion in two places that both compute the documentation-coverage
percentage from `documentMap.Documents`:
1. **Unified path** — `DocumentationCoverageAnalyzer.Analyze` gains the patterns and marks/filters
   excluded docs (`AnalyzeHandler.cs:207-208`).
2. **Legacy path** — the inline doc-coverage loop in `RunLegacyCoverageAsync` (`AnalyzeHandler.cs:1533+`)
   applies the same matcher and represents excluded docs in `LegacyModels.CoverageReport`.

**Rationale**: The spec assumption states the exclusion applies to both paths because both surface the
documentation-coverage percentage. Filtering only one would leave a path where excluded docs still drag
the number. The single source of matching truth is the relocated `ExclusionPatternMatcher`; both call
sites construct it from the same config list.

**Alternatives considered**:
- Filter `documentMap.Documents` once, upstream of both paths — **rejected**; that would remove the
  docs from the map entirely, violating FR-002 (they must remain for generation/analysis/indexing) and
  collapsing this feature into the existing `source.exclude_patterns` behavior. The filter must be
  *local to coverage*, not at the map.

## Decision 5 — Representing "excluded" status (fail-loud)

**Decision**: Excluded docs stay in the per-document details list with a new `excluded: true` flag and
the matched pattern, and are counted in a new aggregate `excluded_docs` field. They are removed from
the denominator (`total_docs` counts only non-excluded docs) and are neither `covered` nor counted as
uncovered/gaps. Exclusion takes precedence over covered/uncovered classification.

**Rationale**: FR-003 requires excluded docs be visibly present, not silently dropped and not
misclassified. Keeping them in `details` with a distinct flag satisfies auditability (User Story 2) for
both JSON and human-readable outputs. Removing them from the denominator while keeping them listed is
exactly the "visible adjustment" the metric needs.

**Alternatives considered**:
- Drop excluded docs from `details` entirely — rejected; violates FR-003 (silent drop).
- Keep them in the denominator but tag them — rejected; defeats the entire purpose (number wouldn't
  improve).

## Summary of resolved unknowns

| Unknown | Resolution |
|---------|------------|
| Config key name | `coverage.coverage_exclude_patterns` |
| Default value | Empty list (no implicit patterns) |
| Matcher reuse | Relocate `ExclusionPatternMatcher` CLI→Core, behavior-preserving |
| Application points | Unified analyzer + legacy inline loop (both) |
| "Excluded" representation | `excluded` flag + matched pattern in details; `excluded_docs` aggregate; out of denominator |
