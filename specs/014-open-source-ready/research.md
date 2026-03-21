# Research: Open Source Ready

**Date**: 2026-03-21 | **Feature**: 014-open-source-ready

## Finding 1: NuGet Packaging Already Configured

**Decision**: Use existing .csproj packaging configuration. No changes needed to PackAsTool/PackageId/ToolCommandName.

**Rationale**: Both projects are already fully configured:
- `Spectra.CLI`: PackAsTool=true, ToolCommandName=spectra, PackageId=Spectra.CLI, Version=1.11.1
- `Spectra.MCP`: PackAsTool=true, ToolCommandName=spectra-mcp, PackageId=Spectra.MCP, Version=1.11.0

**Alternatives considered**: Centralizing version in Directory.Build.props — rejected because CLI and MCP have different versions intentionally.

## Finding 2: Existing Infrastructure Inventory

**Decision**: Leverage existing files, don't recreate. Only add what's missing.

**Rationale**: Research found these already exist and are well-configured:
- LICENSE — MIT, correct copyright (Automate The Planet Ltd.)
- .editorconfig — 59 lines, full C# 12 config with naming conventions
- CONTRIBUTING.md — 44 lines, build/test/PR instructions
- docs/ — 17 guide files covering all major features
- README.md — 97 lines, has architecture diagram, ecosystem table, doc links

**Missing** (to create):
- `.github/workflows/ci.yml` — no CI pipeline
- `.github/workflows/publish.yml` — no release pipeline
- `.github/ISSUE_TEMPLATE/` — no issue templates
- `.github/pull_request_template.md` — no PR template
- `.github/dependabot.yml` — no dependency updates
- README visual redesign (existing is functional but plain)

## Finding 3: Test Failure Analysis

**Decision**: Fix 20 failing tests in Spectra.CLI.Tests. Root causes are auth-dependent tests and integration tests requiring external setup.

**Rationale**: From the test run:
- 7 `CriticFactoryTests` failures — test `TryCreate_ValidProvider_NoApiKey_Fails` for various providers. These expect the CriticFactory to fail when no API key env var is set, but the current implementation uses CopilotCritic which doesn't validate API keys at creation time.
- 1 `QuickstartWorkflowTests` failure — `Step6_Show_DisplaysTestDetails` likely needs test data setup.
- 1 `GenerateCommandTests` failure — `Generate_WithExistingTests_AvoidsIdConflict` likely a test isolation issue.
- 11 other assorted failures — need individual diagnosis.

**Alternatives considered**: Skipping auth-dependent tests — rejected per spec (no unexplained skips).

## Finding 4: Version Strategy for Publishing

**Decision**: Use git tag to set package version at build time via `/p:Version=$VERSION`.

**Rationale**: The .csproj files already have hardcoded versions (1.11.1, 1.11.0). The publish workflow will override these with the tag version using `dotnet pack /p:Version=$VERSION`. This means:
- Development builds use the .csproj version
- Release builds use the git tag version
- No need to maintain version in two places

**Alternatives considered**:
- GitVersion tool — rejected (adds complexity, requires git history analysis)
- Manual version bump commits — rejected (error-prone, tag is authoritative)

## Finding 5: README Style Reference (Testimize)

**Decision**: Follow Testimize README structure: centered banner, badge row, emoji-icon value props, feature blocks with headings, concise quickstart.

**Rationale**: Testimize (https://github.com/AutomateThePlanet/Testimize) from the same organization provides a proven, visually appealing template. Key elements to replicate:
- Full-width banner image (centered)
- Badge row with shields.io badges
- Value proposition section with emoji icons
- Feature sections with emoji headings and brief descriptions
- Clean quickstart with code blocks

**Alternatives considered**: Custom design — rejected (consistency with org style is better).
