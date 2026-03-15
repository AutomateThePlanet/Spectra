# Implementation Plan: Dashboard and Coverage Analysis

**Branch**: `003-dashboard-coverage-analysis` | **Date**: 2026-03-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-dashboard-coverage-analysis/spec.md`

## Summary

Build a static dashboard generator and coverage analysis system for SPECTRA Phase 3. The dashboard command generates a browsable HTML site from test indexes and execution reports. The coverage analysis command scans manual tests and automation code to identify linkage gaps. Both integrate with existing Spectra.CLI infrastructure.

## Technical Context

**Language/Version**: C# 12, .NET 8+ (CLI and coverage analysis); HTML/CSS/JS (dashboard output)
**Primary Dependencies**: Spectra.Core (parsing, indexes), Spectra.CLI (command integration), System.Text.Json, Microsoft.Data.Sqlite (reading .execution DB)
**Storage**: Reads from `tests/*/_index.json`, `reports/*.json`, `.execution/spectra.db`; Writes to output directory (static files)
**Testing**: xUnit with test fixtures
**Target Platform**: Cross-platform CLI (.NET 8), static HTML output viewable in any modern browser
**Project Type**: CLI extension + static site generator
**Performance Goals**: Dashboard generation <30s for 500 tests; Coverage analysis <60s for 1000 tests + 10k automation files
**Constraints**: Static output only (no server runtime); optional auth via serverless functions
**Scale/Scope**: Up to 500 tests, 50 suites, 100 execution reports typical; must handle 1000+ tests gracefully

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Dashboard reads from committed test files and indexes |
| II. Deterministic Execution | PASS | Static generation produces same output for same inputs |
| III. Orchestrator-Agnostic Design | PASS | Dashboard is pure static HTML, no LLM dependency |
| IV. CLI-First Interface | PASS | `spectra dashboard` and `spectra ai analyze --coverage` commands |
| V. Simplicity (YAGNI) | PASS | Single-project extension to Spectra.CLI; no new assemblies needed for core features |

**Quality Gates Compliance**:
- Dashboard generation validates indexes exist before proceeding
- Coverage analysis integrates with existing `spectra validate` patterns
- All commands will support `--dry-run` for CI compatibility

## Project Structure

### Documentation (this feature)

```text
specs/003-dashboard-coverage-analysis/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (dashboard data format)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   ├── Models/
│   │   ├── Coverage/           # NEW: Coverage analysis models
│   │   │   ├── CoverageLink.cs
│   │   │   ├── CoverageReport.cs
│   │   │   └── LinkStatus.cs
│   │   └── Dashboard/          # NEW: Dashboard data models
│   │       ├── DashboardData.cs
│   │       ├── SuiteStats.cs
│   │       └── RunSummary.cs
│   └── Coverage/               # NEW: Coverage analysis logic
│       ├── AutomationScanner.cs
│       ├── LinkReconciler.cs
│       └── CoverageCalculator.cs
├── Spectra.CLI/
│   ├── Commands/
│   │   ├── DashboardCommand.cs     # NEW: spectra dashboard
│   │   └── AnalyzeCommand.cs       # MODIFY: add --coverage flag
│   ├── Dashboard/              # NEW: Dashboard generation
│   │   ├── DashboardGenerator.cs
│   │   ├── DataCollector.cs
│   │   ├── HtmlRenderer.cs
│   │   └── Templates/          # Embedded HTML/CSS/JS templates
│   └── Coverage/               # NEW: CLI coverage output
│       └── CoverageReportWriter.cs
└── Spectra.MCP/                # Existing (no changes)

dashboard-site/                 # NEW: Dashboard frontend assets
├── index.html                  # Template
├── styles/
│   └── main.css
├── scripts/
│   └── app.js                  # Filtering, search, visualization
└── functions/                  # Optional: Cloudflare Pages auth
    └── _middleware.js

tests/
├── Spectra.Core.Tests/
│   └── Coverage/               # NEW: Coverage analysis tests
└── Spectra.CLI.Tests/
    └── Dashboard/              # NEW: Dashboard generation tests
```

**Structure Decision**: Extend existing Spectra.CLI and Spectra.Core projects. Dashboard templates are embedded resources or copied from `dashboard-site/` during build. No new solution projects required for P1 features. Authentication (P2) uses external Cloudflare Pages Functions if needed.

## Complexity Tracking

> No Constitution violations identified. All features fit within existing project structure.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| N/A | - | - |
