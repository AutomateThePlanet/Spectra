# Research: Open-Source Readiness

**Feature**: 014-open-source-ready
**Date**: 2026-03-21

## Current State Audit

### Decision: README Redesign Approach
**Chosen**: Full rewrite following Testimize progressive disclosure pattern
**Rationale**: The current README (90 lines) is functional but plain. Testimize uses banner + badges + emoji features + comparison tables + code examples. SPECTRA should match this visual quality for the AutomateThePlanet ecosystem consistency.
**Alternatives considered**:
- Minimal update (add badges only) — rejected: doesn't meet the visual standard set by Testimize
- GitHub Pages docs site — rejected as future enhancement; README is the priority

### Decision: CI Workflow Configuration
**Chosen**: Single `ci.yml` workflow on ubuntu-latest with `dotnet restore → build → test → upload artifacts`
**Rationale**: Standard .NET CI pattern. Ubuntu is fastest/cheapest. Single workflow keeps configuration simple.
**Alternatives considered**:
- Matrix build (Windows + Linux + macOS) — rejected: adds cost and complexity; defer until cross-platform issues emerge
- Separate build and test workflows — rejected: unnecessary split for a single-solution project

### Decision: NuGet Version Strategy
**Chosen**: Extract version from git tag in the publish workflow using `${GITHUB_REF_NAME#v}` to strip the `v` prefix, passed via `/p:Version=`
**Rationale**: Git tags are the source of truth for releases. No need to maintain version in multiple places. The `.csproj` files already have `PackAsTool=true` and correct `ToolCommandName` values.
**Alternatives considered**:
- GitVersion tool — rejected: over-engineered for this project's needs (Principle V: YAGNI)
- Manual version in Directory.Build.props — rejected: creates risk of tag/prop version mismatch

### Decision: Two NuGet Packages (not three)
**Chosen**: Publish `Spectra.CLI` and `Spectra.MCP` only
**Rationale**: `Spectra.Core` is a shared library consumed internally by CLI and MCP. It's not useful standalone. Publishing it would create a public API surface to maintain.
**Alternatives considered**:
- Publish all three projects — rejected: Core has no standalone use case
- Single unified package — rejected: MCP server should be independently installable

### Decision: Documentation Strategy
**Chosen**: Keep existing `docs/` folder structure as-is; verify links and content quality
**Rationale**: All 16 documentation files are substantive (58-430 lines each). No stubs detected. The structure already covers getting started, CLI reference, configuration, test format, coverage, profiles, grounding, document index, deployment guides, and architecture.
**Alternatives considered**:
- Add deployment/cloudflare-pages-setup.md — already covered by dashboard.yml.template documentation
- Generate docs site with docfx/mkdocs — deferred to future enhancement

### Decision: License Approach
**Chosen**: MIT license at repo root only, no individual file headers
**Rationale**: The LICENSE file already contains correct MIT text with "Copyright (c) 2026 Automate The Planet Ltd." Adding headers to 400+ source files is noisy, error-prone, and not required by MIT license terms.
**Alternatives considered**:
- SPDX headers in every file — rejected: maintenance burden outweighs legal benefit for MIT

### Decision: .editorconfig
**Chosen**: Keep existing .editorconfig as-is (59 lines, comprehensive C# 12 rules)
**Rationale**: Already covers naming conventions, code style, indentation (4 for code, 2 for JSON/YAML/XML), and code quality warnings. No gaps identified.
**Alternatives considered**:
- Add more rules — rejected: current set is comprehensive and matches CLAUDE.md code style guidelines

## Existing Asset Inventory

| Asset | Status | Action |
|-------|--------|--------|
| README.md | Exists (90 lines, plain) | Full rewrite |
| LICENSE | Exists (MIT, correct) | Verify only |
| CONTRIBUTING.md | Exists (basic) | Expand |
| .editorconfig | Exists (comprehensive) | Verify only |
| Directory.Build.props | Exists (v0.1.0) | Verify only |
| assets/spectra_github_readme_banner.png | Exists (463 KB) | Use in README |
| docs/ (16 files) | Exists (all substantive) | Link from README |
| .github/workflows/ci.yml | Missing | Create |
| .github/workflows/publish.yml | Missing | Create |
| .github/ISSUE_TEMPLATE/ | Missing | Create |
| .github/PULL_REQUEST_TEMPLATE.md | Missing | Create |
| .github/dependabot.yml | Missing | Create |
| .github/workflows/dashboard.yml.template | Exists | Keep (not part of this feature) |

## Test Health Research

Test suite composition:
- **Spectra.Core.Tests**: ~349 unit tests (parsing, validation, coverage, index operations)
- **Spectra.CLI.Tests**: ~329 integration tests (commands, dashboard, source, coverage)
- **Spectra.MCP.Tests**: ~306 tests (tools, integration flows, reports)
- **Total**: ~984 tests

Common test failure patterns in .NET projects:
- Path separator issues (Windows backslash vs Linux forward slash)
- Missing test fixtures or temp directory assumptions
- Environment-dependent configuration loading
- DateTime/timezone assumptions

Mitigation strategy: Run `dotnet test`, categorize failures, fix in order of frequency.
