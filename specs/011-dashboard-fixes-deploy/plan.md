# Implementation Plan: Dashboard Improvements and Cloudflare Pages Deployment

**Branch**: `011-dashboard-fixes-deploy` | **Date**: 2026-03-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/011-dashboard-fixes-deploy/spec.md`

## Summary

Fix documentation coverage bug (fragment anchor stripping in path normalization), repair dashboard visualizations (trend chart sizing, treemap data source, filter sidebar visibility), replace the coverage relationships view with a D3.js hierarchical tree, and add Cloudflare Pages deployment with GitHub OAuth authentication. Extends existing DashboardConfig with `cloudflare_project_name`, updates OAuth middleware to support multiple repos and 24-hour sessions, and adds deployment scaffolding to `spectra init`.

## Technical Context

**Language/Version**: C# 12, .NET 8+ (backend); JavaScript ES2020+ (dashboard frontend)
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json (CLI); D3.js v7 (dashboard); Cloudflare Pages Functions (OAuth)
**Storage**: File system for dashboard output; no new storage
**Testing**: xUnit (C# tests); manual visual verification (dashboard UI)
**Target Platform**: Windows/Linux/macOS (CLI); modern browsers (dashboard); Cloudflare Pages edge (hosting)
**Project Type**: CLI tool + static site generator + serverless middleware
**Performance Goals**: Dashboard renders in <2s for projects with up to 500 tests
**Constraints**: No new npm dependencies; D3.js already included via CDN
**Scale/Scope**: Dashboard handles up to 50 suites, 500 tests, 100 docs, 30 trend points

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | Pass | All config, workflow, and test files stored in Git. Dashboard is generated from Git-stored data. |
| II. Deterministic Execution | Pass | Coverage analysis and dashboard generation are deterministic given same inputs. No state machine changes. |
| III. Orchestrator-Agnostic Design | Pass | No MCP changes. Dashboard is a static site independent of any LLM orchestrator. |
| IV. CLI-First Interface | Pass | All functionality exposed via existing CLI commands (`spectra dashboard`, `spectra ai analyze`). Dashboard config read from `spectra.config.json`. |
| V. Simplicity (YAGNI) | Pass | Extends existing models and code. No new abstractions beyond a shared path normalizer utility. Coverage tree enhances existing implementation. |

**Post-Phase 1 Re-check**: All gates still pass. The `cloudflare_project_name` field is a single string addition to an existing model. The OAuth middleware changes are minimal (session duration constant + comma-split for ALLOWED_REPOS). The coverage tree enhancement builds on existing hierarchical tree code.

## Project Structure

### Documentation (this feature)

```text
specs/011-dashboard-fixes-deploy/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Entity changes
├── quickstart.md        # Development quickstart
├── contracts/
│   ├── dashboard-config.md   # Config schema contract
│   ├── oauth-middleware.md    # OAuth flow contract
│   └── github-workflow.md     # CI/CD workflow contract
└── checklists/
    └── requirements.md        # Spec quality checklist
```

### Source Code (repository root)

```text
src/
├── Spectra.CLI/
│   ├── Commands/Init/InitHandler.cs          # Add workflow file creation
│   ├── Dashboard/DataCollector.cs            # Fix path normalization bug
│   ├── Coverage/GapAnalyzer.cs               # Add fragment stripping
│   └── Templates/
│       ├── spectra.config.json               # Add cloudflare_project_name default
│       └── deploy-dashboard.yml              # New workflow template
├── Spectra.Core/
│   └── Models/Config/DashboardConfig.cs      # Add cloudflare_project_name field

dashboard-site/
├── scripts/
│   ├── app.js                                # Trend chart, sidebar, coverage tree
│   └── coverage-map.js                       # Treemap data source fix
├── styles/main.css                           # Styling consistency
├── functions/
│   └── _middleware.js                        # ALLOWED_REPOS, 24h sessions

docs/
└── deployment/
    └── cloudflare-pages-setup.md             # New deployment guide

.github/
└── workflows/
    └── deploy-dashboard.yml                  # New workflow (created by init)

tests/
├── Spectra.Core.Tests/Coverage/              # Path normalizer tests
└── Spectra.CLI.Tests/
    ├── Dashboard/DataCollectorTests.cs        # Fragment matching tests
    └── Coverage/GapAnalyzerTests.cs           # Fragment in gap analysis tests
```

**Structure Decision**: Extends existing project structure. No new projects or major directories. The only new directories are `docs/deployment/` for the setup guide and `src/Spectra.CLI/Templates/` gets one new YAML template file.

## Complexity Tracking

No constitution violations. No complexity justifications needed.
