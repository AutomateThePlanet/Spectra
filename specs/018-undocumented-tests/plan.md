# Implementation Plan: Undocumented Behavior Test Cases

**Branch**: `018-undocumented-tests` | **Date**: 2026-03-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/018-undocumented-tests/spec.md`

## Summary

Enable test case creation from undocumented behavior descriptions via the generation agent. Extends the grounding metadata schema with a `Manual` verdict, bypasses critic verification for manual tests, adds undocumented test metrics to coverage analysis and dashboard (orange category), and updates the generation agent prompt with a conversational flow for capturing undocumented behaviors.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet, GitHub Copilot SDK
**Storage**: File-based (test Markdown files with YAML frontmatter, `_index.json`), SQLite (`.execution/spectra.db`)
**Testing**: xUnit (984 existing tests across 3 test projects)
**Target Platform**: Cross-platform CLI (.NET 8)
**Project Type**: CLI tool + MCP server
**Performance Goals**: N/A — no latency-sensitive paths affected
**Constraints**: Fully additive — zero breaking changes to existing test cases or workflows
**Scale/Scope**: ~15 files modified, ~5 new fields across existing models, ~30 new tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Tests remain as Markdown files in `tests/{suite}/` with YAML frontmatter |
| II. Deterministic Execution | PASS | Manual verdict is a static metadata value — no non-deterministic behavior introduced |
| III. Orchestrator-Agnostic Design | PASS | Grounding metadata is in the test file, not tied to any specific LLM |
| IV. CLI-First Interface | PASS | Coverage analysis exposed via `spectra ai analyze --coverage`; generation via `spectra ai generate` |
| V. Simplicity (YAGNI) | PASS | Extends existing enum + adds 3 fields to existing DTO; no new abstractions or patterns |

**Post-Phase 1 Re-check**: All gates still pass. No new projects, dependencies, or abstractions introduced. The `Manual` verdict is a single enum value addition. Coverage metric is a count field on an existing model.

## Project Structure

### Documentation (this feature)

```text
specs/018-undocumented-tests/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Entity extensions
├── quickstart.md        # Implementation guide
├── contracts/
│   ├── grounding-metadata-extended.schema.json
│   └── undocumented-coverage.schema.json
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Models/
│   │   ├── Grounding/
│   │   │   ├── VerificationVerdict.cs      # Add Manual value
│   │   │   └── GroundingFrontmatter.cs     # Add source, created_by, note fields
│   │   ├── Coverage/
│   │   │   └── DocumentationCoverage.cs    # Add UndocumentedTestCount, UndocumentedTestIds
│   │   └── Dashboard/
│   │       └── CoverageSummaryData.cs      # Add undocumented fields to DocumentationSectionData
│   └── Coverage/
│       └── DocumentationCoverageAnalyzer.cs # Count tests with empty source_refs
├── Spectra.CLI/
│   ├── Agent/
│   │   ├── GroundedPromptBuilder.cs        # Add undocumented behavior flow to system prompt
│   │   └── Critic/
│   │       └── CriticPromptBuilder.cs      # No changes needed
│   ├── Commands/
│   │   └── Generate/
│   │       └── GenerateHandler.cs          # Skip critic for manual verdict
│   ├── Coverage/
│   │   └── CoverageReportWriter.cs         # Surface undocumented metric in reports
│   └── Dashboard/
│       ├── DataCollector.cs                # Populate undocumented metrics
│       └── Templates/
│           ├── styles/main.css             # Add --cov-orange CSS variable
│           └── scripts/app.js              # Orange category rendering + filter toggle

tests/
├── Spectra.Core.Tests/
│   ├── Models/Grounding/                   # VerificationVerdict.Manual tests
│   └── Coverage/                           # DocumentationCoverageAnalyzer undocumented tests
├── Spectra.CLI.Tests/
│   ├── Commands/Generate/                  # Critic bypass integration tests
│   ├── Coverage/                           # Report writer undocumented metric tests
│   └── Dashboard/                          # DataCollector undocumented metric tests
```

**Structure Decision**: All changes are modifications to existing files in the established project structure. No new projects or directories needed (except test files).

## Complexity Tracking

No constitution violations. No complexity justifications needed.
