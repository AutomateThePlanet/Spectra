# Implementation Plan: Coverage-Scoped Document Exclusions

**Branch**: `060-coverage-doc-exclusions` | **Date**: 2026-06-08 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/060-coverage-doc-exclusions/spec.md`

## Summary

Add a third, **coverage-scoped** document exclusion mechanism (`coverage.coverage_exclude_patterns`)
that drops matched documents from the documentation-coverage **denominator only**, leaving them fully
present in the document map for generation, analysis, and indexing. Excluded documents are reported
with a distinct `excluded` status (fail-loud, never silently dropped). The matcher is the existing
`ExclusionPatternMatcher` (relocated from `Spectra.CLI/Source/` to `Spectra.Core` so the Core analyzer
can reuse it, behavior-preserving). With no patterns configured, output is byte-for-byte unchanged.

Technical approach: thread the configured patterns into `DocumentationCoverageAnalyzer.Analyze`
(unified path) and into the inline doc-coverage loop in `RunLegacyCoverageAsync` (legacy path), since
both compute the percentage over `documentMap.Documents`. Extend the `DocumentCoverageDetail` /
`DocumentationCoverage` models (and the legacy report model) with an `excluded` flag + matched pattern.
Update the report writer to surface the new status.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: Microsoft.Extensions.FileSystemGlobbing (existing — used by `ExclusionPatternMatcher`), System.Text.Json
**Storage**: File-based — `spectra.config.json` (config), `docs/` (document map source). No DB involvement in this path.
**Testing**: xUnit — `Spectra.Core.Tests` (analyzer/config/matcher), `Spectra.CLI.Tests` (handler/report)
**Target Platform**: Cross-platform CLI (win32 primary dev host)
**Project Type**: CLI + shared Core library (single-repo, multi-project)
**Performance Goals**: No new performance budget — exclusion is an O(docs × patterns) glob check on an already-loaded in-memory document map; negligible.
**Constraints**: FR-005 — no-config path MUST be byte-for-byte identical to current output. FR-004 — reuse `ExclusionPatternMatcher`, relocate (don't duplicate). Deterministic, model-free path only.
**Scale/Scope**: Single config key, one relocated class, ~2 model extensions, 2 analyzer call sites, 1 report writer. Documentation: coverage + configuration docs.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ Configuration in `spectra.config.json`; docs in `docs/`. No external state introduced. |
| **II. Deterministic Execution** | ✅ Pure deterministic, lexical, model-free path. Same inputs → same coverage output. No MCP/state-machine surface touched. |
| **III. Orchestrator-Agnostic Design** | ✅ No AI/orchestrator/provider surface involved. No MCP tool changes. |
| **IV. CLI-First Interface** | ✅ Feature is exposed via existing `spectra ai analyze --coverage` and config. The agent never writes files directly — output flows through the existing report writer. No new command required. |
| **V. Simplicity (YAGNI)** | ✅ Reuses the existing matcher (FR-004) rather than adding an abstraction; relocate (3rd consumer justifies the move to Core). Additive config key, no feature flags, no back-compat shims. The "3rd similar use case" rule is satisfied — this is the third exclusion consumer, justifying relocation to Core. |

**Gate result: PASS.** No violations. Complexity Tracking table not required.

Re-check after Phase 1 design: **PASS** (no new projects, no new dependencies, no new MCP tools; the only structural change is a behavior-preserving file relocation within existing projects).

## Project Structure

### Documentation (this feature)

```text
specs/060-coverage-doc-exclusions/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (config + report contracts)
│   ├── config-schema.md
│   └── coverage-report.md
├── checklists/
│   └── requirements.md  # (created by /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Coverage/
│   │   └── DocumentationCoverageAnalyzer.cs   # MODIFY: accept coverage-exclude patterns, mark excluded, drop from denominator
│   ├── Models/
│   │   ├── Config/
│   │   │   └── CoverageConfig.cs              # MODIFY: add CoverageExcludePatterns (empty default)
│   │   └── Coverage/
│   │       └── DocumentationCoverage.cs       # MODIFY: add Excluded/ExcludedByPattern to detail; ExcludedDocs to aggregate
│   └── Source/                                # NEW location for relocated matcher
│       └── ExclusionPatternMatcher.cs         # RELOCATE from Spectra.CLI/Source (behavior-preserving)
└── Spectra.CLI/
    ├── Source/
    │   └── ExclusionPatternMatcher.cs         # REMOVE (moved to Core); update namespace usages
    ├── Commands/Analyze/
    │   └── AnalyzeHandler.cs                  # MODIFY: pass patterns to analyzer (unified + legacy paths)
    └── Coverage/
        └── CoverageReportWriter.cs           # MODIFY: render the distinct "excluded" status

tests/
├── Spectra.Core.Tests/
│   └── Coverage/                              # NEW + existing: analyzer exclusion tests, no-config equivalence, 3-concept disambiguation
└── Spectra.CLI.Tests/
    └── ...                                    # report-rendering + handler-wiring tests

docs/                                          # MODIFY: coverage + configuration docs (3-mechanism table)
```

**Structure Decision**: Single-repo multi-project layout (already in place). The one structural change
is relocating `ExclusionPatternMatcher` from `Spectra.CLI/Source/` to `Spectra.Core/Source/` so the
Core-resident `DocumentationCoverageAnalyzer` can consume it without a CLI→Core inversion. The
relocation preserves the public type name and behavior; the existing CLI consumers
(`DocumentIndexService`, `LegacyIndexMigrator`) update only their `using`/namespace. The CLI still
references Core, so all existing consumers keep compiling.

## Complexity Tracking

> No constitutional violations. Table intentionally omitted.
