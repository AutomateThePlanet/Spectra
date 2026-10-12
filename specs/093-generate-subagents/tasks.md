---
description: "Task list for Spec 093 — generate skill as orchestrator (Phase 5b)"
---

# Tasks: Generate Skill as Orchestrator (Phase 5b)

**Input**: Design documents from `/specs/093-generate-subagents/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (spec T1–T5). Each new guard must fail on `20ac624`.
- Guards are written in Phase 2 and run on the untouched sources (`git diff --quiet 20ac624 -- src` must hold) before
  any source edit.
- CLI rows use `VerdictWorkspace` / `BaselineTestWorkspace` from the real `spectra init`, in the `CriteriaCommands`
  collection; install rows use the real `InitHandler` (with skills) and `spectra update-skills`.
- A pre-093 workspace is a real init with `spectra-analyst.agent.md`, `spectra-generator.agent.md` and their manifest
  entries removed, and a user-modified `.claude/skills/spectra-generate/SKILL.md`.

**Organization**: by user story.
- US1 = generation orchestration (P1);
- US2 = routing by the CLI's per-test verdicts, incl. the CLI addition (P1);
- US3 = no written test loses its verification (P2);
- US4 = the user still sees everything (P2; relay verification);
- US5 = install of the two agents (P3).

Because US1's 8a routing needs US2's CLI change, US2 runs first (see Dependencies).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `093-generate-subagents` (= `20ac624`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 2068 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.

---

## Phase 2: Foundational — guard tests and the proof against `20ac624`

- [X] T002 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/VerdictResultsTests.cs (spec T1; FR-016–FR-018), real-init workspace, suite `storage`, TC-101–TC-103. Rows:
  - **results:** compile + write verdicts grounded (TC-101), partial (TC-102), hallucinated (TC-103) → `ingest-verdict --suite storage --all` → `results` has three entries `{id, verdict, score, gate}` with `gate` `pass` / `pass` / `drop`; `grounded`/`partial`/`hallucinated` counts as before;
  - **--ids:** `--ids TC-102,TC-999` → `results` only TC-102; `not_found` == ["TC-999"]; counts only for TC-102;
  - **stale / unreadable:** a stale verdict and a `{not json` verdict → no `results` entry; listed under `stale` / counted in `errors` as before;
  - **--ids without --all** → exit 1, stderr exactly `--ids is only valid with --all.`, stdout empty;
  - **single form** (`--from`) output unchanged (guard).
- [X] T003 [P] Extend tests/Spectra.CLI.Tests/Skills/AgentInstallLayoutTests.cs (spec T2; FR-023): the real init installs `spectra-analyst.agent.md` and `spectra-generator.agent.md` too (six agents under `.claude/agents/`, none under `.github/agents/`); a pre-093 workspace → `update-skills` adds the two, skips a modified `.claude/skills/spectra-generate/SKILL.md`.
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Skills/GenerateAgentTests.cs (spec T3; FR-001–FR-005, FR-013). Per new agent (`spectra-analyst.agent.md`, `spectra-generator.agent.md`): frontmatter (`name`, `tools: [Bash, Read, Write]`, `model: inherit`, `disable-model-invocation: true`, `context: fork`); body: never ask the user, never start a subagent, never edit a test's `.md` file, NON-STOP, `stop: <`, `## Your final message`, nothing else. Then:
  - **analyst:** `spectra ai compile-analysis-prompt --suite <suite> --doc-suite <doc_suite>`, `.spectra/analysis.json`, `spectra ai ingest-analysis --suite <suite> --from .spectra/analysis.json`, "at most 2", the lines `already_covered:`, `recommended:`, `documents_analyzed:`, `breakdown:`, `techniques:`, `boundary_gap:`, `batch <n> | count <k> | focus <`, "at most 8 behaviours", "add up to `recommended`", `refused:` and `refusal:`;
  - **generator:** `spectra ai compile-prompt --suite <suite> --count <count> --focus`, `.spectra/generated.json`, `spectra ai ingest-tests <suite> --from .spectra/generated.json`, "at most 2", `<id> | <title>`, `baseline:`, `warning:`, `failed <error_code>: <first error>`; no `compile-critic-prompt`;
  - **repairer (FR-013):** one line per entry in `results` "including leftover" `repaired-` files, attributed by id.
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Skills/GenerateOrchestratorTests.cs (spec T4, T5; FR-006–FR-012, FR-014, FR-015). Rows:
  - **generate, context budget contract** before the main flow (docs, compiled prompts, test files, generated / staged JSON, verdict files; the allowed calls);
  - **generate, no work in main:** no `spectra ai compile-analysis-prompt`, `spectra ai compile-prompt --suite {suite} --count`, `spectra ai compile-repair-batch`, `spectra ai ingest-tests`, `spectra ai ingest-analysis` command in the main generation flow (the from-description section excluded); no `Read .spectra/verdicts`; the reference table has no "Read `test-cases/{suite}/{id}.md` directly";
  - **generate, delegation:** Task `spectra-analyst`, Task `spectra-generator` per batch with `focus` and `count`, Task `spectra-repairer`;
  - **generate, routing:** `spectra ai ingest-verdict --suite {suite} --all --ids` and "`results`" decide kept / repair / drop; the critic's line is "progress only"; a disagreement or no result → unverified;
  - **generate, guards verbatim:** the NON-STOP contract heading and its four prohibitions; `- **If \`written == 0\` AND \`kept-grounded > 0\`:** STOP immediately. Do NOT proceed to 8b.`; `GROUNDING-INGEST ANOMALY`;
  - **generate, resume:** `audit-grounding` and `action_needed: repair`; no `compile-repair-batch` in the resume note;
  - **generate, report:** every test by id and title with its outcome;
  - **update Step 5a (FR-015):** `spectra ai ingest-verdict --suite <suite> --all --ids`, routing by `results`, disagreement → unverified; the critic's line is progress only;
  - **update and generate (FR-014):** every `written` line (a leftover, "edit again", a re-edit after "still hallucinated") is re-verified or listed unverified;
  - **generate, unverified (C1):** the sentence "Regenerate that single test" is gone; the retry-once-then-unverified rule and the Step 9 heading "Unverified (critic could not judge)" are present;
  - **generate, drops (U1):** a drop requires the CLI's `hallucinated` and an agreeing (or missing) critic line;
  - **from-description (I2):** its Step 4 still points to the Step 8 critic flow;
  - (guard) the from-description section still compiles `--from-description` in the main session.
- [X] T006 Proof run: with `git diff --quiet 20ac624 -- src` holding, run T002–T005. Expect every row except the stated guards to fail for the stated reason. Save the output to the session scratchpad (`093/proof-20ac624.txt`) and summarise it in the Notes.
- [X] T007 Inventory the existing rows that pin moved generate text and record per row "retarget to <agent>" / "stays on the skill" in the Notes: `RepairIngests_KeepTheBaseline` (the `spectra-generate` row), `GenerateSkill_StagesRepairsInTheRepairsFolder_NeverTheUpdateFolder`, `GenerateSkill_Exit8_RetriesTheFailedRepairsAtMostTwice_AsTheSameRepairAttempt`, `GenerateSkill_NonStopRestatement_SaysExit8IsNotAStop_AndTheWrittenZeroGuardIsUnchanged` (the exit-8 half), `BothSkills_KeepAFailedFileAsFailedId_AndRetryWithAFreshFile` (generate half), `GenerationSkillContractTests.GenerationSkill_DrivesFailLoudBoundedLoop`, `GenerationSkill_GatesOnVerdict_DropOnHallucinated`, plus any row the US1 run surfaces; `GenerateSkill_RepairedGrounding_PassesOnlyTheReCritiquedIds` and `GenerateSkill_SkippedHallucinated_IsNotAFailure` stay on the skill; `AgentContent_HasAllAgents` 4 → 6.

**Checkpoint**: guards exist and are proven to fail on `20ac624`.

---

## Phase 3: User Story 2 — the CLI-classified verdict decides the route (P1)

**Goal**: `ingest-verdict --all` returns per-test results and takes `--ids` (FR-016–FR-018); the update skill routes by them (FR-015, update half).

**Independent test**: T002 green; T005's update Step 5a routing row green.

- [X] T008 [US2] Create src/Spectra.CLI/Commands/Generate/IdList.cs: `Parse(string?)` (split on `,`, trim, drop empties, distinct `OrdinalIgnoreCase`) and the `IdsWithoutAllMessage` constant (`--ids is only valid with --all.`); make `IngestGroundingCommand` use it (its `ParseIds` and message constant delegate or are replaced; behaviour unchanged).
- [X] T009 [US2] In src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs: add `--ids`; checks in order (existing `--from` with `--all`; `--ids` without `--all` → exit 1 with the message, before anything is read); batch mode filters to the listed ids, computes `not_found` right after the index read (also on the no-verdict-folder path), collects `results` per classified file (`verdict` lowercase, `score`, `gate` `drop` iff hallucinated); `EmitBatchResult` adds `results` and `not_found`; the human summary (counts line, one line per result, one per unknown id) goes to stdout in human mode and stderr in JSON mode. Single `--from` mode untouched.
- [X] T010 [US2] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md Step 5a: after the critics, run `spectra ai ingest-verdict --suite <suite> --all --ids <ids> --output-format json` and route each test by its `results` entry (grounded → done; partial / hallucinated → the repairer; no entry, or a critic line that disagrees → unverified, reported); the critic's final line is progress only; the same after the repair's re-critic.
- [X] T011 [US2] Check every test that captures `ingest-verdict --all` stderr (`VerdictWorkspace.IngestVerdictAllAsync` callers, `IngestVerdict*Tests`) for an empty-stderr assumption; adapt and list any in the Notes. Run VerdictResultsTests, IngestVerdict*Tests, IngestGroundingCommandTests, RepairedIdsTests, VerdictOlderThanPromptTests, KeptHallucinatedTests: green.

**Checkpoint**: US2 green.

---

## Phase 4: User Story 1 — a large generation run without running out of room (P1) 🎯 MVP

**Goal**: the generation skill orchestrates the analyst, the generator and the repairer (FR-001–FR-012).

**Independent test**: T004's analyst / generator rows and T005's generate rows green.

- [X] T012 [P] [US1] Create src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md per contracts.md and data-model.md (research R2): frontmatter; input; the moved Steps 1–3 (compile with `--doc-suite` and `--focus`, the exit-4 pre-flight refusal → `refused:` + `refusal:` lines verbatim and stop; analyse; Write `.spectra/analysis.json`; ingest; exit 5/6 → at most 2 retries); the batch rule (group by source doc, at most 8 behaviours, proportional counts that add up to `recommended`, largest remainder, no zero batches, focus `<doc>: <titles>`); the return contract.
- [X] T013 [P] [US1] Create src/Spectra.CLI/Skills/Content/Agents/spectra-generator.agent.md (research R3): frontmatter; input; the moved Steps 5–7 (compile; generate reading `docs/` in its own context — all the generation rules the skill gives today: the profile, the ISTQB techniques, `--focus`, grounding in the docs; Write `.spectra/generated.json`; ingest; exit 5/6 → regenerate for the specific error, at most 2 retries; exit 1 → `failed` after any persisted ids); the return contract (`<id> | <title>`, `baseline:`, `warning:`).
- [X] T014 [US1] In src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md (FR-013): the final message has one line per entry in `results` over its ingest calls, including leftover `repaired-<id>.json` files, attributed by id (a retry replaces the earlier line), plus a `failed` line for an input id whose compile was refused.
- [X] T015 [US1] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md: the overview shows the delegation; add `## Context budget contract` (research R5); the reference table: drop "Test file content: Read `test-cases/{suite}/{id}.md` directly …", mark `compile-analysis-prompt`, `compile-prompt --count`, `ingest-analysis`, `ingest-tests`, `ingest-update --all --keep-baseline` as run by the named agent, mark `compile-repair-batch` as not used by this skill, add `--ids` / `results` to the `ingest-verdict --all` row. Keep the NON-STOP contract and the pre-flight section text (the pre-flight refusal now arrives as the analyst's `refused:` lines; say so in one sentence).
- [X] T016 [US1] In spectra-generate.md Steps 1–4: Task `spectra-analyst` with `suite`, `doc_suite`, `focus`; a `refused:` answer → show the `refusal:` lines and ask the user (narrow / raise), as today; re-running the analysis with another doc suite (from the refusal's candidates) or after the user raises `ai.analysis.max_prompt_tokens` means invoking `spectra-analyst` again with the new input — the main session never runs `compile-analysis-prompt` itself (add one sentence saying so to the pre-flight section, otherwise unchanged); Step 4 presents the recommendation from the returned lines (already covered, recommended, breakdown, techniques, boundary gaps, the batch plan) and STOPs for approval; `recommended: 0` → nothing to generate.
- [X] T017 [US1] In spectra-generate.md Steps 5–7: per batch, in order (one at a time), the progress write and Task `spectra-generator` with `suite`, `focus: "<user focus>; <batch focus>"` and `count`; an explicit user count is split over the batches (proportional, largest remainder); report every returned line; collect ids and titles; a `failed` batch is reported and the next runs; never generate in the main session.
- [X] T018 [US1] In spectra-generate.md Step 8a / post-8a: the critic per created id (Task; its line is progress only); then `spectra ai ingest-verdict --suite {suite} --all --ids <created ids> --output-format json`; route by `results`: grounded → kept-grounded, partial → repair list, hallucinated → the existing `record-drop` + `spectra delete` block, **only** when the critic's line agrees or there is no line (a disagreement → kept unverified and reported, never dropped; later grounding batches list it under `skipped_hallucinated` (091), which is expected and not a failure); remove 8a step 5 ("Regenerate that single test … Never keep an unverified test.") and replace it with research R5a: a test with no CLI verdict gets the critic once more, then — still no verdict — is kept **unverified** (never kept-grounded, no grounding block, never regenerated or deleted) and listed in Step 9 under "Unverified (critic could not judge)" with id, title, reason and the next step; then the existing `ingest-grounding --all` call and the written==0 guard **verbatim**, with kept-grounded = the number of `grounded` results.
- [X] T019 [US1] In spectra-generate.md Step 8b: Task `spectra-repairer` with `suite` and the partial ids (no `compile-repair-batch`, no manifest); landed ids (`written` / `unchanged`) → critic per id → `ingest-verdict --suite {suite} --all --ids <landed> --output-format json` → hallucinated dropped → the existing `--repaired --repair-attempts 1 --ids {ids}` call with its `{ids}` paragraph (091 wording kept: written, unchanged, dropped); `failed` → reported, unverified; the resume note: `spectra ai audit-grounding --suite {suite}` → ids with `action_needed: repair` → the repairer.
- [X] T020 [US1] In spectra-generate.md Step 9: the report from the returned lines and CLI results — every created test by id and title with its outcome (kept, repaired, flagged, dropped, failed, unverified), every warning and baseline line; a separate heading "Unverified (critic could not judge)" with id, title, reason and the next step (R5a).
- [X] T021 [US1] Retarget the rows inventoried in T007 in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs and GenerationSkillContractTests.cs (work now in an agent → that agent's content, same strings, whitespace-normalised; main-session rows stay); record each in the Notes.
- [X] T022 [US1] Run GenerateAgentTests, GenerateOrchestratorTests (generate rows), SkillsManifestTests, GenerationSkillContractTests, GenerateSkillContentTests, OrchestratorAgentTests, NoCopilotIsmsTests, TerminologyAuditTests: green.

**Checkpoint**: US1 green.

---

## Phase 5: User Story 3 — no written test loses its verification (P2)

**Goal**: FR-013 (done in T014) and FR-014 in both skills.

**Independent test**: T005's FR-014 rows and T004's repairer row green.

- [X] T023 [US3] In spectra-update.md Step 5 / 5a: the re-verify set is every `written` line (not `manual verdict kept`) received during the suite's run — a group's, a leftover attributed by id, an "edit again", a re-edit after "still hallucinated" — each re-verified when it arrives, or listed unverified if the user declined re-verification.
- [X] T024 [US3] In spectra-generate.md 8b: a `written` line for an id that wasn't in the repair list (a leftover) goes through the same re-critic path or is listed unverified.
- [X] T025 [US3] Run GenerateOrchestratorTests, GenerateAgentTests, OrchestratorSkillTests, SkillsManifestTests: green.

**Checkpoint**: US3 green.

---

## Phase 6: User Story 4 — the user still sees everything (P2)

**Goal**: FR-022.

- [X] T026 [US4] Relay inventory (Notes): what a generation run showed before 093 (the analysis recommendation incl. boundary gaps and the pre-flight refusal; `ingest-tests` ids, baseline line and warnings; the verdict counts; grounding results; repair outcomes; drops; the report) → the line or main-run command that carries each now and the skill sentence that reports it. Fix any gap and add a T004/T005 assertion for it.

---

## Phase 7: User Story 5 — install (P3)

**Goal**: FR-023 (the 092 rule installs the new files; only the count and the pre-093 row change).

- [X] T027 [US5] In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs `AgentContent_HasAllAgents`: six agents (deliberate change 4 → 6); run AgentInstallLayoutTests, ClaudeSkillsInstallTests, InitCommandTests: green.

---

## Phase 8: Polish, docs, verification

- [X] T028 [P] Update docs/usage.md: generation runs analysis, each batch and repairs in subagents; the batch plan in the recommendation; the report; six agents installed.
- [X] T029 [P] Update docs/cli-reference.md: `ingest-verdict --all` `--ids`, `results`, `not_found`, the human summary on stderr in JSON mode, `--ids` without `--all`; the `init` agent count (6).
- [X] T030 [P] Update CHANGELOG.md with "Changed (Spec 093 — generate subagents, Phase 5b)" and "Fixed (092 review findings)" under `[2.6.0]` before `### Notes`, and Notes entries (run `update-skills`; a customised generate skill keeps the old flow; `ingest-verdict --all` summary on stderr in JSON mode).
- [X] T031 [P] Update CLAUDE.md: a short "Recent Changes" entry for 093 with the counts from T032.
- [X] T032 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 2068 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts. Check `git diff --stat 20ac624 -- src` touches only `IngestVerdictCommand.cs`, `IngestGroundingCommand.cs`, `IdList.cs` and `Skills/Content/`.
- [X] T033 Run quickstart "With the built CLI" steps 1–2 (copy the bin) and record the results in the Notes.
- [X] T034 Write specs/093-generate-subagents/acceptance/setup-workspace.sh: a scratch workspace from the real `spectra init` with docs for ≥ 24 tests in ≥ 3 batches (three docs with several sections of rules, numbers and limits), indexed (`spectra docs index`), ready for "generate tests for <area>". Run it once with the built CLI and record the doc / suite listing in the Notes. Point quickstart.md at it.
- [X] T035 Fill in the Notes (counts, the T006 proof, the T007 inventory outcome, the T026 relay inventory, deviations, changed assertions), check line endings of every touched file (no mixed CRLF/LF), mark every task [X], and stop. Ask the user before committing as `feat(093): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push. The manual acceptance (SC-001–SC-003) is the user's run.

---

## Dependencies & Execution Order

- **Setup** (T001) → **Foundational** (T002–T007). T006 before any source edit.
- **US2** (T008–T011) first: US1's 8a / 8b routing uses `--ids` and `results`. T008 → T009 → T010 → T011.
- **US1** (T012–T022): T012, T013, T014 in parallel; T015 → T016 → T017 → T018 → T019 → T020 (one skill file); T021 after them; T022 last.
- **US3** (T023–T025): after US1 (T024 edits the generate skill; T023 edits the update skill after T010).
- **US4** (T026): after US1–US3.
- **US5** (T027): any time after T007 (edits `SkillsManifestTests.cs`; do it after T021).
- **Polish:** T028–T031 after the stories; T032–T035 last.

## Parallel Examples

```text
# Guards (different files):
T002 VerdictResultsTests   T003 AgentInstallLayoutTests   T004 GenerateAgentTests   T005 GenerateOrchestratorTests
# Agents:
T012 spectra-analyst   T013 spectra-generator   T014 spectra-repairer
# Docs:
T028 usage   T029 cli-reference   T030 CHANGELOG   T031 CLAUDE.md
```

## Implementation Strategy

1. **US2 first** (small CLI change): the CLI-classified verdicts both skills route by.
2. **MVP = US1**: the generation run as an orchestrator.
3. **US3**: every written test re-verified or reported.
4. **US4, US5**: relay verification, install count.
5. **Polish**: docs, full run, built-CLI checks, acceptance script; commit and merge on approval; the user runs the
   manual acceptance.

## Notes

- **Baseline (T001):** `20ac624` — Core 1219 / CLI 2068 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 0 errors, the 4 known warnings.
- **Proof (T006):** with `git diff --quiet 20ac624 -- src` holding, every new row failed for the stated reason (no
  `results` / `--ids`; the two agents not bundled; the repairer reported per input id; the main session did the work
  and read verdict files). Guards passed: the single `--from` form, the NON-STOP contract and written==0 guard
  verbatim, the from-description flow, and the 092 install rows. Output kept in the session scratchpad
  (`093/proof-20ac624.txt`).
- **Final run (T032):** `dotnet build --no-incremental`: 0 errors, the same 4 warnings. `dotnet test`: Core 1219 / CLI
  2092 (only `ProfileFormatLoaderTests` fails; +24) / Execution 228 / Integration 8. `ConfigRuleMatrixTests` and
  `InitTemplateWorkspaceTests` green. Scope: `git diff --stat 20ac624 -- src` = `IngestVerdictCommand.cs`,
  `IngestGroundingCommand.cs` (now uses `IdList`), the repairer agent, the generate and update skills; new:
  `IdList.cs`, the analyst and generator agents.
- **T011:** no test assumed an empty stderr from `ingest-verdict --all` (all 82 verdict / grounding / repair rows passed
  unchanged after the stderr summary was added).
- **T007 inventory (outcome):**
  - retargeted to `spectra-repairer`: `BothSkills_KeepAFailedFileAsFailedId_AndRetryWithAFreshFile` (generate half),
    `GenerateSkill_Exit8_RetriesTheFailedRepairsAtMostTwice_AsTheSameRepairAttempt` (the retry; the 8b sentence and
    the `written` / `unchanged` rule stay on the skill);
  - retargeted to `spectra-generator`: the "fail-loud", "retry limit", "regenerate" half of
    `GenerationSkillContractTests.GenerationSkill_DrivesFailLoudBoundedLoop` (the `ingest-verdict` /
    `compile-critic-prompt` half stays on the skill);
  - `RepairIngests_KeepTheBaseline`: the `spectra-generate` InlineData dropped (the skill no longer runs
    `ingest-update`; the repairer's `--keep-baseline` is asserted elsewhere);
  - unchanged and still green: `GenerateSkill_NonStopRestatement_…` (the restated NON-STOP paragraph incl. the exit-8
    sentence is kept verbatim), `GenerateSkill_StagesRepairsInTheRepairsFolder_…` (the table row still names the
    repairs folder), `GenerateSkill_RepairedGrounding_PassesOnlyTheReCritiquedIds`, `GenerateSkill_SkippedHallucinated_IsNotAFailure`,
    `GenerationSkill_GatesOnVerdict_DropOnHallucinated`, the from-description rows;
  - `AgentContent_HasAllAgents` 4 → 6; `UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair` now pins
    `ingest-verdict --suite <suite> --all --ids <id1>,<id2>` (FR-015).
- **Relay inventory (T026):** analysis recommendation + boundary gaps + pre-flight refusal → analyst lines (incl.
  `documents_analyzed`); created tests, baseline line, warnings, compile refusals → generator lines; verdict counts and
  per-test verdicts, grounding results, drops → still main-run commands; repair outcomes → repairer lines (every
  file, by id); the final report → Step 9 by id and title. The ingest-tests JSON has no `notes` field (R3), so the old
  Step 9 sentence about "notes" was dropped. No gap found.
- **Built CLI (T033):** `ingest-verdict --suite s --all --ids TC-101,TC-999` → `results` [TC-101 grounded 0.92 pass],
  `not_found` [TC-999], counts for TC-101 only, the summary on stderr; without `--ids` both tests; `--ids` alone →
  exit 1 with the message. A new workspace → six agents under `.claude/agents/`, no `.github/agents/`; a pre-093
  workspace (analyst / generator removed, the generate skill customised) → `update-skills` adds the two and lists the
  skill under "Skipped (customized by user)", content unchanged.
- **Acceptance script (T034):** `acceptance/setup-workspace.sh` with the built CLI → three docs (storage, sharing,
  retention) indexed into doc suite `_root` (3 documents, ~506 tokens), ready for "generate tests for the storage
  suite". The `/context` measurements and the grounded-rate comparison with the 092 build (SC-001–SC-003) are the
  user's run.
- **Decisions / deviations:**
  - C1 (user decision): a test with no CLI verdict gets the critic once more, then is kept unverified ("Unverified
    (critic could not judge)"), never regenerated; the pre-093 8a step 5 is gone.
  - I3 (analysis, unanswered): the retry-once-then-unverified rule also applies to the update skill's Step 5a, with
    skill-neutral wording (FR-015 says "both skills").
  - I4: contracts.md and data-model.md updated for the C1 / U1 rules.
  - The written==0 guard block (incl. the restated NON-STOP paragraph) and the NON-STOP contract were copied from the
    pre-093 skill by the rewrite script, byte for byte.
  - The batch verdict ingest's human summary now also goes to stderr in JSON mode (the 091 rule; research R1).
- **Line endings (T035):** no touched file has mixed CRLF/LF.
