# Implementation Plan: Review Fixes for Spec 086 — Git-Aware Relinking

**Branch**: `087-review-fixes-086` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/087-review-fixes-086/spec.md`

## Summary

Five fixes to 086's git support. No new features, no format changes.

- **F1:** before any rename lookup, a missing ref under a submodule that isn't checked out (a gitlink with no `.git`
  in its folder) gets `submodule not checked out: <path> (run git submodule update --init)`. It is never RELINK. Triage
  folds such refs into one unchunked group per suite and submodule. The skill treats the reason as setup, and the
  bootstrap and `relink-sources` report it.
- **F2:** `GitExecutable` resolves git from absolute PATH entries only (`git.exe` on Windows), cached per process.
  `ProcessGitRunner` and `UserIdentityResolver` start that absolute path, with arguments passed through
  `ArgumentList`.
- **F3:** `GitEnvironment.Apply` strips the redirecting `GIT_*` variables from each git process's environment and sets
  the two fixed ones. Both callers use it.
- **F4:** `RenameHistory` and `PathAt` take a path comparer: ignore case on Windows and macOS, exact on Linux. One
  distinct match → use git's casing; more than one → `ambiguous rename`.
- **F5:** `source-baseline --from-*` gains `--min-similarity`, validated by the same shared code as
  `relink-sources`. A failed "renames since" scan leaves the owner's refs unbaselined with
  `git history unavailable: …`.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318; git 2.53.0.windows.3 for the fixtures.
**Primary Dependencies**: the 086 git layer (`GitReader`, `ProcessGitRunner`, `OwnerResolver`, `RenameHistory`,
`RenameResolver`, `HistoryBootstrap`), `TriageEngine` / `TriageGrouper`, the `spectra-update` skill, and
`UserIdentityResolver` (Spectra.Execution, which references Spectra.Core). No new packages.
**Storage**: no format change. Only reason strings change, plus one optional `submodule` key on F1 group ref items.
**Testing**: xUnit 2.9.3.
- `[GitFact]` / `[GitTheory]` from 086.
- A new `[CaseInsensitiveGitFact]` that also skips on Linux.
- A new non-parallel collection (`DisableParallelization = true`) for tests that set `GIT_*` variables in the test
  process.
- Pure tables for the executable lookup, the environment helper, the gitlink parser and the comparer seam.
- Baseline at `ede6680`: Core 1165 / CLI 1783 (1 known failure) / Execution 228 / Integration 8, re-measured in T001.

**Target Platform**: Windows (dev), Linux (CI) and macOS. Case behaviour follows the existing platform comparer.
**Project Type**: CLI and shared library.
**Performance Goals**:
- F1 adds one `ls-files -s -z` per owner per command, and only when a ref's file is missing.
- The executable lookup runs once per process.
- The 086 SC-001 test (500 renames, 2,500 tests) stays under 30 s.

**Constraints**:
- Read-only git: the allowed subcommands are unchanged.
- Git is never required.
- Deterministic output.
- Nothing is written on `--dry-run`.
- SPECTRA never runs `git submodule update` itself.

**Scale/Scope**:
- about 3 new Core files (`GitExecutable`, `GitEnvironment`, `GitPathComparer`);
- edits to about 12 files: `ProcessGitRunner`, `GitReader`, `RenameHistory`, `OwnerResolver`, `UserIdentityResolver`,
  `RenameResolver`, `HistoryBootstrap`, `TriageEngine`, `TriageGrouper`, `UpdateTriageHandler`,
  `SourceBaselineCommand/Handler`, `RelinkSourcesHandler`, `BaselineCommandIO`;
- 1 skill;
- 4 docs;
- about 8 new test classes.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Still read-only. F2/F3 make it harder for anything else to hijack a call. SPECTRA tells the user to run `git submodule update --init` and never runs it | ✅ |
| II. Deterministic execution | The engine is untouched. The identity lookup changes only how git is started (absolute path, clean environment); its fallback to the OS user name is unchanged | ✅ |
| III. Orchestrator-agnostic, minimal output | Reasons instead of failures. F1 shrinks output (one group per submodule instead of per test) | ✅ |
| IV. CLI-first, deterministic exit codes, `--dry-run` | One option (`--min-similarity` on `source-baseline`), validated like `relink-sources`; no new codes | ✅ |
| V. Simplicity | Three small Core helpers shared by every git caller; a comparer seam instead of platform branches in the tests | ✅ |
| Test-required discipline | Each fix has a regression test that fails on `ede6680` (T1–T5), plus guards (T6) | ✅ |

**Post-design re-check**: no violations.

## Project Structure

### Documentation

```text
specs/087-review-fixes-086/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/Git/
├── GitExecutable.cs        # NEW (F2): Resolve(pathEntries, currentDir, isWindows, fileExists) pure + cached Path
├── GitEnvironment.cs       # NEW (F3): RedirectingVariables, Settings, Apply(IDictionary<string,string?> env)
├── GitPathComparer.cs      # NEW (F4): Platform (Ordinal on Linux, OrdinalIgnoreCase elsewhere) + Match helper
├── GitProcessRunner.cs     # CreateStartInfo (absolute path, ArgumentList, GitEnvironment.Apply); NotInstalled when unresolved
├── GitReader.cs            # Environment → GitEnvironment.Settings (same two values)
├── OwnerResolver.cs        # GitlinksAsync (ls-files -s -z, cached), UncheckedSubmoduleOfAsync
└── RenameHistory.cs        # comparer seam; ForwardCandidate / PathAt with case-insensitive matching and ambiguity
src/Spectra.Core/Baseline/TriageGrouper.cs   # ChangedItem.Submodule; submodule-only groups are not chunked
src/Spectra.Execution/Identity/UserIdentityResolver.cs   # CreateGitStartInfo (internal): GitExecutable + ArgumentList + GitEnvironment
src/Spectra.CLI/
├── Baseline/RenameResolver.cs      # F1 check before history; RelinkCandidate.Submodule; SubmoduleReason()
├── Baseline/HistoryBootstrap.cs    # F1 for missing refs; minSimilarity; "git history unavailable: …"; PathAt ambiguity
├── Baseline/TriageEngine.cs        # TriagedRef.Submodule → one ChangedItem per submodule
├── Baseline/BaselineCommandIO.cs   # ValidateMinSimilarity (shared message)
├── Commands/Baseline/{SourceBaselineCommand,SourceBaselineHandler}.cs   # --min-similarity
├── Commands/Baseline/RelinkSourcesHandler.cs                            # shared validation
├── Commands/Baseline/UpdateTriageHandler.cs                             # group ref item "submodule" key
└── Skills/Content/Skills/spectra-update.md                              # Step 3: setup problem, not review

tests/
├── Spectra.Core.Tests/Git/  GitExecutableTests, GitEnvironmentTests, GitlinkParseTests, RenameHistoryCaseTests
└── Spectra.CLI.Tests/
    ├── TestFixtures/ CaseInsensitiveGitFactAttribute, GitEnvironmentCollection
    ├── Commands/Git/ SubmoduleNotCheckedOutTests (T1), GitEnvironmentIsolationTests (T3), CaseRenameTests (T4),
    │                 BootstrapThresholdTests (T5), GitStartSourceGuardTests (T2 source guard + start-info checks)
    └── Skills/SkillsManifestTests (F1 skill content)
```

## Implementation Order

1. **T001:** baseline measurement.
2. **F2/F3** (US2, US3): `GitExecutable` and `GitEnvironment` with pure tests first, then
   `ProcessGitRunner.CreateStartInfo`, `UserIdentityResolver.CreateGitStartInfo`, the source guard and the
   hook-environment real-repository test.
3. **F1** (US1): `OwnerResolver` gitlinks, the resolver check, triage grouping (unchunked) and the per-submodule note,
   the `source-baseline --init` warning, the bootstrap's reason, the skill, and T1 against a real clone without
   submodules.
4. **F4** (US4): `GitPathComparer`, the `RenameHistory` comparer seam, `PathAt` ambiguity, the bootstrap's use of it,
   and the real-repository case test (non-Linux). F1 and F4 both edit `HistoryBootstrap.cs`, so F1 comes first, as in
   the tasks (analysis I2).
5. **F5** (US5): `--min-similarity` on `source-baseline`, shared validation, the failing-scan reason, T5.
6. **Docs:** CHANGELOG, CLAUDE.md, cli-reference, usage.
7. **Finish:** a full `dotnet test`, fails-before runs on an `ede6680` worktree for the regression tests, and the
   quickstart. Ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Setting `GIT_DIR` in the test process breaks git fixtures in parallel tests | The env tests run in a `DisableParallelization` collection, set the variables only around the SPECTRA call (not the fixture setup), and restore them in `finally` |
| The PATH lookup misses git where it worked before (for example only `git.cmd` on PATH) | Git for Windows puts `git.exe` in `…\Git\cmd`; documented. If git isn't found, the reason is `no git`, the same as when git is absent |
| An absolute PATH entry equal to the current folder | Also skipped (research R1); T2 row |
| A case-insensitive lookup turns an exact-case 086 result into "ambiguous" | Only when two distinct history paths differ only in case (a real ambiguity on those file systems); T4 row |
| An unchunked submodule group is very large | Only the group's test list grows; the stdout budget (SC-004 of 084) is a test list per group, not per test; documented |
| Fails-before evidence | Each regression test is run on an `ede6680` worktree before the code lands (T0xx); results go in the Notes |

## Complexity Tracking

No constitution violations.
