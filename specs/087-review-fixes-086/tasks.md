---
description: "Task list for Spec 087 — Review fixes for 086 (git-aware relinking)"
---

# Tasks: Review Fixes for Spec 086 — Git-Aware Relinking

**Input**: Design documents from `/specs/087-review-fixes-086/`: spec.md, plan.md, research.md, data-model.md,
contracts/cli-contract.md, quickstart.md.

**Tests**: Required. Every fix has a regression test that **fails on `ede6680`** and passes after. Each story writes
its tests first and records the failure on an `ede6680` worktree (research R6). Git-dependent tests use `[GitFact]` /
`[GitTheory]`; the real-repository casing test uses `[CaseInsensitiveGitFact]`.

**Organization**: one phase per fix, in spec priority order.
- US1 = F1, unchecked-out submodule;
- US2 = F2, git executable;
- US3 = F3, git environment;
- US4 = F4, casing;
- US5 = F5, bootstrap threshold and failures.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1–US5 from spec.md

---

## Phase 1: Setup

- [X] T001 Measure the baseline: `dotnet build` and `dotnet test` on `087-review-fixes-086`. Expect Core 1165 / CLI 1783 (1 known `ProfileFormatLoaderTests` failure) / Execution 228 / Integration 8, plus the 4 pre-existing warnings. Record them in the Notes of specs/087-review-fixes-086/tasks.md.
- [X] T002 [P] Create tests/Spectra.CLI.Tests/TestFixtures/CaseInsensitiveGitFactAttribute.cs: a `FactAttribute` that sets `Skip = GitAvailability.SkipReason` without git, and `Skip = "the file system is case-sensitive (Linux); covered by the comparer-seam tests"` on Linux.
- [X] T003 [P] Create tests/Spectra.CLI.Tests/TestFixtures/GitEnvironmentCollection.cs:
  - `[CollectionDefinition(Name, DisableParallelization = true)]` with `Name = "GitEnvironment"`;
  - `ScopedEnvironment : IDisposable`, which sets the given process environment variables and restores the previous values (including unset) in `Dispose`.
- [X] T004 Create the fails-before worktree for research R6: `git worktree add <scratchpad>/087-before ede6680`, outside the repository and never committed. Each story's "fails before" task copies its new test files there, builds, runs them, and records the result in the Notes; T042 removes it.

---

## Phase 2: Foundational

No shared blocking work: each fix's helper lives in its own story. US3 builds on US2's `CreateStartInfo` (T017, T018).

---

## Phase 3: User Story 1 — A submodule that isn't checked out is a setup problem (Priority: P1) 🎯 MVP

**Goal (F1, FR-001–FR-005)**: refs into a submodule without a checkout are REMOVED with `not found; submodule not checked out: <path> (run git submodule update --init)`. They are never RELINK, fold into one unchunked group per suite and submodule, are reported by `relink-sources` and the bootstrap, and the skill treats them as setup.

**Independent test**: clone a superproject without its docs submodule; triage shows one REMOVED group for the submodule with that reason.

- [X] T005 [P] [US1] Write tests/Spectra.Core.Tests/Git/GitlinkParseTests.cs for `OwnerResolver.ParseGitlinks(byte[], StringComparer)` over `ls-files -s -z` output (`<mode> <sha> <stage>\t<path>\0`):
  - `160000` entries are collected and `100644` / `100755` / `120000` ignored;
  - non-ASCII paths;
  - an empty input;
  - stage numbers other than 0 are still read;
  - the comparer is honoured (OrdinalIgnoreCase finds `Docs/Sub` for `docs/sub`).
- [X] T006 [P] [US1] Write tests/Spectra.CLI.Tests/Commands/Git/SubmoduleNotCheckedOutTests.cs (T1, `[GitFact]`, `CriteriaCommands` collection).
  Fixture: a `child` docs repository (`guide.md`, `setup.md`, each with a `## Setup` section), and a superproject in the workspace with `child` as the `docs/sub` submodule, three tests with refs `docs/sub/guide.md#Setup` / `docs/sub/setup.md#Setup`, baselined and committed. Then `git clone -q <super> <second workspace root>` without submodules (add `GitWorkspace.CloneInto(source, target)`: it empties the target root and clones; it is a fixture command). Rows:
  - in the clone, every ref is REMOVED with the reason exactly `not found; submodule not checked out: docs/sub (run git submodule update --init)` (`removed[]`, one entry per ref);
  - exactly one group for the suite, bucket REMOVED, ref item `{"ref":"docs/sub","state":"REMOVED","submodule":true}`, all three tests, even with `update.group_size` set to 2 in the clone's config (unchunked);
  - no RELINK, even though `child` renamed `guide.md` in a later commit;
  - `relink-sources --dry-run` lists each ref in `not_relinked` with the bare reason;
  - `update-ack --ids` on one of them → `REF_REMOVED`;
  - `source-baseline --init --from-date` for a test added without an entry → `suites[].unbaselined` with the bare reason;
  - the submodule folder deleted entirely (not just empty) → the same reason;
  - a test with refs into two unchecked-out submodules (`docs/sub`, `docs/other`) → its group's items name both;
  - nested: `docs/sub` checked out (`git submodule update --init` in the clone, a fixture command) but its own `inner` submodule not → reason `… docs/sub/inner …`;
  - a checked-out submodule: a rename inside it → RELINK; a file deleted inside it → `not found; no rename found at 90% similarity`;
  - in the baselined clone, with the three tests referencing TC-101 `[guide]`, TC-102 `[setup]`, TC-103 `[guide, setup]`, triage `notes` contains exactly `4 reference(s) from 3 test(s) point into submodule docs/sub, which isn't checked out (run git submodule update --init)` — both numbers asserted (analysis U1, A2);
  - a fresh clone of a superproject **with no baselines at all**: triage lists the tests as unbaselined, and its `notes` carries the same line (`4 reference(s) from 3 test(s) …`); `source-baseline --init` (plain) and `--init --from-date <date>` each put exactly that line in JSON `warnings` and on stderr as `Warning: …`, and leave those refs unbaselined (no key for them in `_source-baseline.json`).
- [X] T007 [P] [US1] In tests/Spectra.CLI.Tests/Skills/SkillsManifestTests.cs, assert the `spectra-update` Step 3 (FR-005):
  - it names `not found; submodule not checked out`;
  - it tells the user to run `git submodule update --init` themselves and re-run triage;
  - it says those tests are not listed for review;
  - the paragraph handling this reason contains no `spectra-delete` / `spectra delete`;
  - no fenced command in the skill runs `git submodule` (the instruction is inline prose only);
  - in the first-time setup, the `source-baseline --init` offer is withheld while triage's `notes` contain the `point into submodule` line: the paragraph that offers `--init` names that condition, and tells the user to initialise the submodule and re-run triage first (analysis U1).
- [X] T008 [US1] Implement in src/Spectra.Core/Git/OwnerResolver.cs:
  - `static IReadOnlySet<string> ParseGitlinks(byte[] stdout, StringComparer comparer)`;
  - `Task<IReadOnlySet<string>> GitlinksAsync(GitOwner owner, CancellationToken ct)`: one `ls-files -s -z` per owner, cached; a failure gives an empty set;
  - `Task<string?> UncheckedSubmoduleOfAsync(GitOwner owner, string fullPath, CancellationToken ct)`: the owner-relative path of the nearest parent folder of `fullPath` (or the folder itself) that is a gitlink and has no `.git` entry (file or directory) on disk.
  Depends on T005.
- [X] T009 [US1] In src/Spectra.CLI/Baseline/RenameResolver.cs:
  - `RelinkCandidate` gains `string? Submodule`;
  - `public static string SubmoduleReason(string path)` → `submodule not checked out: {path} (run git submodule update --init)`;
  - in `CandidateForAsync`, per location, after the owner is found and before any history, `UncheckedSubmoduleOfAsync`; a hit returns `RelinkCandidate(Ref, null, null, SubmoduleReason(p), p)` with `p` repo-root-relative (`/`), and wins over later locations.
  Depends on T008.
- [X] T010 [US1] Triage folding:
  - src/Spectra.Core/Baseline/TriageGrouper.cs: `ChangedItem` gains `bool Submodule` (signature `submodule:<path>=REMOVED`); a group whose items are all `Submodule` is not chunked;
  - src/Spectra.CLI/Baseline/TriageEngine.cs: `TriagedRef.Submodule` from the candidate; `Build` emits one `ChangedItem(false, p, "REMOVED", Submodule: true)` per distinct submodule instead of one per ref, with the reason in `why`;
  - src/Spectra.CLI/Commands/Baseline/UpdateTriageHandler.cs: the group ref item adds `"submodule": true`; `removed` stays per ref;
  - the per-submodule note (FR-004a): `TriageEngine.RunAsync` counts every missing ref whose candidate carries a submodule — baselined refs, unbaselined refs and refs of tests with no entry — and adds one line per submodule to the run's notes: `{N} reference(s) from {M} test(s) point into submodule {p}, which isn't checked out (run git submodule update --init)`, where N counts (test, ref) pairs (a ref listed by three tests counts 3) and M distinct tests (analysis A2). Put the formatter in one place (`RenameResolver.SubmoduleNote(path, refs, tests)`) for T011;
  - extend tests/Spectra.Core.Tests/Baseline/BucketAndGroupingTests.cs: submodule-only groups are not chunked at group size 2, and other groups still are.
  Depends on T009.
- [X] T011 [US1] Bootstrap and `--init` warning:
  - in src/Spectra.CLI/Baseline/HistoryBootstrap.cs `LocateAsync`, for a missing ref, run the same check (owner → `UncheckedSubmoduleOfAsync`) before `PathAt`; a hit → unbaselined with the bare `RenameResolver.SubmoduleReason(...)`;
  - in src/Spectra.CLI/Commands/Baseline/SourceBaselineHandler.cs (plain `--init`, `--from-date`, `--from-ref`): for every missing ref of the scope's tests, ask `context.RelinkCandidateAsync`; count those with a submodule per submodule — N as (test, ref) pairs, M as distinct tests, over the command's scope — and add one warning per submodule with the FR-004a line from `RenameResolver.SubmoduleNote` (JSON `warnings`, and a `Warning:` line on stderr through `PrintLateWarnings`). Those refs stay unbaselined.
  Depends on T008, T009.
- [X] T012 [US1] Update src/Spectra.CLI/Skills/Content/Skills/spectra-update.md Step 3:
  - entries whose reason starts with `not found; submodule not checked out` are a setup problem: name each submodule once, and tell the user to run `git submodule update --init` in the repository themselves (the skill never runs it) and then re-run triage;
  - don't list those tests for review, and don't offer `spectra-delete` for them;
  - add the reason to the Buckets / REMOVED explanation;
  - `git submodule update --init` appears only inline in prose, as an instruction for the user to run, never in a fenced block (analysis I1);
  - first-time setup (Step 2 / Step 2a): while triage's `notes` contain the `point into submodule … which isn't checked out` line, tell the user to initialise that submodule and re-run triage, and don't offer `source-baseline --init` until the note is gone (analysis U1).
  Depends on T007.
- [X] T013 [US1] Fails before: copy T005–T007 to the `ede6680` worktree, run them, and record which assertions fail (T005 does not compile: no `ParseGitlinks`; T006 and T007 fail at runtime) in the Notes.

**Checkpoint**: F1 complete.

---

## Phase 4: User Story 2 — SPECTRA never runs a git program from the workspace (Priority: P1)

**Goal (F2, FR-006–FR-008)**: one shared lookup gives the absolute path of git from absolute PATH entries only. Both git callers start that path, with `ArgumentList`.

**Independent test**: the lookup table never picks a current-folder or relative-entry candidate; neither caller uses the bare name.

- [X] T014 [P] [US2] Write tests/Spectra.Core.Tests/Git/GitExecutableTests.cs, a table over `GitExecutable.Resolve(entries, currentDirectory, isWindows, fileExists)` with fake paths:
  - a `git.exe` in the current folder and a later absolute entry → the later one;
  - entries `""`, `"."`, `"bin"`, `"..\\tools"`, `"C:tools"` (Windows) → skipped;
  - `"\"C:\\Program Files\\Git\\cmd\""` (quoted) → accepted;
  - the first of two absolute matches wins;
  - an absolute entry equal to the current folder → skipped;
  - Windows looks for `git.exe` only (a `git.cmd` / `git` there → no match);
  - non-Windows looks for `git`;
  - none → null.
- [X] T015 [P] [US2] Write tests/Spectra.CLI.Tests/Commands/Git/GitStartSourceGuardTests.cs (FR-008):
  - a source guard: src/Spectra.Core/Git/GitProcessRunner.cs and src/Spectra.Execution/Identity/UserIdentityResolver.cs contain no `ProcessStartInfo("git")`, no `FileName = "git"` and no `Arguments =`;
  - `[GitFact]`: `new ProcessGitRunner().CreateStartInfo(dir, ["-C", dir, "rev-parse"], GitReader.Environment)` has a fully qualified `FileName` equal to `GitExecutable.Path`, `UseShellExecute = false`, and the arguments in `ArgumentList`;
  - `UserIdentityResolver.CreateGitStartInfo()` (internal) has the same `FileName` and `ArgumentList` `["config", "user.name"]`.
- [X] T016 [US2] Implement src/Spectra.Core/Git/GitExecutable.cs per research R1: the pure `Resolve`, and `Path` (a `Lazy<string?>` over the PATH variable split on `Path.PathSeparator`, `Directory.GetCurrentDirectory()`, `OperatingSystem.IsWindows()` and `File.Exists`). Depends on T014.
- [X] T017 [US2] In src/Spectra.Core/Git/GitProcessRunner.cs:
  - add `public ProcessStartInfo? CreateStartInfo(string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string,string> env)` (null when `GitExecutable.Path` is null), with `FileName` = the absolute path, `ArgumentList`, no shell, and the env applied;
  - `RunAsync` uses it, and null → `GitProcessResult(..., NotInstalled: true)`.
  Depends on T016.
- [X] T018 [US2] In src/Spectra.Execution/Identity/UserIdentityResolver.cs:
  - add `internal static ProcessStartInfo? CreateGitStartInfo()` (absolute path, `ArgumentList` `config`, `user.name`, redirected output, no shell);
  - `GetGitUserName` uses it, and null → null, so the identity falls back to the OS user name as today.
  Depends on T016.
- [X] T019 [US2] Fails before: T014 and T015 on the `ede6680` worktree. The source guard fails (bare `"git"`, `Arguments =`); the rest does not compile. Record in the Notes.

---

## Phase 5: User Story 3 — An inherited git environment never redirects a call (Priority: P1)

**Goal (F3, FR-009–FR-011)**: every git process SPECTRA starts drops the redirecting `GIT_*` variables and keeps `GIT_OPTIONAL_LOCKS=0` / `GIT_TERMINAL_PROMPT=0`, through one helper.

**Independent test**: in a simulated pre-commit-hook environment, triage still finds a rename inside a submodule.

- [X] T020 [P] [US3] Write tests/Spectra.Core.Tests/Git/GitEnvironmentTests.cs for `GitEnvironment.Apply` over a dictionary:
  - every F3 name is removed, plus `GIT_CONFIG_KEY_0` / `GIT_CONFIG_VALUE_0` / `GIT_CONFIG_KEY_12`;
  - `PATH`, `HOME`, `GIT_AUTHOR_NAME`, `GIT_SSH_COMMAND` are kept;
  - the two settings are set (overriding inherited values);
  - `extra` is applied last;
  - `IsRedirecting("git_dir")` is true only when case-insensitive (`ignoreCase` parameter; the platform default is `OperatingSystem.IsWindows()`);
  - the default (no `ignoreCase` argument): `IsRedirecting("git_dir")` equals `OperatingSystem.IsWindows()` — case-insensitive on Windows, case-sensitive elsewhere (analysis C1).
- [X] T021 [P] [US3] Write tests/Spectra.CLI.Tests/Commands/Git/GitEnvironmentIsolationTests.cs (T3, `[Collection(GitEnvironmentCollection.Name)]`, `[GitFact]`). Variables are set with `ScopedEnvironment` only around the SPECTRA call:
  - with every F3 variable set, `ProcessGitRunner.CreateStartInfo(...).Environment` and `UserIdentityResolver.CreateGitStartInfo().Environment` contain none of them, and contain `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0` (the runner);
  - real repositories: a superproject with a checked-out `docs/sub` submodule and a baselined test on `docs/sub/guide.md#Setup`; `guide.md` is renamed and committed inside the submodule. With `GIT_DIR=<super>/.git` and `GIT_INDEX_FILE=<super>/.git/index` set (a simulated pre-commit hook), `update-triage` gives RELINK to `docs/sub/<new>`;
  - with `GIT_CONFIG_COUNT=1`, `GIT_CONFIG_KEY_0=user.name`, `GIT_CONFIG_VALUE_0=Injected` (and `GIT_CONFIG_PARAMETERS='user.name'='Injected'`), `UserIdentityResolver` (after `ClearCache`) does not return `Injected`.
- [X] T022 [US3] Implement src/Spectra.Core/Git/GitEnvironment.cs per research R2 (`RedirectingVariables`, `IsRedirecting(name, ignoreCase)`, `Settings`, `Apply`); `GitReader.Environment` returns `GitEnvironment.Settings`. Depends on T020.
- [X] T023 [US3] Call `GitEnvironment.Apply(info.Environment, env)` in `ProcessGitRunner.CreateStartInfo` (replacing the merge loop) and `GitEnvironment.Apply(info.Environment)` in `UserIdentityResolver.CreateGitStartInfo`. Depends on T017, T018, T022.
- [X] T024 [US3] Fails before: T020 and T021 on the `ede6680` worktree. The hook-environment triage gives REMOVED instead of RELINK, and the identity returns `Injected`. Record in the Notes.

---

## Phase 6: User Story 4 — Renames are found when only the casing differs (Priority: P2)

**Goal (F4, FR-012–FR-014)**: rename lookups and the path-at-point walk ignore case on Windows and macOS (one distinct match → git's casing; more than one → `ambiguous rename`); Linux stays exact.

**Independent test**: the comparer-seam tables on every OS; the real-repository case test on Windows/macOS.

- [X] T025 [P] [US4] Write tests/Spectra.Core.Tests/Git/RenameHistoryCaseTests.cs (comparer seam: `StringComparer.OrdinalIgnoreCase` and `Ordinal` passed explicitly).
  - Ignoring case:
    - `docs/Store-Manager/Setup.md` vs history `docs/store-manager/setup.md → docs/guides/setup.md` → target `docs/guides/setup.md`;
    - two history paths differing only in case (`docs/Setup.md → x.md`, `docs/setup.md → y.md`) → `ambiguous rename`;
    - an exact match plus an other-case match → `ambiguous rename`;
    - a chain whose second step was recorded with other casing → followed;
    - `PathAt` maps a different-case current path to git's old path, and gives `ambiguous rename` for two distinct case-variant `New` values.
  - Exact (`Ordinal`): the different-case lookups find nothing (`no rename found …`).
  Update tests/Spectra.Core.Tests/Git/RenameHistoryTests.cs for the `PathAt` tuple result.
- [X] T026 [P] [US4] Write tests/Spectra.CLI.Tests/Commands/Git/CaseRenameTests.cs (`[CaseInsensitiveGitFact]`, `CriteriaCommands` collection):
  - git records `docs/store-manager/setup.md`; a test refs `docs/Store-Manager/Setup.md#Quotas` (it resolves on this file system), baselined; the file is renamed to `docs/guides/Setup-Guide.md` → triage RELINK `renamed to docs/guides/Setup-Guide.md`, and `relink-sources` writes `docs/guides/Setup-Guide.md#Quotas` (git's casing);
  - history with `docs/Setup.md → docs/x.md` then `docs/setup.md → docs/y.md` and a ref `docs/SETUP.md#Quotas` → REMOVED `not found; ambiguous rename`;
  - bootstrap: the ref casing differs from git's, and the file was renamed after the date → `--from-date` baselines the old content (triage LOAD on the number change).
- [X] T027 [US4] Implement:
  - src/Spectra.Core/Git/GitPathComparer.cs (`Platform`);
  - in src/Spectra.Core/Git/RenameHistory.cs, the comparer parameter (default `Platform`), a case-aware `Resolve(path)` (distinct exact keys equal under the comparer: 0 → no rename, more than 1 → ambiguous), used at the start and at each chain step of `ForwardCandidate`, and `PathAt(renames, path, comparer) → (string? Path, string? Reason)` per research R4;
  - `OwnerResolver.RenameHistoryAsync` passes `GitPathComparer.Platform`.
  Depends on T025.
- [X] T028 [US4] In src/Spectra.CLI/Baseline/HistoryBootstrap.cs, use the `PathAt` tuple: an ambiguous result → unbaselined `ambiguous rename`. Depends on T027.
- [X] T029 [US4] Fails before: T025 and T026 on the `ede6680` worktree. T026 gives REMOVED instead of RELINK on Windows; T025 does not compile (no comparer parameter). Record in the Notes.

---

## Phase 7: User Story 5 — The bootstrap uses the chosen threshold and reports a failed history read (Priority: P2)

**Goal (F5, FR-015–FR-017)**: `source-baseline --from-*` accepts `--min-similarity` (same validation as `relink-sources`); a failed "renames since" scan leaves the refs unbaselined with `git history unavailable: …`.

**Independent test**: a heavily edited rename is followed back with `--min-similarity 60`; a forced scan failure names itself.

- [X] T030 [P] [US5] Write tests/Spectra.CLI.Tests/Commands/Git/BootstrapThresholdTests.cs (`[GitFact]`/`[GitTheory]`, `CriteriaCommands` collection):
  - a doc committed on Day1, then renamed with edits that git scores between 60% and 89% on Day3 (measure the fixture's score with the test's own `git log -M60%`, and assert the setup is in range). `--from-date <between>` → the ref is unbaselined `file did not exist at <hash>`; with `--min-similarity 60` → baselined from the old path, and triage is LOAD on the number change;
  - `GitAccess.RunnerOverride` = a `FakeGitRunner` failing every `log` call whose last argument ends in `..HEAD` (`exit 128`, `fatal: bad object`) and passing others to the real runner → the refs are unbaselined with a reason starting `git history unavailable: git failed: fatal: bad object`;
  - validation table: `--min-similarity 49` and `101` with `--from-date` → `INVALID_ARGUMENTS` with exactly the `relink-sources` message; `--min-similarity 60` without `--from-*` → `INVALID_ARGUMENTS` `--min-similarity is only used with --from-date or --from-ref.`; nothing written in any case;
  - JSON `from.min_similarity` is 60 when given and 90 by default with `--from-date`.
- [X] T031 [US5] Add `BaselineCommandIO.ValidateMinSimilarity(int) → string?` in src/Spectra.CLI/Baseline/BaselineCommandIO.cs, and use it in src/Spectra.CLI/Commands/Baseline/RelinkSourcesHandler.cs (same message as today).
- [X] T032 [US5] In src/Spectra.CLI/Commands/Baseline/SourceBaselineCommand.cs and SourceBaselineHandler.cs:
  - add `Option<int?> --min-similarity`;
  - refuse it without `--from-*`, and validate it with T031, in the existing refusal block;
  - pass it to `HistoryBootstrap`;
  - add `min_similarity` to the `from` object.
  Depends on T031.
- [X] T033 [US5] In src/Spectra.CLI/Baseline/HistoryBootstrap.cs: `FromDate` / `FromRefAsync` take `minSimilarity`; `RenamesSinceAsync` uses `-M{N}%` and returns `(renames, reason)`; on failure each ref located in that owner → unbaselined `git history unavailable: {reason}`. Depends on T032.
- [X] T034 [US5] Fails before: T030 on the `ede6680` worktree. The option is unknown (a parse error) and the forced failure gives `file did not exist at …`. Record in the Notes.

---

## Phase 8: Polish, guards, docs

- [X] T035 Guards (T6): add to tests/Spectra.Core.Tests/Git/GitReaderTests.cs an assertion that `GitReader.AllowedSubcommands` is exactly `{rev-parse, log, show, ls-files, cat-file}`. Run unchanged and confirm green: `InitTemplateWorkspaceTests`, `BaselineInitTemplateTests`, `ConfigRuleMatrixTests`, every `Commands.Git` class from 086, `NoGitTests`, and `RelinkPerformanceTests` (still under 30 s, one rename scan).
- [X] T036 [P] Update docs/cli-reference.md:
  - the F1 reason in the REMOVED reasons, the triage groups paragraph (one unchunked group per submodule, `submodule: true`) and the `relink-sources` reasons;
  - `--min-similarity` and `git history unavailable` under the history bootstrap;
  - in the "Git access" section, the executable lookup (absolute PATH entries only, `git.exe` on Windows), the removed variables, and case-insensitive rename lookups on Windows/macOS.
- [X] T037 [P] Update docs/usage.md: a submodule setup note (a clone without `--recurse-submodules` → triage says `submodule not checked out`; run `git submodule update --init`, then triage again), and `--min-similarity` in "Adopting with history".
- [X] T038 [P] Update CHANGELOG.md: add a "Fixed (review fixes, Spec 087)" subsection under `[2.6.0]`, after the 086 subsections and before `### Notes`.
- [X] T039 [P] Update CLAUDE.md: add a "Recent Changes" entry for 087 at the top, in the 085 style, with the counts from T040, and add `--min-similarity` to the `source-baseline --from-*` line of the Commands block.
- [X] T040 Run the full `dotnet build --no-incremental` (no new warnings) and `dotnet test`, and record the counts in the Notes.
- [X] T041 Run quickstart.md steps 1–6 with the built CLI in scratch repositories (step 4 only on Windows/macOS), and record the results in the Notes.
- [X] T042 Finish:
  - remove the `ede6680` worktree (`git worktree remove`);
  - fill in the Notes (counts, the fails-before results per story, deviations, changed assertions) and mark every task [X];
  - stop and ask the user before committing as `fix(087): …` and before merging into `fix/doc-change-update-workflow` with `--no-ff`. Never push.

---

## Dependencies & Execution Order

- **Setup** (T001–T004) → every story.
- **US1** (T005–T013): T005 → T008 → T009 → T010; T008, T009 → T011; T007 → T012.
- **US2** (T014–T019): T014 → T016 → T017, T018.
- **US3** (T020–T024) needs US2's T017 / T018 (`CreateStartInfo`): T020 → T022 → T023.
- **US4** (T025–T029): T025 → T027 → T028. It is independent of US1–US3, but T028 and US1's T011 / US5's T033 all edit HistoryBootstrap.cs, so run them in order: T011 → T028 → T033.
- **US5** (T030–T034): T031 → T032 → T033.
- **Polish** (T035–T042) needs everything.
- Within a story, the tests and the fails-before run come before the fix: T013, T019, T024, T029 and T034 run their tests on `ede6680` once written, independent of the implementation tasks.

## Parallel Examples

```text
# Tests first, across stories (different files):
T005 GitlinkParseTests  T014 GitExecutableTests  T020 GitEnvironmentTests  T025 RenameHistoryCaseTests
T006 SubmoduleNotCheckedOutTests  T015 GitStartSourceGuardTests  T026 CaseRenameTests  T030 BootstrapThresholdTests
# Docs at the end:
T036 cli-reference  T037 usage  T038 CHANGELOG  T039 CLAUDE.md
```

## Implementation Strategy

1. **US2 + US3 first in practice** (the safety fixes: executable and environment). Both are small and every git call
   depends on them.
2. **US1** (the user-visible submodule case, MVP for the motivating repository).
3. **US4**, then **US5**.
4. **Polish:** guards, docs, the full run, the quickstart; then commit and merge on approval.

## Notes

- **Baseline (T001)** on `087-review-fixes-086` = `ede6680`: Core 1165 / CLI 1783 (1 known `ProfileFormatLoaderTests`
  failure) / Execution 228 / Integration 8; 4 pre-existing warnings.
- **Final (T040)**: `dotnet build --no-incremental` — 0 errors, the same 4 warnings. `dotnet test` — Core 1219 (+54) /
  CLI 1812 (+29; the same 1 known failure) / Execution 228 / Integration 8; 0 skipped (git present, Windows, so the
  `[CaseInsensitiveGitFact]` tests ran). `RelinkPerformanceTests` (086 SC-001) still passes under 30 s.
- **Fails before (research R6)**, each new test run on an `ede6680` worktree (since removed):
  - **US1 (T013):** 9 of 12 in `SubmoduleNotCheckedOutTests` + the skill tests fail — REMOVED as `no rename found`,
    per-test groups, no note/warning, bootstrap `file did not exist`, skill text missing. The 3 that pass are guards
    (`update-ack` refusal, a checked-out submodule's normal behaviour, no fenced `git submodule`). `GitlinkParseTests`
    doesn't compile (no `ParseGitlinks`).
  - **US2 (T019):** the source guard fails for both `GitProcessRunner.cs` and `UserIdentityResolver.cs` (bare
    `"git"`, `Arguments =`); the start-info and `GitExecutableTests` don't compile (no API).
  - **US3 (T024):** the pre-commit-hook triage gives REMOVED instead of RELINK; the execution identity is `Injected`
    through each of `GIT_CONFIG_PARAMETERS`, `GIT_CONFIG_COUNT/KEY/VALUE` and `GIT_CONFIG` (file). `GitEnvironmentTests`
    don't compile (no API).
  - **US4 (T029):** all 3 `CaseRenameTests` fail (REMOVED instead of RELINK; `no rename found` instead of `ambiguous
    rename`; bootstrap unbaselined). `RenameHistoryCaseTests` don't compile (no comparer parameter).
  - **US5 (T034):** all 5 `BootstrapThresholdTests` fail (unknown option → exit 1 and no `from.min_similarity`; the
    forced history failure reads as `file did not exist at …`).
- **Quickstart (T041)**, built CLI, scratch repositories, all as documented: (1) a clone without submodules → one
  REMOVED group `docs/sub … submodule not checked out …` and the note; after `git submodule update --init` → UNCHANGED;
  (2) a planted `git.exe` (a copy of `whoami.exe`) and `git.cmd` in the clone never ran; (3) with the superproject's
  `GIT_DIR` / `GIT_INDEX_FILE` set, a rename inside the submodule is RELINK; (4) `docs/Store-Manager/Setup.md#Setup` for
  git's `docs/store-manager/setup.md`, renamed → RELINK `renamed to docs/guides/Setup-Guide.md`; (5) `--from-date`
  leaves a 67%-similar rename unbaselined (`file did not exist at …`), `--min-similarity 60` follows it back (triage LOAD
  `numbers: 10gb→50gb`), `--min-similarity` without `--from-*` → `INVALID_ARGUMENTS`; (6) no `index.lock` anywhere;
  `git status` shows only SPECTRA's files (and the fixture's own submodule commit).
- **Changed assertions:** `RenameHistoryTests.PathAt_UndoesRenamesNewestToOldest` now compares `(path, null)` tuples
  (the `PathAt` result shape, T025). No other existing test changed.
- **Deviations / decisions:**
  - The identity test injects config one route per row: a combined set including `GIT_CONFIG=/nowhere` made git read a
    missing file and hid the other routes, so it passed on `ede6680`.
  - `GitExecutable` also skips an absolute PATH entry equal to the current folder (research R1). No reset hook: the
    tests exercise the pure `Resolve` per case, keeping Core free of test hooks (086 U2; analysis A1 wording).
  - The `source-baseline --init` warning asks the full rename resolver per missing ref (analysis P1 not applied), so
    a plain `--init` with missing files may run one rename scan per repository.
  - Analysis E1/U2/I3 left as they were (user decision).
  - Known residual (spec Assumptions): a bootstrap ref whose casing differs from git's, for a file not renamed since the
    point, is reported `file did not exist at <hash>`.
