# Tasks: Dashboard Branding & Theming

**Input**: Design documents from `/specs/012-dashboard-branding/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests are included — the project constitution requires tests for all public APIs and critical paths.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new files and config model foundation

- [x] T001 [P] Create `BrandingConfig` record in `src/Spectra.Core/Models/Config/BrandingConfig.cs` with properties: `company_name`, `logo`, `favicon`, `theme`, `colors`, `custom_css` (all nullable, JSON snake_case naming)
- [x] T002 [P] Create `ColorPaletteConfig` record in `src/Spectra.Core/Models/Config/ColorPaletteConfig.cs` with properties: `primary`, `accent`, `background`, `text`, `surface`, `border` (all nullable strings)
- [x] T003 Add `BrandingConfig? Branding` property to `DashboardConfig` in `src/Spectra.Core/Models/Config/DashboardConfig.cs` (default null)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core branding injection service that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create `BrandingInjector` class in `src/Spectra.CLI/Dashboard/BrandingInjector.cs` with method `InjectBranding(string html, BrandingConfig? config, string configDir) → string` — when config is null, replace placeholders with defaults ("SPECTRA Dashboard", empty favicon/logo/styles/customCss)
- [x] T005 Implement CSS variable override generation in `BrandingInjector` — build a `<style>:root { ... }</style>` block from `ColorPaletteConfig` values, only including non-null properties
- [x] T006 Implement asset path resolution in `BrandingInjector` — resolve relative paths from `configDir`, validate file existence, return warnings list for missing files
- [x] T007 [P] Create dark theme CSS variable overrides in `dashboard-site/styles/dark-theme.css` with all `:root` variables for dark mode (see data-model.md Dark Theme table)
- [x] T008 Update `dashboard-site/index.html` — replace hardcoded "SPECTRA Dashboard" with `{{COMPANY_NAME}}`, add `{{FAVICON_LINK}}` in head, add `{{LOGO_IMG}}` in header before h1, add `{{BRANDING_STYLES}}` before `</head>`, add `{{CUSTOM_CSS_LINK}}` after default stylesheet link
- [x] T009 Update default template in `DashboardGenerator.GetDefaultTemplate()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` to include the same branding placeholders as T008
- [x] T010 Update default CSS in `DashboardGenerator.GetDefaultCss()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` — add `.header-logo` style (max-height 40px, margin-right 12px)
- [x] T011 Integrate `BrandingInjector` into `DashboardGenerator.GenerateHtmlAsync()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` — call `InjectBranding` after `{{DASHBOARD_DATA}}` replacement, pass branding config from `DashboardConfig`
- [x] T012 Update `DashboardGenerator.CopyStaticAssetsAsync()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` — copy logo, favicon, and custom CSS files to output directory when configured and files exist
- [x] T013 [P] Write unit tests for `BrandingConfig` deserialization in `tests/Spectra.Core.Tests/Config/BrandingConfigTests.cs` — roundtrip serialization, null defaults, partial config, backward compatibility (no branding = null)
- [x] T014 [P] Write unit tests for `BrandingInjector` in `tests/Spectra.CLI.Tests/Dashboard/BrandingInjectorTests.cs` — null config (defaults), full config, partial config (name only, colors only), invalid file paths (warnings), dark theme injection, CSS variable generation, custom CSS link injection

**Checkpoint**: Foundation ready — branding injection pipeline works end-to-end with defaults

---

## Phase 3: User Story 1 — Apply Company Branding (Priority: P1) 🎯 MVP

**Goal**: Users configure company name, logo, favicon, and brand colors in spectra.config.json. Generated dashboard displays custom branding.

**Independent Test**: Set `dashboard.branding.company_name`, `logo`, `favicon`, and `colors.primary` in config → run `spectra dashboard` → verify header shows company name with logo, favicon in browser tab, and primary color applied to header gradient.

### Implementation for User Story 1

- [x] T015 [US1] Wire `DashboardConfig.Branding` through the dashboard command handler — read from loaded `SpectraConfig`, pass to `DashboardGenerator` in `src/Spectra.CLI/Commands/Dashboard/DashboardCommand.cs` (or equivalent handler)
- [x] T016 [US1] Implement company name injection in `BrandingInjector` — replace `{{COMPANY_NAME}}` with `branding.company_name` or "SPECTRA Dashboard" fallback; also update `<title>` tag
- [x] T017 [US1] Implement logo injection in `BrandingInjector` — replace `{{LOGO_IMG}}` with `<img src="logo.{ext}" alt="{company_name}" class="header-logo">` or empty string
- [x] T018 [US1] Implement favicon injection in `BrandingInjector` — replace `{{FAVICON_LINK}}` with `<link rel="icon" href="favicon.{ext}">` or empty string
- [x] T019 [US1] Implement brand color injection in `BrandingInjector` — replace `{{BRANDING_STYLES}}` with `<style>:root { --primary-color: ...; }</style>` using non-null color values from `ColorPaletteConfig`
- [x] T020 [US1] Add branding status logging to dashboard command output — log "Applying {company_name} branding", "Logo copied from {path}", warnings for missing files
- [x] T021 [US1] Update existing `DashboardGeneratorTests` in `tests/Spectra.CLI.Tests/Dashboard/DashboardGeneratorTests.cs` — add tests for branded generation (company name in output HTML, logo img tag present, favicon link present, CSS variable overrides present)
- [x] T022 [US1] Add backward compatibility test in `tests/Spectra.CLI.Tests/Dashboard/DashboardGeneratorTests.cs` — generate with null branding config, verify output matches current default behavior exactly

**Checkpoint**: User Story 1 complete — dashboards can be fully branded with company identity

---

## Phase 4: User Story 2 — Light/Dark Theme (Priority: P2)

**Goal**: Users select "light" or "dark" theme in config. Dashboard adapts all components to the selected theme.

**Independent Test**: Set `dashboard.branding.theme` to "dark" → run `spectra dashboard` → verify dark backgrounds, light text, adapted badges and charts.

### Implementation for User Story 2

- [x] T023 [US2] Implement theme injection in `BrandingInjector` — when theme is "dark", inline the contents of `dark-theme.css` as a `<style>` block within `{{BRANDING_STYLES}}`; ensure brand color overrides apply AFTER theme variables
- [x] T024 [US2] Add `dark` body class injection in `BrandingInjector` — add `class="dark"` to `<body>` tag when theme is "dark" (for any JS-side conditional styling)
- [x] T025 [US2] Update `dashboard-site/scripts/app.js` — on DOMContentLoaded, read `#branding-config` embedded JSON, apply `dark` class to body if theme is "dark"
- [x] T026 [US2] Audit `dashboard-site/styles/main.css` — verify ALL hardcoded colors use CSS variables; fix any that don't (check badges, tooltips, shadows, chart colors, form inputs)
- [x] T027 [US2] Embed branding config as `<script id="branding-config" type="application/json">` in `BrandingInjector` for client-side access
- [x] T028 [US2] Implement theme validation in `BrandingInjector` — validate theme is "light" or "dark" (case-insensitive), warn and default to "light" for invalid values
- [x] T029 [US2] Write dark theme tests in `tests/Spectra.CLI.Tests/Dashboard/BrandingInjectorTests.cs` — dark theme CSS inlined, body class added, brand colors override theme, invalid theme warning, light theme is no-op

**Checkpoint**: User Story 2 complete — dashboards support light and dark themes

---

## Phase 5: User Story 3 — Custom CSS Overrides (Priority: P3)

**Goal**: Power users supply a custom CSS file for fine-grained dashboard styling control.

**Independent Test**: Create a custom CSS file with header override → set `dashboard.branding.custom_css` in config → run `spectra dashboard` → verify custom styles loaded after defaults.

### Implementation for User Story 3

- [x] T030 [US3] Implement custom CSS injection in `BrandingInjector` — replace `{{CUSTOM_CSS_LINK}}` with `<link rel="stylesheet" href="custom.css">` when custom_css is configured and file exists; empty string otherwise
- [x] T031 [US3] Ensure custom CSS is copied to output in `DashboardGenerator.CopyStaticAssetsAsync()` — copy file as `custom.css` to output root
- [x] T032 [US3] Handle missing custom CSS file — warn via CLI output, proceed with generation, replace placeholder with empty string
- [x] T033 [US3] Write custom CSS tests in `tests/Spectra.CLI.Tests/Dashboard/BrandingInjectorTests.cs` — link tag present when configured, missing file warning, custom CSS loaded after theme styles (ordering)

**Checkpoint**: User Story 3 complete — dashboards support custom CSS overrides

---

## Phase 6: User Story 4 — Preview Mode (Priority: P3)

**Goal**: Users run `spectra dashboard --preview` to quickly verify branding with sample data.

**Independent Test**: Configure branding → run `spectra dashboard --preview` → verify dashboard generated with sample data and branding applied, no real test data required.

### Implementation for User Story 4

- [x] T034 [P] [US4] Create `SampleDataFactory` in `src/Spectra.CLI/Dashboard/SampleDataFactory.cs` — static `CreateSampleData()` method returning `DashboardData` with 3 sample suites, 10 tests, 2 runs, coverage summary, and 5 trend points (see data-model.md Sample Data section)
- [x] T035 [US4] Add `--preview` option to dashboard command in `src/Spectra.CLI/Commands/Dashboard/DashboardCommand.cs` — when set, use `SampleDataFactory.CreateSampleData()` instead of `DataCollector`
- [x] T036 [US4] Log preview mode status — output "Preview mode: using sample data" when --preview is active
- [x] T037 [P] [US4] Write tests for `SampleDataFactory` in `tests/Spectra.CLI.Tests/Dashboard/SampleDataFactoryTests.cs` — verify data is complete (non-null suites, tests, runs, coverage, trends), deterministic (same output every call), and valid (positive counts, valid percentages)

**Checkpoint**: User Story 4 complete — branding can be previewed without real data

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation, edge cases, and final quality

- [x] T038 Implement color validation in `BrandingInjector` — validate CSS color format (hex, rgb, hsl, named) using regex; skip invalid values with warning
- [x] T039 Add large asset warning in `BrandingInjector` — warn when logo file exceeds 5 MB
- [x] T040 Update `spectra.config.json` template in `src/Spectra.CLI/Templates/spectra.config.json` — add commented-out `dashboard.branding` section showing available options
- [x] T041 Update CLAUDE.md Recent Changes — add 012-dashboard-branding entry documenting new models, services, config options, and --preview flag
- [x] T042 Run quickstart.md validation — execute the quickstart scenarios end-to-end to verify all branding features work as documented

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T001-T003) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2)
- **User Story 2 (Phase 4)**: Depends on Foundational (Phase 2) — can run in parallel with US1
- **User Story 3 (Phase 5)**: Depends on Foundational (Phase 2) — can run in parallel with US1/US2
- **User Story 4 (Phase 6)**: Depends on Foundational (Phase 2) — can run in parallel with US1/US2/US3
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — no dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational — independent of US1 (both inject into `{{BRANDING_STYLES}}` but theme comes first, colors override)
- **User Story 3 (P3)**: Can start after Foundational — independent (uses separate `{{CUSTOM_CSS_LINK}}` placeholder)
- **User Story 4 (P3)**: Can start after Foundational — independent (only needs `DashboardGenerator`, not branding-specific)

### Within Each User Story

- Models before services
- Services before command integration
- Implementation before tests (constitution allows tests after implementation)
- Core implementation before logging/validation

### Parallel Opportunities

- T001, T002 can run in parallel (separate files, no dependencies)
- T007, T008 can run in parallel (CSS file vs HTML template)
- T013, T014 can run in parallel (separate test files)
- All four user story phases can run in parallel after Foundational completes
- T034, T037 can run in parallel with other US4 tasks (separate files)

---

## Parallel Example: Foundational Phase

```bash
# These can run in parallel (different files):
Task T001: "Create BrandingConfig in src/Spectra.Core/Models/Config/BrandingConfig.cs"
Task T002: "Create ColorPaletteConfig in src/Spectra.Core/Models/Config/ColorPaletteConfig.cs"

# After T001-T003 complete, these can run in parallel:
Task T007: "Create dark-theme.css in dashboard-site/styles/dark-theme.css"
Task T008: "Update dashboard-site/index.html with branding placeholders"
Task T013: "Write BrandingConfig tests in tests/Spectra.Core.Tests/Config/BrandingConfigTests.cs"
Task T014: "Write BrandingInjector tests in tests/Spectra.CLI.Tests/Dashboard/BrandingInjectorTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T014)
3. Complete Phase 3: User Story 1 (T015-T022)
4. **STOP and VALIDATE**: Generate dashboard with company name + logo + colors → verify branding appears
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Branding pipeline works with defaults
2. Add User Story 1 → Company branding works → Deploy (MVP!)
3. Add User Story 2 → Dark theme works → Deploy
4. Add User Story 3 → Custom CSS works → Deploy
5. Add User Story 4 → Preview mode works → Deploy
6. Polish → Validation, config template, documentation

### Recommended Order (Solo Developer)

P1 → P2 → P3 (CSS) → P3 (Preview) → Polish

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Total: 42 tasks across 7 phases
