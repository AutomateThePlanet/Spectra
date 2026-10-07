---
description: "Task list for Spec 088 — Update prompt context (Phase 3)"
---

# Tasks: Update Prompt Context (Phase 3)

**Input**: Design documents from `/specs/088-update-prompt-context/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required (T1–T9 in the spec, plus the SC-001 size check). Write each story's tests with or before its code.
CLI tests that run commands use `BaselineTestWorkspace` in the `CriteriaCommands` collection; RELINK / submodule rows
use `[GitFact]`.

**Organization**: by user story.
- US1 = the prompt's content for one test;
- US2 = one prompt per group;
- US3 = batch ingest, "unchanged", staging cleanup;
- US4 = the skill.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline: `dotnet build` and `dotnet test` on `088-update-prompt-context`. Expect Core 1219 / CLI 1812 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8, plus the 4 pre-existing warnings. Record them in the Notes of specs/088-update-prompt-context/tasks.md.
- [X] T002 [P] Create tests/Spectra.CLI.Tests/TestFixtures/UpdatePromptWorkspace.cs, extension helpers over `BaselineTestWorkspace`:
  - `CompileAsync(params string[] args) → (int Exit, string Prompt, string Error)`, which runs `ai compile-update-prompt` (human mode; the prompt is stdout);
  - `StageEdit(suite, id, TestCase edited)`, which writes `.spectra/updates/{suite}/updated-{id}.json` as a JSON array with `UpdatePromptCompiler.SerializeTest(edited)`;
  - `StagedFiles(suite)`;
  - `Load(suite, id) → TestCase` (parsed from disk);
  - `Count(string text, string needle)` (non-overlapping occurrences).

---

## Phase 2: Foundational

- [X] T003 Extend src/Spectra.CLI/Baseline/BaselineContext.cs (research R1):
  - `Task<string?> GetRawSectionAsync(string sourceRef, CancellationToken ct)`: the same resolution and file cache as `GetSectionAsync`; `SectionExtractor.Extract(text, AnchorOf(ref)).RawText` for a resolving ref, the whole file for an anchor-less ref, null when missing or anchor not found;
  - keep each criterion's current text from the existing criteria scan (first occurrence of an ID wins, as for fingerprints), and add `string? GetCriterionText(string id)`.
  No behaviour change for existing callers.

**Checkpoint**: the baseline context can serve raw sections and criterion text.

---

## Phase 3: User Story 1 — The edit prompt shows the doc change the test must follow (Priority: P1) 🎯 MVP

**Goal (FR-001–FR-006)**: for one test, the prompt has each ref's current section, previous normalised version and what changed (or its unresolved reason), and only its linked criteria with their state; nothing-to-reconcile is refused.

**Independent test**: `compile-update-prompt --test-id` for a test with a changed ref, an unchanged ref and one linked criterion shows exactly those blocks and no other criterion.

- [X] T004 [P] [US1] Write tests/Spectra.CLI.Tests/Generation/UpdateContextBuilderTests.cs (T1, T2; builder-level, a `BaselineTestWorkspace` and `BaselineContext.CreateAsync`). Rows:
  - changed ref → `State == Changed`, bucket LOAD, `Current` = the raw section (with markdown, e.g. `**45GB**`), `Previous` = the snapshot (lowercased), `Why` contains `numbers: 30gb→45gb`, `Diff` non-empty;
  - unchanged ref → `Unchanged`, `Current` set, no `Previous`;
  - a ref not in the baseline entry → `NotBaselined`, `Current` set, `PreviousUnavailableReason` set;
  - snapshot file deleted → `Changed`, `Previous` null, reason "no snapshot", `Why` contains `no diff: snapshot missing`;
  - an anchor-less ref → `Current` = the whole file;
  - anchor not found → `Removed`, reason `anchor not found`, no `Current`;
  - deleted file (no git) → `Removed`, reason `not found; not in a git repository`;
  - `[GitFact]` renamed file → `Relink`, reason `renamed to …`; a submodule not checked out → `Removed` with the submodule reason;
  - criteria: unchanged / changed since baseline / removed / a never-baselined existing criterion (unmarked); none linked → no criterion blocks; a criteria file with an unrelated `AC-OTHER` never appears;
  - two tests sharing a ref with the same fingerprint → one section block listing both tests; with different fingerprints → two blocks;
  - nothing to reconcile: a test whose only ref is missing and whose only criterion is removed → listed in `NothingToReconcile`; a test with one resolvable ref → not.
- [X] T005 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Generate/CompileUpdatePromptContextTests.cs (T1/T2 at the command level, `CriteriaCommands` collection):
  - `--test-id` prompt contains the section heading line `#### S1 — docs/storage.md#Quotas`, `Current section:`, the new value, `Previous version (normalised`, the old value lowercased, `What changed:`, the linked `AC-1` text, and does NOT contain `AC-2` (an unrelated criterion in the same criteria file) or any other criterion ID;
  - an unchanged second ref is marked `State: unchanged`;
  - with `update.light_threshold_words` set to a non-default value (e.g. 2) and a 4-word wording change, the prompt's `State: changed (…)` label (LOAD or LIGHT) matches the bucket `update-triage` gives the same change (analysis C1);
  - nothing to reconcile → exit 4, stdout empty, stderr one JSON document `{command:"compile-update-prompt", success:false, error_code:"NOTHING_TO_RECONCILE", …}` naming the test;
  - unknown test → `TEST_NOT_FOUND`; unknown suite → `SUITE_NOT_FOUND`; neither / both of `--test-id` and `--group` → `INVALID_ARGUMENTS`; all exit 4;
  - no `spectra.config.json` → a prompt on defaults (the 083 read rule), not exit 1.
- [X] T006 [US1] Create src/Spectra.CLI/Generation/UpdateContext.cs per data-model.md:
  - `SectionState`, `SectionBlock`, `LinkedCriterionState`, `CriterionBlock`, `TestBlock`, `UpdateContext`;
  - `UpdateContextBuilder.BuildAsync(tests, baseline, context, lightThreshold, ct)` per research R1:
    - tests sorted by ID (ordinal-ignore-case, then ordinal);
    - sections keyed by (ref as written, baseline fingerprint or null), labelled `S1…` in order of first appearance;
    - the per-ref description via `GetSectionAsync` / `GetRawSectionAsync` / `RelinkCandidateAsync` / `GetSnapshotAsync` / `ChangeClassifier.ClassifyRef`;
    - criteria as the union of linked IDs sorted by ID, with the state from the baseline entry and `GetCriterion`;
    - `NothingToReconcile` per FR-006.
  Depends on T003.
- [X] T007 [US1] Create src/Spectra.CLI/Generation/UpdatePromptRenderer.cs per research R2 and the contract: `SourceDiff(ctx)`, `Criteria(ctx)`, `Tests(ctx)`; `~~~~` fences; LF only; `What changed: removed "a b"; added "c"` from the `DiffRun`s in order; `State:` lines exactly as in contracts/cli-contract.md. Depends on T006.
- [X] T008 [US1] Rewrite src/Spectra.CLI/Prompts/Content/test-update.md (research R4):
  - frontmatter placeholders `source_diff`, `acceptance_criteria`, `tests`, `profile_format`, plus `test_case` and `current_source` marked legacy;
  - body: the edit-don't-regenerate rules, plus "edit only what the source change implicates; a test nothing implicates is returned unchanged; one JSON array, one object per `### TEST`, ids unchanged";
  - sections `## SOURCE SECTIONS` `{{source_diff}}`, `## ACCEPTANCE CRITERIA` `{{acceptance_criteria}}` (in `{{#if}}`), `## TESTS TO REVIEW` `{{tests}}`, `## OUTPUT FORMAT` `{{profile_format}}`.
  In src/Spectra.CLI/Generation/UpdatePromptCompiler.cs add:
  - `IsOutdated(PromptTemplate)` (user-customized and the body lacks `{{tests}}` or `{{source_diff}}`);
  - `CompileContext(UpdateContext, PromptTemplate, string profileFormat, bool group)` → prompt, or `TEMPLATE_OUTDATED` for an outdated template with `group`, or a prompt plus a warning (legacy values: `test_case`, `current_source` = the source diff, `acceptance_criteria`) for an outdated template with one test.
  The legacy `Compile` / `Assemble` stay unchanged (UpdatePromptCompilerTests keep passing). Depends on T007.
- [X] T009 [US1] Rewrite src/Spectra.CLI/Commands/Generate/CompileUpdatePromptCommand.cs for `--test-id` (research R5/R6):
  - `ConfigLoader.LoadForReadAsync(root, Source | Tests)`;
  - failures through `CriteriaCommandIO.WriteFailure` with the contract's codes (exit 4 refusals, exit 1 for `CONFIG_INVALID` / `BASELINE_UNREADABLE`);
  - load the suite's tests (`BaselineCommandIO.LoadSuiteTestsAsync`), the suite baseline and `BaselineContext`;
  - build the context, refuse `NOTHING_TO_RECONCILE`, compile with the template;
  - the size check (`TokenEstimator.Estimate` vs `config.Ai?.Analysis?.MaxPromptTokens ?? PreFlightTokenChecker.DefaultBudgetTokens` → `PROMPT_TOO_LARGE` with both numbers and the suggestion);
  - the light threshold passed to the builder is `config.Update?.EffectiveLightThresholdWords ?? 12`, exactly as `TriageEngine` (analysis C1);
  - warnings on stderr; the prompt on stdout.
  Remove the `CriteriaContextLoader` call (FR-005). Add the `--group` option, unused until T013. Depends on T008.

**Checkpoint**: US1 complete; single-test prompts carry the doc change.

---

## Phase 4: User Story 2 — One prompt per group (Priority: P1)

**Goal (FR-007–FR-013)**: `--group` compiles one prompt for a triage group: shared sections and criteria once, each test once, deterministic, budget-checked; outdated custom templates refused for groups.

**Independent test**: a 10-test group sharing one section compiles to one prompt where the section appears once and every test once, within 1.3 × its raw content.

- [X] T010 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Generate/CompileUpdatePromptGroupTests.cs (T3, T6, T7, SC-001):
  - a group of 3 tests sharing `#Quotas` (2 of them also link `AC-1`) → `#### S1 — docs/storage.md#Quotas` appears exactly once, `AC-1` once, each `### TEST <id>` exactly once in ID order, `## OUTPUT FORMAT` once;
  - two sections in first-appearance order;
  - group missing (no detail file) → `TRIAGE_MISSING`; `--group G9` → `GROUP_NOT_FOUND`; a member's section changed after triage → `TRIAGE_STALE`; all exit 4 in the shared shape;
  - T7: two runs (`--test-id` and `--group`) → byte-identical stdout;
  - T6: `ai.analysis.max_prompt_tokens` set to a value between one test's prompt and the group's → `--group` exit 4 `PROMPT_TOO_LARGE` with both numbers, and each member with `--test-id` → exit 0;
  - SC-001 (analysis A1): a realistic 10-test fixture — one changed section of about 15 lines, each test with 4–6 steps and an expected result, each linking its own criterion of one or two sentences — compile `--group`; the rendered variable part (`UpdatePromptRenderer.SourceDiff` + `Criteria` + `Tests` for that context, or equivalently the prompt minus the template compiled with empty blocks) is ≤ 1.3 × the sum of the raw parts (the section's current text, its snapshot and what-changed line, the 10 criterion texts, the 10 tests' JSON). The template instructions and the output schema are excluded.
- [X] T011 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Generate/UpdatePromptTemplateTests.cs (T4):
  - the built-in template declares `source_diff`, `tests`, `acceptance_criteria`, `profile_format` and its body uses `{{source_diff}}` and `{{tests}}`;
  - a custom `.spectra/prompts/test-update.md` copied from the 087 template (no new placeholders): `--test-id` exit 0, the prompt contains the section's current text (through `current_source`), and stderr warns `spectra prompts reset test-update`; `--group` → exit 4 `TEMPLATE_OUTDATED` with the same hint.
- [X] T012 [US2] Extract the group resolution from src/Spectra.CLI/Commands/Baseline/UpdateAckHandler.cs (`ResolveGroupAsync`, `MatchesAsync`, `SameMap`) into src/Spectra.CLI/Baseline/TriageGroupResolver.cs. `ResolveAsync(root, suite, group, byId, context, ct)` returns the bucket and the tests, with the same exceptions and messages (the messages may name the calling command). `UpdateAckHandler` calls it and keeps its own bucket refusals. All UpdateAck tests stay green.
- [X] T013 [US2] Implement `--group` in src/Spectra.CLI/Commands/Generate/CompileUpdatePromptCommand.cs: resolve the group through `TriageGroupResolver` (any bucket), build the context over its tests, refuse `NOTHING_TO_RECONCILE` naming the tests, compile with `group: true` (`TEMPLATE_OUTDATED` for an outdated template), and apply the size check. The light threshold is `config.Update?.EffectiveLightThresholdWords ?? 12`, as in T009 (analysis C1). Depends on T009, T012.

**Checkpoint**: US2 complete; one prompt per group.

---

## Phase 5: User Story 3 — Batch ingest, "unchanged" = reviewed, staging cleaned (Priority: P1)

**Goal (FR-014–FR-017)**: an identical edit is not written but recorded as reviewed (baseline rebuilt unless `--keep-baseline`); `--all` reports per file and deletes what it consumed.

**Independent test**: three staged files (edited, identical, invalid) → one written, one unchanged, one failed and kept; a second `--all` ingests nothing.

- [X] T014 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateStagingTests.cs (T5, `CriteriaCommands` collection; tests baselined, then the doc changed so the baseline differs):
  - `--all` with `updated-TC-101.json` (edited), `updated-TC-102.json` (identical to the original), `updated-TC-103.json` (malformed JSON) → JSON `written: 1`, `unchanged: 1`, `errors: 1`, `results` with outcomes `written` / `unchanged` / `failed` (`MALFORMED`); TC-102's file is byte-identical to before; TC-102's baseline entry now holds the current fingerprint (it leaves triage); TC-101 and TC-102 staging files are deleted, TC-103's kept;
  - a second `--all` → `written: 0`, `unchanged: 0`, only TC-103 failed again;
  - a staged file for a missing test → `failed` `NO_ORIGINAL` (and `skipped_no_original: 1`), kept;
  - a staged test whose `id` differs from its file name → `failed` `ID_MISMATCH`, kept;
  - `--keep-baseline` with an unchanged edit → `unchanged`, baseline entry byte-identical (not rebuilt), staging file deleted;
  - single form: `--test-id TC-102 --from <identical>` → exit 0, `unchanged: true`, `persisted: 0`, file untouched, baseline rebuilt; with `--keep-baseline` → not rebuilt;
  - the human summary lists each test with its outcome (stderr in JSON mode).
- [X] T015 [US3] In src/Spectra.CLI/Generation/UpdatedTestIngestor.cs:
  - add `static bool IsUnchanged(TestCase original, TestCase candidate)` (research R7: `ApplyEdit` result vs original on Title, Preconditions, Steps, ExpectedResult, TestData, ScenarioFromDoc, SourceRefs, Criteria; each string compared after normalising CRLF / CR to LF and trimming trailing whitespace per line and per field, null ≡ empty; whitespace inside a line counts; lists by sequence; comparison only — persisted content is never altered by it (analysis U1));
  - `IngestAsync` returns `IngestResult` with `Unchanged = true` (no persist) when the drift-free edit is unchanged;
  - add `Unchanged` to `IngestResult` (in its existing file);
  - extend tests/Spectra.CLI.Tests/Generation/UpdatedTestIngestorTests.cs: identical → unchanged, nothing written; an echo that differs only in trailing whitespace or line endings (a step with a trailing space, an expected result with CRLF and a final newline) → `unchanged` (no file write; at the command level the baseline is rebuilt); a change of inner whitespace in a step (`two  spaces` for `two spaces`) → an edit, not unchanged; null vs empty preconditions → unchanged; reordered tags with otherwise identical content → unchanged (tags aren't editable); drift still fails first.
- [X] T016 [US3] Single form in src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs: on `Unchanged`, record the original through `RecordAsync` with `RecordMode.Rebuild` (skip with `KeepBaseline`), and output `unchanged: true`, `persisted: 0` (JSON) / `Test '<id>' needs no change; its baseline was refreshed.` (human). Depends on T015.
- [X] T017 [US3] Batch form in src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs (research R7, contract):
  - a `StagedOutcome` per file (`written` / `unchanged` / `failed` with code and errors; `NO_ORIGINAL`, `UNREADABLE`, `ID_MISMATCH` — the staged test's `id` field vs the file name, ordinal-ignore-case — and the ingest codes);
  - baseline: `Rebuild` records written + unchanged originals, `KeepBaseline` only written;
  - after the lock body and a successful baseline record, delete the staging files of written and unchanged outcomes; on a baseline write failure, delete none;
  - JSON adds `unchanged` and `results`, keeping `written`, `skipped_no_original`, `errors`;
  - the human summary lists each test with its outcome (stderr in JSON mode).
  Check tests/Spectra.CLI.Tests/Commands/Generate/IngestUpdateBatchTests.cs and IngestBaselineRecordingTests.cs still pass; adapt only assertions that relied on staging files staying, and list each in the Notes. Depends on T016.

**Checkpoint**: US3 complete.

---

## Phase 6: User Story 4 — The update skill edits group by group (Priority: P2)

**Goal (FR-018)**: the skill's edit loop compiles per group, stages one file per test, ingests with `--all`, reports per test, retries failures twice with `--test-id`.

**Independent test**: skill content assertions.

- [X] T018 [P] [US4] In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs, assert for `spectra-update` Step 5:
  - `compile-update-prompt --suite <suite> --group`;
  - the staging path `.spectra/updates/<suite>/updated-<id>.json` with the Write tool;
  - `ingest-update <suite> --all`;
  - the report of `results` as updated / unchanged (acknowledged) / failed;
  - "at most twice" with `--test-id` for a failed test;
  - no reference to `.spectra/updated.json`;
  - RELINK / REMOVED / submodule groups never enter the loop (unchanged wording kept);
  - the NON-STOP contract names the staging path as an allowed Write target.
- [X] T019 [US4] Rewrite Step 5 (and the NON-STOP allowed-writes line and the pipeline diagram) of src/Spectra.CLI/Skills/Content/Skills/spectra-update.md per research R8:
  - per selected group: compile `--group`, read and edit in-session, Write one staging file per test (a JSON array with that one test), run `spectra ai ingest-update <suite> --all --output-format json --verbosity quiet`, report per test from `results`;
  - a failed test → up to 2 retries with `compile-update-prompt --test-id` → stage → `--all`, then report and continue;
  - exit-code handling for compile (exit 4 codes incl. `PROMPT_TOO_LARGE` → fall back to `--test-id` per test; `TEMPLATE_OUTDATED` → tell the user to run `spectra prompts reset test-update`);
  - Step 6 (`update-ack`) stays for tests reviewed without editing.
  Depends on T018.

---

## Phase 7: Polish, end to end, docs

- [X] T020 Write tests/Spectra.CLI.Tests/Commands/Generate/UpdatePromptEndToEndTests.cs (T8): run the real `spectra init`, write a doc and a test through `ai ingest-tests`, change the doc section's number, `update-triage` (LOAD group G1), `compile-update-prompt --group G1` (contains the new number), stage an edit using the new number, `ingest-update <suite> --all` (`written: 1`), then `update-triage` → the test is UNCHANGED and no staging file remains.
- [X] T021 [P] Update docs/cli-reference.md: `compile-update-prompt` (`--group`, the prompt layout and states, linked criteria only, refusals and codes, the size check, the 083 read rule, outdated templates) and `ingest-update` (`unchanged`, `results`, staging deletion, `NO_ORIGINAL` / `ID_MISMATCH`).
- [X] T022 [P] Update docs/usage.md "Updating Test Cases After Doc Changes": the edit step per group (what the prompt shows), the batch ingest, "unchanged = reviewed".
- [X] T023 [P] Update CHANGELOG.md: add an "Added / Changed (Spec 088 — update prompt context, Phase 3)" subsection under `[2.6.0]`, before `### Notes`.
- [X] T024 [P] Update CLAUDE.md: add a "Recent Changes" entry for 088 at the top, in the 087 style, with the counts from T025, and the `compile-update-prompt --group` / `ingest-update --all` lines in the Commands block.
- [X] T025 Run the full `dotnet build --no-incremental` (no new warnings) and `dotnet test`, and record the counts in the Notes.
- [X] T026 Run quickstart.md steps 1–6 with the built CLI in a scratch workspace, and record the results in the Notes.
- [X] T027 Fill in the Notes (counts, deviations, changed assertions), mark every task [X], and stop. Ask the user before committing as `feat(088): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T002) → **Foundational** (T003) → stories.
- **US1** (T004–T009): T003 → T006 → T007 → T008 → T009. Tests T004 and T005 come first.
- **US2** (T010–T013) needs US1's T009: T012 is independent; T009, T012 → T013.
- **US3** (T014–T017) is independent of US1/US2 (ingest side): T015 → T016 → T017.
- **US4** (T018–T019) needs US2 and US3 (the commands it calls).
- **Polish** (T020–T027) needs everything; T020 exercises US1–US3 together.
- T009 and T013 both edit CompileUpdatePromptCommand.cs, and T016 and T017 IngestUpdateCommand.cs: run each pair in order.

## Parallel Examples

```text
# Tests first:
T004 UpdateContextBuilderTests   T005 CompileUpdatePromptContextTests   T010 CompileUpdatePromptGroupTests
T011 UpdatePromptTemplateTests   T014 IngestUpdateStagingTests          T018 skill assertions
# Independent streams after Foundational:
US1 (T006–T009)  ||  US3 (T015–T017)  ||  T012 TriageGroupResolver
# Docs at the end:
T021 cli-reference   T022 usage   T023 CHANGELOG   T024 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: single-test prompts with the doc change and linked criteria only. This fixes the "edits blind" problem
   on its own.
2. **US2**: group prompts.
3. **US3**: batch ingest with unchanged and cleanup (the skill needs it).
4. **US4**: the skill on top of US2 and US3.
5. **Polish**: end to end, docs, full run, quickstart; commit and merge on approval.

## Notes

**Counts (T025).** Core 1219 / CLI 1870 (1 pre-existing unrelated failure: ProfileFormatLoaderTests) / Execution 228 / Integration 8 (was CLI 1812; +58). `dotnet build --no-incremental`: 0 errors, the 4 known warnings (DocsApplyMove /
DocsRetire CS0108, CriteriaLayoutMigrator CS8714/CS8621); a CS8619 in the new `UpdatePromptWorkspace` fixture was fixed.

**Quickstart (T026)**, built CLI in a scratch workspace, steps 1–6:
1. init + 3 tests via `ingest-tests` + `source-baseline --init --all-suites` → baselines in place.
2. 30GB → 45GB; triage → one LOAD group G1 (TC-101, TC-102), `numbers: 30gb→45gb`, TC-103 unchanged.
3. `compile-update-prompt --group G1` → exit 0; `#### S1 — docs/storage.md#Quotas` once; current "**45GB**", previous
   "… 30gb …", `What changed: removed "30gb"; added "45gb"`; only `AC-1`; `### TEST TC-101` / `TC-102` once each.
4. Staged TC-101 (edited) + TC-102 (identical), `ingest-update storage --all` → `written=1 unchanged=1`, per-test
   summary on stderr, no staging file left.
5. Triage → all 3 unchanged; `git status` (outside `.spectra/`) shows only `TC-101.md`, `_index.json` and
   `_source-baseline.json`.
6. `--group G9` → exit 4 `GROUP_NOT_FOUND`; a test on a deleted doc with no criteria → exit 4 `NOTHING_TO_RECONCILE`.

**Deviations from the spec / plan.**
- **Budget key.** The token budget is `analysis.max_prompt_tokens` (`config.Analysis.MaxPromptTokens`, top level), not
  `ai.analysis.max_prompt_tokens` as D7 / the 045 CLAUDE.md entry say; docs and the refusal message use the real key.
- **`ingest-update` has no `--dry-run`**; the spec edge case mentioning it and FR-014 were corrected before
  implementation.
- **083 read rule.** `compile-update-prompt` now loads config with `LoadForReadAsync(Source|Tests)` — defaults without
  `spectra.config.json` (it used to require the file).
- **Legacy `UpdatePromptCompiler.Assemble`** (still used by `UpdatePromptCompilerTests`) uses the loaded template only
  when it contains `{{test_case}}`, else its inline fallback.
- **Error codes.** The contract wrote `EMPTY` / `MALFORMED`; the real ingest codes are `EMPTY_CONTENT` /
  `MALFORMED_JSON` (docs use those). Added `PERSIST_FAILED` (an I/O exception while persisting one staged file — was a
  silent `errors++`).
- **`errors` keeps its 077 meaning**: `NO_ORIGINAL` outcomes count in `skipped_no_original`, not in `errors`.
- **Batch index fix.** `--all` regenerated each test's `_index.json` from the suite as loaded before the batch, so a
  later edit reverted an earlier one's index entry; each written edit now replaces the original in that list.
- **`TEMPLATE_OUTDATED` hint** names `spectra update-skills` as well as `spectra prompts reset test-update`: `spectra
  init` writes `.spectra/prompts/test-update.md`, the loader treats any file there as customised, and `update-skills`
  refreshes a copy that was never edited (hash manifest) while `prompts reset` discards edits. Same wording in the
  skill and docs.
- **Repairs.** `spectra-generate`'s repair batch also uses `ingest-update --all --keep-baseline`: a repair returning
  identical content is now `unchanged` (not re-written; no baseline change, as before), and consumed staging files
  are deleted. Its one-line description in the skill names the new fields.

**Changed assertions.**
- `Prompts/TestUpdateTemplateTests.Template_ReconcilesAgainstChangedSourceAndCriteria` asserted `{{test_case}}` /
  `{{current_source}}` in the built-in template; now `{{source_diff}}` / `{{tests}}` / `{{acceptance_criteria}}`
  (the legacy two stay declared, not used in the body). The same class's `Template_PreservesId` ("Keep the original")
  and `Template_KeepsProtectedFieldsUnchanged` ("tags") failed on the first 088 template wording; the template was
  reworded, the assertions were not changed.
- `IngestUpdateBatchTests`, `IngestBaselineRecordingTests`, `IngestUpdateBoundaryTests`, `IngestUpdateSecondRoundTests`
  and `BaselineReviewFixTests` pass unchanged.

**Not applied.** The second analyze's L1–L3 (low wording points) were left as they are.
