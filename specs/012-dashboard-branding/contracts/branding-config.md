# Contract: Branding Configuration Schema

**Feature**: 012-dashboard-branding | **Date**: 2026-03-21

## Config Location

`spectra.config.json` → `dashboard.branding` section

## Schema

```json
{
  "dashboard": {
    "branding": {
      "company_name": "<string|null>",
      "logo": "<file-path|null>",
      "favicon": "<file-path|null>",
      "theme": "<'light'|'dark'>",
      "colors": {
        "primary": "<css-color|null>",
        "accent": "<css-color|null>",
        "background": "<css-color|null>",
        "text": "<css-color|null>",
        "surface": "<css-color|null>",
        "border": "<css-color|null>"
      },
      "custom_css": "<file-path|null>"
    }
  }
}
```

## Field Specifications

### company_name
- **Type**: string or null
- **Default**: null (uses "SPECTRA Dashboard")
- **Applied to**: HTML `<title>`, header `<h1>` text
- **Precedence**: Overrides `dashboard.title` when both are set

### logo
- **Type**: file path (relative or absolute) or null
- **Default**: null (no logo displayed)
- **Supported formats**: PNG, SVG, JPG, GIF, WEBP
- **Applied to**: `<img>` tag in dashboard header, left of company name
- **Asset handling**: Copied to output directory root as `logo.<ext>`

### favicon
- **Type**: file path (relative or absolute) or null
- **Default**: null (no favicon)
- **Supported formats**: ICO, PNG, SVG
- **Applied to**: `<link rel="icon">` in HTML `<head>`
- **Asset handling**: Copied to output directory root as `favicon.<ext>`

### theme
- **Type**: string enum
- **Values**: `"light"`, `"dark"`
- **Default**: `"light"`
- **Case**: Case-insensitive matching
- **Invalid handling**: Warns and defaults to `"light"`

### colors
- **Type**: object with string properties or null
- **Default**: null (uses theme defaults)
- **Valid values**: Any CSS color string (`#hex`, `rgb()`, `hsl()`, named)
- **Invalid handling**: Skips invalid values with warning, uses theme default
- **Precedence**: Custom colors override theme preset values

### custom_css
- **Type**: file path (relative or absolute) or null
- **Default**: null (no custom CSS)
- **Applied to**: `<link rel="stylesheet">` loaded after default + theme styles
- **Asset handling**: Copied to output directory as `custom.css`
- **Missing file handling**: Warns, proceeds without custom CSS

## Path Resolution

All file paths (`logo`, `favicon`, `custom_css`) are resolved as follows:

1. If absolute path → use as-is
2. If relative path → resolve relative to the directory containing `spectra.config.json`
3. If file not found → warn via CLI output, skip the asset

## Injection Points

The branding injector modifies the HTML template at these points:

| Placeholder | Replacement | Condition |
|-------------|-------------|-----------|
| `{{COMPANY_NAME}}` | `branding.company_name` or "SPECTRA Dashboard" | Always |
| `{{FAVICON_LINK}}` | `<link rel="icon" href="favicon.ext">` | When favicon configured |
| `{{LOGO_IMG}}` | `<img src="logo.ext" alt="..." class="header-logo">` | When logo configured |
| `{{BRANDING_STYLES}}` | `<style>:root { ... }</style>` with overrides | When theme or colors configured |
| `{{CUSTOM_CSS_LINK}}` | `<link rel="stylesheet" href="custom.css">` | When custom_css configured |

## CLI Integration

### Dashboard command with branding

```bash
# Generate dashboard with branding from config
spectra dashboard --output ./site

# Preview branding with sample data
spectra dashboard --preview

# Preview with custom output location
spectra dashboard --preview --output ./preview
```

### CLI output messages

```
Branding: Applying "Acme Corp" branding with dark theme
Branding: Logo copied from assets/logo.svg
Branding: Custom CSS copied from assets/custom.css
Branding: Warning - favicon file not found: assets/favicon.ico (skipped)
Branding: Warning - invalid color value for "primary": "notacolor" (using theme default)
```
