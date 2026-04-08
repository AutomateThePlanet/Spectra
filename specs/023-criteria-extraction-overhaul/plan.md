# Implementation Plan: Acceptance Criteria Import & Extraction Overhaul

**Branch**: `023-criteria-extraction-overhaul` | **Date**: 2026-04-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/023-criteria-extraction-overhaul/spec.md`

## Summary

Overhaul the acceptance criteria system: rename "requirements" to "acceptance criteria" throughout the codebase, replace the single-prompt AI extraction (which truncates on large doc sets) with per-document iterative extraction producing individual `.criteria.yaml` files and a master index, and add import support for externally-authored criteria from YAML/CSV/JSON files. Extends coverage analysis and test generation to consume criteria from all sources.

## Technical Context

**Language/Version**: C# 12, .NET 8+  
**Primary Dependencies**: System.CommandLine (CLI), Spectre.Console (terminal UX), YamlDotNet (YAML parsing), System.Text.Json (JSON), CsvHelper (new — CSV import), GitHub Copilot SDK (AI extraction/splitting)  
**Storage**: File system (YAML criteria files, master index); SQLite (execution state — unchanged)  
**Testing**: xUnit, 1241+ existing tests  
**Target Platform**: Windows, Linux, macOS (cross-platform CLI)  
**Project Type**: CLI tool + library  
**Performance Goals**: Per-document extraction keeps AI calls bounded; incremental extraction via SHA-256 hashing  
**Constraints**: Each AI call processes a single document (bounded output, no truncation)  
**Scale/Scope**: Supports projects with 50+ documentation files and 1000+ criteria entries

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Criteria stored as YAML files in `docs/requirements/`, committed to Git |
| II. Deterministic Execution | PASS | SHA-256 hashing ensures same input → same skip/extract decisions; master index is deterministic |
| III. Orchestrator-Agnostic | PASS | Criteria are file-based; no MCP tool changes in this spec (CLI-only) |
| IV. CLI-First Interface | PASS | All operations via `spectra ai analyze` flags; `--dry-run` supported; exit codes defined |
| V. Simplicity (YAGNI) | PASS | No premature abstractions; CsvHelper is the only new dependency (justified for robust CSV parsing); per-document files are the simplest solution that eliminates truncation |

## Project Structure

### Documentation (this feature)

```text
specs/023-criteria-extraction-overhaul/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── cli-commands.md  # CLI command contracts
└── tasks.md             # Phase 2 output
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Models/Coverage/
│   │   ├── AcceptanceCriterion.cs           # NEW: replaces RequirementDefinition
│   │   ├── CriteriaIndex.cs                 # NEW: master index model
│   │   ├── CriteriaSource.cs               # NEW: per-source entry in index
│   │   ├── AcceptanceCriteriaCoverage.cs    # RENAME: from RequirementsCoverage
│   │   └── UnifiedCoverageReport.cs         # MODIFY: rename requirements → criteria
│   ├── Coverage/
│   │   ├── AcceptanceCriteriaCoverageAnalyzer.cs  # RENAME: from RequirementsCoverageAnalyzer
│   │   └── UnifiedCoverageBuilder.cs              # MODIFY: use new types
│   ├── Parsing/
│   │   ├── AcceptanceCriteriaParser.cs      # RENAME: from RequirementsParser
│   │   ├── CriteriaIndexReader.cs           # NEW: reads _criteria_index.yaml
│   │   ├── CriteriaIndexWriter.cs           # NEW: writes _criteria_index.yaml
│   │   ├── CriteriaFileReader.cs            # NEW: reads per-doc .criteria.yaml
│   │   ├── CriteriaFileWriter.cs            # NEW: writes per-doc .criteria.yaml
│   │   └── CsvCriteriaImporter.cs           # NEW: CSV column auto-detect + parse
│   └── Models/
│       ├── TestCaseFrontmatter.cs           # MODIFY: add Criteria field
│       ├── TestCase.cs                      # MODIFY: add Criteria field
│       └── TestIndexEntry.cs                # MODIFY: add Criteria field
├── Spectra.CLI/
│   ├── Commands/Analyze/
│   │   ├── AnalyzeCommand.cs                # MODIFY: add new flags
│   │   └── AnalyzeHandler.cs                # MODIFY: rewrite extraction, add import/list
│   ├── Agent/Copilot/
│   │   └── CriteriaExtractor.cs             # RENAME+REWRITE: from RequirementsExtractor
│   ├── Results/
│   │   ├── ExtractCriteriaResult.cs         # NEW: replaces ExtractRequirementsResult
│   │   ├── ImportCriteriaResult.cs          # NEW
│   │   └── ListCriteriaResult.cs            # NEW
│   ├── Output/
│   │   └── AnalysisPresenter.cs             # MODIFY: terminology
│   ├── Coverage/
│   │   └── CoverageReportWriter.cs          # MODIFY: terminology
│   ├── Skills/
│   │   ├── Content/Skills/spectra-criteria.md  # NEW: 7th SKILL
│   │   ├── Content/Skills/spectra-coverage.md  # MODIFY: terminology
│   │   ├── Content/Skills/spectra-help.md      # MODIFY: terminology
│   │   ├── Content/Agents/spectra-generation.agent.md  # MODIFY: criteria awareness
│   │   ├── Content/Agents/spectra-execution.agent.md   # MODIFY: terminology
│   │   ├── SkillContent.cs                    # MODIFY: add criteria SKILL
│   │   └── SkillsManifest.cs                  # MODIFY: add hash
│   └── Commands/Init/
│       └── InitHandler.cs                     # MODIFY: create criteria template files

dashboard-site/
├── scripts/app.js                             # MODIFY: terminology rename

tests/
├── Spectra.Core.Tests/
│   ├── Coverage/
│   │   └── AcceptanceCriteriaCoverageAnalyzerTests.cs  # RENAME from RequirementsCoverageAnalyzerTests
│   └── Parsing/
│       ├── AcceptanceCriteriaParserTests.cs   # RENAME from RequirementsParserTests
│       ├── CriteriaIndexReaderWriterTests.cs  # NEW
│       ├── CriteriaFileReaderWriterTests.cs   # NEW
│       └── CsvCriteriaImporterTests.cs        # NEW
├── Spectra.CLI.Tests/
│   └── Commands/
│       ├── ExtractCriteriaTests.cs            # NEW (replaces requirement extraction tests)
│       ├── ImportCriteriaTests.cs             # NEW
│       └── ListCriteriaTests.cs               # NEW
```

**Structure Decision**: Follows existing project structure exactly. New files are placed in the same directories as the files they replace or extend. No new projects needed.

## Complexity Tracking

No violations. The feature stays within existing project boundaries and uses established patterns.
