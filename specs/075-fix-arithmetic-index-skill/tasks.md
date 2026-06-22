# Tasks: Critic Arithmetic Mandate, Index-Writer Path Fix, Skill Failure-Branch Guard

**Input**: Design documents from `/specs/075-fix-arithmetic-index-skill/`
**Branch**: `075-fix-arithmetic-index-skill`
**Spec**: 075

**Organization**: Tasks follow the three-phase implementation order from the spec (Phase 1 first — highest severity + independent; Phase 2 — writer fix + cleanup; Phase 3 — skill guard + sweep + docs). No setup or foundational phase needed — no new infrastructure.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1=Critic arithmetic mandate, US2=Index writer path fix, US3=Skill failure-branch guard, US4=Poisoned index heal, US5=Arithmetic sweep

---

## Phase 1: User Story 1 — Critic Arithmetic Mandate (Priority: P1) 🎯 MVP

**Goal**: Add an explicit arithmetic mandate to `critic-verification.md` so the critic independently verifies computed expected values and returns NOT-grounded for wrong arithmetic — even if the underlying principle is documented.

**Independent Test**: Re-critic TC-107 (`1×10⁻⁹ km → 1E-9 nm`) under the updated prompt → verdict NOT grounded, arithmetic error named in a finding. Re-critic TC-125 (`−459.67°F → 0 K`) → still grounded (no false positives). No build or test run needed — prompt-only change.

### Implementation for User Story 1

- [X] T001 [US1] Read `src/Spectra.CLI/Prompts/Content/critic-verification.md` in full to understand current structure and find the correct insertion point for the arithmetic mandate

- [X] T002 [US1] Add arithmetic mandate section to `src/Spectra.CLI/Prompts/Content/critic-verification.md` immediately after the existing "Technique Verification" section. The new section MUST:
  - Be titled "## Arithmetic Verification" (or similar clear heading)
  - State that when an expected result is a **computed value** (unit conversion, formula output, scientific-notation magnitude, derived constant), the critic MUST compute the value independently and compare it to the test's asserted value
  - Explicitly state: a wrong computed value → verdict is NOT `grounded` even if the underlying principle is documented
  - Cover the general case: unit conversions, formulas (F→C→K), conversion factors, derived constants
  - Be ADDITIVE to doc-presence rules: both conditions must hold — principle in docs AND number arithmetically correct
  - Include a worked example: e.g. `1×10⁻⁹ km → expected: 1E-9 nm` — the critic should note that `1×10⁻⁹ km = 1×10⁻⁹ × 10⁶ nm = 1×10⁻³ nm = 0.001 nm` or that `1 km = 10¹² nm` so `1×10⁻⁹ km = 10³ nm = 1000 nm`, and therefore `1E-9 nm` is arithmetically wrong → NOT grounded

**Checkpoint**: Phase 1 complete. ✓

---

## Phase 2: User Story 2 — Index-Writer Path Fix (Priority: P2)

**Goal**: Fix both `LoadExistingTestsAsync` writer sites so `_index.json` always stores bare filenames (`TC-100.md`) on second and subsequent generation rounds. Add regression tests to prevent recurrence.

**Independent Test**: After fix, run a second-round ingest on a test suite and inspect `_index.json` — all `file` fields must be bare with no directory separator. The three 073 consumer fixes (`AuditGroundingHandler:91`, `CompileRepairBatchCommand:95`, `IngestGroundingCommand:115`) remain UNCHANGED.

### Tests for User Story 2

- [X] T003 [P] [US2] Add test `SecondRoundIngest_IndexFileField_RemainsBare` in `tests/Spectra.CLI.Tests/Commands/Generate/IngestTestsSecondRoundTests.cs`

- [X] T004 [P] [US2] Add test `IngestUpdate_AfterUpdate_IndexFileFieldsRemainBare` in `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateSecondRoundTests.cs`

### Implementation for User Story 2

- [X] T005 [US2] Fix `src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs` line 153:
  - Changed: `var relativePath = Path.GetRelativePath(testsPath, file);`
  - To: `var relativePath = Path.GetRelativePath(suitePath, file);`

- [X] T006 [US2] Fix `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs` line 166:
  - Changed: `var relativePath = Path.GetRelativePath(testsPath, file);`
  - To: `var relativePath = Path.GetRelativePath(suitePath, file);`

- [X] T007 [US2] All 4 new tests pass; full test suite green (2070 tests, 0 failures).

**Checkpoint**: Phase 2 complete. ✓

---

## Phase 3: User Story 3 — Skill Failure-Branch Guard (Priority: P3)

**Goal**: Add a failure-branch check after the post-8a batch `ingest-grounding --all` call in `spectra-generate.md` so the agent stops and reports when `written: 0` while kept-grounded count is nonzero, instead of improvising manual edits.

**Independent Test**: A `written: 0` response from `ingest-grounding --all` (with nonzero kept-grounded count) causes the skill to emit STOP+diagnostic and halt before 8b. No `.md` file edits occur. No build needed — skill-only change.

### Implementation for User Story 3

- [X] T008 [US3] Read `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` post-8a batch block

- [X] T009 [US3] Edit `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — added failure-branch guard after post-8a `ingest-grounding --all` call. Guard checks `written` vs kept-grounded count; STOP+report on `written==0`; warning on partial write; NON-STOP CONTRACT restated.

**Checkpoint**: Phase 3 complete. ✓

---

## Phase 4: User Story 4 — Poisoned Index Regeneration (Priority: P4)

**Goal**: Regenerate the unit-converter `_index.json` after the writer fix (T005) so all `file` fields become bare.

- [X] T010 [US4] Build CLI: zero errors.

- [ ] T011 [US4] Re-index unit-converter suite: `spectra docs index --suites unit-converter` (in-workspace operation)

- [ ] T012 [US4] Verify regenerated index: all `file` fields are bare.

- [ ] T013 [US4] Run `spectra ai audit-grounding --suite unit-converter --output-format json` — confirm `grounding_written: true` for written blocks.

**Checkpoint**: Phase 4 complete when T011-T013 done in workspace.

---

## Phase 5: User Story 5 — Arithmetic Sweep (Priority: P5)

**Goal**: Re-critic the 56 grounded unit-converter tests under the new mandate. Identify and resolve TC-107-class arithmetic errors.

- [ ] T014 [US5] Run `spectra ai audit-grounding --suite unit-converter` — confirm grounded count and `grounding_written` state.

- [ ] T015 [P] [US5] For each grounded test with a computed expected value: re-critic under new mandate.

- [ ] T016 [US5] Resolve TC-107: confirm NOT-grounded, then repair or drop.

- [ ] T017 [US5] Resolve any other newly-caught arithmetic errors.

- [ ] T018 [US5] Produce sweep summary: test id, computed value, verdict, disposition.

**Checkpoint**: Phase 5 complete when T014-T018 done in workspace (in-session operations).

---

## Phase 6: Polish — Docs, Build, Verify (FR-010 + version)

- [X] T019 [P] Updated docs: `grounding-verification.md` (Arithmetic Verification section), `cli-reference.md` (bare-filename convention note).

- [X] T020 [P] Updated `usage.md` repair-batch section with written-vs-kept-grounded check and NON-STOP CONTRACT.

- [X] T021 Full test suite: 2070 tests, 0 failures across all projects.

- [ ] T022 Run `spectra validate` in project workspace (in-workspace operation).

- [X] T023 Bumped `Directory.Build.props` version to `2.3.0`. Updated `CLAUDE.md` Recent Changes.

---

## Dependencies & Execution Order

- **Phase 1 (US1)**: No dependencies — start immediately. Fully independent.
- **Phase 2 (US2)**: No dependencies on Phase 1 — can run in parallel with Phase 1.
- **Phase 3 (US3)**: No dependencies on Phase 1 or 2 — can run in parallel.
- **Phase 4 (US4)**: DEPENDS on Phase 2 completion (T005, T006 must be done; build must be run).
- **Phase 5 (US5)**: DEPENDS on Phase 1 completion (T002) and Phase 4 completion (T011-T013).
- **Phase 6 (Polish)**: DEPENDS on all phases complete.

---

## Notes

- [P] tasks = different files, no dependencies on each other
- US labels map tasks to user stories for traceability
- No new projects, dependencies, or abstractions introduced
- The writer fix is exactly two changed characters per file (`testsPath` → `suitePath`)
- US4 and US5 are operational steps (CLI commands run in-workspace), not code tasks
- T011-T018, T022 are in-session workspace operations to be completed when working in the live project workspace
