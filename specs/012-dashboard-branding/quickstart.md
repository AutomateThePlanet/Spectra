# Quickstart: Dashboard Branding & Theming

**Feature**: 012-dashboard-branding | **Date**: 2026-03-21

## What This Feature Adds

Dashboard branding and theming customization:
1. **Company branding** — logo, company name, favicon replace default Spectra identity
2. **Light/dark themes** — two built-in theme presets
3. **Custom colors** — override individual color values via config
4. **Custom CSS** — inject your own stylesheet for full control
5. **Preview mode** — verify branding quickly with sample data

## Prerequisites

A `spectra.config.json` file must exist in your project root.

## Quick Setup

Add a `branding` section to your dashboard config:

```json
{
  "dashboard": {
    "output_dir": "./site",
    "branding": {
      "company_name": "Acme Corp",
      "logo": "assets/logo.svg",
      "theme": "dark",
      "colors": {
        "primary": "#0d47a1"
      }
    }
  }
}
```

## Verification

### 1. Preview Branding (fastest)

```bash
spectra dashboard --preview
cd site && python -m http.server 8080
```

Open http://localhost:8080 — you should see:
- Your company name in the header and browser tab
- Your logo in the header
- Dark theme (dark backgrounds, light text) if configured
- Custom primary color applied to header gradient

### 2. Full Dashboard with Branding

```bash
spectra dashboard --output ./site
```

Verify branding appears across all views (Suites, Tests, Runs, Coverage).

### 3. Minimal Branding (name only)

```json
{
  "dashboard": {
    "branding": {
      "company_name": "My Team"
    }
  }
}
```

Header shows "My Team" instead of "SPECTRA Dashboard". Everything else stays default.

### 4. Dark Theme

```json
{
  "dashboard": {
    "branding": {
      "theme": "dark"
    }
  }
}
```

All components use dark backgrounds with light text. Charts and badges adapt.

### 5. Custom CSS Override

Create `assets/custom.css`:

```css
.header {
    background: linear-gradient(135deg, #8b0000, #b22222) !important;
}
.card {
    border-left: 3px solid #8b0000;
}
```

Reference it in config:

```json
{
  "dashboard": {
    "branding": {
      "custom_css": "assets/custom.css"
    }
  }
}
```

## Key Files

| File | Changes |
|------|---------|
| `src/Spectra.Core/Models/Config/BrandingConfig.cs` | Branding configuration model |
| `src/Spectra.Core/Models/Config/ColorPaletteConfig.cs` | Color override model |
| `src/Spectra.Core/Models/Config/DashboardConfig.cs` | Add branding property |
| `src/Spectra.CLI/Dashboard/BrandingInjector.cs` | Branding → HTML/CSS injection |
| `src/Spectra.CLI/Dashboard/SampleDataFactory.cs` | Preview mode sample data |
| `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` | Integrate branding injection |
| `dashboard-site/index.html` | Branding placeholders |
| `dashboard-site/styles/main.css` | Verify CSS variable coverage |
| `dashboard-site/styles/dark-theme.css` | Dark theme variables |
| `dashboard-site/scripts/app.js` | Apply branding at render time |
