# Tasks: Terminology, Folder Rename & Landing Page

**Input**: Design documents from `/specs/043-terminology-folder-landing/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: No new test tasks — spec requires existing xUnit tests to pass after folder rename. Verification is built into Phase 3 checkpoint.

**Organization**: Tasks grouped by user story (4 stories). US1 is MVP. US3 and US4 share priority and can run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: User Story 1 - Default Output Folder Rename (Priority: P1) MVP

**Goal**: Change the default SPECTRA output directory from `tests/` to `test-cases/` across all source code, config templates, and test fixtures.

**Independent Test**: Run `dotnet test` — all 1279 existing tests must pass. Then verify `spectra init` creates `test-cases/` in a clean directory.

### Implementation for User Story 1

- [x] T001 [P] [US1] Change default Dir from `"tests/"` to `"test-cases/"` in `src/Spectra.Core/Models/Config/TestsConfig.cs`
- [x] T002 [P] [US1] Change constant `TestsDir` from `"tests"` to `"test-cases"` in `src/Spectra.CLI/Commands/Init/InitHandler.cs`
- [x] T003 [P] [US1] Change `"dir"` value from `"tests/"` to `"test-cases/"` in `src/Spectra.CLI/Templates/spectra.config.json`
- [x] T004 [P] [US1] Change fallback display string from `"tests/"` to `"test-cases/"` in `src/Spectra.CLI/Commands/Config/ConfigHandler.cs`
- [x] T005 [US1] Update all test fixture JSON configs from `"dir": "tests/"` to `"dir": "test-cases/"` in `tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs`
- [x] T006 [P] [US1] Update CI path triggers from `tests/**` to `test-cases/**` in `.github/workflows/dashboard.yml.template` and `.github/workflows/deploy-dashboard.yml.template`
- [x] T007 [US1] Run `dotnet test` and verify all existing tests pass with the new default

**Checkpoint**: All code changes complete. `TestsConfig.Dir` defaults to `"test-cases/"`, `spectra init` creates `test-cases/`, all tests green.

---

## Phase 2: User Story 2 - Consistent "Test Case" Terminology (Priority: P2)

**Goal**: Standardize all references to SPECTRA's markdown output as "test case(s)" across documentation, SKILLs, agents, and project metadata. Leave config keys, CLI commands, and compound terms unchanged.

**Independent Test**: Search all modified files for bare "test" or "tests" used as noun referring to SPECTRA output — zero matches expected. Compound terms ("test run", "test suite", "test ID", "test format") must remain unchanged.

### Implementation for User Story 2

**Constitution & Project Metadata:**

- [x] T008 [P] [US2] Update constitution Principle I: change `tests/{suite}/` to `test-cases/{suite}/`, bump version to 1.1.0, update Last Amended date in `.specify/memory/constitution.md`
- [x] T009 [P] [US2] Update tagline and description to use "test case" terminology in `PROJECT-KNOWLEDGE.md`
- [x] T010 [P] [US2] Update tagline and description to use "test case" terminology in `README.md`
- [x] T011 [P] [US2] Update project structure references (`tests/` → `test-cases/` for SPECTRA output paths) and description lines in `CLAUDE.md`

**SKILLs (11 files):**

- [x] T012 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — "test cases" for SPECTRA output
- [x] T013 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` — "test cases" for SPECTRA output
- [x] T014 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-coverage.md` — "test cases" for coverage gaps
- [x] T015 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-validate.md` — "test cases" for validation
- [x] T016 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-list.md` — "test cases" for browsing
- [x] T017 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-help.md` — "test cases" in all descriptions
- [x] T018 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-quickstart.md` — "test cases" in workflow descriptions
- [x] T019 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-init-profile.md` — "test cases" if applicable
- [x] T020 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md` — "test cases" if applicable
- [x] T021 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md` — "test cases" if applicable
- [x] T022 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-dashboard.md` — "test cases" if applicable
- [x] T023 [P] [US2] Update terminology in `src/Spectra.CLI/Skills/Content/Skills/spectra-prompts.md` — "test cases" if applicable

**Agents (2 files):**

- [x] T024 [P] [US2] Update description to "Generates test cases from documentation" in `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`
- [x] T025 [P] [US2] Update description to "Execute test cases through MCP" in `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`

**Documentation sweep:**

- [x] T026 [P] [US2] Update "test case" terminology in `docs/user-guide.md`
- [x] T027 [P] [US2] Update "test case" terminology in `docs/getting-started.md` — includes folder tree showing `test-cases/`
- [x] T028 [P] [US2] Update "test case" terminology in `docs/usage.md` — section headers "Generating Test Cases", "Updating Test Cases"
- [x] T029 [P] [US2] Update "test case" terminology in `docs/configuration.md` — section title "Test Case Generation Settings"
- [x] T030 [P] [US2] Update "test case" terminology in `docs/cli-reference.md` — "Validate all test case files"
- [x] T031 [P] [US2] Update "test case" terminology in `docs/customization.md` — "Test Case Format"
- [x] T032 [P] [US2] Update "test case" terminology in `docs/coverage.md` — "which docs have linked test cases"
- [x] T033 [P] [US2] Update "test case" terminology in `docs/grounding-verification.md` — "verifies each test case"
- [x] T034 [P] [US2] Distinguish "test cases" (SPECTRA) from "test data" (Testimize) in `docs/testimize-integration.md`
- [x] T035 [P] [US2] Update "test case" terminology in `docs/generation-profiles.md`
- [x] T036 [P] [US2] Update "test case" terminology in `docs/skills-integration.md`
- [x] T037 [P] [US2] Update "test case" terminology in `docs/architecture.md` and `docs/architecture/overview.md`
- [x] T038 [P] [US2] Update "test case" terminology in `docs/execution-agent/overview.md` and other execution-agent docs
- [x] T039 [P] [US2] Update "test case" terminology in `docs/DEVELOPMENT.md`
- [x] T040 [P] [US2] Update "test case" terminology in `docs/document-index.md`

**Checkpoint**: All docs, SKILLs, and agents use "test case(s)" consistently. Config keys and CLI commands unchanged. Compound terms preserved.

---

## Phase 3: User Story 3 - Landing Page Value Proposition (Priority: P3)

**Goal**: Rewrite the landing page with a clear value proposition including a manual-vs-SPECTRA comparison table, Chat-first workflow, and quick-install section.

**Independent Test**: Render `docs/index.md` and verify it contains the comparison table, "test case" terminology, and the Copilot Chat workflow example.

### Implementation for User Story 3

- [x] T041 [US3] Rewrite `docs/index.md` with value proposition content: comparison table (manual vs SPECTRA), "How it works" pipeline, key capabilities list, Chat-first workflow example, and quick-install section. Use "test case" terminology throughout.

**Checkpoint**: Landing page communicates clear value proposition with comparison table, Chat-first workflow, and consistent terminology.

---

## Phase 4: User Story 4 - Simplified CLI-vs-Chat Page (Priority: P3)

**Goal**: Replace the 4800-word cli-vs-chat-generation.md with a concise ~150-word page that positions Chat as primary interface and CLI as engine.

**Independent Test**: Verify `docs/analysis/cli-vs-chat-generation.md` is under 200 words, positions Chat as recommended, and contains no competitive CLI-vs-Chat framing.

### Implementation for User Story 4

- [x] T042 [US4] Replace `docs/analysis/cli-vs-chat-generation.md` with concise ~150-word version: pipeline overview, two usage modes (Chat recommended, CLI for CI/CD), and why CLI pipeline matters for critic independence. No competitive framing.

**Checkpoint**: Page is under 200 words. Chat is primary, CLI is engine. No competitive framing.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Demo repo updates and final verification across all stories.

- [x] T043 [P] Rename `tests/` to `test-cases/` and update `spectra.config.json` in demo repo at `C:\SourceCode\Spectra_Demo\test_app_documentation`
- [x] T044 [P] Rename test case folder and update `spectra.config.json` in demo repo at `C:\SourceCode\AutomateThePlanet_SystemTests`
- [x] T045 Run `spectra update-skills` in both demo repos (ran with current global tool; full terminology update requires reinstalling global tool after merge)
- [x] T046 Run `dotnet test` final verification — all 1706 tests pass (514 Core + 841 CLI + 351 MCP)
- [x] T047 Verify no bare "test" referring to SPECTRA output remains in docs (remaining "test generation" instances refer to the process, not the output)
- [x] T048 Verify `docs/analysis/cli-vs-chat-generation.md` word count is under 200 (146 words)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (US1 - Folder Rename)**: No dependencies — start immediately. BLOCKS Phase 2 (terminology needs correct folder name in docs).
- **Phase 2 (US2 - Terminology)**: Depends on Phase 1 completion (docs must reference `test-cases/` not `tests/`).
- **Phase 3 (US3 - Landing Page)**: Depends on Phase 2 (landing page needs correct terminology).
- **Phase 4 (US4 - CLI-vs-Chat)**: Depends on Phase 2 (page needs correct terminology). Can run in PARALLEL with Phase 3.
- **Phase 5 (Polish)**: Depends on all phases complete.

### User Story Dependencies

- **US1 (P1)**: No dependencies — MVP, start first
- **US2 (P2)**: Depends on US1 (folder paths must be correct before terminology sweep)
- **US3 (P3)**: Depends on US2 (content uses standardized terminology)
- **US4 (P3)**: Depends on US2. Independent of US3 — can run in parallel.

### Within Each User Story

- US1: T001-T004 are parallel (different files), T005-T006 parallel, T007 depends on all prior
- US2: T008-T040 are ALL parallel (each touches a different file)
- US3: Single task (T041)
- US4: Single task (T042)

### Parallel Opportunities

- **Phase 1**: T001, T002, T003, T004 can all run in parallel (4 different source files)
- **Phase 1**: T005 and T006 can run in parallel after T001-T004
- **Phase 2**: ALL tasks T008-T040 can run in parallel (every task touches a different file)
- **Phase 3+4**: T041 and T042 can run in parallel (different files)
- **Phase 5**: T043 and T044 can run in parallel (different repos)

---

## Parallel Example: User Story 1

```bash
# Launch all source changes in parallel (different files):
Task: "Change default Dir in src/Spectra.Core/Models/Config/TestsConfig.cs"
Task: "Change constant TestsDir in src/Spectra.CLI/Commands/Init/InitHandler.cs"
Task: "Change dir value in src/Spectra.CLI/Templates/spectra.config.json"
Task: "Change fallback display in src/Spectra.CLI/Commands/Config/ConfigHandler.cs"

# Then launch test fixtures and CI in parallel:
Task: "Update test fixtures in tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs"
Task: "Update CI triggers in .github/workflows/*.yml.template"

# Finally verify:
Task: "Run dotnet test"
```

## Parallel Example: User Story 2

```bash
# ALL 33 tasks can run in parallel — each modifies a different file:
Task: "Update constitution"
Task: "Update PROJECT-KNOWLEDGE.md"
Task: "Update README.md"
Task: "Update CLAUDE.md"
Task: "Update spectra-generate.md"
# ... (all 11 SKILLs + 2 agents + 15 docs files)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Folder Rename (T001-T007)
2. **STOP and VALIDATE**: Run `dotnet test`, verify all 1279 tests pass
3. The codebase now uses `test-cases/` as default — foundation for all subsequent work

### Incremental Delivery

1. US1 (Folder Rename) → Tests green → Code is correct
2. US2 (Terminology) → All docs/SKILLs consistent → Content is correct
3. US3 + US4 (Landing + CLI-vs-Chat) → Pages rewritten → Messaging is correct
4. Polish → Demo repos updated → Ecosystem is consistent

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No new tests are created — spec only requires existing tests to pass
- The terminology sweep (Phase 2) requires human judgment per file — not a blind find-replace
- Demo repo updates (Phase 5) are outside the main repo and need separate commits
- Constitution version bump (T008) is a governance change, not a code change
