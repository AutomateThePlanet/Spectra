---
description: "Task list for Spec 085 — Review Fixes for Spec 084"
---

# Tasks: Review Fixes for Spec 084 — Source Change Baseline

**Input**: Design documents from `/specs/085-review-fixes-084/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-behaviour-changes.md, quickstart.md.

**Tests**: Required. Every fix needs a regression test that fails on `2a43298` and passes after (T1–T9 in the spec).
Write each fix's tests first and confirm they fail before the fix. T2, T9 and the all-parsed half of T4 are guards:
they pass before and after.

**Organization**: by user story.
- US1 = F1 (anchors);
- US2 = F2 + F3 (pruning, dead refs);
- US3 = F4 (meaning flips);
- US4 = F5 (repairs keep the baseline);
- US5 = F6 (console encoding).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline: `dotnet build` and `dotnet test` on `085-review-fixes-084`. Expect Core 1025 / CLI 1699 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8, plus the 4 pre-existing compiler warnings. Record the result in the Notes of specs/085-review-fixes-084/tasks.md.

---

## Phase 2: Foundational

- [X] T002 Add `public static string Clean(string? text)` to src/Spectra.Core/Text/TextNormalizer.cs: today's `Normalize` body without the final `ToLowerInvariant()`. Make `Normalize(text)` return `Clean(text).ToLowerInvariant()`, so there is exactly one stripping pipeline. In tests/Spectra.Core.Tests/Text/TextNormalizerTests.cs, add a theory asserting `Normalize(x) == Clean(x).ToLowerInvariant()` for every input of the existing tables, plus `Clean("## **Bold** <b>x</b>")` keeping case.

**Checkpoint**: `Normalize` is unchanged; all existing normaliser tests pass.

---

## Phase 3: User Story 1 — Headings with markup or explicit IDs resolve (Priority: P1) 🎯 MVP

**Goal**: one matcher: text key → explicit ID → GitHub duplicate suffix. It serves both extraction and coverage, and `no_tests` shows cleaned text.

**Independent test**: the T1 table resolves every heading form to the right section, and T3 shows extraction and coverage agree.

- [X] T003 [P] [US1] Add the T1 matching table to tests/Spectra.Core.Tests/Baseline/SectionExtractorTests.cs. Each row is (document, anchor, expected outcome, expected heading line, expected rule):
  - GitBook `## Create customer records <a href="#create-customer-records" id="create-customer-records"></a>` with `#create-customer-records`;
  - `## Title {#custom-id}` with `#custom-id` (explicit ID) and with `#title` (text);
  - `## Title {: #custom-id}`;
  - `## [Payments](payments.md)` with `#payments`;
  - two `## Setup` headings: `#setup` → first, `#setup-1` → second; three: `#setup-2` → third;
  - `## Overview {#intro}` with `#intro` → explicit ID;
  - rule order: `## Intro` plus `## Overview {#intro}` with `#intro` → `## Intro` (text beats ID);
  - `## Step-2` with `#step-2` → text match, not rule 3;
  - `#setup-0`, `#setup-a`, and `#setup-5` with 2 Setups → AnchorNotFound;
  - `## Config {key}` keeps the braces as text: `#config-key` matches;
  - `id='single'` quotes;
  - plain, emphasised and inline-code headings unchanged.
  Assert the section text, the outcome and `SectionExtractor.Match(...).Rule`.
  Also add FR-001a rows: `SectionExtractor.ComparisonText` of `## Quotas {#quotas}\ntext` equals that of `## Quotas\ntext`; the same for `{: #quotas}` and `{.cls}`; and for a sub-heading's block inside a parent section. A `{#…}` line inside a fenced code block is kept.
- [X] T004 [P] [US1] Add T3 to the same file: for every T1 row that resolves, `CoveredHeadings(headings, anchor)` starts at the heading `Match` picked, and is empty when `Extract` gives AnchorNotFound. Also: `Headings(...)[i].CleanText` is `Create customer records` for the GitBook heading, `Title` for `## Title {#custom-id}`, and `Payments` for the link heading.
- [X] T005 [P] [US1] Add T2 to the same file: a fixture with plain, `**bold**`, `*em*`, `_em_` and `` `code` `` headings, each referenced in the GitBook, GitHub and generator styles. A private copy of the 084 rule (first heading whose `AnchorKey.Make(rawText)` equals `AnchorKey.FromAnchor(anchor)`) and the new `Extract` must give identical section text and `SourceFingerprint.OfSection` for every ref.
- [X] T006 [US1] Implement F1 in src/Spectra.Core/Baseline/SectionExtractor.cs per research R1:
  - `MarkdownHeading` gains body properties computed from `Text`: `CleanText`, `TextKey` and `ExplicitIds`. The attribute-block regex is `\s*\{:?\s*([#.][^}]*)\}\s*$`, and the id regex is `\bid\s*=\s*(?:"([^"]*)"|'([^']*)')`, case-insensitive;
  - new `HeadingMatch` record and `MatchRule` enum;
  - `public static HeadingMatch? Match(IReadOnlyList<MarkdownHeading>, string anchor)` with the three rules: the decoded anchor `^(?<base>.+)-(?<n>[1-9]\d*)$` and at least n+1 text-key matches gives the (n+1)th;
  - `Extract` and `CoveredHeadings` both use `Match`; `SectionLookup.Ambiguous` comes from rule 1.
  - FR-001a: `public static string ComparisonText(string rawSection)` = `TextNormalizer.Normalize` of the section after removing the trailing attribute block from every ATX heading line outside fences. src/Spectra.CLI/Baseline/BaselineContext.cs uses it for the current comparison text (it replaces the direct `TextNormalizer.Normalize(raw)` call), so snapshots and current text agree.
  Remove the two old key comparisons. Depends on T002.
- [X] T007 [US1] Switch `no_tests` to `CleanText` in src/Spectra.CLI/Baseline/TriageEngine.cs. Add a CLI test in tests/Spectra.CLI.Tests/Commands/Baseline/BaselineReviewFixTests.cs (new class, `[Collection(CriteriaCommandsCollection.Name)]`, using `BaselineTestWorkspace`):
  - an unreferenced `## Archive <a id="archive"></a>` in a changed doc is listed as `Archive`;
  - a test referencing `#custom-id` on `## Title {#custom-id}` is UNCHANGED after `source-baseline --init` (under 084 it stays partial, because the ref is never baselined).
- [X] T008 [US1] Add T9 to BaselineReviewFixTests.cs:
  - write a workspace and baseline, re-save it unchanged (`update-ack` of an unchanged test), and assert the baseline file is byte-identical;
  - triage of the unchanged workspace (plain, emphasised and inline-code refs) equals the 084 output, captured as follows:
    1. create the `2a43298` worktree now (and reuse it for T027);
    2. build it, and run the same fixture through the 084 `spectra` there;
    3. paste its stdout into the test verbatim as the expected string;
    4. record the command, the commit and the capture date in the Notes.
    Never hand-write it;
  - the FR-022 cases, each of which must fail on `2a43298`:
    - (a) never resolved: tests referencing an `<a … id>` heading, a link heading and a `{#id}` heading, baselined with `--init`, are partial, and after a second `--init` they are UNCHANGED (on 084 they stay partial);
    - (b) plain → markup: tests baselined on plain `## Quotas` headings, after the heading becomes `## Quotas <a id="quotas"></a>`, `## [Quotas](quotas.md)` and `## Quotas {#quotas}`, are COSMETIC and auto-acked in the next triage, then UNCHANGED (on 084, REMOVED).

**Checkpoint**: T1–T3 and T9 pass; no existing baseline or triage test changes its expectations.

---

## Phase 4: User Story 2 — Baselines are not lost or loosened (Priority: P1)

**Goal**: F2 (prune only when all test files parsed, decided in the store) and F3 (rebuilds keep dead fingerprints).

**Independent test**: T4 across the five writers; T5 via `ingest-update` and `source-baseline --init --force`.

- [X] T009 [P] [US2] Add T4 to BaselineReviewFixTests.cs. Seed a suite with tests TC-1 and TC-2 and a baseline, then overwrite TC-2.md with unparseable frontmatter. For each writer, assert that after it saves, `tests.TC-2` is still in `_source-baseline.json` and a warning (JSON `warnings` or stderr) names `test-cases/storage/TC-2.md` and says nothing was pruned. The writers:
  1. `ingest-tests` (new TC-3);
  2. `ingest-update --test-id TC-1`;
  3. `ingest-update --all` (staged TC-1);
  4. `update-triage` after a cosmetic edit (auto-ack);
  5. `update-ack --ids TC-1`;
  6. `source-baseline --init`.
  Also assert the warning in human mode as a `Warning:` line on stderr, for at least `update-ack` and `source-baseline --init` (not only the ingest commands).
  Then fix TC-2, delete TC-3's file, run `update-ack --ids TC-1`, and assert TC-3's entry is pruned.
- [X] T010 [P] [US2] Add T5 to BaselineReviewFixTests.cs:
  - TC-1 refs `docs/a.md#Gone` and `docs/a.md#Kept` and links `AC-X-001`; baseline it, then delete the `## Gone` section and the criterion;
  - rebuild via `ingest-update --test-id TC-1` (an unrelated title edit) → the next triage has TC-1 REMOVED with `docs/a.md#Gone`, and CRITERIA_REMOVED for `AC-X-001` is in `removed`;
  - the same via `source-baseline --init --force`;
  - then ingest an edit that drops `#Gone` and the criterion → the entry lacks both keys, and TC-1 is no longer in `removed`.
- [X] T011 [US2] Implement F2 in src/Spectra.CLI/Baseline/BaselineStore.cs per research R2:
  - `SuiteScan` record and `ScanSuiteAsync(repoRoot, testsPath, suite, ct)`;
  - `SaveSuiteAsync(testsPath, suite, baseline, SuiteScan scan, List<string> warnings, ct)` prunes to `scan.Ids` only when `scan.AllParsed`. Otherwise it adds `"{files} could not be parsed; no baseline entries were pruned in suite '{suite}'."`, with files joined by `, `, repo-relative, ordinal order.
  Remove the `liveIds` overload.
- [X] T012 [US2] Rewire every caller to the scan:
  - `BaselineCommandIO.LoadSuiteTestsAsync` returns the `SuiteScan` (keeping its per-file parse warning), and triage, ack and init pass it to `SaveSuiteAsync` with their warnings list;
  - `BaselineRecorder.RecordPersistedAsync` scans after persisting and takes a `List<string> warnings`;
  - src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs and IngestUpdateCommand.cs collect those warnings, add `"warnings": [...]` to their success JSON (always present), and print `Warning:` lines on stderr;
  - all five writers (`ingest-tests`, `ingest-update`, `update-triage` auto-ack, `update-ack`, `source-baseline --init`) print late warnings (those raised after config loading, such as parse and prune warnings) as `Warning:` lines on stderr in human mode, as config warnings already are, and keep them in the JSON `warnings`. Add a `BaselineCommandIO.PrintLateWarnings(warnings, alreadyPrinted)` helper so a config warning isn't printed twice.
  Depends on T011.
- [X] T013 [US2] Implement F3 in src/Spectra.CLI/Baseline/BaselineRecorder.cs per research R3. `Replace(baseline, TestCase test, ComputedEntry computed, BaselineWriteSummary summary)` copies the previous fingerprint for each ref in `test.SourceRefs`, and each ID in `test.Criteria`, that is missing from `computed` but present in the previous entry. Update the callers in SourceBaselineHandler, UpdateAckHandler and RecordPersistedAsync.

**Checkpoint**: T4 and T5 pass; the 084 writer tests (`IngestBaselineRecordingTests`, `SourceBaselineCommandTests`, `UpdateAckCommandTests`) stay green.

---

## Phase 5: User Story 3 — Meaning flips are LOAD (Priority: P2)

- [X] T014 [P] [US3] Add T6 rows to tests/Spectra.Core.Tests/Baseline/ChangeClassifierTests.cs:
  - "Users can delete items." → "Users can't delete items." → LOAD, `modal: ∅→can't`;
  - the same with `can’t` → LOAD, `modal: ∅→can't`;
  - "Refunds apply to all items." → "Refunds apply to all items except gift cards." → LOAD;
  - "Sessions always expire." → "Sessions never expire." → LOAD, and the reverse → LOAD;
  - "Click the button." → "Click the link." stays LIGHT;
  - one row each for `none`, `without`, `unless`, `any`, `every`.
- [X] T015 [US3] Implement F4 in src/Spectra.Core/Baseline/ChangeClassifier.cs per research R4: the extended `ModalWords`, and a `Fold` that maps `’` and `‘` to `'` for membership and for `why`. `WordDiff` is unchanged.

---

## Phase 6: User Story 4 — Repairs keep the baseline (Priority: P2)

- [X] T016 [P] [US4] Add T7 to BaselineReviewFixTests.cs:
  - TC-1 refs `#Quotas`; baseline it, then change 10GB → 50GB (a LOAD change is pending);
  - stage a repair to `.spectra/updates/storage/updated-TC-1.json` that also adds a ref `#Sharing`;
  - `ingest-update storage --all --keep-baseline` → the JSON `baseline.mode == "keep"`, the next triage still shows TC-1 LOAD on `#Quotas`, and the entry has a `#Sharing` key;
  - the same through `--test-id TC-1 --from … --keep-baseline`;
  - without the flag, the same edit makes TC-1 UNCHANGED (`mode == "rebuild"`).
- [X] T017 [P] [US4] Add the F5 skill assertions in tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs:
  - every line in `spectra-generate` and `spectra-review-flagged` that contains `spectra ai ingest-update` also contains `--keep-baseline`;
  - no line in `spectra-update` contains `--keep-baseline`.
- [X] T018 [US4] Implement F5:
  - `RecordMode { Rebuild, KeepBaseline }` and the mode parameter on `RecordPersistedAsync`, where KeepBaseline uses `Complete`;
  - `BaselineWriteSummary` gains a `Mode` emitted as `mode`;
  - src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs gains `--keep-baseline` for both forms (help text: "Keep the existing source-change baseline (critic repairs); only add entries for new refs and criteria"). `ingest-tests` uses Rebuild.
- [X] T019 [US4] Update the skills:
  - src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md: the repair batch command becomes `spectra ai ingest-update {suite} --all --keep-baseline --output-format json`, and the flags-table row for `ingest-update {suite} --all` mentions `--keep-baseline` (a repair fixes grounding, not doc drift, so the baseline must not move);
  - src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md Step 3: `spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json --keep-baseline --output-format json`.
  Depends on T017.

---

## Phase 7: User Story 5 — The console is not changed (Priority: P2)

- [X] T020 [P] [US5] Extend tests/Spectra.CLI.Tests/Commands/Baseline/Utf8StdoutTests.cs:
  - add a stderr case: a child process running `ai update-triage --suite nöpe --output-format json` (SUITE_NOT_FOUND names the suite); assert the stderr bytes are UTF-8 without a BOM, containing `nöpe`;
  - add `Program_DoesNotAssignConsoleOutputEncoding`, which locates `src/Spectra.CLI/Program.cs` by walking up from the test assembly to the folder containing `Spectra.slnx`, and asserts it contains no `Console.OutputEncoding =`. The message explains that this changes the user's terminal code page.
- [X] T021 [US5] Implement F6:
  - new src/Spectra.CLI/Infrastructure/ConsoleEncoding.cs with `UseUtf8ForRedirectedStreams()` per research R6;
  - src/Spectra.CLI/Program.cs calls it instead of assigning `Console.OutputEncoding`, with an updated comment.

---

## Phase 8: Polish

- [X] T022 [P] Update docs/cli-reference.md:
  - the anchor-matching rules (three rules, attribute blocks, cleaned `no_tests` text, the two residuals);
  - the extended LOAD word list and apostrophe folding;
  - pruning only when all files parse, with the warning;
  - full rebuilds keeping dead refs;
  - `--keep-baseline` on `ingest-update`, with `baseline.mode` and `warnings` in the ingest JSON;
  - the encoding sentence: redirected streams only, the console is untouched.
- [X] T023 [P] Update docs/usage.md: in the update section, say that critic repairs keep the baseline, so a pending doc change stays in triage, and that an unparseable test file keeps its baseline entry.
- [X] T024 [P] Update CHANGELOG.md with "Fixed (review fixes, Spec 085)" under `[2.6.0]`, before `### Notes`, covering F1–F6.
- [X] T025 [P] Update CLAUDE.md with a "Recent Changes" entry for 085 at the top, in the 084 style, with the test counts from T026.
- [X] T026 Run the full `dotnet build --no-incremental` (no new warnings beyond T001's) and `dotnet test`, and record the counts in the Notes.
- [X] T027 Fails-before check: use the detached worktree at `2a43298` (created in T008), copy in the new and changed test files, and run them. Record which fail per fix (each of F1–F6 needs at least one), and which guards pass (T2, T9, the T4 all-parsed half). Remove the worktree.
- [X] T028 Run the quickstart.md manual steps 1–7 with the built CLI in a scratch git repo, including the `chcp` check in `cmd.exe`, and record the results in the Notes.
- [X] T029 Fill in the Notes (counts, fails-before table, deviations, quickstart results), mark every task [X], and stop. Ask the user before committing as `fix(085): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- T001 → T002 → US1 (T006 needs `Clean`).
- **US1 (T003–T008)** goes before US2–US5. US2, US3 and US5 don't depend on US1 code, but they share test files, so do them in phase order.
- **US2:** T011 → T012 → T013.
- **US4** needs US2's `RecordPersistedAsync` signature (T012, T013).
- **US3** and **US5** are independent of the others.
- **Polish** comes last.

Test tasks run before their implementation tasks in each phase, and must fail before it.

## Parallel Examples

```text
# Tests first (different files):
T003/T004/T005 SectionExtractorTests   T014 ChangeClassifierTests   T017 SkillsManifestTests   T020 Utf8StdoutTests
# BaselineReviewFixTests (T007–T010, T016) share one file: write them in sequence.
# Docs:
T022 cli-reference   T023 usage   T024 CHANGELOG   T025 CLAUDE.md
```

## Implementation Strategy

1. **US1 first.** False REMOVED reports are the most visible failure.
2. **US2 next.** It is the data-loss class.
3. **US3, US4, US5.** Each is small and independent.
4. **Polish.** Docs, the fails-before evidence, the quickstart, then commit on approval.

## Notes

**Baseline (T001, `2a43298`):** Core 1025 / CLI 1699 (1 known failure: `ProfileFormatLoaderTests`) / Execution 228 /
Integration 8. Four pre-existing compiler warnings: CS0108 ×2, CS8714, CS8621.

**Final (T026), `dotnet build --no-incremental` + `dotnet test`:** Core 1118 / CLI 1721 (same 1 known failure) /
Execution 228 / Integration 8. No new compiler warnings.

**084 output capture (T008 / A1):**
- Command: `python specs/…/capture084.py <dir> <worktree>/src/Spectra.CLI/bin/Debug/net8.0/Spectra.CLI.dll`, i.e. the
  scratch script that writes the `BaselineReviewFixTests.Fixture` files and runs `source-baseline --init --all-suites`
  and then `update-triage --all-suites --no-auto-ack --output-format json`, before and after a 10GB → 50GB edit.
- Run on the `2a43298` build in a detached worktree. Captured 2026-10-05T11:51:44Z.
- Its stdout is pasted verbatim into `Expected084Unchanged` / `Expected084Changed`.

**Fails-before (T027).** The new and changed tests were run on the `2a43298` build, in a detached worktree since
removed.

| Fix | Fails on `2a43298` | Guards (pass on both) |
|---|---|---|
| F1 | `HeadingMatchTests` (doesn't compile: `MatchRule`, `Match`, `CleanText`, `ComparisonText` don't exist). `BaselineReviewFixTests`: `NoTests_ShowsCleanedHeadingText`, `CustomIdRef_IsBaselinedAndUnchanged`, `NeverResolvedRefs_…`, and `PlainHeadingThatGainsMarkup_…` ×3 (`<a id>`, link, `{#id}`) | `UnchangedWorkspace_TriagesExactlyAs084_…` (T9); the T2 guarantee rows (the file can't compile there, but the rule copy *is* 084's) |
| F2 | `UnparseableTestFile_KeepsItsEntry_UnderEveryWriter`, `PruneWarning_…` ×2. `WhenEveryFileParses_…` also fails there: its setup runs through the F2 bug, which prunes TC-102 | — |
| F3 | `IngestUpdateRebuild_KeepsDeadRefsReported`, `ForcedInit_KeepsDeadRefsReported` | `DroppingTheDeadRef_…` |
| F4 | `MeaningFlips_AreLoad`: 10 of 11 rows | the ordinary-word row (LIGHT) |
| F5 | `RepairBatch_WithKeepBaseline_…`, `SingleRepair_WithKeepBaseline_…` (unknown option), `UpdateWithoutKeepBaseline_…` (no `baseline.mode`), `RepairIngests_KeepTheBaseline` ×2 | `UpdateSkill_DoesNotKeepTheBaseline` |
| F6 | `Program_DoesNotAssignConsoleOutputEncoding` | `RedirectedStdout_IsUtf8`, `RedirectedStderr_IsUtf8` (084 was already UTF-8; the fix changes *how*) |
| `Clean` (T002) | `TextNormalizerTests.Clean_*` (doesn't compile) | — |

**Quickstart (T028), built CLI in a scratch git repo:**
1. After `--init`, all five anchor forms are UNCHANGED, with no REMOVED and no partial: GitBook `<a id>`, `{#custom-id}`, `[Payments](payments.md)`, `#setup-1`, and a plain heading.
2. With TC-102 corrupted and TC-103 deleted, a cosmetic edit's triage warns `TC-102.md could not be parsed; no baseline entries were pruned in suite 's'`, and both entries are still in the file.
3. "can delete" → "can’t delete" gives LOAD with `modal: ∅→can't`.
4. A repair through `ingest-update --test-id TC-105 --keep-baseline` gives `baseline.mode: "keep"`, and the LOAD change is still pending.
5. Redirected JSON is UTF-8 with no BOM and `→` intact.
6. `chcp` in `cmd.exe` around `spectra list` shows 437 before and after for **both** the 084 and the 085 build. In this harness the CLI's output is always redirected, so the console code-page side effect can't be reproduced here. FR-019's automated evidence is the source guard, which fails on `2a43298`.

**Deviations and notes:**
- **Analysis I4:** T003–T005 went into a new file, `tests/Spectra.Core.Tests/Baseline/HeadingMatchTests.cs`, instead of `SectionExtractorTests.cs`, which also removes the same-file `[P]` conflict.
- **Analysis I2:** rule 3 tests the suffix on the percent-decoded anchor, as research R1 says.
- **Late warnings** are printed as `Warning:` lines on stderr in **both** modes, the same as config warnings. JSON mode already uses stderr for the human summary.
- **The per-file parse warning stays alongside the prune warning:** "TC-102.md could not be parsed and was skipped" (084) is followed by "…; no baseline entries were pruned…" (085). Both name the file.
- **The ingest JSON's `warnings`** uses the ingest commands' existing default serializer, so `'` appears as `\u0027`. That matches the rest of their output.
- **No change was needed in `BaselineCommandIO` for test loading** beyond delegating to `BaselineStore.ScanSuiteAsync`, which is now the one scan.
