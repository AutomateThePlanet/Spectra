# Quickstart: Coverage-Aware Behavior Analysis

**Date**: 2026-04-13 | **Feature**: 044-coverage-aware-analysis

## What changed

The `spectra ai generate` analysis step now considers existing test coverage before recommending new tests. For mature suites, this means accurate gap-only recommendations instead of inflated counts.

## How to verify

### 1. Run analysis on an existing suite

```bash
spectra ai generate --suite my-suite --analyze-only
```

**Expected**: Output shows a coverage snapshot with criteria/doc coverage ratios and recommends only tests for genuine gaps.

### 2. Check JSON output

```bash
spectra ai generate --suite my-suite --analyze-only --output-format json
```

**Expected**: JSON includes `existing_test_count`, `total_criteria`, `covered_criteria`, `uncovered_criteria`, `uncovered_criteria_ids` fields.

### 3. Verify graceful degradation

```bash
spectra ai generate --suite new-suite --analyze-only
```

**Expected**: New suite with no `_index.json` produces standard analysis (no coverage section, no errors).

## Key files to review

| File | Change |
|------|--------|
| `src/Spectra.CLI/Agent/Analysis/CoverageSnapshot.cs` | New model |
| `src/Spectra.CLI/Agent/Analysis/CoverageSnapshotBuilder.cs` | New builder |
| `src/Spectra.CLI/Agent/Analysis/CoverageContextFormatter.cs` | New formatter |
| `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` | Accepts snapshot, injects context |
| `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | Builds snapshot before analysis |
| `src/Spectra.CLI/Prompts/Content/behavior-analysis.md` | New `{{coverage_context}}` placeholder |
| `src/Spectra.CLI/Output/AnalysisPresenter.cs` | Coverage summary display |
| `src/Spectra.CLI/Results/GenerateResult.cs` | New fields on GenerateAnalysis |
