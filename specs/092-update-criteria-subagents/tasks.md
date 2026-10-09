---
description: "Task list for Spec 092 — update and criteria skills as orchestrators (Phase 5)"
---

# Tasks: Update and Criteria Skills as Orchestrators (Phase 5)

**Input**: Design documents from `/specs/092-update-criteria-subagents/`: spec.md, plan.md, research.md, data-model.md,
contracts/agent-contracts.md, quickstart.md.

**Tests**: Required (spec T1–T6). Each new guard must fail on `309174b`.
- The guard tests are written in Phase 2 and run against the untouched sources (`git diff --quiet 309174b -- src` must
  hold) before any source edit.
- Install rows use the real `spectra init` (`InitHandler`, with skills) and `spectra update-skills` through
  `BaselineTestWorkspace.RunAsync`, in the `CriteriaCommands` collection.
- A pre-092 workspace is a real init whose three new agent files and their manifest entries are removed, with a
  user-modified `.claude/skills/spectra-update/SKILL.md`.

**Organization**: by user story.
- US1 = update orchestration (P1);
- US2 = criteria orchestration (P1);
- US3 = the user still sees everything (P1; relay verification);
- US4 = install layout (P2);
- US5 = the critic's one-line answer (P2) is a prerequisite of US1's Step 5a, so its single task (T007) is in Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `092-update-criteria-subagents` (= `309174b`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1219 / CLI 2046 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.

---

## Phase 2: Foundational — guard tests, the proof against `309174b`, the critic's final line

- [X] T002 [P] Write tests/Spectra.CLI.Tests/Skills/AgentInstallLayoutTests.cs (spec T1, T2; FR-016, FR-017), `CriteriaCommands` collection. Rows:
  - **T1:** a real `spectra init` (with skills) → `.claude/agents/` holds `spectra-critic.agent.md`, `spectra-updater.agent.md`, `spectra-repairer.agent.md`, `spectra-extractor.agent.md`; `.github/agents/` doesn't exist;
  - **T1b:** `SkillInstallLayout.AgentPath(root, f)` is `.claude/agents/<f>` for every key of `AgentContent.All` and for a name no agent has yet (`spectra-future.agent.md`) — the general rule;
  - **T2:** a pre-092 workspace (real init; the three new agent files and their manifest entries removed; a line appended to `.claude/skills/spectra-update/SKILL.md`) → `spectra update-skills` exits 0; the three agents exist under `.claude/agents/` with the bundled content; the update skill still ends with the appended line; the output lists it under "Skipped (customized by user)"; nothing under `.github/agents/`.
- [X] T003 [P] Write tests/Spectra.CLI.Tests/Skills/OrchestratorAgentTests.cs (spec T3, T4; FR-001–FR-007). Per new agent (`spectra-updater.agent.md`, `spectra-repairer.agent.md`, `spectra-extractor.agent.md`):
  - it is in `AgentContent.All`;
  - frontmatter: `name: <agent>`, `tools: [Bash, Read, Write]`, `model: inherit`, `disable-model-invocation: true`, `context: fork`; no `Task` or `Edit` in `tools`;
  - body: "never ask the user", "never start a subagent", never edits a test `.md` file, the NON-STOP rules (no shell loops, no interpreter pipes, the `stop:` line), and that the final message is only the return lines;
  - **updater:** `spectra ai compile-update-prompt --suite <suite> --group <group>` and `--test-id <id>`, `PROMPT_TOO_LARGE` → per test, `.spectra/updates/<suite>/updated-<id>.json`, `spectra ai ingest-update <suite> --all`, "at most twice", the line forms `<id> | written`, `(manual verdict kept)`, `<id> | unchanged_pending <BUCKET>: <why>`, `<id> | failed <error_code>: <first error>`, `baseline: created=`, `warning:`; it never runs `update-ack` or the critic;
  - **repairer:** `spectra ai compile-repair-prompt --suite <suite> --test <id>`, `.spectra/repairs/<suite>/repaired-<id>.json`, `--all --keep-baseline`, "at most twice", the three line forms; it contains no `ingest-grounding` and no `spectra-critic` call;
  - **extractor:** `spectra ai compile-extraction-prompt --doc`, `.spectra/criteria.json`, `spectra ai ingest-criteria`, `LOCK_TIMEOUT` (retry once), `INVALID_CRITERION_ID`, the outcome forms `<doc> | extracted|unchanged|rejected|failed <code>`, the changeset lines copied verbatim (`Criteria changeset for`, `+`, `~`, `-` with `still referenced by`, `*`, `No changes.`), duplicate-id `Warning:` lines; it contains no `docs retire`, `docs apply-move` or `docs migrate-criteria`;
  - **T4 (critic):** the critic's last message is exactly one line, `<id> | <verdict> <score>` or `<id> | failed: <reason>` (whitespace normalised); the existing critic rows still pass.
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Skills/OrchestratorSkillTests.cs (spec T5, T6; FR-008–FR-015, FR-018, FR-019). Section slices as in `SkillsManifestTests.UpdateStep` (`**Step 5**` → `**Step 5a**`, `**Step 5a**` → `**Step 6**`, criteria `### Step 2` → `### Step 3`). Rows:
  - **update, context budget contract:** a `## Context budget contract` section before `## Update test cases for a suite` names docs, compiled prompts, test files, staging JSON and verdict files as never read, and the allowed calls (`spectra` commands, `update-ack`, `.spectra/progress.json`, Task);
  - **update, NON-STOP:** names Task calls to `spectra-updater`, `spectra-repairer`, `spectra-critic`, and `Write` only to `.spectra/progress.json`;
  - **update Step 5:** contains Task `spectra-updater` with `{suite, group}` and the edit-again form with `ids`; has no `spectra ai compile-update-prompt` or `spectra ai ingest-update` command and no Write of a staging file; says to report every returned line; `unchanged_pending` still asks acknowledge / edit again; a failure after the subagent's retries is reported, not redone;
  - **update Step 5a:** contains Task `spectra-critic` and Task `spectra-repairer`; routes by the critic's one-line answer; has no `compile-repair-prompt` or `ingest-update` command and no "read each test's verdict file"; a missing or `failed:` critic line → unverified without reading the file; keeps `--all --repaired --repair-attempts 1 --ids` and "repair failed: <error_code>";
  - **update all suites / Step 7:** one suite at a time; the report built from the returned lines (every outcome, baseline line, warning);
  - **criteria Step 2:** contains Task `spectra-extractor` with `{doc, component}`; has no `spectra ai compile-extraction-prompt` or `spectra ai ingest-criteria` command; says to report every returned line;
  - **criteria Step 3:** the summary is built from the returned lines and includes the changeset lines (added, modified, removed with referencing tests, metadata), duplicate-id warnings and failures;
  - (guard) criteria Step 1 still confirms retirements with the user; the import and list flows are unchanged.
- [X] T005 Proof run: with `git diff --quiet 309174b -- src` holding, run the T002–T004 classes. Expect every row except the stated guards to fail for the stated reason (the new agents don't exist; `AgentPath` sends other names to `.github/agents/`; the critic has no final-line rule; the skills do the work in the main session). Save the output to the session scratchpad (`092/proof-309174b.txt`) and summarise it in the Notes.
- [X] T006 Inventory the existing assertions that pin moved text (research R8) in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs and record in the Notes, per row, "retarget to <agent>", "stays on the skill" or "update the count":
  - update Step 5 / 5a: `UpdateSkill_EditLoop_CompilesPerGroup_StagesPerTest_IngestsAll`, `UpdateSkill_EditLoop_ReportsEachResult_UnchangedIsAcknowledged`, `UpdateSkill_EditLoop_RetriesAFailedTestAtMostTwice_WithTestId`, `UpdateSkill_EditLoop_HandlesTheCompileRefusals`, `UpdateSkill_Exit8_ReadsResults_AndNeverStops`, `UpdateSkill_UnchangedPending_AsksToAcknowledgeOrEditAgain_ForLoadAndCriteriaChangedOnly`, `UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair`, `BothSkills_KeepAFailedFileAsFailedId_AndRetryWithAFreshFile` (update half), `UpdateSkill_DoesNotKeepTheBaseline`, `UpdateSkill_RepairedGrounding_PassesTheTestsId`, `UpdateSkill_AFailedRepairIngest_IsRetriedAtMostTwice_InStep5a`, `UpdateSkill_ReCritiquesOnlyALandedRepair_AndReportsAFailedOne`;
  - criteria Step 2: `CriteriaSkill_UsesCompiledPromptAsTheOnlyDocumentSource`, `CriteriaSkill_SummarisesChangesets_AndHandlesInvalidIds`, `CriteriaSkill_WriteSteps_HandleExit1` (Step 2 rows), `CriteriaSkill_CompileStep_ReadsErrorsNotMessage`, `CriteriaSkill_NamesConfigErrorCodes`;
  - agents: `AgentContent_HasAllAgents` (one → four).
- [X] T007 (US5, FR-007) In src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md, add a final section "## Your final message": the last message is exactly one line, `<id> | <verdict> <score>` (e.g. `TC-101 | partial 0.72`), or `<id> | failed: <reason>` when no verdict could be written; nothing else. Leave the frontmatter (pinned model), isolation contract, verdict file and `ingest-verdict --from` unchanged.

**Checkpoint**: guards exist and are proven to fail on `309174b`; the critic answers in one line (T003's T4 row green).

---

## Phase 3: User Story 1 — a large suite update without running out of room (P1) 🎯 MVP

**Goal**: Step 5 / 5a orchestrate the updater, critic and repairer (FR-001–FR-004, FR-008–FR-013).

**Independent test**: T003's updater/repairer rows and T004's update rows green.

- [X] T008 [P] [US1] Create src/Spectra.CLI/Skills/Content/Agents/spectra-updater.agent.md per contracts/agent-contracts.md and data-model.md: frontmatter (R2); input; the moved Step 5 recipe (compile per group or per id, the exit-4 codes incl. `PROMPT_TOO_LARGE`, `TEMPLATE_OUTDATED` (per test + `warning:`), `NOTHING_TO_RECONCILE`, `TRIAGE_STALE` → `stop:`; edit rules; one staging file per test; `ingest-update <suite> --all --output-format json --verbosity quiet`; exit 0/8/1; the outcome meanings incl. the `failed` error codes and `failed-<id>.json`; the bounded retry with a fresh staging file); the return contract (every `results` id, `(manual verdict kept)`, one summed `baseline:` line, `warning:` lines, `stop:`); NON-STOP; never asks, never acks, never runs the critic.
- [X] T009 [P] [US1] Create src/Spectra.CLI/Skills/Content/Agents/spectra-repairer.agent.md per the contracts: frontmatter; input {suite, ids}; per id compile the repair prompt and patch only what the critic flagged; Write `.spectra/repairs/<suite>/repaired-<id>.json`; one `ingest-update <suite> --all --keep-baseline --output-format json --verbosity quiet`; failed ids re-patched per `error_code` (`MULTIPLE_TESTS`, `DRIFT_DETECTED`, `MALFORMED_JSON` / `SCHEMA_INVALID`), at most twice, the same repair attempt; a refused compile → `failed <error_code>`; one line per input id; no critic, no grounding; written so the generation flow can reuse it (no update-flow assumptions).
- [X] T010 [US1] In src/Spectra.CLI/Skills/Content/Skills/spectra-update.md: the overview diagram and paragraph show the delegation; add `## Context budget contract` before `## Update test cases for a suite` (FR-013, incl. which user decisions stay in the main session); rewrite the NON-STOP contract (Task calls to `spectra-updater`, `spectra-repairer`, `spectra-critic`; `Write` only to `.spectra/progress.json`; no Read of docs, prompts, tests, staging or verdict files).
- [X] T011 [US1] In spectra-update.md Step 5: per group, in order: the progress write, then Task `spectra-updater` with `suite: <suite>` and `group: <group>`; report every returned line as returned; `unchanged_pending` LOAD / CRITERIA_CHANGED → ask acknowledge (`update-ack`, main) or edit again (Task `spectra-updater` with `ids: <id>`; at most twice per test, R4); REMOVED / CRITERIA_REMOVED / RELINK → re-run triage (as today); a `failed` line → reported, not redone; a `stop:` line → report it and stop for this suite; a missing line for an id → outcome unknown.
- [X] T012 [US1] In spectra-update.md Step 5a (research R5): keep "ask once" and the `manual verdict kept` rule (now from the updater's `(manual verdict kept)` lines); per written test Task `spectra-critic` and route by its final line; then `spectra ai ingest-verdict --suite <suite> --all` and `spectra ai ingest-grounding --suite <suite> --all`; partial / hallucinated ids → one Task `spectra-repairer` with `suite` and `ids`; for ids returned `written` / `unchanged` only: the critic again per id, `ingest-verdict --all`, `ingest-grounding --suite <suite> --all --repaired --repair-attempts 1 --ids <those ids> --output-format json`; `failed` → "repair failed: <error_code>" (unverified if partial, waiting for the user if hallucinated); still hallucinated → the user's choice (edit again = Task `spectra-updater` with `ids`; restore from git, the user runs it; delete via `spectra-delete`); a missing or `failed:` critic line → unverified, never read the verdict file.
- [X] T013 [US1] In spectra-update.md "Update all suites" and Step 7: one suite at a time through the same Steps 5/5a; the Step 7 report is built from the returned lines (every outcome, each baseline line, every warning, the re-verification counts with ids).
- [X] T014 [US1] Retarget the update rows inventoried in T006 in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs: assertions on work now in the updater/repairer read `AgentContent.All["spectra-updater.agent.md"]` / `["spectra-repairer.agent.md"]` with the same strings; assertions on main-session behaviour stay on the skill (adjusted only where the wording moved); record each change in the Notes.
- [X] T015 [US1] Run OrchestratorAgentTests, OrchestratorSkillTests (update rows), SkillsManifestTests, CriticSubagentSkillTests, NoCopilotIsmsTests, TerminologyAuditTests: green.

**Checkpoint**: US1 green.

---

## Phase 4: User Story 2 — criteria extraction without whole documents in the session (P1)

**Goal**: Step 2 orchestrates the extractor (FR-005, FR-006, FR-014, FR-015, FR-019).

**Independent test**: T003's extractor rows and T004's criteria rows green.

- [X] T016 [P] [US2] Create src/Spectra.CLI/Skills/Content/Agents/spectra-extractor.agent.md per the contracts: frontmatter; input {doc, component}; the moved Step 2a–2c recipe (compile with the empty-document short circuit → `unchanged` + `note:`; exit 4 `MISSING_INPUT` / exit 1 codes → `failed <code>`; extract from the compiled prompt only, never reading `{doc}` separately; Write `.spectra/criteria.json`; `ingest-criteria --doc --component --from .spectra/criteria.json --output-format json`; exit 5/6/7 → regenerate, at most 2 attempts; exit 1 `LOCK_TIMEOUT` → retry once; other exit 1 → `failed <code>`); the return contract with the changeset lines copied verbatim from the ingest's stderr summary and the duplicate-id warnings; never retires, moves or migrates.
- [X] T017 [US2] In src/Spectra.CLI/Skills/Content/Skills/spectra-criteria.md: the overview shows the delegation and a short context budget rule (main never reads the document or a compiled prompt); Step 2: per document, in order, the progress write, then Task `spectra-extractor` with `doc: <path>` and `component: <component>`; report every returned line as returned; a `stop:` line → report it; Step 3: the summary from the returned lines (per document its outcome and changeset lines; removed criteria still referenced by tests; moves, retirements, skipped and failed documents). Steps 0–1 and the import and list flows unchanged.
- [X] T018 [US2] Retarget the criteria rows inventoried in T006 (Step 2 content → `AgentContent.All["spectra-extractor.agent.md"]`, same strings; Step 1 / Step 3 rows stay on the skill); record each change in the Notes.
- [X] T019 [US2] Run OrchestratorAgentTests, OrchestratorSkillTests (criteria rows), SkillsManifestTests: green.

**Checkpoint**: US2 green.

---

## Phase 5: User Story 3 — the user still sees everything (P1)

**Goal**: nothing shown to the user before 092 disappears (FR-018, FR-019).

**Independent test**: the relay inventory below has no gap.

- [X] T020 [US3] Relay inventory (Notes): list what the user saw before 092 in each flow — `ingest-update --all`'s per-file human lines, its baseline summary and warnings; `compile-update-prompt` refusals; `ingest-verdict` / `ingest-grounding` summaries (still run by main); the repair outcomes; `ingest-criteria`'s changeset summary and duplicate warnings; extraction failures — and, for each, the return line or main-run command that carries it now and the skill sentence that reports it. Fix any gap in the agent or skill text, and add a T004 row for it.

**Checkpoint**: US3 verified.

---

## Phase 6: User Story 4 — agents installed in the right place (P2)

**Goal**: every bundled agent installs to `.claude/agents/` (FR-016, FR-017).

**Independent test**: T002 green.

- [X] T021 [US4] In src/Spectra.CLI/Skills/SkillInstallLayout.cs: `AgentPath(root, file)` returns `Path.Combine(root, ".claude", "agents", file)` for every file (no switch); update the doc comments (every bundled agent is a `.claude/agents/` subagent).
- [X] T022 [US4] In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs `AgentContent_HasAllAgents`: the four agent files (deliberate change: one → four); record it in the Notes.
- [X] T023 [US4] Run AgentInstallLayoutTests, ClaudeSkillsInstallTests, InitCommandTests, UpdateSkills tests, StagingGitIgnoreTests, GitIgnoreOfferTests: green.

**Checkpoint**: US4 green; T002 passes.

---

## Phase 7: Polish, docs, verification

- [X] T024 [P] Update docs/usage.md: the prerequisites line (`spectra init` creates the four agents in `.claude/agents/`); the update flow (the edit step and re-verification run in the updater / critic / repairer subagents, the assistant relays every outcome); the criteria flow (one extractor subagent per document; the full changeset is relayed); a customised update or criteria skill keeps the in-session flow until refreshed.
- [X] T025 [P] Update docs/cli-reference.md: the `init` entry's `.claude/agents/` count (4 agents: critic, updater, repairer, extractor); `update-skills` adds missing agents; nothing else (no CLI change).
- [X] T026 [P] Update CHANGELOG.md with "Changed (Spec 092 — update and criteria subagents, Phase 5)" under `[2.6.0]` before `### Notes`, and Notes entries: run `spectra update-skills` to get the agents; a customised update or criteria skill keeps the old flow; the critic's final line.
- [X] T027 [P] Update CLAUDE.md: a short "Recent Changes" entry for 092 at the top, with the counts from T028.
- [X] T028 Run the full `dotnet build --no-incremental` (zero new warnings) and `dotnet test`. Expect Core ≥ 1219 / CLI ≥ 2046 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts.
- [X] T029 Run the quickstart install check (steps 1–2) with the built CLI (copy the bin) in a scratch workspace; record the results in the Notes.
- [X] T030 Write specs/092-update-criteria-subagents/acceptance/setup-workspace.sh for the user's manual acceptance (quickstart "Manual acceptance"): it builds a scratch workspace with the real `spectra init`, one doc with three sections, ≥ 30 tests linked to them, a baseline, then a doc change that makes ≥ 30 tests LOAD in ≥ 3 groups (checked with `update-triage --dry-run`), and 5 more docs for extraction. Run it once with the built CLI and record the triage counts in the Notes. Point quickstart.md at it.
- [X] T031 Fill in the Notes (counts, the T005 proof, the T006 inventory outcome, the T020 relay inventory, deviations, changed assertions), check line endings of every touched file (no mixed CRLF/LF), mark every task [X], and stop. Ask the user before committing as `feat(092): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push. The manual acceptance (SC-001–SC-003) is the user's run.

---

## Dependencies & Execution Order

- **Setup** (T001) → **Foundational** (T002–T007). T005 must run before any source edit; T007 (critic line) before US1's Step 5a (T012).
- **US1** (T008–T015): T008, T009 in parallel; T010 → T011 → T012 → T013 (one skill file); T014 after T011–T013; T015 last.
- **US2** (T016–T019): independent of US1 (other files) except `SkillsManifestTests.cs` (T014, T018 — do them in order).
- **US3** (T020): after US1 and US2.
- **US4** (T021–T023): independent; T022 edits `SkillsManifestTests.cs` (after T018).
- **Polish:** T024–T027 after the stories; T028–T031 last.

## Parallel Examples

```text
# Guards (different files):
T002 AgentInstallLayoutTests   T003 OrchestratorAgentTests   T004 OrchestratorSkillTests
# New agents:
T008 spectra-updater   T009 spectra-repairer   T016 spectra-extractor
# Docs:
T024 usage   T025 cli-reference   T026 CHANGELOG   T027 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: the suite update runs as an orchestrator; the large-update failure mode is gone.
2. **US2**: criteria extraction keeps documents out of the main session.
3. **US3**: verify nothing the user saw is lost.
4. **US4**: the install rule (needed before users can run 1–2 on their own workspaces; independent of the text work).
5. **Polish**: docs, full run, install quickstart, acceptance script; commit and merge on approval; the user runs the
   manual acceptance.

## Notes

- **Baseline (T001):** `309174b` — Core 1219 / CLI 2046 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 0 errors, the 4 known warnings.
- **Proof (T005):** with `git diff --quiet 309174b -- src` holding, 21 of the 22 guard rows failed for the stated reason
  (the three agents not bundled, `AgentPath` routing `spectra-future.agent.md` to `.github/agents/`, no critic
  final-line rule, the skills doing the work in the main session); the guard row (criteria Step 1 and the import / list
  flows) passed. Output kept in the session scratchpad (`092/proof-309174b.txt`).
- **Final run (T028):** `dotnet build --no-incremental`: 0 errors, the same 4 warnings. `dotnet test`: Core 1219 / CLI
  2068 (only `ProfileFormatLoaderTests` fails; +22) / Execution 228 / Integration 8. `ConfigRuleMatrixTests` and
  `InitTemplateWorkspaceTests` green.
- **Scope (FR-021, analysis C2):** `git diff --stat 309174b -- src` = `SkillInstallLayout.cs` + the critic agent and the
  update / criteria skills; new: the three agent files. No CLI, config, template or format change.
- **T006 inventory (outcome):**
  - retargeted to `spectra-updater`: `UpdateSkill_EditLoop_CompilesPerGroup_StagesPerTest_IngestsAll`,
    `…_ReportsEachResult_UnchangedIsAcknowledged`, `…_RetriesAFailedTestAtMostTwice_WithTestId`,
    `…_HandlesTheCompileRefusals`, `UpdateSkill_Exit8_ReadsResults_AndNeverStops`,
    `BothSkills_KeepAFailedFileAsFailedId_AndRetryWithAFreshFile` (update half),
    `UpdateSkill_NonStop_AllowsWritesToTheStagingFiles` (staging write) — these two NON-STOP rows were missing from
    the T006 list and found by the US1 run;
  - retargeted to `spectra-repairer`: the repair strings of `UpdateSkill_ReverifiesEditedTests_AskingOnce_WithOneRepair`,
    `UpdateSkill_DoesNotKeepTheBaseline` (now: neither the skill nor the updater has `--keep-baseline`; the repairer
    does), `UpdateSkill_AFailedRepairIngest_IsRetriedAtMostTwice_InStep5a` (retry), `UpdateSkill_NonStop_AllowsTheCriticAndTheRepairsFolder`
    (repairs write);
  - retargeted to the critic agent: the `compile-critic-prompt` string of `…_ReverifiesEditedTests_…`;
  - stay on the skill (wording adjusted): `UpdateSkill_UnchangedPending_…` (edit again = `spectra-updater` with
    `ids: <id>`, "at most twice per test"), `UpdateSkill_RepairedGrounding_PassesTheTestsId` (`--ids <id1>,<id2>`),
    `UpdateSkill_ReCritiquesOnlyALandedRepair_…` ("only for the ids it returned `written` or `unchanged`");
  - retargeted to `spectra-extractor`: `CriteriaSkill_UsesCompiledPromptAsTheOnlyDocumentSource`,
    `CriteriaSkill_SummarisesChangesets_AndHandlesInvalidIds` (extraction half), `CriteriaSkill_CompileStep_ReadsErrorsNotMessage`;
    the `**2c.` row of `CriteriaSkill_WriteSteps_HandleExit1` moved to the new `CriteriaExtractor_IngestStep_HandlesExit1`
    (the 1a/1b rows stay); `CriteriaSkill_NamesConfigErrorCodes` unchanged (Step 1 still names the codes);
  - `AgentContent_HasAllAgents`: one → four agents.
  - Moved wording is compared whitespace-normalised (the rules, not their line wrapping).
- **Relay inventory (T020):** before 092 → now:
  - `compile-update-prompt` refusals → updater `warning:` (`TEMPLATE_OUTDATED`, `NOTHING_TO_RECONCILE`,
    `PROMPT_TOO_LARGE` — this one was silent; added, with an OrchestratorAgentTests assertion) or `stop:` lines;
  - `ingest-update --all` per-file lines, `manual_verdict_kept`, baseline summary, warnings → updater result lines,
    `(manual verdict kept)`, `baseline:`, `warning:`; relayed by Step 5 and Step 7;
  - `ingest-verdict --all` / `ingest-grounding --all` → still run by the main session (unchanged);
  - critic verdicts → the critic's final line (verdict + score); the findings stay in the verdict file and, for
    partial tests, in the grounding block's condensed findings — deliberate (FR-010/011: route by the line, never read
    the file);
  - repair outcomes → repairer lines; intermediate retry errors are not relayed, the final error per test is
    (deliberate: the outcome, not each attempt);
  - `ingest-criteria` changeset summary + duplicate warnings → extractor lines, verbatim; compile refusals / empty doc →
    `failed <code>` + `warning:` / `unchanged` + `note:`.
- **Install check (T029), built CLI:** a new workspace → `.claude/agents/` = critic, extractor, repairer, updater; no
  `.github/agents/`. A pre-092 workspace (critic only, a customised update skill) → `update-skills` lists the three new
  agents, prints "Skipped (customized by user): .claude\skills\spectra-update\SKILL.md", and the skill keeps the
  appended line. (The automated T2 row checks the files and content; update-skills' Spectre console output isn't
  captured by the test harness, so the "Skipped" line is checked here.)
- **Acceptance script (T030):** `acceptance/setup-workspace.sh` with the built CLI → `update-triage --dry-run`: 33 tests,
  `counts: {load: 33}`, groups G1–G6 (10 + 1 per section); `docs changed`: 6 entries (5 new docs + storage.md). The
  `/context` measurements (SC-001–SC-003) are the user's run.
- **Deviations / decisions (analysis findings left unanswered):**
  - U1: `TRIAGE_STALE` keeps its existing behaviour (FR-022): the updater returns `stop: triage is stale` and the main
    session re-runs Step 1 and continues (an OrchestratorSkillTests row guards it); other `stop:` lines stop the suite.
  - I1: the return lines follow the agreed data-model grammar (`stop:`, `(manual verdict kept)`, `note:`, the
    `Criteria changeset for` heading), which the spec's "only" lists don't name.
  - U2: a missing repairer line → "repair outcome unknown", unverified, not re-critiqued.
  - C1: the new agents pass the existing agent-wide guards (no fenced `spectra ai update` / analyze / docs index blocks;
    terminology audit).
  - I2: the plan's "~25 retargeted assertions" — the real count is 17 rows + 1 moved Theory row + the agent count.
  - The criteria skill has no NON-STOP section; its context budget rule is in the overview (as T017 said).
- **Line endings (T031):** no touched file has mixed CRLF/LF (the critic agent was normalised to CRLF after the append).
