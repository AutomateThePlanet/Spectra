---
description: "Task list for Spec 086 — Git-Aware Relinking and History Bootstrap (Phase 2b)"
---

# Tasks: Git-Aware Relinking and History Bootstrap (Phase 2b)

**Input**: Design documents from `/specs/086-git-relink-sources/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required. T1–T7 run against throwaway git repos created by the tests and are skipped, with a reason, when
git is absent (`[GitFact]` / `[GitTheory]`). Fakes cover the cases that can't be set up portably. Write each story's
tests with or before its code.

**Organization**: by user story.
- US1 = relinking and RELINK triage;
- US2 = the skill;
- US3 = history bootstrap;
- US4 = `docs topology`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US4 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline: `dotnet build` and `dotnet test` on `086-git-relink-sources`. Expect Core 1118 / CLI 1721 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8, plus the 4 pre-existing warnings. Record them and `git --version` in the Notes of specs/086-git-relink-sources/tasks.md.
- [X] T002 [P] Create tests/Spectra.CLI.Tests/TestFixtures/GitFactAttribute.cs:
  - `GitFactAttribute : FactAttribute` and `GitTheoryAttribute : TheoryAttribute`, which set `Skip = "git is not installed; this test needs a real git repository"` when `git --version` fails (checked once, cached);
  - `GitAvailability.IsAvailable` for reuse.
- [X] T003 [P] Create tests/Spectra.CLI.Tests/TestFixtures/GitFixture.cs, which uses the test's own git with every write command allowed:
  - `Init(dir)` with user name, email and `core.autocrlf=false`;
  - `Commit(dir, message, DateTimeOffset? date)`, which sets `GIT_AUTHOR_DATE` / `GIT_COMMITTER_DATE`;
  - `Move(dir, from, to)` (`git mv`);
  - `AddSubmodule(parent, childRepo, path)` (`-c protocol.file.allow=always submodule add`), then commit;
  - `IndexLocks(root)`: every `index.lock` under any `.git` folder or `.git/modules/**`.
  Note: these fixture commands are the test's, not SPECTRA's.

---

## Phase 2: Foundational — read-only git access (blocks every story)

- [X] T004 [P] Create src/Spectra.Core/Git/GitProcessRunner.cs per research R1:
  - `IGitProcessRunner`;
  - `GitProcessResult(int ExitCode, byte[] Stdout, string Stderr, bool NotInstalled)`;
  - `ProcessGitRunner`, which uses `ProcessStartInfo("git")` with `ArgumentList`, the given environment (merged over the inherited one), optional stdin bytes, byte-level stdout capture, a 60 s timeout, and `Win32Exception` → `NotInstalled`.
- [X] T005 [P] Write tests/Spectra.Core.Tests/Git/GitReaderTests.cs against a fake runner that records calls:
  - each allowed subcommand passes, and it is invoked as `-C <dir> -c core.quotepath=false <sub> …` with `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0`;
  - `status`, `add`, `fetch`, `checkout` and `rev-list` throw before any process starts;
  - the reason mapping: NotInstalled → `no git`; "not a git repository" → `not in a git repository`; "detected dubious ownership" → `repository not readable (dubious ownership)`; another failure → `git failed: <first stderr line>`;
  - UTF-8 decoding of `über`.
- [X] T006 Implement src/Spectra.Core/Git/GitReader.cs:
  - the runner is a constructor parameter (`new GitReader(IGitProcessRunner runner)`), so there is no test hook in Core;
  - the allow-list `{rev-parse, log, show, ls-files, cat-file}`;
  - `RunAsync(dir, subcommand, args, stdin?) → GitResult(Ok, Unavailable, Stdout)` with `Text` (UTF-8);
  - the env and `-c core.quotepath=false` on every call;
  - no exception for git problems.
  Depends on T004.
- [X] T007 [P] Write tests/Spectra.Core.Tests/Git/RenameHistoryTests.cs (pure, no git):
  - parsing of the probed `-z` format (`\x1e<hash>\0<ct>\0\nR100\0old\0new\0…`), including non-ASCII paths and several renames per commit;
  - `ForwardCandidate` table: plain rename; chain a→b→c; old path renamed to two different places → `ambiguous rename`; `a → b`, then `a` re-created and `a → c` → `ambiguous rename` (analysis A1); chain ending at a non-existing path → `rename chain ends at a missing file`; no entry → `no rename found at 90% similarity`, or with `(shallow history)` appended when shallow; cycle guard;
  - `PathAt`: renames after a point applied newest→oldest give the old path.
- [X] T008 Implement src/Spectra.Core/Git/RenameHistory.cs per research R3:
  - `GitRename` and `RenameHistory.Parse(byte[] stdout)`;
  - `ForwardCandidate(path, Func<string,bool> existsInWorkingTree, bool shallow) → (Target?, Reason?)`;
  - `PathAt(renamesSincePoint, currentPath)`.
  Depends on T007.
- [X] T009 Implement src/Spectra.Core/Git/OwnerResolver.cs per research R2:
  - nearest existing directory;
  - one `rev-parse --show-toplevel --show-superproject-working-tree --is-shallow-repository HEAD` call, parsed defensively;
  - `GitOwner(Root, IsSubmodule, Shallow, Head)`;
  - cache per directory, also caching unavailable reasons;
  - `RelativePath(owner, fullPath)` with `/`.
  Also `RenameHistoryFor(owner, minSimilarity)`, cached per (root, threshold): one `log -l0 -M<t>% --diff-filter=R --name-status -z --format=%x1e%H%x00%ct HEAD` call. Depends on T006, T008.
- [X] T010 [P] Write tests/Spectra.CLI.Tests/Commands/Git/GitReaderIntegrationTests.cs, the reader-level part of T1 (`[GitFact]`). Build a repo with a submodule and a non-ASCII path, then call `GitReader` and `OwnerResolver` directly with a recording runner wrapping `ProcessGitRunner` (passed through the constructor):
  - every recorded subcommand is in the allow-list, and every env carries `GIT_OPTIONAL_LOCKS=0`;
  - every call carries `-c core.quotepath=false`, and `-z` where used;
  - `docs/über.md` → `docs/neu/über.md` round-trips through the rename scan;
  - after the reads, `GitFixture.IndexLocks` is empty, including in the submodule.
  The CLI-level part is T026 (analysis I1).

**Checkpoint**: Core git access builds; the pure and fake-runner tests pass.

---

## Phase 3: User Story 1 — Renamed doc files are relinked, not lost (Priority: P1) 🎯 MVP

**Goal**: RELINK in triage, then `relink-sources`, which rewrites the ref value, regenerates the index, moves the baseline key with the old fingerprint, and clears orphan status.

**Independent test**: in a repo, rename `docs/a.md` → `docs/guides/a.md`; triage shows RELINK; relink rewrites the ref; the next triage is UNCHANGED, or CHANGED if the rename commit edited the section.

- [X] T011 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Git/RenameTests.cs (T3, `[GitTheory]` table). Each row builds a repo history and asserts the triage result for one ref:
  - plain rename → RELINK `renamed to …`;
  - a rename with an edit under 90% similarity → REMOVED, reason exactly `not found; no rename found at 90% similarity`; with `--min-similarity 50` on `relink-sources` → relinked;
  - an edit above the threshold → RELINK;
  - a plain chain `a → b → c` → RELINK, relinked to `c`;
  - renamed to two places → REMOVED `not found; ambiguous rename`;
  - `a.md → b.md`, then `a.md` re-created and `a.md → c.md` → REMOVED `not found; ambiguous rename`, not relinked (a different history from the previous row; analysis A1);
  - renamed into a file later deleted → REMOVED `not found; rename chain ends at a missing file`;
  - `docs/old/a.md → docs/new/a.md`, with `docs/old/` no longer on disk → the owner is found from the nearest existing parent folder → RELINK to `docs/new/a.md` (FR-004);
  - a rename inside a submodule → RELINK with the parent-relative target;
  - a bare legacy ref under `source.local_dir`, target still under the source dir → the new ref stays source-dir-relative (bare) (FR-007);
  - a bare legacy ref under `source.local_dir`, target moved outside it → the new ref is repo-root-relative (FR-007);
  - a target outside the allowed roots → REMOVED `not found; renamed outside the allowed roots`;
  - the rename happens before any `--init`, in a test with an entry whose ref was therefore never baselined → RELINK, and the ref is not listed as unbaselined (analysis H1);
  - the rename happens before any `--init`, in a test with no entry at all → listed once under RELINK with `unbaselined: true` in the detail file, and not counted in the unbaselined total; the bucket counts add up to the number of tests (analysis H1).
- [X] T012 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Git/RelinkSourcesTests.cs (T4, T7):
  - only the ref value changes: plain, `"…"` and `'…'` quoted refs, and a list with an untouched second ref, each byte-identical apart from the ref;
  - the index is regenerated with the new ref;
  - the baseline key moves with its OLD fingerprint, so the next triage is CHANGED when the rename commit edited the section and UNCHANGED when it didn't;
  - the test already lists the target → the old ref is dropped and the target entry kept;
  - a ref never baselined (rename before any `--init`), in a test with an entry → the ref is rewritten, no key is created, and the next triage lists the new ref as unbaselined (analysis H1);
  - a test with no entry → the ref is rewritten and no entry is created (analysis H1);
  - `status: orphaned` with all refs resolving → the three keys are removed; with another ref still REMOVED → kept;
  - `--dry-run`: the `Snapshot()` of the whole tree (including `.git`) is unchanged;
  - `--min-similarity 49` and `101` → `INVALID_ARGUMENTS`;
  - two runs on the same repo state give byte-identical stdout and files (T7);
  - the human summary is on stderr in JSON mode.
- [X] T013 [US1] Add RELINK to Core:
  - `RefState.Relink` in src/Spectra.Core/Baseline/ChangeClassifier.cs;
  - `Bucket.Relink` with the highest precedence and `Wire()` → `RELINK` in src/Spectra.Core/Baseline/BucketRules.cs;
  - `ChangedItem.Target` (optional, in `Signature`) in src/Spectra.Core/Baseline/TriageGrouper.cs.
  Extend tests/Spectra.Core.Tests/Baseline/BucketAndGroupingTests.cs: the precedence matrix includes RELINK, and RELINK groups split by target.
- [X] T014 [US1] Implement src/Spectra.CLI/Baseline/RenameResolver.cs per research R3:
  - created lazily;
  - `CandidateFor(sourceRef, minSimilarity) → RelinkCandidate(Ref, NewRef, Target, Reason)`, which tries the `SourceRefResolver` candidate locations in order, uses `OwnerResolver` and `RenameHistoryFor`, checks the allowed roots, builds the new ref in the original style (anchor verbatim) and caches per ref;
  - src/Spectra.CLI/Baseline/GitAccess.cs: `internal static IGitProcessRunner? RunnerOverride` (the test hook) and `CreateReader()` → `new GitReader(RunnerOverride ?? new ProcessGitRunner())`, used by every CLI component that reads git (`RenameResolver`, `HistoryBootstrap`, the topology handler) (analysis U2).
  Depends on T009.
- [X] T015 [US1] Wire RELINK into src/Spectra.CLI/Baseline/TriageEngine.cs:
  - when a baselined ref's section is missing with reason `not found`, ask `RenameResolver`: a candidate → `RefState.Relink`, `why = ["renamed to <target>"]`, `ChangedItem.Target = target`; none → `Removed` with the reason `not found; <candidate reason>` (FR-013);
  - an unbaselined ref whose file is missing and has a candidate → `RefState.Relink` (no baseline fingerprint), not listed as an unbaselined ref (FR-012, analysis H1);
  - tests with no entry (today listed as unbaselined and skipped): evaluate their refs for RELINK only; with a RELINK ref, the test gets a RELINK verdict and the same detail record as any triaged test (empty baseline refs, `current` filled for every ref) plus `unbaselined: true`, and is removed from `UnbaselinedIds`; otherwise unchanged (FR-012, research R4, analysis C6);
  - a baselined test whose only unbaselined refs are RELINK refs is removed from `PartialIds` (analysis A1);
  - assert that `unbaselined.full` (stdout and detail file), `counts.unbaselined`, the `partial` lists and the human UNBASELINED line agree, and that the no-entry RELINK test's detail record has `current` filled and `unbaselined: true` (analysis A1, C6);
  - `removed` lists only REMOVED refs; a missing file with no rename at the default threshold has the reason exactly `not found; no rename found at 90% similarity` (assert in T011);
  - `TriagePresenter` shows RELINK;
  - in tests/Spectra.CLI.Tests/Commands/Baseline/UpdateTriageCommandTests.cs:143, change the expected value of `RenamedHeadingOrDeletedFile_IsRemoved`'s deleted-file row from `"not found"` to `"not found; not in a git repository"` (the one deliberate change, FR-013, SC-007, research R9).
- [X] T016 [US1] In src/Spectra.CLI/Commands/Baseline/UpdateAckHandler.cs, refuse RELINK with `REF_REMOVED` and a message naming `spectra ai relink-sources` on both paths:
  - `--ids` (`RefuseRemovedAsync`): besides baselined missing refs, ask the rename resolver about every missing ref with no baseline key, including all refs of a test with no entry (today skipped at `:252-257`), and refuse any with a relink candidate; never create an entry for a test that still has a RELINK ref (analysis U1);
  - `--group`: add `RELINK` to the bucket check at `:188-193` (analysis C5).
  Rows in UpdateAckCommandTests:
  - `--ids` on a baselined test whose baselined ref was renamed;
  - `--ids` on a test with no entry whose ref was renamed before any `--init` → refused, and no entry is created (analysis U1);
  - `--ids` on a baselined test with a never-baselined missing ref that has a candidate → refused, the entry unchanged (analysis U1);
  - `--group` on a RELINK group;
  - `--group` on a RELINK group that contains a no-entry test → refused, with no `TRIAGE_STALE` (analysis C6).
- [X] T017 [US1] Implement src/Spectra.CLI/Baseline/TestRefRewriter.cs per research R6:
  - the scalar-level replacement in the `source_refs` raw entry, keeping the quoting style (escaping as needed, switching to double quotes when plain can't represent the value);
  - removal of a `- …` line for de-duplicated refs;
  - orphan clearing through `TestFileWriter` with `with { Status = null, OrphanedReason = null, OrphanedDate = null }`;
  - re-parse verification (expected `SourceRefs`, every other field equal), with a fallback to the canonical writer plus a warning.
- [X] T018 [US1] Implement src/Spectra.CLI/Commands/Baseline/RelinkSourcesCommand.cs and RelinkSourcesHandler.cs per research R5 and the contract:
  - options, validation and config (`LoadForWriteAsync(Source | Tests)`);
  - the lock, except on dry-run;
  - per suite: scan, then per test the candidates, the new ref list and the orphan decision (every ref's section resolves);
  - then write the files (`TestRefRewriter`, atomic), regenerate the index from a rescan, move the baseline keys (old fingerprint; keep an existing target key), and save with the scan;
  - JSON and the human summary; exit 0.
  Register it in src/Spectra.CLI/Commands/Ai/AiCommand.cs. Depends on T014, T017.

**Checkpoint**: US1 complete; renamed docs are relinked with change detection intact.

---

## Phase 4: User Story 2 — The update skill offers relinking first (Priority: P1)

- [X] T019 [P] [US2] In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs, assert that `spectra-update`:
  - contains `relink-sources` with `--dry-run`, then without it;
  - has a confirmation step ("only after the user says yes");
  - re-runs triage after relinking;
  - names RELINK in the bucket list;
  - never runs `relink-sources` without `--dry-run` before the confirmation (the first occurrence has `--dry-run`);
  - when the user says no, the skill reports the RELINK tests as left for later and goes on with the other buckets (US2 scenario 3);
  - the REMOVED step mentions `relink-sources --min-similarity` with the range 50–100 and `--dry-run` first (FR-024);
  - for a first-time setup, `relink-sources` comes before `source-baseline --init`: no `source-baseline --init` occurrence precedes the RELINK step in the first-time path (the Step 2 offer is conditioned on "no RELINK groups"), and the post-relink offer uses `--from-date`, with plain `--init` named as the fallback (FR-023, analysis H1, U2).
- [X] T020 [US2] Update src/Spectra.CLI/Skills/Content/Skills/spectra-update.md. After Step 2 (summary), if any group is `RELINK`:
  1. run `spectra ai relink-sources --suite <suite> --dry-run --output-format json --verbosity quiet`;
  2. show old → new per test, and the `not_relinked` reasons;
  3. ask the user;
  4. only on a yes, run it without `--dry-run`;
  5. re-run triage before Step 3; on no, report the RELINK tests as left for later.
  Add RELINK to the Buckets section and to the exit-code section (no new codes). In the REMOVED step, add (FR-024): when the reason is `not found; no rename found at 90% similarity`, a doc renamed with heavier edits can be found with `spectra ai relink-sources --suite <suite> --min-similarity <N> --dry-run` (N 50–100), then confirmed as above (analysis U1). In the first-time-setup path (FR-023, analysis H1, U2):
  - when triage has RELINK groups, Step 2 does not offer `source-baseline --init`;
  - the offer moves to after the RELINK step and becomes `spectra ai source-baseline --init --suite <suite> --from-date <date>`, so the bootstrap follows the renames back to the pre-rename content;
  - plain `--init` is the fallback when there is no git or the user declines a date;
  - with no RELINK groups, Step 2 is unchanged.
  Depends on T019.

---

## Phase 5: User Story 3 — Baselines from a past date (Priority: P2)

- [X] T021 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Git/HistoryBootstrapTests.cs (T5, `[GitFact]`):
  - commit a doc on a fixed past date, change a number in a later commit, run `--init --from-date <between>` → the entry holds the old content's fingerprint, and triage shows LOAD;
  - a doc renamed after the date → read from its old path;
  - a ref whose file, or anchor, didn't exist then → unbaselined with the reason, listed in `suites[].unbaselined`;
  - existing entries are untouched;
  - a test with one baselined ref and one unbaselined ref: `--from-date` fills only the unbaselined one, and the baselined fingerprint stays byte-identical (analysis C1);
  - `--from-ref <hash> --owner .` gives the same; other owners use current content, with a warning;
  - refusals (FR-018): `--force` with `--from-date`, `--from-ref` without `--owner`, `--owner` without `--from-ref`, `--from-date` / `--from-ref` without `--init`, both `--from-*`, a bad date → `INVALID_ARGUMENTS`; an unknown ref, or an `--owner` that isn't a repo → `GIT_REF_UNAVAILABLE` (exit 4); nothing written in any case;
  - `--dry-run` writes nothing.
- [X] T022 [US3] Implement src/Spectra.CLI/Baseline/HistoryBootstrap.cs per research R7:
  - bootstrap points per owner (`log -1 --format=%H --before=<date> HEAD`, or `rev-parse --verify --quiet <ref>^{commit}`);
  - renames since the point (`log -l0 -M90% --diff-filter=R --name-status -z --format=%x1e%H <point>..HEAD`) → `PathAt`;
  - content via one `cat-file --batch` process per owner (stdin `<point>:<path>`, `missing` detection);
  - section via `SectionExtractor.Extract`, then the fingerprint and `ComparisonText` snapshot;
  - reasons for unbaselined refs.
- [X] T023 [US3] Extend src/Spectra.CLI/Commands/Baseline/SourceBaselineCommand.cs and SourceBaselineHandler.cs:
  - `--from-date`, `--from-ref`, `--owner`, with validation;
  - with `--from-*`, compute entries through `HistoryBootstrap` and merge with `BaselineRecorder.Complete` (criteria from current content);
  - add `from` and `suites[].unbaselined` to the JSON, and the warnings.
  Depends on T022.

---

## Phase 6: User Story 4 — Repository topology (Priority: P3)

- [X] T024 [P] [US4] Write tests/Spectra.CLI.Tests/Commands/Git/TopologyTests.cs (T2):
  - `[GitFact]`: docs in the main repo, a submodule and a nested submodule, plus a doc in a temp folder outside git, referenced through `source.local_dir` → owners with `is_submodule`, `head`, `shallow` and `referenced_docs`, and the unowned doc with `not in a git repository`;
  - a fake runner, set through `Spectra.CLI.Baseline.GitAccess.RunnerOverride`, returning git's dubious-ownership stderr for one owner → listed as unowned with `repository not readable (dubious ownership)`.
- [X] T025 [US4] Implement src/Spectra.CLI/Commands/Docs/DocsTopologyCommand.cs and DocsTopologyHandler.cs per research R8 (`LoadForReadAsync(Source | Tests)`, JSON plus the human table, exit 0), and register `topology` in src/Spectra.CLI/Commands/Docs/DocsCommand.cs.

---

## Phase 7: Polish, guards, performance, docs

- [X] T026 Write tests/Spectra.CLI.Tests/Commands/Git/GitCliReadOnlyTests.cs, the CLI-level part of T1 (`[GitFact]`; analysis I1). In a repo with a submodule, run `update-triage`, `update-ack` (on a test with a RELINK ref), `relink-sources --dry-run`, `relink-sources`, `source-baseline --init --from-date` and `docs topology` through the CLI, with a recording runner set via `Spectra.CLI.Baseline.GitAccess.RunnerOverride`:
  - every recorded subcommand is in the allow-list, and every env carries `GIT_OPTIONAL_LOCKS=0`;
  - after each command, `GitFixture.IndexLocks` is empty;
  - `git status --porcelain`, run by the test itself, shows only SPECTRA's own file changes, and none under `.git`.
- [X] T027 Write tests/Spectra.CLI.Tests/Commands/Git/NoGitTests.cs (T6):
  - with `Spectra.CLI.Baseline.GitAccess.RunnerOverride` reporting NotInstalled, triage on a workspace with a deleted doc gives REMOVED with the reason `not found; no git` and no RELINK; `relink-sources` reports `no git` for every missing ref and changes nothing; `--from-date` uses current content with a warning;
  - re-run the 083/084/085 guard classes unchanged (`InitTemplateWorkspaceTests`, `BaselineInitTemplateTests`, `ConfigRuleMatrixTests`) and confirm green.
- [X] T028 Write tests/Spectra.CLI.Tests/Commands/Git/RelinkPerformanceTests.cs (SC-001):
  - 500 docs committed, then all renamed in one commit (`git mv` into a new folder), and 2,500 tests in 10 suites (5 refs per doc); 5 suites baselined before the rename, 5 never baselined (no entries);
  - `relink-sources --all-suites --dry-run` takes under 30 s;
  - `update-triage --all-suites --dry-run` lists all 2,500 tests as RELINK, including the 1,250 with no entry (`unbaselined: true` in the detail file, not counted in the unbaselined total) (analysis H1);
  - the recording runner (through `Spectra.CLI.Baseline.GitAccess.RunnerOverride`) shows exactly one `--diff-filter=R` scan per owner;
  - all 2,500 tests are listed as relinked.
- [X] T029 [P] Update docs/cli-reference.md:
  - `relink-sources` (options, output, reasons, what it writes);
  - RELINK in the triage state table and the precedence;
  - `source-baseline --from-date` / `--from-ref` / `--owner` (rules, refusals, unbaselined reasons);
  - `docs topology`;
  - a "Git access" section (read-only subcommands, no locks, never required, the reasons).
- [X] T030 [P] Update docs/usage.md: relinking renamed docs in the update workflow (dry run, confirm, then content review), and bootstrapping from a past date when adopting.
- [X] T031 [P] Update CHANGELOG.md with an "Added (Spec 086 — git-aware relinking, Phase 2b)" subsection under `[2.6.0]`, before `### Notes`.
- [X] T032 [P] Update CLAUDE.md: add a "Recent Changes" entry for 086 at the top, in the 084 style, with the counts from T033, and add the new commands to the Commands block.
- [X] T033 Run the full `dotnet build --no-incremental` (no new warnings beyond T001) and `dotnet test`, and record the counts in the Notes.
- [X] T034 Run the quickstart.md steps 1–9 with the built CLI in a scratch git repo, including the `index.lock` and `git status` checks, and record the results in the Notes.
- [X] T035 Fill in the Notes (counts, deviations, quickstart results, the changed 084 assertion), mark every task [X], and stop. Ask the user before committing as `fix(086): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T003) → **Foundational** (T004–T010; T004 → T006 → T009; T007 → T008 → T009; T010 is the reader-level T1).
- **US1** (T011–T018) needs Foundational:
  - T013 → T015;
  - T014 → T015, T018;
  - T017 → T018.
- **US2** (T019–T020) needs US1's command name and output.
- **US3** (T021–T023) needs Foundational (`OwnerResolver`, `RenameHistory`), and is independent of US1's relink code.
- **US4** (T024–T025) needs Foundational only.
- **Polish** (T026–T035) needs everything.
- T026 (the CLI-level part of T1) runs after US1, US3 and US4 (analysis I1).

## Parallel Examples

```text
# Foundational, first wave:
T004 GitProcessRunner   T005 GitReaderTests   T007 RenameHistoryTests   (T002/T003 fixtures)
# After Foundational, the stories are independent:
US1 (T011–T018)   US3 (T021–T023)   US4 (T024–T025)
# Docs at the end:
T029 cli-reference   T030 usage   T031 CHANGELOG   T032 CLAUDE.md
```

## Implementation Strategy

1. **MVP = Foundational + US1 + US2.** Renamed docs are relinked, with confirmation in the skill. This fixes the
   motivating case (about 1,100 refs).
2. **US3:** history bootstrap for teams adopting the baseline.
3. **US4:** the topology diagnostic.
4. **Polish:** the no-git guard, performance, docs, the quickstart, then commit on approval.

## Notes

- **Baseline (T001)** on `086-git-relink-sources` from `2ea8f16`: Core 1118 / CLI 1721 (1 known `ProfileFormatLoaderTests`
  failure) / Execution 228 / Integration 8; 4 pre-existing warnings (CS0108 ×2, CS8714, CS8621). `git version
  2.53.0.windows.3`.
- **Final (T033)**: `dotnet build --no-incremental` — 0 errors, the same 4 warnings, no new ones. `dotnet test` — Core
  1165 (+47) / CLI 1783 (+62; the same 1 known failure) / Execution 228 / Integration 8; 0 skipped (git present).
- **Performance (T028)**: `relink-sources --all-suites --dry-run` on 500 docs renamed in one commit and 2,500 tests
  (1,250 never baselined): 1.95 s, 2 git calls (one `rev-parse`, one rename scan). Triage lists all 2,500 as RELINK.
- **Quickstart (T034)**, built CLI in a scratch repo, all as documented:
  1–2. init, baseline, `git mv` + 10GB→50GB in one commit;
  3. triage `RELINK 1 … renamed to docs/guides/a.md`;
  4. dry run shows `docs/a.md#Quotas → docs/guides/a.md#Quotas`, `git status` empty;
  5. relink: diff is the one ref line + the baseline key (+ `_index.json`, untracked in this hand-made workspace);
     triage `LOAD numbers: 10gb→50gb`;
  6. no `index.lock`; `git status` shows only test-cases files;
  7. baseline deleted, `--init --from-date 2026-03-15` → triage `LOAD numbers: 10gb→50gb`;
  8. topology `. repository <head> 1 doc(s)`;
  9. copy without `.git`: `REMOVED … not found; not in a git repository`.
- **The changed 084 assertion** (FR-013, SC-007): `UpdateTriageCommandTests.RenamedHeadingOrDeletedFile_IsRemoved`'s
  deleted-file row now expects `not found; not in a git repository` (was `not found`). No other existing test changed;
  `BaselineReviewFixTests`' captured 084 output still matches byte for byte.
- **Deviations / decisions:**
  - T016's rows live in `tests/Spectra.CLI.Tests/Commands/Git/UpdateAckRelinkTests.cs` (with the other git tests),
    not in `UpdateAckCommandTests`.
  - T7 compares `_index.json` without `generated_at` (the index stamps its generation time); test files, baseline,
    relink stdout and triage stdout are byte-identical.
  - `--from-date` with a date only (`2026-03-15`) means the end of that day, UTC, inclusive.
  - Git stdout is decoded as UTF-8 with replacement (never throws), not strict, so a git problem can never escape as
    an exception (FR-003).
  - A RELINK bucket count of 0 is left out of the human count line, so a workspace without renames prints exactly as
    under 085; `source-baseline`'s `from` / `suites[].unbaselined` appear only with `--from-*` for the same reason.
  - `RenameResolver`: when a bare ref's locations give different reasons, one from a location that found a rename
    (ambiguous, chain, outside the roots) wins over `no rename found`.
  - `relink-sources`' `not_relinked[].reason` is the bare reason; only triage prefixes `not found; `.
  - Analysis T1 (T019 testability) is implemented as: every `source-baseline --init` before Step 2a sits in a
    paragraph containing "no `RELINK` group", and the first one after Step 2a has `--from-date`. Analysis D1 (the
    spec's Required Tests table) was left as it is; the tasks cover the H1/U1 cases.
