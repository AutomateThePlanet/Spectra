# Tasks: Grounding Oracle Fix + Batch Grounding-Ingest (Spec 073 / 072 Amendment)

**Input**: `specs/073-fix-grounding-oracle-batch/`  
**Branch**: `073-fix-grounding-oracle-batch`  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

**Organization**: Tasks grouped by user story. US1 gate (oracle fix) before US2 (repair-batch verification) because the spec's investigation protocol requires confirming the oracle fix before diagnosing a second bug — even though both are CONFIRMED independent at code level (same pattern; see plan.md Root Cause section).

---

## Phase 1: Setup

**Purpose**: Confirm baseline before any changes.

- [ ] T001 Run `dotnet build` and confirm clean build with zero errors
- [ ] T002 Run `dotnet test` and capture baseline passing-test count for regression reference

---

## Phase 2: Foundational

**Purpose**: No new infrastructure required — all types, services, DI, and test harness exist. Investigation is complete (file+line confirmed in plan.md). Proceed directly to user story phases.

**⚠️ CRITICAL**: T001–T002 (clean baseline) must complete before any source edits.

---

## Phase 3: User Story 1 — Oracle Reports Correct Grounding State (Priority: P1) 🎯 MVP

**Goal**: Fix `AuditGroundingHandler.cs:91` (`testsPath` → `suitePath`) so `grounding_written` reflects actual `.md` frontmatter state; fix the test fixture that accidentally worked with the buggy code.

**Independent Test**: `dotnet test --filter "Category=AuditGrounding|AuditGroundingCommandTests"` — all existing tests pass with corrected `"file":"TC-NNN.md"` fixture format.

### Implementation for User Story 1

- [ ] T003 [US1] Fix `AuditGroundingHandler.cs:91` — change `Path.Combine(testsPath, testEntry.File)` to `Path.Combine(suitePath, testEntry.File)` in `src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs`
- [ ] T004 [US1] Fix `AuditGroundingCommandTests.WriteTestMd` _index.json fixture: change `"file":"{{suite}}/{{id}}.md"` to `"file":"{{id}}.md"` at line 54 in `tests/Spectra.CLI.Tests/Commands/AuditGroundingCommandTests.cs`
- [ ] T005 [US1] Fix `AuditGroundingCommandTests.MixedSuite_SummaryCounts` consolidated index (line 213): change all three `"file":"smoke/TC-310.md"`, `"file":"smoke/TC-311.md"`, `"file":"smoke/TC-312.md"` entries to `"file":"TC-310.md"`, `"file":"TC-311.md"`, `"file":"TC-312.md"` in `tests/Spectra.CLI.Tests/Commands/AuditGroundingCommandTests.cs`
- [ ] T006 [US1] Run `dotnet build` (must be clean) then `dotnet test --filter "AuditGroundingCommandTests"` — all 9 existing tests must pass

**Checkpoint**: Oracle reports correct `grounding_written` state. US1 independently verified.

---

## Phase 4: User Story 2 — Repair Batch Targets the Right Tests (Priority: P2)

**Goal**: Fix `CompileRepairBatchCommand.cs:95` (same `testsPath` → `suitePath` bug, CONFIRMED INDEPENDENT — not downstream of US1); fix the two test fixture locations that accidentally worked with the buggy code.

**Independent Test**: `dotnet test --filter "CompileRepairBatchCommandTests"` — all 7 existing tests pass with corrected `"file":"TC-NNN.md"` fixture format.

### Implementation for User Story 2

- [ ] T007 [US2] Fix `CompileRepairBatchCommand.cs:95` — change `Path.Combine(testsPath, testEntry.File)` to `Path.Combine(suitePath, testEntry.File)`; also fix misleading comment at line 93 (`entry.File is relative to testsPath (e.g., "smoke/TC-401.md")` → `entry.File is relative to suitePath (e.g., "TC-401.md") — matches GeneratedTestIngestor.ParseTestCase`) in `src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs`
- [ ] T008 [US2] Fix `CompileRepairBatchCommandTests.WriteTestMd` _index.json fixture: change `"file":"{{suite}}/{{id}}.md"` to `"file":"{{id}}.md"` at line 56 in `tests/Spectra.CLI.Tests/Commands/CompileRepairBatchCommandTests.cs`
- [ ] T009 [US2] Fix `CompileRepairBatchCommandTests.AppendTestToIndex` entry format: change `"file":"{{{suite}}}/{{{tid}}}.md"` to `"file":"{{{tid}}}.md"` at line 67 in `tests/Spectra.CLI.Tests/Commands/CompileRepairBatchCommandTests.cs`
- [ ] T010 [US2] Run `dotnet build` then `dotnet test --filter "CompileRepairBatchCommandTests"` — all 7 existing tests must pass
- [ ] T011 [US2] Run full `dotnet test` — ALL tests pass (Phase 1 gate)
- [ ] T012 [US2] REPACK: `dotnet pack src/Spectra.CLI -c Release -o /tmp/nupkg`; REINSTALL: `dotnet tool uninstall -g spectra; dotnet tool install -g spectra --add-source /tmp/nupkg`; VERIFY: `spectra --version` matches HEAD commit (Phase 1 git-hash gate)

**Checkpoint**: Both path bugs fixed. Oracle + repair-batch work on correctly-formatted `_index.json` entries. Phase 1 gate passed.

---

## Phase 5: User Story 3 — Batch Grounding-Ingest in One Call (Priority: P3)

**Goal**: Add `--all` flag to `IngestGroundingCommand.cs` (make `--test` optional); implement batch logic using the existing `GroundingWriteBackService.WriteAsync` (no code duplication); add tests for the new mode.

**Independent Test**: `dotnet test --filter "IngestGroundingCommandTests"` — all existing tests pass plus new `--all` batch tests.

**Batch filter design** (from plan.md Research section):
- Without `--repaired`: process `grounded` verdicts only; skip `partial` verdicts (pre-repair filter, prevents premature partial blocks)
- With `--repaired`: process all ungrounded (both `grounded` and `partial` verdicts)
- Always skip tests whose `.md` already has a grounding block (idempotent check)
- Always skip `hallucinated` — test file gone, `File.Exists` = false → skip naturally

### Implementation for User Story 3

- [ ] T013 [US3] Update `IngestGroundingCommand.cs` — remove `IsRequired = true` from `--test` option; add `--all` boolean flag; update handler signature to accept `allMode` parameter in `src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs`
- [ ] T014 [US3] Add batch handler logic in `IngestGroundingCommand.cs`: when `--all` is set — scan `.spectra/verdicts/critic-verdict-*.json`, build suite `_index.json` lookup, for each verdict entry resolve `testFilePath = Path.Combine(suitePath, testEntry.File)`, skip already-grounded (parse `.md` frontmatter, `tc.Grounding is not null`), apply `partial`-skip-without-`--repaired` filter, call `GroundingWriteBackService.WriteAsync`, accumulate counts; emit JSON result with `mode:"batch"`, `written`, `skipped_already_written`, `skipped_partial_pre_repair`, `errors`; validate in handler that exactly one of `--test` / `--all` is provided (return exit 1 if neither) in `src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs`
- [ ] T015 [US3] Add batch tests in `IngestGroundingCommandTests.cs` — 7 new test methods: (a) `NoVerdictDir_AllMode_WritesNothing_ExitsZero`, (b) `AllMode_GroundedVerdicts_WritesBatchBlocks`, (c) `AllMode_SkipsAlreadyWritten_Idempotent`, (d) `AllMode_SkipsPartialWithoutRepaired`, (e) `AllMode_WritesPartialWithRepaired`, (f) `AllMode_MixedSuite_CorrectCounts`, (g) `NeitherTestNorAll_ReturnsError` in `tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs`
- [ ] T016 [US3] Run `dotnet build` then `dotnet test --filter "IngestGroundingCommandTests"` — all existing + 7 new tests pass

**Checkpoint**: `ingest-grounding --suite <s> --all` works in one call. US3 independently verified.

---

## Phase 6: User Story 4 — Zero Shell Improvisation in Generate Cycle (Priority: P4)

**Goal**: Rewrite `spectra-generate.md` Steps 8–9 to use batch `ingest-grounding --all` calls (one after 8a loop, one after 8b loop) and add explicit prohibition block against `find`/`grep`/`cat`/`ls` improvisation.

**Independent Test**: Skill text review — Steps 8a, 8b, and 9 contain NO per-test `ingest-grounding` loop; prohibition block present; test path format stated explicitly.

**Step 8 restructure** (from plan.md Research table):
- 8a loop: per-test critic + `ingest-verdict` + `record-drop` (if hallucinated); no `ingest-grounding` calls inside the loop
- After 8a: ONE call `ingest-grounding --suite {suite} --all` (writes grounded blocks; skips partial)
- 8b loop: per-entry repair (compile-repair-prompt → patch → re-critic → ingest-update + ingest-verdict); no `ingest-grounding` inside loop
- After 8b: ONE call `ingest-grounding --suite {suite} --all --repaired --repair-attempts 1` (writes partial blocks; skips already-written)

### Implementation for User Story 4

- [ ] T017 [US4] Remove per-test `ingest-grounding --suite {suite} --test {id}` call from inside the Step 8a critic loop in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- [ ] T018 [US4] Add batch call `spectra ai ingest-grounding --suite {suite} --all` after the Step 8a critic loop (before Step 8b) in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- [ ] T019 [US4] Remove per-test `ingest-grounding --suite {suite} --test {id} [--repaired] [--repair-attempts N]` calls from inside the Step 8b repair loop; add batch call `spectra ai ingest-grounding --suite {suite} --all --repaired --repair-attempts 1` after the 8b loop in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- [ ] T020 [US4] Add prohibition block to Step 9 routing notes in `spectra-generate.md`: "Test files are at `test-cases/{suite}/{id}.md` — never use `find` to locate. Grounding state: `audit-grounding --suite {suite}` — never use `ls .spectra/verdicts/` or `grep`. Config: `spectra config --raw` — never use `cat spectra.config.json`." in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- [ ] T021 [US4] Run `dotnet build` then full `dotnet test` — ALL tests pass (Phase 2 gate)
- [ ] T022 [US4] REPACK + REINSTALL + verify git-hash (Phase 2 gate): `dotnet pack src/Spectra.CLI -c Release -o /tmp/nupkg`; reinstall + `spectra --version`

**Checkpoint**: Skill routes all grounding-ingest through CLI verbs; zero loops. Phase 2 gate passed.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Docs update, CLAUDE.md Recent Changes entry, and final gate.

- [ ] T023 Update `docs/cli-reference.md` — add `--all` form to the `ingest-grounding` entry: new synopsis `spectra ai ingest-grounding --suite <s> --all [--repaired] [--repair-attempts N]`, flags table (`--all`: batch mode, writes grounding for all eligible tests), exit code notes, and JSON result shape (`mode`, `written`, `skipped_already_written`, `skipped_partial_pre_repair`, `errors`) in `docs/cli-reference.md`
- [ ] T024 Update `docs/usage.md` — update "Grounding & Repair" or "Verdict Disposition" section to show the two-call batch pattern: (1) `ingest-grounding --suite --all` after critic loop for grounded tests; (2) `ingest-grounding --suite --all --repaired --repair-attempts 1` after repair loop for partial tests; note that `grounding_written` reflects `.md` frontmatter (source of truth, same path the writer uses) in `docs/usage.md`
- [ ] T025 Update `CLAUDE.md` — add `073-fix-grounding-oracle-batch` entry to `## Recent Changes`: oracle fix (AuditGroundingHandler:91 testsPath→suitePath), repair-batch fix (CompileRepairBatchCommand:95 same fix, independent bug), `ingest-grounding --suite --all` batch form, Step 8 skill routing + prohibition block in `CLAUDE.md`
- [ ] T026 Run full `dotnet test` — ALL tests pass (Phase 3 / final gate)
- [ ] T027 REPACK + REINSTALL + verify git-hash (final delivery gate)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: No blocking tasks for this amendment — all infrastructure exists
- **US1 (Phase 3)**: Start after T001–T002 baseline; complete before US2 (spec gate ordering)
- **US2 (Phase 4)**: Start after US1 gate passes (T006 green); T011–T012 are the Phase 1 REPACK gate
- **US3 (Phase 5)**: Can start after US1 gate; no code dependency on US2 fix (independent command)
- **US4 (Phase 6)**: Requires US3 complete (skill references `--all` flag which must exist first); T021–T022 are Phase 2 REPACK gate
- **Polish (Phase 7)**: After Phase 6 gate; T026–T027 are final delivery gate

### User Story Dependencies

- **US1 (P1)**: Independent — starts after baseline
- **US2 (P2)**: Ordered after US1 for spec-gate safety; code is independent (same bug, separate file)
- **US3 (P3)**: Independent of US1/US2 (different command, different file); can run in parallel with US2 if needed
- **US4 (P4)**: Depends on US3 (skill must reference `--all` which must exist)

### Parallel Opportunities

- T003 + T007: same fix pattern in different files — PARALLELIZABLE if skipping the sequential gate
- T004 + T008: test fixture format fix in different files — PARALLELIZABLE
- T005 + T009: test fixture secondary fix locations — PARALLELIZABLE
- T013 + T014: same file (`IngestGroundingCommand.cs`) — sequential
- T017–T020: all in same file (`spectra-generate.md`) — sequential
- T023 + T024: different docs files — PARALLELIZABLE [P]

---

## Parallel Example: If Running US1 + US2 Together (Same Bug Pattern)

```bash
# All four source edits are in different files — parallelizable:
Task A: Fix AuditGroundingHandler.cs:91
Task B: Fix CompileRepairBatchCommand.cs:95

Task C: Fix AuditGroundingCommandTests.cs WriteTestMd:54 + MixedSuite lines 211-213
Task D: Fix CompileRepairBatchCommandTests.cs WriteTestMd:56 + AppendTestToIndex:67

# Then sequential:
dotnet build → dotnet test → REPACK + REINSTALL
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: baseline
2. Complete Phase 3: US1 oracle fix (T003–T006)
3. **STOP and VALIDATE**: oracle reports correct state
4. Gate: `audit-grounding --suite unit-converter` → 45 written

### Full Phase 1 Gate

1. Complete US1 (T003–T006)
2. Complete US2 (T007–T012)
3. Gate: `audit-grounding` ✓ + `compile-repair-batch` → 28-entry manifest ✓ + REPACK ✓

### Full Phase 2 Gate (Adds Batch + Routing)

1. Complete US3 (T013–T016)
2. Complete US4 (T017–T022)
3. Gate: `ingest-grounding --suite --all` works ✓ + skill has no per-test loop ✓ + REPACK ✓

---

## Notes

- Root causes for all bugs are CONFIRMED at file+line (see plan.md) — no investigation tasks needed
- The spec says "fix oracle BEFORE repair-batch filter"; code inspection confirmed they're independent, but the sequential phase ordering is preserved for gate safety
- `IngestGroundingCommand.cs` batch logic reuses `GroundingWriteBackService.WriteAsync` — zero duplicated write code (FR-A8)
- All test fixture changes are format-alignment only (`"suite/id.md"` → `"id.md"`); no test logic changes
- The skill file `spectra-generate.md` is bundled via `spectra update-skills` — the NuGet REPACK gate at T012/T022/T027 is required for the skill to take effect on installed instances
