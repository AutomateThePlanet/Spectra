# Quickstart: SPECTRA Branding & Design System

**Feature**: 012-dashboard-branding
**Date**: 2026-03-21

## Prerequisites

- .NET 8+ SDK installed
- Brand asset files present in `assets/` directory (already committed)
- Access to the `dashboard-site/` template directory

## Implementation Order

### Step 1: Copy brand assets into dashboard template

Copy `spectra_dashboard_banner.png` and `spectra_favicon.png` from `assets/` into `dashboard-site/assets/` so they're included in every generated site.

### Step 2: Update dashboard-site/index.html

- Add favicon `<link>` tag in `<head>`
- Replace the text title in the nav bar with an `<img>` tag for the dashboard banner
- Update Google Fonts import URL from DM Sans/IBM Plex Sans to Inter

### Step 3: Update dashboard-site/styles/main.css

- Replace `:root` CSS variables with new design tokens
- Update all component styles to use new variable names
- Add new component classes (status badges, priority badges, nav tabs)
- Update typography to Inter font family
- Add responsive layout rules (max-width 1400px, sidebar 240px)

### Step 4: Update dashboard-site/scripts/app.js

- Update CSS class names in generated HTML to match new design system
- Add status-colored left borders to test rows
- Update badge class references for status/priority
- Add automation percentage color coding and mini progress bar in sidebar

### Step 5: Update dashboard-site/scripts/coverage-map.js

- Update treemap colors to use design token values

### Step 6: Update DashboardGenerator.cs hardcoded defaults

- Update `GetDefaultTemplate()` with favicon link and logo image
- Update `GetDefaultCss()` with new design tokens and component styles
- Update `GetDefaultJs()` with new class references

### Step 7: Update README.md

- Add banner image markup at the top of the file

### Step 8: Update tests

- Add test verifying assets are copied to output directory
- Update any tests that assert on specific CSS content or HTML structure

## Verification

```bash
# Build
dotnet build

# Run tests
dotnet test

# Generate dashboard and inspect visually
dotnet run --project src/Spectra.CLI -- dashboard --output ./test-site

# Open ./test-site/index.html in a browser
# Verify: logo in nav, favicon in tab, consistent styling across all tabs
```

## Key Files to Modify

| File | Change Type | Scope |
|------|-------------|-------|
| `dashboard-site/index.html` | Modify | Favicon, nav logo, font import |
| `dashboard-site/styles/main.css` | Major rewrite | Full design system replacement |
| `dashboard-site/scripts/app.js` | Modify | CSS class references in generated HTML |
| `dashboard-site/scripts/coverage-map.js` | Minor | Color values |
| `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` | Modify | Hardcoded defaults |
| `tests/Spectra.CLI.Tests/Dashboard/DashboardGeneratorTests.cs` | Modify | Asset copy verification |
| `README.md` | Minor | Add banner image |

## Risk Areas

- **CSS variable renaming**: If any variable name is missed, that component will lose its styling. Systematic search-and-replace required.
- **JavaScript class references**: `app.js` generates HTML with class names. All class name changes in CSS must be reflected in JS.
- **Hardcoded defaults**: The fallback CSS/HTML/JS in `DashboardGenerator.cs` must match the template files, or users without the template directory get a different experience.
