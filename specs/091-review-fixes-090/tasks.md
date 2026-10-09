---
description: "Task list for Spec 091 — review fixes for 090"
---

# Tasks: Review Fixes for Spec 090

**Input**: Design documents from `/specs/091-review-fixes-090/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required (spec T1–T8). Each fix has a test that fails on `5c10c44`.
- The regression tests are written in Phase 2 and run against the untouched sources before any source edit
  (`git diff --quiet 5c10c44 -- src` must hold), so they are command-level and use only APIs that exist on `5c10c44`
  (`VerdictWorkspace`, `UpdatePromptWorkspace`, `BaselineTestWorkspace`).
- Workspaces are made with the real `spectra init` (`InitWithRealTemplateAsync`) in the `CriteriaCommands` collection.
- File times are set with `File.SetLastWriteTimeUtc`; no test sleeps.
- A pre-091 workspace has 090 records (no `compiled_at`), verdict files and grounding blocks.

**Organization**: by user story, in priority order.
- US1 = F1, old verdicts are stale (P1);
- US2 = F2, `--ids` for repaired grounding (P1);
- US3 = F3, kept hallucinated tests (P2);
- US4 = F4, critic agent text (P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `091-review-fixes-090` (= `5c10c44`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 2001 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.

---

## Phase 2: Foundational — regression tests and the proof against `5c10c44`

- [X] T002 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/VerdictOlderThanPromptTests.cs (spec T1–T3, FR-001–FR-005). Workspace from the real init, suite `storage`, TC-101/TC-102 linked to `docs/storage.md#Quotas`. Rows:
  - **T1 (fails on 5c10c44):** compile TC-101 (`CompileCriticAsync`), write a grounded verdict, set its time to now − 1 h; edit TC-101 by hand (change the expected result in the file); compile again; write no verdict. Then:
    - `ingest-verdict --from .spectra/verdicts/critic-verdict-TC-101.json` → exit 4, `error_code` `VERDICT_STALE`, message contains "older than the last critic prompt";
    - `ingest-verdict --suite storage --all` → `stale` contains TC-101;
    - `ingest-grounding --suite storage --test TC-101` → exit 4, `VERDICT_STALE`, TC-101 has no block;
    - `ingest-grounding --suite storage --all` → `stale` contains TC-101, `written` 0, TC-101 has no block.
  - **T1b (fails on 5c10c44):** after a compile, the record JSON has `compiled_at`, parseable as UTC and within [before, after] of the call.
  - **T2 (guard):** after the T1 sequence, write the same verdict bytes again and set the time to now + 1 min → `ingest-grounding --all` writes TC-101's block.
  - **T3 (guard):** a 090-format record (rewrite the record JSON without `compiled_at`) and a verdict set to now − 1 h with matching content → all four commands accept it; with the content changed by hand → stale (090 rule).
- [X] T003 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/RepairedIdsTests.cs (spec T4–T6, FR-006, FR-007). Rows:
  - **T4:** TC-130 and TC-131 with fresh partial verdicts (compile, then verdict). Stage a malformed `repaired-TC-130.json` and a good `repaired-TC-131.json`; `ingest-update storage --all --keep-baseline` (TC-130 failed, TC-131 written); compile TC-131 and write a grounded verdict. `ingest-grounding --suite storage --all --repaired --repair-attempts 1 --ids TC-131` → exit 0, TC-131 has a `repaired: true` block, TC-130 has no block.
  - **T5:** the same setup, `--all --repaired --repair-attempts 1` without `--ids` → exit 4, `error_code` `IDS_REQUIRED` (stderr JSON, shared shape), no test file changed (byte-compare both).
  - **T6:** `--all --ids TC-131,TC-999` → `not_found` == ["TC-999"], TC-131 processed.
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/KeptHallucinatedTests.cs (spec T7, FR-010, FR-011). Rows:
  - **T7:** TC-140 exists with a fresh hallucinated verdict and no block (compile, then verdict) → `ingest-grounding --all`: `errors` 0, `skipped_hallucinated` 1, `hallucinated` == ["TC-140"], TC-140 unchanged.
  - **T7b (guard, its own row):** only TC-141, with a verdict file of `{not json` (no record) → `ingest-grounding --all`: `errors` 1, TC-141 unchanged.
- [X] T005 [P] Extend tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs (spec T8, FR-009, FR-012, FR-013). Rows:
  - `spectra-generate` Step 8b's repaired call contains `--repaired --repair-attempts 1 --ids`;
  - `spectra-generate` says `skipped_hallucinated` is not a failure;
  - `spectra-update` Step 5a's repaired call contains `--ids <id>`;
  - `spectra-update` Step 5a's repair retry, **section-scoped**: slice spectra-update.md from `**Step 5a**` to the next `**Step ` header; inside that slice require "at most twice" together with `repaired-<id>.json` and `--ids <id>` (Step 5's own "at most twice" for edits must not satisfy it);
  - `spectra-update` Step 5a runs step 4 (re-critic, `ingest-verdict --all`, repaired grounding) only when step 3 reported `written` or `unchanged` for the test, and has the failed branch: "repair failed: <error_code>", no repaired grounding, the test listed as unverified (partial) or waiting for the user (hallucinated);
  - `spectra-generate` Step 8b's `--ids` list, **paragraph-scoped**: find the paragraph containing `--ids {ids}` and require, within that same paragraph, "written", "unchanged" and "dropped" — not the phrase "written or unchanged" alone (the re-critic phase heading already has it);
  - the critic agent (`AgentContent.All["spectra-critic.agent.md"]`), checked with whitespace normalised (runs of spaces/newlines → one space, so wrapping can't defeat it), does not contain "It writes nothing" and says the compile writes only the fingerprint record, never the verdict, and calls no model;
  - (guard) the existing NON-STOP and written==0 guard rows still pass unchanged.
- [X] T006 Proof run: with `git diff --quiet 5c10c44 -- src` holding, run the T002–T005 classes. Expect every T1, T1b, T4, T5, T6, T7 and T8 row to fail for the stated reason and the guard rows (T2, T3, T7b, NON-STOP) to pass. Both section-scoped T005 rows (Step 5a retry, 8b `--ids` paragraph) must fail here; if either passes on `5c10c44`, tighten it further before any source edit. Save the output to the session scratchpad (`091/proof-5c10c44.txt`) and summarise it in the Notes.

**Checkpoint**: the regression tests exist and are proven to fail on `5c10c44`.

---

## Phase 3: User Story 1 — old verdicts are stale (P1) 🎯 MVP

**Goal**: a verdict written before the last critic prompt is never accepted (FR-001–FR-005).

**Independent test**: T002 rows green.

- [X] T007 [US1] Add rows to tests/Spectra.CLI.Tests/Commands/Generate/VerdictOlderThanPromptTests.cs:
  - verdict time vs `compiled_at`: − 3 s → stale; − 1 s → fresh (allowance); equal → fresh; + 1 min → fresh;
  - `ingest-grounding --test TC-101 --from <copy>` where the copy's time is before `compiled_at` → `VERDICT_STALE`;
  - a verdict from stdin for `ingest-verdict` → accepted (no file time);
  - a pre-091 workspace: a 090 record, a verdict file and a critic grounding block on another test → every command runs as on `5c10c44`.
- [X] T008 [US1] In src/Spectra.CLI/Verification/VerdictFreshness.cs:
  - `VerdictRecord` gains a trailing optional parameter `[property: JsonPropertyName("compiled_at")] DateTimeOffset? CompiledAt = null`;
  - add `enum StaleReason { ContentChanged, OlderThanPrompt }` and `TimestampAllowance = TimeSpan.FromSeconds(2)`;
  - `FreshnessResult` gains `Reason`, `VerdictWrittenAt`, `CompiledAt`; `Message(id)` keeps the 090 text for `ContentChanged` and says "The verdict for {id} is older than the last critic prompt for {id} (verdict written {t:u}, prompt compiled {c:u}); run the critic for {id} again." for `OlderThanPrompt`;
  - `Check(root, id, current, string? verdictPath = null)`: the fingerprint rule first; then, when the record has `CompiledAt` and `verdictPath` exists, `File.GetLastWriteTimeUtc(verdictPath) < CompiledAt − TimestampAllowance` → stale (`OlderThanPrompt`);
  - `CheckRecordedTestAsync(root, id, verdictPath, ct)` passes the path through.
- [X] T009 [US1] In src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs `RecordAsync`: write `CompiledAt = DateTimeOffset.UtcNow`. Still nothing on a refusal; nothing deleted or renamed.
- [X] T010 [US1] In src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs: `--from` passes `from` to `CheckRecordedTestAsync`; the `--all` loop passes the verdict file it reads to `Check`.
- [X] T011 [US1] In src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs: the `--all` loop passes `vf` to `Check`; `--test` passes the path it actually read (`--from` or the default verdict path).
- [X] T012 [US1] Run VerdictOlderThanPromptTests, VerdictStaleTests, ManualVerdictTests, EditDropsVerdictTests, RepairLoopAfterEditTests, IngestGroundingCommandTests: green (RepairLoopAfterEditTests' `--all --repaired` row is adapted in T015).

**Checkpoint**: US1 green; T002 passes.

---

## Phase 4: User Story 2 — `--ids` for repaired grounding (P1)

**Goal**: only re-critiqued tests are stamped `repaired` (FR-006–FR-009).

**Independent test**: T003 rows green.

- [X] T013 [US2] Add rows to tests/Spectra.CLI.Tests/Commands/Generate/RepairedIdsTests.cs:
  - `--ids TC-131` without `--all` → exit 1, stderr is exactly "--ids is only valid with --all." (plus newline), stdout empty, no test file changed (byte-compare);
  - `--test TC-131 --all` → exit 1, stderr is exactly "Use either --test <id> or --all, not both." (plus newline), stdout empty, no test file changed (byte-compare) (FR-006a, a deliberate change: `--all` won);
  - `--test TC-131 --all --repaired` (no `--ids`) → exit 1 with "Use either --test <id> or --all, not both.", not `IDS_REQUIRED`;
  - `--ids TC-131` alone (no `--test`, no `--all`) → exit 1 with "--ids is only valid with --all." (not the "Either …" message);
  - `--all --repaired --ids " , "` (empty after trimming) → `IDS_REQUIRED`;
  - `--all --repaired` without `--ids` in a suite with no `_index.json` → still `IDS_REQUIRED` (checked first);
  - `--ids tc-131, TC-131` → TC-131 processed once (case-insensitive, de-duplicated);
  - `--all --ids TC-999` with no `.spectra/verdicts/` folder → exit 0, `not_found` == ["TC-999"];
  - the human summary names it: "TC-999: not found in suite 'storage'" (stdout in human mode; stderr in JSON mode);
  - `--all` (no `--repaired`) with `--ids TC-131` → only TC-131 considered; without `--ids` → as on `5c10c44`, `not_found` == [];
  - a Manual-verdict test and an already-grounded test, both listed in `--ids` with fresh verdicts → `manual` / `skipped_already_written`, nothing written for either;
  - **pre-091 workspace (FR-015):** the T4 scenario with every record rewritten without `compiled_at` (090 format) → the same outcomes (only TC-131 gets a block; without `--ids` → `IDS_REQUIRED`).
- [X] T014 [US2] In src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs:
  - add `--ids` (`Option<string?>`, "With --all: only these tests (comma-separated); required with --all --repaired");
  - parse: split on `,`, trim, drop empties, distinct `OrdinalIgnoreCase`;
  - argument checks, in this order and all before reading config, index or verdicts (nothing on stdout, nothing written; the strings must stay identical to T013 and T022):
    1. `--test` with `--all` → exit 1, stderr "Use either --test <id> or --all, not both.";
    2. `--ids` without `--all` → exit 1, stderr "--ids is only valid with --all.";
    3. neither `--test` nor `--all` → exit 1, the existing "Either --test <id> or --all must be specified.";
    4. `--all --repaired` with no ids after trimming → `IDS_REQUIRED` (exit 4, see below);
  - `--all --repaired` with no ids → `CriteriaCommandIO.WriteFailure(json, "ingest-grounding", "IDS_REQUIRED", "--all --repaired needs --ids with the tests re-critiqued after the repair; nothing was written.")`, exit 4, before reading config, index or verdicts (`public const string ErrorIdsRequired`);
  - `RunBatchAsync` takes the id set: `not_found` = ids not in the index (computed right after the index read); the loop skips verdict files of unlisted ids;
  - `EmitBatchResult` adds `not_found` (always present, `[]` without `--ids`), including the no-verdict-folder path, and the human summary gains one line per unknown id (`TC-999: not found in suite 'storage'`) — stdout in human mode, stderr in JSON mode (together with the existing counts line).
- [X] T015 [US2] Adapt the deliberate assertions (research R8):
  - tests/Spectra.CLI.Tests/Commands/IngestGroundingCommandTests.cs `AllMode_WritesPartialWithRepaired`: add `--ids TC-504`;
  - tests/Spectra.CLI.Tests/Commands/Generate/RepairLoopAfterEditTests.cs `TheGenerationRepairLoop_StillWritesTheRepairedVerdict`: add `--ids TC-102`; `skipped_already_written` 1 → 0;
  - search `tests/` for any other `--all` + `--repaired` call and adapt it; list every change in the Notes.
- [X] T016 [US2] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md Step 8b: the post-loop call becomes `spectra ai ingest-grounding --suite {suite} --all --repaired --repair-attempts 1 --ids {ids} --output-format json`, with `{ids}` = the comma-separated tests re-critiqued in this round (repair ingest `written` or `unchanged`) minus those dropped in the re-critic phase; skip the call when that list is empty. Do not touch the NON-STOP contract or the written==0 guard (check with `git diff` that those lines are unchanged).
- [X] T017 [US2] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md Step 5a:
  - step 4 (invoke the critic again, `ingest-verdict --suite <suite> --all`, then `spectra ai ingest-grounding --suite <suite> --all --repaired --repair-attempts 1 --ids <id> --output-format json`) runs **only** when step 3's `ingest-update <suite> --all --keep-baseline` reported `written` or `unchanged` for `<id>`;
  - when step 3 reports `failed` for `<id>`: re-patch it according to `error_code` (`MULTIPLE_TESTS`: one test per file; `DRIFT_DETECTED`: touch only what the critic flagged; `MALFORMED_JSON` / `SCHEMA_INVALID`: fix the JSON), write a fresh `.spectra/repairs/<suite>/repaired-<id>.json`, and run the same call again — **at most twice** (the same bound as Step 5 edits). These retries are part of the same repair attempt and don't change `--repair-attempts 1`;
  - only when it still reports `failed` after those retries (its file is now `.spectra/repairs/<suite>/failed-<id>.json`): report "repair failed: <error_code>", write no repaired grounding, and list the test as **unverified** (it was partial) or **waiting for the user** (it was hallucinated — the same choice as "still hallucinated": edit again, restore from git, or delete through `spectra-delete`).

**Checkpoint**: US2 green (FR-006–FR-009 incl. the human `not_found` line); T003 and the `--ids` and repair-outcome rows of T005 pass.

---

## Phase 5: User Story 3 — kept hallucinated tests (P2)

**Goal**: a kept hallucinated test is reported by id, not as an error (FR-010–FR-012).

**Independent test**: T004 rows green.

- [X] T018 [US3] Add rows to tests/Spectra.CLI.Tests/Commands/Generate/KeptHallucinatedTests.cs:
  - a hallucinated verdict that is also stale → listed under `stale`, not `hallucinated`;
  - a hallucinated verdict for a test that already has a block → `skipped_already_written`;
  - an index entry whose test file is gone, with a hallucinated verdict → neither `hallucinated` nor `errors` (the generation drop case);
  - stderr in JSON mode contains "TC-140: hallucinated, waiting for review" and the counts line; human mode prints both on stdout;
  - `ingest-grounding --test TC-140` → exit 4 as on `5c10c44`;
  - one batch: TC-140 (fresh hallucinated verdict, no block) and TC-141 (verdict file `{not json`, no record) → `errors` 1, `skipped_hallucinated` 1, `hallucinated` == ["TC-140"], both test files unchanged (post-fix row; T7b stays a separate guard row for the proof on `5c10c44`);
  - **pre-091 workspace (FR-015):** the T7 scenario with TC-140's record rewritten without `compiled_at` (090 format) → the same outcome (`errors` 0, `skipped_hallucinated` 1).
- [X] T019 [US3] In src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs `RunBatchAsync`: after the stale check, read the verdict (unreadable or empty → `errors`), parse it once; `hallucinated` → add to `hallucinated` and continue; reuse the parse for the partial pre-repair filter; unparseable → fall through to `WriteAsync` (refused → `errors`). `EmitBatchResult` adds `skipped_hallucinated` and `hallucinated`; the human summary gains one line per hallucinated id (`TC-140: hallucinated, waiting for review`), printed where T014 prints the summary (stdout in human mode, stderr in JSON mode).
- [X] T020 [US3] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md, next to the ingest-grounding result handling: `skipped_hallucinated` / `hallucinated` lists tests kept with a hallucinated verdict (for example by the update flow); it is not a failure — report the ids.

**Checkpoint**: US3 green; T004 passes.

---

## Phase 6: User Story 4 — critic agent text (P3)

**Goal**: the agent says what compiling its prompt writes (FR-013).

**Independent test**: T005's agent row green.

- [X] T021 [US4] In src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md step 1, replace "It writes nothing and calls no model." with: "It writes only the fingerprint record (`.spectra/verdicts/critic-input-<id>.json`, with the time it was compiled) — never the verdict — and calls no model." The old sentence spans a line break in the file ("…It writes nothing and" / "calls no model."): match across the newline, then re-wrap the paragraph at the file's width and indentation. T005 checks with whitespace normalised.

**Checkpoint**: T005 passes in full.

---

## Phase 7: Polish, docs, verification

- [X] T022 [P] Update docs/cli-reference.md:
  - `compile-critic-prompt`: compiling the whole suite (no `--test`) records `compiled_at` for every test, so older verdicts become stale and need a new critic run;
  - `compile-critic-prompt`: replace "Writes nothing; calls no model" (~line 470) — it writes only the fingerprint record, now with `compiled_at`;
  - `ingest-verdict` / `ingest-grounding`: the `compiled_at` rule (older than the last critic prompt → `VERDICT_STALE`; 2 s allowance; 090 records fingerprint only; no record or stdin → accepted);
  - `ingest-grounding`: `--ids`, `IDS_REQUIRED`, `not_found`, `skipped_hallucinated` / `hallucinated`, the human summary on stderr in JSON mode; `--test` with `--all` ("Use either --test <id> or --all, not both.") and `--ids` without `--all` ("--ids is only valid with --all.") are invalid (exit 1).
- [X] T023 [P] Update docs/usage.md: a failed critic run never revives an older verdict; repair rounds stamp only the re-critiqued tests; kept hallucinated tests are reported by id.
- [X] T024 [P] Update CHANGELOG.md with "Fixed (review fixes, Spec 091)" under `[2.6.0]`, before `### Notes`, and Notes entries: a customised skill that calls `--all --repaired` without `--ids` now fails with `IDS_REQUIRED` (run `update-skills` or add `--ids`); 090 records keep the fingerprint rule until the critic prompt is compiled again; `ingest-grounding --test` together with `--all` is now refused (exit 1; `--all` used to win).
- [X] T025 [P] Update CLAUDE.md: a short "Recent Changes" entry for 091 at the top, with the counts from T026.
- [X] T026 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 2001 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts.
- [X] T027 Run quickstart.md steps 1–7 with the built CLI (copy the bin) in a scratch workspace, and record the results in the Notes.
- [X] T028 Fill in the Notes (counts, the T006 proof, deviations, changed assertions), check line endings of every touched file (no mixed CRLF/LF), mark every task [X], and stop. Ask the user before committing as `fix(091): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001) → **Foundational** (T002–T006). T006 must run before any source edit.
- **US1** (T007–T012): T007 first; T008 → T009, T010, T011 (T010 and T011 are different files); T012 last.
- **US2** (T013–T017): after US1, because T011 and T014 both edit `IngestGroundingCommand.cs`. T013 first; T014 → T015; T016 and T017 in parallel.
- **US3** (T018–T020): after US2 (T019 edits the same batch loop and `EmitBatchResult`). T020 after T016 (same skill file).
- **US4** (T021): independent; can run any time after T006.
- **Polish:** T022–T025 after the stories; T026–T028 last.

## Parallel Examples

```text
# Regression tests (different files):
T002 VerdictOlderThanPromptTests   T003 RepairedIdsTests   T004 KeptHallucinatedTests   T005 SkillsManifestTests
# US1 call sites after T008:
T009 CompileCriticPromptCommand   T010 IngestVerdictCommand   T011 IngestGroundingCommand
# US2 skills:
T016 spectra-generate.md   T017 spectra-update.md
# Docs:
T022 cli-reference   T023 usage   T024 CHANGELOG   T025 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: a failed critic run can no longer turn an old verdict into a test's grounding.
2. **US2**: repaired blocks only for tests that were actually re-critiqued.
3. **US3**: kept hallucinated tests are named instead of counted as errors.
4. **US4**: the critic agent's text.
5. **Polish**: docs, full run, quickstart; commit and merge on approval.

## Notes

- **Baseline (T001):** `5c10c44` — Core 1219 / CLI 2001 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 0 errors, the 4 known warnings (`DocsApplyMoveCommand`, `DocsRetireCommand`, `CriteriaLayoutMigrator` ×2).
- **Regression proof (T006):** with `git diff --quiet 5c10c44 -- src` holding, the T002–T005 classes were run. All 16
  must-fail rows failed for the stated reason: T1 ×4 (the old verdict was accepted / not listed as stale), T1b (no
  `compiled_at`), T4 and T6 (`Unrecognized command or argument '--ids'`), T5 (exit 0, blocks written), T7 (`errors` 1),
  and the 6 T8 text rows, including both section-scoped ones (Step 5a "at most twice", the 8b `--ids {ids}`
  paragraph). The 5 guard rows passed (T2, T3 ×2, T7b, the NON-STOP / written==0 row). Output kept in the session
  scratchpad (`091/proof-5c10c44.txt`).
- **Final run (T026):** `dotnet build --no-incremental`: 0 errors, the same 4 warnings (none new). `dotnet test`:
  Core 1219 / CLI 2046 (only `ProfileFormatLoaderTests` fails; +45 tests) / Execution 228 / Integration 8.
  `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` green.
- **Quickstart (T027), built CLI, workspace from the real `spectra init`:**
  1. init and `ingest-tests` exit 0.
  2. F1: the record has `compiled_at` (UTC, `+00:00`); after an hour-old verdict, a hand edit and a second compile,
     `ingest-verdict --from` → exit 4 `VERDICT_STALE` "older than the last critic prompt … (verdict written …, prompt
     compiled …)"; `ingest-grounding --test` → exit 4; both `--all` forms list TC-101 under `stale` (`written` 0).
     Rewriting the same bytes (new file time) → `written` 1.
  3. A 090 record (no `compiled_at`) with an hour-old verdict and unchanged content → accepted, block written.
  4. F2: `--all --repaired` without `--ids` → exit 4 `IDS_REQUIRED`, no block; `--ids TC-103,TC-999` → `written` 1,
     `not_found` ["TC-999"], stderr (JSON mode) has the counts line and `TC-999: not found in suite 's'`; TC-103
     `repaired: true`. `--test --all` → `Use either --test <id> or --all, not both.` exit 1; `--ids` alone →
     `--ids is only valid with --all.` exit 1.
  5. F3: TC-102 with a fresh hallucinated verdict → `errors` 0, `skipped_hallucinated` 1, `hallucinated` ["TC-102"];
     stderr names `TC-102: hallucinated, waiting for review`; `--test TC-102` → exit 4.
  6. F4: after `update-skills`, `.claude/agents/spectra-critic.agent.md` lines 45–46 say the compile writes only the
     fingerprint record; "It writes nothing" no longer occurs.
  7. `audit-grounding` exit 0 on the mixed workspace (090 record, verdict files, blocks).
- **Line endings (T028):** no touched `.cs`/`.md`/`.json` file has mixed CRLF/LF.
- **Changed assertions (deliberate, research R8):**
  - `IngestGroundingCommandTests.AllMode_WritesPartialWithRepaired`: `--ids TC-504` added (FR-007).
  - `RepairLoopAfterEditTests.TheGenerationRepairLoop_StillWritesTheRepairedVerdict`: `--ids TC-102` added;
    `skipped_already_written` 1 → 0 (TC-101 is no longer considered).
  - No other test calls `--all --repaired` without `--ids` (searched).
- **Deviations / decisions:**
  - Usage errors (`--test` with `--all`, `--ids` without `--all`, neither) stay plain stderr in both output modes, like
    the existing "Either --test <id> or --all must be specified." (user decision; analysis I6 left as a documented
    exception to the shared JSON failure shape, which applies to coded refusals such as `IDS_REQUIRED`).
  - The `{ids}` definition sits in the same paragraph as the 8b call (analysis L1), so the paragraph-scoped check
    covers it.
  - `--ids` given but empty without `--repaired` selects no test (an empty selection), not today's whole suite.
  - The batch's human summary now also goes to stderr in JSON mode (research R5); stdout keeps one JSON document.
  - docs/usage.md "Repair Batch & Resume" steps 2–3 still described the pre-089 staging folder; corrected with the 091
    text.
- **Findings, not changed here:** the 090-review findings 4–6 stay deferred (audit-grounding doesn't show staleness;
  the gitignore offer record is per clone; delete parses every test file); batch `--dry-run` / `--no-review` for the
  ingest commands stays a tracked constitution IV gap (Phase 6).
