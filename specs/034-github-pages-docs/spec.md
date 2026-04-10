# Feature Specification: GitHub Pages Documentation Site

**Feature Branch**: `034-github-pages-docs`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: Deploy `docs/` as a Just the Docs Jekyll site at `automatetheplanet.github.io/Spectra/` with ATP brand theme, sidebar navigation, search, and GitHub Actions auto-deploy.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Branded docs site with sidebar & search (Priority: P1)

A new SPECTRA user lands on the documentation site and can immediately discover the full set of guides via a persistent sidebar, search across all pages, and navigate between topics — all under the Automate The Planet visual identity.

**Why this priority**: Without this, every doc must be browsed individually on GitHub. Discoverability is the entire point of the feature.

**Independent Test**: Visit `https://automatetheplanet.github.io/Spectra/`, see the SPECTRA landing page, click any sidebar entry, search for "coverage" and find the Coverage Analysis page.

**Acceptance Scenarios**:

1. **Given** the site is deployed, **When** a user opens the root URL, **Then** they see the SPECTRA landing page with navigation to Getting Started and View on GitHub.
2. **Given** the user is on any page, **When** they type "coverage" into the search box, **Then** results from `coverage.md` appear with content snippets.
3. **Given** the user is on `getting-started`, **When** they click "Configuration" in the sidebar, **Then** the Configuration page loads correctly.

---

### User Story 2 - Auto-deploy on push to main (Priority: P1)

A maintainer edits a doc and pushes to `main`. Within a few minutes the site rebuilds automatically with no manual steps.

**Why this priority**: Documentation must stay in sync with the repo. Manual deploy steps cause drift.

**Independent Test**: Edit `docs/cli-reference.md`, push to `main`, observe the `Deploy Documentation` workflow run successfully and the change appear on the live site.

**Acceptance Scenarios**:

1. **Given** a doc file is changed and pushed to `main`, **When** the workflow runs, **Then** it builds and deploys the site successfully.
2. **Given** a non-doc file is changed and pushed, **When** GitHub processes the push, **Then** the docs workflow does NOT run.

---

### User Story 3 - ATP brand identity (Priority: P2)

The docs site visually aligns with other Automate The Planet projects: dark navy sidebar, teal accents, clean white content.

**Why this priority**: Brand alignment matters for credibility but the site is functional without it.

**Independent Test**: Visit any page, observe sidebar background is `#16213e`, links/buttons are teal, body background is near-white.

**Acceptance Scenarios**:

1. **Given** the site renders, **When** the user inspects sidebar styling, **Then** it uses the SPECTRA color scheme defined in `_sass/color_schemes/spectra.scss`.

---

### Edge Cases

- Machine-generated files (`_index.md`, `*.criteria.yaml`, `_criteria_index.yaml`, `criteria/` directory) must NOT appear in site navigation if/when they exist.
- Subfolder docs (`docs/architecture/overview.md`, `docs/analysis/cli-vs-chat-generation.md`, `docs/deployment/*.md`, `docs/execution-agent/*.md`) must work correctly with `parent` frontmatter.
- Cross-doc relative links between files in different folders must continue to resolve.
- The workflow must not run on non-`docs/` changes (waste of CI time).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Repository MUST contain `docs/_config.yml` configuring the Just the Docs theme, site title "SPECTRA", description, base URL `/Spectra`, search, custom color scheme `spectra`, footer, aux links (GitHub, NuGet), `gh_edit_*` settings, and exclusion patterns for machine-generated files.
- **FR-002**: Repository MUST contain `docs/Gemfile` declaring `jekyll-seo-tag` and `just-the-docs` gems.
- **FR-003**: Repository MUST contain `docs/_sass/color_schemes/spectra.scss` overriding sidebar/body/link/code/header colors per the ATP palette.
- **FR-004**: Repository MUST contain `docs/index.md` as the landing page with `layout: home`, `nav_order: 0`, intro copy, quick install snippet, and links to Getting Started and GitHub.
- **FR-005**: Each existing user-facing doc file under `docs/` MUST have YAML frontmatter (`title`, `nav_order`, optional `parent`/`has_children`) inserted at the top, preserving existing content.
- **FR-006**: Section parent pages (`docs/user-guide.md`, `docs/deployment.md`, `docs/architecture.md`, `docs/execution-agents.md`) MUST exist with `has_children: true` to group related docs in the sidebar.
- **FR-007**: Repository MUST contain `.github/workflows/docs.yml` that builds Jekyll from `./docs` and deploys to GitHub Pages on push to `main` when files under `docs/` or the workflow itself change, plus `workflow_dispatch`.
- **FR-008**: The site build MUST exclude `_index.md`, `_index.yaml`, `_index.json`, `criteria/` directory, `**/*.criteria.yaml`, and `_criteria_index.yaml`.
- **FR-009**: `README.md` MUST be updated with a documentation site badge and a note pointing readers to `https://automatetheplanet.github.io/Spectra/`.
- **FR-010**: `CLAUDE.md` Recent Changes MUST list spec 034 with a complete summary line.
- **FR-011**: The site MUST navigate to the actual file locations in the repo — `docs/architecture/overview.md`, `docs/analysis/cli-vs-chat-generation.md`, `docs/deployment/cloudflare-pages-setup.md`, `docs/deployment/github-packages-setup.md`, `docs/execution-agent/*.md` — without moving files.

### Key Entities

- **Site config (`_config.yml`)**: Jekyll/Just the Docs settings, theme, branding, exclusions.
- **Color scheme (`spectra.scss`)**: SCSS variable overrides defining ATP palette.
- **Page frontmatter**: Per-file YAML block (`title`, `nav_order`, `parent`, `has_children`) controlling sidebar position.
- **Workflow (`docs.yml`)**: GitHub Actions pipeline building and publishing the site.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After merging to `main`, the `Deploy Documentation` GitHub Actions workflow completes successfully on first run.
- **SC-002**: All 17+ existing user-facing documentation files appear in the sidebar under the correct section (Top-level, User Guide, Deployment, Architecture, Execution Agents, Development) with no duplicates and no machine-generated files leaking in.
- **SC-003**: Searching the deployed site for any heading present in any doc returns that page in the search results.
- **SC-004**: Every page on the deployed site shows the SPECTRA color scheme (dark navy sidebar, teal links).
- **SC-005**: No existing test in the C# test suite fails (this change is documentation-only).

## Assumptions

- The GitHub Pages source for the repository will be set to "GitHub Actions" (a one-time manual repo setting; documented in the workflow file but not automatable from the workflow itself).
- Existing relative cross-doc links use `.md` extensions and resolve correctly under Just the Docs default behavior; broken links will be fixed reactively if discovered post-deploy.
- No SVG logo asset is required for v1 — the sidebar will fall back to the text title if `spectra-logo.svg` is absent. Logo/favicon can be added later without code changes.
- Files in `docs/execution-agent/` are user-facing and should be included in the site under a new "Execution Agents" section.
