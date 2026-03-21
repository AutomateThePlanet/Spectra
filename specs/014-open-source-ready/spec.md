# Feature Specification: Open-Source Readiness

**Feature Branch**: `014-open-source-ready`
**Created**: 2026-03-21
**Status**: Draft
**Input**: User description: "Make SPECTRA open-source ready. This covers README redesign, CI/CD pipelines, NuGet publishing, test health, documentation structure, and licensing."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover and Evaluate SPECTRA (Priority: P1)

A potential user finds the SPECTRA GitHub repository and needs to quickly understand what SPECTRA does, why it matters, and whether it fits their needs. The README must communicate the value proposition clearly with visual appeal — banner image, status badges, emoji-driven feature showcase, and copy-paste quick start instructions — following the Testimize repo style.

**Why this priority**: First impressions determine whether a visitor clones the repo or moves on. The README is the single most important asset for open-source adoption.

**Independent Test**: Can be tested by viewing the README on GitHub and confirming it renders correctly with banner, badges, value proposition, feature details, quick start, architecture diagram, and ecosystem context.

**Acceptance Scenarios**:

1. **Given** a visitor lands on the GitHub repository, **When** they view the README, **Then** they see a centered banner image, status badges (NuGet versions, CI status, license, .NET version), and a one-line value proposition.
2. **Given** a visitor is evaluating SPECTRA, **When** they scroll through the README, **Then** they find a "Why SPECTRA?" section with 6 key differentiators using emoji icons and concise descriptions.
3. **Given** a visitor wants to try SPECTRA, **When** they reach the Quick Start section, **Then** they find copy-paste-ready commands to install and run SPECTRA in under 5 minutes.
4. **Given** a visitor wants to understand SPECTRA's ecosystem, **When** they view the README, **Then** they find an ecosystem table showing BELLATRIX, Testimize, and SPECTRA with their roles and links.
5. **Given** a visitor wants detailed documentation, **When** they find the documentation links table, **Then** every link resolves to an existing file in the `docs/` folder.

---

### User Story 2 - CI Pipeline Validates Every Change (Priority: P1)

A contributor pushes code or opens a pull request. An automated CI pipeline builds the project, runs all tests, and reports results. The contributor and maintainers see immediately whether the change is safe to merge.

**Why this priority**: CI is foundational for open-source trust. Contributors need fast feedback and maintainers need confidence that PRs don't break the build.

**Independent Test**: Can be tested by pushing a commit to a branch and verifying the GitHub Actions workflow triggers, builds successfully, runs tests, and uploads test result artifacts.

**Acceptance Scenarios**:

1. **Given** a contributor pushes to main or opens a PR targeting main, **When** the push/PR event fires, **Then** a CI workflow triggers that restores, builds (Release configuration), and runs all tests.
2. **Given** the CI workflow completes, **When** any test fails, **Then** the workflow fails and test result artifacts (TRX format) are uploaded for inspection.
3. **Given** the CI workflow completes successfully, **When** all tests pass, **Then** the workflow shows a green check and test results are available as downloadable artifacts.

---

### User Story 3 - Publish NuGet Packages on Release (Priority: P1)

A maintainer tags a release (e.g., `v1.11.0`). The publish workflow automatically builds, tests, packs, and publishes two NuGet packages — `Spectra.CLI` as a dotnet global tool and `Spectra.MCP` as a standalone execution server — to nuget.org.

**Why this priority**: NuGet publishing is the primary distribution mechanism. Users install SPECTRA via `dotnet tool install`, so packages must be published reliably on every release.

**Independent Test**: Can be tested by creating a version tag and verifying the workflow packs both projects and pushes to NuGet (or a test feed).

**Acceptance Scenarios**:

1. **Given** a maintainer pushes a tag matching `v*`, **When** the publish workflow triggers, **Then** it builds, tests, packs both `Spectra.CLI` and `Spectra.MCP`, and pushes `.nupkg` files to nuget.org.
2. **Given** the version tag is `v1.11.0`, **When** the packages are packed, **Then** the package version matches `1.11.0` (tag without the `v` prefix).
3. **Given** a package version already exists on nuget.org, **When** the push is attempted, **Then** it skips the duplicate without failing the workflow.

---

### User Story 4 - All Tests Pass (Priority: P1)

A contributor clones the repo and runs `dotnet test`. All tests pass with zero failures. No tests are skipped without a documented reason.

**Why this priority**: 100% green tests are a basic credibility signal. Failing tests in an open-source repo signal abandonment or poor quality.

**Independent Test**: Can be tested by running `dotnet test` on a fresh clone and verifying all tests pass.

**Acceptance Scenarios**:

1. **Given** a contributor clones the repo, **When** they run `dotnet test`, **Then** all tests pass with zero failures across all three test projects.
2. **Given** a test was previously failing due to path issues, missing fixtures, or external dependencies, **When** the fix is applied, **Then** the test passes reliably across multiple runs.
3. **Given** a test cannot be stabilized, **When** it is skipped, **Then** it has a skip attribute with a clear explanation of why.

---

### User Story 5 - Contributor Onboarding (Priority: P2)

A new contributor wants to contribute to SPECTRA. They find clear instructions for building locally, running tests, code style conventions, and the PR process. Issue templates and PR templates guide them through the workflow.

**Why this priority**: Contributor friction is the biggest barrier to open-source participation. Clear templates and docs reduce the barrier to entry.

**Independent Test**: Can be tested by following the CONTRIBUTING.md instructions on a fresh machine and successfully building and testing the project.

**Acceptance Scenarios**:

1. **Given** a new contributor reads CONTRIBUTING.md, **When** they follow the build instructions, **Then** they can build and run all tests successfully.
2. **Given** a contributor opens a new issue, **When** they see the issue template chooser, **Then** they can select between "Bug Report" and "Feature Request" with guided fields.
3. **Given** a contributor opens a PR, **When** the PR form loads, **Then** they see a checklist template covering tests, documentation, and breaking changes.
4. **Given** contributors use different editors, **When** they open any source file, **Then** the `.editorconfig` enforces consistent indentation, encoding, and line endings.

---

### User Story 6 - Documentation is Complete and Navigable (Priority: P2)

A user or contributor needs to understand a specific SPECTRA feature. They find documentation in a well-organized `docs/` folder with proper cross-linking from the README. Every link resolves to a real file with meaningful content.

**Why this priority**: Broken links and missing docs erode trust. Complete documentation demonstrates project maturity.

**Independent Test**: Can be tested by clicking every documentation link in the README and verifying each resolves to an existing file with meaningful content.

**Acceptance Scenarios**:

1. **Given** the README contains documentation links, **When** a user clicks any link, **Then** it resolves to an existing markdown file in the `docs/` folder.
2. **Given** a user navigates the docs folder, **When** they browse the directory, **Then** they find a logical structure covering getting started, CLI reference, configuration, test format, coverage, profiles, grounding, document index, deployment, execution agent, and architecture.

---

### User Story 7 - License and Legal Clarity (Priority: P2)

A potential adopter needs to verify the licensing terms before using SPECTRA. They find a clear MIT license file at the repository root, and the README badge links to it.

**Why this priority**: License clarity is a prerequisite for enterprise adoption and open-source contribution.

**Independent Test**: Can be tested by verifying the LICENSE file exists at the repo root with MIT license text and the README badge links to it.

**Acceptance Scenarios**:

1. **Given** a user checks the repository, **When** they look for licensing information, **Then** they find a LICENSE file at the repository root with MIT license text.
2. **Given** the README has a license badge, **When** a user clicks the badge, **Then** it links to the LICENSE file.

---

### User Story 8 - Automated Dependency Updates (Priority: P3)

Dependencies stay current through automated Dependabot PRs for NuGet packages on a weekly schedule.

**Why this priority**: Keeping dependencies updated prevents security vulnerabilities and reduces upgrade debt over time.

**Independent Test**: Can be tested by verifying the Dependabot configuration file exists and is correctly formatted.

**Acceptance Scenarios**:

1. **Given** the repository has a Dependabot configuration, **When** a new NuGet package version is available, **Then** Dependabot creates a PR with the update on a weekly schedule.

---

### Edge Cases

- What happens when the banner image file is missing? The README should still be readable (alt text fallback).
- What happens when NuGet push fails due to network issues? The workflow should fail clearly with actionable error messages.
- What happens when a contributor runs tests on a different OS (Windows vs Linux vs macOS)? Tests should pass cross-platform, or platform-specific tests should be clearly marked.
- What happens when the version tag format is wrong (e.g., `release-1.0` instead of `v1.0`)? The publish workflow should only trigger on `v*` tags and ignore other tags.
- What happens when a contributor opens an issue without using a template? GitHub's template chooser should guide them, but not block free-form issues entirely.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Repository MUST have a README.md with centered banner image, status badges row (NuGet CLI, NuGet MCP, CI status, license, .NET version), one-line value proposition, "Why SPECTRA?" section with 6 emoji-driven differentiators, Key Features section with detailed descriptions, Quick Start section with copy-paste commands, architecture diagram, ecosystem table, documentation links table, project status section, and contributing/license sections.
- **FR-002**: Repository MUST have a GitHub Actions CI workflow (`.github/workflows/ci.yml`) that triggers on push to main and on all PRs targeting main, restoring dependencies, building in Release configuration, running all tests with TRX logging, and uploading test results as artifacts.
- **FR-003**: Repository MUST have a GitHub Actions publish workflow (`.github/workflows/publish.yml`) that triggers on `v*` tags, builds, tests, packs both `Spectra.CLI` and `Spectra.MCP` as NuGet packages, and pushes them to nuget.org with skip-duplicate enabled.
- **FR-004**: All tests across Spectra.Core.Tests, Spectra.CLI.Tests, and Spectra.MCP.Tests MUST pass with zero failures when running `dotnet test`.
- **FR-005**: Repository MUST have a LICENSE file at the root containing MIT license text with correct copyright holder and year.
- **FR-006**: Repository MUST have issue templates in `.github/ISSUE_TEMPLATE/` for bug reports (`bug_report.md`) and feature requests (`feature_request.md`) with guided fields.
- **FR-007**: Repository MUST have a PR template (`.github/PULL_REQUEST_TEMPLATE.md`) with a checklist covering tests passing, documentation updated, and breaking changes documented.
- **FR-008**: CONTRIBUTING.md MUST include sections for building locally, running tests, code style guidelines (referencing .editorconfig), and the PR process.
- **FR-009**: Repository MUST have an `.editorconfig` enforcing consistent code style (indentation, encoding, line endings, trailing whitespace).
- **FR-010**: Repository MUST have a Dependabot configuration (`.github/dependabot.yml`) for weekly NuGet package updates.
- **FR-011**: All documentation files referenced in README.md MUST exist in the `docs/` folder with meaningful content (not stubs).
- **FR-012**: NuGet package version MUST be derived from the git tag in the publish workflow, with `PackAsTool=true` and correct `ToolCommandName` values (`spectra` for CLI, `spectra-mcp` for MCP).
- **FR-013**: CI workflow MUST upload test results (TRX format) as downloadable artifacts regardless of test outcome.
- **FR-014**: The README MUST include a documentation links table with entries for all docs in the `docs/` folder.

### Key Entities

- **README.md**: Primary landing page for the repository containing all marketing, onboarding, and navigation content.
- **CI Workflow**: GitHub Actions workflow for continuous integration — build and test on every push/PR.
- **Publish Workflow**: GitHub Actions workflow for NuGet package publishing triggered by version tags.
- **NuGet Packages**: Two distributable packages — `Spectra.CLI` (global tool) and `Spectra.MCP` (execution server).
- **Documentation Set**: Collection of markdown files in `docs/` covering all SPECTRA features and workflows.
- **Contributor Templates**: Issue templates, PR template, CONTRIBUTING.md, and .editorconfig for contributor onboarding.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build --configuration Release` completes with zero errors across all projects.
- **SC-002**: `dotnet test` passes all tests (984+) with zero failures across all three test projects.
- **SC-003**: Every hyperlink in README.md resolves to an existing file or valid external URL.
- **SC-004**: `dotnet pack` successfully produces `.nupkg` files for both Spectra.CLI and Spectra.MCP.
- **SC-005**: README renders correctly on GitHub with visible banner placeholder, clickable badges, formatted tables, and proper markdown rendering.
- **SC-006**: A new contributor can clone, build, and run tests within 5 minutes by following CONTRIBUTING.md instructions.
- **SC-007**: CI workflow definition is syntactically valid and references correct paths and .NET version.

## Assumptions

- The banner image (`assets/spectra_github_readme_banner.png`) will be provided or created separately — the README references it but image asset creation is outside this spec's scope.
- The repository is hosted on GitHub under the `AutomateThePlanet` organization.
- NuGet API key will be stored as a GitHub secret named `NUGET_API_KEY`.
- The existing `.editorconfig`, `CONTRIBUTING.md`, and `LICENSE` files may need updates but the files already exist.
- Version management uses `Directory.Build.props` or individual `.csproj` files — the publish workflow extracts version from the git tag.
- No license headers are added to individual source files — MIT license at repo root is sufficient for open-source compliance.
- The Testimize repo (github.com/AutomateThePlanet/Testimize) serves as the visual style reference for README design.
