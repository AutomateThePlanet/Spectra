# Implementation Plan: Coverage Semantics Fix & Criteria-Generation Pipeline

**Branch**: `028-coverage-criteria-fix` | **Date**: 2026-04-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/028-coverage-criteria-fix/spec.md`

## Summary

Wire acceptance criteria into the test generation pipeline so generated tests include `criteria: [AC-XXX]` fields in frontmatter, enabling non-zero criteria coverage. The coverage analyzers themselves are already correct — the real bug is that GenerateHandler never loads or passes criteria context to the AI agent.

## Audit Findings (What's Actually Broken)

| Component | Status | Finding |
|-----------|--------|---------|
| DocumentationCoverageAnalyzer | **OK** | Correctly checks test existence via `source_refs`, not automation |
| AcceptanceCriteriaCoverageAnalyzer | **OK** | Correctly reads BOTH `test.Requirements` AND `test.Criteria` fields |
| AutomationScanner | **OK** | Correctly scans automation files for test IDs |
| TestCaseFrontmatter | **OK** | Has both `Requirements` and `Criteria` properties |
| TestCase model | **OK** | Propagates both fields |
| TestIndexEntry | **OK** | Includes both fields in `_index.json` |
| TestCaseParser | **OK** | Parses `criteria` from YAML frontmatter |
| TestFileWriter | **OK** | Writes `criteria` to files (only when non-empty) |
| TestClassifier | **OK** | Already detects orphaned/outdated criteria references |
| **GenerateHandler** | **BUG** | Does NOT load criteria before calling AI agent |
| **CopilotGenerationAgent** | **BUG** | Has `criteriaContext` parameter in `BuildFullPrompt()` but it's always null |
| Dashboard DataCollector | **OK** | Correctly collects all three coverage types |

**Root cause**: GenerateHandler never loads criteria, so AI never receives criteria context, so generated tests never contain `criteria: [AC-XXX]`, so criteria coverage is always 0%.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: YamlDotNet (frontmatter), System.Text.Json, GitHub Copilot SDK
**Storage**: File-based (`.criteria.yaml`, test `.md` files, `_index.json`)
**Testing**: xUnit
**Target Platform**: Windows/Linux/macOS CLI
**Project Type**: CLI tool
**Constraints**: Backward compatibility with `requirements: []` field

## Constitution Check

| Principle | Status |
|-----------|--------|
| I. GitHub as Source of Truth | Pass |
| II. Deterministic Execution | Pass |
| III. Orchestrator-Agnostic | Pass |
| IV. CLI-First Interface | Pass |
| V. Simplicity (YAGNI) | Pass |

## Project Structure

### Source Code (files to modify)

```text
src/Spectra.CLI/
├── Commands/Generate/GenerateHandler.cs         # FIX: load criteria before AI call
├── Agent/Copilot/GenerationAgent.cs             # FIX: pass criteriaContext through call chain
└── IO/TestFileWriter.cs                         # FIX: write criteria: [] even when empty

tests/
├── Spectra.Core.Tests/Coverage/                 # ADD: semantic verification tests
└── Spectra.CLI.Tests/                           # ADD: criteria loading tests
```

## Implementation Details

### Phase 1: Wire Criteria into Generation Pipeline

#### 1.1 GenerateHandler — Load criteria before AI call

Before calling `agent.GenerateTestsAsync()`:
1. Read criteria from per-doc `.criteria.yaml` files matching the target suite's documents
2. Also match criteria by `component` field equaling suite name
3. Format as context string with IDs and text
4. Pass to agent

#### 1.2 CopilotGenerationAgent — Pass criteria context through call chain

`BuildFullPrompt(prompt, count, criteriaContext)` already accepts `criteriaContext` but it's always null. Add criteriaContext parameter to `GenerateTestsAsync()` and pass through.

#### 1.3 TestFileWriter — Always write criteria field

Change to write `criteria: []` even when empty so the field is visible and editable.

### Phase 2: Verify Coverage Semantics (tests only)

The analyzers are correct. Add regression tests:
- DocCoverage: document with tests (no automation) → covered
- CriteriaCoverage: criterion in `criteria: []` → covered; in `requirements: []` → covered (backward compat)
- AutomationCoverage: existing tests sufficient

### Phase 3: Dashboard & Reports (verify only)

DataCollector already uses AcceptanceCriteriaCoverageAnalyzer correctly. Verify gap types display.

### Phase 4: Update Flow (verify only)

TestClassifier already handles orphaned/outdated criteria. Verify UpdateHandler passes criteria data.

## Implementation Order

1. Phase 1.1-1.2: Wire criteria into GenerateHandler → Agent (core fix)
2. Phase 1.3: TestFileWriter always writes `criteria: []`
3. Phase 2: Add coverage semantic tests
4. Phase 3-4: Verify dashboard and update flow, fix if needed
5. Documentation updates
