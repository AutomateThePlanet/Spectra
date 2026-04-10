# Implementation Plan: Publish to NuGet.org

**Branch**: `035-nuget-org-publish` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/035-nuget-org-publish/spec.md`

## Summary

Move Spectra package distribution from GitHub Packages (private feed, requires PAT) to NuGet.org (public default feed, zero-config). Three packages — `Spectra.Core`, `Spectra.CLI`, `Spectra.MCP` — must be published with complete public-feed metadata (description, license, repo URL, tags, embedded README) on every version tag push. Internal pipelines and the bundled `deploy-dashboard.yml` template must install the CLI from the default feed with no source registration. The legacy `docs/deployment/github-packages-setup.md` page is deleted along with all references.

Current state discovered during planning:

- `.github/workflows/publish.yml` packs only `Spectra.CLI` and `Spectra.MCP` (not `Spectra.Core`), pushes to GitHub Packages, uses `${{ secrets.GITHUB_TOKEN }}`, and uses `/p:Version=` (not `-p:PackageVersion=`). It already extracts `VERSION` from the tag.
- `src/Spectra.CLI/Spectra.CLI.csproj` and `src/Spectra.MCP/Spectra.MCP.csproj` hardcode `<Version>1.36.0</Version>` and have minimal metadata (no license, no repo URL, no tags, no README, wrong author). `Spectra.Core` has no packaging metadata at all.
- `.github/workflows/deploy-dashboard.yml.template` (the active workflow file) installs `spectra-cli` (wrong package id) with no source config. The bundled CLI template at `src/Spectra.CLI/Templates/deploy-dashboard.yml` (the file actually copied into user repos by `spectra init`) still does the GitHub Packages source registration with `GH_PACKAGES_TOKEN`. This file is the canonical user-facing template and **must** be updated.
- `docs/deployment/github-packages-setup.md` exists and is referenced by `docs/deployment.md`.
- `docs/getting-started.md` line 28 shows `dotnet tool install -g spectra` (lowercase, wrong — should be `Spectra.CLI`).
- `README.md` already shows correct casing (`Spectra.CLI`) and already has nuget.org badges that will start working once the first package is pushed.
- Documentation site `_config.yml` already references `https://www.nuget.org/packages/Spectra.CLI`.

## Technical Context

**Language/Version**: C# 12, .NET 8.0
**Primary Dependencies**: None added; this feature is build/release configuration only
**Storage**: N/A
**Testing**: xUnit (existing test suites must remain green; no new test projects)
**Target Platform**: GitHub Actions (ubuntu-latest), nuget.org public feed
**Project Type**: CLI / library (.NET tool packages)
**Performance Goals**: N/A — release pipeline runs on tag push only
**Constraints**:
- Pipeline must be idempotent on re-pushed tags (`--skip-duplicate`)
- Tests must pass before any push to nuget.org (no broken releases)
- Single secret (`NUGET_API_KEY`) authenticates to the public feed
- Package version derived solely from the git tag — no hardcoded `<Version>` in `.csproj`
**Scale/Scope**: 3 publishable packages; ~6 file modifications; 1 file deletion; 0 new code

## Constitution Check

| Principle | Compliance | Notes |
|-----------|------------|-------|
| I. GitHub as Source of Truth | ✅ Pass | All workflow and project changes live in Git; no external storage introduced. |
| II. Deterministic Execution | ✅ N/A | Feature does not touch the MCP execution engine. |
| III. Orchestrator-Agnostic Design | ✅ N/A | No MCP API changes. |
| IV. CLI-First Interface | ✅ Pass | No new CLI surface; the CLI installation method becomes simpler, not more interactive. |
| V. Simplicity (YAGNI) | ✅ Pass | No new abstractions, helpers, or feature flags. Direct edits to existing project/workflow files. Source Link, signing, package icons, and mirror feeds explicitly out of scope. |

**Quality Gates**: `spectra validate` is unaffected; existing CI must continue to pass. No test files are added or removed.

**Result**: PASS — no violations to justify in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/035-nuget-org-publish/
├── spec.md              # /speckit.specify output
├── plan.md              # This file
├── research.md          # Phase 0 — feed/auth/metadata decisions
├── data-model.md        # Phase 1 — package metadata schema
├── quickstart.md        # Phase 1 — release & verification walkthrough
├── contracts/
│   └── publish-workflow.md  # Tag → publish trigger contract & metadata contract
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit.tasks output (created later)
```

### Source Code (repository root)

This feature edits existing files only — no new source directories.

```text
.github/workflows/
├── publish.yml                          # MODIFY: pack Core+CLI+MCP, push to nuget.org, NUGET_API_KEY
└── deploy-dashboard.yml.template        # MODIFY: fix package id casing (spectra-cli → Spectra.CLI)

src/Spectra.Core/
└── Spectra.Core.csproj                  # MODIFY: add full nuget.org PackagePropertyGroup + README ItemGroup

src/Spectra.CLI/
├── Spectra.CLI.csproj                   # MODIFY: replace minimal metadata with full nuget.org metadata; remove hardcoded <Version>
└── Templates/
    └── deploy-dashboard.yml             # MODIFY: drop GH Packages source step (this is the file shipped to user repos)

src/Spectra.MCP/
└── Spectra.MCP.csproj                   # MODIFY: replace minimal metadata with full nuget.org metadata; remove hardcoded <Version>

docs/
├── deployment/
│   └── github-packages-setup.md         # DELETE
├── deployment.md                        # MODIFY: remove "GitHub Packages" mention from intro
└── getting-started.md                   # MODIFY: fix install command casing (spectra → Spectra.CLI)

README.md                                # AUDIT only — install command + badges already correct
```

**Structure Decision**: Edits to existing files only. No new projects, source directories, or test projects. The bundled CLI template at `src/Spectra.CLI/Templates/deploy-dashboard.yml` is the canonical file users receive via `spectra init`, so it is the one that must be updated (the `.template` file in `.github/workflows/` is a separate, repo-internal copy and is also updated for consistency).

## Complexity Tracking

> No constitution violations to justify — table left empty intentionally.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | (none)     | (none)                              |
