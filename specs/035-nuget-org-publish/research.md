# Phase 0 — Research: Publish to NuGet.org

**Feature**: 035-nuget-org-publish
**Date**: 2026-04-10

All Technical Context items were known going in (no NEEDS CLARIFICATION markers). Research below documents the design decisions and the alternatives considered for each.

## D1 — Publish target: nuget.org via official push URL

**Decision**: Push all three packages to `https://api.nuget.org/v3/index.json` using `dotnet nuget push ... --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate`.

**Rationale**:
- nuget.org is the default feed bundled with the .NET SDK; users need zero source configuration to install.
- An API key already exists, scoped to the `Spectra.*` package family — narrowest practical scope.
- `--skip-duplicate` makes the publish step idempotent: re-pushing an already-released tag completes successfully (FR-010, SC-004) without overwriting.

**Alternatives considered**:
- *Stay on GitHub Packages*: requires PAT for cross-repo install — the entire problem statement.
- *MyGet / custom feed*: introduces a new third party for no benefit; nuget.org is the canonical default.
- *Mirror to both nuget.org and GitHub Packages*: out of scope per spec; doubles credential surface and adds maintenance with no user benefit for a public project.
- *Use `nuget.exe push`*: `dotnet nuget push` is the modern, cross-platform equivalent and is already available on `actions/setup-dotnet`.

## D2 — Versioning: derive from git tag, no `<Version>` in `.csproj`

**Decision**: Strip the leading `v` from `${GITHUB_REF}` and pass the result via `-p:PackageVersion=${VERSION}` on each `dotnet pack` invocation. Remove all `<Version>` properties from the three publishable `.csproj` files.

**Rationale**:
- Single source of truth for the released version: the git tag.
- Eliminates the recurring drift bug where `Spectra.CLI.csproj` and `Spectra.MCP.csproj` carry a stale hardcoded version (currently `1.36.0`) that disagrees with the next release tag.
- `-p:PackageVersion=` is the supported MSBuild property for `dotnet pack` and is unambiguous (the existing workflow uses `/p:Version=`, which works but is less specific and bleeds into assembly versions; switching to `PackageVersion` keeps it scoped to the .nupkg only).

**Alternatives considered**:
- *Keep `<Version>1.0.0-dev</Version>` as a local-build fallback*: rejected — `dotnet pack` works fine without one (it defaults to `1.0.0`), and a stale fallback in source is the exact bug we are removing.
- *Compute version in a separate `version.json` (Nerdbank.GitVersioning)*: extra dependency; YAGNI per Constitution V.

## D3 — Test gate before publish

**Decision**: Run `dotnet test --configuration Release --no-build` after `dotnet build` and before any `dotnet pack` step. Pipeline fails fast and publishes nothing if tests fail.

**Rationale**:
- FR-008/SC-008: failing tests must prevent any publish.
- Gating before pack (rather than between pack and push) means we never produce broken `.nupkg` artifacts in the workflow's release-archive step either.

**Alternatives considered**:
- *Run tests in a separate job and use `needs:`*: slightly more parallelism but more YAML and a re-checkout/restore cost. Single-job pipeline is simpler and the existing publish workflow is already single-job.
- *Skip tests on publish (rely on CI on `main`)*: rejected — a tag could land on a commit that never went through CI, or after a force-push.

## D4 — Package metadata fields

**Decision**: Each of the three `.csproj` files gets the same complete `<PropertyGroup>` block (with package-specific `PackageId`/`Description`/`PackageTags`):

| Field | Value |
|-------|-------|
| `PackageId` | `Spectra.Core` / `Spectra.CLI` / `Spectra.MCP` |
| `Authors` | `Anton Angelov` |
| `Company` | `Automate The Planet` |
| `Description` | Per-package one-liner |
| `PackageLicenseExpression` | `Apache-2.0` (per `LICENSE` and the badge in README) |
| `PackageProjectUrl` | `https://github.com/AutomateThePlanet/Spectra` |
| `RepositoryUrl` | same |
| `RepositoryType` | `git` |
| `PackageTags` | `testing;test-generation;ai;mcp;qa;test-automation;spectra` (+ `dotnet-tool` for CLI/MCP, + `copilot` for CLI, + `test-execution` for MCP) |
| `PackageReadmeFile` | `README.md` |

Plus an `<ItemGroup>` block packing the repo-root README into the .nupkg:

```xml
<ItemGroup>
  <None Include="../../README.md" Pack="true" PackagePath="/" />
</ItemGroup>
```

**Rationale**:
- The user-supplied spec lists `MIT` as the license, but the actual repository ships under **Apache 2.0** (`LICENSE` file + README badge). The package metadata must match the real license, otherwise nuget.org displays a contradictory license expression. **Apache-2.0** is correct.
- README embedding gives the nuget.org listing a meaningful long description, install snippet, and badges — same content users see on GitHub.
- Tags are chosen for discoverability on nuget.org search (testing/QA/AI/MCP/.NET tool).

**Alternatives considered**:
- *Per-package custom READMEs*: nice-to-have but YAGNI for launch; one canonical README is sufficient.
- *`<PackageLicenseFile>` instead of expression*: SPDX expression is the modern, lighter approach and renders a recognized license badge on nuget.org.

## D5 — Internal pipeline & user-facing template

**Decision**: Update **two** files, not just one:
1. `src/Spectra.CLI/Templates/deploy-dashboard.yml` — the file `spectra init` copies into user repos. Drop the GitHub Packages source registration entirely; install becomes one line: `dotnet tool install -g Spectra.CLI`. Remove the `GH_PACKAGES_TOKEN` requirement from the file's header comment.
2. `.github/workflows/deploy-dashboard.yml.template` — the repo-internal example. Fix the wrong package id (`spectra-cli` → `Spectra.CLI`) so it matches the canonical template.

**Rationale**:
- The bundled CLI template is what users actually receive; it is the file the spec's User Story 4 cares about.
- The `.github/workflows/` `.template` file currently has a mismatched package id (`spectra-cli`), which is a latent bug — fixing it now keeps both copies consistent and avoids confusion in future audits.
- Spec FR-013: removing `GH_PACKAGES_TOKEN` from any workflow reference. This applies to the user-facing template's header comment, not just the live workflow.

**Alternatives considered**:
- *Only update the bundled template*: leaves the in-repo `.template` file with a wrong package id; future audits would flag it.
- *Delete one of the two copies to consolidate*: out of scope; the duplication is pre-existing and not what this feature is solving.

## D6 — Documentation cleanup

**Decision**:
- Delete `docs/deployment/github-packages-setup.md`.
- Edit `docs/deployment.md` to drop the "GitHub Packages" reference from the one-line description.
- Edit `docs/getting-started.md` line 28 to use `Spectra.CLI` (correct casing matching `<PackageId>`) instead of `spectra`.
- README.md needs no changes — install command and badges are already correct.
- `docs/DEVELOPMENT.md` needs no changes — local pack-and-install workflow is preserved (FR-016).
- `CLAUDE.md` needs no changes — does not reference GitHub Packages.

**Rationale**:
- Deleting the setup page is the most direct way to satisfy FR-015/SC-006. No grep should find a remaining link.
- The casing fix in `getting-started.md` is the only real risk for new users — `dotnet tool install -g spectra` will fail with "package not found" once we publish under id `Spectra.CLI`.

**Alternatives considered**:
- *Keep the page as a "legacy" reference*: rejected — confuses new users and contradicts SC-006.
- *Redirect the page to a "no longer needed" stub*: pure noise; deletion is cleaner.

## D7 — Package id casing audit

**Decision**: Use `Spectra.CLI`, `Spectra.MCP`, `Spectra.Core` consistently in workflow, project files, and all docs. The casing must be byte-identical because `dotnet tool install` is case-insensitive on the wire but the displayed package id and badge URLs are case-preserving — inconsistent casing is a smell users notice.

**Audit checklist (informational, not a code change)**:
- ✅ `Spectra.CLI.csproj`: `<PackageId>Spectra.CLI</PackageId>`
- ✅ README.md badges and install command
- ✅ `docs/index.md`, `docs/cli-reference.md`, all `docs/execution-agent/*` pages
- ❌ `docs/getting-started.md` (currently lowercase `spectra`) — fixed by D6
- ❌ `.github/workflows/deploy-dashboard.yml.template` (currently `spectra-cli`) — fixed by D5
- ✅ `src/Spectra.CLI/Templates/deploy-dashboard.yml` (currently `Spectra.CLI`)

## D8 — Manual / out-of-scope items confirmed

Carried forward from spec, documented here so they don't get rediscovered later:

- **Manual**: Removing `GH_PACKAGES_TOKEN` from the GitHub repo secret store after this ships.
- **Out of scope**: Package icons, Source Link, symbol packages (.snupkg), package signing, secondary mirror feed, changelog automation.
- **Pre-provisioned**: nuget.org account, the API key (scoped `Spectra.*`), and the `NUGET_API_KEY` repo secret. The pipeline consumes them; this feature does not create them.

---

**Status**: Phase 0 complete. No NEEDS CLARIFICATION markers remain. Ready for Phase 1.
