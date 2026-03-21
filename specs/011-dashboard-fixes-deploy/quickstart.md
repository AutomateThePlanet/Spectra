# Quickstart: Dashboard Improvements and Cloudflare Pages Deployment

**Date**: 2026-03-21 | **Branch**: `011-dashboard-fixes-deploy`

## Prerequisites

- .NET 8+ SDK
- Node.js (for local dashboard preview)
- Git

## Build & Test

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run specific test projects
dotnet test tests/Spectra.Core.Tests
dotnet test tests/Spectra.CLI.Tests
```

## Verify Coverage Fix

```bash
# Run coverage analysis — should show non-zero documentation coverage
# when test files have source_refs with fragment anchors
dotnet run --project src/Spectra.CLI -- ai analyze --coverage
```

## Generate Dashboard Locally

```bash
# Generate dashboard to ./site
dotnet run --project src/Spectra.CLI -- dashboard --output ./site

# Preview: open site/index.html in a browser
```

## Key Files to Modify

### Coverage Bug Fix (P1)
- `src/Spectra.CLI/Dashboard/DataCollector.cs` — fix path normalization in BuildCoverageSummaryAsync
- `src/Spectra.CLI/Coverage/GapAnalyzer.cs` — add fragment stripping to NormalizePath

### Dashboard UI Fixes (P1)
- `dashboard-site/scripts/app.js` — trend chart, sidebar, coverage tree
- `dashboard-site/scripts/coverage-map.js` — treemap data source fix
- `dashboard-site/styles/main.css` — styling consistency

### Deployment (P2)
- `.github/workflows/deploy-dashboard.yml` — CI/CD workflow (new)
- `dashboard-site/functions/_middleware.js` — OAuth updates
- `docs/deployment/cloudflare-pages-setup.md` — setup guide (new)

### Config & Init (P3)
- `src/Spectra.Core/Models/Config/DashboardConfig.cs` — add cloudflare_project_name
- `src/Spectra.CLI/Templates/spectra.config.json` — default config template
- `src/Spectra.CLI/Commands/Init/InitHandler.cs` — add workflow file creation
