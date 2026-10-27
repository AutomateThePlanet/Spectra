# Implementation Plan: Git-Aware Relinking and History Bootstrap (Phase 2b)

**Branch**: `086-git-relink-sources` | **Date**: 2026-10-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/086-git-relink-sources/spec.md`

## Summary

Read-only git support for two cases Phase 2a can't handle: doc files that were renamed (they show as REMOVED today),
and baselines that should start in the past.

- **`GitReader`** owns every git call, restricted to `rev-parse`, `log`, `show`, `ls-files` and `cat-file`, with
  optional locks off, unquoted paths, NUL-separated output, UTF-8, and graceful "unavailable" reasons.
- **Rename history** comes from one rename scan per owner repository, including submodules. It feeds:
  - a new **RELINK** triage bucket;
  - **`relink-sources`**, which rewrites only the ref value in the test file, regenerates the index, moves the
    baseline key with its old fingerprint, and clears orphan status;
  - **history bootstrap** (`source-baseline --init --from-date` / `--from-ref --owner`), which reads past content
    through `cat-file --batch`.
- **`docs topology`** reports the owners.

Without git, everything behaves as under 085, with reasons.

## Technical Context

**Language/Version**: C# 12, .NET 8; SDK 9.0.318; the git CLI (tested with 2.53.0.windows.3).
**Primary Dependencies**:
- `System.Diagnostics.Process` for git;
- the existing `SourceRefResolver`, `SectionExtractor` (085 matcher), `SourceFingerprint`, `BaselineStore`,
  `BaselineRecorder`, `TestCaseParser` + `TestCaseLayout`, `TestFileWriter`, `IndexGenerator` / `IndexWriter`.
No new packages, and no LibGit2Sharp.
**Storage**: No format change. Test files (ref values and orphan keys only), `_index.json`, `_source-baseline.json`
(key moves) and snapshots (bootstrap).
**Testing**: xUnit 2.9.3, with custom `[GitFact]` / `[GitTheory]` that skip when git is absent. Throwaway git fixture
repos, including submodules. The baseline at `2ea8f16` is Core 1118 / CLI 1721 (1 known failure) / Execution 228 /
Integration 8; re-measured in T001.
**Target Platform**: Windows (dev) and Linux (CI). Paths are passed to git owner-relative with `/`; output is
UTF-8 and NUL-separated.
**Project Type**: CLI and shared library.
**Performance Goals**: `relink-sources --all-suites --dry-run` with 500 renamed docs and 2,500 tests in under 30 s, with
exactly one rename scan per owner. Triage adds no git call unless a referenced file is missing.
**Constraints**:
- SPECTRA never writes to a git repository; no `index.lock` is ever created.
- Git is never required; unavailability is a reported reason.
- Deterministic output; nothing written on `--dry-run`.
- No new config.
**Scale/Scope**:
- about 9 new source files (Core `Git/` ×4; CLI: `RenameResolver`, `TestRefRewriter`, `HistoryBootstrap`, 2
  command pairs);
- about 8 edited files (triage engine, bucket rules, grouper, ack handler, source-baseline command/handler,
  `AiCommand`, `DocsCommand`);
- 1 skill;
- 4 docs;
- about 8 new test classes, plus a git fixture and the skip attributes.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Git history is read-only input. Relinked tests and baselines are ordinary committed files, so git stays the undo. SPECTRA never writes to a repo, guarded by the T1 `index.lock` checks and the subcommand allow-list. | ✅ |
| II. Deterministic execution | The engine is untouched. Rename resolution is a deterministic function of repo state, and T7 checks byte-identical outputs. | ✅ |
| III. Orchestrator-agnostic, minimal output | Model-free. Compact JSON plus the human summary; reasons instead of failures. | ✅ |
| IV. CLI-first, deterministic exit codes, `--dry-run` | Two new commands and two new options, with `--dry-run` everywhere that writes. One new refusal code (`GIT_REF_UNAVAILABLE`). | ✅ |
| V. Simplicity | The git CLI, not a native library. One reader, one rename scan per owner, a lazy resolver. The text-level ref edit is the minimum that keeps SC-003, and it is verified by re-parsing. | ✅ |
| Test-required discipline | T1–T7 against real throwaway repos (skipped without git), plus fakes for unportable cases (dubious ownership, git missing). | ✅ |

**Post-design re-check**: no violations. A native git library was considered and rejected (research R1).

## Project Structure

### Documentation

```text
specs/086-git-relink-sources/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/cli-contract.md
├── checklists/requirements.md
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Git/                               # NEW — read-only git access
│   ├── GitProcessRunner.cs            # IGitProcessRunner + Process-based runner (env, bytes, timeout)
│   ├── GitReader.cs                   # allow-list, -C, -c core.quotepath=false, unavailable reasons; runner via constructor (no test hooks in Core)
│   ├── OwnerResolver.cs               # nearest repo per dir; submodule/shallow/head
│   └── RenameHistory.cs               # -z parser, forward candidates (chains, ambiguity), PathAt (backwards)
└── Baseline/
    ├── ChangeClassifier.cs            # RefState.Relink
    ├── BucketRules.cs                 # Bucket.Relink (top precedence), Wire
    └── TriageGrouper.cs               # ChangedItem.Target in the signature

src/Spectra.CLI/
├── Baseline/
│   ├── GitAccess.cs                   # NEW: internal RunnerOverride test hook + CreateReader() (analysis U2)
│   ├── RenameResolver.cs              # NEW: ref → candidate via SourceRefResolver locations, owners, cached histories
│   ├── TestRefRewriter.cs             # NEW: text-level ref rewrite + orphan clearing, verified by re-parse
│   ├── HistoryBootstrap.cs            # NEW: bootstrap points, path-at-point, cat-file --batch content
│   └── TriageEngine.cs                # RELINK decision; REMOVED reasons
├── Commands/Baseline/
│   ├── RelinkSourcesCommand.cs  RelinkSourcesHandler.cs   # NEW
│   ├── SourceBaselineCommand.cs SourceBaselineHandler.cs  # --from-date / --from-ref / --owner
│   └── UpdateAckHandler.cs            # message for RELINK refs
├── Commands/Docs/DocsTopologyCommand.cs DocsTopologyHandler.cs   # NEW
├── Commands/Ai/AiCommand.cs, Commands/Docs/DocsCommand.cs        # register
└── Skills/Content/Skills/spectra-update.md                       # RELINK step (FR-023), REMOVED-step hint (FR-024)

tests/
├── Spectra.Core.Tests/Git/            # RenameHistoryParserTests (pure, no git), GitReaderTests (fake runner: allow-list, env, reasons)
├── Spectra.CLI.Tests/TestFixtures/GitFixture.cs, GitFactAttribute.cs
└── Spectra.CLI.Tests/Commands/Git/    # GitReaderIntegrationTests (T1), TopologyTests (T2), RenameTests (T3),
                                       # RelinkSourcesTests (T4, T7), HistoryBootstrapTests (T5), NoGitTests (T6),
                                       # RelinkPerformanceTests (SC-001); SkillsManifestTests additions
```

## Implementation Order

1. **Baseline measurement** (T001), and the git version recorded.
2. **Core `Git/`:** runner, reader (allow-list, env), the rename-history parser and candidate logic (pure tests
   first), the owner resolver.
3. **Test infrastructure:** `GitFixture` and `[GitFact]`. Then T1, T2, T3 against real repos (including submodules)
   plus the fakes.
4. **Triage RELINK:** state, bucket, grouping, `RenameResolver`, engine wiring. Update the one 084 assertion whose
   reason text changes (research R9 note).
5. **`relink-sources`:** `TestRefRewriter` (verify by re-parse), the key move, orphan clearing, the command. Then T4
   and T7.
6. **History bootstrap:** `HistoryBootstrap`, then the `source-baseline` options and validation. Then T5.
7. **`docs topology`.**
8. **No-git guard (T6), the performance test (SC-001), the skill and its tests.**
9. **Docs:**
   - `CHANGELOG.md` (086 under `[2.6.0]`);
   - `CLAUDE.md` "Recent Changes";
   - `docs/cli-reference.md` (`relink-sources`, RELINK, `--from-*`, `docs topology`, the git requirements and
     guarantees);
   - `docs/usage.md` (relink step, bootstrap).
10. **Finish:** full `dotnet test`, then the quickstart with the built CLI, including the `index.lock` and `git status`
    checks. Commit and merge on approval; never push.

## Risks

| Risk | Mitigation |
|---|---|
| A big restructure commit exceeds git's rename-detection limit, so renames come back as delete + add | `-l0` (probed as accepted on git 2.53); the perf fixture renames 500 files in one commit |
| The ref text edit corrupts YAML or changes other values | The result is re-parsed and every field compared; on any mismatch, fall back to the canonical writer with a warning. Tests cover plain, `"…"` and `'…'` refs |
| Git output encoding or quoting on Windows | `-c core.quotepath=false`, `-z`, byte-level UTF-8 decoding; T1 round-trips `docs/über.md` |
| Git takes a lock or prompts | `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0`, read-only subcommands only; T1 scans for `index.lock` after every command, including in submodules |
| Triage gets slower | The resolver is lazy, and git runs only when a referenced file is missing; one rename scan per owner per command |
| Existing tests assert `"not found"` for a deleted file | One known assertion (`UpdateTriageCommandTests.cs:143`), changed deliberately to `not found; not in a git repository` and noted (FR-013, research R9). The `not found` prefix keeps the old meaning readable |
| Git missing on CI | `[GitFact]` skips with a reason; the fake runners cover the no-git and dubious-ownership paths without git |
| The test hook leaks into Core | The hook lives in Spectra.CLI (`GitAccess.RunnerOverride`, internal); Core's `GitReader` takes its runner through the constructor (analysis U2) |

## Complexity Tracking

No constitution violations.
