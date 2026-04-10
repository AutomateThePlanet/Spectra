# Tasks: Publish to NuGet.org

**Input**: Design documents from `/specs/035-nuget-org-publish/`
**Prerequisites**: spec.md, plan.md, research.md, data-model.md, contracts/publish-workflow.md, quickstart.md

**Tests**: No new test code is required. The release pipeline already runs the existing test suites; this feature does not modify any C# code paths and therefore needs no new unit/integration tests. A "verification" task at the end exercises the existing tests via `dotnet build` + `dotnet test` to confirm no regressions from `.csproj` edits.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md). Each story is independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P] tasks (different files, no dependencies)
- **[Story]**: User story this task supports (US1, US2, US3, US4)
- All paths are absolute or relative to repository root `C:/SourceCode/Spectra/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a clean working state and confirm prerequisites before touching anything.

- [ ] T001 Verify the repo secret `NUGET_API_KEY` exists in GitHub via `gh secret list --repo AutomateThePlanet/Spectra` (read-only check; if missing, halt and notify maintainer — this feature assumes it is pre-provisioned per spec Assumptions).
- [ ] T002 Verify current working tree is clean (`git status`) and branch is `035-nuget-org-publish`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Package metadata edits that must land before the workflow can successfully pack any of the three projects with READMEs and the correct license. All three .csproj edits are foundational because the workflow's `dotnet pack` step (modified in Phase 3 for US2) will fail with NU5039 if any project lacks the embedded README, and the resulting nuget.org listing for US1 depends on the metadata being correct.

**⚠️ CRITICAL**: User Story 1 (zero-config install) and User Story 2 (tag-driven publish) both require these metadata changes. They are foundational to both.

- [ ] T003 [P] [Foundation] Edit `src/Spectra.Core/Spectra.Core.csproj`: add a second `<PropertyGroup>` containing `PackageId=Spectra.Core`, `Authors=Anton Angelov`, `Company=Automate The Planet`, the Core `Description` from data-model.md, `PackageLicenseExpression=Apache-2.0`, `PackageProjectUrl` and `RepositoryUrl=https://github.com/AutomateThePlanet/Spectra`, `RepositoryType=git`, `PackageTags=testing;test-generation;ai;mcp;qa;test-automation;spectra`, and `PackageReadmeFile=README.md`. Add an `<ItemGroup>` with `<None Include="../../README.md" Pack="true" PackagePath="/" />`. Do NOT add a `<Version>` element.
- [ ] T004 [P] [Foundation] Edit `src/Spectra.CLI/Spectra.CLI.csproj`: in the existing `<PropertyGroup>` that holds `PackAsTool`, **remove** `<Version>1.36.0</Version>`, **change** `<Authors>Spectra</Authors>` to `<Authors>Anton Angelov</Authors>`, **replace** `<Description>` with the CLI description from data-model.md, and **add** `<Company>Automate The Planet</Company>`, `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`, `<PackageProjectUrl>https://github.com/AutomateThePlanet/Spectra</PackageProjectUrl>`, `<RepositoryUrl>https://github.com/AutomateThePlanet/Spectra</RepositoryUrl>`, `<RepositoryType>git</RepositoryType>`, `<PackageTags>testing;test-generation;ai;mcp;copilot;qa;test-automation;spectra;dotnet-tool</PackageTags>`, `<PackageReadmeFile>README.md</PackageReadmeFile>`. Add a new `<ItemGroup>` with `<None Include="../../README.md" Pack="true" PackagePath="/" />`. Keep all existing `<EmbeddedResource>` items untouched.
- [ ] T005 [P] [Foundation] Edit `src/Spectra.MCP/Spectra.MCP.csproj`: same shape as T004 — remove `<Version>1.36.0</Version>`, change `<Authors>Spectra</Authors>` → `<Authors>Anton Angelov</Authors>`, replace `<Description>` with the MCP description from data-model.md, add `<Company>Automate The Planet</Company>`, `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`, `<PackageProjectUrl>` + `<RepositoryUrl>` + `<RepositoryType>git</RepositoryType>`, `<PackageTags>testing;mcp;test-execution;ai;qa;spectra;dotnet-tool</PackageTags>`, `<PackageReadmeFile>README.md</PackageReadmeFile>`. Add `<ItemGroup>` with the README `<None Include>`.
- [ ] T006 [Foundation] Run `dotnet build -c Release` from repo root and confirm zero new warnings/errors introduced by the .csproj edits (T003–T005).
- [ ] T007 [Foundation] Run `dotnet pack src/Spectra.Core/Spectra.Core.csproj -c Release --no-build -o ./nupkg-test -p:PackageVersion=0.0.0-test` and inspect the resulting `Spectra.Core.0.0.0-test.nupkg` (it's a zip): confirm `README.md` is at the package root, the `.nuspec` lists `licenseExpression>Apache-2.0`, `projectUrl`, `repository`, and `tags`. Repeat for Spectra.CLI and Spectra.MCP. Delete `./nupkg-test/` after verification.

**Checkpoint**: All three projects produce nuget.org-ready `.nupkg` files locally. Foundation complete.

---

## Phase 3: User Story 2 - Tag-driven release publishing (Priority: P1) 🎯 MVP

**Goal**: Pushing a `v*` tag publishes all three packages to nuget.org via a single, idempotent, test-gated workflow run.

**Why first**: User Story 1 (zero-config install) cannot be observed until at least one successful publish lands on nuget.org. US2 unblocks US1's verification path.

**Independent Test**: Push a pre-release tag (e.g., `v1.36.0-rc1`) to a temporary branch and verify the workflow completes end-to-end with all three packages appearing on nuget.org at that exact version. Or dry-run validate the workflow YAML with `gh workflow view publish.yml` and inspect the diff.

### Implementation for User Story 2

- [ ] T008 [US2] Edit `.github/workflows/publish.yml`:
  - Keep `name`, `on`, `permissions`, `jobs.publish.runs-on`, `Checkout`, `Setup .NET`, and `Extract version from tag` steps as-is.
  - Keep `Restore dependencies`, `Build`, `Test` steps as-is (they already match the contract — test runs before pack).
  - **Add** a third pack step `Pack Spectra.Core` running `dotnet pack src/Spectra.Core/Spectra.Core.csproj --configuration Release --no-build --output ./nupkg -p:PackageVersion=${{ steps.version.outputs.VERSION }}`. Place it **before** the existing CLI/MCP pack steps.
  - **Modify** the existing `Pack Spectra.CLI` and `Pack Spectra.MCP` steps to use `-p:PackageVersion=` instead of `/p:Version=` (keep all other args identical).
  - **Replace** the `Push to GitHub Packages` step with a `Push to NuGet.org` step running `dotnet nuget push ./nupkg/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate`.
  - Keep the `Create GitHub Release` step unchanged.
  - **Remove** `packages: write` from the workflow's `permissions:` block (no longer pushing to GitHub Packages); leave `contents: write` (still creating GitHub Releases).

**Checkpoint**: Workflow file is internally consistent and matches `contracts/publish-workflow.md`. The next tag push will exercise it end-to-end.

---

## Phase 4: User Story 1 - Zero-config install for new users (Priority: P1) 🎯 MVP

**Goal**: A new user runs one `dotnet tool install -g Spectra.CLI` command on a clean machine — no PAT, no source config — and gets a working CLI.

**Dependency**: Functionally satisfied by Phase 3 (US2) — once the workflow publishes to nuget.org, the install path "just works." This phase only requires the **documentation** to point at the right install command with the right casing, so users can find and copy the correct command.

**Independent Test**: After the next release, run `dotnet tool install -g Spectra.CLI` on a clean container and verify the CLI installs without any prior `dotnet nuget add source`. Pre-release: grep all user-facing docs for the wrong-cased install command and confirm zero matches.

### Implementation for User Story 1

- [ ] T009 [P] [US1] Edit `docs/getting-started.md` line 28: change `dotnet tool install -g spectra` → `dotnet tool install -g Spectra.CLI` (case must match `<PackageId>` in `Spectra.CLI.csproj`). No other changes to the file.
- [ ] T010 [P] [US1] Audit-only check (no edit needed): grep README.md for the install command — it already reads `dotnet tool install -g Spectra.CLI` (line 121). Confirm and move on.

**Checkpoint**: All documented install commands match the published `PackageId`. After the first nuget.org release lands, US1 is fully observable.

---

## Phase 5: User Story 3 - Internal pipelines no longer depend on private feed credentials (Priority: P2)

**Goal**: The bundled `deploy-dashboard.yml` template (the file `spectra init` ships into user repos) and the in-repo `.template` copy install the CLI from nuget.org with no source config and no `GH_PACKAGES_TOKEN` reference.

**Independent Test**: Grep both files for `GH_PACKAGES_TOKEN`, `nuget.pkg.github`, and `dotnet nuget add source`. All must return zero matches. Then re-run `spectra init` in a scratch directory and confirm the generated `.github/workflows/deploy-dashboard.yml` contains only the simple `dotnet tool install -g Spectra.CLI` install step.

### Implementation for User Story 3

- [ ] T011 [P] [US3] Edit `src/Spectra.CLI/Templates/deploy-dashboard.yml` (the canonical user-facing template, embedded as a resource and copied by `spectra init`):
  - Remove `GH_PACKAGES_TOKEN` from the `Required GitHub Secrets:` header comment block.
  - Replace the multi-line `Install SPECTRA CLI` step (which runs `dotnet nuget add source ... --password ${{ secrets.GH_PACKAGES_TOKEN }} ... && dotnet tool install -g Spectra.CLI`) with a single-line step: `run: dotnet tool install -g Spectra.CLI`.
  - Leave all other steps (Cloudflare deploy, summary, etc.) untouched.
- [ ] T012 [P] [US3] Edit `.github/workflows/deploy-dashboard.yml.template` (the in-repo example copy): change `dotnet tool install -g spectra-cli` → `dotnet tool install -g Spectra.CLI` (correct casing matching `<PackageId>`). No source-config or token changes needed (this file does not currently reference GitHub Packages — it's a latent casing bug that we are fixing now for consistency with the canonical template in T011).
- [ ] T013 [US3] (Manual, post-merge) After this branch is merged and the first nuget.org release succeeds, remove `GH_PACKAGES_TOKEN` from `AutomateThePlanet/Spectra` → Settings → Secrets → Actions. Documented here for traceability per spec FR-013/SC-009; not automatable.

**Checkpoint**: The user-facing template no longer references the private feed or its token. New `spectra init` runs produce a workflow that "just works" with zero secret setup.

---

## Phase 6: User Story 4 - Documentation matches reality (Priority: P2)

**Goal**: All user-facing docs show the new public-feed install flow. The legacy GitHub Packages setup page is deleted. No remaining links or references.

**Independent Test**: `grep -ri "github.packages\|nuget\.pkg\.github\|github-packages-setup\|GH_PACKAGES_TOKEN" docs/ README.md CLAUDE.md` — must return zero matches **outside** of `specs/` (which is allowed to retain historical context).

### Implementation for User Story 4

- [ ] T014 [P] [US4] Delete `docs/deployment/github-packages-setup.md`.
- [ ] T015 [P] [US4] Edit `docs/deployment.md`: remove the "GitHub Packages, " phrase from the body so the description reads "...Cloudflare Pages and Copilot Spaces" (or simply "...external integrations"). One-line change.
- [ ] T016 [US4] Run a final grep across `docs/`, `README.md`, `CLAUDE.md`, `AGENTS.md`, and `src/Spectra.CLI/Templates/` for the strings `github-packages-setup`, `nuget.pkg.github`, `GH_PACKAGES_TOKEN`, `read:packages`. Zero matches expected. (Matches inside `specs/035-nuget-org-publish/` are acceptable — they document the historical state.)

**Checkpoint**: Documentation is consistent end-to-end with the new public-feed flow.

---

## Phase 7: Polish & Verification

**Purpose**: Run the full local build/test cycle to confirm the .csproj edits introduced no regressions, and dry-validate the workflow YAML.

- [ ] T017 Run `dotnet build -c Release` from repo root — must succeed with no new warnings.
- [ ] T018 Run `dotnet test -c Release --no-build` from repo root — all existing tests must pass (sanity check that .csproj edits did not break the build graph or test discovery).
- [ ] T019 Validate `.github/workflows/publish.yml` YAML syntax: `gh workflow view publish.yml` (or any local YAML linter). Workflow must list correctly in the GitHub Actions UI.
- [ ] T020 Walk through `quickstart.md` mentally and confirm every command in the "Cutting a release" section maps to an existing step in `publish.yml`. No discrepancies.
- [ ] T021 Final commit: stage all changed files (3 .csproj, 1 workflow, 2 templates, 2 docs edits, 1 docs deletion) and create a single commit with message `feat(033): publish to NuGet.org instead of GitHub Packages`. Do **not** push the release tag yet — that is a separate maintainer action covered by quickstart.md.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001, T002 — read-only checks. No dependencies.
- **Phase 2 (Foundational)**: T003–T007 — must complete before Phase 3 (the workflow's pack step depends on the embedded README being present in all three .csproj files).
- **Phase 3 (US2 — workflow)**: T008 — depends on Phase 2 complete.
- **Phase 4 (US1 — docs casing)**: T009, T010 — independent of Phase 3 in code, but functionally observable only after Phase 3 ships and a tag is pushed. Can run in parallel with Phases 3, 5, 6.
- **Phase 5 (US3 — templates)**: T011, T012 — independent of all other phases. Can run in parallel.
- **Phase 6 (US4 — docs cleanup)**: T014, T015, T016 — independent of all other phases. Can run in parallel.
- **Phase 7 (Polish)**: T017–T021 — depends on Phases 2–6 all complete.

### Story Dependencies

- **US2 (P1)** depends on **Foundational** (Phase 2).
- **US1 (P1)** depends on **US2** functionally (publish must succeed before zero-config install can be observed), but the documentation tasks (T009, T010) have no code dependency and can land in parallel.
- **US3 (P2)** depends only on **Foundational** (no — actually independent; the templates do not consume the .csproj files).
- **US4 (P2)** is fully independent; documentation cleanup can land at any time.

### Within Each User Story

- All [P] tasks within a phase touch different files and can run in parallel.
- T006 (build), T007 (pack inspection), T017 (build), T018 (test) are sequential gates within their phases.

---

## Parallel Opportunities

```bash
# Phase 2 — all three .csproj edits in parallel:
T003 [P] Spectra.Core.csproj
T004 [P] Spectra.CLI.csproj
T005 [P] Spectra.MCP.csproj
# Then T006 → T007 sequentially.

# Phases 4, 5, 6 — all run in parallel after Phase 2:
T009 [P] docs/getting-started.md
T011 [P] src/Spectra.CLI/Templates/deploy-dashboard.yml
T012 [P] .github/workflows/deploy-dashboard.yml.template
T014 [P] delete docs/deployment/github-packages-setup.md
T015 [P] docs/deployment.md
```

---

## Implementation Strategy

### Single-pass MVP (recommended for this feature)

Because the feature is small (~9 file edits, 1 deletion) and all four user stories are tightly coupled to a single release, do all phases in one branch and merge as one PR:

1. T001–T002 (Setup, ~1 min)
2. T003–T007 (Foundational metadata, ~10 min, parallelizable)
3. T008 (Workflow rewrite, ~5 min)
4. T009–T010 (US1 docs, ~1 min)
5. T011–T012 (US3 templates, ~3 min)
6. T014–T016 (US4 docs cleanup, ~2 min)
7. T017–T021 (Polish + commit, ~5 min)
8. Merge PR.
9. Maintainer: push release tag per quickstart.md.
10. Maintainer: post-release manual cleanup (T013) — remove `GH_PACKAGES_TOKEN`.

---

## Notes

- This feature edits only build/release configuration. There are no new C# code paths and no new test code.
- Path conventions: project files are in `src/<Project>/<Project>.csproj`. Workflows are in `.github/workflows/`. The user-facing template that ships in `spectra init` is `src/Spectra.CLI/Templates/deploy-dashboard.yml`.
- The `LICENSE` file in repo root is **Apache 2.0**, not MIT (the original spec input had MIT — this was caught during research and corrected in research.md D4 and data-model.md).
- T013 is a manual administrative task; it is listed for traceability but cannot be performed by code.
- Re-pushing a tag is safe (`--skip-duplicate` makes the workflow idempotent), but a *failed partial push* requires bumping to the next patch version — see quickstart.md "Recovering from a bad release".
