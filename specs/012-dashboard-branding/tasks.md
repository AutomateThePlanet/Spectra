# Tasks: SPECTRA Branding & Design System

**Input**: Design documents from `/specs/012-dashboard-branding/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Brand Asset Staging)

**Purpose**: Copy brand assets into the dashboard template directory so they ship with every generated site

- [ ] T001 Create `dashboard-site/assets/` directory and copy `assets/spectra_dashboard_banner.png` and `assets/spectra_favicon.png` into it

---

## Phase 2: Foundational (Design Tokens & Typography)

**Purpose**: Replace the CSS variable system and font imports — all component styling in later phases depends on these tokens being in place

**CRITICAL**: No component styling work can begin until this phase is complete

- [ ] T002 Replace the `:root` CSS variables block in `dashboard-site/styles/main.css` with the SPECTRA design tokens: primary colors (`--color-navy`, `--color-navy-light`, `--color-beige`, `--color-beige-dark`), spectral palette (`--color-green`, `--color-teal`, `--color-gold`, `--color-orange`, `--color-red`), neutral grays (`--color-gray-50` through `--color-gray-900`), backgrounds (`--bg-page`, `--bg-card`, `--bg-nav`, `--bg-sidebar`), and shadows (`--shadow-sm`, `--shadow-md`, `--shadow-lg`)
- [ ] T003 Update all CSS rules in `dashboard-site/styles/main.css` that reference old variable names (`--primary`, `--primary-light`, `--success`, `--warning`, `--danger`, `--bg`, `--card-bg`, etc.) to use the new SPECTRA token names per the mapping in research.md
- [ ] T004 Update the Google Fonts import in `dashboard-site/index.html` from DM Sans / IBM Plex Sans to Inter (weights 400, 500, 600, 700) and update the `body` font-family declaration in `dashboard-site/styles/main.css` to `'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif`
- [ ] T005 Add `.test-id, code` monospace font rule (`'JetBrains Mono', 'Fira Code', monospace; font-size: 13px`) and update heading sizes (`h1: 24px/700, h2: 20px/600, h3: 16px/600`) in `dashboard-site/styles/main.css`

**Checkpoint**: Design tokens and typography are in place. All subsequent component styling will use these tokens.

---

## Phase 3: User Story 1 - Dashboard Visual Identity (Priority: P1) MVP

**Goal**: Brand the dashboard with SPECTRA logo in nav bar, favicon in browser tab, and navy color scheme applied globally

**Independent Test**: Generate a dashboard and open in browser — logo visible in nav, favicon in tab, navy/beige/spectral colors throughout

### Implementation for User Story 1

- [ ] T006 [US1] Add `<link rel="icon" type="image/png" href="assets/spectra_favicon.png">` to the `<head>` section of `dashboard-site/index.html`
- [ ] T007 [US1] Replace the plain text "SPECTRA Dashboard" title in the nav bar of `dashboard-site/index.html` with an `<img src="assets/spectra_dashboard_banner.png" alt="SPECTRA" style="height: 40px;">` element, keeping the navy (#1B2A4A) background
- [ ] T008 [US1] Update the `.header` styles in `dashboard-site/styles/main.css` to use `background: var(--bg-nav)` (solid navy) instead of the current linear gradient, and ensure the nav bar height accommodates the 40-48px logo image
- [ ] T009 [US1] Update the page background in `dashboard-site/styles/main.css` to use `background: var(--bg-page)` and body text color to `color: var(--color-gray-900)`

**Checkpoint**: Dashboard has SPECTRA logo in nav, favicon in tab, navy background on nav bar, light gray page background. Visually branded.

---

## Phase 4: User Story 2 - Design System Consistency (Priority: P1)

**Goal**: Apply cohesive component styles across all dashboard elements — cards, badges, tables, navigation tabs, sidebar, and test list rows

**Independent Test**: Navigate each tab (Suites, Tests, Run History, Coverage) and verify consistent styling: pill badges, rounded cards with hover shadows, uppercase table headers, styled sidebar headings, status-colored test row borders

### Implementation for User Story 2

- [ ] T010 [P] [US2] Update `.nav-btn` class (rename to `.nav-tab` or add `.nav-tab` styles) in `dashboard-site/styles/main.css` with semi-transparent white borders, hover/active states per spec: `color: rgba(255,255,255,0.7)`, `border: 1px solid rgba(255,255,255,0.2)`, `border-radius: 8px`, active state with `background: rgba(255,255,255,0.15)`. Update corresponding class references in `dashboard-site/index.html`
- [ ] T011 [P] [US2] Update `.card` styles in `dashboard-site/styles/main.css`: `border-radius: 12px`, `border: 1px solid var(--color-gray-200)`, `padding: 24px`, `box-shadow: var(--shadow-sm)`, hover `box-shadow: var(--shadow-md)` with `transition: box-shadow 0.2s ease`
- [ ] T012 [P] [US2] Add status badge classes in `dashboard-site/styles/main.css`: base `.badge` (pill shape `border-radius: 9999px`, `font-size: 12px`, `font-weight: 600`, uppercase, `letter-spacing: 0.025em`), `.badge-passed`/`.badge-automated` (green bg #DCFCE7, color #166534), `.badge-failed`/`.badge-uncovered` (red bg #FEE2E2, color #991B1B), `.badge-skipped`/`.badge-partial` (yellow bg #FEF3C7, color #92400E), `.badge-blocked` (purple bg #F3E8FF, color #6B21A8)
- [ ] T013 [P] [US2] Add priority indicator classes in `dashboard-site/styles/main.css`: `.priority-high` (`color: var(--color-red)`, `font-weight: 600`), `.priority-medium` (`color: var(--color-gold)`, `font-weight: 600`), `.priority-low` (`color: var(--color-gray-500)`, `font-weight: 500`)
- [ ] T014 [P] [US2] Update table styles in `dashboard-site/styles/main.css`: `border-collapse: separate`, `th` uppercase with `font-size: 11px`, `font-weight: 600`, `letter-spacing: 0.05em`, `color: var(--color-gray-500)`, `border-bottom: 2px solid var(--color-gray-200)`. `td` padding `12px 16px`, `border-bottom: 1px solid var(--color-gray-100)`. `tr:hover td` background `var(--color-gray-50)`
- [ ] T015 [P] [US2] Update `.sidebar` styles in `dashboard-site/styles/main.css`: `background: var(--bg-sidebar)`, `border-right: 1px solid var(--color-gray-200)`, sidebar `h3` uppercase with `font-size: 11px`, `font-weight: 700`, `letter-spacing: 0.08em`, `color: var(--color-gray-500)`
- [ ] T016 [US2] Update `dashboard-site/scripts/app.js` to apply new CSS classes in generated HTML: use `.badge-passed`/`.badge-failed`/`.badge-skipped`/`.badge-blocked` for status indicators, `.priority-high`/`.priority-medium`/`.priority-low` for priority display, and `.nav-tab` (if renamed from `.nav-btn`) for navigation buttons
- [ ] T017 [US2] Add status-colored left borders to test list rows in `dashboard-site/scripts/app.js`: apply `border-left: 3px solid` with green (`var(--color-green)` or `#16A34A`) for passed, red (`#DC2626`) for failed, and gray (`#D1D5DB`) for no execution data. Apply `.test-id` class to test ID elements for monospace styling
- [ ] T018 [US2] Add text truncation for long test titles in `dashboard-site/styles/main.css`: `.test-title` with `overflow: hidden`, `text-overflow: ellipsis`, `white-space: nowrap`, `max-width` appropriate for the layout
- [ ] T019 [US2] Update sidebar summary in `dashboard-site/scripts/app.js`: make stat numbers larger and bolder, color-code automation percentage (green `#16A34A` for >= 80%, yellow `#D97706` for >= 50%, red `#DC2626` for < 50%), add a mini progress bar element next to the automation percentage
- [ ] T020 [US2] Update `dashboard-site/scripts/coverage-map.js` treemap colors to use the SPECTRA spectral palette values (#16A34A for high automation, #D97706 for partial, #DC2626 for none)

**Checkpoint**: All dashboard components have consistent SPECTRA styling. Cards, badges, tables, sidebar, test rows, and treemap all use the design system.

---

## Phase 5: User Story 3 - GitHub Repository Branding (Priority: P2)

**Goal**: Add the SPECTRA banner image to the top of README.md for professional GitHub presence

**Independent Test**: View README.md on GitHub and confirm centered banner image at full width

### Implementation for User Story 3

- [ ] T021 [P] [US3] Add the SPECTRA banner image markup to the top of `README.md`: `<p align="center"><img src="assets/spectra_github_readme_banner.png" alt="SPECTRA" width="100%"></p>`

**Checkpoint**: GitHub repository shows branded banner at the top of README.

---

## Phase 6: User Story 4 - Responsive Layout (Priority: P2)

**Goal**: Dashboard layout centers at max-width 1400px with 240px fixed sidebar on desktop, adapts with 16px padding on mobile

**Independent Test**: Resize browser window — content centers on wide screens, sidebar collapses on narrow screens, cards stack vertically on mobile

### Implementation for User Story 4

- [ ] T022 [US4] Update page layout rules in `dashboard-site/styles/main.css`: set main content area `max-width: 1400px` with `margin: 0 auto` for centering, sidebar width to `240px` fixed, consistent `gap: 24px` between card grid items
- [ ] T023 [US4] Add/update responsive breakpoint styles in `dashboard-site/styles/main.css`: at `max-width: 768px` apply `padding: 16px` to the content area, stack sidebar below or hide it, and ensure cards flow into a single column with `24px` gap

**Checkpoint**: Dashboard looks good on desktop (1400px+) and mobile (< 768px).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Update C# hardcoded defaults to match template changes, update tests, final verification

- [ ] T024 Update `GetDefaultTemplate()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` to include the favicon link, Inter font import, logo image in nav bar, and new class names matching the updated template
- [ ] T025 Update `GetDefaultCss()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` to include the SPECTRA design tokens, component styles, and typography matching `dashboard-site/styles/main.css`
- [ ] T026 Update `GetDefaultJs()` in `src/Spectra.CLI/Dashboard/DashboardGenerator.cs` to match class name changes made in `dashboard-site/scripts/app.js` (badge classes, priority classes, nav tab classes, test row borders, sidebar stats)
- [ ] T027 Update tests in `tests/Spectra.CLI.Tests/Dashboard/DashboardGeneratorTests.cs` to verify that generated dashboard output includes favicon link, logo image reference, and that assets directory is present in output when generated from template
- [ ] T028 Run full test suite (`dotnet test`) to verify no existing tests are broken by the template and generator changes
- [ ] T029 Generate a test dashboard (`dotnet run --project src/Spectra.CLI -- dashboard --output ./test-site`) and visually verify all tabs (Suites, Tests, Run History, Coverage) display with consistent SPECTRA branding — confirm no functionality regressions in filters, search, sorting, or data visualizations

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (assets must be staged before HTML can reference them)
- **US1 (Phase 3)**: Depends on Phase 2 (needs design tokens and font imports in place)
- **US2 (Phase 4)**: Depends on Phase 2 (needs design tokens). Can run in parallel with US1.
- **US3 (Phase 5)**: No dependencies on other phases — README edit is independent
- **US4 (Phase 6)**: Depends on Phase 4 (responsive rules need component styles to be final)
- **Polish (Phase 7)**: Depends on Phases 3, 4, 5, 6 (hardcoded defaults must match final template state)

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories. Can parallel with US1.
- **User Story 3 (P2)**: Fully independent — can start at any time
- **User Story 4 (P2)**: Depends on US2 component styles being final (responsive rules wrap existing components)

### Within Each User Story

- CSS changes before JS changes (JS applies CSS classes)
- Template HTML before CSS (HTML defines structure that CSS targets)
- All component styles before responsive rules

### Parallel Opportunities

- T010, T011, T012, T013, T014, T015 can all run in parallel (different CSS rule blocks, no overlapping selectors)
- T021 (README) can run in parallel with any other task
- US1 and US2 can run in parallel after Phase 2 completes
- T024, T025, T026 can run in parallel (different methods in the same C# file)

---

## Parallel Example: User Story 2

```bash
# Launch all component CSS tasks in parallel (different selectors, no conflicts):
Task: T010 "Update nav tab styles in dashboard-site/styles/main.css"
Task: T011 "Update card styles in dashboard-site/styles/main.css"
Task: T012 "Add status badge classes in dashboard-site/styles/main.css"
Task: T013 "Add priority indicator classes in dashboard-site/styles/main.css"
Task: T014 "Update table styles in dashboard-site/styles/main.css"
Task: T015 "Update sidebar styles in dashboard-site/styles/main.css"

# Then sequentially (depends on CSS classes being defined):
Task: T016 "Update app.js to apply new CSS classes"
Task: T017 "Add status-colored left borders to test rows"
Task: T019 "Update sidebar summary with color-coded automation"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 — both P1)

1. Complete Phase 1: Setup (copy brand assets)
2. Complete Phase 2: Foundational (design tokens + typography)
3. Complete Phase 3: US1 (logo, favicon, colors)
4. Complete Phase 4: US2 (component consistency)
5. **STOP and VALIDATE**: Generate dashboard, visually verify all 4 tabs
6. The dashboard should already look polished and branded at this point

### Incremental Delivery

1. Setup + Foundational → Tokens and fonts ready
2. Add US1 → Dashboard has brand identity (logo, favicon, colors) → Validate
3. Add US2 → All components styled consistently → Validate
4. Add US3 → README branded → Validate
5. Add US4 → Responsive layout polished → Validate
6. Polish → Hardcoded defaults updated, tests pass → Final validation

---

## Notes

- [P] tasks = different CSS rule blocks or different files, no dependencies
- [Story] label maps task to specific user story for traceability
- This feature is pure restyling — no C# model changes, no data changes, no new CLI flags
- The main risk is CSS class name mismatches between main.css and app.js — systematic audit needed in T016
- The hardcoded defaults in DashboardGenerator.cs (Phase 7) must be the LAST thing updated since they mirror final template state
- Commit after each phase to create clear checkpoints
