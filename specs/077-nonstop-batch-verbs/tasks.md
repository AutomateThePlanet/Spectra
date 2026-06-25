# Tasks: Non-Stop Seam Coverage — Verdict & Update Batch Verbs, Manifest Consumption, General Contract Preamble

**Input**: Design documents from `/specs/077-nonstop-batch-verbs/`  
**Branch**: `077-nonstop-batch-verbs`  
**Spec**: 077

**Organization**: Phase 1 (US1+US2 CLI verbs) in parallel → Phase 2 (US3+US4 skill) sequential after verbs are built → Phase 3 (Polish/docs). Verbs must exist before the skill is updated to point to them.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1=ingest-verdict --all, US2=ingest-update --all, US3=manifest consumption, US4=general preamble

---

## Phase 1: User Story 1 — `ingest-verdict --suite --all` (Priority: P1) 🎯 MVP

**Goal**: Add a `--suite --all` batch mode to `IngestVerdictCommand.cs` that enumerates all verdict files for a suite in one call, classifies each with `VerdictIngestor.Classify()`, and emits a summary JSON — eliminating the per-test shell loop.

**Independent Test**: `spectra ai ingest-verdict --suite unit-converter --all --output-format json` completes in a single call with `{"grounded": N, "partial": M, ...}`. Per-test `--from` mode still works unchanged.

### Regression Tests for User Story 1

- [ ] T001 [P] [US1] Read `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs` and `src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs` (batch section lines 72-163) to confirm the exact batch pattern before writing tests

- [ ] T002 [P] [US1] Create `tests/Spectra.CLI.Tests/Commands/Generate/IngestVerdictBatchTests.cs` with:
  - `BatchIngest_AllSuiteVerdicts_CountsMatch` — creates N verdict files for suite, calls `--all`, asserts `grounded + partial + hallucinated == N`
  - `BatchIngest_OtherSuiteVerdicts_AreSkipped` — verdict files for two suites present; filter returns only the target suite
  - `BatchIngest_EmptyVerdictDir_ReturnsZero` — `.spectra/verdicts/` absent or empty; exit 0, `{grounded:0, partial:0, ...}`
  - `BatchIngest_FromAndAll_AreMutuallyExclusive` — passing both `--from` and `--all` errors cleanly
  - `PerTestMode_StillWorks_AfterBatchAdded` — existing `--from` mode produces same result as before

### Implementation for User Story 1

- [ ] T003 [US1] Read `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs` fully to get current option registration and `RunAsync` structure

- [ ] T004 [US1] Add `--suite <s>` option (required when `--all`) and `--all` flag to `IngestVerdictCommand.cs`; add `RunBatchAsync` method mirroring `IngestGroundingCommand.RunBatchAsync`:
  - Enumerate `.spectra/verdicts/critic-verdict-*.json`
  - Load `_index.json`, filter to named suite via id lookup
  - Call `VerdictIngestor.Classify(content)` per file
  - Aggregate `{grounded, partial, hallucinated, errors}` counts
  - Emit JSON summary (mirroring `IngestGroundingCommand.EmitBatchResult` shape)
  - Return `ExitSuccess` even when some errors occur (they go in `errors` count)
  - Mutual exclusion: if both `--from` and `--all` passed, emit error and return `ExitError`
  - `--all` without `--suite` emits error and returns `ExitError`

- [ ] T005 [US1] Run `dotnet build` — zero errors before proceeding

- [ ] T006 [US1] Run `dotnet test tests/Spectra.CLI.Tests` — all new tests green; existing verdict tests unchanged

**Checkpoint**: Phase 1 complete — `ingest-verdict --suite --all` works, per-test `--from` backward-compatible, all tests green.

---

## Phase 2: User Story 2 — `ingest-update --suite --all` (Priority: P2)

**Goal**: Add a `--all` batch mode to `IngestUpdateCommand.cs`. Defines and uses new staging convention `.spectra/updates/{suite}/updated-{id}.json`. After all repairs, one call ingests all staged updates.

**Independent Test**: Agent writes repaired tests to `.spectra/updates/checkout/updated-TC-100.json` etc., then `spectra ai ingest-update checkout --all` ingests all in one call.

### Regression Tests for User Story 2

- [ ] T007 [P] [US2] Create `tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBatchTests.cs` with:
  - `BatchIngest_AllStagedUpdates_CountsMatch` — creates N staged update files in `.spectra/updates/{suite}/`, calls `--all`, asserts `written == N`
  - `BatchIngest_NoStagingDir_ReturnsZero` — staging dir absent; exit 0, `{written:0}`
  - `BatchIngest_MalformedUpdateFile_FailsLoudPerEntry_ContinuesRest` — one bad file; `errors: 1`, rest written
  - `BatchIngest_FromAndAll_AreMutuallyExclusive` — both `--from` and `--all` errors
  - `PerEntryMode_StillWorks_AfterBatchAdded` — existing `suite --test-id --from` mode unchanged

### Implementation for User Story 2

- [ ] T008 [US2] Read `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs` fully

- [ ] T009 [US2] Add `--all` flag to `IngestUpdateCommand.cs`; define staging convention `.spectra/updates/{suite}/updated-{id}.json`; add `RunBatchAsync` method:
  - Enumerate `.spectra/updates/{suite}/updated-*.json` files
  - Extract `id` from filename: `updated-{id}.json` → `{id}`
  - Load suite's existing tests (reuse `LoadExistingTestsAsync`)
  - For each staged file: locate original test by id, call `UpdatedTestIngestor.IngestAsync(content, testsPath, suite, original, existingTests, ct)`
  - Aggregate `{written, skipped_no_original, errors}` counts
  - Emit JSON summary
  - Mutual exclusion: `--from`/`--test-id` and `--all` are mutually exclusive; emit error if both
  - `--all` makes `--test-id` optional (not required)
  - Add `.spectra/updates/` to `.spectraignore` / gitignore alongside `.spectra/verdicts/`

- [ ] T010 [US2] Run `dotnet build` — zero errors

- [ ] T011 [US2] Run `dotnet test tests/Spectra.CLI.Tests` — all new tests green; existing update tests unchanged

**Checkpoint**: Phase 2 complete — `ingest-update {suite} --all` works; per-entry mode backward-compatible; test suite green.

---

## Phase 3: User Story 3 + 4 — Skill Changes (Priority: P2/P3)

**Goal**: Replace per-test skill loops with single batch calls, add manifest-consumption instruction, add general non-stop preamble. Depends on Phases 1+2 (verbs must exist before skill points to them).

**Independent Test**: Read `spectra-generate.md` — (a) per-test `ingest-verdict` loops replaced with `ingest-verdict --suite --all`, (b) per-entry `ingest-update` loop replaced with write-then-batch pattern, (c) manifest-consumption instruction present at Step 8b, (d) general preamble before Step 7. `ingest-grounding --all` guard unchanged.

### Implementation for User Story 3 + 4

- [ ] T012 [US3] [US4] Read `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` in full to confirm exact line numbers for:
  - Per-test `ingest-verdict --from` loop in Step 8a.1 (lines ~206-209)
  - Per-test `ingest-verdict --from` loop in Step 8b.4 (lines ~257-260)
  - Per-entry `ingest-update` invocation in Step 8b.3 (lines ~241-265 region)
  - The post-8a `ingest-grounding --all` guard (US3 from Spec 075) — must remain UNCHANGED
  - The current 8b manifest-consumption text (what it says now)

- [ ] T013 [US3] Edit `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md` — Step 8a changes:
  - Replace the per-test `ingest-verdict --from .spectra/verdicts/critic-verdict-{id}.json` loop with a single call:
    ```
    spectra ai ingest-verdict --suite {suite} --all --output-format json
    ```
  - Preserve the existing `ingest-grounding --all` call and US3 guard UNCHANGED

- [ ] T014 [US3] Edit `spectra-generate.md` — Step 8b changes:
  - Replace the per-entry `ingest-update {suite} --test-id {id} --from ...` loop with:
    - Per-entry: agent patches the test, writes to `.spectra/updates/{suite}/updated-{id}.json` (Write tool)
    - After all repairs: `spectra ai ingest-update {suite} --all --output-format json`
  - Replace the per-test `ingest-verdict --from` loop at Step 8b.4 with a single `ingest-verdict --suite {suite} --all` call
  - Add explicit manifest-consumption instruction at the compile-repair-batch step:
    > "If `compile-repair-batch` output exceeded inline capacity and was saved to a tool-results file, use the Read tool to read the file. Iterate over the JSON entries in-context — do NOT pipe the manifest to `python`, `jq`, or any interpreter. The full `prompt` field per entry is needed and must be read in-context, not extracted via shell. Do NOT accept a 'scripting for all projects' allowlist option."

- [ ] T015 [US4] Edit `spectra-generate.md` — add general non-stop contract preamble immediately before Step 7:
  > "**NON-STOP CONTRACT (all seams, Steps 7–9):** Every step is either a single `Bash(spectra *)` call or a `Write` to a spectra-authored path. If no single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it. Prohibited behaviors (each is a missing-affordance signal, not a license to improvise): (a) shell loops over per-test verbs (`for id in …; do spectra …; done`), (b) piping `spectra` output to any interpreter (`python -c`, `jq`, etc.), (c) manual `.md` file editing, (d) rewriting `grounding:`/`verdict:` frontmatter by hand. Never accept a 'scripting for all projects' allowlist option — that is the inverse of this contract. The per-seam guards below (e.g., the grounding `written:0` check) are specific instances of this general rule."

- [ ] T016 Verify regression gate: `IngestGroundingCommand.cs`, `AuditGroundingHandler.cs:91`, `CompileRepairBatchCommand.cs:95`, `IngestGroundingCommand.cs:115` — confirm ZERO diff on these files after all skill edits (skill edits don't touch C#)

**Checkpoint**: Phase 3 complete — skill has batch calls, manifest-consumption instruction, general preamble; grounding guard unchanged.

---

## Phase 4: Polish — Docs, Build, Version

- [ ] T017 [P] Update `docs/cli-reference.md` — add `ingest-verdict --suite <s> --all` and `ingest-update <suite> --all` entries, each mirroring the existing `ingest-grounding --all` entry in format and placement

- [ ] T018 [P] Update `docs/usage.md` — update the "Repair Batch & Resume" section to reflect the new write-then-batch pattern for `ingest-update` and the batch `ingest-verdict --all` call

- [ ] T019 Full test suite: `dotnet test` — Core 568 + CLI (≥1266+new) + Execution 228, 0 failures

- [ ] T020 Run `dotnet build` one final time — confirm zero errors, zero warnings on new code

- [ ] T021 [P] Bump `Directory.Build.props` `<Version>` to `2.4.0`; update `CLAUDE.md` Recent Changes section with Spec 077 entry

---

## Dependencies & Execution Order

- **Phase 1 (US1)** and **Phase 2 (US2)**: Can run in parallel — different files (`IngestVerdictCommand.cs` vs `IngestUpdateCommand.cs`)
- **Phase 3 (US3+US4)**: DEPENDS on Phases 1 + 2 complete (new verbs must be built and tested before skill is updated to reference them)
- **Phase 4 (Polish)**: DEPENDS on all implementation phases complete

### Parallel Opportunities

- T001 and T007 can run in parallel (reading different files)
- T002 and T007 can run in parallel (different test files)
- T017 and T018 can run in parallel (different doc files)
- T017, T018, T021 can all run in parallel

---

## Notes

- [P] tasks = different files, no dependencies on each other
- `ingest-verdict --all` classifies but does NOT write — it is advisory-gate only (same as per-test mode)
- `.spectra/updates/{suite}/` is a NEW staging convention — must be gitignored
- `--from` and `--all` are mutually exclusive in BOTH new commands
- The three 073 consumer fixes and the 075 US1/US2/US3 changes are in C# source files that are NOT touched by skill edits — regression gate is automatic
- No new NuGet packages, no new projects, no schema changes
- Test count: +5 in Spectra.CLI.Tests for US1, +5 for US2 = ~10 new tests
