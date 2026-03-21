# Implementation Plan: Dashboard Branding & Theming

**Branch**: `012-dashboard-branding` | **Date**: 2026-03-21 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/012-dashboard-branding/spec.md`

## Summary

Add dashboard branding and theming customization via `spectra.config.json`. Users configure company name, logo, favicon, color scheme, light/dark theme, and custom CSS. The dashboard generator injects branding configuration into the HTML template at build time, and client-side JavaScript applies the settings via CSS custom properties. A `--preview` flag enables fast branding verification with sample data.

**Key insight**: The existing CSS already uses CSS custom properties (`:root` variables) for all colors, so theme switching and brand color overrides can be implemented by generating a `:root` override block — no CSS rewrite needed.

## Technical Context

**Language/Version**: C# 12 / .NET 8+ (backend), JavaScript ES2020+ (dashboard)
**Primary Dependencies**: System.Text.Json (config), vanilla JS + CSS custom properties (theming)
**Storage**: N/A — branding config lives in `spectra.config.json`, assets copied to output directory
**Testing**: xUnit (C# config/generator), manual browser testing (visual theming)
**Target Platform**: Static HTML dashboard served locally or via Cloudflare Pages
**Project Type**: CLI tool + static dashboard site
**Performance Goals**: <500ms branding overhead on dashboard generation (SC-004)
**Constraints**: No new JS framework dependencies; CSS-only theming via custom properties
**Scale/Scope**: Single dashboard output; branding affects header, sidebar, cards, charts

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Branding config is in `spectra.config.json` (Git-tracked). Assets referenced by path. |
| II. Deterministic Execution | PASS | Same config + same assets = same dashboard output. No runtime state. |
| III. Orchestrator-Agnostic | N/A | Dashboard branding is not an orchestrator integration. |
| IV. CLI-First Interface | PASS | `spectra dashboard` generates branded output. `--preview` flag for verification. |
| V. Simplicity (YAGNI) | PASS | Leverages existing CSS custom properties. Two theme presets only (light/dark). No image processing. No framework additions. |

## Project Structure

### Documentation (this feature)

```text
specs/012-dashboard-branding/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Phase 1 data model
├── quickstart.md        # Phase 1 quickstart
├── contracts/           # Phase 1 contracts
│   └── branding-config.md # Config schema contract
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Config/
│       ├── DashboardConfig.cs         # Update: add BrandingConfig property
│       ├── BrandingConfig.cs          # New: branding configuration model
│       └── ColorPaletteConfig.cs      # New: custom color overrides model
└── Spectra.CLI/
    ├── Commands/Dashboard/
    │   └── DashboardCommand.cs        # Update: add --preview flag
    └── Dashboard/
        ├── DashboardGenerator.cs      # Update: inject branding into template
        ├── BrandingInjector.cs         # New: branding → CSS/HTML injection logic
        └── SampleDataFactory.cs       # New: generate mock data for --preview

dashboard-site/
├── index.html                         # Update: add branding placeholders
├── scripts/
│   └── app.js                         # Update: apply branding from embedded config
└── styles/
    ├── main.css                       # Update: ensure all colors use CSS vars
    └── dark-theme.css                 # New: dark theme variable overrides

tests/
├── Spectra.Core.Tests/
│   └── Config/
│       └── BrandingConfigTests.cs     # New: config deserialization tests
└── Spectra.CLI.Tests/
    └── Dashboard/
        ├── BrandingInjectorTests.cs   # New: injection logic tests
        ├── SampleDataFactoryTests.cs  # New: preview data tests
        └── DashboardGeneratorTests.cs # Update: branding integration tests
```

**Structure Decision**: Follows existing pattern — new models in `Spectra.Core/Models/Config/`, new services in `Spectra.CLI/Dashboard/`, new tests alongside existing test files. No new projects. The `dark-theme.css` is a separate file to keep light/dark concerns isolated.

## Implementation Phases

### Phase A: Config Model — BrandingConfig (FR-001, FR-009)

**Goal**: Define the branding configuration data model and add it to DashboardConfig.

**Changes**:

1. **`BrandingConfig.cs`** — New record with properties:
   - `company_name` (string?) — replaces "SPECTRA Dashboard" header text
   - `logo` (string?) — path to logo image file
   - `favicon` (string?) — path to favicon file
   - `theme` (string?) — "light" (default) or "dark"
   - `colors` (ColorPaletteConfig?) — custom color overrides
   - `custom_css` (string?) — path to custom CSS file

2. **`ColorPaletteConfig.cs`** — New record with properties:
   - `primary`, `accent`, `background`, `text`, `surface`, `border` — all string? (CSS color values)

3. **`DashboardConfig.cs`** — Add `BrandingConfig? Branding` property (default null for backward compat).

4. **Tests** — Deserialization roundtrip, null/default handling, backward compatibility.

### Phase B: Branding Injection — BrandingInjector (FR-002 through FR-008, FR-010, FR-012, FR-013)

**Goal**: Build the service that transforms branding config into HTML/CSS modifications.

**Changes**:

1. **`BrandingInjector.cs`** — New service with methods:
   - `InjectBranding(string html, BrandingConfig? config, string configDir)` → string
   - Generates a `<style>` block with `:root` overrides from config colors
   - If theme is "dark", includes dark theme CSS variable overrides
   - Replaces `{{COMPANY_NAME}}` placeholder in HTML (or `SPECTRA Dashboard` text)
   - Adds `<link rel="icon" href="favicon.ico">` if favicon configured
   - Adds `<img>` logo element in header if logo configured
   - Appends `<link rel="stylesheet" href="custom.css">` if custom_css configured
   - Embeds branding config as `<script id="branding-config" type="application/json">`
   - Validates file paths (warns on missing, continues without failing)
   - Resolves relative paths from configDir

2. **Asset copying** in `DashboardGenerator`:
   - Copy logo file to output if configured and exists
   - Copy favicon to output if configured and exists
   - Copy custom CSS to output if configured and exists

3. **`dark-theme.css`** — CSS custom property overrides for dark mode:
   - Dark background (`#0f172a`), light text (`#e2e8f0`)
   - Dark card bg (`#1e293b`), dark border (`#334155`)
   - Adjusted shadow values, badge colors for dark backgrounds

4. **Tests** — Injection with full config, partial config, null config, invalid paths, dark theme.

### Phase C: Dashboard Template Updates (FR-002, FR-003, FR-004, FR-005, FR-006)

**Goal**: Update the HTML template and JavaScript to support branding.

**Changes**:

1. **`index.html`** (dashboard-site):
   - Add `{{FAVICON_LINK}}` placeholder in `<head>`
   - Add `{{LOGO_IMG}}` placeholder in header before `<h1>`
   - Change `<h1>SPECTRA Dashboard</h1>` to `<h1>{{COMPANY_NAME}}</h1>`
   - Add `{{BRANDING_STYLES}}` placeholder before `</head>` for injected CSS
   - Add `{{CUSTOM_CSS_LINK}}` placeholder after default stylesheet link

2. **`app.js`**:
   - On DOMContentLoaded, read branding config from `#branding-config` script tag
   - Apply `dark` class to `<body>` if theme is dark
   - No further JS changes needed — CSS custom properties handle the rest

3. **Default template in `DashboardGenerator.cs`**:
   - Mirror the same placeholder changes as dashboard-site/index.html
   - Ensure `GetDefaultCss()` dark-theme variables match `dark-theme.css`

4. **`main.css`** — Verify all hardcoded colors use CSS variables (audit and fix any that don't).

### Phase D: Preview Mode — SampleDataFactory (FR-011)

**Goal**: Add `--preview` flag to dashboard command for fast branding verification.

**Changes**:

1. **`SampleDataFactory.cs`** — New static class:
   - `CreateSampleData()` → `DashboardData` with:
     - 3 sample suites ("Checkout", "Authentication", "Search")
     - 10 sample tests with varied priorities, tags, components
     - 2 sample runs with mixed pass/fail results
     - Sample coverage summary with all three sections populated
     - Sample trend data with 5 data points

2. **Dashboard command** — Add `--preview` option:
   - When set, use `SampleDataFactory.CreateSampleData()` instead of `DataCollector`
   - Still applies branding from config
   - Outputs to the configured output directory

3. **Tests** — Verify sample data is valid and complete.

### Phase E: Validation & Edge Cases (FR-010)

**Goal**: Validate branding configuration and handle edge cases gracefully.

**Changes**:

1. **Validation in `BrandingInjector`**:
   - Validate `theme` is "light" or "dark" (warn + default to "light" otherwise)
   - Validate color values match CSS color pattern (hex, rgb, hsl, named)
   - Validate file paths exist (warn if missing, don't fail)
   - Warn if logo file > 5 MB

2. **CLI output**:
   - Log which branding settings were applied
   - Log warnings for invalid/missing values
   - Log "Using default Spectra branding" when no branding configured

## Constitution Check — Post-Design

| Principle | Status | Notes |
|-----------|--------|-------|
| I. GitHub as Source of Truth | PASS | Config in spectra.config.json. Assets referenced by path. |
| II. Deterministic Execution | PASS | Same config = same output. |
| III. Orchestrator-Agnostic | N/A | Not applicable. |
| IV. CLI-First Interface | PASS | `spectra dashboard [--preview]`. CI-friendly. |
| V. Simplicity (YAGNI) | PASS | CSS custom properties, no framework. Two themes only. |

## Complexity Tracking

No constitution violations. All changes extend existing patterns:
- Config model follows `DashboardConfig` pattern
- CSS theming uses existing `:root` custom properties
- Template injection follows existing `{{DASHBOARD_DATA}}` placeholder pattern
- Tests follow existing xUnit patterns
