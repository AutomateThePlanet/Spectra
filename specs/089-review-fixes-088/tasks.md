---
description: "Task list for Spec 089 — Review fixes for 088 (update prompt context)"
---

# Tasks: Review Fixes for Spec 088 — Update Prompt Context

**Input**: Design documents from `/specs/089-review-fixes-088/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required. Every fix needs a regression test that fails on `6ea7520` (spec T1–T7). Write each story's tests
before its code.

The regression proof (T032) runs the new test files against `6ea7520` in a scratch worktree. So the rows that prove a
fix are **command-level**: they run the CLI and assert on files, JSON and exit codes, and compile without any 089 API.
Builder- or helper-level rows that need new types are extra coverage, not the proof.

CLI tests use `BaselineTestWorkspace` / `UpdatePromptWorkspace` in the `CriteriaCommands` collection. RELINK rows use
`[GitFact]`.

089 changes no on-disk format, so any workspace baselined with `source-baseline --init` counts as a pre-089 workspace
for FR-025, but each fix still needs its own row.

**Organization**: by user story, in the spec's priority order.
- US1 = F1 (folder per flow);
- US2 = F2 (`MULTIPLE_TESTS`);
- US3 = F3 (`unchanged_pending`);
- US4 = F4 (criterion "not baselined");
- US5 = F5 (exit 8);
- US6 = F6 (gitignore).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US6 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `089-review-fixes-088` (= `6ea7520`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 1870 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.
- [X] T002 [P] Extend tests/Spectra.CLI.Tests/TestFixtures/UpdatePromptWorkspace.cs with:
  - `StageRepair(suite, id, TestCase)`, which writes `.spectra/repairs/{suite}/repaired-{id}.json` (a one-element array, as `StageEdit` does);
  - `StageRepairRaw(suite, id, content)`;
  - `RepairFiles(suite)`;
  - `IngestAllAsync(params string[] extra) → (Exit, Out, Err)`, which runs `ai ingest-update <suite> --all … --output-format json` without throwing on a non-zero exit;
  - `InitWithRealTemplateAsync()`, which runs `InitHandler` with the embedded template and `skipSkills: true`, as in `UpdatePromptEndToEndTests`.

  Use only APIs that exist on `6ea7520`, so the fixture compiles in the T032 worktree.

---

## Phase 2: Foundational

- [X] T003 Inventory the existing assertions 089 changes deliberately (research R8). Grep tests/Spectra.CLI.Tests for:
  - `ingest-update` with `--all`;
  - `RunJsonAsync(… "ingest-update"`;
  - `.spectra/updates/`;
  - identical staged edits (`StageIdentical`, `StageEdit(… Load(…))`);
  - `LinkedCriterionState.Unchanged`.

  Confirm or extend the R8 table in the Notes, with each test's name, what changes and which fix causes it. No source edits.

**Checkpoint**: baseline recorded; affected tests known.

---

## Phase 3: User Story 1 — Update edits and critic repairs never ingest each other's leftovers (Priority: P1) 🎯 MVP

**Goal (FR-001–FR-005)**: `--all` reads only `.spectra/updates/<suite>/`, and `--all --keep-baseline` reads only `.spectra/repairs/<suite>/repaired-<id>.json`.

**Independent test**: leftovers in both folders, then both batches back to back; each ingests only its own files in its own mode.

- [X] T004 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/StagingFolderSplitTests.cs (T1). The rows:
  - **(a)** TC-101 has a pending LOAD change (baselined, then 30GB → 45GB). A kept repair file `repaired-TC-101.json` with a real edit is in `.spectra/repairs/storage/`. `--all` → `results` has no TC-101 entry, TC-101's `.md` and baseline entry are byte-identical, and the repair file is still there. On 6ea7520 the update batch doesn't read the repairs folder either; this row guards the direction F1 adds.
  - **(b)** A kept, malformed update file `updated-TC-102.json` is in `.spectra/updates/storage/`. `--all --keep-baseline` → no TC-102 entry, and the file is untouched and still there.
  - **(c)** A valid `repaired-TC-101.json` → `--all --keep-baseline` gives `written`, `baseline.mode == "keep"`, `results[0].file == ".spectra/repairs/storage/repaired-TC-101.json"`, and the file is deleted. TC-101 is still LOAD in the next `update-triage --dry-run`.
  - **(d)** Back to back with leftovers in both folders: `--all` then `--all --keep-baseline` (and the reverse order). Each ingests only its own folder in its own mode.
  - **(e)** Row (d) on a workspace made by `InitWithRealTemplateAsync`.
  - **(f) Pre-089 workspace (FR-025):** a workspace baselined with `source-baseline --init` on the pre-089 layout, with
    a leftover repair file in `.spectra/updates/<suite>/` (as a pre-089 repair loop left it).
    - `--all --keep-baseline` never reads it: no entry in `results`, and the file is untouched.
    - `--all` reads it in Rebuild mode: it is `written` and its baseline rebuilt. This is the documented edge case,
      asserted so it stays visible.
  - **Regression proof:** on 6ea7520, (c) fails (`--all --keep-baseline` reads `.spectra/updates/` and finds nothing) and (b) fails (the update file is read and reported).
- [X] T005 [US1] In src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs, `RunBatchAsync` chooses its folder and prefix from the mode (research R1):
  - `Rebuild` → `.spectra/updates/<suite>/` and `updated-`;
  - `KeepBaseline` → `.spectra/repairs/<suite>/` and `repaired-`.

  The id is taken after the mode's prefix; files without it are ignored. A missing folder → empty result, exit 0. Keep every per-file rule as in 088.
- [X] T006 [US1] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:
  - Step 8b's repair phase writes `.spectra/repairs/{suite}/repaired-{id}.json`;
  - the batch-ingest paragraph names `.spectra/repairs/{suite}/`;
  - the CLI flags-table row for `ingest-update {suite} --all --keep-baseline` names the repairs folder.

  In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs, assert that the generation skill names `.spectra/repairs/{suite}/repaired-{id}.json` and never `.spectra/updates/`, and that `RepairIngests_KeepTheBaseline` still holds.
- [X] T007 [US1] Adapt the F1 assertions from T003, staging repairs in `.spectra/repairs/` through `StageRepair`:
  - `IngestUpdateStagingTests.KeepBaseline_UnchangedEdit_LeavesTheBaselineAsItWas_AndDeletesTheStagingFile`;
  - `BaselineReviewFixTests.RepairBatch_WithKeepBaseline_LeavesThePendingChange`, through its `Stage` helper or a new `StageRepair` helper.

  List each change in the Notes.

**Checkpoint**: US1 green; repairs and update edits are separated.

---

## Phase 4: User Story 2 — A staged file with several tests fails loud (Priority: P1)

**Goal (FR-006–FR-008)**: more than one test object → `MULTIPLE_TESTS`, nothing written. The single form exits 5; in a batch it is a `failed` file that is kept.

**Independent test**: a 10-element group array next to a valid file → the array fails and is kept, and the valid file is ingested.

- [X] T008 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Generate/MultipleTestsTests.cs (T2). The rows:
  - **Single form:** `--test-id TC-101 --from <2 objects>` → exit 5, stderr JSON `error_code == "MULTIPLE_TESTS"`, the error names `2` and both ids, and TC-101's file is byte-identical.
  - **Batch:** `updated-TC-101.json` holds a 10-element group array (TC-101…TC-110). `updated-TC-111.json` is a valid edit. → TC-101 `failed` with `MULTIPLE_TESTS` naming `10` and the ids; none of TC-101…TC-110 changed; the file is kept. TC-111 is `written`. Assert the outcomes, not the exit code (US5 covers that).
  - One valid object plus one object without `title` → `MULTIPLE_TESTS` (it counts objects, not valid tests).
  - Two objects with the same id → `MULTIPLE_TESTS`.
  - An object without `id` is listed as `(no id)`.
  - An empty array → `NO_TESTS` (unchanged).
  - A 10-element array whose first element's id differs from the file name → `MULTIPLE_TESTS`, not `ID_MISMATCH`.
  - **Regression proof:** on 6ea7520 the first test is persisted and the file deleted.
- [X] T009 [US2] Add the following:
  - `public static IReadOnlyList<string?>? ArrayObjects(string? content)` in src/Spectra.CLI/Generation/GeneratedTestIngestor.cs, using the same `ExtractJson` / `TryParseArray` as `ParseAndValidate`. It returns each object element's `id` (or null), or null when the content isn't an array.
  - In src/Spectra.CLI/Generation/UpdatedTestIngestor.cs: `public const string MultipleTests = "MULTIPLE_TESTS"`, a `MultipleTestsMessage(ids)` helper, and the check at the top of `IngestAsync` (more than one object → `IngestResult.Failure(MultipleTests, …)` before `ParseAndValidate`).
  - Unit rows in tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs.
- [X] T010 [US2] In `IngestUpdateCommand.RunBatchAsync`, replace the `StagedId` pre-check with `ArrayObjects`:
  - more than one object → `failed` with `MULTIPLE_TESTS` (the same message);
  - exactly one object whose id differs from the file's → `ID_MISMATCH`;
  - otherwise ingest.

  Remove `StagedId` if it is no longer used. The single form maps `MULTIPLE_TESTS` to exit 5 through the existing mapping.

**Checkpoint**: US2 green.

---

## Phase 5: User Story 3 — Returning a test unchanged doesn't hide a substantive change (Priority: P1)

**Goal (FR-009–FR-015)**: an unchanged edit, without `--keep-baseline`, for a test whose triage bucket needs an edit → `unchanged_pending`, with nothing recorded.

**Independent test**: identical edits for a LOAD test and a LIGHT test → the LOAD test stays LOAD and the LIGHT test leaves triage.

- [X] T011 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Generate/UnchangedPendingTests.cs (T3). Each case is baselined, then the doc or criteria change, then an identical edit is staged with `StageEdit(…, Load(…))` and `--all` runs.
  - **Theory, one row per class needing an edit:**
    - **LOAD:** 30GB → 45GB.
    - **CRITERIA_CHANGED:** the linked criterion reworded.
    - **REMOVED:** the anchor's heading renamed.
    - **CRITERIA_REMOVED:** the linked criterion deleted.
    - **RELINK:** the doc file renamed and committed, as a `[GitFact]` row.
  - **Expected for each:**
    - outcome `unchanged_pending`, with `bucket` the wire name and `why` non-empty;
    - `unchanged_pending: 1` in the batch JSON;
    - the `.md` byte-identical and the baseline file byte-identical;
    - the staging file deleted;
    - the same bucket in the next `update-triage --dry-run`.
  - **Rows that count as reviewed:**
    - **LIGHT:** a few words reworded, under the threshold.
    - **COSMETIC:** bold markup added; uses `update-triage --no-auto-ack --dry-run` to see it before the ingest.
    - **UNCHANGED.**
    - For each: outcome `unchanged`, the baseline entry now holds the current fingerprints, and the test is gone from the next triage.
  - **Other rows:**
    - No baseline entry (a test written after `source-baseline --init`, with no entry) → `unchanged`, and an entry is recorded.
    - `--keep-baseline` with an identical repair in `.spectra/repairs/` for a LOAD test → `unchanged`, nothing recorded, still LOAD.
    - Single form for the LOAD row → exit 0, JSON `outcome == "unchanged_pending"`, `unchanged == true`, `persisted == 0`, `bucket == "LOAD"`, `why` present. The human output (non-JSON run) contains `Test 'TC-101' returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again).`
    - The batch human summary on stderr in JSON mode contains `TC-101: returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)`.
    - The LOAD and LIGHT rows repeated on a workspace from `InitWithRealTemplateAsync`.
    - **Pre-089 workspace (FR-025):** a workspace baselined with `source-baseline --init`, with one LOAD test and one
      LIGHT test returned unchanged in one batch → the LOAD test is `unchanged_pending` and the baseline file's entry
      for it is byte-identical; the LIGHT test is `unchanged` and recorded.
    - **Partly baselined entry (U1):** an entry with ref A baselined and ref B added to the test later (no fingerprint
      for B), returned unchanged with a LIGHT change on A → `unchanged`; afterwards B has its fingerprint and A's is
      refreshed.
  - **Regression proof:** on 6ea7520 the LOAD row records the baseline and the test leaves triage.
- [X] T012 [US3] Create src/Spectra.CLI/Baseline/PendingChangeCheck.cs (research R3):
  - `public sealed record PendingChange(Bucket Bucket, IReadOnlyList<string> Why)`;
  - `public static IReadOnlySet<Bucket> NeedsEdit` = Load, CriteriaChanged, Removed, CriteriaRemoved, Relink;
  - `public static async Task<PendingChange?> ClassifyAsync(string suite, TestCase original, SourceBaseline baseline, BaselineContext context, int threshold, CancellationToken ct)`. It returns null when `baseline.Tests` has no entry for the test's id. Otherwise it calls `TriageEngine.TriageTestAsync(…, cosmeticAcked: false, ct)` and returns `PendingChange(verdict.Bucket, verdict.Why)`.
- [X] T013 [US3] Single form in src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs. For an `Unchanged` result in `Rebuild` mode, create a `BaselineContext` (`BaselineContext.CreateAsync(root, config, ct)`) inside the lock and classify with threshold `config.Update?.EffectiveLightThresholdWords ?? 12`.
  - **When the bucket needs an edit:** record nothing. Output JSON `outcome: "unchanged_pending"`, `unchanged: true`, `persisted: 0`, `bucket`, `why`, `baseline` (empty summary, mode `rebuild`) and `warnings`. Human output: the FR-013 sentence.
  - **Otherwise:** behave as in 088.
  - Every success JSON gains `outcome` (`written` / `unchanged` / `unchanged_pending`).
- [X] T014 [US3] Batch form in the same file:
  - `StagedOutcome` gains `UnchangedPending = "unchanged_pending"` and `Bucket`/`Why`, emitted in `results` only for that outcome.
  - One `BaselineContext` per run, created lazily inside the lock on the first unchanged outcome in `Rebuild` mode.
  - The set to record is the written tests plus the `unchanged` (not pending) originals in `Rebuild`.
  - `unchanged_pending` staging files are deleted with the consumed ones.
  - The JSON gains the `unchanged_pending` count.
  - The human line is `  <id>: returned unchanged — <BUCKET> change still pending (acknowledge with update-ack or edit again)`, and the summary header gains `unchanged_pending=N`.
- [X] T015 [US3] Adapt the F3 assertions from T003, and list each change in the Notes:
  - `IngestUpdateStagingTests.EditedUnchangedMalformed_…`: TC-102 is now `unchanged_pending`, its baseline is untouched, and it is still in triage.
  - `SingleForm_IdenticalEdit_…`: the rebuilt case moves to a LIGHT test, and the LOAD case asserts `unchanged_pending`.
  - `TheHumanSummary_ListsEachTest_InBothModes`: the TC-102 line.
- [X] T016 [US3] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md Step 5, after `ingest-update --all`, report `unchanged_pending` tests with `bucket` and `why`.
  - **LOAD and CRITERIA_CHANGED:** ask the user per group:
    - **acknowledge** → `spectra ai update-ack --suite <suite> --ids <ids> --output-format json --verbosity quiet`;
    - **edit again** → `spectra ai compile-update-prompt --suite <suite> --test-id <id>` → edit in-session → Write `.spectra/updates/<suite>/updated-<id>.json` → `spectra ai ingest-update <suite> --all --output-format json --verbosity quiet`. This counts toward the same at most two retries per test as failures.

    Never acknowledge without the user's answer.
  - **REMOVED, CRITERIA_REMOVED and RELINK:** edit groups never contain these (Step 4: those groups never enter the edit loop), so seeing one at ingest means the documentation changed after triage. Report "the documentation changed again since triage", offer no acknowledgement (`update-ack` refuses with `REF_REMOVED` for REMOVED and RELINK, `CRITERION_REMOVED` for CRITERIA_REMOVED), and tell the user to re-run `spectra ai update-triage`. The test is then handled by Step 2a (RELINK) or Step 3 (REMOVED / CRITERIA_REMOVED).

  Step 7's report gains "returned unchanged, still pending". Assert in SkillsManifestTests:
  - `unchanged_pending` with `update-ack` offered for LOAD and CRITERIA_CHANGED only;
  - the "never acknowledge without the user's answer" wording;
  - the exact edit-again sequence and "at most twice";
  - the "changed again since triage" handling naming REMOVED, CRITERIA_REMOVED, RELINK, `REF_REMOVED`, `CRITERION_REMOVED` and `update-triage`.

**Checkpoint**: US3 green; a missed LOAD change stays in triage.

---

## Phase 6: User Story 4 — Criteria with no baseline value are labelled as such (Priority: P2)

**Goal (FR-016–FR-017)**: a never-baselined linked criterion is shown as "not baselined", not as unchanged.

**Independent test**: four rows (no entry; entry without the criterion; equal; different) → four labels.

- [X] T017 [P] [US4] Command-level rows in tests/Spectra.CLI.Tests/Commands/Generate/CompileUpdatePromptContextTests.cs (T4, the proof). In `compile-update-prompt --test-id` output:
  - no baseline at all → `- AC-1 (not baselined — compare the test with the current text): Each user MUST get 30GB`;
  - baselined without AC-1, then AC-1 linked by editing the test's `criteria` → the same label;
  - baselined and equal → `- AC-1: …`;
  - baselined, then the criterion reworded → `(changed since baseline)`;
  - a group where one linking test is baselined-equal and another has no entry → `not baselined` (precedence);
  - byte-identical across two runs.

  Add builder rows in tests/Spectra.CLI.Tests/Generation/UpdateContextBuilderTests.cs asserting `LinkedCriterionState.NotBaselined`. These are extra coverage, not the proof.
- [X] T018 [US4] In src/Spectra.CLI/Generation/UpdateContext.cs:
  - add `NotBaselined` to `LinkedCriterionState`;
  - compute each criterion's state with the precedence Removed > ChangedSinceBaseline > NotBaselined > Unchanged (research R4);
  - a test with no entry, or an entry without the id, contributes `NotBaselined`.

  In src/Spectra.CLI/Generation/UpdatePromptRenderer.cs, render `- <id> (not baselined — compare the test with the current text): <text>`.
- [X] T019 [US4] Adapt the F4 assertions from T003, and list each in the Notes:
  - `UpdateContextBuilderTests.Criteria_NeverBaselined_IsUnmarked_NoneLinked_IsEmpty` → `NotBaselined`;
  - `CompileUpdatePromptContextTests.OnlyALinkedCriterion_StillGivesAPrompt` → the not-baselined line.

**Checkpoint**: US4 green.

---

## Phase 7: User Story 5 — A batch with failed files says so in its exit code (Priority: P2)

**Goal (FR-018–FR-021)**: `--all` exits 8 when any file failed, and 0 otherwise. Whole-command errors stay exit 1.

**Independent test**: one failed file → 8; all failed → 8; no failure (incl. `unchanged_pending`) → 0.

- [X] T020 [P] [US5] Write tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateExitCodeTests.cs (T5). The rows:
  - **Exit 8 with one failed file:** a theory over the failure codes, each next to a valid file — `NO_ORIGINAL`, `ID_MISMATCH`, `MALFORMED_JSON`, `SCHEMA_INVALID`, `DRIFT_DETECTED`, and `MULTIPLE_TESTS` (a command-level row, no new API).
    - Each exits 8.
    - The JSON parses and has the same keys as an exit-0 batch.
    - The valid file is `written` and deleted; the failed file is kept.
  - **Every file failed** → exit 8.
  - **Exit 0:**
    - only `written` / `unchanged` → 0;
    - an `unchanged_pending` outcome alone → 0;
    - an empty or missing folder → 0.
  - **Repairs folder:** a failed file there with `--keep-baseline` → 8.
  - `UNREADABLE` and `PERSIST_FAILED` are covered by the shared "any `failed` outcome → exit 8" rule. The test asserts
    the rule over the outcome list, with one row per simulable code (`NO_ORIGINAL`, `ID_MISMATCH`, `MULTIPLE_TESTS`, an
    ingest code), rather than simulating I/O failures.
  - **Exit 1:** an unparseable `_source-baseline.json` → 1 with `BASELINE_UNREADABLE`, and no staging file deleted.
  - **Regression proof:** on 6ea7520 the failure rows exit 0.
- [X] T021 [US5] In `IngestUpdateCommand`, add `ExitStagedFailures = 8` with a doc comment saying it is command-namespaced, like `suite delete`'s 8. `RunBatchAsync` returns it after `EmitBatchResult` when any outcome is `failed`. The early exit-1 paths stay.
- [X] T022 [US5] Adapt the F5 assertions from T003 (exit 0 → 8), and list each in the Notes:
  - `IngestUpdateBatchTests.BatchIngest_MalformedUpdateFile_FailsLoudPerEntry_ContinuesRest`;
  - `IngestUpdateBatchTests.BatchIngest_StagedUpdateForMissingOriginal_IsSkipped`;
  - in `IngestUpdateStagingTests`, `EditedUnchangedMalformed_…`, `NoOriginal_IsFailedAndKept`, `AStagedIdOtherThanTheFileName_IsIdMismatch_AndKept` and `TheHumanSummary_…`. These currently use `RunJsonAsync`, which throws on non-zero; switch them to `IngestAllAsync`.
- [X] T023 [US5] Skills:
  - **spectra-update.md Step 5:** exit `0` or `8` → read `results`; `8` means some files failed — retry them (at most twice) and continue, never stop. Exit `1` stays as in "Exit codes".
  - **spectra-generate.md Step 8b:**
    - exit `8` → read `results`, re-patch the `failed` tests fixing the reported error, re-stage them in `.spectra/repairs/{suite}/`, and run the same call again (at most twice), then continue;
    - re-critic only tests reported `written` or `unchanged`;
    - state that re-staging and re-ingesting after an ingest failure completes the same repair attempt: it does not
      increase the count passed to `ingest-grounding --repaired --repair-attempts N`, and it does not trigger another
      critic pass on its own;
    - add one sentence to the restated NON-STOP paragraph: exit 8 from `ingest-update --all` is neither broken nor zero-result; the batch completed and `results` lists the failures.
    - Leave the `written == 0` guard text unchanged.
  - **SkillsManifestTests:**
    - both skills mention exit `8` with `results`;
    - the generation skill caps the retry at twice and states that the retry is the same repair attempt;
    - the `written == 0` STOP guard text is still present verbatim.

**Checkpoint**: US5 green; partly failed batches are visible to callers.

---

## Phase 8: User Story 6 — The staging folders are gitignored (Priority: P3)

**Goal (FR-022, FR-022a)**: `init` and `update-skills` apply one shared list that includes `.spectra/updates/` and `.spectra/repairs/`.

**Independent test**: the real init writes both entries; `update-skills` on a pre-089 workspace adds them once.

- [X] T024 [P] [US6] Write tests/Spectra.CLI.Tests/Commands/Init/StagingGitIgnoreTests.cs (T6). The rows:
  - **Real `InitHandler`:** `.gitignore` contains `.spectra/updates/` and `.spectra/repairs/`.
  - **Pre-089 workspace:** `.gitignore` holds the 088 list plus user lines; skill files are installed (`update-skills` needs them); a baseline is recorded.
    - `update-skills` (through the CLI, `spectra update-skills`) adds both entries exactly once.
    - Every original line is kept byte-for-byte.
    - The output lists `.gitignore` under "Updated:".
    - A second run changes nothing.
  - **Plain `init` on an existing workspace:** exit 1, and `.gitignore` is unchanged.
  - **Guard:** every pattern of the 088 init list is still in the shared list.
  - **Regression proof:** on 6ea7520 the entries are missing and `update-skills` never touches `.gitignore`.
- [X] T025 [US6] Extract src/Spectra.CLI/Commands/Init/SpectraGitIgnore.cs from `InitHandler.UpdateGitIgnoreAsync`:
  - `public static IReadOnlyList<string> Patterns` = the existing list + `.spectra/updates/` + `.spectra/repairs/` (with a Spec 089 comment);
  - `public static Task<IReadOnlyList<string>> EnsureAsync(string root, CancellationToken ct)`, the same append and create behaviour, returning the added patterns.

  src/Spectra.CLI/Commands/Init/InitHandler.cs calls it.
- [X] T026 [US6] src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs calls `SpectraGitIgnore.EnsureAsync(currentDir, ct)` after processing the files. It adds the `.gitignore` path to `updated` when anything was added, and to `unchanged` otherwise.
- [X] T027 [P] [US6] Add `.spectra/updates/` to the repository's .gitignore next to `.spectra/repairs/`, in the "per-session verdict files, drop trail, and repair scratch" block.

**Checkpoint**: US6 green.

---

## Phase 9: Polish, docs, regression proof

- [X] T028 [P] Update docs/cli-reference.md for `ingest-update`:
  - the two folders per mode;
  - the outcomes table with `unchanged_pending`;
  - `MULTIPLE_TESTS`;
  - the single-form `outcome`, `bucket` and `why`;
  - exit `8` next to 5 and 6 (batch);
  - the human summary lines.

  Also update the `ingest-update {suite} --all` row of the flags table, the `compile-update-prompt` criterion labels (not baselined), and a note on `update-skills` updating `.gitignore`.
- [X] T029 [P] Update docs/usage.md "Updating Test Cases After Doc Changes":
  - in the edit step, a test returned unchanged while a LOAD-class change is pending stays in triage and the assistant asks to acknowledge or edit again;
  - one test per staged file;
  - exit 8.

  In the Repairs paragraph, add the repairs folder.
- [X] T030 [P] Update CHANGELOG.md with a "Fixed (review fixes, Spec 089)" subsection under `[2.6.0]`, after the 088 subsections and before `### Notes`. Include the upgrade note: pre-089 repair leftovers in `.spectra/updates/<suite>/` can't be told apart from update edits. Check for `updated-*.json` files there before the first update batch (`ls .spectra/updates/*/`), and delete leftovers from a repair run. Also mention `spectra update-skills` adding the gitignore entries.
- [X] T031 [P] Update CLAUDE.md: a short "Recent Changes" entry for 089 at the top (F1–F6 in one line each, plus the counts from T033), and the `ingest-update` Commands line (`--all` reads updates/; `--all --keep-baseline` reads repairs/; exit 8).
- [X] T032 Regression proof (SC-006):
  1. `git worktree add <scratchpad>/wt-6ea7520 6ea7520`.
  2. Copy into it the new test files (StagingFolderSplitTests, MultipleTestsTests, UnchangedPendingTests, IngestUpdateExitCodeTests, StagingGitIgnoreTests), the command-level rows added to CompileUpdatePromptContextTests (as a separate file if needed), and the extended UpdatePromptWorkspace fixture.
  3. Build and run them.
  4. Record per fix the rows that fail there (each of F1–F6 needs at least one), then `git worktree remove`.

  Builder-level rows that don't compile on 6ea7520 are left out of the copy.
- [X] T033 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 1870 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts.
- [X] T034 Run quickstart.md steps 1–7 with the built CLI in a scratch workspace (copy the bin), and record the results in the Notes.
- [X] T035 Fill in the Notes (counts, the T032 proof, deviations, changed assertions), mark every task [X], and stop. Ask the user before committing as `fix(089): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T002) → **Foundational** (T003) → stories.
- **US1** (T004–T007), **US2** (T008–T010), **US3** (T011–T016) and **US5** (T020–T023) all edit `IngestUpdateCommand.cs`, so implement them in order: T005 → T010 → T013 → T014 → T021. Their test files (T004, T008, T011, T020) are independent and can be written first, in parallel.
- **US3** depends on US1 for its `--keep-baseline` row (repairs folder).
- **US5** depends on US2 (`MULTIPLE_TESTS` row) and US3 (`unchanged_pending` row).
- **US4** (T017–T019) and **US6** (T024–T027) are independent of the ingest stories and of each other.
- **The skills** use separate sections of the same two files: T006 (generate 8b path), T016 (update Step 5 pending), T023 (both, exit 8). Do them in that order.
- **Polish**: T028–T031 after the stories; T032 after all tests exist; T033–T035 last.

## Parallel Examples

```text
# Tests first (all different files):
T004 StagingFolderSplitTests   T008 MultipleTestsTests   T011 UnchangedPendingTests
T017 CompileUpdatePromptContextTests rows   T020 IngestUpdateExitCodeTests   T024 StagingGitIgnoreTests
# Independent streams:
US4 (T018–T019)  ||  US6 (T025–T027)  ||  the ingest chain (T005 → T010 → T013/T014 → T021)
# Docs at the end:
T028 cli-reference   T029 usage   T030 CHANGELOG   T031 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: the folder split stops repairs and update edits from ingesting each other in the wrong baseline
   mode. This is the defect that silently marks doc changes as reviewed.
2. **US2 and US3**: the two remaining silent-data-loss defects (dropped tests, a hidden LOAD change).
3. **US4 and US5**: labels and exit codes.
4. **US6**: hygiene.
5. **Polish**: docs, the 6ea7520 regression proof, a full run, the quickstart; commit and merge on approval.

## Notes

- **Baseline (T001)** on `6ea7520`: Core 1219 / CLI 1870 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 4 known warnings.
- **Final (T033):** Core 1219 / CLI 1928 (+58; only `ProfileFormatLoaderTests` fails) / Execution 228 / Integration 8.
  `dotnet build --no-incremental`: 0 errors, the same 4 warnings (no new ones). `ConfigRuleMatrixTests` and
  `InitTemplateWorkspaceTests` green.

**Regression proof (T032, SC-006).** Done before any source edit instead of in a worktree: the six new test files and
the extended fixture compile against the untouched `6ea7520` sources (`git diff --quiet 6ea7520 -- src` was clean), so
running them first *is* the run on `6ea7520`. Result: 38 of 50 failed there, all 50 pass now. Per fix (rows that fail on
`6ea7520` for the fix's own reason):
- **F1:** StagingFolderSplitTests (b), (c), (d ×2), (e), (f) — the repair batch read `.spectra/updates/`. Row (a)
  passes there too, as planned (the update batch never read a repairs folder).
- **F2:** MultipleTestsTests — single form, the 10-element array, one valid + one invalid object, duplicate ids,
  `(no id)`, a group array starting with another id (the first test was persisted). The empty-array row passes there
  (unchanged `NO_TESTS`), as intended.
- **F3:** UnchangedPendingTests — LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED, RELINK, single form, batch summary,
  real-init and pre-089 rows (`unchanged`, baseline rebuilt). The keep-baseline row failed there through F1 (repairs
  folder not read). The reviewable rows (LIGHT/COSMETIC/UNCHANGED) failed only on the new `unchanged_pending` field —
  coverage, not proof; the no-entry and partly-baselined rows pass there, as intended.
- **F4:** CompileUpdatePromptCriterionLabelTests — no baseline, entry without the criterion, group precedence
  (`- AC-1: …` was printed). Equal / changed rows pass there, as intended.
- **F5:** IngestUpdateExitCodeTests — every failure row (exit 0 there). The exit-0 and exit-1 rows pass there, as
  intended.
- **F6:** StagingGitIgnoreTests — real init and `update-skills` on a pre-089 workspace (entries missing). Plain init
  refusing passes there, as intended.
- Saved list: scratchpad `089/proof-6ea7520.txt`.

**Quickstart (T034)**, built CLI in a scratch workspace:
1. Real `spectra init`: `.gitignore` lists `.spectra/updates/` and `.spectra/repairs/`; 4 tests ingested.
2. 30GB → 45GB and "by link" → "through a link": triage G1 LOAD (TC-101, TC-102, TC-104), G2 LIGHT (TC-103).
3. Identical edits for TC-101 and TC-103 → `--all`: TC-101 `unchanged_pending` (LOAD, `numbers: 30gb→45gb`), TC-103
   `unchanged`; exit 0; staging empty; next triage still has TC-101 in the LOAD group, TC-103 gone.
4. A 3-element array as `updated-TC-102.json` → exit 8, `MULTIPLE_TESTS` naming TC-102, TC-101, TC-103; file kept.
5. A real repair in `.spectra/repairs/storage/` → `--all --keep-baseline`: TC-101 `written`, `baseline.mode: keep`,
   `updated-TC-102.json` untouched; the following `--all` read only `updated-TC-102.json` (exit 8 again).
6. A test linked to AC-1 before the criterion existed → `- AC-1 (not baselined — compare the test with the current
   text): Each user MUST get 30GB`.
7. `update-skills` on a pre-089 `.gitignore`: both entries added once, every original line kept (CRLF-normalised),
   second run no change.

The first quickstart run had two script bugs (a `tr -d '[]'` that broke the step-4 JSON — correctly reported as
`MALFORMED_JSON`, exit 8; and a byte-level `.gitignore` compare, see the next note); both fixed and re-run.

**Deviations.**
- **T032 proof** ran in place, before any source edit, rather than in a `git worktree` (same effect, no worktree).
- **T017 rows** went into a new file, `CompileUpdatePromptCriterionLabelTests.cs`, instead of
  `CompileUpdatePromptContextTests.cs` (the task allowed it), so the proof copy needed no edited 088 file.
- **T006 assertion:** the generation skill names `.spectra/updates/` once, in "repairs never go to `.spectra/updates/`",
  so the test asserts the update staging *file* path (`.spectra/updates/{suite}/updated-{id}.json`) is absent rather
  than any mention of the folder.
- **T024:** `update-skills` reports through Spectre's `AnsiConsole`, which console redirection doesn't capture
  reliably, so the test asserts the `.gitignore` itself, not the "Updated:" line. The line is listed in code
  (`UpdateSkillsHandler`).
- **`.gitignore` append:** `SpectraGitIgnore.EnsureAsync` keeps init's 088 append exactly: it trims the file's trailing
  whitespace and appends with the platform newline, so on Windows the appended block is CRLF in a LF file. Pre-existing
  behaviour, unchanged; every original line is kept.
- **Analysis I3/A3** (second pass) folded in: usage.md says acknowledge-or-edit-again for LOAD / CRITERIA_CHANGED and
  "re-run triage" for the others; the generation skill says a test written on a retry is re-critiqued once in the
  normal phase.
- **Added (not in a task):** the batch pre-check uses `ArrayObjects` for `ID_MISMATCH` too, so `StagedId` is gone; the
  single-form `unchanged_pending` human output also lists the `why` lines.

**Deliberately changed assertions (11, as research R8 predicted; no other existing test changed):**
| Test | Was | Now | Fix |
|---|---|---|---|
| `IngestUpdateBatchTests.BatchIngest_MalformedUpdateFile_FailsLoudPerEntry_ContinuesRest` | exit 0 | exit 8 | F5 |
| `IngestUpdateBatchTests.BatchIngest_StagedUpdateForMissingOriginal_IsSkipped` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.EditedUnchangedMalformed_…` → `EditedPendingMalformed_OneWrittenOnePendingOneFailed_ConsumedFilesDeleted` | exit 0; TC-102 `unchanged`, rebuilt | exit 8; TC-102 `unchanged_pending`, fingerprint unchanged, still LOAD | F3, F5 |
| `IngestUpdateStagingTests.NoOriginal_IsFailedAndKept` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.AStagedIdOtherThanTheFileName_IsIdMismatch_AndKept` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.KeepBaseline_UnchangedEdit_…` | staged in `.spectra/updates/` | `StageRepair` (`.spectra/repairs/`) | F1 |
| `IngestUpdateStagingTests.SingleForm_IdenticalEdit_…` → `SingleForm_IdenticalEdit_PendingLoadIsNotRecorded_LightIsRebuilt_UnlessKeepBaseline` | LOAD test rebuilt | LOAD `unchanged_pending`; the rebuilt case uses TC-104 with a LIGHT change (constructor gains TC-104 on Sharing) | F3 |
| `IngestUpdateStagingTests.TheHumanSummary_ListsEachTest_InBothModes` | exit 0; `TC-102: unchanged` | exit 8; the pending line; header has `unchanged_pending=0` | F3, F5 |
| `BaselineReviewFixTests.RepairBatch_WithKeepBaseline_LeavesThePendingChange` | `Stage` (updates) | `StageRepair` (repairs) | F1 |
| `UpdateContextBuilderTests.Criteria_NeverBaselined_IsUnmarked_NoneLinked_IsEmpty` → `…_IsNotBaselined_…` | `Unchanged` | `NotBaselined` | F4 |
| `CompileUpdatePromptContextTests.OnlyALinkedCriterion_StillGivesAPrompt` | `- AC-1: …` | `- AC-1 (not baselined — …): …` | F4 |
