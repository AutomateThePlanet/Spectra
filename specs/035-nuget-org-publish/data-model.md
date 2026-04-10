# Phase 1 — Data Model: Publish to NuGet.org

**Feature**: 035-nuget-org-publish
**Date**: 2026-04-10

This feature has no runtime data model. The "entities" are build/release artifacts and configuration. They are documented here as schemas so the implementation tasks have an unambiguous target shape.

## Entity 1 — Published Package

A `.nupkg` file uploaded to nuget.org. One per publishable Spectra project.

| Field | Source | Spectra.Core | Spectra.CLI | Spectra.MCP |
|-------|--------|--------------|-------------|-------------|
| `PackageId` | `.csproj` | `Spectra.Core` | `Spectra.CLI` | `Spectra.MCP` |
| `Version` | git tag (workflow) | `${VERSION}` | `${VERSION}` | `${VERSION}` |
| `Authors` | `.csproj` | `Anton Angelov` | `Anton Angelov` | `Anton Angelov` |
| `Company` | `.csproj` | `Automate The Planet` | `Automate The Planet` | `Automate The Planet` |
| `Description` | `.csproj` | "Core library for SPECTRA — AI-native test generation framework. Shared models, parsing, validation, and coverage analysis." | "AI-native test generation CLI. Generate test cases from documentation with dual-model grounding verification, coverage analysis, and visual dashboards." | "MCP execution server for SPECTRA. Deterministic AI-orchestrated test execution with state machine, pause/resume, and multi-format reports." |
| `PackageLicenseExpression` | `.csproj` | `Apache-2.0` | `Apache-2.0` | `Apache-2.0` |
| `PackageProjectUrl` | `.csproj` | `https://github.com/AutomateThePlanet/Spectra` | same | same |
| `RepositoryUrl` | `.csproj` | same | same | same |
| `RepositoryType` | `.csproj` | `git` | `git` | `git` |
| `PackageTags` | `.csproj` | `testing;test-generation;ai;mcp;qa;test-automation;spectra` | `testing;test-generation;ai;mcp;copilot;qa;test-automation;spectra;dotnet-tool` | `testing;mcp;test-execution;ai;qa;spectra;dotnet-tool` |
| `PackageReadmeFile` | `.csproj` | `README.md` | `README.md` | `README.md` |
| `PackAsTool` | `.csproj` | _(absent)_ | `true` | `true` |
| `ToolCommandName` | `.csproj` | _(N/A)_ | `spectra` | `spectra-mcp` |
| Embedded README | `<None Include="../../README.md" Pack="true" PackagePath="/" />` | required | required | required |

### Validation rules

- `PackageId` MUST match the casing in README install commands and the `nuget.org` badge URLs.
- `<Version>` element MUST NOT appear in any of the three project files. Version is supplied at pack time via `-p:PackageVersion=`.
- `PackageLicenseExpression` MUST be `Apache-2.0` (matches the `LICENSE` file at repo root). Not `MIT`.
- `PackageReadmeFile` MUST resolve to a file actually included in the .nupkg via the `<None>` item. If the include is missing, `dotnet pack` fails with NU5039.
- All three projects MUST list identical `Authors`, `Company`, `RepositoryUrl`, `PackageProjectUrl`, `RepositoryType`, `PackageLicenseExpression`, `PackageReadmeFile`. Differences allowed only on `PackageId`, `Description`, `PackageTags`, `PackAsTool`, `ToolCommandName`.

## Entity 2 — Release Tag

A git tag matching the pattern `v*` pushed to the `AutomateThePlanet/Spectra` repository.

| Field | Format | Example | Validation |
|-------|--------|---------|------------|
| Raw ref | `refs/tags/v<semver>` | `refs/tags/v1.36.0` | Workflow trigger filter `tags: ['v*']` |
| Stripped version | `<semver>` | `1.36.0` | Computed by `${GITHUB_REF#refs/tags/v}`; passed to `dotnet pack -p:PackageVersion=...` |

### State transitions

```
(no tag)  --[git tag v1.36.0 && git push]-->  TAG_PUSHED
TAG_PUSHED  --[publish.yml runs]-->            BUILDING
BUILDING    --[tests pass]-->                   PACKING
BUILDING    --[tests fail]-->                   FAILED (no publish)
PACKING     --[3 .nupkg produced]-->            PUSHING
PUSHING     --[push success]-->                 PUBLISHED
PUSHING     --[version already exists]-->       PUBLISHED (--skip-duplicate, idempotent)
PUSHING     --[push error (auth/network)]-->    FAILED (partial state visible in run log)
```

## Entity 3 — Publish Credential

A repository secret authorizing the workflow to push to nuget.org.

| Field | Value |
|-------|-------|
| Secret name | `NUGET_API_KEY` |
| Scope | nuget.org API key, glob `Spectra.*` |
| Permissions | Push new packages and new versions of existing packages |
| Consumer | `dotnet nuget push --api-key ${{ secrets.NUGET_API_KEY }}` step in `publish.yml` |
| Provisioned by | Repository admin (already done — out of scope for this feature) |

### Decommissioning

The legacy secret `GH_PACKAGES_TOKEN` MUST be removable from the repo secret store after this feature ships. No workflow file may reference it. This is verified post-implementation by grepping the entire `.github/workflows/` and `src/Spectra.CLI/Templates/` directories for the string `GH_PACKAGES_TOKEN` and finding zero matches.

---

**Status**: Phase 1 data model complete.
