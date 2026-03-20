# Quickstart: Coverage Dashboard Visualizations

**Feature**: 009-coverage-dashboard-viz | **Date**: 2026-03-20

## What This Feature Adds

Enhanced dashboard coverage visualizations:
1. **Progress bars with drill-down** — expandable detail lists for each coverage type
2. **Empty state guidance** — helpful messages when coverage is unconfigured
3. **Donut chart** — overall test health distribution (automated/manual/unlinked)
4. **Treemap** — suites sized by test count, colored by automation coverage

## Prerequisites

Coverage analysis is already implemented. To see data in the dashboard:

```bash
# Generate coverage data
spectra ai analyze --coverage

# Generate dashboard
spectra dashboard --output ./site

# Serve locally
cd site && python -m http.server 8080
```

## Verification

### Progress Bars with Drill-Down

1. Open dashboard → Coverage tab
2. Three stacked cards: Documentation, Requirements, Automation
3. Click "Show details" on any card → detail list expands with per-item breakdown
4. Color coding: green >= 80%, yellow >= 50%, red < 50%

### Empty States

1. Generate dashboard with no requirements configured
2. Requirements card shows guidance: "No requirements tracked yet..." with setup instructions
3. If no automation links, automation card shows: "No automation links detected..." with `--auto-link` instructions

### Donut Chart

1. Open dashboard → Coverage tab
2. Donut chart at top shows: green (automated), yellow (manual-only), red (unlinked)
3. Center shows total test count
4. Hover segments for tooltips

### Treemap

1. Open dashboard → Coverage tab
2. Below progress bars, treemap shows suites as colored blocks
3. Block size = test count, color = automation %
4. Click a block → navigates to that suite's test list

## Key Files

| File | Changes |
|------|---------|
| `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs` | Typed detail models |
| `src/Spectra.CLI/Dashboard/DataCollector.cs` | Populate detail lists |
| `dashboard-site/scripts/app.js` | Expand/collapse, empty states, donut chart |
| `dashboard-site/scripts/coverage-map.js` | Treemap visualization |
| `dashboard-site/styles/main.css` | New CSS for donut, treemap, toggle |
