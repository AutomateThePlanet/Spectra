# Implementation Plan: Criteria Folder Rename, Index Exclusion & Coverage Fix

**Branch**: `026-criteria-folder-coverage-fix` | **Date**: 2026-04-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/026-criteria-folder-coverage-fix/spec.md`

## Summary

Three bugfixes in the criteria subsystem: (1) rename physical folder `docs/requirements/` → `docs/criteria/` with auto-migration, (2) exclude `_index.md` metadata files from criteria extraction, (3) fix dashboard showing "0 of 0" acceptance criteria coverage. All changes are path defaults, exclusion logic, and coverage aggregation — no new commands or features.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json, System.CommandLine, Spectre.Console, CsvHelper
**Storage**: File system (YAML, JSON, Markdown), SQLite (execution only — not affected)
**Testing**: xUnit, 1315+ existing tests
**Target Platform**: Windows, macOS, Linux (cross-platform CLI)
**Project Type**: CLI tool + MCP server
**Performance Goals**: N/A (bugfix, no new perf requirements)
**Constraints**: Backward compatible — existing `docs/requirements/` projects must auto-migrate
**Scale/Scope**: ~20 files modified, ~20 new tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | All criteria files remain in Git |
| II. Deterministic Execution | PASS | No state machine changes |
| III. Orchestrator-Agnostic | PASS | No MCP changes |
| IV. CLI-First | PASS | No new commands, existing CLI unaffected |
| V. Simplicity (YAGNI) | PASS | Bugfix only, no new abstractions |

## Project Structure

### Documentation (this feature)

```text
specs/026-criteria-folder-coverage-fix/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code (affected files)

```text
src/
├── Spectra.Core/
│   ├── Models/Config/CoverageConfig.cs          # Default path changes
│   └── Coverage/
│       └── AcceptanceCriteriaCoverageAnalyzer.cs # Fix coverage aggregation
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── Analyze/AnalyzeHandler.cs            # Add migration logic
│   │   ├── Init/InitHandler.cs                  # Create docs/criteria/
│   │   └── Update/UpdateHandler.cs              # Fix fallback defaults
│   ├── Agent/Copilot/CriteriaExtractor.cs       # Skip _index.md
│   ├── Dashboard/DataCollector.cs               # Fix criteria data population
│   └── Coverage/CoverageReportWriter.cs         # Fix user message

dashboard-site/
└── scripts/app.js                               # Fix unit label, empty state msg

tests/
├── Spectra.Core.Tests/Coverage/                 # Update paths, add new tests
├── Spectra.CLI.Tests/Commands/                  # Migration tests
└── Spectra.CLI.Tests/Dashboard/                 # Criteria coverage data tests
```

**Structure Decision**: No new directories or projects. All changes modify existing files.

## Complexity Tracking

No constitution violations.

## Research Findings

### Change 1: Folder Rename — Affected Locations

From research, these are the exact locations with hardcoded "docs/requirements":

| File | Lines | Type |
|------|-------|------|
| `CoverageConfig.cs` | 97, 103, 109 | Default property values |
| `InitHandler.cs` | 421, 440, 454 | Folder/file creation in init |
| `UpdateHandler.cs` | 243-244 | Null-coalescing fallback defaults |
| `CoverageReportWriter.cs` | 107 | User-facing error message |

All other code (AnalyzeHandler, DocsIndexHandler, CriteriaExtractor, readers/writers) resolves paths from config — changing `CoverageConfig.cs` defaults propagates automatically.

Documentation files to update:
- `CLAUDE.md` line 14
- `docs/configuration.md` lines 168, 184
- `docs/cli-reference.md` line 41
- `docs/coverage.md` lines 91, 165
- `docs/getting-started.md` lines 41-42
- `docs/PROJECT-KNOWLEDGE.md` lines 132, 411

### Change 2: Index Exclusion — Where to Add

`CriteriaExtractor.cs` receives document paths as parameters. The exclusion must be added in the caller that iterates documents — this is in `AnalyzeHandler.cs` where documents are enumerated for extraction. Add a `ShouldSkipDocument()` check before passing each document to the extractor.

### Change 3: Coverage Fix — Root Cause

`AcceptanceCriteriaCoverageAnalyzer.cs` (lines 21-70) has two entry points:
- `AnalyzeFromDirectoryAsync(criteriaDir, criteriaIndexPath)` — reads per-document files
- `AnalyzeAsync(criteriaFilePath)` — derives directory from file path

`DataCollector.cs` (line 729) calls with `config.Coverage.CriteriaFile` which points to the master index. Need to verify:
1. Whether `AnalyzeAsync` properly enumerates per-document `.criteria.yaml` files
2. Whether test frontmatter `criteria:` field matching works correctly
3. Whether `has_criteria_file` flag is populated in the JSON output

Dashboard `app.js` (line 927) reads `summary.acceptance_criteria` — need to verify C# serialization produces this exact key.

Additionally, `app.js` line 1349 displays `"X of Y acceptance_criteria covered"` — the unit label `acceptance_criteria` should be `criteria` for proper grammar.

## Implementation Approach

### Phase 1: Config & Default Path Changes
1. Update `CoverageConfig.cs` defaults to `"docs/criteria"`
2. Update `InitHandler.cs` to create `docs/criteria/`
3. Update `UpdateHandler.cs` fallback defaults
4. Update `CoverageReportWriter.cs` message

### Phase 2: Auto-Migration
1. Add migration helper method in `AnalyzeHandler.cs`
2. Call at start of analyze command execution
3. Rename folder, update config file, delete `_index.criteria.yaml`

### Phase 3: Index Exclusion
1. Add `ShouldSkipDocument()` method
2. Apply in document enumeration loop before extraction

### Phase 4: Coverage Fix
1. Read and debug `AcceptanceCriteriaCoverageAnalyzer.cs` flow
2. Fix per-document file enumeration and aggregation
3. Fix test frontmatter matching
4. Fix `DataCollector.cs` criteria data population
5. Fix `app.js` unit label and empty state message

### Phase 5: Documentation & Tests
1. Update all documentation references
2. Update test fixtures
3. Add new unit tests for migration, exclusion, and coverage
