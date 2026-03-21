# Feature Specification: Dashboard Branding & Theming

**Feature Branch**: `012-dashboard-branding`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Add dashboard branding and theming customization. Allow users to configure custom logos, color schemes, company name, favicon, and visual themes for generated dashboards via spectra.config.json. Support light/dark themes, custom CSS overrides, and brand asset paths. The dashboard generator should inject branding configuration into the HTML template at build time."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Apply Company Branding to Dashboard (Priority: P1)

A QA lead generates a dashboard to share with stakeholders. They want the dashboard to display their company logo, company name, and use the company's brand colors so the report looks professional and recognizable rather than carrying generic Spectra branding.

**Why this priority**: The most fundamental branding need — replacing generic branding with the organization's identity. Without this, dashboards look like internal tool output rather than professional deliverables.

**Independent Test**: Can be fully tested by configuring logo, company name, and primary/accent colors in spectra.config.json, running `spectra dashboard`, and verifying the output HTML displays the custom branding elements instead of Spectra defaults.

**Acceptance Scenarios**:

1. **Given** a spectra.config.json with `branding.company_name` set to "Acme Corp", **When** the dashboard is generated, **Then** the header displays "Acme Corp" instead of "SPECTRA Dashboard" and the page title reflects the company name.
2. **Given** a spectra.config.json with `branding.logo` pointing to a valid image file, **When** the dashboard is generated, **Then** the logo appears in the dashboard header alongside the company name.
3. **Given** a spectra.config.json with `branding.colors.primary` and `branding.colors.accent` set, **When** the dashboard is generated, **Then** the header background, navigation buttons, and interactive elements use the specified colors.
4. **Given** no branding configuration exists, **When** the dashboard is generated, **Then** the default Spectra branding is applied (backward compatible).
5. **Given** a spectra.config.json with `branding.favicon` pointing to a valid icon file, **When** the dashboard is generated, **Then** the favicon is copied to the output directory and linked in the HTML head.

---

### User Story 2 - Switch Between Light and Dark Themes (Priority: P2)

A user wants to generate a dashboard that matches their organization's preferred visual style — either a light theme for printed reports or a dark theme for on-screen viewing. They select a theme in configuration and the entire dashboard adapts.

**Why this priority**: Theme support is the next most impactful visual customization after basic branding. It transforms the overall look with a single setting.

**Independent Test**: Can be fully tested by setting `branding.theme` to "light" or "dark" in config, generating a dashboard, and verifying the page uses the correct background, text, and component colors for that theme.

**Acceptance Scenarios**:

1. **Given** `branding.theme` is set to "dark", **When** the dashboard is generated, **Then** the page uses a dark background with light text, and all UI components (cards, sidebar, filters) adapt to the dark palette.
2. **Given** `branding.theme` is set to "light", **When** the dashboard is generated, **Then** the page uses a light background with dark text (current default appearance).
3. **Given** `branding.theme` is not set, **When** the dashboard is generated, **Then** the "light" theme is applied by default.
4. **Given** `branding.theme` is "dark" and custom brand colors are also set, **When** the dashboard is generated, **Then** the brand colors are applied on top of the dark theme base palette.

---

### User Story 3 - Apply Custom CSS Overrides (Priority: P3)

A power user needs fine-grained control over dashboard styling beyond what the built-in theme and color options provide. They supply a custom CSS file that is injected after the default styles, allowing them to override any visual aspect.

**Why this priority**: Covers advanced customization needs for organizations with strict brand guidelines that go beyond color and logo changes.

**Independent Test**: Can be fully tested by creating a custom CSS file, referencing it in `branding.custom_css`, generating a dashboard, and verifying the custom styles are included after the default stylesheet.

**Acceptance Scenarios**:

1. **Given** `branding.custom_css` points to a valid CSS file, **When** the dashboard is generated, **Then** the custom CSS is included in the output and loaded after the default stylesheet so overrides take effect.
2. **Given** `branding.custom_css` points to a file that does not exist, **When** the dashboard is generated, **Then** a warning is displayed but dashboard generation completes successfully with default styles.
3. **Given** the custom CSS file contains rules that override header styles, **When** the dashboard is rendered, **Then** the custom rules take precedence over both theme and brand color defaults.

---

### User Story 4 - Preview Branding Before Full Generation (Priority: P3)

A user wants to quickly verify their branding configuration looks correct before generating the full dashboard with data. They run a preview command that produces a sample dashboard with placeholder data and their branding applied.

**Why this priority**: Saves time during branding setup by providing fast feedback without requiring real test data or a full generation cycle.

**Independent Test**: Can be fully tested by running `spectra dashboard --preview` and verifying it produces a dashboard with sample data and the configured branding applied.

**Acceptance Scenarios**:

1. **Given** branding configuration is set, **When** `spectra dashboard --preview` is run, **Then** a dashboard is generated with placeholder/sample data and the configured branding applied.
2. **Given** no branding configuration exists, **When** `spectra dashboard --preview` is run, **Then** a dashboard is generated with default Spectra branding and sample data.

---

### Edge Cases

- What happens when the logo file path is invalid or the file is corrupted? The system displays a warning and generates the dashboard without the logo, falling back to text-only header.
- What happens when custom color values are invalid (e.g., not valid hex or CSS color)? The system ignores invalid color values with a warning and falls back to theme defaults.
- What happens when the logo file is extremely large (>5 MB)? The system warns the user that large assets may affect dashboard load time but proceeds with generation.
- What happens when branding is configured but the template_dir is also set to a custom template? Brand colors, logo, and company name are injected into whatever template is used; custom CSS is appended after the template's styles.
- What happens when the same color property is set in both theme and custom brand colors? Custom brand colors always take precedence over theme defaults.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a `branding` configuration section in spectra.config.json with fields for company_name, logo, favicon, theme, colors, and custom_css.
- **FR-002**: System MUST replace the default dashboard title and header text with the configured company_name when provided.
- **FR-003**: System MUST copy the logo file to the output directory and embed it in the dashboard header when a valid logo path is configured.
- **FR-004**: System MUST copy the favicon file to the output directory and link it in the HTML head when a valid favicon path is configured.
- **FR-005**: System MUST support "light" and "dark" theme values, with "light" as the default.
- **FR-006**: System MUST apply theme-appropriate color palettes to all dashboard UI components (header, sidebar, cards, charts, badges, filters).
- **FR-007**: System MUST support custom color overrides for primary, accent, background, text, and surface colors via the colors configuration object.
- **FR-008**: System MUST inject custom CSS after default styles when a valid custom_css file path is configured.
- **FR-009**: System MUST preserve full backward compatibility — dashboards generated without any branding configuration MUST look identical to the current output.
- **FR-010**: System MUST validate branding configuration at generation time and report warnings for invalid values (bad file paths, invalid colors) without failing the generation.
- **FR-011**: System MUST support a `--preview` flag on the dashboard command to generate a branded dashboard with sample data for quick visual verification.
- **FR-012**: System MUST inject branding configuration as a data attribute or embedded JSON block in the HTML so the client-side JavaScript can apply dynamic branding at render time.
- **FR-013**: System MUST support relative and absolute file paths for logo, favicon, and custom_css assets, resolving relative paths from the spectra.config.json location.

### Key Entities

- **BrandingConfig**: The branding configuration object within spectra.config.json. Contains company_name (string), logo (file path), favicon (file path), theme ("light" | "dark"), colors (color overrides object), and custom_css (file path).
- **ColorPalette**: A set of named color values (primary, accent, background, text, surface, border) that define the visual appearance of a theme. Each theme has a default palette; user-configured colors override individual values.
- **ThemePreset**: A predefined combination of color palette, typography adjustments, and component styling. Two built-in presets: "light" and "dark".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can fully brand a dashboard (logo, company name, colors) with a single configuration change and one command execution.
- **SC-002**: A branded dashboard is visually indistinguishable from a custom-built report — no residual default branding leaks through when branding is configured.
- **SC-003**: Switching between light and dark themes updates all dashboard components consistently — no visual artifacts or unreadable text combinations.
- **SC-004**: Dashboard generation time increases by no more than 500ms when branding is configured (asset copying and CSS injection overhead).
- **SC-005**: 100% backward compatibility — existing dashboards generated without branding configuration produce identical output before and after this feature is implemented.
- **SC-006**: Users can preview branding changes in under 5 seconds using the preview flag, without requiring real test data.

## Assumptions

- Logo and favicon files are user-provided and stored in the project directory or an accessible path. The system copies them to the output — it does not host or transform them.
- Color values follow standard CSS color syntax (hex, rgb, hsl, named colors). The system does not convert between color formats.
- The dark theme is a complete palette inversion (dark backgrounds, light text) — not just a color filter on the light theme.
- Custom CSS is trusted user input. The system does not sanitize or validate CSS content beyond checking file existence.
- The `--preview` flag generates sample data inline (e.g., 3 mock suites, 10 mock tests, 2 mock runs) — it does not require any existing test files or execution history.

## Scope Boundaries

**In scope**:
- Branding configuration in spectra.config.json
- Logo, favicon, company name injection
- Light/dark theme presets
- Custom color overrides
- Custom CSS file injection
- Preview mode for branding verification
- Branding applied to both the dashboard-site template and the embedded default template in DashboardGenerator

**Out of scope**:
- Custom font loading (beyond what the existing template already loads)
- Multiple theme presets beyond light/dark
- Live theme switching in the rendered dashboard (theme is baked in at generation time)
- Logo resizing or image processing
- White-labeling of Spectra CLI output or terminal UX
- Branding for HTML execution reports (only the dashboard)
