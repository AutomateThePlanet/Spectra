# Tasks: Repair-Orchestration Hardening & Inspection Surface

**Input**: Design documents from `specs/072-repair-batch-inspect/`
**Branch**: `072-repair-batch-inspect`

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)

---

## Phase 1: Setup

**Purpose**: Scratch-file directory isolation before any implementation.

- [ ] T001 Add `.spectra/repairs/` to framework repo `.gitignore` (after line 38, alongside `.spectra/verdicts/`)

---

## Phase 2: Foundational (Blocking Prerequisite)

**Purpose**: Shared result type needed before `audit-grounding` command can be wired up.

⚠️ **CRITICAL**: T002 must complete before Phase 3 US1 work on `AuditGroundingCommand`.

- [ ] T002 Create `src/Spectra.CLI/Results/AuditGroundingResult.cs` with three types: `AuditGroundingResult : CommandResult` (fields: `Suite`, `Tests`, `Summary`), `AuditGroundingEntry` (fields: `Id`, `Verdict`, `Score`, `GroundingWritten`, `FlaggedForReview`, `ActionNeeded`, `File?` — all with `[JsonPropertyName]` attributes matching data-model.md), and `AuditGroundingSummary` (fields: `Total`, `GroundingWritten`, `PartialPendingRepair`, `FlaggedForReview`)

**Checkpoint**: Result type available — US1 implementation can begin.

---

## Phase 3: User Story 1 — Grounding State Is Inspectable (Priority: P1) 🎯 MVP

**Goal**: `audit-grounding` reports per-test grounding state; `show {id}` returns `file` field; agent no longer needs shell improvisation for state reads.

**Independent Test**: Run `spectra ai audit-grounding --suite unit-converter --output-format json` against the Spec 071 consumer run — verify 15 tests report `action_needed: repair` and 20 report `action_needed: none`. Run `spectra show TC-100 --output-format json` — verify `file` field present.

### Implementation for User Story 1

- [ ] T003 [P] [US1] Add `public string? File { get; init; }` with `[JsonPropertyName("file")]` and `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` to `TestDetail` in `src/Spectra.CLI/Results/ShowResult.cs` (after `Suite` property, before `Component`)
- [ ] T004 [US1] Update `src/Spectra.CLI/Commands/Show/ShowHandler.cs`: change `DisplayTestAsync` signature to add `string? filePath` parameter; pass `Path.GetRelativePath(basePath, testPath)` from `ExecuteAsync` call-site at line ~82; add `File = filePath` to the `TestDetail` initializer at line ~153
- [ ] T005 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/ShowHandlerFileFieldTests.cs`: 4 tests verifying (1) JSON output includes `file` field, (2) `file` is working-dir-relative (no drive letter prefix), (3) `file` ends with `.md`, (4) human-readable output is unchanged (no "file:" line). Use the existing `WorkingDirectory` collection and temp-dir pattern from `CompileRepairPromptCommandTests.cs`.
- [ ] T006 [P] [US1] Create `src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs`: class `AuditGroundingHandler` with method `RunAsync(string suite, bool json, CancellationToken ct) → Task<int>`; (a) reads `_currentDir`/`.spectra/verdicts/critic-verdict-TC-NNN.json` files; (b) looks up each test via suite index (`IndexWriter.ReadAsync`); (c) parses the `.md` with `TestCaseParser` to check `tc.Grounding is not null` for `grounding_written` and `tc.Grounding?.FlaggedForReview`; (d) computes `action_needed` per contracts/audit-grounding.md rules; (e) emits `AuditGroundingResult` JSON or human-readable table. Mirror `ReviewFlaggedHandler` structure (`src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs`) for pattern.
- [ ] T007 [US1] Create `src/Spectra.CLI/Commands/Review/AuditGroundingCommand.cs`: `Command("audit-grounding", ...)` with `--suite`/`-s` (required) and `--output-format` options; delegates to `AuditGroundingHandler.RunAsync`. Follow `ReviewFlaggedCommand.cs` pattern.
- [ ] T008 [US1] Register `new AuditGroundingCommand()` in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` in the Spec 071 block (after `ReviewFlaggedCommand`, before `InitSeamProgressCommand`)
- [ ] T009 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/AuditGroundingCommandTests.cs`: 8 tests per contracts/audit-grounding.md test contract — `UngroundedPartial_ReportsActionRepair`, `GroundedTest_ReportsActionNone`, `FlaggedTest_ReportsActionReview`, `SummaryCountsMatchEntries`, `JsonOutput_MatchesSchema`, `SuiteNotFound_Exits1`, `NoVerdictFiles_EmptyTests`, `HumanOutput_ContainsHeaders`. Use temp-dir pattern; write verdict JSON + `.md` files with/without `grounding:` frontmatter blocks.
- [ ] T010 [US1] Add `config --raw` awareness note to `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`: in the "State inspection" or "Useful commands" section (or near the Step 8 notes), add one bullet: "`spectra config --raw` — prints the resolved spectra.config.json (use instead of reading the file directly)".

**Checkpoint**: Phase 1 gate — pack, reinstall, run gate tests.

- [ ] T011 Build + pack + install: `dotnet build`, `dotnet pack src/Spectra.CLI`, `dotnet tool update -g spectra --add-source ./artifacts/` (or equivalent), then verify `spectra --version` git-hash matches HEAD. Run `dotnet test` and confirm all tests pass including T005/T009.

---

## Phase 4: User Story 2 — Repair Batch Compiles Deterministically (Priority: P2)

**Goal**: `compile-repair-batch` produces a JSON manifest of ungrounded partials in one deterministic call; Step 8 rewritten as numbered manifest-driven loop with resume note.

**Independent Test**: Run `spectra ai compile-repair-batch --suite unit-converter` against the consumer repo — verify manifest contains exactly 15 entries (all ungrounded partials). Re-run after manually adding a grounding block to one test's `.md` — verify manifest now has 14 entries (resume filter works).

### Implementation for User Story 2

- [ ] T012 [US2] Create `src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs`: `Command("compile-repair-batch", ...)` with `--suite`/`-s` (required). Logic: (a) resolve `testsDir` via `spectra.config.json` (copy `ResolveTestsDirAsync` pattern from `CompileRepairPromptCommand.cs:170-183`); (b) read suite index via `IndexWriter.ReadAsync`; (c) enumerate `critic-verdict-TC-NNN.json` files in `.spectra/verdicts/`; (d) for each, call `VerdictIngestor.Classify()` — skip non-partial verdicts; (e) look up test in index, read `.md` via `TestCaseParser` — if `tc.Grounding is not null`, skip (already grounded, resume checkpoint); (f) call `RepairPromptCompiler.Compile(test, nonGroundedFindings, sourceDocs)` (REUSE — same as `CompileRepairPromptCommand.cs:134`); (g) build manifest entry `{id, suite, file, source_refs, repair_prompt}`; (h) emit `JsonSerializer.Serialize(manifest)` to stdout; exit 0. Exit 1 on suite-not-found. `LoadDocumentsFromRefsAsync` can be private-copied from `CompileRepairPromptCommand.cs:146-168` (or extracted to a shared helper — single additional caller is insufficient for a shared abstraction per YAGNI).
- [ ] T013 [US2] Register `new CompileRepairBatchCommand()` in `src/Spectra.CLI/Commands/Ai/AiCommand.cs` in a new Spec 072 block comment (after the Spec 071 block, before `InitSeamProgressCommand`)
- [ ] T014 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/CompileRepairBatchCommandTests.cs`: 8 tests per contracts/compile-repair-batch.md test contract — `EmptyManifest_WhenAllAlreadyGrounded`, `ManifestContainsOnlyUngroundedPartials`, `ManifestExcludesGroundedVerdicts`, `ManifestExcludesHallucinatedVerdicts`, `SuiteNotFound_Exits1`, `NoVerdictFiles_EmptyManifest`, `ManifestEntryHasFileField`, `RepairPromptIsNonEmpty`. Use temp-dir pattern; write verdict JSONs and test `.md` files with/without grounding frontmatter.
- [ ] T015 [US2] Rewrite Step 8 in `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`: replace the current 6×N sequential per-test prose loop with the numbered manifest-driven structure from `quickstart.md` Step 8 section. Numbered steps: 8.1 `compile-repair-batch` → manifest; 8.2 for-each loop with sub-steps (a) read prompt, (b) write `repaired-{id}.json`, (c) spawn critic subagent, (d) `ingest-update --from`, (e) `ingest-grounding`. Include the flag-and-continue rule and the resume note. Adopt `repaired-{id}.json` naming (`.spectra/repairs/` dir). Keep the Spec 071 four-bucket Step 9 summary unchanged.

**Checkpoint**: Phase 2 gate — pack, reinstall, run gate tests.

- [ ] T016 Build + pack + install (same as T011 pattern) — verify `spectra --version` hash matches HEAD. Run `dotnet test` — confirm all pass including T014.

---

## Phase 5: User Story 3 — Full Cycle Completes Unattended (Priority: P3)

**Goal**: Docs updated; all new verbs and the `show` file field documented in cli-reference and usage.

**Independent Test**: Verify no raw-shell commands appear in the updated skill Step 8 — every agent action references `spectra ai *` or a Write. Check `docs/cli-reference.md` contains entries for `compile-repair-batch`, `audit-grounding`, and the updated `show` description.

### Implementation for User Story 3

- [ ] T017 [US3] Update `docs/cli-reference.md`: (a) add `spectra ai compile-repair-batch` entry (after `compile-repair-prompt`; flags: `--suite`; exit codes: 0/1; output: JSON array manifest); (b) add `spectra ai audit-grounding` entry (after `ingest-grounding`; flags: `--suite`, `--output-format`; exit codes: 0/1); (c) add `file` field to `spectra show` description; (d) add `config --raw` to the `spectra config` entry if not already documented.
- [ ] T018 [P] [US3] Update `docs/usage.md` repair loop section: replace the existing partial-repair description with a paragraph describing the manifest-driven resumable loop (reference `compile-repair-batch` + `audit-grounding` + resume-via-checkpoint). Keep the Spec 071 verdict-disposition section header and the three-verdict table unchanged.

---

## Phase N: Polish & Cross-Cutting Concerns

- [ ] T019 Run `dotnet test` from repo root — all tests must pass (Core.Tests, CLI.Tests, Execution.Tests)
- [ ] T020 Run `spectra validate` in the consumer repo (`C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST`) — exit 0 required
- [ ] T021 Verify `spectra ai audit-grounding --suite unit-converter --output-format json` against consumer repo reports 15 `action_needed: repair` entries and 20 `action_needed: none` entries (confirming Phase 1 gate is live against real data)
- [ ] T022 Final pack + install + verify hash, then commit Spec 072 — message: `feat(critic): repair-orchestration hardening + inspection surface (Spec 072)`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: No dependencies — can start immediately
- **Foundational (T002)**: Depends on T001 — blocks T007/T008 (AuditGroundingCommand uses AuditGroundingResult)
- **US1 (T003–T011)**: Depends on T002; T003/T005/T006/T009/T010 are parallelizable; T004 depends on T003; T007 depends on T006; T008 depends on T007
- **US2 (T012–T016)**: Depends on US1 complete (AiCommand.cs serialized); T014 parallelizable with T012; T015 depends on T012 complete (writes same file as T010 — done in US1, sequential OK)
- **US3 (T017–T018)**: Depends on US2 complete; T018 parallelizable with T017
- **Polish (T019–T022)**: Depends on US3 complete

### Sequential constraint — AiCommand.cs

T008 (register audit-grounding) and T013 (register compile-repair-batch) both edit `AiCommand.cs`. T008 is in Phase 3, T013 is in Phase 4 — sequential by phase, no conflict.

### Sequential constraint — spectra-generate.md

T010 (add config --raw note) is in Phase 3. T015 (Step 8 rewrite) is in Phase 4. Both edit the same file. Sequential by phase — complete T010 before starting T015.

---

## Parallel Opportunities

### US1 (after T002 completes)

```
T003 (ShowResult.cs file field)    ← parallel
T005 (ShowHandlerFileFieldTests)   ← parallel
T006 (AuditGroundingHandler)       ← parallel
T009 (AuditGroundingCommandTests)  ← parallel
T010 (config --raw note in skill)  ← parallel
↓ then sequential:
T004 (ShowHandler update — depends on T003)
T007 (AuditGroundingCommand — depends on T006)
T008 (AiCommand registration — depends on T007)
T011 (pack/install/verify)
```

### US2 (after T011 completes)

```
T012 (CompileRepairBatchCommand)   ← first
T014 (CompileRepairBatchTests)     ← parallel with T012
↓ then sequential:
T013 (AiCommand registration — depends on T012)
T015 (Step 8 rewrite — depends on T010 complete from US1)
T016 (pack/install/verify)
```

### US3 (after T016 completes)

```
T017 (cli-reference.md)   ← first (two sections, one file)
T018 (usage.md)           ← parallel with T017
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. T001 (setup) → T002 (foundational) → T003–T011 (US1)
2. Validate: `spectra show TC-100 --output-format json` has `file` field; `audit-grounding --suite unit-converter` reports 15 pending
3. Stop and validate — the inspection surface alone unblocks state visibility

### Full Delivery (All Stories)

1. US1 → T011 gate → US2 → T016 gate → US3 → Polish
2. Each phase gate (T011, T016) verifies via pack+reinstall+test before proceeding

---

## Notes

- All new C# files follow the existing namespace pattern: commands in `Spectra.CLI.Commands.*`, results in `Spectra.CLI.Results`
- `RepairPromptCompiler.Compile()` is called verbatim in T012 — zero duplicated prompt logic
- `TestCaseParser` grounding block detection: `tc.Grounding is not null` (CONFIRMED pattern from `ReviewFlaggedHandler.cs:72`)
- Tests use temp-dir isolation (`Path.GetTempPath() + Guid`) and `Directory.SetCurrentDirectory` per the existing `CompileRepairPromptCommandTests.cs` pattern
- Phase gate tasks (T011, T016) are blocking — do not proceed to next phase until they pass
- No new NuGet packages; no new projects; no structural changes to existing commands
