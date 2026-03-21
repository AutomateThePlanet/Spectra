# Tasks: Open Source Ready

**Input**: Design documents from `/specs/014-open-source-ready/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, quickstart.md

**Tests**: Test fix tasks are included as a core user story. No new test files created — existing failing tests are fixed.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create directory structures needed by multiple stories

- [x] T001 Create `assets/` directory at repo root for README banner image
- [x] T002 Create `.github/ISSUE_TEMPLATE/` directory for issue templates

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Fix tests — blocks CI pipeline (can't have CI that fails)

**⚠️ CRITICAL**: CI pipeline (US2) depends on tests passing first

- [x] T003 Run `dotnet test` and capture full failure report — document all failing tests with their error messages in a comment for reference
- [x] T004 Fix `CriticFactoryTests` failures (7 tests) in `tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs` — these test `TryCreate_ValidProvider_NoApiKey_Fails` but CopilotCritic doesn't validate API keys at creation; update tests to match actual CopilotCritic behavior or mock the dependency
- [x] T005 Fix `QuickstartWorkflowTests.Step6_Show_DisplaysTestDetails` in `tests/Spectra.CLI.Tests/Integration/QuickstartWorkflowTests.cs` — add required test data setup or fix assertion
- [x] T006 Fix `GenerateCommandTests.Generate_WithExistingTests_AvoidsIdConflict` in `tests/Spectra.CLI.Tests/Commands/GenerateCommandTests.cs` — fix test isolation issue
- [x] T007 Fix remaining failing tests (investigate each, apply appropriate fix — mock, fixture, path fix)
- [x] T008 Run `dotnet test` and verify 100% pass rate — zero failures, document any skipped tests with reason

**Checkpoint**: All tests pass — CI pipeline can now be created

---

## Phase 3: User Story 1 — README Redesign (Priority: P1) 🎯 MVP

**Goal**: Professional README with banner, badges, value props, features, quickstart.

**Independent Test**: Visit repo on GitHub, verify banner renders, badges show live data, quickstart commands work.

### Implementation for User Story 1

- [x] T009 [US1] Create placeholder banner reference in `README.md` — add `<p align="center"><img src="assets/spectra_github_readme_banner.png" alt="SPECTRA" width="100%"></p>` at top (actual image to be added separately)
- [x] T010 [US1] Add badge row to `README.md` — NuGet CLI, NuGet MCP, CI status, License (MIT), .NET 8.0+ badges using shields.io
- [x] T011 [US1] Add one-liner tagline section to `README.md` — centered bold text: "AI-native test generation and execution framework"
- [x] T012 [US1] Add "Why SPECTRA?" section to `README.md` — 6 value propositions with emoji icons (documentation reading, AI guardrails, tests as Markdown, deterministic execution, coverage visibility, no migration needed)
- [x] T013 [US1] Add "Key Features" section to `README.md` — 6 detailed feature blocks: AI Test Generation, Grounding Verification, Coverage Analysis, Visual Dashboard, MCP Execution Engine, Generation Profiles
- [x] T014 [US1] Add "Quick Start" section to `README.md` — concise installation via `dotnet tool install -g Spectra.CLI`, then `spectra init` and `spectra ai generate` with example output
- [x] T015 [US1] Keep existing architecture diagram, ecosystem table, documentation links, project status sections — reorganize into the new README flow
- [x] T016 [US1] Verify all links in `README.md` resolve to real files — check every `docs/` link, every external URL, fix any broken references

**Checkpoint**: README is professionally redesigned with all sections

---

## Phase 4: User Story 2 — CI Pipeline (Priority: P1)

**Goal**: Automated build + test on every push/PR.

**Independent Test**: Push a commit to a branch, open a PR, verify CI workflow triggers and reports pass/fail.

### Implementation for User Story 2

- [x] T017 [US2] Create `.github/workflows/ci.yml` — trigger on push to main and PR to main; steps: checkout, setup .NET 8, restore, build Release, test Release with trx logger, upload test-results artifact (always)
- [x] T018 [US2] Verify CI workflow syntax is valid — run `actionlint` or manually verify YAML structure matches GitHub Actions schema

**Checkpoint**: CI pipeline runs on push/PR

---

## Phase 5: User Story 3 — NuGet Publishing (Priority: P2)

**Goal**: Tag-triggered NuGet package publishing.

**Independent Test**: Push a `v*` tag, verify publish workflow triggers and attempts to pack both packages.

### Implementation for User Story 3

- [x] T019 [US3] Create `.github/workflows/publish.yml` — trigger on tag `v*`; steps: checkout, setup .NET 8, extract version from tag, restore, build, test (gate), pack Spectra.CLI and Spectra.MCP with `/p:Version`, push to NuGet.org with `--skip-duplicate`
- [x] T020 [US3] Verify `dotnet pack` succeeds locally for both projects — run `dotnet pack src/Spectra.CLI/Spectra.CLI.csproj --configuration Release` and `dotnet pack src/Spectra.MCP/Spectra.MCP.csproj --configuration Release` and confirm .nupkg files are produced

**Checkpoint**: NuGet publishing pipeline ready (requires NUGET_API_KEY secret for actual publishing)

---

## Phase 6: User Story 5 & 6 — License & Documentation (Priority: P2)

**Goal**: Verify LICENSE is correct, documentation is complete and linked.

**Independent Test**: Click every link in README and docs. Verify LICENSE has MIT text.

### Implementation for User Story 5 & 6

- [x] T021 [P] [US5] Verify `LICENSE` file at repo root — confirm MIT text with correct copyright year and holder (Automate The Planet Ltd.)
- [x] T022 [US6] Walk all links in `docs/` files — verify every cross-reference resolves, fix broken links
- [x] T023 [US6] Verify `CONTRIBUTING.md` has current build/test/PR instructions — update if commands or paths have changed

**Checkpoint**: License verified, all doc links work

---

## Phase 7: User Story 7 — Community Templates & Tooling (Priority: P3)

**Goal**: Issue templates, PR template, Dependabot config.

**Independent Test**: Create issue on GitHub — verify templates appear. Open PR — verify checklist appears.

### Implementation for User Story 7

- [x] T024 [P] [US7] Create `.github/ISSUE_TEMPLATE/bug_report.md` — structured template with sections: description, steps to reproduce, expected behavior, actual behavior, environment (OS, .NET version, SPECTRA version)
- [x] T025 [P] [US7] Create `.github/ISSUE_TEMPLATE/feature_request.md` — template with sections: problem description, proposed solution, alternatives considered, additional context
- [x] T026 [P] [US7] Create `.github/pull_request_template.md` — checklist: tests pass, documentation updated, no breaking changes (or documented), description of changes
- [x] T027 [P] [US7] Create `.github/dependabot.yml` — NuGet ecosystem, root directory, weekly schedule

**Checkpoint**: Community templates in place

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation

- [x] T028 Update CLAUDE.md Recent Changes — add 014-open-source-ready entry
- [x] T029 Run full `dotnet build && dotnet test` to verify everything passes
- [x] T030 Verify `dotnet pack` succeeds for both Spectra.CLI and Spectra.MCP
- [x] T031 Run quickstart.md validation — verify all verification steps work

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: No dependencies — test fixes are independent. BLOCKS US2 (CI)
- **User Story 1 (Phase 3)**: No dependency on test fixes — can start in parallel
- **User Story 2 (Phase 4)**: Depends on Foundational (tests must pass before CI makes sense)
- **User Story 3 (Phase 5)**: Depends on US2 (CI should exist before publish pipeline)
- **User Story 5&6 (Phase 6)**: Independent — can start anytime
- **User Story 7 (Phase 7)**: Independent — can start anytime
- **Polish (Phase 8)**: Depends on all stories complete

### Parallel Opportunities

- T001, T002 can run in parallel (different directories)
- US1 (README) can run in parallel with Phase 2 (test fixes)
- T024, T025, T026, T027 can all run in parallel (different files)
- US5/US6 can run in parallel with everything else

---

## Implementation Strategy

### MVP First

1. Fix tests (Phase 2) → green suite
2. CI pipeline (Phase 4) → automated quality gate
3. README redesign (Phase 3) → professional first impression
4. **STOP and VALIDATE**: Tests pass, CI runs, README looks good

### Incremental Delivery

1. Tests + CI → quality foundation
2. README → first impression
3. NuGet publishing → installable tool
4. License/docs verification → completeness
5. Community templates → contributor experience
6. Polish → final validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Total: 31 tasks across 8 phases
- The banner image design is out of scope — a placeholder reference is added
- NuGet publishing requires `NUGET_API_KEY` secret configured by maintainer
