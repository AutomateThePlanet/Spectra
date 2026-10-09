---
description: "Task list for Spec 090 — Re-verify after update (Phase 4)"
---

# Tasks: Re-verify After Update (Phase 4)

**Input**: Design documents from `/specs/090-reverify-after-update/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required (spec T1–T13). Write each story's tests before its code.
- The 089-review fixes (B1 = FR-B1, D1–D3) must have tests that fail on `fc82b7a`. Those tests are written in Phase 2 and
  run against the untouched sources before any source edit (`git diff --quiet fc82b7a -- src` must hold), so they are
  command-level and use only APIs that exist on `fc82b7a`.
- CLI tests use `BaselineTestWorkspace` / `UpdatePromptWorkspace` in the `CriteriaCommands` collection, `TempWorkspace`
  for delete, and the real `InitHandler` for real-init rows.
- A pre-090 workspace is one baselined with `source-baseline --init` that has verdict files without a fingerprint
  record, critic and Manual grounding blocks, and a skills manifest without `gitignore_offered` (FR-E4).

**Organization**: by user story, in priority order.
- US1 = re-verification (A, P1);
- US2 = failed staging files (FR-B1, P1);
- US4 = safe delete (C, P1);
- US3 = `--dry-run` (FR-B2, P2);
- US5 = gitignore (D, P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `090-reverify-after-update` (= `fc82b7a`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 1928 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.
- [X] T002 [P] Create tests/Spectra.CLI.Tests/TestFixtures/VerdictWorkspace.cs, extension helpers over `BaselineTestWorkspace` that use only `fc82b7a` APIs:
  - `WriteVerdict(id, verdict, score = 0.9, findings = null)`, which writes `.spectra/verdicts/critic-verdict-<id>.json` in the critic's JSON shape (copy the shape from an existing ingest-verdict test fixture);
  - `VerdictRecordPath(id)` → `.spectra/verdicts/critic-input-<id>.json`;
  - `GroundingOf(suite, id)`, which parses the test and returns its `Grounding`;
  - `WriteManualGrounding(suite, id)`, which writes a `grounding:` block with `verdict: manual` through the parser and `TestFileWriter`;
  - `CompileCriticAsync(suite, id)`, `IngestVerdictAllAsync(suite)`, `IngestGroundingAsync(params string[] args)`, `AuditGroundingJsonAsync(suite)`, each running the CLI and returning (Exit, Out, Err).

---

## Phase 2: Foundational — regression proof for the 089-review fixes and the change inventory

- [X] T003 Write tests/Spectra.CLI.Tests/Commands/Generate/FailedStagingTests.cs (spec T6, FR-B1). Command-level, both folders (`--all` and `--all --keep-baseline`). The rows:
  - a malformed `updated-TC-101.json` → `--all` exits 8, `results[0].file == ".spectra/updates/storage/failed-TC-101.json"`, that file exists, and `updated-TC-101.json` doesn't;
  - a second `--all` reports nothing (exit 0, empty `results`);
  - an older `failed-TC-101.json` plus a new failing `updated-TC-101.json` → the old one is replaced (its content now equals the new one);
  - a `failed-TC-101.json` plus a good `updated-TC-101.json` → `written`, and both files are gone;
  - the same for `unchanged` and `unchanged_pending` outcomes;
  - the same rows in `.spectra/repairs/storage/` with `repaired-` / `failed-`.
- [X] T004 Write tests/Spectra.CLI.Tests/Commands/Init/GitIgnoreOfferTests.cs (spec T11–T13, FR-D1–D3). Command-level through `spectra update-skills` / the real `InitHandler`, reading `.spectra/skills-manifest.json` as JSON. The rows:
  - **T11 (FR-D1):**
    - a pattern recorded in `gitignore_offered` and absent from `.gitignore` is not added;
    - a pre-090 manifest (no `gitignore_offered`) and a `.gitignore` missing `.spectra/progress.json` (an 089-list pattern) → not added, while `.spectra/updates/` and `.spectra/repairs/` are added;
    - no `.gitignore` → none created, exit 0;
    - after a run, the manifest's `gitignore_offered` contains every pattern;
    - a second run changes nothing.
  - **T12 (FR-D2):**
    - `.spectra/updates` (no slash) counts as present;
    - `# .spectra/updates/` (comment) doesn't;
    - `foo/.spectra/updates/` (longer line) doesn't;
    - `!.spectra/updates/` → not added and recorded as offered.
  - **T13 (FR-D3):**
    - a CRLF file without a final newline → the existing bytes are a prefix of the new file, then exactly one CRLF, then the patterns with CRLF;
    - an LF file ending with `\n` → no blank separator line added beyond the existing content, and the new lines are LF;
    - trailing blank lines in the existing file are kept.
  - A real-init row (`skipSkills: true`): `InitHandler` → full list in `.gitignore`; `.spectra/skills-manifest.json` exists and its `gitignore_offered` holds every pattern of the 090 list. If `init --skip-skills` turns out not to write the manifest, FR-D1's pre-090 rule applies on the first `update-skills` run instead: assert that, and note it here in the tasks (analysis A1).
- [X] T005 Run T003 and T004 on the untouched sources: `git diff --quiet fc82b7a -- src` must succeed, then `dotnet test --filter "FullyQualifiedName~FailedStagingTests|FullyQualifiedName~GitIgnoreOfferTests"`. Record in the Notes, per fix (B1, D1, D2, D3), the rows that fail and why, and the rows that pass (and why that is expected). Save the list to the scratchpad.
- [X] T006 Inventory the assertions 090 changes deliberately (research R9). Grep tests/Spectra.CLI.Tests for:
  - `StagedFiles(` / `RepairFiles(` expecting `updated-` / `repaired-` names after a failure (089 tests, FR-B1);
  - `Grounding` asserted kept after an edit (FR-A1);
  - `compile-repair-prompt` refusals for `hallucinated` (R8);
  - `dependsOn:` in `TempWorkspace` and `DeleteHandlerTests` (FR-C1);
  - `skipped_already_written` counts that now become `skipped_manual` (FR-A3);
  - tests that read `.spectra/verdicts/` file listings (records now appear there);
  - `audit-grounding` assertions on entry or total counts for suites with tests that have no verdict file (FR-A7).

  Record the list in the Notes. No source edits.

**Checkpoint**: baseline recorded; the B1 and D proof captured; affected tests known.

---

## Phase 3: User Story 1 — An edited test is re-verified, and a stale verdict never lands on it (Priority: P1) 🎯 MVP

**Goal (FR-A1–FR-A5)**: a written edit drops the critic verdict, verdicts are tied to the content they judged, Manual verdicts are never overwritten, and the update skill re-verifies.

**Independent test**: edit a grounded test → unverified; an old verdict is refused as stale; a fresh critic pass grounds it.

- [X] T007 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/VerdictStaleTests.cs (spec T2, FR-A2). The rows:
  - **Record:** `compile-critic-prompt --suite storage --test TC-101` writes `critic-input-TC-101.json` with `id`, `suite`, `file` and a `sha256:` fingerprint. Legacy `--test <path>` writes one with `suite: null`. A refusal (missing docs) writes none. The suite-wide form (no `--test`) writes one per test.
  - **The fingerprint ignores grounding:** compile, write a grounded verdict, then `ingest-grounding --test` → written. Compiling again gives the same fingerprint.
  - **Stale, single form:** compile, write a verdict, edit the test file by hand (expected result), then:
    - `ingest-grounding --suite storage --test TC-101` → exit 4, `VERDICT_STALE` in the shared failure JSON, the test file byte-identical;
    - `ingest-verdict --from .spectra/verdicts/critic-verdict-TC-101.json` → exit 4, `VERDICT_STALE`.
  - **Stale, `--all`:** `ingest-verdict --suite storage --all` → `skipped_stale: 1`, `stale: ["TC-101"]`, the verdict not counted as grounded. `ingest-grounding --suite storage --all` → `skipped_stale: 1`, nothing written.
  - **No record (pre-090):** delete the record → both commands accept the verdict as before. `ingest-verdict` from stdin is accepted.
  - **Rows on a pre-090 workspace:** baselines, a verdict without a record, and a critic grounding block.
- [X] T008 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/ManualVerdictTests.cs (spec T3, FR-A3). The rows:
  - a test with a Manual block plus a grounded verdict file → `ingest-grounding --test` exits 4 `MANUAL_VERDICT`, and the file is byte-identical;
  - `--all` → `skipped_manual: 1`, `manual: ["TC-101"]`, `skipped_already_written` not counting it;
  - a Manual block and a fresh fingerprint record → still refused.
- [X] T009 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/EditDropsVerdictTests.cs (spec T1, FR-A1). Run each row through the single form, `--all`, and `--all --keep-baseline` (repairs folder). The rows:
  - **Critic block:** a test with a critic grounding block, verdict file and record, given an edit (`written`) → the block is gone, the verdict file and record are deleted, and every other line of the test file matches an edit of the same test without a block. `audit-grounding --suite storage` shows the test with `grounding_written: false` and no verdict.
  - **Manual block:** the block is kept byte-for-byte. JSON `manual_verdict_kept` is `true` (single) or `["TC-101"]` (batch). The human line reads `TC-101: updated (manual verdict kept)`.
  - **`unchanged` / `unchanged_pending` outcomes:** block, verdict file and record untouched.
  - **Real-init and pre-090 workspace rows.**
- [X] T010 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/RepairLoopAfterEditTests.cs (spec T4, FR-A1, FR-E2). On the CLI, the generation flow end to end:
  1. TC-101 and TC-102; compile the critic prompt for both;
  2. a grounded verdict for TC-101 and a partial one for TC-102;
  3. `ingest-verdict --all`, then `ingest-grounding --all` → TC-101 written, TC-102 skipped (pre-repair);
  4. stage a repair of TC-102 in `.spectra/repairs/`, then `ingest-update --all --keep-baseline` → `written`; TC-102's old verdict file and record are gone;
  5. compile again and write a grounded verdict for TC-102, then `ingest-verdict --all` and `ingest-grounding --all --repaired --repair-attempts 1` → TC-102 written with `repair_attempts: 1`, `repaired: true`.
  - A partial re-verdict → written flagged (`flagged_for_review: false`, since repaired). Match whatever `GroundingWriteBackService` writes on `fc82b7a` for `--repaired`, and assert that value.
  - **Flagged-review repair (analysis C1):** a test with a flagged partial block → `ingest-update storage --test-id TC-101 --from .spectra/repaired.json --keep-baseline` (`written`) → block and verdict file gone → `compile-critic-prompt` again (new fingerprint) and a grounded verdict → `ingest-grounding --suite storage --test TC-101 --repaired --repair-attempts 2` writes the block with `repaired: true` and `repair_attempts: 2`.
  - **Skill rows:** SkillsManifestTests asserts the generate skill's `written == 0` guard paragraph and the NON-STOP restatement verbatim (the existing 089 assertions stay), and that `spectra-review-flagged` still passes `--repair-attempts 2` explicitly.
- [X] T011 [US1] Create src/Spectra.CLI/Verification/TestContentFingerprint.cs, with `Of(TestCase) → "sha256:<hex>"` over `UpdatePromptCompiler.SerializeTest(test)` with CRLF normalised to LF. Create src/Spectra.CLI/Verification/VerdictFreshness.cs, with:
  - `RecordPath(root, id)` and `VerdictPath(root, id)`;
  - `WriteRecordAsync(root, VerdictRecord)`, an atomic temp-file-then-move write;
  - `ReadRecord(root, id)`;
  - `Check(root, id, TestCase current) → Fresh | Stale | NoRecord` (with both fingerprints);
  - `VerdictFiles.RemoveAsync(root, id)`, which deletes the verdict file and the record when present and returns the removed paths;
  - `record VerdictRecord(Id, Suite?, File, Fingerprint)`.
- [X] T012 [US1] In src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs, after each successful `CriticPromptCompiler.Compile`, in both suite mode and legacy path mode, write the record with a repo-relative `file`. Nothing is written on a refusal or error. Stdout is unchanged.
- [X] T013 [US1] In src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs:
  - **`--all`:** for each verdict file of the suite, load the test (from the index) and run `VerdictFreshness.Check`. A stale verdict is not classified; the JSON gains `skipped_stale` and `stale`, and the human summary gains them too.
  - **`--from`:** when the path is `.spectra/verdicts/critic-verdict-<id>.json` and a record exists, load the test from the record's `file` and check. Stale → exit 4 `VERDICT_STALE` in the shared failure shape (via `CriteriaCommandIO.WriteFailureJson`), naming the test and both fingerprints. Stdin or other paths are unchecked.
- [X] T014 [US1] In src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs:
  - **Single (`--test`):** a Manual verdict on the test → exit 4 `MANUAL_VERDICT`. A stale verdict → exit 4 `VERDICT_STALE`. Both use the shared failure shape and write nothing.
  - **`--all`:** check Manual before the already-written skip (`skipped_manual` + `manual`), then the existing skip, then staleness (`skipped_stale` + `stale`).
  - The JSON and human summary gain the new counts. The other counts keep their meaning.
- [X] T015 [US1] In src/Spectra.CLI/Generation/UpdatedTestIngestor.cs, `ApplyEdit` keeps `Grounding` only when its `Verdict` is `VerificationVerdict.Manual` (else null). In src/Spectra.CLI/Generation/IngestResult.cs, `Success(…)` carries `ManualVerdictKept` (true when the original had a Manual verdict). Unit rows go in tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs: critic grounding dropped, Manual kept, no grounding stays none.
- [X] T016 [US1] In src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs:
  - after a written outcome (single and batch, both modes, not on `--dry-run`), call `VerdictFiles.RemoveAsync(root, id)`, inside the same completed-run path that deletes consumed staging files;
  - the single JSON gains `manual_verdict_kept` (bool), and the batch JSON gains `manual_verdict_kept` (ids);
  - the human line for such a test reads `<id>: updated (manual verdict kept)`;
  - a verdict file that can't be deleted becomes a warning.
- [X] T017 [US1] FR-A6 (spec T15): in src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs, accept `partial` and `hallucinated` verdicts and refuse only `grounded` (keep exit 4 and adjust the message). Update its doc comment. Add rows to the existing compile-repair-prompt tests (hallucinated → prompt; grounded → refusal), and record any changed assertion in the Notes.
- [X] T018 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Review/AuditGroundingUnverifiedTests.cs (spec T14, FR-A7). The rows:
  - an edited test (block and verdict removed by FR-A1) → `verdict: "none"`, `grounding_written: false`, `action_needed: "critic"`, and `unverified: 1`;
  - a fresh-clone layout (a critic block, no verdict file) → the block's verdict and score, `grounding_written: true`, `action_needed` as today (`review` when flagged, else `none`) — not `"none"`;
  - a Manual block → `verdict: "manual"`, `grounding_written: true`, `action_needed: "none"`;
  - a pre-090 workspace where every test has a verdict file → output identical to `fc82b7a` apart from `unverified: 0` (compare with output captured from the `fc82b7a` build);
  - the exit code is unchanged in every row.
- [X] T019 [US1] In src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs (research R8a), keep the verdict-file entries exactly as they are, then add one entry per indexed test without a verdict file from its parsed grounding block (critic block → its verdict and score, `grounding_written: true`, `action_needed` from the existing rule; Manual → `manual`, `true`, `none`; no block → `none`, `false`, `critic`), ordered by test id after the existing entries. The summary gains `unverified`; the human table shows the new rows and the count. The exit code is unchanged.
- [X] T020 [US1] Rewrite src/Spectra.CLI/Skills/Content/Skills/spectra-update.md for FR-A4 (research R8). Add **Step 5a — Re-verify edited tests**, after each group's ingest:
  - **Scope:** for `written` results not in `manual_verdict_kept`. The first time in a run, ask "Re-verify the edited tests with the critic now? (recommended)"; if declined, collect them as unverified.
  - **Critic:** invoke the `spectra-critic` subagent per test with the Task tool (suite and id only; same procedure as the generate skill's 8a.1), then run `spectra ai ingest-verdict --suite <suite> --all --output-format json` and `spectra ai ingest-grounding --suite <suite> --all --output-format json`.
  - **Read each verdict file:**
    - **grounded** → done.
    - **partial / hallucinated** → one repair: `spectra ai compile-repair-prompt --suite <suite> --test <id>`, patch, Write `.spectra/repairs/<suite>/repaired-<id>.json`, `spectra ai ingest-update <suite> --all --keep-baseline --output-format json --verbosity quiet`, re-critic, `ingest-verdict --suite <suite> --all`, then `ingest-grounding --suite <suite> --all --repaired --repair-attempts 1`.
    - **Still partial** → flagged for review.
    - **Still hallucinated** → stop for that test and ask the user: edit again, restore the previous version from git (the user runs it), or delete through the `spectra-delete` skill. Never `record-drop` or delete from this skill.
  - **Step 7:** add grounded / partial-flagged / hallucinated / unverified / manual-kept counts with ids.
  - **NON-STOP allowed writes:** add `.spectra/repairs/<suite>/repaired-<id>.json`.
  - **Keep** the "never run delete" rule and every existing rule.

  Assert in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs (spec T5):
  - the step, the ask-once, the exact command sequence, `--repair-attempts 1`, "never `record-drop`" and no `spectra delete` invocation;
  - the summary counts;
  - the NON-STOP repairs path.
- [X] T021 [US1] Adapt the R9 assertions from T006 for FR-A1, FR-A3, FR-A6 and FR-A7 (R8, R8a) (ingestor grounding-kept rows, repair-prompt hallucinated refusal, any `skipped_already_written` that is now `skipped_manual`). List each in the Notes.

**Checkpoint**: US1 green; an edited test can't carry a stale verdict.

---

## Phase 4: User Story 2 — A failed staging file is never read again by a later batch (Priority: P1)

**Goal (FR-B1)**: failed files become `failed-<id>.json`, are never read by `--all`, and are cleared by a later success for the id.

**Independent test**: T003 (FailedStagingTests).

- [X] T022 [US2] In src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs, `RunBatchAsync` (completed path only, after the baseline record, next to the 088 deletion):
  - rename each `failed` outcome's file to `<folder>/failed-<id>.json` with `File.Move(…, overwrite: true)`, and set its `File` to the new repo-relative path;
  - for each `written` / `unchanged` / `unchanged_pending` outcome, delete `<folder>/failed-<id>.json` when present;
  - on a baseline write failure, rename and delete nothing;
  - the human failure line names the `failed-` path.
- [X] T023 [US2] Adapt the 089 assertions that expect a kept `updated-<id>.json` / `repaired-<id>.json` after a failure (T006 list: IngestUpdateStagingTests, StagingFolderSplitTests, MultipleTestsTests, IngestUpdateExitCodeTests, IngestUpdateBatchTests, and any others) to `failed-<id>.json`. Keep every other assertion. List each in the Notes.
- [X] T024 [US2] Skills: in spectra-update.md Step 5 and spectra-generate.md Step 8b, change "its staging file is kept" / "write … again (it replaces the kept file)" to the `failed-<id>.json` rule: the failed file is kept as `failed-<id>.json` for reading the error, and a retry writes a fresh `updated-<id>.json` / `repaired-<id>.json`. Assert the wording in SkillsManifestTests (both skills). The generate skill's NON-STOP and `written == 0` text stays verbatim.

**Checkpoint**: US2 green; T003 passes.

---

## Phase 5: User Story 4 — Deleting a test leaves nothing broken or stale behind (Priority: P1)

**Goal (FR-C1–FR-C4)**: delete changes only scalar `depends_on` values (losslessly), warns on list forms, and removes the baseline entries, staging files and verdict files.

**Independent test**: delete TC-101 with a scalar dependent, a body `- TC-101` and a list-form dependent → only the scalar `depends_on` changes; the rest are byte-identical; TC-101's stale state is gone.

- [X] T025 [P] [US4] Write tests/Spectra.CLI.Tests/Commands/Delete/SafeDeleteTests.cs (spec T8, T9). The rows:
  - **Scalar dependent:** TC-102 with `depends_on: TC-101` (LF file and CRLF file) → the key is dropped and every other byte is identical (compare with the original minus the `depends_on:` line).
  - **Comment above `depends_on:`:** dropped with the key (078 rule), asserted.
  - **TC-103:** a body line `- TC-101` and `criteria: [AC-1]` → byte-identical.
  - **TC-104:** block-list `depends_on:\n  - TC-101` and TC-105 with a flow list `[TC-101, TC-102]` → byte-identical, and JSON `warnings` names each file and `TC-101`.
  - **Dependent id:** a scalar dependent whose body also has a line `id: TC-999` → it is reported under its frontmatter id, never `TC-999`.
  - **Baseline:** TC-101's entry is removed from `_source-baseline.json` and the other entries are byte-identical.
  - **Staging:** `updated-TC-101.json`, `failed-TC-101.json` and `repaired-TC-101.json` are removed; another test's files are kept.
  - **Verdicts:** `critic-verdict-TC-101.json` and `critic-input-TC-101.json` are removed; snapshots are untouched.
  - **`--dry-run`:** the JSON lists `baseline_entries_removed`, `staging_files_removed`, `verdict_files_removed`, `warnings` and the dependency cleanup, and the workspace is byte-identical.
  - **Real-init and pre-090 workspace rows.**
  - **A triage group naming TC-101** → `TRIAGE_STALE`, as before (FR-C regression).
- [X] T026 [P] [US4] Write tests/Spectra.CLI.Tests/Commands/Update/LegacyUpdateDeleteTests.cs (spec T10, FR-C3). Run `spectra ai update <suite> --no-interaction --no-review --delete-orphaned` on a test the legacy classifier marks Orphaned (`UpdateReviewer.AutoReview`: `UpdateClassification.Orphaned` when `deleteOrphaned`). Confirm the exact `TestClassifier` condition for Orphaned while writing the fixture, and note it here. Assert that the delete goes through the shared delete service: the file is deleted; its index entry, baseline entry, staging files and verdict files are removed; a scalar dependent's `depends_on` is dropped losslessly.
- [X] T027 [US4] Add `BaselineStore.RemoveEntriesAsync(root, testsPath, suite, IReadOnlyCollection<string> ids, ct)` to src/Spectra.CLI/Baseline/BaselineStore.cs. It is called under `WithLockAsync` by the caller: strict load, remove the keys (ordinal), atomic save, no prune, and it returns the removed ids. A missing baseline file removes nothing.
- [X] T028 [US4] Create src/Spectra.CLI/Commands/Delete/TestDeletionService.cs (research R6):
  - **`PlanAsync`** resolves ids through the suite indexes, falling back to parsing each `.md` and matching the parsed `Id`. It collects:
    - scalar dependents: parseable tests with `DependsOn` equal to a deleted id, trimmed and case-insensitive;
    - list-dependent warnings: unparseable files whose frontmatter, loaded on its own with YamlDotNet `YamlStream`, has a `depends_on` sequence containing a deleted id, with the id from that frontmatter;
    - baseline entries, staging files and verdict files.
  - **`ApplyAsync`**, in this order:
    1. the indexes (the existing code);
    2. scalar dependents written as `test with { DependsOn = null }` through `TestFileWriter`;
    3. baseline entries removed under the baseline lock;
    4. staging and verdict files deleted;
    5. the test files deleted last.

  Each step's errors are collected, as today.
- [X] T029 [US4] Make src/Spectra.CLI/Commands/Delete/DeleteHandler.cs use the service for plan, dry run and apply. Remove `DependsOnLineRegex`, `StripDependsOnAsync` and the `id:` regex lookups. Add `BaselineEntriesRemoved`, `StagingFilesRemoved`, `VerdictFilesRemoved` and `Warnings` to the delete result model (find it next to `DeleteResult`), and to the human output. Exit codes are unchanged.
- [X] T030 [US4] In src/Spectra.CLI/Commands/Update/UpdateHandler.cs, `ApplyChangesAsync` deletes `ToDelete` proposals through `TestDeletionService` (automation checks off, as today). The bare `File.Delete` goes. Output lines stay the same.
- [X] T031 [US4] Adapt tests/Spectra.CLI.Tests/TestFixtures/TempWorkspace.cs `AddTestCase(dependsOn:)`: one id → scalar `depends_on: <id>`; several → the block form, with a comment that such files don't parse. Adapt `DeleteHandlerTests.Delete_CleansUpDependsOn` to the scalar form. List the changes in the Notes.
- [X] T032 [US4] FR-C4: assert in SkillsManifestTests that spectra-update still delegates deletes to `spectra-delete` and never runs `spectra delete` itself (keep or add the assertion).

**Checkpoint**: US4 green.

---

## Phase 6: User Story 3 — `ingest-update` can be previewed (Priority: P2)

**Goal (FR-B2)**: `--dry-run` reports the real run's outcomes and exit code, and changes nothing.

**Independent test**: one staging folder with every outcome; the dry run equals the following real run; the workspace hash is unchanged after the dry run.

- [X] T033 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateDryRunTests.cs (spec T7). The rows:
  - **Batch:** a staging folder holding written, unchanged, unchanged_pending, `MULTIPLE_TESTS`, `ID_MISMATCH`, `DRIFT_DETECTED` and `NO_ORIGINAL` → `--all --dry-run` has `dry_run: true` and the same `results` (outcome, code, bucket) and exit code as the real `--all` run right after.
  - **Nothing changes on the dry run:** a SHA-256 of every file under the workspace is equal before and after (test files, index, baseline, staging, verdicts, no `failed-` files).
  - **Single form:** `--test-id … --from … --dry-run` gives the same outcome JSON and exit code as the real run, with nothing written.
  - **Repairs folder:** `--all --keep-baseline --dry-run`.
  - **An unreadable baseline** → exit 1, as in the real run.
  - **No lock (analysis U2):** with `BaselineStore.LockTimeoutOverride` short and the baseline lock held by the test, `ingest-update storage --all --dry-run` completes with the normal outcomes and exit code, while the real run exits 1 with `LOCK_TIMEOUT`.
- [X] T034 [US3] Implement:
  - `persist` in `UpdatedTestIngestor.IngestAsync` (default true; false returns the same result without `PersistAsync`);
  - a `--dry-run` option in `IngestUpdateCommand`, for both forms:
    - strict `LoadSuiteAsync` without `WithLockAsync`;
    - the same pending check;
    - no `RecordAsync`, verdict cleanup, staging delete or rename;
    - JSON `dry_run: true` with a zero baseline summary;
    - a human first line `Dry run: nothing written.`;
    - the same exit code.

**Checkpoint**: US3 green.

---

## Phase 7: User Story 5 — Upgrading never overrides the user's `.gitignore` choices (Priority: P2)

**Goal (FR-D1–FR-D3)**: `update-skills` offers each pattern once, never creates `.gitignore`, checks presence by whole line, and appends without changing existing bytes.

**Independent test**: T004 (GitIgnoreOfferTests).

- [X] T035 [US5] Create src/Spectra.Core/Text/Newlines.cs, with `public static string Detect(string text)` (the logic of `TestCaseParser.DetectNewline`: CRLF when the first `\n` follows `\r`, else LF). Make src/Spectra.Core/Parsing/TestCaseParser.cs use it.
- [X] T036 [US5] Add `[JsonPropertyName("gitignore_offered")] public List<string>? GitIgnoreOffered` to src/Spectra.CLI/Skills/SkillsManifest.cs. It is null when absent (pre-090). Check that `SkillsManifestStore` round-trips it.
- [X] T037 [US5] Rewrite src/Spectra.CLI/Commands/Init/SpectraGitIgnore.cs (research R7):
  - `Patterns` (unchanged list) and `Pre090Offered` (`Patterns` minus `.spectra/updates/` and `.spectra/repairs/`);
  - `IsPresent(lines, pattern)`: whole trimmed lines; lines starting with `#` skipped; `pattern` or `pattern.TrimEnd('/')`;
  - `IsNegated(lines, pattern)`: a line `!` + either form;
  - `AppendAsync(path, patterns)`: read the text; newline = `Newlines.Detect`, defaulting to `\n` when the file has none; add one newline first when the content is non-empty and doesn't end with `\n`; `# SPECTRA` only when absent; each pattern plus the newline; existing bytes never changed;
  - `EnsureForInitAsync(root) → added`: create with the full list when absent, else append those neither present nor negated;
  - `OfferNewAsync(root, offered) → (added, nowOffered, missingFile)`.
- [X] T038 [US5] Wire the new behaviour into the two commands:
  - **src/Spectra.CLI/Commands/Init/InitHandler.cs:** `EnsureForInitAsync`, then record every pattern in the manifest's `gitignore_offered`.
  - **src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs:**
    - computes the candidates (never offered, using `Pre090Offered` when the record is null);
    - calls `OfferNewAsync`;
    - with no `.gitignore`, warns `no .gitignore; add: <patterns>` and records nothing;
    - otherwise records present, negated and added patterns;
    - reports `Updated: .gitignore (added: <patterns>)`;
    - saves the manifest.
- [X] T039 [US5] Re-run the 089 StagingGitIgnoreTests. Adapt deliberately only what FR-D changes (the byte-preserving append, and no create from `update-skills`), and list each change in the Notes.

**Checkpoint**: US5 green; T004 passes.

---

## Phase 8: Polish, docs, verification

- [X] T040 [P] Update docs/cli-reference.md:
  - `ingest-update`: verdict cleanup, `manual_verdict_kept`, `failed-<id>.json`, `--dry-run`;
  - `compile-critic-prompt`: the fingerprint record;
  - `ingest-verdict` / `ingest-grounding`: `VERDICT_STALE`, `MANUAL_VERDICT`, `skipped_stale`, `skipped_manual`;
  - `compile-repair-prompt`: accepts hallucinated verdicts;
  - `audit-grounding` (FR-A7): entries for tests without a verdict file (`none` / `critic`, the block's verdict, or `manual`), the `unverified` count, entries for tests with a verdict file unchanged, exit code unchanged;
  - `delete`: scalar-only `depends_on`, list warnings, baseline/staging/verdict cleanup, the JSON additions;
  - `update-skills` / `init`: the offered set, whole-line presence, the byte-preserving append.
- [X] T041 [P] Update docs/usage.md:
  - the edit step gains re-verification (critic, one repair, asking about hallucinated, the summary), and: "Edited tests show as unverified in `audit-grounding` until they are re-verified.";
  - failed staging files;
  - `--dry-run`;
  - delete is safe for the update workflow (scalar `depends_on`, list warning);
  - `update-skills` respects removed patterns.
- [X] T042 [P] Update CHANGELOG.md with "Added / Changed / Fixed (Spec 090 — re-verify after update, Phase 4)" subsections under `[2.6.0]`, before `### Notes`. Add Notes entries:
  - (in the Changed subsection, not Notes) `audit-grounding` also lists tests without a verdict file, judged by their grounding block, with an `unverified` count;
  - pre-090 verdicts without a record are accepted;
  - `depends_on` lists are not supported and are warned about;
  - `compile-critic-prompt` now writes a record;
  - `suite delete` leaves staging folders (known).
- [X] T043 [P] Update CLAUDE.md: a short "Recent Changes" entry for 090 at the top, with the counts from T045, and the Commands lines for `ingest-update` (`--dry-run`) and `delete`.
- [X] T044 FR-A5 check: `git diff --quiet fc82b7a -- src/Spectra.CLI/Prompts/Content/test-update.md src/Spectra.CLI/Generation/UpdateContext.cs src/Spectra.CLI/Generation/UpdatePromptRenderer.cs src/Spectra.CLI/Commands/Generate/CompileUpdatePromptCommand.cs` must succeed. Record the result in the Notes.
- [X] T045 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 1928 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts.
- [X] T046 Run quickstart.md steps 1–8 with the built CLI (copy the bin) in a scratch workspace, and record the results in the Notes.
- [X] T047 Fill in the Notes (counts, the T005 proof, deviations, changed assertions), check line endings of every touched file (no mixed CRLF/LF), mark every task [X], and stop. Ask the user before committing as `feat(090): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T002) → **Foundational** (T003–T006) → stories. T005 must run before any source edit.
- **US1** (T007–T021):
  - tests T007–T010 and T018 first;
  - code: T011 → T012 → T013, T014; T015 → T016; then T017; T019 (audit-grounding, after T016 so the edited-test row is real); then the skill (T020) and the adaptations (T021).
- **US2** (T022–T024): independent of US1 in code, but T016 and T022 both edit `IngestUpdateCommand.cs`; do T016 first.
- **US4** (T025–T032): independent of US1 and US2, except that it deletes the files US1 and US2 define (`critic-input-`, `failed-`). Do it after them so the deletion rows can use the real names.
- **US3** (T033–T034): after US1 and US2, which change the paths the dry run must skip.
- **US5** (T035–T039): independent; can run in parallel with US1–US4.
- **Skills:** spectra-update.md is touched by T020, T024 and T032, and spectra-generate.md by T024. Do them in that order.
- **Polish:** T040–T043 after the stories; T044–T047 last.

## Parallel Examples

```text
# Tests first (different files):
T007 VerdictStaleTests   T008 ManualVerdictTests   T009 EditDropsVerdictTests   T010 RepairLoopAfterEditTests
T018 AuditGroundingUnverifiedTests
T025 SafeDeleteTests     T026 LegacyUpdateDeleteTests   T033 IngestUpdateDryRunTests
# Independent streams:
US5 (T035–T039)  ||  US4 code (T027–T030)  ||  the ingest chain (T015 → T016 → T022 → T034)
# Docs:
T040 cli-reference   T041 usage   T042 CHANGELOG   T043 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: an edited test can no longer carry a verdict given to other content, and stale or Manual-overwriting
   ingests are refused. This is the core of Phase 4.
2. **US2**: failed staging files stop leaking (089 finding 1).
3. **US4**: delete is safe for the update workflow.
4. **US3 and US5**: `--dry-run` (constitution IV) and the gitignore fixes (089 findings 2–4).
5. **Polish**: docs, FR-A5 check, full run, quickstart; commit and merge on approval.

## Notes

- **Baseline (T001):** `fc82b7a` — Core 1219 / CLI 1928 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 4 known build warnings (`DocsApplyMoveCommand`, `DocsRetireCommand`, `CriteriaLayoutMigrator` ×2).
- **Regression proof (T005):** the 089-review rows (B1 = `FailedStagingTests`, D1–D3 = `GitIgnoreOfferTests`) were run
  against unchanged `src` (`git diff --quiet fc82b7a -- src`): 21 of 22 fail for the stated reason; the control row
  `AFailedFileOfAnotherTest_IsLeftAlone` passes. Output kept in the session scratchpad (`proof-fc82b7a.txt`).
- **Final run (T045):** `dotnet build --no-incremental`: 0 errors, the same 4 known warnings (none new). `dotnet test`:
  Core 1219 / CLI 2001 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8. The first full run
  also failed `DoctorIdsHandlerTests.Fix_UpdatesDependsOnReferences`: it asserted the block-list line `- TC-100` that
  the old `TempWorkspace` wrote; `doctor ids` never rewrites `depends_on`, so only the assertion changed (scalar form).
  `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` green.
- **FR-A5 (T044):** `git diff --quiet fc82b7a` over `test-update.md`, `UpdateContext.cs`, `UpdatePromptRenderer.cs`,
  `CompileUpdatePromptCommand.cs` (and `UpdatePromptCompiler.cs`) → unchanged.
- **Quickstart (T046), built CLI, scratch workspace from the real `spectra init`:**
  1. `compile-critic-prompt` wrote `critic-input-TC-101.json`; `ingest-verdict --all` grounded 1, `ingest-grounding
     --all` wrote 1 block.
  2. `ingest-update s --all` → written; grounding block, verdict file and record gone; `audit-grounding` lists TC-101
     (and the never-verified TC-102/103) with verdict `none`, `action_needed: critic`, `unverified: 3`.
  3. TC-102 edited after compile → exit 4 `VERDICT_STALE`; TC-103 Manual → exit 4 `MANUAL_VERDICT` (shared failure
     JSON, on stderr like every `WriteFailureJson` caller).
  4. Malformed `updated-TC-102.json` → exit 8, `results[].file` = `failed-TC-102.json`; second `--all` → no results,
     exit 0; a good `updated-TC-102.json` → written, staging folder empty.
  5. Three files (unchanged / written / `ID_MISMATCH`): `--dry-run` and the real run give the same outcomes and exit 8;
     the workspace hash is identical before and after the dry run.
  6. `delete TC-101 --dry-run` lists the dependent, `s/TC-101`, `repairs/s/failed-TC-101.json`, the verdict and record;
     the real delete removes only TC-102's `depends_on: TC-101` line; TC-103 (body `- TC-101`) byte-identical; baseline,
     staging and TC-101's verdict files gone (TC-103's verdict kept).
  7. CRLF `.gitignore` without a final newline, `.spectra/progress.json` and the staging folders removed, no
     `gitignore_offered`: `update-skills` → `Updated: .gitignore (added: .spectra/updates/, .spectra/repairs/)`; old bytes
     a prefix of the new file; tail `\r\n.spectra/updates/\r\n.spectra/repairs/\r\n`; `progress.json` not re-added;
     second run no change; with `.gitignore` deleted nothing is created (no warning: every pattern was already offered —
     the warning row is `GitIgnoreOfferTests.NoGitIgnore_IsNotCreated_AndNothingIsRecorded`).
  8. `.spectra/verdicts/` deleted: TC-102 (critic block) → `grounded`, 0.9, `grounding_written: true`, `none`; TC-103
     → `manual`; `unverified: 0`.
- **Line endings (T047):** no touched `.cs`/`.md`/`.json`/`.gitignore` file has mixed CRLF/LF.
- **Changed assertions (deliberate):**
  - `CompileRepairPromptCommandTests`: a hallucinated verdict → exit 0 (FR-A6).
  - `AuditGroundingCommandTests.EmptyVerdictDir`: Total 1, Unverified 1 (FR-A7).
  - `WriterPathPreservationTests`: an edit's `Grounding` is null, the block lines are gone (FR-A1).
  - FR-B1 kept-file names: `StagingFolderSplitTests`, `MultipleTestsTests`, `IngestUpdateStagingTests` (second run exit
    0 / empty; the human run stages a fresh file; kept path asserted), `IngestUpdateExitCodeTests`.
  - `TempWorkspace`: one dependency is written as scalar `depends_on:` (lists don't parse);
    `DeleteHandlerTests.Delete_CleansUpDependsOn`; `SuiteOpsTests.Delete_ExternalDeps_RefusesByDefault` uses two ids;
    `DoctorIdsHandlerTests.Fix_UpdatesDependsOnReferences` asserts `depends_on: TC-100`.
  - `StagingGitIgnoreTests`: strips `gitignore_offered` to simulate a pre-089 workspace.
  - `SkillsManifestTests`: `DoesNotKeepTheBaseline` narrowed to the update edit step (Step 5a passes `--keep-baseline`
    for repairs).
- **Deviations:** none from the FRs. The fingerprint record is written for the legacy `--test <path>` form too (suite
  `null`). A refusal (`VERDICT_STALE`, `MANUAL_VERDICT`) writes its JSON to stderr, as the shared shape always has.
- **Findings, not changed here:**
  - `spectra suite delete` misses scalar `depends_on` dependents outside the suite and cascades lossily; it also leaves
    the suite's staging folders.
  - `audit-grounding` lists every verdict file in `.spectra/verdicts/`, not only the suite's (pre-existing).
  - `spectra validate` reports success when test files fail to parse.
  - The 078 writer moves known keys into `KeyOrder` on the first write of a hand-ordered file.
  - `.spectra/verdicts/` (and `.spectra/dropped-tests.json`) are in the repository's own `.gitignore` but not in the
    list `spectra init` writes, so user workspaces may commit verdict files and fingerprint records.
