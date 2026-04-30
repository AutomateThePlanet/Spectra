# Implementation Plan: Document Index Restructure

**Branch**: `045-doc-index-restructure` | **Date**: 2026-04-29 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/045-doc-index-restructure/spec.md`

## Summary

Replace the monolithic `docs/_index.md` with a structured layout under `docs/_index/` (manifest + per-suite index files + separated checksum store). Suite identity is derived from the documentation directory tree, with overrides via per-document frontmatter and project config. Default exclusion patterns flag archived/release-notes suites as analysis-skipped. All consumers (`BehaviorAnalyzer`, `RequirementsExtractor` / `CriteriaExtractor`, `DocumentationCoverageAnalyzer`, dashboard data collector) load the manifest first and fetch only the suite index files they need, capped by a configurable pre-flight token budget that fails fast with an actionable error rather than overflowing the model context window. Legacy single-file indexes auto-migrate on first run, atomic on failure, with a `.bak` of the original preserved.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: GitHub Copilot SDK, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet (already in 044), Microsoft.Extensions.FileSystemGlobbing for glob matching
**Storage**: File-based — `docs/_index/_manifest.yaml` (YAML), `docs/_index/groups/{suite}.index.md` (Markdown), `docs/_index/_checksums.json` (JSON)
**Testing**: xUnit (Spectra.Core.Tests, Spectra.CLI.Tests, Spectra.MCP.Tests). Real 541-doc legacy fixture under `tests/TestFixtures/legacy_index_541docs/` for migration tests. Synthetic tree generators for suite resolver and exclusion-pattern tests.
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS). Path handling MUST use forward slashes for stored relative paths; `Path.DirectorySeparatorChar` only at filesystem boundaries.
**Project Type**: CLI tool (existing `Spectra.CLI`/`Spectra.Core`/`Spectra.MCP` solution layout — no new project)
**Performance Goals**: `spectra docs index` on a 541-doc / 12-suite project completes in ≤ 1.5× the wall-clock time of the legacy single-file indexer. Manifest read cost ≤ 50ms cold. Per-suite file IO is acceptable up to ~50 suites.
**Constraints**: Pre-flight prompt size MUST stay below `ai.analysis.max_prompt_tokens` (default 96,000) — the model has a 128K context window, the 32K margin covers response + prompt template + ancillary content. No new top-level CLI commands in Phase 1; existing `spectra docs index` keeps the same primary invocation. Atomic file writes (write-to-temp then rename) for the manifest, checksum store, and any group file.
**Scale/Scope**: Reported worst case is 541 docs / 12 suites. Spillover (per-doc files) triggers when a single suite exceeds `coverage.max_suite_tokens` (default 80,000) — the 200-doc single-suite fixture from §5.1 of the source spec covers this case.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance | Notes |
|---|---|---|
| **I. GitHub as Source of Truth** | ✅ | All artifacts (`_manifest.yaml`, `groups/*.index.md`, `_checksums.json`) live under `docs/` and are designed to be checked into Git. No external storage. |
| **II. Deterministic Execution** | ✅ | Suite resolution rules are priority-ordered and total: same input always produces the same suite assignment. Manifest content is sorted by suite ID; entries within a suite sorted by relative path. Migration is idempotent. |
| **III. Orchestrator-Agnostic Design** | ✅ | The pre-flight token budget fails locally before any AI call; no orchestrator-specific assumptions. The new manifest is just a smaller serialization of existing content — no new MCP tools or AI contracts. |
| **IV. CLI-First Interface** | ✅ | All new behavior reachable via existing CLI commands (`spectra docs index`, `spectra ai generate`, `spectra ai analyze`). New flags (`--no-migrate`, `--include-archived`, `--suites <ids>`) are deterministic and CI-friendly. Introspection commands (`list-suites`, `show-suite`) deferrable to Phase 4. |
| **V. Simplicity (YAGNI)** | ✅ with note | The new layout adds three artifacts where one used to exist — justified because each artifact has a different lifecycle (manifest = always loaded, suite files = lazy-loaded, checksums = never AI-visible). The "spillover" sub-feature (Phase 3) is genuinely speculative for Spec 041's needs but freezes the file shape so 041 doesn't have to introduce new shapes. Acceptable per §6 of the source spec. |

**Gate result**: PASS. No Complexity Tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/045-doc-index-restructure/
├── plan.md              # This file (/speckit.plan output)
├── spec.md              # /speckit.specify output (already written)
├── research.md          # Phase 0 output (this command)
├── data-model.md        # Phase 1 output (this command)
├── quickstart.md        # Phase 1 output (this command)
├── contracts/           # Phase 1 output (this command)
│   ├── manifest-schema.md
│   ├── checksum-store-schema.md
│   ├── suite-index-file-format.md
│   ├── docs-index-result.json
│   └── cli-surface.md
├── checklists/
│   └── requirements.md  # Already written by /speckit.specify
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

The feature spans existing projects. No new top-level projects are created.

```text
src/
├── Spectra.Core/
│   ├── Models/
│   │   └── Index/                              # NEW namespace
│   │       ├── DocIndexManifest.cs             # NEW — top-level manifest model
│   │       ├── DocSuiteEntry.cs                # NEW — one suite in manifest
│   │       ├── ChecksumStore.cs                # NEW — separated hash store
│   │       └── SuiteIndexFile.cs               # NEW — per-suite parsed model
│   ├── Index/
│   │   ├── DocumentIndexReader.cs              # KEEP, marked legacy-only
│   │   ├── DocumentIndexWriter.cs              # KEEP, marked legacy-only
│   │   ├── DocIndexManifestReader.cs           # NEW — YAML reader
│   │   ├── DocIndexManifestWriter.cs           # NEW — YAML writer (atomic)
│   │   ├── ChecksumStoreReader.cs              # NEW — JSON reader
│   │   ├── ChecksumStoreWriter.cs              # NEW — JSON writer (atomic)
│   │   ├── SuiteIndexFileReader.cs             # NEW — per-suite MD reader
│   │   └── SuiteIndexFileWriter.cs             # NEW — per-suite MD writer (atomic)
│   └── Models/Config/
│       ├── SourceConfig.cs                     # MODIFY — add doc_index_dir, group_overrides
│       ├── CoverageConfig.cs                   # MODIFY — add analysis_exclude_patterns, max_suite_tokens
│       └── AnalysisConfig.cs                   # MODIFY — add max_prompt_tokens
│
├── Spectra.CLI/
│   ├── Source/
│   │   ├── DocumentIndexService.cs             # MODIFY — orchestrate manifest + suite + checksum writers
│   │   ├── SuiteResolver.cs                    # NEW — implements §3.5 of spec
│   │   ├── ExclusionPatternMatcher.cs          # NEW — globs (Phase 3)
│   │   └── DocumentMapBuilder.cs               # MODIFY — read from manifest
│   ├── Index/
│   │   ├── LegacyIndexMigrator.cs              # NEW — implements §3.8
│   │   └── PreFlightTokenChecker.cs            # NEW — actionable budget error
│   ├── Commands/
│   │   └── Docs/
│   │       ├── DocsIndexHandler.cs             # MODIFY — migration + new layout
│   │       ├── DocsIndexCommand.cs             # MODIFY — new flags --no-migrate, --include-archived, --suites, --suites-only
│   │       ├── DocsListSuitesHandler.cs        # NEW (Phase 4) — list-suites
│   │       └── DocsShowSuiteHandler.cs         # NEW (Phase 4) — show-suite
│   ├── Agent/Copilot/
│   │   ├── BehaviorAnalyzer.cs                 # MODIFY — manifest-driven loading + pre-flight check
│   │   ├── RequirementsExtractor.cs            # MODIFY — iterate from manifest
│   │   └── SuiteSelector.cs                    # NEW — maps --suite/--focus/no-filter to suite IDs
│   ├── Coverage/
│   │   └── DocumentationCoverageAnalyzer.cs    # MODIFY — walk manifest
│   └── Results/
│       └── DocsIndexResult.cs                  # MODIFY — add suites[], manifest, migration{}
│
└── Spectra.CLI/Dashboard/
    └── DataCollector.cs                        # MODIFY — manifest first, lazy per-suite

tests/
├── Spectra.Core.Tests/
│   ├── Index/
│   │   ├── DocIndexManifestRoundTripTests.cs   # NEW (~6)
│   │   ├── ChecksumStoreRoundTripTests.cs      # NEW (~4)
│   │   └── SuiteIndexFileRoundTripTests.cs     # NEW (~3)
│   └── Models/
│       └── DocSuiteEntryTests.cs               # NEW (~3)
├── Spectra.CLI.Tests/
│   ├── Source/
│   │   ├── SuiteResolverTests.cs               # NEW (~10)
│   │   ├── ExclusionPatternMatcherTests.cs     # NEW (~8)
│   │   └── SuiteSelectorTests.cs               # NEW (~7)
│   ├── Index/
│   │   ├── LegacyIndexMigratorTests.cs         # NEW (~9, includes 541-doc fixture)
│   │   ├── PreFlightTokenCheckerTests.cs       # NEW (~5)
│   │   └── DocsIndexHandler_NewLayoutTests.cs  # NEW (~7)
│   ├── Agent/Copilot/
│   │   ├── BehaviorAnalyzer_ManifestDrivenTests.cs       # NEW (~6)
│   │   ├── BehaviorAnalyzer_PreFlightTokenLimitTests.cs  # NEW (~4)
│   │   ├── BehaviorAnalyzer_SuiteResolutionTests.cs      # NEW (~6)
│   │   └── RequirementsExtractor_SkipsArchivedTests.cs   # NEW (~5)
│   ├── Coverage/
│   │   └── DocumentationCoverage_FromManifestTests.cs    # NEW (~4)
│   ├── Dashboard/
│   │   └── DataCollector_LazySuiteLoadTests.cs           # NEW (~3)
│   └── Commands/Docs/
│       ├── DocsListSuitesHandlerTests.cs       # NEW Phase 4 (~4)
│       └── DocsShowSuiteHandlerTests.cs        # NEW Phase 4 (~3)
└── TestFixtures/
    ├── legacy_index_541docs/                   # NEW — real reported file
    │   ├── _index.md
    │   └── README.md
    ├── large_single_suite/                     # NEW — 200 synthetic docs
    └── with_archived/                          # NEW — Old/, legacy/, archive/, release-notes/
```

**Structure Decision**: This feature reuses the existing three-project structure (Spectra.Core / Spectra.CLI / Spectra.MCP). New code lives in `Spectra.Core/Index/` (file readers/writers + models) and `Spectra.CLI/Source/`, `Spectra.CLI/Index/`, `Spectra.CLI/Agent/Copilot/`. The split mirrors the existing pattern from Spec 023 (`CriteriaIndexReader`/`CriteriaIndexWriter` in Core; orchestration in CLI). No new top-level project. Test fixtures land under `tests/TestFixtures/`, matching the existing `TestFixtures` convention referenced in `CLAUDE.md`.

## Complexity Tracking

> No Constitution violations. This table is empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _(none)_ | _(none)_ | _(none)_ |
