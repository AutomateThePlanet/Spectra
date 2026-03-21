# Research: SPECTRA Branding & Design System

**Feature**: 012-dashboard-branding
**Date**: 2026-03-21

## Research Findings

### 1. Current Dashboard Template Architecture

**Decision**: Modify template files in-place; no architectural changes needed.

**Rationale**: The dashboard uses a simple template substitution model — `DashboardGenerator.cs` loads `dashboard-site/index.html`, replaces `{{DASHBOARD_DATA}}` with JSON, and copies `styles/` and `scripts/` subdirectories to the output. Adding an `assets/` subdirectory to `dashboard-site/` will automatically be copied by the existing `CopyStaticAssetsAsync()` recursive directory copy logic.

**Alternatives considered**:
- Embedding assets as base64 in HTML — rejected because it bloats the HTML file size and complicates template maintenance
- Adding a new `--assets` CLI flag — rejected per YAGNI; the template directory already handles asset discovery

### 2. Font Strategy

**Decision**: Replace DM Sans / IBM Plex Sans with Inter via Google Fonts CDN, with system font fallbacks.

**Rationale**: Inter is a widely-used UI font designed for screen readability. The existing template already loads fonts from Google Fonts CDN (DM Sans, IBM Plex Sans), so this is a URL swap. System font fallbacks (`-apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`) ensure the dashboard works offline.

**Alternatives considered**:
- Self-hosting Inter font files — rejected because it adds file management complexity and the CDN approach is already established
- Keeping DM Sans alongside Inter — rejected because mixing font families undermines the cohesive brand identity

### 3. CSS Variable Migration Strategy

**Decision**: Replace the existing `:root` CSS variables with the new SPECTRA design tokens. Update all CSS rules that reference old variables.

**Rationale**: The current CSS already uses CSS custom properties (`:root` variables for `--primary`, `--success`, `--danger`, etc.). The new design tokens follow the same pattern but with SPECTRA brand colors. A search-and-replace on variable names plus updating the `:root` block is the most direct approach.

**Key mappings**:
- `--primary` (#1e40af) → `--color-navy` (#1B2A4A)
- `--primary-light` (#3b82f6) → `--color-navy-light` (#2D3F5E)
- `--success` (#16a34a) → `--color-green` (#16A34A) — same value
- `--warning` (#d97706) → `--color-gold` (#D97706) — same value
- `--danger` (#dc2626) → `--color-red` (#DC2626) — same value
- `--bg` (#f1f5f9) → `--bg-page` (#F9FAFB)
- `--card-bg` (#ffffff) → `--bg-card` (#FFFFFF) — same value
- New additions: `--color-beige`, `--color-beige-dark`, `--color-teal`, `--color-orange`, shadow tokens, gray scale

**Alternatives considered**:
- Adding new variables alongside old ones — rejected because it creates confusion and dead CSS
- Using a CSS preprocessor (Sass) — rejected per YAGNI; plain CSS variables are sufficient

### 4. Asset Copying in Generator

**Decision**: Place brand assets in `dashboard-site/assets/` so the existing recursive copy handles them. Also copy from repo-root `assets/` as a fallback.

**Rationale**: The `CopyDirectory()` method in `DashboardGenerator.cs` already recursively copies all subdirectories from the template directory. By placing `spectra_dashboard_banner.png` and `spectra_favicon.png` in `dashboard-site/assets/`, they will be automatically included in every generated dashboard without any C# code changes for the copy logic itself. The HTML template references them as `assets/spectra_dashboard_banner.png` and `assets/spectra_favicon.png`.

**Alternatives considered**:
- Adding explicit asset copy logic in C# — rejected because the recursive copy already handles this
- Referencing assets from the repo root `assets/` directory — rejected because the generated dashboard needs self-contained assets in its output directory

### 5. Hardcoded Defaults in DashboardGenerator.cs

**Decision**: Update `GetDefaultCss()`, `GetDefaultTemplate()`, and `GetDefaultJs()` fallback methods to reflect the new design system.

**Rationale**: `DashboardGenerator.cs` contains hardcoded fallback HTML/CSS/JS (lines 179-656) used when the template directory isn't found. These must be updated to match the new design system so that both template-based and fallback generation produce branded output.

**Alternatives considered**:
- Removing hardcoded defaults entirely, requiring the template directory — rejected because it would break the command when run from arbitrary directories without the template nearby
- Keeping old defaults as-is — rejected because it creates inconsistency between template and fallback paths

### 6. JavaScript Class Name Compatibility

**Decision**: Audit `app.js` for CSS class references and update to match new class names where changed. Minimize class name changes to avoid breaking JS selectors.

**Rationale**: The JavaScript code references CSS classes like `.badge`, `.card`, `.test-row`, `.nav-btn`, etc. The design system introduces new modifier classes (`.badge-passed`, `.badge-failed`, `.priority-high`, etc.) but the base classes remain compatible. The main changes are additive — new classes for new styling — rather than renaming existing classes.

**Alternatives considered**:
- Rewriting app.js to use a component framework — rejected per YAGNI and the "don't restructure" constraint
- Using data attributes instead of classes for JS selection — rejected because it adds unnecessary refactoring

### 7. README Banner Integration

**Decision**: Add the banner image as the first element in README.md using centered HTML.

**Rationale**: The user specified the exact markup. This is a one-line addition at the top of the file.

**Alternatives considered**: None — the requirement is unambiguous.
