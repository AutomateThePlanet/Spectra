# Implementation Plan: Coverage Analysis & Dashboard Visualizations

**Branch**: `009-coverage-dashboard-viz` | **Date**: 2026-03-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-coverage-dashboard-viz/spec.md`

## Summary

Three-type coverage analysis (documentation, requirements, automation) with `--auto-link`, unified reporting, and four dashboard visualizations (progress bars with drill-down, donut chart, treemap, empty states).

**Critical finding**: Research reveals that P1 (core analysis), P2 (auto-link), and P7 (init config) are **already fully implemented** in the codebase. The remaining work is P3/P6 (dashboard progress bar enhancement), P4 (donut chart), and P5 (treemap). This plan focuses exclusively on dashboard visualization work.

## Technical Context

**Language/Version**: C# 12 / .NET 8+ (backend), JavaScript ES2020+ (dashboard)
**Primary Dependencies**: D3.js v7 (CDN, treemap), custom SVG (donut chart), vanilla JS (progress bars)
**Storage**: N/A — dashboard reads embedded JSON from `<script id="dashboard-data">`
**Testing**: xUnit (C# data collector), manual browser testing (JS visualizations)
**Target Platform**: Static HTML dashboard served locally or via Cloudflare Pages
**Project Type**: CLI tool + static dashboard site
**Performance Goals**: Dashboard renders within 1 second of page load (SC-004)
**Constraints**: No React/framework dependencies; D3.js is the only allowed external JS library
**Scale/Scope**: Up to 500 tests across 20 suites displayed in dashboard

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Coverage data flows from Git-stored files through CLI into dashboard |
| II. Deterministic Execution | PASS | Dashboard renders deterministically from embedded JSON data |
| III. Orchestrator-Agnostic | N/A | Dashboard is not an orchestrator integration |
| IV. CLI-First Interface | PASS | `spectra dashboard` generates the static site; visualizations enhance the output |
| V. Simplicity (YAGNI) | PASS | Reusing D3.js (already a dependency) and custom SVG (existing pattern). No new framework dependencies. |

## Project Structure

### Documentation (this feature)

```text
specs/009-coverage-dashboard-viz/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 quickstart
├── contracts/           # Phase 1 contracts
│   └── coverage-data.md # Dashboard JSON data contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Dashboard/
│       └── CoverageSummaryData.cs    # Update: typed detail models (replace object)
└── Spectra.CLI/
    └── Dashboard/
        └── DataCollector.cs          # Update: populate detail lists for all 3 sections

dashboard-site/
├── scripts/
│   ├── app.js                        # Update: expand/collapse, empty states, donut chart
│   └── coverage-map.js               # Update: add treemap visualization
└── styles/
    └── main.css                       # Update: donut, treemap, expand/collapse styles

tests/
└── Spectra.CLI.Tests/
    └── Dashboard/
        └── DataCollectorTests.cs      # Update: verify detail data population
```

**Structure Decision**: All changes are modifications to existing files. No new projects or major restructuring. The dashboard visualizations use the existing vanilla JS + D3 + custom SVG patterns.

## Implementation Phases

### Phase A: Backend — Detail Data Population (P3 prerequisite)

**Goal**: Ensure `CoverageSummaryData` detail lists contain typed data for all three sections.

**Current state**: `CoverageSectionData.Details` is `IReadOnlyList<object>?` — needs typed detail models so the dashboard JS can reliably read properties.

**Changes**:

1. **`CoverageSummaryData.cs`** — Replace `IReadOnlyList<object>?` with typed detail models:
   - `DocumentationCoverageDetail`: `doc`, `test_count`, `covered`, `test_ids`
   - `RequirementCoverageDetail`: `id`, `title`, `tests`, `covered`
   - `AutomationSuiteDetail`: `suite`, `total`, `automated`, `percentage`
   - Keep separate detail list properties on each section rather than generic `Details`

2. **`DataCollector.cs`** — Populate detail lists in `BuildCoverageSummaryAsync()`:
   - Documentation: for each doc in docs/, include path, test count, coverage status
   - Requirements: from `RequirementsCoverageAnalyzer` results, include id, title, test IDs, coverage
   - Automation: per-suite breakdown from test index data

3. **Tests** — Verify `DataCollector` populates all three detail lists correctly.

### Phase B: Dashboard — Progress Bar Drill-Down (P3 + P6)

**Goal**: Enhance three-section progress bars with expandable detail lists and empty state guidance.

**Changes to `app.js`**:

1. **Expand/collapse detail lists**:
   - Detail lists start collapsed (hidden)
   - "Show details" / "Hide details" toggle button below progress bar
   - CSS transition for smooth expand animation
   - Detail items show: name, count, covered/uncovered icon

2. **Empty state messages** (when total === 0 or no data configured):
   - Documentation: "All documents have test coverage!" (when 100%) or standard progress
   - Requirements: "No requirements tracked yet. Add a `requirements` field to test YAML frontmatter or create `docs/requirements/_requirements.yaml`"
   - Automation: "No automation links detected. Run `spectra ai analyze --coverage --auto-link` to scan automation code."

3. **CSS additions** in `main.css`:
   - `.coverage-toggle-btn` — expand/collapse button styling
   - `.coverage-detail-list.collapsed` — hidden state
   - Transition animation for detail list expansion

### Phase C: Dashboard — Donut Chart (P4)

**Goal**: Add a donut chart showing overall test distribution above the progress bars.

**Implementation** (custom SVG in `app.js`, matching existing trend chart pattern):

1. **Data calculation**:
   - Automated: tests with `automated_by` (green)
   - Manual-only: tests with `source_refs` but no `automated_by` (yellow)
   - Unlinked: tests with neither (red)
   - Source: derive from `data.tests` array or `data.coverage_summary`

2. **SVG donut chart**:
   - SVG circle with `stroke-dasharray` / `stroke-dashoffset` for segments
   - Center text: total test count
   - Hover tooltips showing count + percentage per segment
   - Legend below chart: colored dots + labels

3. **Placement**: At top of coverage tab, before progress bars.

### Phase D: Dashboard — Treemap (P5)

**Goal**: Add a D3.js treemap visualization showing suites sized by test count, colored by automation %.

**Implementation** (in `coverage-map.js`, extending existing D3 usage):

1. **Data preparation**:
   - Build hierarchy: root → suites (from `data.suites`)
   - Each suite: name, test count, automation percentage
   - Automation % derived from `data.coverage_summary.automation.details` (suite-level)

2. **D3 treemap layout**:
   - `d3.treemap()` with squarified tiling
   - Block size = test count
   - Block color: green (>= 50% automated), yellow (> 0% but < 50%), red (0%)
   - Labels: suite name, test count, automation %

3. **Interactivity**:
   - Click block → filter tests view to that suite (reuse existing `showSuiteTests()`)
   - Hover → tooltip with suite name, total tests, automated count, percentage

4. **Placement**: Below progress bars, above existing coverage map.

## Complexity Tracking

No constitution violations. All changes extend existing patterns (vanilla JS + D3 + custom SVG).
