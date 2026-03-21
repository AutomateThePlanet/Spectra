# Data Model: Dashboard Branding & Theming

**Feature**: 012-dashboard-branding | **Date**: 2026-03-21

## Entities

### BrandingConfig (new)

Branding configuration nested within DashboardConfig.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| company_name | string? | null | Display name replacing "SPECTRA Dashboard" in header and page title |
| logo | string? | null | Path to logo image file (PNG, SVG, JPG). Relative to config file location. |
| favicon | string? | null | Path to favicon file (ICO, PNG). Relative to config file location. |
| theme | string? | "light" | Theme preset: "light" or "dark" |
| colors | ColorPaletteConfig? | null | Custom color overrides applied on top of the theme |
| custom_css | string? | null | Path to custom CSS file. Relative to config file location. |

### ColorPaletteConfig (new)

Custom color overrides. Any null field inherits from the selected theme's defaults.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| primary | string? | null | Primary brand color (header background, active nav) |
| accent | string? | null | Accent color (links, interactive elements) |
| background | string? | null | Page background color |
| text | string? | null | Primary text color |
| surface | string? | null | Card/panel background color |
| border | string? | null | Border color for cards, dividers, inputs |

### DashboardConfig (existing — modify)

Add branding property.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| output_dir | string | "./site" | Output directory (existing) |
| title | string? | null | Dashboard title — superseded by branding.company_name when set (existing) |
| template_dir | string? | null | Custom template directory (existing) |
| include_coverage | bool | true | Include coverage visualization (existing) |
| include_runs | bool | true | Include run history (existing) |
| max_trend_points | int | 30 | Maximum trend data points (existing) |
| **branding** | **BrandingConfig?** | **null** | **Branding and theming configuration (new)** |

## Theme Presets

### Light Theme (default)

Maps to existing `:root` CSS variables:

| CSS Variable | Value | Purpose |
|-------------|-------|---------|
| --primary-color | #1e40af | Header gradient start |
| --primary-light | #3b82f6 | Header gradient end |
| --accent-color | #2563eb | Interactive elements |
| --text-color | #1e293b | Primary text |
| --text-muted | #64748b | Secondary text |
| --bg-color | #f1f5f9 | Page background |
| --card-bg | #ffffff | Card/panel background |
| --border-color | #e2e8f0 | Borders |

### Dark Theme

| CSS Variable | Value | Purpose |
|-------------|-------|---------|
| --primary-color | #1e3a5f | Header gradient start (muted) |
| --primary-light | #2563eb | Header gradient end |
| --accent-color | #3b82f6 | Interactive elements |
| --text-color | #e2e8f0 | Primary text (light on dark) |
| --text-muted | #94a3b8 | Secondary text |
| --bg-color | #0f172a | Page background (dark) |
| --card-bg | #1e293b | Card background (dark surface) |
| --border-color | #334155 | Borders (subtle on dark) |
| --color-success | #22c55e | Green (brighter for dark bg) |
| --color-warning | #f59e0b | Yellow (brighter for dark bg) |
| --color-danger | #ef4444 | Red (brighter for dark bg) |
| --shadow-sm | 0 1px 2px rgba(0,0,0,0.2) | Slightly stronger shadows |
| --shadow-md | 0 4px 6px rgba(0,0,0,0.3) | Slightly stronger shadows |

## Config JSON Structure

```json
{
  "dashboard": {
    "output_dir": "./site",
    "branding": {
      "company_name": "Acme Corp",
      "logo": "assets/logo.svg",
      "favicon": "assets/favicon.ico",
      "theme": "dark",
      "colors": {
        "primary": "#0d47a1",
        "accent": "#1565c0"
      },
      "custom_css": "assets/custom-dashboard.css"
    }
  }
}
```

## Relationships

```
spectra.config.json
└── dashboard (DashboardConfig)
    ├── output_dir, title, template_dir, etc. (existing)
    └── branding (BrandingConfig)
        └── colors (ColorPaletteConfig)
```

## Validation Rules

- `theme` must be "light" or "dark" (case-insensitive). Invalid values default to "light" with warning.
- `colors` values must be valid CSS color strings (hex: `#RGB`/`#RRGGBB`, `rgb()`, `hsl()`, or named colors). Invalid values are skipped with warning.
- `logo`, `favicon`, `custom_css` paths are resolved relative to the directory containing `spectra.config.json`. Absolute paths are also accepted.
- Missing asset files produce a warning but do not fail generation.
- `company_name` takes precedence over the existing `title` field when both are set.
- All branding fields are optional — null/omitted means "use default".

## Sample Data (Preview Mode)

The `SampleDataFactory` generates a fixed `DashboardData` for `--preview`:

| Section | Sample Content |
|---------|---------------|
| Suites | 3 suites: "Checkout" (15 tests), "Authentication" (10 tests), "Search" (8 tests) |
| Tests | 10 tests with varied priorities (high/medium/low), components, tags, automation status |
| Runs | 2 runs: one with 80% pass rate, one with 60% pass rate |
| Coverage | Documentation: 75%, Requirements: 50%, Automation: 40% |
| Trends | 5 data points showing improving pass rate |
