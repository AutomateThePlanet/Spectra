---
description: "Task list for Spec 096 — workspace safety and hygiene (Phase 6, part 1)"
---

# Tasks: Workspace Safety and Hygiene (Phase 6, Part 1)

**Input**: Design documents from `/specs/096-workspace-safety/`: spec.md, plan.md, research.md, data-model.md,
contracts/contracts.md, quickstart.md.

**Tests**: Required (spec "Required Tests"). Each new rule must fail on `23aa08c`, except the stated guards.
- Guards are written in Phase 2 and run on the untouched sources (`git diff --quiet 23aa08c -- src` must hold) before
  any source edit.
- Rows that use new members (`ManifestKey`) can't compile on the base. They are staged out for the proof run (as in
  094/095), and their failure is "member missing".
- CLI rows use `BaselineTestWorkspace` from the real `spectra init` (`InitWithRealTemplateAsync`, or the real
  `InitHandler` with skills for F1) in the `CriteriaCommands` collection.

**Organization**: by user story (spec.md).
- US1 = edits never overwritten (F1, P1);
- US2 = suite delete touches nothing else (F2, P1);
- US3 = truthful show and validate (F3, F4, P1);
- US4 = gitignore offer per repository (F5, P2);
- US5 = faster delete, script line endings, green tests, unused key (F6–F9, P3).

F8 (test infrastructure) runs first in Phase 2, so every later run is deterministic.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline on `096-workspace-safety` (= `23aa08c`): `dotnet build --no-incremental` and `dotnet test`. Expect Core 1245 / CLI 2194 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8 and the 4 known warnings. Record them in the Notes.
- [X] T002 Measure the delete baseline on `23aa08c` (R7). Use the built CLI in a scratch workspace from the real init with 2,500 tests in 5 suites (via `ingest-tests`) and `depends_on` on 1 % of them. Time `spectra delete <one id> --force --output-format json` 3 times. Record the median, the machine and the output in the Notes and the scratchpad (`096/delete-baseline.txt`).

---

## Phase 2: Foundational — guard tests, proof, test infrastructure

- [X] T003 [P] Write tests/Spectra.CLI.Tests/Skills/UpdateSkillsKeepsEditsTests.cs (F1; FR-001–FR-003). Use the real `InitHandler` with skills. Rows:
  - **edits kept:**
    - edit `.spectra/prompts/test-generation.md`, `profiles/_default.yaml` and `CUSTOMIZATION.md` (append a line);
    - run `spectra update-skills`;
    - all three are byte-identical and listed under the existing "Skipped (customized by user)" message — init's normalised record is now found (analyze M1; fails on the base: templates are overwritten through the key mismatch);
  - **no record, equal content:** delete a skill's manifest entry without changing the file → recorded again, file unchanged;
  - **no record, different content:** delete a skill's entry and edit the file → skipped with the "no record — it may have been edited" message, byte-identical (the only row asserting the new message);
  - **duplicate keys (analyze H1):**
    - (a) the template under the relative key = hash(v1) and the absolute key = hash(v2), file = v2 (unedited, the bundled text with a marker that makes it "v2") → updated or unchanged, never "customized";
    - (b) the same two entries, the file edited by the user → skipped as customized, byte-identical;
    - (c) after the run, the manifest holds one normalised entry for that path;
  - **moved workspace (analyze M1):**
    - (a) a full manifest written at another root (every key absolute under `D:\old\ws`) → every entry re-rooted, no warning, no edited file touched, unedited files updated normally;
    - (b) one unrecognised foreign key (`D:\elsewhere\notes.txt`) → dropped, with the single summary warning;
  - **pre-096 manifest:**
    - mixed keys: absolute skill keys, `\` relative template keys, plus `D:\elsewhere\x.md` (no install-path tail);
    - after `update-skills`, the saved manifest has only `/` relative keys and no foreign key;
    - the single summary warning reports 1 dropped entry;
    - an edited template is byte-identical;
  - **094 check:** after normalisation, `compile-prompt --batch` on an unedited pre-094 template copy still picks the bundled template (guard: passes on both).
- [X] T004 [P] Write tests/Spectra.CLI.Tests/Commands/Suite/SuiteDeleteSafetyTests.cs (F2; FR-004, FR-005). Suites:
  - `a`: TC-101, TC-102;
  - `b`: TC-201 `depends_on: TC-101`; TC-202 `depends_on: [TC-101]`; TC-203 with a step line `  - TC-101` and a 3-newline run in its body;
  - `c`: TC-301, a test with a CRLF file and a blank-line run, no dependency.

  Plus `.spectra/updates/a/updated-TC-101.json`, `.spectra/repairs/a/failed-TC-102.json`, `.spectra/generation-plan/a.json`, and `.spectra/verdicts/critic-verdict-TC-101.json` / `critic-input-TC-101.json`. Rows:
  - **`--dry-run --output-format json`:**
    - exit 0;
    - lists `dependents_cleaned` (TC-201), `warnings` (TC-202) and `leftovers` (all five paths);
    - no file changed (snapshot of the workspace bytes);
  - **`--force`:**
    - TC-201's `depends_on` removed and the rest of its bytes kept (lossless);
    - TC-202 byte-identical and warned;
    - TC-203 and suite `c` byte-identical;
    - all leftovers gone; `a` removed;
    - **no baseline lock (analyze M2):** suites `b` and `c` have `_source-baseline.json` files → byte-identical after the delete; with `BaselineStore.LockTimeoutOverride` short and `.spectra/baseline.lock` held by the test, `suite delete a --force` still succeeds;
  - **guards:**
    - without `--force` → `EXTERNAL_DEPENDENCIES` as before;
    - an automation-linked suite → `AUTOMATION_LINKED` as before.
- [X] T005 [P] Write tests/Spectra.CLI.Tests/Commands/ShowValidateTruthTests.cs (F3, F4; FR-006, FR-007). Rows:
  - **show:** a suite created by `ingest-tests` (bare file names in `_index.json`) → `spectra show TC-101 --output-format json` succeeds and prints the test; legacy index `file: "<suite>/TC-101.md"` still found (guard);
  - **validate:**
    - one test with broken frontmatter → exit 1, JSON `status: "failed"`, message counts the error;
    - human mode → exit 1, no "Validation passed.";
    - guard: a clean suite → exit 0.
- [X] T006 [P] Write tests/Spectra.CLI.Tests/Commands/Init/GitIgnoreMarkerTests.cs (F5; FR-008, FR-009). Rows:
  - **init:** the real init's `.gitignore` contains `.spectra/verdicts/` and `.spectra/dropped-tests.json` and one `# spectra-offered:` line naming every pattern;
  - **093 workspace:**
    - setup: a recorded `gitignore_offered` without the two entries, no marker;
    - `update-skills` adds the two once and writes one marker line;
    - a second run changes nothing;
  - **teammate clone:**
    - setup: no manifest record, a marker naming `.spectra/verdicts/`, the pattern itself removed;
    - `update-skills` doesn't re-add it;
  - **migration:**
    - setup: a recorded list that includes a pattern the user removed, no marker;
    - after `update-skills` the pattern is still absent and a marker names it;
  - **no `.gitignore`:** nothing created (guard).
- [X] T007 [P] Write tests/Spectra.CLI.Tests/Commands/Delete/DeletePerformanceTests.cs (F6; FR-010, SC-005; analyze H2). Use 2,500 tests in 5 suites, built directly on disk (fast) in a `CriteriaCommands` workspace, exactly one of which has `depends_on` the target. Rows:
  - **deterministic guard:** the service's internal full-parse counter (test hook, `InternalsVisibleTo`) — deleting the one target performs exactly 2 full parses (the target and its scalar dependent), not 2,500. Log the elapsed time in the test output only (no time assertion);
  - **parity:** `PlanAsync` on a mixed workspace (scalar, list and unparseable dependents; targets in two suites; a suite without `_index.json`) gives the expected plan (asserted values; passes on the base too, as a guard).

  The T002-vs-096 timing comparison goes in T031 / Notes (SC-005 is a recorded measurement).
- [X] T008 [P] Write tests/Spectra.CLI.Tests/Infrastructure/RepositoryAttributesTests.cs (F7; FR-011, `[GitFact]`): `git check-attr eol -- specs/094-generation-batch-prompts/acceptance/setup-workspace.sh` reports `lf`, and the working copy has no `\r`.
- [X] T009 [P] Write tests/Spectra.CLI.Tests/Config/UnusedBatchSizeKeyTests.cs (F9; FR-013). Rows:
  - the embedded init template has no `generation_batch_size`;
  - guard: a config with `ai.generation_batch_size: 30` loads through `ConfigLoader.LoadForWriteAsync`, and `ingest-analysis` runs without error.
- [X] T010 Add tests/Spectra.CLI.Tests/TestFixtures/WorkingDirectoryCollection.cs (F8): `[CollectionDefinition("WorkingDirectory", DisableParallelization = true)]`. Fix `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown` in tests/Spectra.CLI.Tests/Profile/ProfileFormatLoaderTests.cs to assert only headings the embedded usage guide has (find the missing one; keep the others). Run the full CLI suite: 0 failures.
- [X] T011 Proof run. With `git diff --quiet 23aa08c -- src` holding, stage out the rows using new members and run T003–T009. Every row except the stated guards fails for the stated reason. Restore the staged files, save the output to the scratchpad (`096/proof-23aa08c.txt`) and summarise it in the Notes.
- [X] T012 Inventory the existing rows that pin absolute manifest keys, exact `.gitignore` bytes, `GitIgnoreOffered` writes, the old `suite delete` cleanup or `validate` exit codes (e.g. `AgentInstallLayoutTests`, `SkillsManifestTests`, `StagingGitIgnoreTests`, `GitIgnoreOfferTests`, `GenerationPlanGitIgnoreTests`, the `SuiteDelete*` tests, `Validate*` tests). Record "retarget" / "unchanged" in the Notes.

**Checkpoint**: guards proven against `23aa08c`; the test suite runs deterministically.

---

## Phase 3: User Story 1 — edits never overwritten (P1) 🎯 MVP

**Goal**: one manifest key form; no record → never overwrite.
**Independent test**: T003 passes.

- [X] T013 [US1] Add src/Spectra.CLI/Skills/ManifestKey.cs: `For(root, path)` (repo-relative, `/`) and `TryNormalize(root, key, out normalized)` (false outside the root).
- [X] T014 [US1] In src/Spectra.CLI/Skills/SkillsManifest.cs (`SkillsManifestStore`), normalise keys on load: absolute under the root → relative; `\` → `/`; absolute keys outside the root → re-rooted by their install-path tail (R1, analyze M1; last segments, case-insensitive on Windows), unrecognised foreign keys → dropped with one summary warning ("N manifest entries from another location were dropped (was the workspace moved?)"); duplicate keys → all hashes kept as candidates (analyze H1; the file is unedited when it matches any). Saves write one normalised entry per key.
- [X] T015 [US1] Use `ManifestKey.For` for every manifest write and read:
  - src/Spectra.CLI/Commands/Init/InitHandler.cs (skills, agents, usage guide, templates, profile, CUSTOMIZATION);
  - src/Spectra.CLI/Commands/Prompts/PromptsResetHandler.cs;
  - src/Spectra.CLI/Commands/Generate/CompileBatchPrompt.cs (`IsUneditedCopyAsync`: one key).
- [X] T016 [US1] In src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs:
  - look up and write through `ManifestKey.For`;
  - an existing file with no record → record it if equal to bundled, else skip with the no-record message (template id for templates, "delete it" for the others);
  - print the manifest's dropped-key warnings.

  T003 passes.

**Checkpoint**: edited files survive `update-skills` in real-init and pre-096 workspaces.

---

## Phase 4: User Story 2 — suite delete touches nothing else (P1)

**Goal**: the 090 deletion rules for suites, plus leftovers.
**Independent test**: T004 passes.

- [X] T017 [US2] In src/Spectra.CLI/Commands/Delete/TestDeletionService.cs add `SuiteLeftovers(string suite)`: `.spectra/updates/<suite>/`, `.spectra/repairs/<suite>/` (whole folders) and `.spectra/generation-plan/<suite>.json`, each only when present. Add an executor that removes them (no-op in dry run).
- [X] T018 [US2] Rewrite the cleanup in src/Spectra.CLI/Commands/Suite/SuiteDeleteHandler.cs:
  - load `TestDeletionService`, resolve the suite's tests and build the plan;
  - derive `ExternalDependencyCleanup` from the plan (scalar dependents plus warned ones) for the existing refusals;
  - with `--dry-run`, list `dependents_cleaned`, `warnings`, `leftovers` (verdict and fingerprint-record files plus the suite leftovers) and write nothing;
  - otherwise clean the scalar dependents (service, lossless writer), remove the verdict and fingerprint-record files and the suite leftovers, delete the suite folder and update the config (as before);
  - never remove the suite's own baseline entries and never take the baseline lock for them (analyze M2): `_source-baseline.json` goes with the folder. Mechanism (analyze L2): `TestDeletionService`'s plan and executor take an `includeBaseline` option (default `true`; suite delete passes `false`), so the plan lists no baseline entries and the executor never takes the baseline lock. The M2 row in T004 pins it;
  - delete `CascadeRemoveDependsOnAsync` and `CountExternalDepsAsync`.

  Add `DependentsCleaned`, `Warnings`, `LeftoversRemoved` / `Leftovers` to the suite delete result type. T004 passes.

**Checkpoint**: other suites' files keep every byte except the cleaned dependency.

---

## Phase 5: User Story 3 — truthful show and validate (P1)

**Goal**: `show` finds standard tests; `validate` fails on parse errors.
**Independent test**: T005 passes.

- [X] T019 [P] [US3] In src/Spectra.CLI/Commands/Show/ShowHandler.cs resolve `Path.Combine(suiteDir, entry.File)` first and fall back to `Path.Combine(testsDir, entry.File)` (legacy).
- [X] T020 [P] [US3] In src/Spectra.CLI/Commands/Validate/ValidateHandler.cs compute `failed = !result.IsValid || totalErrors > 0` and use it for the JSON status and message, the human output and the exit code.

**Checkpoint**: T005 green.

---

## Phase 6: User Story 4 — gitignore offer per repository (P2)

**Goal**: two new entries; offered markers in `.gitignore`; one-time migration.
**Independent test**: T006 passes.

- [X] T021 [US4] In src/Spectra.CLI/Commands/Init/SpectraGitIgnore.cs:
  - add `.spectra/verdicts/` and `.spectra/dropped-tests.json` to `Patterns` (keep `Pre090Offered` the explicit 089 list);
  - add the marker format: parse `# spectra-offered: …` lines, and `MarkerLine(patterns)`;
  - `EnsureForInitAsync` writes one marker line after the patterns;
  - `OfferNewAsync(root, legacyRecord)` follows R6: offered = markers ∪ legacy ∪ (`Pre090Offered` when neither); add the candidates not present or negated; append one marker line for every pattern of offered ∪ candidates without a marker; no `.gitignore` → nothing.
- [X] T022 [US4] In src/Spectra.CLI/Commands/UpdateSkills/UpdateSkillsHandler.cs and src/Spectra.CLI/Commands/Init/InitHandler.cs stop writing `GitIgnoreOffered` (read only for migration); keep the report lines. T006 passes.

**Checkpoint**: removed patterns stay removed in every clone.

---

## Phase 7: User Story 5 — delete performance, line endings, green tests, unused key (P3)

**Goal**: F6, F7, F9 (F8 was done in Phase 2).
**Independent test**: T007, T008, T009 pass; the full suite is green.

- [X] T023 [US5] In src/Spectra.CLI/Commands/Delete/TestDeletionService.cs (R7), with an `internal` full-parse counter for the T007 guard (`InternalsVisibleTo` already exists for the test project):
  - `LoadAsync` reads each file's frontmatter only (text up to the closing `---`, `FrontmatterOnly`) for id and `depends_on`;
  - full `TestCaseParser.Parse` happens lazily for targets and for scalar dependents of deleted ids;
  - targets are resolved through `_index.json` per suite with the catalog as fallback.

  `SafeDeleteTests`, the 090 delete rows and T007 pass.
- [X] T024 [P] [US5] Add .gitattributes (`*.sh text eol=lf`) and rewrite the three `specs/*/acceptance/setup-workspace.sh` working copies with LF. T008 passes.
- [X] T025 [P] [US5] Remove `"generation_batch_size": 30` from src/Spectra.CLI/Templates/spectra.config.json. Mark `AiConfig.GenerationBatchSize` in src/Spectra.Core/Models/Config/AiConfig.cs `[Obsolete("Unused; generation.batch_size applies (Spec 094/096).")]`, with no new warnings at use sites. T009 passes, and `ConfigRuleMatrixTests` / `InitTemplateWorkspaceTests` stay green.

**Checkpoint**: T007–T009 green.

---

## Phase 8: Polish, docs, verification

- [X] T026 [P] Update docs/cli-reference.md:
  - the update-skills skip message and manifest normalisation;
  - the gitignore markers and the two new entries;
  - `suite delete` cleanup, leftovers and new JSON fields;
  - `validate` exit on parse errors;
  - `show` lookup.
- [X] T027 [P] Update docs/usage.md (update-skills keeps edits; suite delete cleanup) and docs/configuration.md (`ai.generation_batch_size` unused; `generation.batch_size` applies).
- [X] T028 [P] Update CHANGELOG.md under `[2.6.0]` with "Fixed (Spec 096 — workspace safety, Phase 6 part 1)" (F1–F9) and Notes entries: the gitignore marker line; normalised manifest keys; `suite delete` no longer edits list `depends_on`; `validate` now fails on parse errors.
- [X] T029 [P] Update CLAUDE.md: a short "Recent Changes" entry for 096 with the counts from T030.
- [X] T030 Run the full `dotnet build --no-incremental` (no new warnings) and `dotnet test`. Expect Core ≥ 1245 / CLI ≥ 2194 with **0 failures** / Execution 228 / Integration 8. Confirm `ConfigRuleMatrixTests` and `InitTemplateWorkspaceTests` are green. Record the counts and the T007 logged time. Check that `git diff --stat 23aa08c -- src` touches only the files in plan.md.
- [X] T031 Run the quickstart CLI checks 1–9 (check 7 = the SC-005 timing: the T002 baseline against 096, same machine and data) with the built CLI on a real-init workspace and a pre-096 workspace (pre-096 manifest with relative and absolute keys, edited templates, an old config with `ai.generation_batch_size`), and record the results in the Notes.
- [X] T032 Fill in the Notes (counts, the T011 proof, the T012 inventory and changed assertions, the delete times (SC-005), deviations; analyze L1: F8 is test infrastructure — proven by the full run having 0 failures (`ProfileFormatLoaderTests` fails on `23aa08c`) and by the `WorkingDirectory` collection definition existing, not by a new failing row). Check that no touched file has mixed CRLF/LF; edited files keep their working-copy form. Mark every task [X] and stop. Ask the user before committing as `fix(096): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001, T002) → **Foundational** (T003–T012). T011 before any source edit; T010 (F8) is test-only and may land before the proof.
- **US1** (T013 → T014 → T015 → T016).
- **US2** (T017 → T018); T018 after T016 is not required (different files).
- **US3** (T019, T020 in parallel).
- **US4** (T021 → T022); T022 after T016 (same `UpdateSkillsHandler.cs`).
- **US5:** T023 after T017 (same `TestDeletionService.cs`); T024, T025 any time.
- **Polish:** T026–T029 after the stories; T030–T032 last.

## Parallel Examples

```text
# Guards (different files):
T003 UpdateSkillsKeepsEdits  T004 SuiteDeleteSafety  T005 ShowValidateTruth  T006 GitIgnoreMarker
T007 DeletePerformance  T008 RepositoryAttributes  T009 UnusedBatchSizeKey
# Independent fixes:
T019 ShowHandler  T020 ValidateHandler  T024 .gitattributes  T025 config template
# Docs:
T026 cli-reference  T027 usage + configuration  T028 CHANGELOG  T029 CLAUDE.md
```

## Implementation Strategy

1. **MVP = US1**: no more lost edits.
2. **US2, US3**: suite delete and truthful commands.
3. **US4**: gitignore per repository.
4. **US5**: performance, line endings, the unused key.
5. **Polish**: docs, full green run, built-CLI checks; commit and merge on approval.

## Notes

**Counts (T030).** `dotnet build --no-incremental`: 0 warnings, 0 errors. `dotnet test`: Core 1245 / CLI 2228 /
Execution 228 / Integration 8 — **0 failures**, the first fully green run since 078. `ConfigRuleMatrixTests` and
`InitTemplateWorkspaceTests` green. `git diff --stat 23aa08c -- src`: the 13 files of plan.md plus the new
`Skills/ManifestKey.cs`.

**Proof (T011, `src` untouched at 23aa08c).** 30 rows run: 21 failed on 23aa08c (the 20 rule rows plus the staged
`FullParses` row — the counter doesn't exist there), 9 passed as guards (no `.gitignore`,
`show` legacy form, `validate` clean, no baseline lock, without `--force`, old config loads, the usage-guide row,
duplicate keys (a)/(b), the delete parity row). Output: scratchpad `096/proof-23aa08c.txt`.

**T012 inventory.**
- Retargeted (deliberate assertion changes, each commented "Spec 096"):
  - `GitIgnoreOfferTests` — the record is read from the `.gitignore` marker lines instead of the manifest;
    `InitPre090Async` also strips marker lines (a pre-096 file has none); the byte-exact append rows
    (CRLF, LF, trailing blanks, section header, negation, migration) now end with one marker line naming every pattern;
    `TheRealInit…` no longer asserts that init writes the manifest.
  - `GenerationPlanGitIgnoreTests` — a 090–093 / pre-090 workspace is arranged without marker lines (and a 093 record
    written explicitly, since init no longer writes one); `TheRealInit…` asserts the marker instead of the manifest.
  - `InitCommandTests` — 3 rows counted a pattern by substring; the marker line names every pattern, so they count
    whole lines (`PatternLines`).
  - `SkillsManifestTests.SaveAndLoad_RoundTrips` — used `/path/to/SKILL.md`, an absolute key matching no install path,
    which is now dropped on load; it round-trips a repo-relative key.
  - `ProfileFormatLoaderTests.LoadEmbeddedUsageGuide_IsNonEmptyMarkdown` — F8 (asserts `## Executing Tests`).
- Unchanged and green: `AgentInstallLayoutTests`, `StagingGitIgnoreTests`, `SuiteOpsTests`, `BaselineFileGuardTests`
  (suite delete/rename rows), `ValidateCommandTests`, `QuickstartWorkflowTests`, `SafeDeleteTests`, the other
  `SkillsManifestTests` rows.

**Delete timing (SC-005, T002 vs T031; same machine, same 2,500-test data, 25 dependents, `delete TC-0001 --force`).**
23aa08c: 3,694 / 3,827 / 3,864 ms (median 3,827). 096: 1,104 / 1,036 / 1,076 ms (median 1,076) — **3.6×**. Same JSON
results (1 deleted, 25 `depends_on` cleaned, 1 baseline entry). In process (T007 row): load 187 ms, resolve 2 ms,
`FullParses` = 2. Dry run of a known id: 3,542 → 822 ms.

**Quickstart (T031).** Scratchpad `096/quickstart.py` against the built CLI: 24/24 checks pass (1, 2, 2b, 3–6, 8, 9;
7 = the timing above). Check 2 uses a pre-096 manifest (absolute skill/agent keys, `\` template keys, one
`D:\other\x.md` key) with an edited template; check 8 an old config with `ai.generation_batch_size`. Two harness
fixes, not product changes: Spectre wraps the summary warning at the console width (compared whitespace-normalised),
and `validate` needs a suite (an empty workspace exits 1 "No test suites found", as before 096).

**Deviations.**
- F6 went further than the plan to reach SC-005: the frontmatter-only catalog alone measured 2.2 s (1.7×). Now only
  files containing `depends_on` have their frontmatter read at load; every other id is read when needed; targets
  resolve through the suite `_index.json` when the indexed file still has that id (otherwise the old scan, first file in
  path order); and the files are read in parallel and synchronously (async per-file reads were the cost). Results are
  the same — the parity row and `SafeDeleteTests` are unchanged.
- `suite delete` refuses without `--force` on any external reference, scalar or not (list and unparseable ones count
  too, as the regex counted list items before); `external_dependency_cleanup` lists only what is cleared.
- `suite delete` now prints a human summary on success (stdout; stderr in JSON mode). Before, human mode printed
  nothing on success. A failed cleanup step (`ApplyAsync` error) gives status `failed`, exit 1, and the error in
  `warnings`.
- `validate`'s JSON `errors` now include the parse errors (`file`, `[CODE] message`); `valid` is unchanged.
- `update-skills` clears the legacy `gitignore_offered` after migrating it (it is no longer written); `init` no longer
  touches the skills manifest for gitignore.

**F8 (analyze L1).** Test infrastructure, no production change: proven by the full run having 0 failures
(`ProfileFormatLoaderTests` fails on 23aa08c) and by the `WorkingDirectory` collection definition existing, not by a new
failing row.

**Line endings.** No touched file has mixed CRLF/LF; edited files keep their working-copy form (CRLF), new files are LF.
`.gitattributes` re-checked-out the 8 tracked `*.sh` files as LF (`git ls-files --eol`: `i/lf w/lf attr/text eol=lf`).
