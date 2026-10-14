---
description: "Task list for Spec 097 — verification hardening (Phase 6, part 2)"
---

# Tasks: Verification Hardening (Phase 6, Part 2)

**Input**: Design documents from `/specs/097-verification-hardening/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (FR-025). Each new rule must fail on `807047c`, except the stated guards.
- The rows are written in Phase 2 and run on the untouched sources (`git diff --quiet 807047c -- src` must hold) before
  any source edit.
- Rows that use new members (`AiConfig.Critic`, `AgentContent.Render`, the `dryRun` parameter, `FreshnessResult.ReferenceAt`)
  can't compile on the base. They are staged out for the proof run (as in 094–096); their failure is "member missing".
- CLI rows use `BaselineTestWorkspace` (real `spectra init`; `InitWithSkillsAsync` where skills are needed) in the
  non-parallel `CriteriaCommands` collection and run the real root command (`RunAsync`).
- Skill rows read the bundled content through `SkillContent` / `AgentContent`, as `SkillsManifestTests` does.

**Organization**: by user story (spec.md).
- US1 = grounding blocks only for confirmed tests (F1, P1);
- US2 = truthful, small audit (F2, P1);
- US3 = no repair from a stale verdict (F3, P1);
- US4 = one clock (F4, P2);
- US5 = `--dry-run` on the batch ingests (F6, P2);
- US6 = the critic model (F7, P2);
- US7 = empty `--ids` (F5, P3).

US2 and US3 call `VerdictFreshness.Check`. It exists on the base, and US4 changes only its reference time, so the
stories stay independent. US2's `--ids` uses the US7 rule (`IdList` "given" = non-empty) — implement T034 before T017
or inline the same check.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US7 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `097-verification-hardening` (= `807047c`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1245 / CLI 2228 / Execution 228 / Integration 8 with 0 failures. Record the warning count. Record everything in the Notes.
- [X] T002 Add the pre-097 fixture helpers in tests/Spectra.CLI.Tests/TestFixtures/VerificationWorkspace.cs (extensions on `BaselineTestWorkspace`, R8):
  - `WriteVerdict(id, verdict, score, findings?)`;
  - `WriteRecord(id, suite, file, fingerprint, compiledAt?)` — 090 form without `compiled_at`, 091 form with it;
  - `SetFileTime(path, utc)`;
  - `TwoSuites()` — suites `a` (TC-101…TC-104) and `b` (TC-201, TC-202) with indexes, verdict files for both, and a stray `critic-verdict-TC-999.json`;
  - `PinnedCriticAgent()` — the critic agent as 807047c installed it (`model: claude-sonnet-4-6`) with its manifest hash recorded.

---

## Phase 2: Foundational — rule rows and proof

- [X] T003 [P] Write tests/Spectra.CLI.Tests/Skills/GroundingIdsSkillTests.cs (F1; FR-001–FR-003; R9). Rows:
  - **generate 8a:**
    - the batch call is `spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json`;
    - the step says the call is skipped when no test was kept as grounded;
    - no `ingest-grounding --suite {suite} --all --output-format json` line without `--ids` remains;
  - **update 5a:**
    - the grounding call carries `--ids <grounded ids>` and comes after the routing by `results`;
    - it is skipped when there are none;
  - **verbatim guards** (pass on both): the `GROUNDING-INGEST ANOMALY` / `GROUNDING-INGEST WARNING` blocks and the "NON-STOP CONTRACT (restated)" paragraph equal copies of the 807047c text kept in the test.
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/GroundingIdsBatchTests.cs (F1 CLI side; SC-001):
  - fresh `grounded` verdicts for TC-101, TC-102 and TC-103 (records written by the real `compile-critic-prompt`);
  - `ingest-grounding --suite a --all --ids TC-101,TC-102` → blocks on TC-101 and TC-102 only, `written: 2`, TC-103 byte-identical (guard: passes on the base — the 091 `--ids` already filters; it pins the behaviour the skills now rely on).
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Commands/Review/AuditTruthTests.cs (F2; FR-004–FR-009). Use `TwoSuites()`. Rows:
  - **scope:** `audit-grounding --suite a --output-format json` lists only a's indexed ids; no TC-201/TC-202, no TC-999;
  - **stale:**
    - TC-102 has a 091 record, and its test file is edited after the record (fingerprint changes);
    - its entry has `stale: true` and `action_needed: "critic"`, and `summary.stale` is 1;
    - another entry with no record has no `stale` field;
  - **stale by time:** TC-103's verdict file time is set 1 h before its record's file time → `stale: true`;
  - **`--action repair`:** only `repair` entries are listed; the summary equals the unfiltered run's summary;
  - **`--action critic,review` and `--action critic --action review`:** the same list, the union;
  - **`--action bogus`:** exit 1, stderr names the allowed values;
  - **`--ids TC-101,TC-999`:** only TC-101 is listed, and `not_found: ["TC-999"]`;
  - **`--ids " , "`:** the same as no `--ids` (US7 rule);
  - **human:** the Action column shows `critic (stale)` and the summary line contains `1 stale`;
  - **JSON mode (analyze A1):** with `--ids TC-101,TC-999 --output-format json`, stdout parses as one JSON document and
    stderr has `TC-999: not found in suite 'a'`;
  - **skill:** the generate skill's resume note calls `audit-grounding --suite {suite} --action repair`.
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/RepairFreshnessTests.cs (F3; FR-010–FR-012). Rows:
  - **prompt, stale:** TC-102 has a partial verdict and a record whose fingerprint differs from the current test → `compile-repair-prompt --suite a --test TC-102` exits 4, and stderr starts `VERDICT_STALE:` and contains the ingest message (`FreshnessResult.Message`);
  - **prompt, stale by time:** the verdict file time is set before the record file time → exit 4;
  - **prompt, no record:** exit 0 with a prompt (guard);
  - **batch:**
    - TC-102 stale partial, TC-104 fresh partial;
    - stdout is a JSON array with TC-104 only;
    - stderr has a JSON line with `"stale"` naming TC-102.
- [X] T007 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/OneClockTests.cs (F4; FR-013–FR-015; SC-004). Write the record through the real `compile-critic-prompt` and the verdict file, then set file times explicitly. Rows:
  - record file time t, verdict t + 10 s, `compiled_at` rewritten to t + 1 h → `ingest-verdict --from` and `ingest-grounding --test` accept (fails on the base: refused);
  - record t, verdict t − 10 s, `compiled_at` rewritten to t − 1 h → `VERDICT_STALE` (fails on the base: accepted);
  - verdict t − 1 s → fresh (allowance; guard);
  - 090 record (no `compiled_at`), verdict t − 1 h → fresh (guard);
  - `compile-critic-prompt` twice for an unchanged test → the record file's time moves forward (guard for FR-014).
- [X] T008 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/BatchDryRunTests.cs (F6; FR-017–FR-018; SC-005). Each row runs on two identical workspaces and compares a SHA-256 tree snapshot. Rows:
  - **`ingest-grounding --suite a --all --dry-run`:**
    - the JSON equals the real run's JSON except `dry_run: true`;
    - the exit code is the same;
    - the dry-run workspace is unchanged;
  - **the same with `--ids` and with `--repaired --repair-attempts 1 --ids`**;
  - **`ingest-verdict --suite a --all --dry-run`:** the JSON equals the real run's plus `dry_run: true`; the workspace is unchanged;
  - **`ingest-verdict --from F --dry-run`:** accepted, `dry_run: true`;
  - **`ingest-analysis --suite a --from analysis.json --dry-run`:**
    - the same JSON and exit code as the real run, plus `dry_run: true`;
    - no `.spectra/generation-plan/a.json`;
    - the human summary is on stderr in JSON mode;
  - **`ingest-grounding --suite a --test TC-101 --dry-run`:** exit 1, stderr `--dry-run requires --all`, workspace unchanged.
- [X] T009 [P] Write tests/Spectra.CLI.Tests/Skills/CriticModelTests.cs (F7; FR-020–FR-024; SC-006). Rows:
  - **init:**
    - the real init (with skills) writes `"critic": {"model": "sonnet"}` under `ai`;
    - the installed `.claude/agents/spectra-critic.agent.md` has `model: sonnet`;
  - **change the key:** set `ai.critic.model` to `opus`, run `update-skills` → the agent has `model: opus` and is listed under "Updated";
  - **values:** `inherit` and `claude-opus-4-6[1m]` are written as given;
  - **edited agent:** append a line to the agent, set `haiku`, run `update-skills` → the agent is byte-identical and listed under "Skipped (customized by user)";
  - **pre-097:** `PinnedCriticAgent()` plus a config without `ai.critic` → after `update-skills` the agent has `model: sonnet`;
  - **invalid model (analyze U1, U2):** for `"two words"`, `""`, `"a\"b"` and `"[1m]"` (starts with `[`):
    - `update-skills` exits 0;
    - one warning `spectra.config.json is invalid (CONFIG_INVALID: …); the critic agent's model was not changed`;
    - the critic agent keeps its installed model line;
    - the other skills and agents are refreshed as usual;
  - **non-string model (analyze A2):** `5` → the same outcome. Assert only `CONFIG_INVALID` plus the key path
    (`ai.critic.model` or `$.ai.critic.model`) in the warning;
  - **unrelated invalid config (U2):** `update.group_size: 0`, a duplicate top-level key, and malformed JSON → each
    gives the same warning, skills refreshed, critic model unchanged, exit 0;
  - **guard:** `ConfigLoader.LoadForWriteAsync` with `ai.critic.model: "[1m]"` → `CONFIG_INVALID`;
  - **retired keys:** a config with `ai.critic.provider` and `ai.critic.model: "opus"` → `spectra validate` shows the retired-key notice, and `update-skills` writes `model: opus`;
  - **083 guard:** a config without `ai`, or with `"ai": {"critic": null}` → defaults (`sonnet`), no error;
  - **dead code:** no type named `CriticFactory` or `CriticModelResolver` in the CLI assembly (reflection).
- [X] T010 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/EmptyIdsTests.cs (F5; FR-016). Rows:
  - `ingest-grounding --suite a --all --ids ""` and `--ids " , "` → the same `written` as without `--ids` (fails on the base: 0);
  - the same with `--repaired --repair-attempts 1` → exit 4 `IDS_REQUIRED` (guard);
  - `ingest-verdict --suite a --all --ids " "` → the same `results` as without `--ids` (fails on the base);
  - `ingest-grounding --suite a --test TC-101 --ids ""` → no `--ids is only valid with --all` usage error.
- [X] T011 Proof run. With `git diff --quiet 807047c -- src` holding, stage out the rows that use new members and run T003–T010. Every row except the stated guards fails for the stated reason. Record the counts and reasons in the Notes and in the scratchpad (`097/proof-807047c.txt`). Restore the staged rows.
- [X] T012 Inventory the existing rows affected and record "retarget" or "unchanged" for each in the Notes:
  - rows that list stray or other-suite verdicts in `AuditGroundingCommandTests`;
  - rows that rely on `compiled_at` as the reference in `VerdictOlderThanPromptTests`;
  - rows that parse `compile-repair-batch` stdout;
  - rows that assert the critic agent text or `claude-sonnet-4-6` (`CriticSubagentSkillTests`, `NoCopilotIsmsTests`, `SkillsManifestTests`, `AgentInstallLayoutTests`);
  - the init-template matrix (`ConfigRuleMatrixTests`, `InitTemplateWorkspaceTests`);
  - the tests of the classes removed in T033: `CriticFactoryTests`, `CriticAuthTests`, `CriticModelResolverTests`, Core `CriticConfigClampTests`, `CriticConfigTests` — removed with the code; record the count difference.

**Checkpoint**: rules proven against `807047c`.

---

## Phase 3: User Story 1 — grounding blocks only for confirmed tests (P1) 🎯 MVP

**Goal**: the skills name the tests they ground.
**Independent test**: T003 passes (T004 stays green).

- [X] T013 [US1] Edit src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md, Step 8a:
  - the `grounded` row adds the id to the kept-grounded list (the count stays the list's length);
  - the batch call becomes `spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json`, followed by one sentence: skip it when no test was kept as grounded;
  - the "Writes grounding blocks for all grounded tests" line names only the listed tests.

  The 075 guard blocks and the NON-STOP restatement are not touched.
- [X] T014 [US1] Edit src/Spectra.CLI/Skills/Content/Skills/spectra-update.md, Step 5a:
  - move the `ingest-grounding` call after the routing by `results`;
  - it becomes `spectra ai ingest-grounding --suite <suite> --all --ids <the grounded ids> --output-format json`, skipped when no test of the group is `grounded`;
  - an **unverified** test (no entry, or the critic's line disagrees) is never in the list.

  The repair lines (`--repaired … --ids`) are unchanged. T003 passes.

**Checkpoint**: a test reported unverified never gets a block from the skills' batch calls.

---

## Phase 4: User Story 2 — truthful, small audit (P1)

**Goal**: one suite, filters, stale verdicts flagged.
**Independent test**: T005 passes.

- [X] T015 [US2] In src/Spectra.CLI/Results/AuditGroundingResult.cs:
  - add `AuditGroundingEntry.Stale` (`bool?`, `stale`, null-ignored);
  - add `AuditGroundingSummary.Stale` (`int`, `stale`);
  - add `AuditGroundingResult.NotFound` (`IReadOnlyList<string>?`, `not_found`, null-ignored).
- [X] T016 [US2] (needs T034) In src/Spectra.CLI/Commands/Review/AuditGroundingCommand.cs:
  - add `--action` (`Option<string[]>`, repeatable, comma-split, case-insensitive) and `--ids` (`IdList`, empty = not given);
  - validate `--action` against `critic`, `repair`, `review`, `none` before any read: exit 1 with `--action must be one of critic, repair, review, none (got '<v>').`;
  - pass the filters to the handler.
- [X] T017 [US2] In src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs:
  - read verdict files only for ids in the suite index;
  - for a verdict-file entry whose test parses, run `VerdictFreshness.Check(currentDir, id, test, vf)`; stale → `Stale = true`, `ActionNeeded = "critic"`;
  - compute the summary (with `Stale`) over all entries, then apply `--action` / `--ids` to `tests` only;
  - `NotFound` holds the listed ids absent from the index;
  - human output: `critic (stale)`, `| N stale`, `<id>: not found in suite '<s>'`. In JSON mode the not-found and
    stale lines go to stderr and stdout stays the JSON document; the table stays human-mode only (FR-009a);
  - `EmitEmpty` gains `Stale = 0`.

  T005 passes (except its skill row).
- [X] T018 [US2] Edit the resume note in src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md (and its CLI-flags table row for `audit-grounding`): `spectra ai audit-grounding --suite {suite} --action repair --output-format json`. T005's skill row passes.

**Checkpoint**: the audit lists one suite, flags stale verdicts, and the resume path sees only repair ids.

---

## Phase 5: User Story 3 — no repair from a stale verdict (P1)

**Goal**: both repair commands check freshness.
**Independent test**: T006 passes.

- [X] T019 [US3] In src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs:
  - after the test parses and the verdict file is found, run `VerdictFreshness.Check(currentDir, testId, test, from is null ? verdictPath : null)`;
  - stale → `Console.Error.WriteLine($"{VerdictFreshness.ErrorVerdictStale}: {result.Message(testId)}")` and return `ExitRefused` (4).
- [X] T020 [US3] In src/Spectra.CLI/Commands/Generate/CompileRepairBatchCommand.cs:
  - check each partial test's freshness before compiling; skip a stale test;
  - after the loop, when any were stale, write one stderr JSON line `{"command":"compile-repair-batch","stale":[{"id","reason"}]}` and a human line per test (`<id>: stale verdict — run the critic again`);
  - stdout stays the array.

  T006 passes.

**Checkpoint**: stale verdicts never drive a repair.

---

## Phase 6: User Story 4 — one clock (P2)

**Goal**: freshness compares two file-system times.
**Independent test**: T007 passes.

- [X] T021 [US4] In src/Spectra.CLI/Verification/VerdictFreshness.cs:
  - when the record has `compiled_at` and the verdict is a file, the reference is `File.GetLastWriteTimeUtc(RecordPath(root, id))`, falling back to `compiled_at` on an IO error;
  - add `FreshnessResult.ReferenceAt`, and make the `OlderThanPrompt` message use it;
  - update the doc comments (091 → 097 rule).

  `WriteRecordAsync` is unchanged (temp + move already sets the file time on every compile): FR-014 needs no code
  change, and the T007 guard pins the existing behaviour. T007 passes; `VerdictOlderThanPromptTests` still pass or are retargeted per T012.

**Checkpoint**: clock skew no longer refuses new verdicts.

---

## Phase 7: User Story 5 — `--dry-run` on the batch ingests (P2)

**Goal**: the three batch ingests support `--dry-run` and write nothing.
**Independent test**: T008 passes.

- [X] T022 [US5] In src/Spectra.CLI/IO/GroundingWriteBackService.cs, add a `bool dryRun = false` parameter to `WriteAsync`: compute everything and return the same result, but skip `_writer.WriteAsync`.
- [X] T023 [US5] In src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs:
  - read `GlobalOptions.DryRunOption`;
  - `--test` with `--dry-run` → plain stderr `--dry-run requires --all`, exit 1 (after the existing argument checks, before any read);
  - pass `dryRun` to `RunBatchAsync` and on to `WriteAsync`;
  - add `dry_run: true` to the batch JSON when set; the human summary is unchanged.
- [X] T024 [US5] In src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs, read `GlobalOptions.DryRunOption` and add `dry_run: true` to the JSON of both forms when set (the command writes nothing either way).
- [X] T025 [US5] In src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs, read `GlobalOptions.DryRunOption`:
  - skip `GenerationPlanStore.SaveAsync` when set;
  - add `dry_run: true` to the success JSON;
  - everything else, including refusals and the summary, is unchanged.

  T008 passes.
- [X] T026 [P] [US5] In specs/091-review-fixes-090/plan.md and specs/094-generation-batch-prompts/plan.md:
  - mark the constitution IV row "resolved by 097";
  - remove the Complexity Tracking rows for `ingest-grounding --all` / `ingest-verdict --all` and `ingest-analysis` (FR-019).

**Checkpoint**: constitution IV satisfied for the three commands.

---

## Phase 8: User Story 6 — the critic model (P2)

**Goal**: `ai.critic.model` selects the critic's model; dead code removed.
**Independent test**: T009 passes.

- [X] T027 [US6] In src/Spectra.Core/Models/Config/CriticConfig.cs, trim the class to `Model` (`model`) plus `public const string DefaultModel = "sonnet"`. In src/Spectra.Core/Models/Config/AiConfig.cs:
  - add `[JsonPropertyName("critic")] public CriticConfig? Critic { get; init; }`;
  - add `[JsonIgnore] public string EffectiveCriticModel => Critic?.Model ?? CriticConfig.DefaultModel`;
  - update the Spec 069 comment.
- [X] T028 [US6] In src/Spectra.Core/Config/ConfigLoader.cs (`Validate`), when `Ai?.Critic?.Model` is not null and doesn't match `^[A-Za-z0-9][A-Za-z0-9._:\[\]-]{0,199}$` (analyze U1), add `INVALID_CRITIC_MODEL`: `ai.critic.model must be one word (an alias such as sonnet, a model id, or inherit)`. It surfaces as `CONFIG_INVALID`. Keep `DeprecatedKeyPaths` unchanged.
- [X] T029 [P] [US6] In src/Spectra.CLI/Templates/spectra.config.json:
  - add `"critic": { "model": "sonnet" }` under `ai`;
  - change `_comment_ai`: the critic runs as the `spectra-critic` subagent on `ai.critic.model` (an alias, a model id or `inherit`); rerun `spectra update-skills` after changing it.
- [X] T030 [US6] In src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md, set `model: {{CRITIC_MODEL}}`. In src/Spectra.CLI/Skills/AgentContent.cs:
  - add `public static string Render(string name, string content, string criticModel)`, which replaces `{{CRITIC_MODEL}}` in the critic agent only;
  - add `public static IEnumerable<(string Name, string Content)> Rendered(string criticModel)`;
  - make sure `SkillResourceLoader` leaves the placeholder alone.
- [X] T031 [US6] In src/Spectra.CLI/Commands/Init/InitHandler.cs, after writing the config, read `EffectiveCriticModel` through `ConfigLoader.LoadForReadAsync(_workingDirectory, ConfigSections.None, ct)` (default on failure: init just wrote the template). Install the agents from `AgentContent.Rendered(model)` and record the rendered hash.
- [X] T032 [US6] In src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs:
  - load the config with `LoadForReadAsync(currentDir, ConfigSections.None, ct)` only for the critic model (FR-022a):
    - no config → `sonnet`;
    - valid → `config.Ai.EffectiveCriticModel`;
    - invalid for any reason → one warning `spectra.config.json is invalid ({ErrorCode}: {first error}); the critic
      agent's model was not changed`. The model is read from the installed critic agent's frontmatter `model:` line
      (`sonnet` when the agent is missing or has no such line);
  - never return a failure because of the config;
  - process the agents from `AgentContent.Rendered(model)` (096 rule unchanged: an edited agent is skipped).

  T009 passes.
- [X] T033 [US6] Delete src/Spectra.CLI/Agent/Critic/CriticFactory.cs and src/Spectra.CLI/Agent/Critic/CriticModelResolver.cs (FR-023). Delete their test classes:
  - tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryTests.cs and CriticAuthTests.cs;
  - tests/Spectra.CLI.Tests/Agent/CriticModelResolverTests.cs;
  - tests/Spectra.Core.Tests/Config/CriticConfigClampTests.cs and tests/Spectra.Core.Tests/Models/Config/CriticConfigTests.cs.

  If a CriticConfig test covers `model` deserialisation, keep that row (moved to CriticModelTests). Build with no new warnings.

**Checkpoint**: the critic runs on the configured model; edited agents are never overwritten.

---

## Phase 9: User Story 7 — empty `--ids` (P3)

**Goal**: an empty id list counts as not given.
**Independent test**: T010 passes.

- [X] T034 [US7] In src/Spectra.CLI/Commands/Generate/IdList.cs, add `IsGiven(string? value) => Parse(value).Count > 0`. Use it for `idsGiven` in src/Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs and src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs, and in the audit command (T016). The `IDS_REQUIRED` check is unchanged. T010 passes.

**Checkpoint**: no silent "nothing processed".

---

## Phase 10: Polish, docs, verification

- [X] T035 [P] Update docs/cli-reference.md:
  - `audit-grounding`: `--action`, `--ids`, `stale`, suite scope;
  - `compile-repair-prompt` / `compile-repair-batch`: stale refusal, the stderr `stale` line;
  - `--dry-run` on `ingest-grounding --all`, `ingest-verdict` and `ingest-analysis`;
  - the empty `--ids` rule;
  - the 097 freshness clock;
  - `ai.critic.model`: fix the stale "default `claude-sonnet-4-6`" line.
- [X] T036 [P] Update docs/usage.md (resume via `--action repair`; changing the critic model and rerunning `update-skills`) and docs/configuration.md (`ai.critic.model`).
- [X] T037 [P] Update CHANGELOG.md under `[2.6.0]` with "Fixed (Spec 097 — verification hardening, Phase 6 part 2)" (F1–F7). Add Notes entries:
  - the audit no longer lists other suites or stray verdicts;
  - `update-skills` reads the config for the critic model; an invalid config only warns, keeps the critic's model line,
    and still refreshes everything else (analyze U2);
  - a pinned critic agent moves to `sonnet`;
  - `--dry-run` stops writing.
- [X] T038 [P] Update CLAUDE.md: fix the Tech Stack sentence about `ai.critic.model`, and add a short "Recent Changes" entry for 097 with the counts from T039.
- [X] T039 Run the full `dotnet build --no-incremental` (no new warnings) and `dotnet test`. Expect 0 failures. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts (Core and CLI drop by the removed dead-code tests; see T012). Check that `git diff --stat 807047c -- src` touches only the files in plan.md.
- [X] T040 Run the quickstart CLI checks 1–8 with the built CLI on a real-init workspace and a pre-097 workspace, and record the results in the Notes. The pre-097 workspace has:
  - 090 and 091 records;
  - verdict files of two suites and a stray verdict;
  - the pinned critic agent;
  - no `ai.critic`.
- [X] T041 Fill in the Notes:
  - counts;
  - the T011 proof;
  - the T012 inventory and the changed assertions;
  - deviations.

  Check that no touched file has mixed CRLF/LF. Mark every task [X] and stop. Ask the user before committing as `fix(097): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001, T002) → **Foundational** (T003–T012). T011 comes before any source edit.
- **Stories** after T011–T012, in any order.
  - US2's `--ids` uses T034 (US7), so do T034 first or inline the rule.
  - US1 (T013, T014) and US2's T018 both edit `spectra-generate.md`, so run them one after the other.
  - T023 and T034 both edit `IngestGroundingCommand.cs`; T024 and T034 both edit `IngestVerdictCommand.cs`. Run them one after the other.
- **Polish:** T035–T038 after the stories; T039–T041 last.

### Parallel opportunities

```text
Phase 2:  T003 T004 T005 T006 T007 T008 T009 T010   (different test files)
US5/US6:  T026 (old plan rows) ‖ T029 (template) ‖ T027–T028 (Core config)
Polish:   T035 cli-reference  T036 usage + configuration  T037 CHANGELOG  T038 CLAUDE.md
```

## Implementation Strategy

- **MVP:** US1 + US2 + US3 (P1). The grounding claim and the audit are truthful, and no stale repair happens.
- **Then:** US4 (one clock), US5 (dry run), US6 (critic model), US7 (empty ids).
- **Finish:** run the full suite and the quickstart on real-init and pre-097 workspaces.

## Notes

**Baseline (T001, `807047c`).** Core 1245 / CLI 2228 / Execution 228 / Integration 8, 0 failures; 4 warnings
(`DocsApplyMoveCommand`/`DocsRetireCommand` CS0108, `CriteriaLayoutMigrator` CS8714/CS8621).

**Proof (T011, `src` untouched at `807047c`).** 56 rows in 8 classes ran: 46 failed, each for the intended reason, and
the 10 stated guards passed. No row needed staging, because the rows use only APIs that exist on the base. Output:
scratchpad `097/proof-807047c.txt`.
- **Skill text rows:** the call lines are not found.
- **Audit:** other suites and the stray TC-999 are listed; `--action` / `--ids` are "Unrecognized"; there is no
  `stale` key.
- **Repair commands:** they compile a prompt from a stale verdict (exit 0).
- **Clock:** with `compiled_at` +1 h, a new verdict is refused; with −1 h, an old one is accepted.
- **Empty `--ids`:** 0 written, and a usage error without `--all`.
- **Dry run:** files are written, there is no `dry_run` key, and `--test` accepts `--dry-run`.
- **Critic model:**
  - the agent is pinned to `claude-sonnet-4-6`;
  - the key is ignored and the config has no `ai.critic`;
  - `update-skills` doesn't read the config (no warning);
  - `LoadForWriteAsync` accepts `"[1m]"`;
  - `CriticFactory` exists.
- **Guards that passed:** the 3 verbatim guard texts, GroundingIdsBatch, the 090 record, the allowance, the record file
  time moving, the no-record repair prompt, `IDS_REQUIRED` with empty ids, and the edited critic agent.

**T012 inventory and changed assertions.**
- **Retargeted (deliberate, commented "Spec 097"):**
  - `NoCopilotIsmsTests.CriticSubagent_IsExempt_KeepsModelPinAndExplicitOnly`: the bundle has `model: {{CRITIC_MODEL}}`,
    rendered `model: sonnet`.
  - `SkillsManifestTests.UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair`: the 5a grounding line now has
    `--ids <grounded ids>`.
- **Removed with the dead code (FR-023), 41 rows:**
  - CLI 26: `CriticFactoryTests`, `CriticAuthTests`, `CriticModelResolverTests`, and
    `VerificationIntegrationTests.VerificationPipeline_CriticFactory_ValidatesProviders`;
  - Core 15: `CriticConfigClampTests`, `CriticConfigTests`.
- **Unchanged and green:**
  - `AuditGroundingCommandTests` and `AuditGroundingUnverifiedTests` (no row relied on stray verdicts);
  - `VerdictOlderThanPromptTests` (the record file time is within milliseconds of `compiled_at`);
  - `CompileRepairBatchCommandTests`, `CompileRepairPromptCommandTests`, `IngestGroundingCommandTests`,
    `IngestVerdict*`, `EditDropsVerdictTests`, `GenerateOrchestratorTests`;
  - `CriticSubagentSkillTests`, `AgentInstallLayoutTests`;
  - `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` (the init template gains `ai.critic`).
- `GenerateOrchestratorTests.GenerateSkill_ResumesFromTheGroundingAudit` pins "`action_needed: repair`". The resume
  note keeps that phrase next to the new `--action repair` call, so the row is unchanged.

**Counts (T039).** `dotnet build --no-incremental`: the same 4 warnings, 0 errors. Core 1230 (1245 − 15 removed) / CLI
2258 (2228 + 56 new − 26 removed) / Execution 228 / Integration 8 — 0 failures. `git diff --stat 807047c -- src`: the
files in plan.md, plus the new `DryRunJson.cs` and the deleted `ICriticRuntime.cs`.

**Quickstart (T040).** The scratchpad `097/quickstart.py` runs 21 checks against the built CLI; all 21 pass. They cover
checks 1–7 on:
- a pre-097-shaped workspace: suites `a` and `b`, a 090 record (no `compiled_at`) and 091 records, a stray verdict, and
  a stale verdict;
- real-init workspaces, with and without skills;
- a pre-097 critic agent: pinned `claude-sonnet-4-6`, hash recorded, no `ai.critic`, so it moves to `sonnet`.

Check 8 is the full run above.

**Deviations.**
- **`ICriticRuntime` removed too.** It was referenced only by `CriticFactory`, so it became dead code (FR-023 spirit,
  noted in CHANGELOG).
- **`compile-repair-batch` stale output.** The `stale` list goes to stderr as one JSON line; stdout stays the array
  (R3).
- **`ingest-verdict --dry-run`.** It is accepted in both forms, because the command never writes (spec amended at plan
  time, R6).
- **`update-skills` warning text.** It strips the loader's own `spectra.config.json is invalid: ` prefix, so the warning
  reads `spectra.config.json is invalid (CONFIG_INVALID: <reason>); …` without the phrase twice.
- **`ai.critic.model` regex.** It ends in `\z`, not `$`, because .NET's `$` also matches before a trailing newline.
- **Analyze I3–I6.** These wording fixes (FR-022 qualifier, data-model, research R2, checklist note) were applied
  during implementation.

**Flaky row (pre-existing, not 097).** In one full run under load (9 m 46 s), `CriteriaStoreLockTests.WaitingWriter_ProceedsAfterRelease` failed
(a 150 ms timing assertion on the criteria lock, untouched by 097). It passed 5/5 alone and in the next full run
(Core 1230 / CLI 2258 / Execution 228 / Integration 8, 0 failures). Candidate for 098.

**Line endings.** No touched file has mixed CRLF/LF. Edited files keep their working-copy form, and new files are LF.
