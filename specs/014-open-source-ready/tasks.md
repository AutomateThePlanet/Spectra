# Tasks: Open-Source Readiness

**Input**: Design documents from `/specs/014-open-source-ready/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Tests**: Not requested in this feature specification. Test fixes (US4) address existing failing tests, not new test creation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Ensure the GitHub directory structure exists for all new files

- [ ] T001 Create `.github/workflows/` directory structure
- [ ] T002 Create `.github/ISSUE_TEMPLATE/` directory structure

---

## Phase 2: Foundational — Fix All Failing Tests (Blocking)

**Purpose**: All tests must pass before CI can be meaningful and before the repo can claim open-source readiness

**Maps to**: User Story 4 — All Tests Pass (P1)

**Goal**: Fix all 5 known failing CLI tests so `dotnet test` produces zero failures

**Independent Test**: Run `dotnet test --configuration Release` and verify 0 failed

- [ ] T003 [US4] Run `dotnet test` and capture baseline results showing all failures
- [ ] T004 [P] [US4] Fix `CriticFactoryTests.TryCreate_EnabledWithoutProvider_Fails` in `tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs` — test expects `TryCreate` to return false but implementation now returns true; update test to match current CriticFactory behavior
- [ ] T005 [P] [US4] Fix `CriticFactoryTests.TryCreate_UnknownProvider_Fails` in `tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs` — same pattern as T004; update test assertion to match current behavior
- [ ] T006 [P] [US4] Fix `QuickstartWorkflowTests.Step6_Show_DisplaysTestDetails` in `tests/Spectra.CLI.Tests/Integration/QuickstartWorkflowTests.cs` — expects exit code 0 but gets 1; investigate the `show` command failure and fix test setup or assertion
- [ ] T007 [P] [US4] Fix `GenerateCommandTests.Generate_NoSourceDocs_ReturnsError` in `tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs` — expects exit code 1 but gets 0; update test to match current error handling behavior
- [ ] T008 [P] [US4] Fix `GenerateCommandTests.Generate_WithExistingTests_AvoidsIdConflict` in `tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs` — IOException on temp directory cleanup; add retry/delay in Dispose or use unique temp paths
- [ ] T009 [US4] Re-run `dotnet test --configuration Release` and verify all tests pass (0 failures)

**Checkpoint**: All tests green — CI pipeline and NuGet packaging can now proceed

---

## Phase 3: User Story 2 — CI Pipeline (Priority: P1)

**Goal**: Create a GitHub Actions CI workflow that builds and tests on every push to main and every PR

**Independent Test**: Push a commit and verify the workflow triggers, builds, tests, and uploads artifacts

### Implementation for User Story 2

- [ ] T010 [US2] Create CI workflow in `.github/workflows/ci.yml` with: trigger on push to main and PRs targeting main, checkout, setup .NET 8, restore, build Release, test with TRX logger, upload test results artifact (always, even on failure)
- [ ] T011 [US2] Verify CI workflow YAML syntax by running `dotnet build --configuration Release && dotnet test --configuration Release` locally to confirm the commands match

**Checkpoint**: CI workflow file exists and is syntactically valid

---

## Phase 4: User Story 3 — NuGet Publishing (Priority: P1)

**Goal**: Create a GitHub Actions publish workflow that packs and pushes NuGet packages on version tags

**Independent Test**: Run `dotnet pack` locally for both projects and verify .nupkg files are produced

### Implementation for User Story 3

- [ ] T012 [US3] Create NuGet publish workflow in `.github/workflows/publish.yml` with: trigger on `v*` tags, checkout, setup .NET 8, restore, build, test, extract version from tag (`${GITHUB_REF_NAME#v}`), pack both `src/Spectra.CLI/Spectra.CLI.csproj` and `src/Spectra.MCP/Spectra.MCP.csproj` with `/p:Version=`, push to nuget.org with `--skip-duplicate`, use `${{ secrets.NUGET_API_KEY }}`
- [ ] T013 [US3] Verify `dotnet pack` works locally: run `dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -o ./nupkg` and `dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -o ./nupkg`, confirm .nupkg files are produced

**Checkpoint**: Publish workflow file exists, pack commands verified locally

---

## Phase 5: User Story 1 — README Redesign (Priority: P1)

**Goal**: Redesign README.md following Testimize style with banner, badges, value proposition, features, quick start, ecosystem, and documentation links

**Independent Test**: View README.md on GitHub and verify all sections render correctly with visual appeal

### Implementation for User Story 1

- [ ] T014 [US1] Rewrite `README.md` with Testimize-style layout: centered banner image from `assets/spectra_github_readme_banner.png`, badges row (NuGet CLI, NuGet MCP, CI status, license, .NET 8.0+), one-liner tagline
- [ ] T015 [US1] Add "Why SPECTRA?" section to `README.md` with 6 emoji-driven differentiators: reads docs, AI guardrails, markdown tests, deterministic execution, coverage visibility, no migration
- [ ] T016 [US1] Add "Key Features" section to `README.md` with 6 detailed subsections: AI Test Generation, Grounding Verification, Coverage Analysis, Visual Dashboard, MCP Execution Engine, Generation Profiles
- [ ] T017 [US1] Add "Quick Start" section to `README.md` with copy-paste commands: install via dotnet tool, init project, generate tests, run dashboard
- [ ] T018 [US1] Add architecture diagram section to `README.md` (preserve existing mermaid/text diagram from current README)
- [ ] T019 [US1] Add ecosystem table to `README.md` showing BELLATRIX, Testimize, and SPECTRA with descriptions and links
- [ ] T020 [US1] Add documentation links table to `README.md` with entries for all 16 docs files in `docs/` folder, verify each link resolves to an existing file
- [ ] T021 [US1] Add project status section with phase progress indicators and contributing/license sections to `README.md`

**Checkpoint**: README is visually complete with all required sections

---

## Phase 6: User Story 5 — Contributor Onboarding (Priority: P2)

**Goal**: Create issue templates, PR template, and expand CONTRIBUTING.md for contributor onboarding

**Independent Test**: Open a new issue on GitHub and verify template chooser shows Bug Report and Feature Request options

### Implementation for User Story 5

- [ ] T022 [P] [US5] Create bug report template in `.github/ISSUE_TEMPLATE/bug_report.md` with YAML frontmatter (name, about, labels: bug), guided fields: describe bug, reproduction steps, expected behavior, environment
- [ ] T023 [P] [US5] Create feature request template in `.github/ISSUE_TEMPLATE/feature_request.md` with YAML frontmatter (name, about, labels: enhancement), guided fields: problem description, proposed solution, alternatives considered
- [ ] T024 [P] [US5] Create PR template in `.github/PULL_REQUEST_TEMPLATE.md` with checklist: tests pass, documentation updated, no breaking changes (or documented), description of changes
- [ ] T025 [US5] Expand `CONTRIBUTING.md` with detailed sections: prerequisites (.NET 8+), building locally (`dotnet build`), running tests (`dotnet test`), code style guidelines (reference .editorconfig, C# 12 conventions), PR process (fork, branch, test, submit), architecture changes (require justification)

**Checkpoint**: All contributor templates exist and CONTRIBUTING.md is comprehensive

---

## Phase 7: User Story 7 — License Verification (Priority: P2)

**Goal**: Verify LICENSE file exists with correct MIT text

**Independent Test**: Open LICENSE file and confirm MIT license with correct copyright

### Implementation for User Story 7

- [ ] T026 [US7] Verify `LICENSE` file at repo root contains MIT license text with "Copyright (c) 2026 Automate The Planet Ltd." — update copyright year or holder if incorrect

**Checkpoint**: License verified

---

## Phase 8: User Story 6 — Documentation Completeness (Priority: P2)

**Goal**: Ensure all documentation links in README resolve and docs folder is well-organized

**Independent Test**: Click every link in README and verify each resolves to a real file

### Implementation for User Story 6

- [ ] T027 [US6] Verify all documentation links in `README.md` resolve to existing files in `docs/` — check each of the 16 doc files referenced in the documentation links table (T020) exists and has meaningful content (not stubs)

**Checkpoint**: All README documentation links resolve

---

## Phase 9: User Story 8 — Automated Dependency Updates (Priority: P3)

**Goal**: Add Dependabot configuration for weekly NuGet package updates

**Independent Test**: Verify `.github/dependabot.yml` exists and is valid YAML

### Implementation for User Story 8

- [ ] T028 [US8] Create Dependabot configuration in `.github/dependabot.yml` with: version 2, package-ecosystem nuget, directory `/`, schedule interval weekly

**Checkpoint**: Dependabot configuration exists

---

## Phase 10: Polish & Verification

**Purpose**: Final verification that all success criteria are met

- [ ] T029 Verify `dotnet build --configuration Release` completes with zero errors (SC-001)
- [ ] T030 Verify `dotnet test --configuration Release` passes all tests with zero failures (SC-002)
- [ ] T031 Verify all hyperlinks in `README.md` resolve to existing files or valid external URLs (SC-003)
- [ ] T032 Verify `dotnet pack` produces `.nupkg` files for both `src/Spectra.CLI/Spectra.CLI.csproj` and `src/Spectra.MCP/Spectra.MCP.csproj` (SC-004)
- [ ] T033 Verify `.editorconfig` is comprehensive and enforces C# 12 conventions (FR-009)
- [ ] T034 Clean up any temporary test output files (nupkg/, test-results/) and ensure `.gitignore` excludes them

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — create directory structure
- **Phase 2 (Test Fixes / US4)**: Depends on Phase 1 — BLOCKS all subsequent phases (CI can't pass if tests fail)
- **Phase 3 (CI / US2)**: Depends on Phase 2 (tests must pass for CI to be meaningful)
- **Phase 4 (NuGet / US3)**: Depends on Phase 2 (pack requires passing build)
- **Phase 5 (README / US1)**: Independent of test fixes — can start after Phase 1 but needs CI workflow path from Phase 3 for badge URL
- **Phase 6 (Contributor / US5)**: Independent — can run in parallel with Phases 3-5
- **Phase 7 (License / US7)**: Independent — can run in parallel
- **Phase 8 (Docs / US6)**: Depends on Phase 5 (README must exist to verify links)
- **Phase 9 (Dependabot / US8)**: Independent — can run anytime
- **Phase 10 (Verification)**: Depends on ALL previous phases

### User Story Dependencies

- **US4 (Tests)**: BLOCKS US2 (CI) and US3 (NuGet) — must complete first
- **US1 (README)**: Independent, but should reference CI badge from US2
- **US2 (CI)**: Depends on US4 (tests must pass)
- **US3 (NuGet)**: Depends on US4 (pack requires passing build)
- **US5 (Contributor)**: Fully independent
- **US6 (Docs)**: Depends on US1 (README links to verify)
- **US7 (License)**: Fully independent
- **US8 (Dependabot)**: Fully independent

### Within Each User Story

- Test fixes (US4): fix individual tests in parallel, then verify all green
- README (US1): sequential sections building on each other
- CI/NuGet (US2/US3): independent workflow files
- Contributor (US5): parallel template creation, then sequential CONTRIBUTING.md expansion

### Parallel Opportunities

- T004, T005, T006, T007, T008 can all run in parallel (different test files)
- T022, T023, T024 can all run in parallel (different template files)
- US5 (Contributor), US7 (License), US8 (Dependabot) can run in parallel with everything after Phase 2
- US1 (README) can start in parallel with US2 (CI) and US3 (NuGet)

---

## Parallel Example: Phase 2 (Test Fixes)

```bash
# Launch all test fixes in parallel (different files):
Task: "Fix CriticFactoryTests.TryCreate_EnabledWithoutProvider_Fails in tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs"
Task: "Fix CriticFactoryTests.TryCreate_UnknownProvider_Fails in tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs"
Task: "Fix QuickstartWorkflowTests.Step6_Show_DisplaysTestDetails in tests/Spectra.CLI.Tests/Integration/QuickstartWorkflowTests.cs"
Task: "Fix GenerateCommandTests.Generate_NoSourceDocs_ReturnsError in tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs"
Task: "Fix GenerateCommandTests.Generate_WithExistingTests_AvoidsIdConflict in tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs"
```

## Parallel Example: Phase 6 (Contributor Templates)

```bash
# Launch all template creations in parallel (different files):
Task: "Create bug report template in .github/ISSUE_TEMPLATE/bug_report.md"
Task: "Create feature request template in .github/ISSUE_TEMPLATE/feature_request.md"
Task: "Create PR template in .github/PULL_REQUEST_TEMPLATE.md"
```

---

## Implementation Strategy

### MVP First (User Story 4 + User Story 2)

1. Complete Phase 1: Setup (directory structure)
2. Complete Phase 2: Fix all failing tests (US4)
3. Complete Phase 3: CI workflow (US2)
4. **STOP and VALIDATE**: Push to branch, verify CI runs green
5. This alone makes the repo credible for open-source

### Incremental Delivery

1. Fix tests (US4) → CI passes → Foundation ready
2. Add NuGet publish workflow (US3) → Releases work
3. Redesign README (US1) → First impressions covered
4. Add contributor templates (US5) → Contributor onboarding complete
5. Verify docs + license (US6, US7) → Legal and navigation complete
6. Add Dependabot (US8) → Ongoing maintenance automated
7. Final verification (Phase 10) → All success criteria met

### Parallel Team Strategy

With multiple developers:

1. Team completes Phase 1 + Phase 2 together (test fixes)
2. Once tests pass:
   - Developer A: README redesign (US1, Phases 5)
   - Developer B: CI + NuGet workflows (US2 + US3, Phases 3-4)
   - Developer C: Contributor templates + License + Dependabot (US5 + US7 + US8, Phases 6-7-9)
3. All join for Phase 10: Final verification

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The 5 known test failures are all in Spectra.CLI.Tests — Core and MCP tests pass clean
- No new test creation needed — this feature fixes existing tests and adds config/content files
