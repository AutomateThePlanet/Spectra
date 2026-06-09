# Queued feature 050 — Coverage doc exclusions

> **Investigation-only.** No production code, specs, configs, or skills were modified.
> Every claim about current behavior is cited `file:line`. Hypotheses are marked `INFERRED`
> with what would confirm them.

## Two preliminaries

- **No draft file exists** for this feature. Searches across the repo for "coverage exclusion",
  "exclude doc", and a `queued/`/`backlog/`/`draft/` directory returned nothing. Intent below is
  reconstructed from the one-line summary: *"ability to exclude docs from coverage accounting."*
- **Numbering collision.** The repo already ships an *implemented, unrelated* spec
  `specs/050-from-desc-criteria-injection` (v1.52.6). The number `050` here is only a working label
  for this conceptual feature; the two have nothing to do with each other.

## Reconstructed intent

Let a user mark certain documents as out-of-scope for the documentation-coverage percentage, so the
coverage number reflects only docs that are *supposed* to have test cases. Typical targets:
release-notes, changelogs, summaries, archived/legacy material.

## 1. Does the problem still exist? — **Yes**

The documentation-coverage calculation is purely lexical and counts **every** document in the map,
with no per-analysis exclusion hook:

- `DocumentationCoverageAnalyzer.Analyze()` iterates `documentMap.Documents` and, for each doc,
  counts tests whose `source_refs` point at it; the percentage is `coveredDocs / totalDocs` over the
  full map (`src/Spectra.Core/Coverage/DocumentationCoverageAnalyzer.cs:20-39`). There is no model
  involvement anywhere in this path — it is string/ID matching only.
- The coverage command builds the map and feeds it straight to the analyzer with **no exclusion step
  in between**: `var documentMap = await mapBuilder.BuildAsync(basePath, ct)`
  (`src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs:174-175`) → `docAnalyzer.Analyze(documentMap,
  allTests)` (`AnalyzeHandler.cs:207-208`).

The only filter that currently reaches the coverage denominator is `SourceConfig.ExcludePatterns`,
applied at *discovery* time:

- `DocumentMapBuilder.BuildAsync` enumerates files via `_discovery.DiscoverWithRelativePaths`
  (`src/Spectra.CLI/Source/DocumentMapBuilder.cs:30`), and `SourceDiscovery` applies
  `SourceConfig.IncludePatterns` / `ExcludePatterns`.
- `SourceConfig.ExcludePatterns` defaults to `["**/CHANGELOG.md"]`
  (`src/Spectra.Core/Models/Config/SourceConfig.cs:48-49`).

But `SourceConfig.ExcludePatterns` is a **total** removal — an excluded doc disappears from the map
entirely, so it is also invisible to generation and analysis, not just coverage. There is no
*coverage-specific* exclusion that keeps a doc in scope for everything else while dropping it from
the coverage %.

### The `AnalysisExcludePatterns` near-miss (resolved, was INFERRED)

`CoverageConfig.AnalysisExcludePatterns` looks like it might already do this, but it does **not**
affect the coverage denominator:

- Its own doc-comment says matched docs are "still indexed but whose suites are flagged
  `skip_analysis: true`" (`src/Spectra.Core/Models/Config/CoverageConfig.cs:117-140`; defaults
  include `**/release-notes/**`, `**/CHANGELOG*`, `**/SUMMARY.md`).
- It is consumed **only by the index/migration path**, never by coverage:
  `src/Spectra.CLI/Source/DocumentIndexService.cs:255` and
  `src/Spectra.CLI/Index/LegacyIndexMigrator.cs:121` pass it into an `ExclusionPatternMatcher`. The
  coverage command (`AnalyzeHandler.cs:174-208`) never reads it.

**Confirmed (no longer INFERRED):** a doc matched by `AnalysisExcludePatterns` (e.g.
`docs/release-notes/v1.md`) but not by `SourceConfig.ExcludePatterns` is still in
`documentMap.Documents` and therefore still counts against the coverage percentage. That is exactly
the gap this feature targets.

## 2. Where is the seam now?

Entirely in the deterministic, model-free core + CLI coverage path. The migration never touched it
(coverage stayed lexical per ARCHITECTURE-v2 §48–59 / `docs/investigation/03-deterministic-core.md`).
Candidate owning components:

- **Config:** add a coverage-scoped exclusion list (conceptually
  `coverage.coverage_exclude_patterns`, distinct from both `SourceConfig.ExcludePatterns` and
  `CoverageConfig.AnalysisExcludePatterns`) in `src/Spectra.Core/Models/Config/CoverageConfig.cs`.
- **Analyzer:** apply the filter inside `DocumentationCoverageAnalyzer.Analyze`
  (`DocumentationCoverageAnalyzer.cs:20`), or filter the map just before the call in
  `AnalyzeHandler.cs:207`. Excluded docs should be reported as a distinct "excluded" status rather
  than silently dropped, so the denominator change is visible.
- **Reuse, do not reinvent the matcher:** `ExclusionPatternMatcher`
  (`src/Spectra.CLI/Source/ExclusionPatternMatcher.cs:12-55`) already wraps
  `Microsoft.Extensions.FileSystemGlobbing` for full glob semantics (`**`, `*`, brace expansion) and
  exposes `IsExcluded(path, out matchedPattern)`. It currently lives in `Spectra.CLI`; reusing it
  from `Spectra.Core` would require relocating it (or duplicating the small wrapper).

## 3. Verdict — **SURVIVES UNCHANGED**

The migration removed none of the ground this feature stood on. Coverage was lexical before and is
lexical now; the feature is a deterministic config + filter addition, implementable against current
code essentially as originally intended. The only refinement versus a naive original is to position
it as a *third, coverage-scoped* exclusion layer, since the codebase already has two adjacent
exclusion mechanisms with different semantics (total-removal vs. skip-analysis) that must not be
conflated.

## 4. Dependencies / risk

- **Regression net:** touches `Spectra.Core/Coverage` and the `AnalyzeHandler` coverage path.
  Changing the denominator changes the headline coverage number, so existing coverage tests and any
  dashboard snapshots will move — expected, but must be updated deliberately.
- **No provider/SDK entanglement.** This path imports no model and no Copilot SDK; it is unaffected
  by the still-pending provider/SDK retirement.
- **Naming hazard (call out in the spec):** three overlapping exclusion concepts now —
  `SourceConfig.ExcludePatterns` (remove from map entirely), `CoverageConfig.AnalysisExcludePatterns`
  (index but skip analysis), and the proposed coverage-scoped exclusion. Clear naming/docs are the
  main design risk, not technical difficulty.
