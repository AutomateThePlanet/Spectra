# Implementation Plan: SPECTRA Branding & Design System

**Branch**: `012-dashboard-branding` | **Date**: 2026-03-21 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/012-dashboard-branding/spec.md`

## Summary

Apply SPECTRA brand identity and a cohesive design system to the dashboard and GitHub README. The dashboard currently uses a generic blue color scheme with DM Sans/IBM Plex Sans fonts. This feature replaces that with the SPECTRA brand palette (navy/beige/spectral accents), Inter typography, logo-branded navigation, favicon, and polished component styles (cards, badges, tables, sidebar). The GitHub README gets a centered banner image. All changes are to static template files (HTML/CSS) and the C# dashboard generator's asset-copying logic — no data model or JavaScript logic changes required.

## Technical Context

**Language/Version**: C# 12 / .NET 8+ (dashboard generator), HTML/CSS/JS (dashboard template)
**Primary Dependencies**: Spectre.Console (CLI output), System.Text.Json (serialization), Google Fonts CDN (Inter), D3.js (treemap visualization)
**Storage**: N/A (static file generation)
**Testing**: xUnit (C# tests for DashboardGenerator, DataCollector)
**Target Platform**: Cross-platform CLI generating static HTML dashboard viewable in modern browsers
**Project Type**: CLI tool generating static web output
**Performance Goals**: Dashboard generation time increase < 10% from asset copying
**Constraints**: Offline-capable with system font fallbacks; no build step for dashboard output
**Scale/Scope**: 3 template files modified (HTML, CSS, JS class names), 1 C# file updated (asset copying), 1 README updated, 3 brand assets copied

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Brand assets stored in `assets/` in Git. Template files in `dashboard-site/` in Git. |
| II. Deterministic Execution | PASS | No execution engine changes. Dashboard generation remains deterministic. |
| III. Orchestrator-Agnostic Design | PASS | No MCP or orchestrator changes. |
| IV. CLI-First Interface | PASS | Dashboard command unchanged. No new CLI flags needed. |
| V. Simplicity (YAGNI) | PASS | Pure restyling — no new abstractions, no new projects, no new dependencies beyond Google Fonts CDN (with fallbacks). |

**Quality Gates**: No test file or schema changes — existing gates unaffected.

**Post-Phase 1 Re-check**: PASS — no structural changes introduced.

## Project Structure

### Documentation (this feature)

```text
specs/012-dashboard-branding/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
assets/                              # Brand assets (already exist)
├── spectra_dashboard_banner.png     # Nav bar logo (navy bg, white text)
├── spectra_favicon.png              # Browser favicon (spectral eye)
└── spectra_github_readme_banner.png # README banner (beige bg, navy text)

dashboard-site/                      # Static dashboard template (MODIFIED)
├── index.html                       # Add favicon link, update nav bar logo
├── styles/
│   └── main.css                     # Replace color scheme, typography, component styles
├── scripts/
│   ├── app.js                       # Update CSS class references for new component styles
│   └── coverage-map.js              # Update treemap colors to use design tokens
└── assets/                          # NEW: copied brand assets for generated sites
    ├── spectra_dashboard_banner.png
    └── spectra_favicon.png

src/Spectra.CLI/Dashboard/
└── DashboardGenerator.cs            # MODIFIED: copy assets/ to output, update defaults

tests/Spectra.CLI.Tests/Dashboard/
└── DashboardGeneratorTests.cs       # MODIFIED: verify asset copying

README.md                            # MODIFIED: add banner image at top
```

**Structure Decision**: No new projects or directories beyond adding `dashboard-site/assets/` for the brand images that ship with every generated site. The generator's `CopyStaticAssetsAsync()` already recursively copies subdirectories from the template, so placing assets in `dashboard-site/assets/` means they automatically appear in output.

## Complexity Tracking

> No constitution violations. No complexity tracking needed.
