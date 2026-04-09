# Tasks: SPECTRA Update SKILL + Documentation Sync

**Input**: Design documents from `/specs/029-update-skill-docs-sync/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Included per spec requirement (10-16 new tests).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No project initialization needed — existing codebase. This phase is a no-op.

**Checkpoint**: Ready to proceed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend UpdateResult model with fields required by the SKILL before creating the SKILL itself.

**CRITICAL**: The SKILL reads `.spectra-result.json` and expects these fields. Must complete before US1.

- [x] T001 Add `FlaggedTestEntry` record class to `src/Spectra.CLI/Results/UpdateResult.cs` with properties: Id (string), Title (string), Classification (string), Reason (string) — all with `[JsonPropertyName]` attributes
- [x] T002 Add new fields to `UpdateResult` class in `src/Spectra.CLI/Results/UpdateResult.cs`: Success (bool), TotalTests (int), TestsFlagged (int), FlaggedTests (IReadOnlyList<FlaggedTestEntry>?), Duration (string?) — matching JSON schema from data-model.md
- [x] T003 Update `UpdateHandler` in `src/Spectra.CLI/Commands/Update/UpdateHandler.cs` to populate the new UpdateResult fields (TotalTests = sum of classification counts, TestsFlagged = orphaned + redundant, FlaggedTests list from classified tests, Duration from stopwatch, Success = true on completion)

**Checkpoint**: UpdateResult model complete — SKILL can now read all expected fields from `.spectra-result.json`.

---

## Phase 3: User Story 1+2 - SKILL File + Bundled Content (Priority: P1) MVP

**Goal**: Create the `spectra-update` SKILL as the 10th embedded resource so `spectra init` and `spectra update-skills` include it automatically.

**Independent Test**: Run `spectra init` in a temp directory; verify `.github/skills/spectra-update/SKILL.md` exists with correct frontmatter.

### Implementation for User Story 1+2

- [x] T004 [US1] Create embedded SKILL resource file at `src/Spectra.CLI/Skills/Content/Skills/spectra-update.md` following the 5-step progress flow pattern from `spectra-docs.md`: frontmatter with `name: spectra-update`, `tools: [{{READONLY_TOOLS}}]`, `model: GPT-4o`, `disable-model-invocation: true`; body with Step 1 (show preview), Step 2 (runInTerminal with `spectra ai update --suite <suite> --no-interaction --output-format json --verbosity quiet`), Step 3 (awaitTerminal with "do NOTHING" instruction), Step 4 (readFile `.spectra-result.json` with classification presentation), Step 5 (suggest next steps); plus preview-with-diff section, update-all-suites section, classification meanings, and example user requests
- [x] T005 [US1] Add `Update` property to `src/Spectra.CLI/Skills/SkillContent.cs`: `public static string Update => All["spectra-update"];` following the pattern of existing properties (line 10-19)
- [x] T006 [US1] Verify the embedded resource is correctly discovered by building the project (`dotnet build src/Spectra.CLI`) and confirming no errors about missing resource

### Tests for User Story 1+2

- [x] T007 [P] [US1] Add test in `tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs`: update existing SKILL count assertion from 9 to 10 SKILLs in `SkillContent.All`
- [x] T008 [P] [US1] Add test: `spectra-update` SKILL content contains `--no-interaction --output-format json --verbosity quiet` flags
- [x] T009 [P] [US1] Add test: `spectra-update` SKILL uses `**Step` format (not `### Tool call`)
- [x] T010 [P] [US1] Add test: `spectra-update` SKILL contains "do NOTHING" wait instruction between runInTerminal and awaitTerminal
- [x] T011 [P] [US1] Add test: `spectra-update` SKILL frontmatter contains `browser/openBrowserPage` OR `{{READONLY_TOOLS}}` in tools list

**Checkpoint**: `spectra init` creates all 10 SKILL files including `spectra-update`. `spectra update-skills` manages the new SKILL.

---

## Phase 4: User Story 3 - Agent Delegation (Priority: P2)

**Goal**: Both agents delegate update requests to the `spectra-update` SKILL instead of handling inline.

**Independent Test**: Inspect agent content for delegation entries; verify no raw CLI blocks for update outside delegation table.

### Implementation for User Story 3

- [x] T012 [US3] Modify `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`: remove the inline "Update tests" section (lines 56-67 with Step 1-4 and CLI command block) and add a delegation row in the "Other tasks (delegation)" table: `| Update tests | spectra-update | spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet |`
- [x] T013 [US3] Modify `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`: add delegation row in the "CLI Tasks (delegation)" table: `| Update tests | spectra-update | spectra ai update --suite {suite} --no-interaction --output-format json --verbosity quiet |`

### Tests for User Story 3

- [x] T014 [P] [US3] Add test: generation agent content contains `spectra-update` delegation entry (string contains assertion on `AgentContent`)
- [x] T015 [P] [US3] Add test: execution agent content contains `spectra-update` delegation entry (string contains assertion on `AgentContent`)
- [x] T016 [P] [US3] Add test: no agent content contains fenced code block with `spectra ai update` (negative assertion — delegation only, no duplicated CLI blocks)

**Checkpoint**: Both agents correctly route "update tests" requests to the SKILL.

---

## Phase 5: User Story 4 - Documentation Sync (Priority: P3)

**Goal**: All documentation files reflect 10 bundled SKILLs with consistent counts and tables.

**Independent Test**: Search docs for SKILL count references; all should say "10."

### Implementation for User Story 4

- [x] T017 [P] [US4] Update `PROJECT-KNOWLEDGE.md`: change "9 Bundled SKILLs" to "10 Bundled SKILLs", add `spectra-update` row to SKILL table (second position after `spectra-generate`), update agent table descriptions to mention update delegation, add spec 029 to completed specs table
- [x] T018 [P] [US4] Update `CLAUDE.md`: add spec 029 entry in Recent Changes section: "029-spectra-update-skill: COMPLETE - Added spectra-update SKILL (10th bundled SKILL) for test update workflow via Copilot Chat..."
- [x] T019 [P] [US4] Update `README.md`: update SKILL count reference to 10 in Copilot Chat integration section
- [x] T020 [P] [US4] Update `CHANGELOG.md`: add new entry for spectra-update SKILL addition

**Checkpoint**: All documentation consistently references 10 bundled SKILLs.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and version bump.

- [x] T021 Run full test suite (`dotnet test`) — verify all tests pass with no regressions
- [x] T022 Verify version bump if needed in project files (check existing version convention)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No-op
- **Foundational (Phase 2)**: No dependencies — UpdateResult extensions
- **US1+2 (Phase 3)**: Depends on Phase 2 (SKILL reads UpdateResult fields)
- **US3 (Phase 4)**: Independent of Phase 3 (agent changes don't depend on SKILL content)
- **US4 (Phase 5)**: Independent of Phases 3-4 (documentation is standalone)
- **Polish (Phase 6)**: Depends on all phases complete

### User Story Dependencies

- **US1+2 (P1)**: Depends on Foundational (Phase 2) for UpdateResult fields
- **US3 (P2)**: Can start after Phase 2 — agent files are independent of SKILL resource
- **US4 (P3)**: Can start immediately — documentation changes are independent

### Within Each User Story

- T001 → T002 → T003 (sequential: class first, then fields, then handler)
- T004 → T005 → T006 (sequential: resource, property, build verify)
- T012, T013 can run in parallel (different agent files)
- T017, T018, T019, T020 can all run in parallel (different doc files)
- All test tasks within a phase can run in parallel

### Parallel Opportunities

- Phase 4 (US3) and Phase 5 (US4) can run in parallel with each other
- All [P] marked tasks within each phase can run in parallel
- T007-T011 (US1 tests) can all run in parallel after T004-T006
- T014-T016 (US3 tests) can all run in parallel after T012-T013

---

## Parallel Example: User Story 3 + User Story 4

```bash
# These can run simultaneously after Phase 2:

# Agent delegation (US3):
Task: "Modify spectra-generation.agent.md delegation table"
Task: "Modify spectra-execution.agent.md delegation table"

# Documentation (US4) — all in parallel:
Task: "Update PROJECT-KNOWLEDGE.md"
Task: "Update CLAUDE.md"
Task: "Update README.md"
Task: "Update CHANGELOG.md"
```

---

## Implementation Strategy

### MVP First (User Story 1+2 Only)

1. Complete Phase 2: Foundational (UpdateResult extensions)
2. Complete Phase 3: SKILL file + bundled content
3. **STOP and VALIDATE**: Build and run tests — SKILL file exists, count is 10
4. The SKILL is functional even without agent delegation (can be invoked directly)

### Incremental Delivery

1. Phase 2 → UpdateResult ready
2. Phase 3 → SKILL created and bundled (MVP!)
3. Phase 4 → Agents delegate to SKILL (discoverability)
4. Phase 5 → Documentation consistent (completeness)
5. Phase 6 → Full validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- The SKILL resource auto-discovery means `InitHandler.cs` and `SkillsManifest.cs` need no code changes
- `AgentContent.cs` auto-loads from embedded resources — no code changes needed there either
- Total: 22 tasks (3 foundational, 8 US1+2, 5 US3, 4 US4, 2 polish)
