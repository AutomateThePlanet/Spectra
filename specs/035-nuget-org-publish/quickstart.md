# Quickstart: Cutting a NuGet.org Release

**Feature**: 035-nuget-org-publish
**Audience**: SPECTRA maintainers

This is the operational walkthrough for the new release flow once this feature is implemented.

## Prerequisites (one-time, already done)

- nuget.org account exists.
- API key created on nuget.org, scoped to glob `Spectra.*`.
- Repo secret `NUGET_API_KEY` set in `AutomateThePlanet/Spectra` → Settings → Secrets → Actions.

## Cutting a release

```bash
# 1. Make sure main is green and you're at the commit you want to ship.
git checkout main
git pull
gh run list --workflow=ci.yml --branch=main --limit=1   # confirm CI is green

# 2. Pick the next semver. Example: 1.36.0.
VERSION=1.36.0

# 3. Tag and push.
git tag v$VERSION
git push origin v$VERSION

# 4. Watch the workflow.
gh run watch
```

The workflow will:

1. Check out the tagged commit.
2. Build everything in Release.
3. Run the full test suite. **If anything fails, no packages are published.**
4. Pack `Spectra.Core`, `Spectra.CLI`, `Spectra.MCP` at version `1.36.0`.
5. Push all three `.nupkg` files to `https://api.nuget.org/v3/index.json`.
6. Create a GitHub Release `v1.36.0` with auto-generated notes and the three `.nupkg` files attached.

## Verifying the release

```bash
# Wait ~5 minutes for nuget.org indexing, then:
dotnet tool install -g Spectra.CLI --version 1.36.0
spectra --version   # should print 1.36.0
```

Or visit:

- https://www.nuget.org/packages/Spectra.CLI/1.36.0
- https://www.nuget.org/packages/Spectra.MCP/1.36.0
- https://www.nuget.org/packages/Spectra.Core/1.36.0

Each page should show:

- ✅ Description text (from `.csproj`)
- ✅ Long description = the project README
- ✅ License: Apache-2.0
- ✅ Project website link → github.com/AutomateThePlanet/Spectra
- ✅ Source repository link → same
- ✅ Tags listed (testing, ai, mcp, etc.)
- ✅ Author: Anton Angelov / Automate The Planet

## Recovering from a bad release

### Test failure or pack failure

The workflow goes red, nothing is published, no `.nupkg` reaches nuget.org.

```bash
# Fix the underlying issue, push the fix to main.
# Delete the broken tag locally and remotely:
git tag -d v$VERSION
git push origin :refs/tags/v$VERSION
# Re-tag the new commit and push.
git tag v$VERSION
git push origin v$VERSION
```

### Push partially failed (e.g., one of three packages succeeded)

The workflow goes red with two of three packages already on nuget.org. nuget.org **does not allow re-pushing** the same version (`--skip-duplicate` will silently ignore them on retry).

The recovery is to bump to the next patch and re-release:

```bash
VERSION=1.36.1
git tag v$VERSION
git push origin v$VERSION
```

The two packages that succeeded under `1.36.0` will remain available; consumers transitioning to `1.36.1` get the matching set.

### Re-running the same tag

`--skip-duplicate` makes this safe. Pushing the same tag twice produces a green run on the second attempt and does not overwrite or duplicate any nuget.org listing.

```bash
git push origin v$VERSION --force-with-lease   # not needed for the publish, only if rewriting the tag locally
gh run rerun <run-id>
```

## Local development (unchanged)

Local pack-and-install still works the same way for contributors testing unpublished builds:

```bash
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release
dotnet tool uninstall -g Spectra.CLI 2>/dev/null
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI
```

This path bypasses nuget.org entirely. See `docs/DEVELOPMENT.md` for the full local-build workflow.

## After this feature ships — manual cleanup

Once the first nuget.org release is verified:

1. Remove `GH_PACKAGES_TOKEN` from `AutomateThePlanet/Spectra` → Settings → Secrets → Actions.
2. (Optional) Unlist the old GitHub Packages versions, or leave them for historical reference.

These are manual administrative steps; not automated by this feature.
