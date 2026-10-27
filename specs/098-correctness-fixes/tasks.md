# Tasks: Correctness Fixes (Phase 6, Part 3)

**Input**: Design documents from `/specs/098-correctness-fixes/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/contracts.md, quickstart.md

**Tests**: required by the spec (FR-022: every fix has a regression test that fails on `986da01`).

## Phase 1: Setup

- [X] T001 Record the baseline on `986da01`: `dotnet build` + `dotnet test` (Core / CLI / Execution / Integration counts, 0 failures) in specs/098-correctness-fixes/tasks.md Notes
- [X] T002 Inventory the existing rows that pin behaviour this spec changes (097 `CriticModelTests` invalid-model rows, audit `repair` rows, suite delete `leftovers*` rows, keep-baseline `Created` rows, `data-id` rows, 095 `duplicate_threshold` rows) and list them in Notes

## Phase 2: Foundational

- [X] T003 Add pre-098 workspace helpers (init template with `coverage_threshold` removed / `ai.critic` replaced) next to `ConfigRuleMatrixTests.InitTemplate()` in tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs

## Phase 3: User Story 1 — a pre-069 workspace keeps working (P1, F1)

- [X] T004 [US1] Write tests/Spectra.CLI.Tests/Config/LegacyCriticConfigTests.cs: typed rule (v1.45 block, 058 block, `openai/gpt-4.1`, number, `[1m]`, non-object, new block), `spectra validate` notice text, `update-skills` installs `model: sonnet` with one warning, real init + legacy config runs validate / update-triage / audit-grounding / compile-critic-prompt
- [X] T005 [US1] Add `ConfigRuleMatrixTests` rows "pre-069 critic block (v1.45)" and "ai.critic.model openai/gpt-4.1" (both 0/null/0 for critic and ingest) in tests/Spectra.CLI.Tests/Commands/ConfigRuleMatrixTests.cs
- [X] T006 [US1] Prove T004/T005 fail on the untouched src (Notes)
- [X] T007 [US1] `CriticConfig` JSON converter (any token; string `model`; other keys recorded; invalid model / non-object recorded) in src/Spectra.Core/Models/Config/CriticConfig.cs
- [X] T008 [US1] `AiConfig.CriticModel` (model, warning) and `EffectiveCriticModel` in src/Spectra.Core/Models/Config/AiConfig.cs
- [X] T009 [US1] `ConfigLoader`: drop `INVALID_CRITIC_MODEL`; retired-key list gains `ai.critic.enabled` / `timeout_seconds` / `max_concurrent` in src/Spectra.Core/Config/ConfigLoader.cs
- [X] T010 [US1] Notice text (retired keys + legacy-model sentence) in src/Spectra.CLI/Commands/Validate/ValidateHandler.cs
- [X] T011 [US1] One warning when the configured model is ignored in src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs
- [X] T012 [US1] Retarget the 097 rows that pinned `CONFIG_INVALID` for an invalid model in tests/Spectra.CLI.Tests/Skills/CriticModelTests.cs (Notes)

## Phase 4: User Story 2 — the audit says what each test needs (P1, F2)

- [X] T013 [US2] Write tests/Spectra.CLI.Tests/Commands/Review/AuditActionTests.cs (grounded/partial/hallucinated without block → ground/repair/review; stale → critic; block → none/review; `--action ground`; summary counts; interrupted run; kept hallucinated) and a skill-text guard for the resume routing in tests/Spectra.CLI.Tests/Skills/AuditResumeSkillTests.cs
- [X] T014 [US2] Action rule + summary counts in src/Spectra.CLI/Commands/Review/AuditGroundingHandler.cs and src/Spectra.CLI/Results/AuditGroundingResult.cs; `ground` in `Actions` / option help in src/Spectra.CLI/Commands/Review/AuditGroundingCommand.cs
- [X] T015 [US2] Generate resume path routes ground / repair / review; CLI flags table lists `ground` in src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md
- [X] T016 [US2] Retarget audit rows that expected `repair` for grounded / hallucinated verdicts (Notes)

## Phase 5: User Story 3 — honest counts and records (P2, F3 + F4)

- [X] T017 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Suite/SuiteDeleteReportTests.cs (3 verdict files reported once, dry run and real run, human summary)
- [X] T018 [P] [US3] Write tests/Spectra.CLI.Tests/Skills/ManifestNewestTests.cs (relative v1 + absolute v2, edited → skipped, record v2; restore v2 → updated; same-rank ambiguity → candidates saved and read back)
- [X] T019 [US3] `verdict_files_removed`; leftovers without verdict files in src/Spectra.CLI/Commands/Suite/SuiteDeleteHandler.cs and src/Spectra.CLI/Results/SuiteResults.cs
- [X] T020 [US3] Ranked record + `candidates` on save/load in src/Spectra.CLI/Skills/SkillsManifest.cs

## Phase 6: User Story 4 — a repair never baselines a test (P2, F5)

- [X] T021 [US4] Write tests/Spectra.CLI.Tests/Baseline/KeepBaselineNoEntryTests.cs (unbaselined repaired → no entry, triage unbaselined, `left_unbaselined: 1`; baselined repaired → unchanged; rebuild unchanged)
- [X] T022 [US4] Skip + count in keep-baseline mode in src/Spectra.CLI/Baseline/BaselineRecorder.cs

## Phase 7: User Story 5 — anchors (P2, F6)

- [X] T023 [US5] Write tests/Spectra.Core.Tests/Baseline/HeadingIdAnchorTests.cs (`data-id`/`aria-id` not ids in matcher and canonical ref; `id=` forms still ids; image heading `#setup`/`#-setup`/`#logo-setup`; 084 `{#id}` snapshot → COSMETIC; body `{…}` untouched) and a CLI triage row with an 084 snapshot
- [X] T024 [US5] Whole-attribute `id=` in src/Spectra.Core/Baseline/SectionExtractor.cs and src/Spectra.Core/Baseline/SectionRef.cs
- [X] T025 [US5] `ImageFreeKey` and rule 1 in src/Spectra.Core/Baseline/SectionExtractor.cs
- [X] T026 [US5] `SnapshotComparisonText` + COSMETIC equality in src/Spectra.Core/Baseline/SectionExtractor.cs and src/Spectra.Core/Baseline/ChangeClassifier.cs

## Phase 8: User Story 6 — per-test criterion state (P3, F7)

- [X] T027 [US6] Write tests/Spectra.CLI.Tests/Generation/GroupCriterionStateTests.cs (disagreeing group annotated per test; agreeing group and single test byte-identical to 097 renderer; determinism)
- [X] T028 [US6] `TestBlock.CriterionStates` in src/Spectra.CLI/Generation/UpdateContext.cs; annotation in src/Spectra.CLI/Generation/UpdatePromptRenderer.cs

## Phase 9: User Story 7 — count-mode batches survive a doc edit (P3, F8)

- [X] T029 [US7] Write tests/Spectra.CLI.Tests/Commands/Generate/CountFallbackStaleTests.cs (customised template + changed / unresolvable section → exit 0 + warning; bundled → PLAN_STALE)
- [X] T030 [US7] Mode decided before staleness in src/Spectra.CLI/Commands/Generate/CompileBatchPrompt.cs

## Phase 10: User Story 8 — coverage threshold (P3, F9)

- [X] T031 [US8] Write tests/Spectra.CLI.Tests/Commands/Generate/CoverageThresholdTests.cs (coverage key used; fallback with notice when duplicate ≠ 0.6; no notice at 0.6; range → CONFIG_INVALID; init template has 0.6; 095 plan read) + `ConfigRuleMatrixTests` rows (`coverage_threshold` 2 → CONFIG_INVALID, null → ok)
- [X] T032 [US8] `CoverageThreshold`, `DuplicateThreshold` setting, choice in src/Spectra.Core/Models/Config/GenerationConfig.cs; range check in src/Spectra.Core/Config/ConfigLoader.cs
- [X] T033 [US8] Use the choice in src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs and the warning text in src/Spectra.Core/Validation/TitleCoverage.cs; plan field in src/Spectra.CLI/Generation/Plan/GenerationPlan.cs
- [X] T034 [US8] `coverage_threshold: 0.6` in src/Spectra.CLI/Templates/spectra.config.json

## Phase 11: Polish

- [X] T035 [P] docs/cli-reference.md (audit actions, suite delete field, `coverage_threshold`), docs/configuration.md (`ai.critic` legacy rule, `coverage_threshold`), docs/usage.md (resume routing)
- [X] T036 [P] CHANGELOG.md and a short CLAUDE.md Recent Changes entry
- [X] T037 Full `dotnet build` + `dotnet test`, 0 failures; record counts in Notes
- [X] T038 Built-CLI quickstart on a real-init and a pre-098 workspace (quickstart.md steps 1–3, 10); Notes
- [ ] T039 Ask the user before committing and merging; never push

## Dependencies

- Phase 1–2 before the stories. Stories are independent of each other (different files), except T009 and T032 (both
  `ConfigLoader.cs`) and T024–T026 (one file): do them in order.
- Within a story: tests → proof of failure → implementation → retargets.

## Parallel examples

- T017 and T018 (different test files); T035 and T036.

## Implementation strategy

US1 first (top priority, every command in a legacy workspace), then US2; P2/P3 stories in order. Each story is a
complete, testable increment.

## Notes

- **T001 baseline (`986da01`):** Core 1230 / CLI 2258 / Execution 228 / Integration 8, 0 failures.
- **T006 proof:** with `src` stashed (986da01 sources) the new rows fail — Core 11 (`HeadingIdAnchorTests`: prefixed
  ids ×4, image anchors ×2, 084 snapshots ×4, canonical ref), CLI 52 (every new class plus the retargeted
  `CriticModelTests` rows and the `ConfigRuleMatrixTests` rows `ai.critic.model openai/gpt-4.1` and
  `generation.coverage_threshold 2`). Guard rows (whole `id=`, 085 anchors, real edits, body braces, agreeing groups,
  batch-mode `PLAN_STALE`, `v1.45` / `v058` commands, which only broke `update-skills`) pass on both, as intended.
  `Snapshot084IdTests` (added after the proof run) exercises the same `ChangeClassifier` path that fails the Core rows.
- **T002 deliberate assertion changes** (each has a `Spec 098 … deliberate change` comment):
  - `CriticModelTests`: `AnInvalidModel_*` (warn + `sonnet`, was CONFIG_INVALID + installed line kept),
    `ANonStringModel_*`, `CommandsThatNeedAValidConfig_*` (loads), `TheRetiredKeys_*` (model next to `provider` is now
    ignored — 097 honoured it).
  - `ProviderRetirementTests` (Core): three rows asserted no notice for blocks with `enabled` / `max_concurrent`.
  - `AuditTruthTests`: `--action repair` lists TC-104 only (TC-101 is `ground`); unknown-action message includes
    `ground`; resume row now pins `--action ground,repair,review`.
  - `SuiteDeleteSafetyTests.TheDryRun_*`: verdict files are read from `verdict_files_removed`.
  - `PlanCoverageIngestTests`: threshold row uses `coverage_threshold`; out-of-range row removes `coverage_threshold`
    (pre-098 fallback) so the warning still names `duplicate_threshold`. `GenerationPlanBuilderTests`: plan key
    `coverage_threshold`. `TitleCoverageTests` (Core): default warning names `generation.coverage_threshold`.
- **F6 design change during implementation:** the image-free key is a fallback after the 085 text key (not part of the
  same rule), so no ref that resolves today changes target; snapshot stripping is anchored on the current section's
  heading texts (`CurrentSection.HeadingTexts`), so a `{…}` in body text is never stripped.
- **T037 full run:** Core 1253 / CLI 2326 / Execution 228 / Integration 8, 0 failures; build shows only the 4 pre-existing warnings (099).
- **T038 quickstart (built CLI, scratch workspace):** real `spectra init` → `ai.critic.model: sonnet`,
  `coverage_threshold: 0.6`, agent `model: sonnet`, no warning. v1.45 block + `ai.fallback_strategy` (and no
  `coverage_threshold`): validate / update-triage / docs changed exit 0 with no CONFIG_INVALID; validate notice lists
  `ai.fallback_strategy, ai.critic.provider, ai.critic.enabled` + the legacy sentence; update-skills warns once and
  installs `model: sonnet`. `openai/gpt-4.1`: commands run; update-skills warns "not valid … using sonnet".
