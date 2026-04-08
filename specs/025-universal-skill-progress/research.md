# Research: Universal Progress/Result for SKILL-Wrapped Commands

**Date**: 2026-04-08  
**Branch**: `025-universal-skill-progress`

## Research Questions & Findings

### RQ-1: Which commands already have progress/result support?

**Decision**: Only GenerateHandler and DocsIndexHandler have full progress/result support. Four handlers need it added.

**Findings**:
- `GenerateHandler` (lines 1682-1811): Full inline implementation with `FlushWriteFile()`, `WriteResultFile()`, `WriteInProgressResultFile()`, `WriteErrorResultFile()`, and `ProgressPageWriter.WriteProgressPage()` calls
- `DocsIndexHandler` (lines 236-327): Same pattern, independently implemented with identical helper methods, supports phases: scanning → indexing → extracting-criteria → completed
- `UpdateHandler`: Uses `ProgressReporter` for console spinners only. No file output.
- `AnalyzeHandler`: Uses `Console.WriteLine()` only. No ProgressReporter, no file output.
- `DashboardHandler`: Uses `Console.WriteLine()` only. No ProgressReporter, no file output.
- `ValidateHandler`: Uses `Console.Error.WriteLine()` only. No file output.

**Rationale**: The two existing implementations have identical duplicated code (~130 lines each). Extracting a shared `ProgressManager` eliminates this duplication and makes it trivial to add to the remaining four handlers.

### RQ-2: What does the existing ProgressPageWriter support?

**Decision**: ProgressPageWriter is already generalized for multiple commands but needs minor extension for new command types.

**Findings** (file: `src/Spectra.CLI/Progress/ProgressPageWriter.cs`, 621 lines):
- Static utility class with `WriteProgressPage(htmlPath, jsonData, isTerminal)` and `BuildHtml()`
- Already supports two phase stepper configs: generate phases and docs-index phases
- Auto-refresh via `<meta http-equiv="refresh" content="2">` tag, omitted when `isTerminal=true`
- Renders phase stepper, status card, summary cards, analysis breakdown, error card, file links
- Atomic write pattern: writes to temp file first, then moves to final path
- All HTML is self-contained (no external resources)

**What needs extension**:
- Add phase stepper configs for: coverage, extract-criteria, dashboard, update
- Add summary card renderers for each new command type
- The `BuildStepper()` method switches on `status` string — needs new status values

### RQ-3: What typed result models already exist?

**Decision**: All needed result models exist from specs 020/023. Only `AnalyzeCoverageResult` needs a field rename.

**Findings** (all in `src/Spectra.CLI/Results/`):
| Model | Key Fields | Needs Changes? |
|-------|-----------|----------------|
| `CommandResult` (base) | Command, Status, Timestamp, Message | No |
| `GenerateResult` | suite, analysis, generation, suggestions | No |
| `DocsIndexResult` | documents_indexed/skipped/new/total, criteria_extracted | No |
| `ExtractCriteriaResult` | documents_processed/skipped, criteria_new/updated/unchanged | No |
| `AnalyzeCoverageResult` | documentation, **requirements**, automation | **Yes — rename `requirements` → `acceptanceCriteria`** |
| `ValidateResult` | total_files, valid, errors | No |
| `DashboardResult` | output_path, pages_generated, suites/tests/runs_included | No |
| `ImportCriteriaResult` | imported, split, normalized, merged | No |
| `ListCriteriaResult` | criteria, total, covered, coverage_pct | No |

### RQ-4: Is the dashboard JavaScript broken?

**Decision**: Dashboard JavaScript is NOT broken — it already uses correct field names. The spec overestimated the damage.

**Findings**:
- `app.js` references `coverage_summary.acceptance_criteria` (snake_case) — correct
- `coverage-map.js` references `coverageSummary.automation.details` — correct
- Section labels rendered dynamically: "Documentation Coverage", "Acceptance Criteria Coverage", "Automation Coverage" — all correct
- Empty state handling exists: checks `has_criteria_file` flag, shows guidance messages
- No references to old "requirement" or "requirementsCoverage" field names found anywhere in dashboard JS/HTML

**Impact on plan**: Part E of the spec (Dashboard Coverage Fix) is largely already done. Only need to verify DataCollector null safety and possibly the `AnalyzeCoverageResult` field name mismatch when outputting JSON for SKILLs (not dashboard).

### RQ-5: Does spectra-docs SKILL already exist?

**Decision**: Yes, it was already created in spec 024.

**Findings**:
- `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md` exists (66 lines)
- `SkillContent.cs` has `DocsIndex` property that loads it
- `SkillsManifest.cs` tracks its hash
- The SKILL already includes progress page flow pattern

**Impact on plan**: Story 6 (New spectra-docs SKILL) is already complete. Only SKILL updates for other commands remain.

### RQ-6: What "requirement" strings remain in user-facing code?

**Decision**: Very few remain — the rename is nearly complete from spec 023/024.

**Findings**:
- `AnalyzeCoverageResult.cs`: `requirements` field name (JSON property)
- `AnalyzeCommand.cs`: `--extract-requirements` hidden alias (intentionally kept for backward compat)
- `CoverageReportWriter.cs`: One message references `docs/requirements/` path (this is the actual directory path, not terminology)
- `ExtractRequirementsResult.cs`: Legacy result class (may still be used by `--extract-requirements` alias)

**What to fix**: Only `AnalyzeCoverageResult.requirements` → `AnalyzeCoverageResult.acceptanceCriteria` (with `[JsonPropertyName]` for JSON output). The `--extract-requirements` alias and directory path are intentional.

### RQ-7: What is the DataCollector null-safety status?

**Decision**: DataCollector already has robust null-safety. Minor gaps only.

**Findings** (`src/Spectra.CLI/Dashboard/DataCollector.cs`):
- `BuildCoverageSummaryAsync()` catches all exceptions and returns zero-state CoverageSummaryData
- Zero-state has all counts at 0, all detail lists as empty arrays
- `AcceptanceCriteriaSectionData` includes `HasCriteriaFile = false` in zero-state
- `CoverageSummaryData` in `DashboardData` is nullable — dashboard JS handles `null` with fallback computation

**Remaining gap**: If `BuildCoverageSummaryAsync()` succeeds but one section's analyzer returns null internally, the individual section could be null. Should add null-coalescing at the section level.

## Alternatives Considered

### Alternative: Per-handler inline progress code (rejected)
Each handler writes its own progress/result files independently (current pattern for Generate + DocsIndex).
**Rejected because**: Already causes ~130 lines of duplicated code per handler. Adding 4 more handlers this way means 500+ lines of duplicate code with inevitable drift.

### Alternative: Event-based progress system (rejected)
Handlers publish progress events, a central subscriber writes files.
**Rejected because**: Over-engineered for the use case. SPECTRA is CLI-first; simple method calls on a shared service are sufficient. Event systems add debugging complexity.

### Alternative: Skip progress pages for medium-duration commands (considered)
Only add progress pages for commands taking >30 seconds (generate, update, extract-criteria). Skip for coverage analysis and dashboard.
**Rejected because**: The marginal cost of adding progress support via ProgressManager is near-zero once the infrastructure exists. Better to be consistent.
