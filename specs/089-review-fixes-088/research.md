# Research: Review Fixes for Spec 088

All facts were checked against `6ea7520`. No open clarifications remain. F6's upgrade path was decided by the user on
2026-10-08 (R6).

## R1 — One staging folder per flow (F1)

**Decision.** `IngestUpdateCommand.RunBatchAsync` picks its folder and file prefix from the record mode:

| Mode | Folder | File | Read by |
|---|---|---|---|
| `Rebuild` (`--all`) | `.spectra/updates/<suite>/` | `updated-<id>.json` | the update skill |
| `KeepBaseline` (`--all --keep-baseline`) | `.spectra/repairs/<suite>/` | `repaired-<id>.json` | the generation skill's repair loop |

Neither mode enumerates the other folder. Every per-file rule is the same in both (088 FR-016): outcomes, results,
deleting consumed files only after the baseline record, keeping failed files. A missing folder reports nothing to
ingest and exits 0. The `file` field in `results` shows the folder actually read.

**Rationale.** The folder carries the intent. A file can't be ingested in the wrong baseline mode, because each mode
reads only its own folder. Detecting a file's origin from its content is impossible: repair and update edits have the
same shape.

**Alternatives.**
- A marker field inside the staged JSON: the model writes the file, so the marker can't be trusted.
- One folder with a mode recorded per file: the same trust problem, and more format.
- Refusing when the other folder has files: this blocks a legitimate flow because of the other one's leftovers.

**Not detected:** pre-089 repair leftovers in `.spectra/updates/`. They are documented in the CHANGELOG with how to
check: look for `updated-*.json` files that a repair wrote, then delete them.

## R2 — Exactly one test per staged file (F2)

**Decision.**
- `GeneratedTestIngestor` gains a small public helper, `ArrayObjects(content)`. It uses the same extraction as
  `ParseAndValidate` and returns the array's object elements' `id` values, with `null` for an object without an id.
  It returns `null` when the content doesn't parse as an array.
- `UpdatedTestIngestor.IngestAsync` calls it first. When there are more than one object, it returns
  `IngestResult.Failure(MULTIPLE_TESTS, …)` before parsing, validation, the drift check or any write.
- The message names the count and the ids (`(no id)` for a missing one), for example `The edit holds 10 test objects
  (TC-101, TC-102, …); a staged file holds exactly one test.`
- Malformed or truncated content keeps its existing codes. Zero objects stays `NO_TESTS` (from `ParseAndValidate`).

In the batch, the 088 `ID_MISMATCH` pre-check uses the same helper. More than one object gives `MULTIPLE_TESTS` with
the same message, so a group array never reports `ID_MISMATCH` because of its first element. Exactly one object whose
`id` differs from the file name gives `ID_MISMATCH`. The single form maps `MULTIPLE_TESTS` to exit 5, like every
content code except `SCHEMA_INVALID`.

**Rationale.** The check counts objects, not valid tests. An array with one valid test and one object without a
title must not slip through as "one test". Counting before validation also means a group array is reported as
"several tests" rather than as a schema error on a later element.

**Alternatives.** Ingesting each element as its own edit was rejected: it is a new feature, and the file name names
only one test. Checking `PersistedTests.Count` after validation was rejected because it misses objects dropped as
invalid.

## R3 — An unchanged edit doesn't hide a pending change (F3)

**Decision.** New `Baseline/PendingChangeCheck.ClassifyAsync(suite, original, baseline, context, threshold, ct)`
returns `PendingChange(Bucket, Why)` or `null`:
- It returns `null` when the test has no baseline entry. The caller records the test, as in 088.
- Otherwise it calls `TriageEngine.TriageTestAsync(suite, original, entry, context, threshold, cosmeticAcked: false,
  ct)` and uses its `Verdict.Bucket` and `Verdict.Why`.
- The buckets that need an edit are `Load`, `CriteriaChanged`, `Removed`, `CriteriaRemoved` and `Relink`
  (`PendingChangeCheck.NeedsEdit`). `Unchanged`, `Cosmetic` and `Light` count as reviewed.

`IngestUpdateCommand` uses the check only for an `Unchanged` ingest result in `Rebuild` mode:
- The threshold is `config.Update?.EffectiveLightThresholdWords ?? 12`, the same expression triage and
  `compile-update-prompt` use.
- The `BaselineContext` is created once per run, lazily on the first unchanged result, inside the baseline lock and
  against the baseline loaded there.
- When the bucket needs an edit, the outcome is `unchanged_pending`. Nothing is written and nothing is recorded, and
  in a batch the staging file is deleted. Otherwise the outcome is `unchanged` and the original is recorded with
  `Rebuild`, as in 088.

The original test is classified. An unchanged edit equals the original in every editable field, `source_refs` and
`criteria` included (088 FR-015), so the refs and criteria classified are the ones the edit would keep.

**Rationale.** The check reuses triage itself, so "pending" means exactly what the next triage would show (SC-003). A
COSMETIC change is reviewable without an edit, so it counts as reviewed, as COSMETIC auto-acknowledgement already
does.

**Alternatives.**
- Reading `.spectra/update-triage.json`: it can be stale, and the single form needs no triage.
- Recording and warning: this loses the change, which is the defect itself.
- Refusing the file as `failed`: it would be retried as a failure, but the model's answer ("no edit needed") is a
  legitimate answer that needs the user's decision, not a retry.

## R4 — Criterion state "not baselined" (F4)

**Decision.**
- `LinkedCriterionState` gains `NotBaselined`.
- For each linked criterion that exists, a test contributes one of three values:
  - "changed": the test's baseline entry has the id with a different fingerprint;
  - "not baselined": the test has no entry, or its entry lacks the id;
  - "unchanged": the fingerprint is equal.
- Across a group, the state is the strongest of the linking tests' values. The precedence is Removed (the id doesn't
  exist) > ChangedSinceBaseline > NotBaselined > Unchanged.
- The renderer line is `- AC-1 (not baselined — compare the test with the current text): <text>`, the same form as
  `(changed since baseline)`.
- Order and fences are unchanged, so the output stays byte-identical for identical inputs.

**Rationale.** In the prompt, "unchanged" means there is nothing to reconcile. A criterion that was never compared
must not claim that. The wording matches the sections' `not baselined` state.

## R5 — Exit 8 for a batch with failures (F5)

**Decision.**
- `IngestUpdateCommand` gets `ExitStagedFailures = 8`, command-namespaced in the same way as `suite delete`'s 8.
  `ExitCodes` itself is unchanged.
- `RunBatchAsync` returns 8 after emitting the normal result when any outcome is `failed`, whatever its code
  (`NO_ORIGINAL` included). It returns 0 otherwise; `unchanged_pending` is not a failure.
- The existing early exits keep 1: config, lock, `BASELINE_UNREADABLE`, and `BASELINE_WRITE_FAILED` after a persist.
- The JSON, the deletions and the kept files are exactly as with exit 0.

**Skills.** Both skills read `results` on exit 8 and retry the failed tests. The generation skill's restated NON-STOP
paragraph says a broken or zero-result verb is a STOP signal. It gains one sentence saying that exit 8 from
`ingest-update --all` is neither: the batch completed and `results` lists the failures. Its `written == 0` guard,
which is about `ingest-grounding`, is untouched.

## R6 — Gitignore for new and existing workspaces (F6)

**Decision (user, 2026-10-08):** existing workspaces get the entries from `spectra update-skills`.
- The pattern list and the idempotent append are extracted from `InitHandler.UpdateGitIgnoreAsync` into
  `Commands/Init/SpectraGitIgnore`, with a `Patterns` list and `EnsureAsync(root, ct)` that returns the added entries.
  The append keeps the existing behaviour: a missing pattern is appended under `# SPECTRA` and the file is created
  when absent.
- `InitHandler` and `UpdateSkillsHandler` both call `SpectraGitIgnore.EnsureAsync`. `update-skills` lists `.gitignore`
  under "Updated:" when it added something.
- The list gains `.spectra/updates/` and `.spectra/repairs/`. The repository's `.gitignore` gains
  `.spectra/updates/`; `.spectra/repairs/` is already there.
- Plain `spectra init` on an existing workspace keeps refusing.

**Rationale.** `update-skills` is the documented upgrade step: it already refreshes unedited skills and prompt
templates. One shared list keeps init and upgrade from drifting.

**Alternatives** (offered to the user):
- `init --force`: it overwrites the config.
- Changing init's refusal path: a side effect of a command that reports failure.
- Documentation only.

## R7 — Skill changes

**`spectra-update` Step 5:**
- On exit 0 or 8, report `results`.
- `failed`: retry at most twice with `--test-id`, as in 088.
- `unchanged_pending`: list the tests with `bucket` and `why`.
  - **LOAD and CRITERIA_CHANGED:** ask the user per group whether to acknowledge (`spectra ai update-ack --suite
    <suite> --ids …`) or edit again. Editing again is `compile-update-prompt --test-id`, then edit, stage and
    `ingest-update --all`, within the same at most two retries per test as failures. Never acknowledge without the
    user's answer.
  - **REMOVED, CRITERIA_REMOVED and RELINK:** these never come from an edit group, so the documentation changed after
    triage. Say so, offer no acknowledgement (`update-ack` refuses with `REF_REMOVED` / `CRITERION_REMOVED`), and have
    the user re-run `update-triage`; Step 2a or Step 3 then handles the test (analysis I1).

**`spectra-generate` Step 8b:**
- Write to `.spectra/repairs/{suite}/repaired-{id}.json`.
- On exit 8, re-patch the failed tests, fixing the reported error, and re-stage. Run the same call again at most
  twice, then continue.
- Re-critic only the tests reported `written` or `unchanged`.
- Update the flags-table row and the result description (`unchanged_pending` never occurs with `--keep-baseline`).

**SkillsManifestTests:** assertions for each of these rules.

## R8 — Existing assertions that change deliberately

| Test | Today | After 089 | Fix |
|---|---|---|---|
| `IngestUpdateBatchTests.BatchIngest_MalformedUpdateFile_FailsLoudPerEntry_ContinuesRest` | exit 0 | exit 8 | F5 |
| `IngestUpdateBatchTests.BatchIngest_StagedUpdateForMissingOriginal_IsSkipped` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.EditedUnchangedMalformed_…` | exit 0; TC-102 (LOAD) `unchanged`, baseline rebuilt, leaves triage | exit 8; TC-102 `unchanged_pending`, baseline untouched, still LOAD | F3, F5 |
| `IngestUpdateStagingTests.NoOriginal_IsFailedAndKept` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.AStagedIdOtherThanTheFileName_IsIdMismatch_AndKept` | exit 0 | exit 8 | F5 |
| `IngestUpdateStagingTests.KeepBaseline_UnchangedEdit_…` | staged in `.spectra/updates/` | staged in `.spectra/repairs/` | F1 |
| `IngestUpdateStagingTests.SingleForm_IdenticalEdit_IsUnchanged_BaselineRebuilt_UnlessKeepBaseline` | LOAD test rebuilt | `unchanged_pending`, not rebuilt; the rebuilt case uses a LIGHT change | F3 |
| `IngestUpdateStagingTests.TheHumanSummary_ListsEachTest_InBothModes` | exit 0; `TC-102: unchanged` | exit 8; the TC-102 line is the pending wording | F3, F5 |
| `BaselineReviewFixTests.RepairBatch_WithKeepBaseline_LeavesThePendingChange` | staged in `.spectra/updates/` | staged in `.spectra/repairs/` | F1 |
| `UpdateContextBuilderTests.Criteria_NeverBaselined_IsUnmarked_NoneLinked_IsEmpty` | `Unchanged` | `NotBaselined` | F4 |
| `CompileUpdatePromptContextTests.OnlyALinkedCriterion_StillGivesAPrompt` | `- AC-1: …` (no baseline) | `- AC-1 (not baselined — …): …` | F4 |

The plan task re-checks the whole CLI suite for others: a full run at the start of implementation lists every test
that changes.

## R9 — Verification on real workspaces (083 lesson)

- **Real init:** `UpdatePromptEndToEndTests`-style tests run `InitHandler` with the embedded template. They cover F1
  (both flows back to back), F3 (a LOAD and a LIGHT test) and F6 (both `.gitignore` entries).
- **Pre-089 workspace:** a workspace initialised with the 088 gitignore list and with baselines recorded. In it,
  `update-skills` adds the two entries once and keeps every other line; F1 and F3 are re-run there.
- **Quickstart:** the built CLI in a scratch workspace.
- **Guards:** `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` stay green (no config change).
