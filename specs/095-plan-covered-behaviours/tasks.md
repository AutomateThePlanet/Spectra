---
description: "Task list for Spec 095 — plan only uncovered behaviours (094 review fix)"
---

# Tasks: Plan Only Uncovered Behaviours (094 Review Fix)

**Input**: Design documents from `/specs/095-plan-covered-behaviours/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (spec "Required Tests"; plan T1–T5). Each new rule must fail on `db20415`, except the 094-plan
compatibility guard.
- Guards are written in Phase 2 and run on the untouched sources (`git diff --quiet db20415 -- src` must hold) before
  any source edit.
- Rows that use new APIs (`TitleCoverage`, the `duplicateThreshold` parameter, `ResolvedBehavior.CoveredBy`) can't
  compile on the base. They are staged out of the test projects for the proof run (as in 094), and their failure is
  "type/member missing".
- CLI rows use `BaselineTestWorkspace` from the real `spectra init` with the 094 `GenerationPlanWorkspace` fixture, in
  the `CriteriaCommands` collection.

**Organization**: by user story (spec.md).
- US1 = the recommendation counts what is missing (P1);
- US2 = the plan never asks for tests that already exist (P1);
- US3 = existing workspaces keep working (P2).

US2 depends on US1's per-behaviour coverage.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US3 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `095-plan-covered-behaviours` (= `db20415`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1228 / CLI 2167 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.

---

## Phase 2: Foundational — guard tests and the proof against `db20415`

- [X] T002 [P] Write tests/Spectra.Core.Tests/Validation/TitleCoverageTests.cs (T1; FR-001, R1):
  - `TitleCoverage.Match` gives exactly `DuplicateDetector.CalculateTitleSimilarity` for the best match, over 2,000 seeded random title pairs (words from a fixed list, mixed case and punctuation);
  - covered iff best ≥ threshold (rows at 0.6 and 0.9 for one pair of similarity ~0.8);
  - ties → lowest id (ordinal; two tests with identical titles `TC-120` / `TC-105` → `TC-105`);
  - an empty behaviour title or test title → not covered;
  - case / punctuation / spacing differences → similarity 1;
  - no tests → all null;
  - **unpadded ids (analyze L3):** equal similarity for `TC-9` and `TC-10` → `TC-10` (ordinal order, as documented);
  - **threshold warning (analyze L1):** `TitleCoverage.ThresholdWarning` returns "generation.duplicate_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered." for 0, −0.5 and 1.5, and null for 0.6 and 1.
- [X] T003 [P] Write tests/Spectra.Core.Tests/Validation/TitleCoveragePerformanceTests.cs (T1 perf; FR-005, SC-004): 2,500 tests × 100 behaviours with realistic word-based titles (seeded; ~10 behaviours close to a test title), `Stopwatch` around `TitleCoverage.Match`, assert < 2 s and write the time to `ITestOutputHelper`. The class runs in its own collection with `DisableParallelization = true` (like 087's `GitEnvironmentCollection`) (analyze L2); record the measured time in the Notes.
- [X] T004 [P] Rewrite the coverage rows in tests/Spectra.CLI.Tests/Generation/AnalysisRecommendationBuilderTests.cs (T2; FR-001–FR-003). Replace `Build_RecommendedCount_FlooredAtZero_WhenSnapshotCoversAll` (deliberate; Notes) with:
  - **snapshot ignored:** a snapshot with `ExistingTestCount = 5` and no matching tests → `AlreadyCovered` 0, `RecommendedCount` 3 — the same as without a snapshot;
  - **50 tests / 12 gaps:** 50 existing tests, 15 behaviours of which 3 match test titles → `AlreadyCovered` 3, `RecommendedCount` 12, with and without a snapshot `{ExistingTestCount = 50}`;
  - **threshold:** `duplicateThreshold: 0.9` vs the default for a ~0.8 pair;
  - **per behaviour:** `CoveredBy` per behaviour (matching test id or null), ties → lowest id;
  - **breakdowns:** still over all behaviours.
- [X] T005 [P] Add rows to tests/Spectra.CLI.Tests/Generation/Plan/GenerationPlanBuilderTests.cs (builder; FR-006, R5):
  - covered behaviours (`ResolvedBehavior … CoveredBy: "TC-101"`) keep their ids and `covered_by` in the plan but are in no batch;
  - a covered unresolved behaviour is not in `unsourced`;
  - counts reconcile with the uncovered count;
  - all covered → no batches, `planned_total` 0;
  - `sections` lists only refs used by batches;
  - **counts = behaviours (analyze M1):** for a plan built as the ingest builds it (recommended = uncovered count), every batch's `count == behavior_ids.Count` and Σ counts == `recommended`; the 094 proportional / `one_per_batch` rows stay unchanged;
  - `duplicate_threshold` serialised after `batch_size`; byte-identical rerun;
  - a 094 plan JSON (no `duplicate_threshold`, no `covered_by`) parses with `CoverageKnown == false` and every `CoveredBy == null` (guard on parse; the property is new).
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/PlanCoverageIngestTests.cs (T3; FR-001–FR-007, FR-011). Use the real-init `GenerationPlanWorkspace` with tests written into `test-cases/storage/` (`ws.WriteTest` + `RebuildIndex`) whose titles match some behaviours. Rows:
  - `already_covered` / `recommended` from the rule;
  - `covered` JSON entries `{id, title, covered_by}`;
  - stderr summary lines `covered: <title> (<covered_by>)` between the batch lines and the `unresolved:` lines;
  - the same counts with and without the criteria files (snapshot vs none);
  - `generation.duplicate_threshold: 0.9` in the config → fewer covered;
  - no key → 0.6, no warning;
  - the plan's batches hold only uncovered behaviours;
  - all covered → `batches: []`, `planned_total` 0;
  - the 50-tests / 12-gaps case end to end → `recommended` 12, batch counts sum 12;
  - **covered + unresolvable source (analyze M2):** a behaviour whose title matches a test and whose source is `docs/nope.md` → in `covered`, not in `unresolved`, in no batch (not `unsourced`), and exactly one summary line for it (`covered: …`);
  - **threshold warning (analyze L1):** `generation.duplicate_threshold: 0` → exit 0 and the stderr warning line.
- [X] T007 [P] Write tests/Spectra.CLI.Tests/Commands/Generate/PlanCoverageCompileTests.cs (T4; FR-008, FR-010):
  - compile every batch of a plan with covered behaviours: no covered title in any `--batch` prompt (incl. `unsourced`);
  - **094-plan guard** (passes on both): a hand-written 094 plan file (094 serialisation, no `duplicate_threshold` / `covered_by`) → `--batch 1` exits 0 and names its behaviours;
  - **094-plan warning:** the same plan → `warnings` contains "the generation plan predates per-behaviour coverage; re-run the analysis for corrected counts".
- [X] T008 [P] Write tests/Spectra.CLI.Tests/Skills/CoveredLinesTextTests.cs (T5; FR-009, FR-010):
  - the analyst relays `covered: <title> (<covered_by>)` lines verbatim;
  - the generate skill's Step 4 shows "Already covered — not generated" with the `covered:` lines;
  - Step 9 lists the covered behaviours;
  - the skill names the "predates per-behaviour coverage" warning and offers to re-run the analysis;
  - guards: the 093/094 rows still pass (written==0 guard verbatim, `batch: <id>`).
- [X] T009 Proof run. With `git diff --quiet db20415 -- src` holding, stage T002, T003 and the T004/T005 rows that use new members out of the test projects. Run the rest of T004–T008: every row except the stated guards fails for the stated reason. Restore the staged files. Save the output to the session scratchpad (`095/proof-db20415.txt`) and summarise it in the Notes.
- [X] T010 Inventory the existing rows that depend on the old count (snapshot test count, `CountCoveredBehaviors`, 094 plan/ingest rows asserting `recommended` / batches in suites with tests) and record "retarget" / "unchanged" in the Notes.

**Checkpoint**: guards written and proven against `db20415`.

---

## Phase 3: User Story 1 — the recommendation counts what is missing (P1) 🎯 MVP

**Goal**: one per-behaviour rule; `already_covered` / `recommended` from it; same with and without a snapshot.
**Independent test**: T002, T003, T004 and the T006 count rows pass.

- [X] T011 [US1] In src/Spectra.Core/Validation/DuplicateDetector.cs expose `public static string NormalizeTitle(string)` (the existing normalisation; `NormalizeText` calls it) — no behaviour change.
- [X] T012 [US1] Add src/Spectra.Core/Validation/TitleCoverage.cs (R1, R2):
  - `record CoverageMatch(string TestId, double Similarity)`;
  - `static IReadOnlyList<CoverageMatch?> Match(IReadOnlyList<string> behaviourTitles, IReadOnlyList<(string Id, string Title)> tests, double threshold)`;
  - normalise once; exact equality → 1;
  - prune by length bound against `max(threshold, best)` (equal bounds still computed);
  - two-row Levenshtein with early stop beyond the largest useful distance;
  - similarity `1 − d / maxLen`; ties → lowest id (ordinal).

  T002 and T003 pass.
- [X] T013 [US1] In src/Spectra.CLI/Generation/AnalysisRecommendationBuilder.cs and AnalysisRecommendation.cs:
  - `Build(…, double duplicateThreshold = 0.6)` computes coverage with `TitleCoverage` over the existing tests (id, title);
  - `AlreadyCovered` = covered, `RecommendedCount` = uncovered;
  - expose `CoveredBy` (per behaviour, aligned with `Behaviors`);
  - ignore `snapshot` for counts (doc comment);
  - `CountCoveredBehaviors` delegates to the rule at 0.6.

  T004 passes.
- [X] T014 [US1] In src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs:
  - pass `config.Generation.DuplicateThreshold`;
  - stop building the `CoverageSnapshot` (pass `null`);
  - add `covered: [{id, title, covered_by}]` to the JSON;
  - add the `covered: <title> (<covered_by>)` summary lines (after the batch lines, before `unresolved:`; `|` → `/` is not needed here);
  - `unresolved` (JSON and lines) only for uncovered behaviours (analyze M2);
  - print `TitleCoverage.ThresholdWarning` to stderr as a `Warning:` line when set (analyze L1).

  The T006 count rows pass.

**Checkpoint**: counts correct.

---

## Phase 4: User Story 2 — the plan never asks for tests that already exist (P1)

**Goal**: batches, `unsourced` and counts over uncovered behaviours only; covered ones shown, never compiled.
**Independent test**: T005, T006 plan rows, T007 prompt row and T008 pass.

- [X] T015 [US2] In src/Spectra.CLI/Generation/Plan/GenerationPlan.cs:
  - `PlannedBehavior.CoveredBy`; plan-level `DuplicateThreshold` (`double?`) and `CoverageKnown => DuplicateThreshold is not null`;
  - serialise `duplicate_threshold` after `batch_size` (when set) and `covered_by` for every behaviour;
  - parse both as optional.
- [X] T016 [US2] In src/Spectra.CLI/Generation/Plan/GenerationPlanBuilder.cs: `ResolvedBehavior` gains the optional trailing `CoveredBy`; `Build(…, double? duplicateThreshold = null)`. Ids for all behaviours; batches, `unsourced` and `Counts` over uncovered only; `sections` filtered to refs used by batches. T005 passes.
- [X] T017 [US2] In src/Spectra.CLI/Commands/Generate/IngestAnalysisCommand.cs `BuildPlanAsync`: pass each behaviour's `CoveredBy` and the threshold; `recommended` = uncovered. The T006 plan rows pass.
- [X] T018 [US2] Update src/Spectra.CLI/Skills/Content/Agents/spectra-analyst.agent.md (Step 4 and the final message: the `covered: <title> (<covered_by>)` lines, verbatim; example) and src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:
  - Steps 1–3 bullets name the `covered:` lines;
  - Step 4 "Already covered — not generated:" with the lines;
  - Step 9 lists them.

  The T008 rows for these pass.

**Checkpoint**: no batch names a covered behaviour; the user sees the covered ones.

---

## Phase 5: User Story 3 — existing workspaces keep working (P2)

**Goal**: a 094 plan still compiles, with a re-run warning; real-init defaults.
**Independent test**: T007 094-plan rows and the T006 "no key" row pass; quickstart check 6.

- [X] T019 [US3] In src/Spectra.CLI/Commands/Generate/CompileBatchPrompt.cs: when `!plan.CoverageKnown`, add the warning "the generation plan predates per-behaviour coverage; re-run the analysis for corrected counts" (every mode). The T007 warning row passes.
- [X] T020 [US3] In src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md: the "predates per-behaviour coverage" warning is reported once, with an offer to re-run the analysis (invoke `spectra-analyst`, then back to Step 4). The T008 row passes.

**Checkpoint**: pre-095 workspaces compile; the user is told to re-analyse.

---

## Phase 6: Polish, docs, verification

- [X] T021 [P] Update docs/cli-reference.md: `ingest-analysis` `already_covered` / `recommended` meaning (per-behaviour rule, threshold), `covered`, `unresolved` only for uncovered behaviours, the plan's `duplicate_threshold` / `covered_by`, uncovered-only batches and their counts ("one per uncovered behaviour"), the threshold warning, the 094-plan warning.
- [X] T022 [P] Update docs/usage.md (covered behaviours shown at approval, never generated) and docs/configuration.md (`duplicate_threshold` is also the analysis coverage threshold).
- [X] T023 [P] Update CHANGELOG.md under `[2.6.0]`:
  - "Fixed (review fixes, Spec 095)" — the covered count, the uncovered-only plan;
  - a Notes entry on the changed meaning of `already_covered` / `recommended`, re-running the analysis for a 094 plan, and the edge case `duplicate_threshold` outside (0, 1] (used as given, with a stderr warning).
- [X] T024 [P] Update CLAUDE.md: a short "Recent Changes" entry for 095 with the counts from T025.
- [X] T025 Run the full `dotnet build --no-incremental` (0 new warnings) and `dotnet test`. Expect Core ≥ 1228 / CLI ≥ 2167 with only `ProfileFormatLoaderTests` failing / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts and the T003 time. Check that `git diff --stat db20415 -- src` touches only the files in plan.md.
- [X] T026 Run the quickstart CLI checks 1–8 with the built CLI on a real-init workspace and a pre-095 workspace (a 094 plan, a suite with tests). Record the results in the Notes.
- [X] T027 Fill in the Notes (counts, the T009 proof, the T010 inventory, the replaced 044 assertion and any other changed assertion, deviations, the T003 time; analyze L4: the 094-plan path is covered by the CLI rows (T007), since the skill always analyses before compiling). Check that no touched file has mixed CRLF/LF; edited files keep their working-copy CRLF. Mark every task [X] and stop. Ask the user before committing as `fix(095): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001) → **Foundational** (T002–T010). T009 before any source edit.
- **US1** (T011 → T012 → T013 → T014).
- **US2** (T015 → T016 → T017; T018 after T014) — needs US1's `CoveredBy`.
- **US3** (T019 after T015; T020 after T018 — same skill file).
- **Polish:** T021–T024 after the stories; T025–T027 last.

## Parallel Examples

```text
# Guards (different files):
T002 TitleCoverageTests  T003 TitleCoveragePerformanceTests  T004 AnalysisRecommendationBuilderTests
T005 GenerationPlanBuilderTests  T006 PlanCoverageIngestTests  T007 PlanCoverageCompileTests  T008 CoveredLinesTextTests
# Docs:
T021 cli-reference  T022 usage + configuration  T023 CHANGELOG  T024 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: correct counts from one rule.
2. **US2**: the plan batches only uncovered behaviours; agents and skill show covered ones.
3. **US3**: 094 plans keep working with a re-run warning.
4. **Polish**: docs, full run, built-CLI checks; commit and merge on approval.

## Notes

- **Baseline (T001):** `db20415` — Core 1228 / CLI 2167 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 /
  Integration 8; 0 errors, the 4 known warnings.
- **Proof (T009):** with `git diff --quiet db20415 -- src` holding, 27 CLI rows ran (T004 minus its two new-member rows,
  T006, T007, T008, and the existing builder rows): all 16 new rule rows failed for the stated reason (snapshot count /
  no `covered` / batched covered behaviours / no threshold warning / no 094-plan warning / no `covered:` text). Guards
  passed: the 094-plan compatibility row, the 093/094 skill guards, and the breakdowns row ("as today"). T002, T003, the
  T005 builder rows and the T004 threshold / `CoveredBy` rows don't compile on the base (members missing) — that is
  their failure. Output: scratchpad `095/proof-db20415.txt`.
- **Inventory (T010) / changed assertions:** the 044 row `Build_RecommendedCount_FlooredAtZero_WhenSnapshotCoversAll`
  is replaced (deliberate) by `Build_ASnapshot_NoLongerDecidesTheCoveredCount` and the 50-tests / 12-gaps,
  threshold, `CoveredBy` and breakdown rows. Unchanged and green: `BehaviorAnalyzerTests.CountCoveredBehaviors_*`
  (`CountCoveredBehaviors` delegates to the rule at 0.6), every 094 plan / ingest / compile row (their workspaces have no
  tests, and every 094 builder count still holds). The 094 plan constant moved to `GenerationPlanWorkspace.Plan094`.
- **Correction during implementation (M1):** the 094 share rule does not give each batch its own behaviour count for
  unequal batches (4 and 1 → 3 and 2), contrary to the analysis. `GenerationPlanBuilder.Counts` gained one explicit
  case — when `recommended` equals the batched behaviours (always, from the ingest), counts = behaviours per batch —
  so the M1 decision holds; research R5 records it. All 094 count rows keep their values.
- **Performance (T003, FR-005):** the first version (bounded two-row DP) took 5.3 s (Debug); with Myers' bit-parallel
  distance (titles ≤ 64 chars, else the bounded DP) and the length and character-bag lower bounds: 829–856 ms for 2,500
  tests × 100 behaviours (Debug, standalone). In the full parallel run, single timings reached 2.0–2.1 s (other test
  assemblies share the CPU; one run's three timings were 2,099 / 2,031 / 1,488 ms), so the row takes the fastest of five
  runs (the rule is deterministic, so slower runs only measure the shared machine). Equality with
  `DuplicateDetector.CalculateTitleSimilarity` is pinned over 2,000 seeded pairs (titles above and below 64 chars).
- **Final run (T025):** see the CLAUDE.md entry — Core 1245 (+17) / CLI 2194 (+27; only `ProfileFormatLoaderTests`) /
  Execution 228 / Integration 8; the 4 known warnings.
- **Built-CLI checks (T026)** on a real-init workspace (094 acceptance script) with 50 ingested tests:
  1. 15 behaviours (3 match test titles) → `already_covered` 3, `recommended` 12, `covered` B1–B3 → TC-107/121/140,
     batches 3 + 3 + 6 = 12, three `covered:` lines;
  2. without `docs/criteria/` → the same counts;
  3. a near-match title: covered by TC-107 at 0.6, uncovered at 0.99; no key → no warning;
  4. no batch prompt contains a covered title;
  5. all covered → `recommended` 0, `batches: []`;
  6. a 094 plan (no `duplicate_threshold` / `covered_by`) → exit 0 with the "predates per-behaviour coverage" warning;
  7. threshold 0 → exit 0, `recommended` 0, the stderr warning;
  8. a covered behaviour with source `docs/nope.md` → only in `covered`, one summary line; the uncovered unresolved one
     is in `unsourced`.
- **Analyze L4:** the 094-plan path is covered by the CLI rows (T007) and check 6; the skill always analyses before it
  compiles, so it only meets a 094 plan when a batch is compiled without a fresh analysis.
- **Deviations:** the ingest no longer builds the `CoverageSnapshot` (R4); the human summary's "counts raised to one per
  batch" line stays but can't be reached from the ingest. Edited CRLF files keep CRLF.
