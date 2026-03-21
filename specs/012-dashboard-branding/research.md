# Research: Dashboard Branding & Theming

**Date**: 2026-03-21 | **Feature**: 012-dashboard-branding

## Finding 1: CSS Custom Properties Already in Place

**Decision**: Use CSS custom property overrides for theming — no CSS rewrite needed.

**Rationale**: `dashboard-site/styles/main.css` already defines all visual properties as `:root` CSS variables (`--primary-color`, `--primary-light`, `--accent-color`, `--text-color`, `--bg-color`, `--card-bg`, `--border-color`, etc.). Brand colors and dark theme can be implemented by injecting a `<style>` block that overrides these variables.

**Alternatives considered**:
- Generating a complete replacement CSS file — rejected (fragile, hard to maintain)
- Using JavaScript to set variables at runtime — rejected (causes flash of unstyled content)
- CSS-in-JS approach — rejected (adds framework dependency, violates YAGNI)

## Finding 2: Template Placeholder Pattern Established

**Decision**: Add new `{{PLACEHOLDERS}}` for branding elements, following the existing `{{DASHBOARD_DATA}}` pattern.

**Rationale**: `DashboardGenerator.GenerateHtmlAsync()` already does `template.Replace("{{DASHBOARD_DATA}}", jsonData)`. Extending this with `{{COMPANY_NAME}}`, `{{LOGO_IMG}}`, `{{FAVICON_LINK}}`, `{{BRANDING_STYLES}}`, and `{{CUSTOM_CSS_LINK}}` is consistent and simple. Both the `dashboard-site/index.html` template and the inline `GetDefaultTemplate()` need to be updated.

**Alternatives considered**:
- Full templating engine (Razor, Handlebars) — rejected (massive dependency for simple replacements)
- DOM manipulation post-render — rejected (server-side string replacement is simpler and more reliable)

## Finding 3: Dark Theme Strategy

**Decision**: Dark theme implemented as a separate CSS file (`dark-theme.css`) with `:root` overrides, conditionally included by the injector.

**Rationale**: Keeping dark theme variables in a separate file makes them easy to maintain and test independently. The injector includes the dark theme CSS inline (embedded `<style>` block) rather than as a separate file link, so the dark theme works even when the dashboard is opened as a single HTML file.

**Alternatives considered**:
- `prefers-color-scheme` media query — rejected (spec requires theme to be baked in at generation time, not responsive to OS settings)
- Single CSS file with `.dark` class — considered viable but less maintainable for large variable sets
- CSS custom property sets per theme — chosen approach, cleanest separation

## Finding 4: Asset Handling Strategy

**Decision**: Copy logo, favicon, and custom CSS files to the output directory. No image processing.

**Rationale**: The dashboard is a static site. Assets must be co-located in the output directory. The generator already has `CopyStaticAssetsAsync()` and `CopyDirectory()` methods for copying styles and scripts. Logo/favicon copying follows the same pattern.

**Alternatives considered**:
- Base64 embedding in HTML — rejected (bloats HTML, bad for caching, breaks favicon standards)
- Asset CDN/URL references — rejected (requires network access, breaks offline viewing)
- Image resize/optimization — rejected (out of scope per spec, adds dependency)

## Finding 5: Preview Mode Data Structure

**Decision**: Create a `SampleDataFactory` that produces a complete `DashboardData` object with realistic mock data.

**Rationale**: The preview mode needs all sections populated (suites, tests, runs, coverage, trends) to verify branding across every dashboard view. The factory creates a fixed, deterministic dataset — same preview every time.

**Alternatives considered**:
- Empty dashboard with "sample" placeholders — rejected (can't verify branding on real UI components)
- Minimal data (1 suite, 1 test) — rejected (misses edge cases in coverage view, trend charts)
- Random data — rejected (non-deterministic, harder to verify visually)

## Finding 6: spectra.config.json Structure

**Decision**: Add `branding` as a top-level section in the dashboard config, nested under the existing `dashboard` key pattern.

**Rationale**: The existing config has top-level sections (`source`, `tests`, `ai`, `generation`, `validation`, `git`). Dashboard config (`DashboardConfig`) already exists as a model with `output_dir`, `title`, `template_dir`, etc. Adding `branding` as a property of `DashboardConfig` keeps dashboard concerns together. The `title` property already serves a similar purpose to `company_name` — the new `branding.company_name` will take precedence over `title` when set.

**Alternatives considered**:
- Top-level `branding` key separate from `dashboard` — rejected (branding is dashboard-specific)
- Separate `branding.json` file — rejected (adds config file proliferation, one config file is simpler)
