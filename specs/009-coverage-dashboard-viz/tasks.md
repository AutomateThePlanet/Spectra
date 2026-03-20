# Tasks: Coverage Dashboard Visualizations

**Input**: Design documents from `/specs/009-coverage-dashboard-viz/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/coverage-data.md

**Note**: Research confirmed that User Stories 1 (P1 — unified coverage analysis), 2 (P2 — auto-link), and 7 (P1 — init config) are **already fully implemented**. Tasks below cover only the remaining dashboard visualization work: typed detail models, progress bar drill-down, empty states, donut chart, and treemap.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US3, US4, US5, US6)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new project setup needed — all infrastructure exists. This phase covers only the backend data model changes that all dashboard stories depend on.

- [x] T001 Add typed detail model classes (DocumentationCoverageDetail, RequirementCoverageDetail, AutomationSuiteDetail, UnlinkedTestDetail) to `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`
- [x] T002 Replace `IReadOnlyList<object>? Details` with typed detail properties on each section data class in `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`
- [x] T003 Populate documentation detail list in `DataCollector.BuildCoverageSummaryAsync()` in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T004 Populate requirements detail list in `DataCollector.BuildCoverageSummaryAsync()` in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T005 Populate automation detail list and unlinked_tests in `DataCollector.BuildCoverageSummaryAsync()` in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T006 Add `has_requirements_file` boolean to RequirementsSectionData in `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`

**Checkpoint**: Backend produces typed, populated coverage detail data in the dashboard JSON payload. Verify with `dotnet build` and existing tests pass.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: CSS infrastructure needed by all dashboard visualization stories

**⚠️ CRITICAL**: Dashboard stories depend on these styles being in place

- [x] T007 [P] Add expand/collapse toggle button styles (`.coverage-toggle-btn`, `.coverage-detail-list`, `.coverage-detail-list.collapsed`) in `dashboard-site/styles/main.css`
- [x] T008 [P] Add donut chart styles (`.donut-chart`, `.donut-segment`, `.donut-center`, `.donut-legend`) in `dashboard-site/styles/main.css`
- [x] T009 [P] Add treemap styles (`.coverage-treemap`, `.treemap-block`, `.treemap-tooltip`) in `dashboard-site/styles/main.css`

**Checkpoint**: All CSS classes exist for dashboard components. No visual changes yet.

---

## Phase 3: User Story 3 — Progress Bar Drill-Down (Priority: P3) 🎯 MVP

**Goal**: Enhance three-section progress bars with expandable detail lists showing per-item coverage breakdown

**Independent Test**: Generate dashboard with coverage data, open Coverage tab, verify three progress bar cards with "Show details" toggle that expands per-item lists with covered/uncovered indicators

### Implementation for User Story 3

- [x] T010 [US3] Refactor `renderThreeSectionCoverage()` to read typed detail arrays from `data.coverage_summary` in `dashboard-site/scripts/app.js`
- [x] T011 [US3] Add expand/collapse "Show details" / "Hide details" toggle button below each progress bar in `dashboard-site/scripts/app.js`
- [x] T012 [US3] Render documentation detail list (doc path, test count, covered/uncovered icon) in `dashboard-site/scripts/app.js`
- [x] T013 [US3] Render requirements detail list (ID, title, linked test IDs, covered/uncovered icon) in `dashboard-site/scripts/app.js`
- [x] T014 [US3] Render automation detail list (per-suite breakdown: suite name, automated/total, percentage) in `dashboard-site/scripts/app.js`
- [x] T015 [US3] Add CSS transition animation for detail list expand/collapse in `dashboard-site/styles/main.css`
- [x] T016 [US3] Verify color coding thresholds: green >= 80%, yellow >= 50%, red < 50% in `dashboard-site/scripts/app.js`

**Checkpoint**: Three progress bar cards with expandable details, correct color coding, smooth animation

---

## Phase 4: User Story 6 — Empty State Guidance (Priority: P3)

**Goal**: Show helpful guidance messages when coverage data is missing or unconfigured

**Independent Test**: Generate dashboard with no requirements configured and no automation links, verify guidance text appears instead of bare zero-percent bars

### Implementation for User Story 6

- [x] T017 [US6] Add empty state check for documentation section — show "All documents have test coverage!" when 100% or guidance when no docs in `dashboard-site/scripts/app.js`
- [x] T018 [US6] Add empty state for requirements section — show "No requirements tracked yet..." with setup instructions when `has_requirements_file === false` and total === 0 in `dashboard-site/scripts/app.js`
- [x] T019 [US6] Add empty state for automation section — show "No automation links detected..." with `--auto-link` instructions when total === 0 in `dashboard-site/scripts/app.js`
- [x] T020 [US6] Style empty state messages (`.coverage-empty-state`, icon + text layout) in `dashboard-site/styles/main.css`

**Checkpoint**: Empty states show actionable guidance instead of confusing 0% bars

---

## Phase 5: User Story 4 — Donut Chart (Priority: P4)

**Goal**: Add a donut chart showing overall test distribution (automated/manual-only/unlinked) above the progress bars

**Independent Test**: Generate dashboard with known test distributions, verify donut chart renders correct proportional segments with proper colors and center label showing total count

### Implementation for User Story 4

- [x] T021 [US4] Compute donut chart data from `data.tests` array — categorize each test as automated (has `automated_by`), manual-only (has `source_refs` but no `automated_by`), or unlinked (neither) in `dashboard-site/scripts/app.js`
- [x] T022 [US4] Render SVG donut chart using `stroke-dasharray`/`stroke-dashoffset` for segments (green=automated, yellow=manual-only, red=unlinked) in `dashboard-site/scripts/app.js`
- [x] T023 [US4] Add center text showing total test count inside the donut ring in `dashboard-site/scripts/app.js`
- [x] T024 [US4] Add hover tooltips showing count + percentage per segment in `dashboard-site/scripts/app.js`
- [x] T025 [US4] Add legend below chart (colored dots + labels: "Automated", "Manual Only", "Unlinked") in `dashboard-site/scripts/app.js`
- [x] T026 [US4] Place donut chart at top of coverage tab, before progress bars in `dashboard-site/scripts/app.js`

**Checkpoint**: Donut chart renders above progress bars with correct segment proportions, center label, tooltips, and legend

---

## Phase 6: User Story 5 — Treemap Visualization (Priority: P5)

**Goal**: Add a D3.js treemap showing suites as blocks sized by test count and colored by automation coverage percentage

**Independent Test**: Generate dashboard with multi-suite test data, verify treemap renders blocks of correct relative sizes with color coding (green >= 50%, yellow > 0%, red = 0%)

### Implementation for User Story 5

- [x] T027 [US5] Build treemap data hierarchy from `data.suites` + `data.coverage_summary.automation.details` (suite name, test count, automation %) in `dashboard-site/scripts/coverage-map.js`
- [x] T028 [US5] Create D3 treemap layout using `d3.treemap()` with squarified tiling, sized by test count in `dashboard-site/scripts/coverage-map.js`
- [x] T029 [US5] Apply block color coding: green (>= 50% automated), yellow (> 0% but < 50%), red (0%) in `dashboard-site/scripts/coverage-map.js`
- [x] T030 [US5] Add block labels (suite name, test count, automation %) in `dashboard-site/scripts/coverage-map.js`
- [x] T031 [US5] Add hover tooltip with suite name, total tests, automated count, percentage in `dashboard-site/scripts/coverage-map.js`
- [x] T032 [US5] Add click handler to navigate to suite test list (reuse existing `showSuiteTests()` if available) in `dashboard-site/scripts/coverage-map.js`
- [x] T033 [US5] Place treemap below progress bars section in coverage tab, integrate with `renderCoverage()` in `dashboard-site/scripts/app.js`

**Checkpoint**: Treemap renders below progress bars with correctly sized/colored blocks, tooltips, and click navigation

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation and cleanup across all dashboard stories

- [x] T034 Verify backward compatibility — dashboard with no `coverage_summary` falls back to legacy `coverage.nodes`/`coverage.links` path in `dashboard-site/scripts/app.js`
- [x] T035 Verify DataCollector detail arrays are sorted per contract (docs by path, requirements by ID, suites by name) in `src/Spectra.CLI/Dashboard/DataCollector.cs`
- [x] T036 Run `dotnet test` to verify all existing tests still pass
- [x] T037 Run quickstart.md validation — generate dashboard and verify all four visualizations render correctly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — backend model + data population
- **Foundational (Phase 2)**: Depends on Phase 1 (CSS only, can start in parallel with late Phase 1 tasks)
- **US3 Progress Bars (Phase 3)**: Depends on Phase 1 (typed data) + Phase 2 (CSS)
- **US6 Empty States (Phase 4)**: Depends on Phase 3 (progress bar rendering code)
- **US4 Donut Chart (Phase 5)**: Depends on Phase 2 (CSS) — does NOT depend on Phase 3/4
- **US5 Treemap (Phase 6)**: Depends on Phase 1 (automation details data) + Phase 2 (CSS)
- **Polish (Phase 7)**: Depends on all previous phases

### User Story Dependencies

- **US3 (Progress Bars)**: Needs typed detail data from Phase 1 — core dashboard MVP
- **US6 (Empty States)**: Needs progress bar rendering from US3 — extends it with conditionals
- **US4 (Donut Chart)**: Independent of US3/US6 — reads from `data.tests` directly
- **US5 (Treemap)**: Independent of US3/US4/US6 — reads from `data.suites` + `coverage_summary.automation.details`

### Parallel Opportunities

- T007, T008, T009 can all run in parallel (different CSS sections)
- US4 (Donut) and US5 (Treemap) can run in parallel (different files: app.js vs coverage-map.js)
- US4 and US5 can both start after Phase 2, without waiting for US3/US6

### Within Each User Story

- Models/data before rendering
- Core rendering before interactive features (tooltips, clicks)
- All stories independently testable via dashboard generation

---

## Parallel Example: After Phase 2

```bash
# Launch US4 (Donut) and US5 (Treemap) in parallel:
Task: "Compute donut chart data in dashboard-site/scripts/app.js"
Task: "Build treemap data hierarchy in dashboard-site/scripts/coverage-map.js"

# US3 (Progress Bars) and US6 (Empty States) are sequential:
Task: "Refactor renderThreeSectionCoverage() in app.js"  # US3 first
Task: "Add empty state checks in app.js"                  # US6 after US3
```

---

## Implementation Strategy

### MVP First (US3 + US6 — Progress Bars + Empty States)

1. Complete Phase 1: Backend typed models + data population
2. Complete Phase 2: CSS infrastructure
3. Complete Phase 3: Progress bar drill-down (US3)
4. Complete Phase 4: Empty states (US6)
5. **STOP and VALIDATE**: Generate dashboard, verify progress bars with details + empty states
6. Deploy/demo if ready — users get the most actionable visualization first

### Incremental Delivery

1. Phase 1 + 2 → Backend + CSS ready
2. Add US3 + US6 → Progress bars with drill-down and empty states → Deploy (MVP!)
3. Add US4 → Donut chart → Deploy
4. Add US5 → Treemap → Deploy
5. Phase 7 → Polish and validate

### Parallel Execution

After Phase 2 completes:
- Stream A: US3 → US6 (progress bars → empty states, same file)
- Stream B: US4 (donut chart, app.js — different section)
- Stream C: US5 (treemap, coverage-map.js — different file)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US1, US2, US7 are SKIPPED — already fully implemented per research.md
- D3.js already loaded via CDN — no new dependencies for treemap
- Donut chart uses custom SVG (matching existing trend chart pattern) — no new dependencies
- Backward compatibility: dashboard must still work when `coverage_summary` is null/missing
