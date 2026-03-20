# Research: Coverage Analysis & Dashboard Visualizations

**Date**: 2026-03-20 | **Feature**: 009-coverage-dashboard-viz

## Finding 1: Core Coverage Analysis Already Implemented

**Decision**: Skip P1 (Unified Coverage Analysis), P2 (Auto-Link), and P7 (Init Coverage Config) — they are fully implemented.

**Rationale**: Comprehensive codebase exploration confirmed all three coverage types, auto-link with frontmatter writing, and init bootstrapping are complete:

- `DocumentationCoverageAnalyzer`, `RequirementsCoverageAnalyzer`, `AutomationScanner` + `LinkReconciler` + `CoverageCalculator` — all in `src/Spectra.Core/Coverage/`
- `UnifiedCoverageReport` model with three sections — in `src/Spectra.Core/Models/Coverage/`
- `AnalyzeHandler` orchestrates all three analyses with `--coverage` and `--auto-link` flags
- `AutoLinkService` + `FrontmatterUpdater` handle auto-link write path
- `CoverageConfig` has `scan_patterns`, `file_extensions`, `requirements_file`
- `TestCase` has `AutomatedBy` and `Requirements` fields
- `InitHandler` creates `_requirements.yaml` template
- 1630+ lines of coverage tests across 9 test files

**Alternatives considered**: Reimplementing from scratch — rejected, existing implementation matches spec requirements.

## Finding 2: Dashboard Three-Section Progress Bars Partially Implemented

**Decision**: The current `renderThreeSectionCoverage()` in `app.js` renders basic progress bars. Needs enhancement for: expandable detail lists, animation, and empty state guidance.

**Rationale**: Current implementation shows three stacked `.coverage-section` containers with progress bars and percentage labels, but:
- Detail lists are flat (no expand/collapse)
- No empty state guidance messages
- Color coding exists (green/yellow/red) but needs threshold verification

**Alternatives considered**: Full rewrite — rejected, extend existing implementation.

## Finding 3: Dashboard is Vanilla JS + D3 (NOT React)

**Decision**: All new dashboard visualizations must use vanilla JavaScript + D3.js + custom SVG. Recharts CANNOT be used (it's a React-only library and the dashboard is plain HTML+JS).

**Rationale**: Dashboard is static HTML with `{{DASHBOARD_DATA}}` placeholder. JavaScript files: `app.js` (main) and `coverage-map.js` (D3 force graph). Only external dependency is D3.js v7 via CDN.

**Alternatives considered**:
- Recharts — rejected (requires React, would mean rewriting entire dashboard)
- Chart.js — rejected (adds another CDN dependency; D3 already available)
- Custom SVG — chosen for donut chart (consistent with existing trend chart approach)
- D3.js — chosen for treemap (already a dependency, purpose-built for this)

## Finding 4: Coverage Data Flow to Dashboard

**Decision**: Coverage data flows through `DataCollector.BuildCoverageSummaryAsync()` → `DashboardData.CoverageSummary` → embedded JSON in HTML → client-side JS rendering.

**Rationale**: `CoverageSummaryData` has three `CoverageSectionData` properties (documentation, requirements, automation). Each has `covered`, `total`, `percentage`, and optional `details`. The detail list data structure needs to be populated by `DataCollector` for the expandable detail lists to work.

**Alternatives considered**: Separate coverage.json file — rejected (dashboard reads single embedded JSON payload).

## Finding 5: Auto-Link Replace Behavior (Clarification)

**Decision**: Auto-link replaces `automated_by` with scan results (authoritative source). Current implementation in `AutoLinkService.GenerateLinks()` generates the full set of links; `FrontmatterUpdater.UpdateAutomatedBy()` replaces the field.

**Rationale**: From spec clarification session. Current `FrontmatterUpdater` already uses replacement behavior (regex replace of existing value or insertion of new field). Matches spec requirement.

**Alternatives considered**: Additive-only merge — rejected per clarification.
