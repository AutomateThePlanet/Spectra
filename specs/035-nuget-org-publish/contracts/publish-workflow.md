# Contract: Publish Workflow

**Feature**: 035-nuget-org-publish
**Date**: 2026-04-10

This is the external contract between a maintainer pushing a tag and the resulting state on nuget.org.

## Trigger

| Input | Format | Example |
|-------|--------|---------|
| Git tag push to `AutomateThePlanet/Spectra` | `v<MAJOR>.<MINOR>.<PATCH>` | `v1.36.0` |

The workflow file `.github/workflows/publish.yml` MUST declare:

```yaml
on:
  push:
    tags: ['v*']
```

## Inputs (provided by environment)

| Input | Source | Required |
|-------|--------|----------|
| `GITHUB_REF` | GitHub Actions runtime | Yes (used to derive version) |
| `secrets.NUGET_API_KEY` | Repository secret | Yes (push step fails without it) |

## Steps (ordered, each MUST succeed for the next to run)

1. Checkout (`actions/checkout@v6`, `fetch-depth: 0`).
2. Setup .NET 8 SDK (`actions/setup-dotnet@v5`).
3. Extract version: `VERSION=${GITHUB_REF#refs/tags/v}`.
4. `dotnet restore`.
5. `dotnet build --configuration Release --no-restore`.
6. `dotnet test --configuration Release --no-build`. **GATE**: failure here aborts the workflow with no packages published.
7. `dotnet pack src/Spectra.Core/Spectra.Core.csproj --configuration Release --no-build --output ./nupkg -p:PackageVersion=${VERSION}`.
8. `dotnet pack src/Spectra.CLI/Spectra.CLI.csproj --configuration Release --no-build --output ./nupkg -p:PackageVersion=${VERSION}`.
9. `dotnet pack src/Spectra.MCP/Spectra.MCP.csproj --configuration Release --no-build --output ./nupkg -p:PackageVersion=${VERSION}`.
10. `dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`.
11. Create GitHub Release (`softprops/action-gh-release@v2`) — preserved from existing workflow, attaches the three `.nupkg` files.

## Outputs (observable post-conditions)

| Output | Location | Verification |
|--------|----------|--------------|
| `Spectra.Core.${VERSION}.nupkg` | `https://www.nuget.org/packages/Spectra.Core/${VERSION}` | Page exists, metadata matches data-model.md |
| `Spectra.CLI.${VERSION}.nupkg` | `https://www.nuget.org/packages/Spectra.CLI/${VERSION}` | Page exists, README rendered, install command works |
| `Spectra.MCP.${VERSION}.nupkg` | `https://www.nuget.org/packages/Spectra.MCP/${VERSION}` | Page exists, README rendered |
| GitHub Release `v${VERSION}` | `https://github.com/AutomateThePlanet/Spectra/releases/tag/v${VERSION}` | Auto-generated notes + 3 attached `.nupkg` |

## Error contract

| Failure | Workflow exit | nuget.org state |
|---------|---------------|-----------------|
| Build fails | Step 5 fails, workflow red | Unchanged |
| Tests fail | Step 6 fails, workflow red | Unchanged |
| Pack fails (e.g., missing README) | Steps 7–9 fail, workflow red | Unchanged |
| Push fails for one package, succeeds for two | Step 10 fails, workflow red | Partial — visible in nuget.org listing; maintainer bumps patch and re-tags |
| Tag re-pushed (version already on nuget.org) | Step 10 succeeds via `--skip-duplicate`, workflow green | Unchanged (existing version preserved) |
| `NUGET_API_KEY` missing/invalid | Step 10 fails with auth error | Unchanged |

## Idempotency contract

Re-running the workflow on a tag whose version is already on nuget.org MUST:
- Produce a green workflow run (`--skip-duplicate` makes the push a no-op).
- Leave the existing published versions byte-identical (no overwrite).
- Re-create / update the GitHub Release if `softprops/action-gh-release@v2` is configured to do so (it is — `tag_name` matches existing).

## Out of contract

- This workflow does not sign packages.
- This workflow does not produce symbol packages (.snupkg).
- This workflow does not push to any feed other than nuget.org.
- This workflow does not bump version files in the repo (version comes from the tag, period).
