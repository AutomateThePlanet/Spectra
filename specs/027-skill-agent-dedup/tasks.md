# Tasks: SKILL/Agent Prompt Deduplication

**Input**: Design documents from `/specs/027-skill-agent-dedup/`
**Prerequisites**: plan.md (required), spec.md (required)

**Tests**: Included — the spec explicitly requires new validation tests.

**Organization**: Tasks grouped by user story. SKILL fixes are foundational (agents delegate to them).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Foundational — SKILL Consistency Fixes

**Purpose**: Fix SKILL file inconsistencies so agents can reliably delegate to them. MUST complete before agent refactoring.

**⚠️ CRITICAL**: Agent delegation (US1) depends on SKILLs being consistent first.

- [x] T001 [P] Fix `spectra-list.md`: replace "Tool call N" with "Step N" format, replace `terminalLastCommand` with `readFile .spectra-result.json`, add `--no-interaction --output-format json --verbosity quiet` flags in `src/Spectra.CLI/Skills/Content/Skills/spectra-list.md`
- [x] T002 [P] Fix `spectra-init-profile.md`: replace "Tool call N" with "Step N" format, replace `terminalLastCommand` with `readFile .spectra-result.json`, add `--no-interaction --output-format json --verbosity quiet` flags in `src/Spectra.CLI/Skills/Content/Skills/spectra-init-profile.md`
- [x] T003 [P] Fix `spectra-validate.md`: replace "Tool call N" with "Step N" format in `src/Spectra.CLI/Skills/Content/Skills/spectra-validate.md`
- [x] T004 [P] Add "do NOTHING between runInTerminal and awaitTerminal" instruction to `src/Spectra.CLI/Skills/Content/Skills/spectra-coverage.md`
- [x] T005 [P] Add "do NOTHING between runInTerminal and awaitTerminal" instruction to `src/Spectra.CLI/Skills/Content/Skills/spectra-dashboard.md`
- [x] T006 [P] Add incremental vs force note to `src/Spectra.CLI/Skills/Content/Skills/spectra-docs.md`: "Incremental mode skips unchanged files (SHA-256 hash check). Use `--force` for a complete rebuild."
- [x] T007 [P] [US3] Add "Acceptance Criteria" section (extract, import, list, filter) and "Documentation Index" section (index, force-reindex) to `src/Spectra.CLI/Skills/Content/Skills/spectra-help.md`

**Checkpoint**: All 8 SKILL files now use consistent patterns (Step N, readFile, do NOTHING). Help SKILL covers all 8 command categories.

---

## Phase 2: User Story 1 — Agent Delegates CLI Tasks to SKILLs (Priority: P1) 🎯 MVP

**Goal**: Remove duplicated CLI command blocks from both agents, replace with delegation tables pointing to authoritative SKILL files.

**Independent Test**: Verify that neither agent contains full CLI command blocks for the 6 delegated tasks, and both contain delegation tables with correct SKILL names.

### Implementation for User Story 1

- [x] T008 [P] [US1] Refactor execution agent: remove 6 CLI blocks (coverage, criteria, dashboard, docs index, validate, list/show), remove help table, remove redundant "NEVER" warnings (MCP for dashboard, createFile, search web), keep essential warnings (askQuestion, fabricate), add delegation table and consolidated delegation rule. Target ≤200 lines in `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`
- [x] T009 [P] [US1] Refactor generation agent: remove 6 CLI blocks (coverage, criteria, dashboard, validate, list/show, docs index), remove help table, keep generate and update sections unchanged, add delegation table. Target ≤90 lines in `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`

**Checkpoint**: Both agents are routers. Execution ≤200 lines, generation ≤90 lines. All CLI commands have exactly one source of truth (their SKILL file).

---

## Phase 3: User Story 4 — Update-Skills Delivers New Content (Priority: P3)

**Goal**: Verify that `spectra update-skills` correctly detects changed content and delivers updates to users.

**Independent Test**: Run `dotnet test` to confirm all existing SKILL hash and update tests pass with the new content.

### Implementation for User Story 4

- [x] T010 [US4] Run full test suite (`dotnet test`) to verify all existing SKILL content, hash, init, and update-skills tests pass with changed embedded content. Fix any failures.

**Checkpoint**: All existing tests pass. Hash-based update mechanism works correctly with new content.

---

## Phase 4: Tests

**Purpose**: Add new validation tests to enforce the deduplication invariants going forward.

- [x] T011 [P] Add test: execution agent line count ≤ 200 in `tests/Spectra.CLI.Tests/`
- [x] T012 [P] Add test: generation agent line count ≤ 100 in `tests/Spectra.CLI.Tests/`
- [x] T013 [P] Add test: agent content does NOT contain full CLI command blocks (assert absence of `spectra ai analyze --coverage`, `spectra dashboard --output`, `spectra validate --no-interaction`, `spectra docs index`, `spectra ai analyze --extract-criteria`, `spectra ai analyze --list-criteria` as multi-line command blocks) in `tests/Spectra.CLI.Tests/`
- [x] T014 [P] Add test: all SKILL files use "Step N" format — assert no SKILL content contains "Tool call" in `tests/Spectra.CLI.Tests/`
- [x] T015 [P] Add test: no SKILL file uses `terminalLastCommand` — assert all JSON-output SKILLs use `readFile` in `tests/Spectra.CLI.Tests/`
- [x] T016 [P] Add test: help SKILL contains entries for all 8 command categories (generate, coverage, dashboard, validate, list, criteria, docs index, update) in `tests/Spectra.CLI.Tests/`

**Checkpoint**: 6 new tests enforce deduplication invariants. Any future duplication reintroduction will be caught.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Update documentation to reflect the refactoring.

- [x] T017 [P] Update `CLAUDE.md`: add 027-skill-agent-dedup entry to Recent Changes, update agent descriptions to mention delegation pattern and reduced line counts
- [x] T018 [P] Update `PROJECT-KNOWLEDGE.md`: update agent prompts section to describe delegation model, update SKILL format conventions to note "Step N" and `readFile` patterns

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — start immediately. All 7 tasks are parallelizable.
- **Phase 2 (US1 — Agent Delegation)**: Depends on Phase 1 completion. Both agent tasks (T008, T009) are parallelizable.
- **Phase 3 (US4 — Verify Delivery)**: Depends on Phase 2 completion (all content changes done).
- **Phase 4 (Tests)**: Depends on Phase 3 completion. All 6 test tasks are parallelizable.
- **Phase 5 (Polish)**: Depends on Phase 4 completion. Both doc tasks are parallelizable.

### User Story Dependencies

- **US2 (SKILL Consistency)**: Phase 1 — no dependencies on other stories
- **US3 (Help Completeness)**: Phase 1, T007 — no dependencies on other stories
- **US1 (Agent Delegation)**: Phase 2 — depends on US2 and US3 being complete (agents delegate to fixed SKILLs)
- **US4 (Update-Skills)**: Phase 3 — depends on all content changes being complete

### Parallel Opportunities

- **Phase 1**: All 7 SKILL fix tasks (T001–T007) can run in parallel — different files
- **Phase 2**: Both agent refactoring tasks (T008, T009) can run in parallel — different files
- **Phase 4**: All 6 test tasks (T011–T016) can run in parallel — test isolation

---

## Parallel Example: Phase 1 (SKILL Fixes)

```
# All 7 SKILL fixes in parallel:
Task: "Fix spectra-list.md step format and result reading"
Task: "Fix spectra-init-profile.md step format and result reading"
Task: "Fix spectra-validate.md step format"
Task: "Add do NOTHING to spectra-coverage.md"
Task: "Add do NOTHING to spectra-dashboard.md"
Task: "Add incremental note to spectra-docs.md"
Task: "Add criteria + docs sections to spectra-help.md"
```

## Parallel Example: Phase 2 (Agent Refactoring)

```
# Both agents in parallel:
Task: "Refactor execution agent — remove CLI blocks, add delegation table"
Task: "Refactor generation agent — remove CLI blocks, add delegation table"
```

---

## Implementation Strategy

### MVP First (User Story 1 — Agent Delegation)

1. Complete Phase 1: Fix SKILL consistency (7 parallel tasks)
2. Complete Phase 2: Refactor both agents (2 parallel tasks)
3. **STOP and VALIDATE**: Verify agents ≤ line limits, no CLI block duplication
4. Run `dotnet test` to confirm no regressions

### Full Delivery

1. Phase 1: SKILL fixes → Phase 2: Agent refactoring → Phase 3: Verify delivery
2. Phase 4: Add validation tests → Phase 5: Update documentation
3. Final `dotnet test` — all existing + new tests pass

---

## Notes

- No C# code changes needed — only embedded .md content files and tests
- SHA-256 hashes update automatically at runtime (computed from loaded content)
- Total tasks: 18
- Phase 1: 7 tasks (all parallel)
- Phase 2: 2 tasks (parallel)
- Phase 3: 1 task
- Phase 4: 6 tasks (all parallel)
- Phase 5: 2 tasks (parallel)
