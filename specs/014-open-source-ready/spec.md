# Feature Specification: Open Source Ready

**Feature Branch**: `014-open-source-ready`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Make SPECTRA open-source ready — README redesign, CI/CD pipelines, NuGet publishing, test health, documentation structure, licensing, and community templates."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - README Redesign (Priority: P1)

A developer discovers SPECTRA on GitHub. They land on the repository page and immediately understand what the tool does, why it's valuable, and how to get started — all from the README. The page looks professional with a banner, badges, feature icons, and copy-paste quickstart instructions.

**Why this priority**: The README is the first thing every visitor sees. A professional, visually appealing README is the single most impactful change for open-source adoption. It determines whether developers stay or bounce.

**Independent Test**: Visit the repository page on GitHub. Verify the banner image renders, all badges show live data (NuGet version, CI status, license), the value proposition is immediately clear, and the quickstart instructions work when followed exactly.

**Acceptance Scenarios**:

1. **Given** a user visits the SPECTRA GitHub repository, **When** the page loads, **Then** a professional banner image is displayed at the top, followed by a row of status badges (NuGet CLI, NuGet MCP, CI, License, .NET version).
2. **Given** a user reads the README, **When** they reach the "Why SPECTRA?" section, **Then** they see 6 value propositions with emoji icons explaining what makes SPECTRA unique.
3. **Given** a user reads the "Key Features" section, **When** they scan the features, **Then** each feature has a heading with icon, a brief description, and enough context to understand the capability without reading docs.
4. **Given** a user follows the "Quick Start" section, **When** they run the commands listed, **Then** they can install SPECTRA and generate their first test suite within 5 minutes.
5. **Given** the README has links to documentation pages, **When** a user clicks any link, **Then** it resolves to a real, non-empty page (no broken links).
6. **Given** the README includes an ecosystem section, **When** a user reads it, **Then** they understand how SPECTRA relates to BELLATRIX and Testimize.

---

### User Story 2 - CI Pipeline (Priority: P1)

A contributor opens a pull request. The CI pipeline automatically builds the project and runs all tests. The PR shows a green or red status check, and test results are available as downloadable artifacts.

**Why this priority**: CI is a prerequisite for accepting external contributions safely. Without it, there's no automated quality gate.

**Independent Test**: Open a PR against main. Verify the CI workflow triggers, builds in Release mode, runs all tests, and reports pass/fail status on the PR.

**Acceptance Scenarios**:

1. **Given** a developer pushes to main or opens a PR against main, **When** the push/PR is created, **Then** the CI pipeline triggers automatically.
2. **Given** the CI pipeline runs, **When** the build step executes, **Then** all projects compile successfully in Release configuration.
3. **Given** the CI pipeline runs, **When** the test step executes, **Then** all test projects run and results are reported.
4. **Given** any test fails, **When** the pipeline completes, **Then** the pipeline status is "failed" and the PR cannot be merged (when branch protection is enabled).
5. **Given** the pipeline completes (pass or fail), **When** a user checks artifacts, **Then** test result files are available for download.

---

### User Story 3 - NuGet Publishing (Priority: P2)

A maintainer pushes a version tag (e.g., `v1.0.0`). The publishing pipeline automatically builds, tests, packs, and pushes two NuGet packages: `Spectra.CLI` (global tool) and `Spectra.MCP` (server library).

**Why this priority**: NuGet publishing is how users install SPECTRA (`dotnet tool install -g Spectra.CLI`). Without it, users must build from source.

**Independent Test**: Push a version tag `v0.1.0-test`. Verify the pipeline runs, creates two .nupkg files, and (in a dry-run or test feed) would push them to NuGet.org.

**Acceptance Scenarios**:

1. **Given** a maintainer pushes a tag matching `v*`, **When** the tag push is detected, **Then** the NuGet publishing pipeline triggers.
2. **Given** the pipeline runs, **When** the pack step executes, **Then** two NuGet packages are produced: `Spectra.CLI` and `Spectra.MCP`.
3. **Given** the `Spectra.CLI` package is packed, **When** inspected, **Then** it is configured as a .NET global tool with the command name `spectra`.
4. **Given** both packages are packed, **When** the push step executes, **Then** packages are pushed to NuGet.org (requires `NUGET_API_KEY` secret).
5. **Given** a package version already exists on NuGet, **When** the push step executes, **Then** it skips the duplicate without failing.
6. **Given** the version tag is `v1.2.3`, **When** the packages are built, **Then** both packages have version `1.2.3`.

---

### User Story 4 - Fix All Failing Tests (Priority: P1)

A developer clones the repository and runs `dotnet test`. All tests pass on the first run without any external configuration or dependencies. This is the baseline quality bar for an open-source project.

**Why this priority**: Same level as CI — failing tests on clone erode trust and block contributions. A 100% green test suite is non-negotiable for open-source credibility.

**Independent Test**: Clone the repository on a clean machine with only .NET 8 SDK installed. Run `dotnet test`. Verify 100% pass rate with zero skipped tests.

**Acceptance Scenarios**:

1. **Given** a developer clones the repository, **When** they run `dotnet test`, **Then** all tests pass (exit code 0).
2. **Given** tests were previously failing due to missing configuration, **When** tests are fixed, **Then** they use self-contained test fixtures with embedded default configuration.
3. **Given** tests were previously failing due to external dependencies, **When** tests are fixed, **Then** external dependencies are mocked or stubbed.
4. **Given** any test is marked as skipped, **When** the test suite is reviewed, **Then** each skipped test has a documented reason in a code comment explaining why and when it can be re-enabled.

---

### User Story 5 - License and Legal (Priority: P2)

The repository has a clear MIT license file at the root. Contributors and users know they can use, modify, and distribute SPECTRA freely.

**Why this priority**: No license = no open-source. MIT is the most permissive and widely adopted choice for .NET tools.

**Independent Test**: Verify LICENSE file exists at repo root, contains valid MIT license text with the correct copyright holder, and the README links to it.

**Acceptance Scenarios**:

1. **Given** a user visits the repository, **When** they look for license information, **Then** a LICENSE file exists at the repo root with MIT license text.
2. **Given** the LICENSE file exists, **When** a user reads it, **Then** it contains the correct copyright year and organization name.
3. **Given** the README has a license badge, **When** a user clicks it, **Then** it links to the LICENSE file.

---

### User Story 6 - Documentation Structure (Priority: P2)

All documentation is organized in a `docs/` folder with a clear hierarchy. Every link in the README and docs resolves to a real file. A developer can find information about any SPECTRA feature through the documentation.

**Why this priority**: Documentation is the second most important factor (after README) for open-source adoption. Organized docs reduce support burden and enable self-service.

**Independent Test**: Click every link in README.md and every cross-link in docs/ files. Verify all resolve to real, non-empty pages.

**Acceptance Scenarios**:

1. **Given** the docs/ folder exists, **When** a developer browses it, **Then** files are organized by topic: getting started, CLI reference, configuration, test format, coverage, profiles, verification, deployment, architecture, and development guide.
2. **Given** the README links to documentation pages, **When** each link is followed, **Then** it resolves to a real file with substantive content (not stubs).
3. **Given** a new contributor wants to build locally, **When** they read CONTRIBUTING.md or docs/DEVELOPMENT.md, **Then** they find step-by-step build and test instructions.

---

### User Story 7 - Community Templates and Tooling (Priority: P3)

The repository has GitHub issue templates, PR template, contributing guide, editor configuration, and dependency management. These reduce friction for external contributors.

**Why this priority**: Nice-to-have polish that makes the project feel well-maintained. Lower priority than core quality (tests, CI, README) but important for contributor experience.

**Independent Test**: Create a new issue on GitHub. Verify issue templates appear (bug report, feature request). Open a PR and verify the PR template appears with a checklist.

**Acceptance Scenarios**:

1. **Given** a user creates a new issue, **When** the issue form loads, **Then** they can choose between "Bug Report" and "Feature Request" templates with guided fields.
2. **Given** a contributor opens a PR, **When** the PR form loads, **Then** a checklist template appears with items: tests pass, documentation updated, no breaking changes.
3. **Given** a contributor opens the project in any editor, **When** they edit code, **Then** the editor configuration file enforces consistent formatting (indentation, line endings, charset).
4. **Given** Dependabot is configured, **When** a NuGet dependency has an update, **Then** Dependabot automatically creates a PR with the update.
5. **Given** a potential contributor reads CONTRIBUTING.md, **When** they follow the guide, **Then** they can build, test, and submit a PR without asking for help.

---

### Edge Cases

- What happens when the NuGet API key secret is not configured? The publish pipeline fails with a clear error message indicating the missing secret, without exposing any sensitive information.
- What happens when a CI build fails on a PR from a fork? The CI pipeline runs but secrets are not available to fork PRs (standard GitHub security). Build and test steps still work since they don't need secrets.
- What happens when a contributor clones on Windows vs Linux vs macOS? All tests must pass cross-platform. Path handling uses forward slashes or platform-agnostic APIs.
- What happens when the banner image file is missing from assets/? The README degrades gracefully — the text content is still readable without the image.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Repository MUST have a professionally designed README.md with banner image, badge row, value proposition, feature showcase, quickstart guide, architecture diagram, ecosystem table, documentation links, and license information.
- **FR-002**: Repository MUST have a CI pipeline that triggers on push to main and all PRs, building all projects and running all tests in Release configuration.
- **FR-003**: CI pipeline MUST upload test results as downloadable artifacts.
- **FR-004**: Repository MUST have a NuGet publishing pipeline that triggers on version tags (`v*`), producing and pushing `Spectra.CLI` and `Spectra.MCP` packages.
- **FR-005**: `Spectra.CLI` MUST be packaged as a .NET global tool with command name `spectra`.
- **FR-006**: Package version MUST be derived from the git tag (e.g., tag `v1.2.3` produces version `1.2.3`).
- **FR-007**: All tests MUST pass on a clean clone with only .NET 8 SDK installed — no external configuration or services required.
- **FR-008**: Repository MUST have an MIT LICENSE file at the root with correct copyright information.
- **FR-009**: Documentation MUST be organized in a `docs/` folder with files covering: getting started, CLI reference, configuration, test format, coverage, profiles, verification, deployment, architecture, and development.
- **FR-010**: Every link in README.md and docs/ files MUST resolve to a real, non-empty target.
- **FR-011**: Repository MUST have GitHub issue templates (bug report, feature request) and a PR template with a review checklist.
- **FR-012**: Repository MUST have a CONTRIBUTING.md with build instructions, test instructions, code style guidelines, and PR process.
- **FR-013**: Repository MUST have an .editorconfig file for consistent code formatting.
- **FR-014**: Repository MUST have Dependabot configuration for NuGet dependency updates.
- **FR-015**: All CI/CD pipelines MUST not expose secrets in logs or artifact outputs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet test` achieves 100% pass rate on a clean clone (zero failures, zero unexplained skips).
- **SC-002**: CI pipeline completes in under 10 minutes for build + test.
- **SC-003**: A new developer can install SPECTRA and generate their first test suite within 5 minutes by following the README quickstart.
- **SC-004**: 100% of links in README.md and docs/ files resolve to valid targets (no 404s, no dead links).
- **SC-005**: NuGet packages can be installed via `dotnet tool install -g Spectra.CLI` after publishing.
- **SC-006**: The repository passes the "30-second test" — a developer visiting for the first time understands what SPECTRA does within 30 seconds of reading the README.

## Assumptions

- The banner image will be provided or created as `assets/spectra_github_readme_banner.png` — the implementation creates the assets/ directory and references it, but the actual image design is handled separately.
- NuGet.org publishing requires a `NUGET_API_KEY` GitHub secret configured by a maintainer. The pipeline assumes this secret exists.
- The Testimize repository (https://github.com/AutomateThePlanet/Testimize) is used as a style reference for README layout, not as a functional dependency.
- Cross-platform test compatibility targets Windows, Linux, and macOS. CI runs on ubuntu-latest; developers may work on any OS.
- Documentation content (docs/) will be created as substantive guides, not stubs. Exact content depth is determined during implementation based on existing knowledge in the codebase.
- Branch protection rules are recommended in documentation but not enforced by this feature (that's a repo settings change, not a code change).

## Scope Boundaries

**In scope**:
- README.md full redesign with banner, badges, features, quickstart, ecosystem, docs links
- CI pipeline (.github/workflows/ci.yml) — build + test on push/PR
- NuGet publish pipeline (.github/workflows/publish.yml) — pack + push on tag
- Project file updates for NuGet packaging (PackAsTool, PackageId, etc.)
- Fix all failing tests to achieve 100% green
- MIT LICENSE file
- Documentation structure in docs/ with core guides
- CONTRIBUTING.md with build/test/style/PR instructions
- GitHub issue templates and PR template
- .editorconfig for code formatting
- Dependabot configuration

**Out of scope**:
- GitHub Pages or docs site (future enhancement)
- Banner image design (placeholder or existing image used)
- Branch protection rule configuration (documented as recommendation only)
- Code signing for NuGet packages
- Release notes automation (future enhancement)
- Changelog generation
- Security policy (SECURITY.md — future enhancement)
