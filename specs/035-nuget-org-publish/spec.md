# Feature Specification: Publish to NuGet.org

**Feature Branch**: `035-nuget-org-publish`
**Created**: 2026-04-10
**Status**: Draft
**Input**: User description: "Move Spectra package distribution from GitHub Packages (private feed) to NuGet.org (public feed) so installation requires zero configuration."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Zero-config install for new users (Priority: P1)

A developer hears about SPECTRA, opens their terminal, runs a single `dotnet tool install` command against the default NuGet feed, and is ready to use the CLI within seconds — no personal access tokens, no custom feed configuration, no GitHub account required.

**Why this priority**: This is the entire reason for the feature. SPECTRA is now a public open-source project and the current GitHub Packages friction (PAT creation, source registration, secret management) is the single biggest barrier to first use. Without this, the open-source launch underperforms.

**Independent Test**: On a clean machine (or fresh container) with only the .NET SDK installed, run the documented install command and verify the CLI is available and runnable. No prior nuget source configuration should be needed.

**Acceptance Scenarios**:

1. **Given** a clean machine with the .NET 8 SDK installed and no Spectra-specific NuGet sources, **When** the user runs the documented install command, **Then** the Spectra CLI installs successfully from the default public feed and is invocable from any directory.
2. **Given** a user reading the project README or getting-started docs, **When** they follow the install instructions verbatim, **Then** they encounter no steps involving personal access tokens, GitHub authentication, or `dotnet nuget add source`.
3. **Given** an existing user who previously installed from GitHub Packages, **When** they update to the latest version, **Then** the update succeeds without re-authentication or feed reconfiguration.

---

### User Story 2 - Tag-driven release publishing (Priority: P1)

A maintainer cuts a release by pushing a version tag (e.g., `v1.36.0`). The release pipeline automatically builds, tests, packs, and publishes all three Spectra packages to the public feed using a single repository secret. The package version on the feed exactly matches the tag.

**Why this priority**: Without an automated, tag-driven publish path the team cannot ship reliably. Manual publishing is error-prone and risks inconsistent versions across the three packages.

**Independent Test**: Push a release tag to a test branch (or use a pre-release version) and verify the workflow completes end-to-end: build → test → pack → push, with all three packages appearing on the public feed at the tagged version.

**Acceptance Scenarios**:

1. **Given** a maintainer pushes a tag matching the release pattern, **When** the publish workflow runs, **Then** all three packages are built, all tests pass, and all three packages are pushed to the public feed at the tag's version.
2. **Given** the test step fails during a release, **When** the workflow evaluates results, **Then** no packages are pushed to the public feed.
3. **Given** a tag is re-pushed for a version that already exists on the feed, **When** the publish workflow runs, **Then** the workflow completes successfully without overwriting or failing on the duplicate.
4. **Given** the published packages, **When** a user views them on the feed, **Then** each shows accurate metadata: title, description, authors, license, project URL, repository link, tags, and embedded README.

---

### User Story 3 - Internal pipelines no longer depend on private feed credentials (Priority: P2)

The repository's own dashboard deployment pipeline installs the Spectra CLI using the same zero-config command an external user would use. No GitHub Packages secret is referenced anywhere in the workflows.

**Why this priority**: Removes a maintenance burden (rotating PATs), eliminates a class of secret-leak risk, and proves the public install path works under CI conditions identical to a real user.

**Independent Test**: Run the dashboard deployment workflow and verify it installs the CLI without referencing any GitHub Packages source or token.

**Acceptance Scenarios**:

1. **Given** the dashboard deployment workflow, **When** it executes the install step, **Then** it uses only the default public feed and no private-feed token.
2. **Given** the repository's secret store, **When** the GitHub Packages token is removed, **Then** no workflow fails because of the removal.

---

### User Story 4 - Documentation matches reality (Priority: P2)

A new user reading any official Spectra doc (README, getting started, etc.) sees install instructions consistent with the new public-feed flow, and finds no stale references to PAT setup or private-feed configuration.

**Why this priority**: Stale docs that show the old flow will send users down a broken path even after the technical change ships. Doc consistency is what makes the zero-config experience real.

**Independent Test**: Search all user-facing documentation for references to GitHub Packages, PATs, or custom NuGet sources related to installing Spectra. None should remain in the install path. Follow each doc's install instructions verbatim and confirm they work.

**Acceptance Scenarios**:

1. **Given** the README and getting-started guide, **When** a user reads the install section, **Then** they see only the simple public-feed install command with the correct package identifier casing.
2. **Given** the repository's documentation set, **When** searched for the obsolete GitHub Packages setup guide, **Then** no such file or link exists.
3. **Given** a contributor doing local development, **When** they read the development documentation, **Then** the local-build / local-install workflow (using a locally produced package) is preserved and still functions.

### Edge Cases

- What happens when a tag is pushed but tests fail? → No publish; the workflow fails and the feed is unchanged.
- What happens when the same version tag is re-pushed (e.g., after fixing the workflow)? → Publish is idempotent; the existing version on the feed is preserved and the workflow does not error on the duplicate.
- What happens when one of the three packages fails to push but the other two succeed? → The workflow reports failure; the partially-published state must be visible so a maintainer can decide whether to bump the patch version and retry.
- What happens when the publish credential is missing or invalid? → The workflow fails clearly at the push step with a credential error, before any package is uploaded.
- What happens when a user has both the old private feed and the new public feed configured? → The new public-feed package resolves correctly; the old feed becomes irrelevant and can be removed at the user's leisure.
- What happens when a user follows old cached documentation that still references PAT setup? → The new install command must still work without any PAT, so the old steps become harmless extras rather than blockers.
- What happens during local development when a contributor wants to test an unpublished build? → They can still pack locally and install from a local directory source; this path is unchanged.

## Requirements *(mandatory)*

### Functional Requirements

#### Distribution

- **FR-001**: All three Spectra packages MUST be published to the public default .NET package feed.
- **FR-002**: A user MUST be able to install the Spectra CLI on a clean machine using a single install command, with no prior configuration of package sources or credentials.
- **FR-003**: Each published package MUST carry complete, accurate metadata: package identifier, authors, company, description, license expression, project URL, repository URL, repository type, tags, and an embedded README.
- **FR-004**: Each published package MUST include the project README as the package's displayed long description on the feed.
- **FR-005**: Package identifiers MUST use a consistent casing across the workflow, project files, and all documentation so install commands copied from any source resolve correctly.

#### Release pipeline

- **FR-006**: A release MUST be triggered by pushing a version tag matching the project's release tag convention.
- **FR-007**: The release pipeline MUST derive the published package version from the tag itself (no manual version entry, no hardcoded version in project files).
- **FR-008**: The release pipeline MUST run the full test suite before packing, and MUST NOT publish any package if tests fail.
- **FR-009**: The release pipeline MUST publish all three packages atomically in intent — i.e., a single pipeline run is responsible for all three, and any partial-success state is surfaced as a pipeline failure.
- **FR-010**: Re-pushing a previously released tag MUST NOT cause the pipeline to fail; the existing published version MUST be preserved unchanged.
- **FR-011**: The release pipeline MUST authenticate to the public feed using a single repository secret dedicated to this purpose.

#### Internal consumers

- **FR-012**: Any internal repository workflow that installs the Spectra CLI MUST install it from the public default feed using the same command external users use, with no reference to a private feed or private-feed credential.
- **FR-013**: The repository's secret store MUST NOT require the legacy private-feed token after this feature ships; any workflow reference to it MUST be removed.

#### Documentation

- **FR-014**: All user-facing installation instructions (README, getting started, and any other public-facing onboarding doc) MUST show only the public-feed install flow.
- **FR-015**: Any documentation page dedicated to setting up the legacy private feed MUST be removed from the documentation set.
- **FR-016**: Local-development documentation describing how contributors build, pack, and install Spectra from a local directory source MUST be preserved and remain accurate.
- **FR-017**: Documentation MUST NOT reference personal access tokens, private feed authentication, or `dotnet nuget add source` as steps required to install Spectra for normal use.

### Key Entities

- **Published package**: A versioned, downloadable artifact for one of the three Spectra projects (Core, CLI, MCP), carrying identifier, version, descriptive metadata, license, links, tags, and embedded README.
- **Release tag**: A version-bearing git tag whose presence triggers the publish pipeline and whose value determines the published package version.
- **Publish credential**: A single repository-scoped secret authorizing the pipeline to push packages to the public feed, scoped narrowly to the Spectra package family.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new user on a clean machine can go from "never heard of Spectra" to "CLI installed and runnable" in a single command and under one minute, with zero account creation, token generation, or feed configuration.
- **SC-002**: The documented install instructions in the README and getting-started guide contain zero steps related to personal access tokens, private-feed authentication, or custom package source registration.
- **SC-003**: Pushing a release tag publishes all three packages to the public feed at the matching version in a single automated pipeline run, with no manual intervention.
- **SC-004**: Re-pushing an already-released tag completes the pipeline successfully without altering or duplicating the existing published version.
- **SC-005**: The repository's dashboard deployment pipeline installs the Spectra CLI using the same single command an external user would use, and references no private-feed secret.
- **SC-006**: The legacy private-feed setup documentation page no longer exists in the documentation set, and no remaining doc links to it.
- **SC-007**: Each published package's feed listing displays accurate title, description, authors, license, project URL, repository link, tags, and embedded README with no missing fields.
- **SC-008**: A failing test run during a release prevents any package from being published.
- **SC-009**: After this feature ships, the legacy private-feed token can be removed from the repository's secret store with no workflow failures.

## Assumptions

- The public feed account, the publishing credential scoped to the Spectra package family, and the repository secret holding that credential are already configured. This feature consumes them; it does not provision them.
- The release tag convention used by the project (a `v` prefix followed by a semantic version) is the canonical trigger and is preserved.
- Local development workflows that pack and install from a local directory source are out of scope for change — they continue to work as they do today.
- Package icons, Source Link, symbol packages, package signing, and any secondary mirror feed are out of scope for this feature.
- Removing the legacy private-feed secret from the repository's secret store is a manual administrative step performed after the workflow changes ship; it is not automated by this feature.
