# Tasks: Dashboard and Coverage Analysis

**Input**: Design documents from `/specs/003-dashboard-coverage-analysis/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and configuration structure

- [X] T001 Create dashboard-site/ directory structure per plan (index.html, styles/, scripts/)
- [ ] T002 [P] Add dashboard configuration section to spectra.config.json schema in src/Spectra.Core/Config/
- [ ] T003 [P] Add coverage configuration section to spectra.config.json schema in src/Spectra.Core/Config/
- [X] T004 [P] Create src/Spectra.Core/Models/Dashboard/ directory for dashboard models
- [X] T005 [P] Create src/Spectra.Core/Models/Coverage/ directory for coverage models
- [X] T006 [P] Create src/Spectra.Core/Coverage/ directory for coverage analysis logic
- [X] T007 [P] Create src/Spectra.CLI/Dashboard/ directory for dashboard generation
- [X] T008 [P] Create src/Spectra.CLI/Coverage/ directory for coverage CLI output
- [X] T009 [P] Create tests/Spectra.Core.Tests/Coverage/ directory for coverage tests
- [X] T010 [P] Create tests/Spectra.CLI.Tests/Dashboard/ directory for dashboard tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and infrastructure that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T011 Create DashboardData model in src/Spectra.Core/Models/Dashboard/DashboardData.cs
- [X] T012 [P] Create SuiteStats model in src/Spectra.Core/Models/Dashboard/SuiteStats.cs
- [X] T013 [P] Create TestEntry model in src/Spectra.Core/Models/Dashboard/TestEntry.cs
- [X] T014 [P] Create RunSummary model in src/Spectra.Core/Models/Dashboard/RunSummary.cs
- [X] T015 [P] Create CoverageData model in src/Spectra.Core/Models/Dashboard/CoverageData.cs
- [X] T016 [P] Create CoverageNode model in src/Spectra.Core/Models/Dashboard/CoverageNode.cs
- [X] T017 [P] Create CoverageLink model in src/Spectra.Core/Models/Coverage/CoverageLink.cs
- [X] T018 [P] Create LinkStatus enum in src/Spectra.Core/Models/Coverage/LinkStatus.cs
- [X] T019 [P] Create CoverageReport model in src/Spectra.Core/Models/Coverage/CoverageReport.cs
- [X] T020 [P] Create CoverageSummary model in src/Spectra.Core/Models/Coverage/CoverageSummary.cs
- [X] T021 [P] Create SuiteCoverage model in src/Spectra.Core/Models/Coverage/SuiteCoverage.cs
- [X] T022 [P] Create ComponentCoverage model in src/Spectra.Core/Models/Coverage/ComponentCoverage.cs
- [X] T023 [P] Create UnlinkedTest model in src/Spectra.Core/Models/Coverage/UnlinkedTest.cs
- [X] T024 [P] Create OrphanedAutomation model in src/Spectra.Core/Models/Coverage/OrphanedAutomation.cs
- [X] T025 [P] Create BrokenLink model in src/Spectra.Core/Models/Coverage/BrokenLink.cs
- [X] T026 [P] Create LinkMismatch model in src/Spectra.Core/Models/Coverage/LinkMismatch.cs
- [X] T027 Create ExecutionDbReader for reading .execution/spectra.db in src/Spectra.Core/Storage/ExecutionDbReader.cs
- [X] T028 Add unit tests for ExecutionDbReader in tests/Spectra.Core.Tests/Storage/ExecutionDbReaderTests.cs

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Generate Dashboard Site (Priority: P1) MVP

**Goal**: Generate a complete static website from test suite indexes and execution reports

**Independent Test**: Generate dashboard from repository with 3+ suites, verify all pages render with accurate data

### Implementation for User Story 1

- [X] T029 [US1] Create DataCollector service in src/Spectra.CLI/Dashboard/DataCollector.cs
- [X] T030 [US1] Implement suite index reading in DataCollector (read all _index.json files)
- [X] T031 [US1] Implement execution report reading from reports/ directory in DataCollector
- [X] T032 [US1] Implement execution data reading from .execution/ database in DataCollector
- [X] T033 [US1] Create DashboardGenerator service in src/Spectra.CLI/Dashboard/DashboardGenerator.cs
- [X] T034 [US1] Implement HTML template loading from dashboard-site/ in DashboardGenerator
- [X] T035 [US1] Implement JSON data embedding in HTML output in DashboardGenerator
- [X] T036 [US1] Create HtmlRenderer service in src/Spectra.CLI/Dashboard/HtmlRenderer.cs (embedded in DashboardGenerator)
- [X] T037 [US1] Implement suite browser page generation in HtmlRenderer (embedded in DashboardGenerator)
- [X] T038 [US1] Implement test case page rendering with Markdown content in HtmlRenderer (embedded in DashboardGenerator)
- [X] T039 [US1] Create DashboardCommand CLI handler in src/Spectra.CLI/Commands/DashboardCommand.cs
- [X] T040 [US1] Register DashboardCommand with CLI root in src/Spectra.CLI/Program.cs
- [X] T041 [US1] Create base dashboard HTML template in dashboard-site/index.html
- [X] T042 [P] [US1] Create dashboard CSS styles in dashboard-site/styles/main.css
- [X] T043 [P] [US1] Create dashboard JavaScript (data loading) in dashboard-site/scripts/app.js
- [X] T044 [US1] Add unit tests for DataCollector in tests/Spectra.CLI.Tests/Dashboard/DataCollectorTests.cs
- [X] T045 [US1] Add unit tests for DashboardGenerator in tests/Spectra.CLI.Tests/Dashboard/DashboardGeneratorTests.cs
- [X] T046 [US1] Add integration test for dashboard command in tests/Spectra.CLI.Tests/Dashboard/DashboardCommandTests.cs

**Checkpoint**: `spectra dashboard --output ./site` generates working static site

---

## Phase 4: User Story 2 - Browse Suites and Tests (Priority: P1)

**Goal**: Provide filtering by priority, tags, component, and search functionality

**Independent Test**: Generate dashboard with varied tests, verify filters narrow results correctly

### Implementation for User Story 2

- [ ] T047 [US2] Implement client-side priority filtering in dashboard-site/scripts/app.js
- [ ] T048 [US2] Implement client-side tag filtering (multi-select, AND logic) in dashboard-site/scripts/app.js
- [ ] T049 [US2] Implement client-side component filtering in dashboard-site/scripts/app.js
- [ ] T050 [US2] Implement client-side text search (ID and title) in dashboard-site/scripts/app.js
- [ ] T051 [US2] Create filter UI components in dashboard-site/index.html
- [ ] T052 [US2] Style filter UI in dashboard-site/styles/main.css
- [ ] T053 [US2] Implement test card click navigation to detail view in dashboard-site/scripts/app.js
- [ ] T054 [US2] Implement test detail view rendering with metadata in dashboard-site/scripts/app.js

**Checkpoint**: Dashboard filters work correctly, test navigation works

---

## Phase 5: User Story 3 - View Execution History (Priority: P1)

**Goal**: Display past execution runs with drill-down to individual results

**Independent Test**: Generate dashboard from 5+ execution reports, verify run list and drill-down work

### Implementation for User Story 3

- [ ] T055 [US3] Extend DataCollector to aggregate run history with pass/fail counts
- [ ] T056 [US3] Create run history page template in dashboard-site/index.html
- [ ] T057 [US3] Implement run history list rendering (sorted by date) in dashboard-site/scripts/app.js
- [ ] T058 [US3] Implement run detail view with individual test outcomes in dashboard-site/scripts/app.js
- [ ] T059 [US3] Implement trend calculation (pass rate over time) in DataCollector
- [ ] T060 [US3] Render trend visualization (simple chart) in dashboard-site/scripts/app.js
- [ ] T061 [US3] Style run history and detail views in dashboard-site/styles/main.css
- [ ] T062 [US3] Add tests for run history aggregation in tests/Spectra.CLI.Tests/Dashboard/DataCollectorTests.cs

**Checkpoint**: Run history displays correctly with drill-down

---

## Phase 6: User Story 5 - Analyze Automation Coverage (Priority: P1)

**Goal**: Scan tests and automation code to identify coverage gaps

**Independent Test**: Run analysis on repo with mixed linking states, verify accurate reporting

### Implementation for User Story 5

- [ ] T063 [US5] Create AutomationScanner in src/Spectra.Core/Coverage/AutomationScanner.cs
- [ ] T064 [US5] Implement regex-based attribute pattern matching in AutomationScanner
- [ ] T065 [US5] Implement file scanning with configurable directories in AutomationScanner
- [ ] T066 [US5] Create LinkReconciler in src/Spectra.Core/Coverage/LinkReconciler.cs
- [ ] T067 [US5] Implement test→automation map building (from automated_by) in LinkReconciler
- [ ] T068 [US5] Implement automation→test map building (from attributes) in LinkReconciler
- [ ] T069 [US5] Implement bidirectional link reconciliation algorithm in LinkReconciler
- [ ] T070 [US5] Implement unlinked test detection in LinkReconciler
- [ ] T071 [US5] Implement orphaned automation detection in LinkReconciler
- [ ] T072 [US5] Implement broken link detection in LinkReconciler
- [ ] T073 [US5] Implement mismatch detection in LinkReconciler
- [ ] T074 [US5] Create CoverageCalculator in src/Spectra.Core/Coverage/CoverageCalculator.cs
- [ ] T075 [US5] Implement coverage percentage calculation per suite in CoverageCalculator
- [ ] T076 [US5] Implement coverage percentage calculation per component in CoverageCalculator
- [ ] T077 [US5] Add --coverage flag to AnalyzeCommand in src/Spectra.CLI/Commands/AnalyzeCommand.cs
- [ ] T078 [US5] Implement coverage analysis orchestration in AnalyzeCommand
- [ ] T079 [US5] Add unit tests for AutomationScanner in tests/Spectra.Core.Tests/Coverage/AutomationScannerTests.cs
- [ ] T080 [US5] Add unit tests for LinkReconciler in tests/Spectra.Core.Tests/Coverage/LinkReconcilerTests.cs
- [ ] T081 [US5] Add unit tests for CoverageCalculator in tests/Spectra.Core.Tests/Coverage/CoverageCalculatorTests.cs
- [ ] T082 [US5] Add integration test for coverage analysis in tests/Spectra.CLI.Tests/Coverage/CoverageAnalysisTests.cs

**Checkpoint**: `spectra ai analyze --coverage` produces accurate coverage report

---

## Phase 7: User Story 7 - Export Coverage Report (Priority: P2)

**Goal**: Output coverage analysis in Markdown and JSON formats

**Independent Test**: Run analysis with each format flag, verify output structure

### Implementation for User Story 7

- [ ] T083 [US7] Create CoverageReportWriter in src/Spectra.CLI/Coverage/CoverageReportWriter.cs
- [ ] T084 [US7] Implement JSON report generation in CoverageReportWriter
- [ ] T085 [US7] Implement Markdown report generation in CoverageReportWriter
- [ ] T086 [US7] Add --format flag (json/markdown) to AnalyzeCommand
- [ ] T087 [US7] Add --output flag for file path to AnalyzeCommand
- [ ] T088 [US7] Add unit tests for CoverageReportWriter in tests/Spectra.CLI.Tests/Coverage/CoverageReportWriterTests.cs

**Checkpoint**: Coverage reports export in both formats to file or stdout

---

## Phase 8: User Story 4 - Visualize Coverage Relationships (Priority: P2)

**Goal**: Interactive coverage map showing doc→test→automation relationships

**Independent Test**: Generate dashboard with linked tests, verify visualization displays correctly

### Implementation for User Story 4

- [ ] T089 [US4] Extend DataCollector to build CoverageData with nodes and links
- [ ] T090 [US4] Implement document node extraction from document map in DataCollector
- [ ] T091 [US4] Implement link building from source_refs and automated_by in DataCollector
- [ ] T092 [US4] Create coverage map page template in dashboard-site/index.html
- [ ] T093 [US4] Add D3.js library reference (CDN) to dashboard-site/index.html
- [ ] T094 [US4] Implement D3.js tree visualization in dashboard-site/scripts/coverage-map.js
- [ ] T095 [US4] Implement node color coding (green/yellow/red) in coverage-map.js
- [ ] T096 [US4] Implement node click handler for detail view in coverage-map.js
- [ ] T097 [US4] Style coverage map visualization in dashboard-site/styles/main.css
- [ ] T098 [US4] Add tests for coverage data generation in tests/Spectra.CLI.Tests/Dashboard/CoverageDataTests.cs

**Checkpoint**: Coverage mind map renders with correct relationships and colors

---

## Phase 9: User Story 6 - Control Dashboard Access (Priority: P2)

**Goal**: Optional GitHub OAuth authentication for hosted dashboard

**Independent Test**: Deploy with auth enabled, verify unauthenticated users blocked

### Implementation for User Story 6

- [ ] T099 [US6] Create Cloudflare Pages middleware template in dashboard-site/functions/_middleware.js
- [ ] T100 [US6] Implement session cookie checking in middleware
- [ ] T101 [US6] Implement GitHub OAuth redirect flow in middleware
- [ ] T102 [US6] Create OAuth callback handler in dashboard-site/functions/auth/callback.js
- [ ] T103 [US6] Implement repository access verification in callback handler
- [ ] T104 [US6] Implement access denied page template in dashboard-site/access-denied.html
- [ ] T105 [US6] Add auth configuration documentation to quickstart.md

**Checkpoint**: Authentication blocks unauthorized users, allows authorized users

---

## Phase 10: User Story 8 - Automate Dashboard Deployment (Priority: P3)

**Goal**: GitHub Action workflow to regenerate and deploy dashboard on changes

**Independent Test**: Commit test file change, verify workflow triggers and deploys

### Implementation for User Story 8

- [ ] T106 [US8] Create GitHub Action workflow template in .github/workflows/dashboard.yml.template
- [ ] T107 [US8] Implement workflow triggers for tests/** and reports/** paths
- [ ] T108 [US8] Add dashboard generation step using spectra CLI
- [ ] T109 [US8] Add Cloudflare Pages deployment step
- [ ] T110 [US8] Document workflow setup in quickstart.md

**Checkpoint**: Dashboard auto-deploys on test/report changes

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, error handling, and documentation

- [ ] T111 Handle empty repository state (no suites) with helpful message in DashboardGenerator
- [ ] T112 Handle missing/stale indexes with warnings in DataCollector
- [ ] T113 Handle missing automation directories gracefully in AutomationScanner
- [ ] T114 Handle malformed report files with skip and warn in DataCollector
- [ ] T115 [P] Add --dry-run flag to DashboardCommand for CI compatibility
- [ ] T116 [P] Add --verbose flag to coverage analysis for detailed output
- [ ] T117 Update CLAUDE.md with Phase 3 commands and patterns
- [ ] T118 Run quickstart.md validation manually

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **US1-Generate Dashboard (Phase 3)**: Depends on Foundational - core functionality
- **US2-Browse Tests (Phase 4)**: Depends on US1 (needs working dashboard)
- **US3-Execution History (Phase 5)**: Depends on US1 (needs working dashboard)
- **US5-Coverage Analysis (Phase 6)**: Depends on Foundational - independent of dashboard
- **US7-Export Report (Phase 7)**: Depends on US5 (needs coverage analysis)
- **US4-Coverage Visualization (Phase 8)**: Depends on US1 and US5 (needs dashboard and coverage data)
- **US6-Authentication (Phase 9)**: Depends on US1 (needs working dashboard)
- **US8-Auto Deploy (Phase 10)**: Depends on US1 (needs dashboard command)
- **Polish (Phase 11)**: Depends on all desired user stories being complete

### User Story Dependencies

```
US1 (Generate Dashboard) ──┬──► US2 (Browse Tests)
                           ├──► US3 (Execution History)
                           ├──► US4 (Coverage Visualization) ◄── US5
                           ├──► US6 (Authentication)
                           └──► US8 (Auto Deploy)

US5 (Coverage Analysis) ───┬──► US7 (Export Report)
                           └──► US4 (Coverage Visualization)
```

### Parallel Opportunities

**Phase 1 (Setup)**: T002-T010 can all run in parallel
**Phase 2 (Foundational)**: T012-T026 can all run in parallel (models)
**Phase 3+ (User Stories)**:
- US1: T042-T043 can run in parallel (CSS/JS)
- US5: T079-T081 can run in parallel (unit tests)
- After Phase 2: US1 and US5 can proceed in parallel (independent)

---

## Parallel Example: User Story 5 (Coverage Analysis)

```bash
# Launch all unit tests in parallel:
Task: "Add unit tests for AutomationScanner in tests/Spectra.Core.Tests/Coverage/AutomationScannerTests.cs"
Task: "Add unit tests for LinkReconciler in tests/Spectra.Core.Tests/Coverage/LinkReconcilerTests.cs"
Task: "Add unit tests for CoverageCalculator in tests/Spectra.Core.Tests/Coverage/CoverageCalculatorTests.cs"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: US1 - Generate Dashboard
4. **STOP and VALIDATE**: Test dashboard generation
5. Deploy/demo basic dashboard

### P1 Stories (Dashboard + Coverage)

1. Complete Setup + Foundational
2. Parallel work:
   - Stream A: US1 → US2 → US3 (Dashboard flow)
   - Stream B: US5 → US7 (Coverage analysis flow)
3. US4 (Coverage Visualization) after both streams complete

### Full Feature Set

1. P1 stories (Dashboard + Coverage)
2. P2 stories (US4 Visualization, US6 Auth, US7 Export)
3. P3 story (US8 Auto Deploy)
4. Polish phase

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- US1 and US5 can run in parallel (independent P1 stories)
- US4 (Visualization) needs both dashboard and coverage analysis complete
- Dashboard frontend uses vanilla JS + D3.js (no Node.js build)
- Coverage analysis is CLI-only, dashboard visualization is separate
- Stop at any checkpoint to validate independently
