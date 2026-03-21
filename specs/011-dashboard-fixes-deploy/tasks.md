# Tasks: Dashboard Improvements and Cloudflare Pages Deployment

**Input**: Design documents from `/specs/011-dashboard-fixes-deploy/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new project initialization needed — this feature extends an existing codebase. Setup consists of creating the shared utility needed by multiple stories.

- [x] T001 Add `cloudflare_project_name` property (default: "spectra-dashboard") to `src/Spectra.Core/Models/Config/DashboardConfig.cs` with `[JsonPropertyName("cloudflare_project_name")]` attribute
- [x] T002 Add `cloudflare_project_name` field to default config template in `src/Spectra.CLI/Templates/spectra.config.json` under the dashboard section

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared path normalization utility used by US1 coverage bug fix and consumed by existing analyzers

**CRITICAL**: US1 and US2 depend on this phase

- [x] T003 Create shared `SourceRefNormalizer` static class in `src/Spectra.Core/Coverage/SourceRefNormalizer.cs` with methods: `StripFragment(string sourceRef)` (strips everything after #), `NormalizePath(string path)` (strips fragment + normalizes slashes to forward + trims leading slash), and `NormalizeForComparison(string path)` (NormalizePath + lowercase). Use `StringComparer.OrdinalIgnoreCase` for dictionary lookups.
- [x] T004 Add unit tests for `SourceRefNormalizer` in `tests/Spectra.Core.Tests/Coverage/SourceRefNormalizerTests.cs` — test fragment stripping (`docs/auth.md#Login-Flow` → `docs/auth.md`), slash normalization (`docs\\auth.md` → `docs/auth.md`), leading slash trimming, case normalization, no-fragment passthrough, empty/null handling

**Checkpoint**: Shared normalizer ready — user story implementation can begin

---

## Phase 3: User Story 1 — Accurate Coverage Metrics (Priority: P1) MVP

**Goal**: Fix documentation coverage showing 0% due to fragment anchors in source_refs not matching filesystem paths

**Independent Test**: Run `spectra ai analyze --coverage` on a project with fragment-anchored source_refs and verify non-zero documentation coverage

### Implementation for User Story 1

- [x] T005 [US1] Update `BuildCoverageSummaryAsync()` in `src/Spectra.CLI/Dashboard/DataCollector.cs` — replace inline fragment stripping (line ~646) with `SourceRefNormalizer.NormalizePath()` and use `StringComparer.OrdinalIgnoreCase` for the `docToTests` dictionary. Also normalize `relativePath` on line ~663 using `SourceRefNormalizer.NormalizePath()` before dictionary lookup.
- [x] T006 [US1] Update `NormalizePath()` in `src/Spectra.CLI/Coverage/GapAnalyzer.cs` (line ~273-276) — add fragment stripping by calling `SourceRefNormalizer.NormalizePath()` instead of inline slash replacement
- [x] T007 [US1] Add integration test in `tests/Spectra.CLI.Tests/Dashboard/DataCollectorTests.cs` — test that `BuildCoverageSummaryAsync` correctly matches source_refs with fragment anchors (`docs/checkout.md#Payment-Flow`) to filesystem doc paths (`docs/checkout.md`), returning non-zero doc coverage
- [x] T008 [US1] Add test in `tests/Spectra.CLI.Tests/Coverage/GapAnalyzerTests.cs` — test that `AnalyzeGaps` correctly handles source_refs with fragments and doesn't double-count coverage

**Checkpoint**: `spectra ai analyze --coverage` now reports accurate documentation coverage percentages

---

## Phase 4: User Story 2 — Usable Dashboard Layout and Visualizations (Priority: P1)

**Goal**: Fix trend chart sizing, hide coverage tab filters, fix treemap data source, replace coverage relationships with D3.js hierarchical tree

**Independent Test**: Generate dashboard with real data, visually verify: compact trend chart, no coverage sidebar, accurate treemap colors, readable coverage tree

### Implementation for User Story 2

- [x] T009 [P] [US2] Fix trend chart max height in `dashboard-site/styles/main.css` — verify `.trend-chart` and `.trend-svg` have `max-height: 220px` CSS constraint. If the chart container lacks a hard max-height, add it. Verify the single-point compact card fallback renders correctly (`.trend-compact-summary` class).
- [x] T010 [P] [US2] Fix coverage tab sidebar visibility in `dashboard-site/scripts/app.js` — verify the sidebar toggle logic (line ~176-183) works correctly when switching to Coverage tab. Ensure `.content` area expands to full width when `.sidebar.hidden` is applied. Add CSS rule in `dashboard-site/styles/main.css` for `.main:has(.sidebar.hidden) .content` or equivalent to make content full-width.
- [x] T011 [P] [US2] Fix treemap data source mismatch in `dashboard-site/scripts/coverage-map.js` — normalize suite name matching between `window.dashboardData.suites[].name` and `coverageSummary.automation.details[].suite` by trimming and lowercasing both sides when building `autoPctMap` (line ~22-28). Log warning to console if a suite has no automation match.
- [x] T012 [US2] Implement D3.js hierarchical coverage tree in `dashboard-site/scripts/coverage-map.js` — add `renderCoverageTree(coverageSummary, tests)` function that: (1) builds CoverageTreeNode hierarchy from dashboard data (docs grouped by folder → domain, each doc → feature, tests grouped by component → area, individual tests → leaf), (2) adds "Unlinked Tests" synthetic domain for tests without source_refs, (3) renders D3.js horizontal tree layout with `d3.tree()`, (4) uses SVG with `d3.zoom()` for pan/zoom support
- [x] T013 [US2] Add coverage tree node rendering in `dashboard-site/scripts/coverage-map.js` — document nodes as rectangles (green/yellow/orange/red based on automation status of children), test nodes as circles (green if automated, yellow if manual). Each document node shows: short filename, test count badge, automation % text, mini SVG progress bar. Each test node shows: test ID + short title, automated/manual icon.
- [x] T014 [US2] Add coverage tree interactivity in `dashboard-site/scripts/coverage-map.js` — click document/area nodes to expand/collapse children (start collapsed to document level), hover tooltip with full details (full path, all test IDs, automation file paths), Expand All / Collapse All buttons above the tree
- [x] T015 [US2] Add coverage tree styles in `dashboard-site/styles/main.css` — styles for `.coverage-tree-container` (responsive, min-height 400px), node shapes (rect for docs, circle for tests), color classes for coverage status (green >= 80%, yellow >= 50%, orange > 0%, red = 0%), progress bar mini-element, tooltip styles, zoom controls, Expand/Collapse button styles
- [x] T016 [US2] Wire coverage tree into dashboard app in `dashboard-site/scripts/app.js` — call `renderCoverageTree()` from the coverage view render function, passing `data.coverage_summary` and `data.tests`. Replace or supplement the existing CSS-based tree (lines ~1006-1120) with the new D3 tree.
- [x] T017 [US2] Apply general styling improvements in `dashboard-site/styles/main.css` — (1) ensure all `.card` and `.coverage-section` elements use consistent `border-radius: var(--radius-lg)`, `box-shadow: var(--shadow-sm)`, `padding: 1.5rem`, (2) coverage progress bars use color thresholds: green (`var(--cov-green)`) >= 80%, yellow (`var(--cov-amber)`) >= 50%, red (`var(--cov-red)`) < 50%, (3) `.nav-btn.active` has distinct background/border styling, (4) consistent `gap: 1.5rem` between card sections

**Checkpoint**: Dashboard renders accurate, usable visualizations with correct data

---

## Phase 5: User Story 3 — Automated Dashboard Deployment (Priority: P2)

**Goal**: GitHub Actions workflow auto-deploys dashboard to Cloudflare Pages when test data changes

**Independent Test**: Push a change to a test file on main branch, verify workflow triggers and dashboard deploys

### Implementation for User Story 3

- [x] T018 [US3] Create GitHub Actions workflow at `.github/workflows/deploy-dashboard.yml` — triggers on push to main (paths: `tests/**`, `.execution/**`, `docs/**`, `spectra.config.json`) and workflow_dispatch. Steps: checkout, setup-dotnet 8.0.x, install spectra tool, run `spectra ai analyze --coverage --auto-link` (continue-on-error), run `spectra dashboard --output ./site`, read `cloudflare_project_name` from `spectra.config.json` (default: spectra-dashboard), deploy with `cloudflare/wrangler-action@v3` using `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` secrets
- [x] T019 [US3] Create workflow template at `src/Spectra.CLI/Templates/deploy-dashboard.yml` — same content as T018 but with comments explaining each secret and placeholder for project name. This template is copied by `spectra init`.

**Checkpoint**: Workflow file ready for deployment (requires user to configure secrets)

---

## Phase 6: User Story 4 — Authenticated Dashboard Access (Priority: P2)

**Goal**: GitHub OAuth restricts dashboard access to authorized repo members, 24-hour sessions

**Independent Test**: Deploy dashboard, visit URL unauthenticated → redirect to GitHub login → authenticate → dashboard loads (or access-denied)

### Implementation for User Story 4

- [x] T020 [US4] Update session duration in `dashboard-site/functions/_middleware.js` — change `SESSION_DURATION` constant from `7 * 24 * 60 * 60` (604800) to `24 * 60 * 60` (86400). Update the `Max-Age` in the Set-Cookie header to match.
- [x] T021 [US4] Update `ALLOWED_REPO` to `ALLOWED_REPOS` in `dashboard-site/functions/_middleware.js` — rename env var read from `env.ALLOWED_REPO` to `env.ALLOWED_REPOS`. Split comma-separated value into array. In the repo access check, iterate over each repo and grant access if user has read access to ANY of them (short-circuit on first match). Update comments and error messages.
- [x] T022 [US4] Verify and fix callback handler at `dashboard-site/functions/auth/callback.js` — if it's a placeholder (just returns 404 or redirect), verify the middleware handles the callback flow correctly. If callback logic needs to be in `callback.js` for Cloudflare Pages routing, move the relevant callback handling there. Ensure the `/auth/callback` route works end-to-end.
- [x] T023 [US4] Update `dashboard-site/access-denied.html` — verify it displays clear error messages for each error code (`no_repo_access`, `access_denied`, `token_error`, `user_fetch_failed`). Add guidance text explaining that the user needs read access to an allowed repository and should contact the repository admin.

**Checkpoint**: OAuth flow works end-to-end with multi-repo support and 24-hour sessions

---

## Phase 7: User Story 5 — Dashboard Deployment Setup Guide (Priority: P2)

**Goal**: Self-sufficient step-by-step guide for deploying dashboard to Cloudflare Pages with GitHub OAuth

**Independent Test**: A person unfamiliar with Cloudflare can follow the guide to deploy a working authenticated dashboard

### Implementation for User Story 5

- [x] T024 [US5] Create deployment setup guide at `docs/deployment/cloudflare-pages-setup.md` — sections: Prerequisites (GitHub repo with SPECTRA, Cloudflare account, admin access), Step 1: Create Cloudflare Pages Project (dash.cloudflare.com → Pages → Direct Upload, note project name and account ID), Step 2: Create API Token (profile/api-tokens, "Edit Cloudflare Pages" template), Step 3: Create GitHub OAuth App (github.com/settings/developers, homepage URL `https://<project>.pages.dev`, callback URL `https://<project>.pages.dev/auth/callback`), Step 4: Configure GitHub Secrets (CLOUDFLARE_API_TOKEN, CLOUDFLARE_ACCOUNT_ID), Step 5: Configure Cloudflare Pages Env Vars (GITHUB_CLIENT_ID, GITHUB_CLIENT_SECRET, ALLOWED_REPOS, SESSION_SECRET, AUTH_ENABLED=true), Step 6: First Deployment (push or manual trigger, verify), Custom Domain (optional), Troubleshooting (access denied → ALLOWED_REPOS mismatch, callback error → URL mismatch, deploy fails → token permissions, empty dashboard → spectra errors, session issues → SECRET rotation), Security Notes (OAuth repo access, encrypted cookies, 24h expiry, Cloudflare Access for enterprise). No actual secrets in the guide.

**Checkpoint**: Guide is complete and self-sufficient

---

## Phase 8: User Story 6 — Init Scaffolding (Priority: P3)

**Goal**: `spectra init` creates deployment workflow and shows setup guide reference

**Independent Test**: Run `spectra init` in a new directory, verify workflow file created and guide message displayed

### Implementation for User Story 6

- [x] T025 [US6] Update `src/Spectra.CLI/Commands/Init/InitHandler.cs` — after existing GitHub integration file creation (after `.github/agents/` and `.github/skills/` creation), add code to copy `deploy-dashboard.yml` template to `.github/workflows/deploy-dashboard.yml`. Add console output message: "Dashboard auto-deployment workflow created. See docs/deployment/cloudflare-pages-setup.md for setup instructions."
- [x] T026 [US6] Add test for init workflow creation in `tests/Spectra.CLI.Tests/Commands/InitHandlerTests.cs` (or appropriate test file) — verify `spectra init` creates `.github/workflows/deploy-dashboard.yml`, verify file contains no actual secrets, verify console output includes setup guide reference

**Checkpoint**: `spectra init` includes deployment scaffolding

---

## Phase 9: User Story 7 — Dashboard Configuration (Priority: P3)

**Goal**: Dashboard config supports `cloudflare_project_name` with sensible defaults

**Independent Test**: Set `cloudflare_project_name` in config, verify workflow reads it correctly

### Implementation for User Story 7

- [x] T027 [US7] Verify `cloudflare_project_name` integration end-to-end — confirm the GitHub workflow (T018) reads `cloudflare_project_name` from `spectra.config.json` and passes it to the wrangler deploy command. Confirm the default value "spectra-dashboard" is used when not configured. This is a verification task linking T001, T002, and T018.

**Checkpoint**: Config field works end-to-end in the deployment pipeline

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final verification and cleanup across all stories

- [x] T028 Run `dotnet build` and fix any compilation errors across all modified C# files
- [x] T029 Run `dotnet test` and verify all existing tests pass plus new tests (T004, T007, T008, T026)
- [x] T030 [P] Generate dashboard locally (`spectra dashboard --output ./site`) and visually verify: coverage percentages non-zero, trend chart compact, no coverage sidebar, treemap colors correct, coverage tree renders and expands/collapses
- [x] T031 [P] Review all modified files for consistency: no hardcoded secrets, no debug console.logs left in production JS, consistent naming across new code
- [x] T032 Run quickstart.md validation — follow quickstart steps to verify build, test, and dashboard generation work

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS US1
- **US1 (Phase 3)**: Depends on Foundational (T003, T004) — coverage normalizer
- **US2 (Phase 4)**: Can start after Setup (no dependency on Foundational). T009-T011 are independent CSS/JS fixes. T012-T016 build the coverage tree (sequential within the tree work). T017 is independent styling.
- **US3 (Phase 5)**: Can start after Setup (T001, T002) — needs config field for project name
- **US4 (Phase 6)**: No dependencies on other stories — standalone OAuth work
- **US5 (Phase 7)**: Can start after US3 and US4 are designed (needs to document what they produce)
- **US6 (Phase 8)**: Depends on US3 (T019 template must exist)
- **US7 (Phase 9)**: Depends on T001, T002 (config field) and T018 (workflow)
- **Polish (Phase 10)**: Depends on all stories complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (shared normalizer)
- **US2 (P1)**: Independent — can start immediately (different files from US1)
- **US3 (P2)**: Depends on T001-T002 (config field)
- **US4 (P2)**: Fully independent
- **US5 (P2)**: Best done after US3+US4 are defined
- **US6 (P3)**: Depends on US3 (T019 template)
- **US7 (P3)**: Verification only — depends on T001, T002, T018

### Parallel Opportunities

- T001 and T002 can run in parallel (different files)
- T003 and T004 can run sequentially but T003 first
- T009, T010, T011 can all run in parallel (different aspects of dashboard)
- T012, T013, T014 are sequential (building the tree incrementally)
- T017 can run in parallel with T012-T016 (styling vs tree logic)
- T018 and T019 can run in parallel with US1 work
- T020, T021 can run in parallel (different sections of middleware)
- US2 and US3 and US4 can all proceed in parallel (different file sets)

---

## Parallel Example: User Story 2

```bash
# These three tasks modify different aspects and can run in parallel:
Task T009: "Fix trend chart max height in dashboard-site/styles/main.css"
Task T010: "Fix coverage tab sidebar in dashboard-site/scripts/app.js"
Task T011: "Fix treemap data source in dashboard-site/scripts/coverage-map.js"

# After the parallel fixes, the tree work is sequential:
Task T012: "Build D3.js tree hierarchy in dashboard-site/scripts/coverage-map.js"
Task T013: "Add tree node rendering in dashboard-site/scripts/coverage-map.js"
Task T014: "Add tree interactivity in dashboard-site/scripts/coverage-map.js"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T004)
3. Complete Phase 3: US1 — Coverage bug fix (T005-T008)
4. Complete Phase 4: US2 — Dashboard UI fixes (T009-T017)
5. **STOP and VALIDATE**: Generate dashboard, verify coverage is accurate and visualizations work
6. Deploy/demo if ready — dashboard is now usable

### Incremental Delivery

1. Setup + Foundational → Shared normalizer ready
2. US1 → Coverage metrics accurate → Verify with `spectra ai analyze --coverage`
3. US2 → Dashboard visualizations fixed → Visual verification
4. US3 + US4 → Deployment pipeline + OAuth → Can deploy to Cloudflare
5. US5 → Setup guide written → Guide reviewed
6. US6 + US7 → Init scaffolding + config → Full feature complete
7. Polish → All tests pass, visual QA done

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (coverage bug) + US2 (dashboard UI)
   - Developer B: US3 (workflow) + US4 (OAuth) + US5 (guide)
   - Developer C: US6 (init) + US7 (config verification)
3. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Tests included for C# changes per constitution requirement (test-required discipline)
- Dashboard JS changes verified via visual inspection (no automated UI tests)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
