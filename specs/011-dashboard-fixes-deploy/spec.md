# Feature Specification: Dashboard Improvements and Cloudflare Pages Deployment

**Feature Branch**: `011-dashboard-fixes-deploy`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "SPECTRA Dashboard Improvements and Cloudflare Pages Deployment"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Accurate Coverage Metrics in Dashboard (Priority: P1)

A QA lead opens the SPECTRA dashboard to assess how well their documentation is covered by tests. Currently, the documentation coverage section shows 0% even though tests reference documentation files via `source_refs`. The root cause is that `source_refs` entries include fragment anchors (e.g., `docs/auth.md#Login-Flow`) which fail to match the actual filesystem path (`docs/auth.md`). The coverage analyzer must strip fragment anchors before path comparison so that the dashboard displays truthful documentation coverage percentages.

**Why this priority**: Coverage metrics are the primary value proposition of the dashboard. If they show 0% when real coverage exists, the entire dashboard loses credibility and usefulness.

**Independent Test**: Can be fully tested by running `spectra ai analyze --coverage` on a project where test files have `source_refs` with fragment anchors, and verifying the documentation coverage percentage is non-zero and accurate.

**Acceptance Scenarios**:

1. **Given** test files with `source_refs` containing fragment anchors like `docs/checkout.md#Payment-Flow`, **When** the coverage analyzer runs, **Then** it matches the reference to the actual file `docs/checkout.md` and counts the test as covering that document.
2. **Given** test files with `source_refs` without fragment anchors like `docs/checkout.md`, **When** the coverage analyzer runs, **Then** matching behavior is unchanged and the test is correctly counted.
3. **Given** a `source_ref` pointing to a document that does not exist on the filesystem (with or without fragment), **When** the coverage analyzer runs, **Then** the reference is counted as unmatched and does not inflate coverage numbers.

---

### User Story 2 - Usable Dashboard Layout and Visualizations (Priority: P1)

A team member opens the dashboard to review test execution history and coverage. Currently, the pass rate trend chart dominates the viewport (60% height), the coverage tab shows irrelevant test filters, the treemap shows 0% automation for all suites despite accurate automation data elsewhere, and the coverage relationship graph is an unusable force-directed layout. The dashboard needs layout fixes and a replacement coverage visualization so that users can efficiently navigate and understand their project's test health.

**Why this priority**: Layout and visualization issues make the dashboard impractical for daily use. Users cannot extract insights from broken or misleading visualizations.

**Independent Test**: Can be tested by generating a dashboard with real project data and visually verifying: trend chart is compact, coverage tab has no sidebar filters, treemap reflects actual automation percentages, and the coverage relationship visualization shows a readable hierarchical tree.

**Acceptance Scenarios**:

1. **Given** a dashboard with run history data, **When** the user views the Run History tab, **Then** the trend chart occupies no more than 220px of vertical space and does not push other content below the fold.
2. **Given** a dashboard with only one execution run, **When** the user views the Run History tab, **Then** a compact summary card is shown instead of a nearly empty chart.
3. **Given** the user navigates to the Coverage tab, **When** the tab loads, **Then** no test filter sidebar (Priority, Component, Search, Tags) is visible and coverage content uses the full page width.
4. **Given** suites with varying automation percentages, **When** the user views the Suite Coverage Treemap, **Then** each suite tile reflects its actual automation percentage with appropriate color coding (not all red/0%).
5. **Given** coverage data linking documents to tests and automation, **When** the user views the Coverage Relationships section, **Then** a hierarchical tree visualization appears showing the flow from documentation domains to features to test areas to individual tests.
6. **Given** the hierarchical coverage tree, **When** the user clicks a document node, **Then** it expands to show linked test cases grouped by component, with each node displaying coverage percentage and test count.
7. **Given** tests with no `source_refs`, **When** the coverage tree renders, **Then** those tests appear under an "Unlinked Tests" group.

---

### User Story 3 - Automated Dashboard Deployment (Priority: P2)

A team lead wants the dashboard to automatically update whenever test data changes in the repository. They set up a continuous deployment pipeline that rebuilds and publishes the dashboard to a hosted URL when changes are pushed to test files, documentation, execution data, or configuration. This eliminates the manual step of running `spectra dashboard` and uploading the output.

**Why this priority**: Automation ensures the dashboard is always current without manual intervention. However, it requires the dashboard itself to be accurate first (P1 stories).

**Independent Test**: Can be tested by pushing a change to a test file on the main branch and verifying that the deployment pipeline triggers, the dashboard is rebuilt, and the updated site is accessible at the hosted URL.

**Acceptance Scenarios**:

1. **Given** a repository with the deployment workflow configured, **When** a commit is pushed to the main branch that modifies files in `tests/`, `docs/`, `.execution/`, or `spectra.config.json`, **Then** the deployment pipeline triggers automatically.
2. **Given** the deployment pipeline triggers, **When** it completes successfully, **Then** the dashboard is published and accessible at the configured hosting URL.
3. **Given** the deployment pipeline, **When** a user triggers the workflow manually via the repository's Actions UI, **Then** it runs the same build and deploy steps as an automatic trigger.
4. **Given** changes only to files outside the trigger paths (e.g., source code in `src/`), **When** a commit is pushed, **Then** the deployment pipeline does not trigger.

---

### User Story 4 - Authenticated Dashboard Access (Priority: P2)

A team lead wants to restrict dashboard access to authorized team members. The hosted dashboard requires authentication via GitHub OAuth, verifying that the visitor has read access to the project's repository. Unauthenticated visitors are redirected to authenticate, and users without repository access see an access-denied page.

**Why this priority**: Access control ensures sensitive project metrics are only visible to authorized team members. Required for any production deployment of the dashboard.

**Independent Test**: Can be tested by visiting the hosted dashboard URL without authentication and verifying redirect to GitHub login, then authenticating with a user who has repo access and verifying dashboard loads, then authenticating with a user who lacks repo access and verifying the access-denied page appears.

**Acceptance Scenarios**:

1. **Given** an unauthenticated visitor, **When** they access the dashboard URL, **Then** they are redirected to GitHub's OAuth authorization page.
2. **Given** a user who completes GitHub authentication and has read access to an allowed repository, **When** the OAuth callback completes, **Then** they are redirected to the dashboard with a valid session.
3. **Given** a user who completes GitHub authentication but does NOT have access to any allowed repository, **When** the OAuth callback completes, **Then** they see the access-denied page with a clear explanation.
4. **Given** a user with a valid session, **When** they return to the dashboard within the session duration, **Then** they can access the dashboard without re-authenticating.
5. **Given** a user whose session has expired, **When** they access the dashboard, **Then** they are redirected to re-authenticate.

---

### User Story 5 - Dashboard Deployment Setup Guide (Priority: P2)

A QA lead or developer who has never used the hosting platform needs to set up the entire deployment pipeline from scratch. They need a self-sufficient step-by-step guide that walks them through creating the hosting project, configuring authentication, setting up secrets, and verifying the deployment — without requiring prior knowledge of the hosting platform.

**Why this priority**: Without clear documentation, the deployment setup becomes a barrier to adoption. The guide must be written for the least experienced expected user.

**Independent Test**: Can be tested by having a person unfamiliar with the hosting platform follow the guide from start to finish and successfully deploy a working, authenticated dashboard.

**Acceptance Scenarios**:

1. **Given** a user with no prior hosting platform experience, **When** they follow the setup guide from beginning to end, **Then** they have a working dashboard deployment with authentication enabled.
2. **Given** the setup guide, **When** a user encounters a common error (wrong callback URL, misconfigured secrets, token permission issues), **Then** the troubleshooting section describes the symptom and resolution.
3. **Given** the guide references secrets or tokens, **When** the user reads the guide, **Then** no actual secret values are present — only placeholder names and instructions for where to generate/configure them.

---

### User Story 6 - Project Initialization Includes Deployment Scaffolding (Priority: P3)

When a new project is initialized with SPECTRA, the initialization process should include the deployment workflow file and inform the user about the deployment setup guide. This reduces friction for teams that want continuous deployment from day one.

**Why this priority**: Nice-to-have convenience. Teams can always add the workflow manually. Including it in init just reduces one step.

**Independent Test**: Can be tested by running `spectra init` in a new project directory and verifying the deployment workflow file is created and the user sees a message pointing to the deployment setup guide.

**Acceptance Scenarios**:

1. **Given** a new project directory, **When** `spectra init` runs, **Then** a deployment workflow file is created in the expected CI/CD directory.
2. **Given** `spectra init` completes, **When** the user reviews the console output, **Then** they see a message directing them to the deployment setup documentation.
3. **Given** the generated workflow file, **When** a user inspects its contents, **Then** no secrets or tokens are embedded — only references to secret names that must be configured separately.

---

### User Story 7 - Dashboard Configuration (Priority: P3)

A user wants to customize dashboard behavior — output directory, hosting project name, which sections to include, and how many historical data points to display. These options are configured in the project's configuration file and respected by both the CLI dashboard command and the deployment pipeline.

**Why this priority**: Configuration flexibility is valuable but the dashboard works with sensible defaults. This is an enhancement to existing functionality.

**Independent Test**: Can be tested by modifying dashboard configuration values and verifying the generated dashboard reflects the changes (correct output path, correct number of trend points, sections included/excluded).

**Acceptance Scenarios**:

1. **Given** a configuration with `include_coverage` set to false, **When** the dashboard is generated, **Then** the coverage section is omitted from the output.
2. **Given** a configuration with `max_trend_points` set to 10, **When** the dashboard is generated with 30 historical runs, **Then** only the 10 most recent data points appear in the trend chart.
3. **Given** a configuration with a custom `cloudflare_project_name`, **When** the deployment pipeline runs, **Then** it deploys to the project matching that name.

---

### Edge Cases

- What happens when a test file has a `source_ref` pointing to a document path that exists but with different casing (e.g., `docs/Auth.md` vs `docs/auth.md`)? The comparison should be case-insensitive on case-insensitive filesystems.
- What happens when the coverage tree visualization has hundreds of test nodes? The tree should remain navigable with collapsed-by-default behavior and smooth expand/collapse.
- What happens when there are zero execution runs? The trend chart section should show an appropriate empty state message instead of a blank chart.
- What happens when no documentation files exist in the project? The coverage tree should show an empty state with guidance on adding documentation.
- What happens when the deployment pipeline runs but the hosting platform credentials are misconfigured? The pipeline should fail with a clear error message identifying which credential is missing or invalid.
- What happens when the session secret is rotated? All existing sessions should become invalid, requiring users to re-authenticate (expected behavior, documented in troubleshooting).
- What happens when `ALLOWED_REPOS` contains repositories the OAuth app doesn't have access to? The middleware should still check the user's access via the provider's API; the OAuth app permissions are separate from user permissions.

## Requirements *(mandatory)*

### Functional Requirements

**Coverage Accuracy**

- **FR-001**: The coverage analyzer MUST strip fragment anchors (everything after and including `#`) from `source_refs` before comparing against filesystem document paths.
- **FR-002**: The coverage analyzer MUST use case-insensitive path comparison when matching `source_refs` to document files.
- **FR-003**: The dashboard's treemap visualization MUST use the same data source as the automation coverage summary card, ensuring consistent percentages.

**Dashboard Layout and Visualizations**

- **FR-004**: The pass rate trend chart MUST have a maximum height of 220 pixels.
- **FR-005**: When only one data point exists in run history, the trend section MUST display a compact summary card instead of a chart.
- **FR-006**: The test filter sidebar (Priority, Component, Search, Tags) MUST be hidden on the Coverage tab and MUST only appear on the Suites and Tests tabs.
- **FR-007**: When filters are hidden, the main content area MUST expand to use the full page width.
- **FR-008**: The coverage relationships visualization MUST use a hierarchical tree layout (not force-directed graph) with four levels: domain (doc folder), feature (doc file), area (test component), and test (individual test case).
- **FR-009**: The coverage tree MUST start collapsed to the document level and support click-to-expand for deeper levels.
- **FR-010**: Document nodes in the coverage tree MUST display: short filename, linked test count, automation percentage, and a mini progress bar.
- **FR-011**: Test nodes in the coverage tree MUST display: test ID, short title, and an icon indicating automated or manual status.
- **FR-012**: The coverage tree MUST include an "Unlinked Tests" group for tests without `source_refs`.
- **FR-013**: The coverage tree MUST support zoom, pan, and Expand All / Collapse All controls.

**Styling**

- **FR-014**: All dashboard cards MUST have consistent border-radius, shadow, and padding.
- **FR-015**: Coverage progress bars MUST use color coding: green for >= 80%, yellow for >= 50%, red for < 50%.
- **FR-016**: The active navigation tab MUST be clearly distinguished from inactive tabs.

**Automated Deployment**

- **FR-017**: A CI/CD workflow MUST trigger on pushes to the main branch when files change in `tests/`, `.execution/`, `docs/`, or `spectra.config.json`.
- **FR-018**: The workflow MUST also support manual triggering.
- **FR-019**: The workflow MUST run coverage analysis, generate the dashboard, and deploy the output to the hosting platform.
- **FR-020**: The hosting platform project name MUST be configurable via the dashboard configuration section.

**Authentication**

- **FR-021**: The dashboard hosting MUST support optional GitHub OAuth authentication.
- **FR-022**: Unauthenticated visitors MUST be redirected to GitHub's OAuth authorization flow.
- **FR-023**: After successful authentication, the system MUST verify the user has read access to at least one allowed repository.
- **FR-024**: Users who fail the repository access check MUST see an access-denied page with a clear explanation.
- **FR-025**: Authenticated sessions MUST be stored in encrypted/signed cookies with a configurable expiration period.
- **FR-026**: Static assets (stylesheets, scripts, images) MUST be served without requiring authentication.

**Documentation**

- **FR-027**: A deployment setup guide MUST provide step-by-step instructions covering: hosting project creation, API token generation, OAuth app creation, repository secrets configuration, environment variables configuration, first deployment verification, and troubleshooting.
- **FR-028**: The guide MUST NOT contain any actual secrets, tokens, or credentials — only placeholder names and generation instructions.

**Initialization**

- **FR-029**: The `spectra init` command MUST create a CI/CD workflow file for dashboard deployment.
- **FR-030**: The `spectra init` command MUST display a message directing users to the deployment setup guide.
- **FR-031**: Generated workflow files MUST NOT contain any secrets or tokens.

**Configuration**

- **FR-032**: The dashboard configuration section MUST support: `output_dir`, `cloudflare_project_name`, `include_coverage`, `include_runs`, and `max_trend_points`.
- **FR-033**: All configuration fields MUST have sensible default values that produce a working dashboard without explicit configuration.

### Key Entities

- **Coverage Report**: Aggregated coverage data with three sections (documentation, requirements, automation) and per-item detail breakdowns.
- **Dashboard Data**: Serialized JSON bundle containing suite statistics, test entries, run history, coverage summaries, and trend data — embedded in the dashboard HTML for client-side rendering.
- **Coverage Tree Node**: A hierarchical node representing a domain (doc folder), feature (doc file), area (test component group), or individual test — with computed coverage percentage and automation status.
- **Session Token**: A signed/encrypted token stored as a cookie, containing user identity, expiration timestamp, and authorization status — used to maintain authenticated sessions.
- **Dashboard Configuration**: Project-level settings controlling dashboard output path, hosting project name, included sections, and trend data limits.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Documentation coverage percentage reported by the analyzer matches the actual ratio of documents referenced by tests (accounting for fragment anchors), with zero false negatives caused by anchor suffixes.
- **SC-002**: The pass rate trend chart occupies no more than 220px of vertical space, and with a single data point, displays a compact card instead of a chart.
- **SC-003**: 100% of coverage tab views show full-width content with no test filter sidebar visible.
- **SC-004**: Treemap automation percentages match the corresponding automation coverage card values within 1% for every suite.
- **SC-005**: The coverage tree visualization renders a readable hierarchical layout where all document labels are fully visible without overlapping at the default zoom level.
- **SC-006**: A first-time user can complete the full deployment setup (from zero to working authenticated dashboard) by following only the setup guide, without external documentation.
- **SC-007**: Dashboard deployment triggers automatically on relevant file changes and the updated dashboard is accessible within 5 minutes of the push.
- **SC-008**: Authenticated users with repository access can view the dashboard; users without access see the access-denied page — with zero exceptions.
- **SC-009**: `spectra init` produces a complete deployment workflow file and displays the setup guide reference, with no manual file creation needed.

## Assumptions

- The hosting platform for deployment is Cloudflare Pages, chosen for its free tier, edge network performance, and native support for serverless functions (used for OAuth middleware).
- GitHub OAuth is the sole authentication provider. Alternative providers (Google, SAML, etc.) are out of scope.
- The deployment workflow uses GitHub Actions as the CI/CD platform, consistent with the project's existing GitHub-based tooling.
- Session duration is 24 hours. The existing implementation uses 7 days, which will be reduced to match the security requirement of shorter-lived sessions.
- The `ALLOWED_REPOS` environment variable uses comma-separated `org/repo` format for specifying multiple authorized repositories.
- The coverage tree visualization uses D3.js, which is already a dependency for the existing treemap visualization.
- The deployment guide targets users with basic familiarity with GitHub (repositories, settings, Actions) but no Cloudflare experience.
