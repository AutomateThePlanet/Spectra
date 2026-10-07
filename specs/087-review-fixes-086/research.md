# Research: Spec 087 — Review Fixes for 086

## R0 — What exists at `ede6680` (checked)

| Fact | Where | Consequence |
|---|---|---|
| `new ProcessStartInfo("git")`, `ArgumentList`, the env merged over the inherited one | `Core/Git/GitProcessRunner.cs` | F2/F3 change how the start info is built; a public `CreateStartInfo` lets tests inspect the file name and environment without launching anything |
| `FileName = "git"`, `Arguments = "config user.name"` (a string command line) | `Execution/Identity/UserIdentityResolver.cs:75-78` | F2: absolute path, `ArgumentList`; F3: the same environment helper |
| `GitReader.Environment` = `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0` | `Core/Git/GitReader.cs` | Becomes `GitEnvironment.Settings` (same values) |
| `RenameHistory._targets` keyed `Ordinal`; `PathAt` compares `Ordinal` | `Core/Git/RenameHistory.cs` | F4 comparer seam |
| `RenamesSinceAsync`: `-M90%`; `!Ok → []` | `CLI/Baseline/HistoryBootstrap.cs` | F5 |
| Clone without `--recurse-submodules`: empty submodule folder; `ls-files -s -z` → `160000 <sha> 0\tdocs/sub\0`; `rev-parse` in the empty folder → the superproject | probe (git 2.53) | F1 detection with an allowed subcommand |
| Platform path comparison elsewhere: `OperatingSystem.IsLinux() ? Ordinal : OrdinalIgnoreCase` | `SourceRefResolver`, `TriageEngine`, `OwnerResolver` | F4 reuses that rule (Windows and macOS ignore case) |
| Execution's internals are visible to `Spectra.CLI.Tests` | `Spectra.Execution.csproj` | The identity start-info test lives with the other git tests |

## R1 — Git executable (F2)

**Decision.** `Spectra.Core.Git.GitExecutable`:
- `Resolve(IEnumerable<string?> pathEntries, string currentDirectory, bool isWindows, Func<string, bool> fileExists) →
  string?` is pure, for each entry in order:
  1. trim whitespace and surrounding double quotes;
  2. skip it if it is empty, `.`, or not fully qualified (`Path.IsPathFullyQualified`: rejects relative paths,
     `C:foo` and `\foo`);
  3. skip it if its full path equals the current directory (compared with the platform comparer);
  4. candidate = `entry/git.exe` on Windows, `entry/git` elsewhere; return the first that exists.
- `Path` (static, `Lazy<string?>`) applies this to `Environment.GetEnvironmentVariable("PATH")` split on
  `Path.PathSeparator`, the process's current directory, and `File.Exists`. It's cached for the process.
- No PATHEXT, no `.cmd` / `.bat`, and no search of the application folder or the current folder.

**Rationale.** It closes the Windows current-directory search that `CreateProcess` does for a bare name. Skipping an
absolute entry equal to the current folder covers a PATH that names the workspace explicitly.

**The current folder is the one at first resolution** (analysis A1). `Path` is resolved once per process, so the
current-folder check uses the working folder at that moment. In real use that is the command's working folder: one
process per command. Tests check the rule through the pure `Resolve` with an explicit current folder per case, so no
test depends on the cached `Path` and nothing needs resetting. This keeps Core free of test hooks (086 analysis U2).

**Alternatives.** `where git` / `which git` would spawn a process, and `where` searches the current folder first.
Rejected.

## R2 — Git environment (F3)

**Decision.** `Spectra.Core.Git.GitEnvironment`:
- `RedirectingVariables`: the F3 list.
- `IsRedirecting(name)`: in the list, or starts with `GIT_CONFIG_KEY_` / `GIT_CONFIG_VALUE_`. Compared ignoring case
  on Windows (environment names are case-insensitive there) and exactly elsewhere.
- `Settings`: `GIT_OPTIONAL_LOCKS=0`, `GIT_TERMINAL_PROMPT=0`.
- `Apply(IDictionary<string, string?> environment, IReadOnlyDictionary<string, string>? extra = null)`: removes every
  redirecting key, then sets `Settings`, then `extra`.
- `ProcessGitRunner.CreateStartInfo` and `UserIdentityResolver.CreateGitStartInfo` call `Apply(info.Environment, …)`.

**Rationale.** `ProcessStartInfo.Environment` starts as a copy of the process environment, so removing keys there is
the only way to stop inheritance. One helper keeps both callers identical (FR-011).

**Test strategy.**
- **Unit:** `Apply` over a dictionary.
- **Start info:** `CreateStartInfo` with the variables set in the process environment, then inspect
  `info.Environment`.
- **Behaviour (the regression):** in a real superproject with a submodule rename, set `GIT_DIR` and `GIT_INDEX_FILE`
  to the superproject's (what a pre-commit hook sees), run triage, and expect RELINK. On `ede6680` the submodule's
  `log` reads the superproject, so the rename isn't found.
- These tests mutate the process environment, so they run in a `DisableParallelization` collection, set the variables
  only around the SPECTRA call, and restore them in `finally`.

## R3 — Unchecked-out submodule (F1)

**Decision.**
- **`OwnerResolver.GitlinksAsync(owner)`:** one `ls-files -s -z` per owner, cached. Parsed into the set of
  owner-relative paths whose mode is `160000`, using the platform comparer. A failure means an empty set: the check is
  skipped and 086 behaviour follows.
- **`OwnerResolver.UncheckedSubmoduleOfAsync(owner, fullPath) → string?`:** the owner-relative path of the deepest
  gitlink that is the missing path's folder or one of its parents, and whose folder has no `.git` entry. Otherwise
  null. Each parent folder of the owner-relative path is checked, nearest first.
- **`RenameResolver.CandidateForAsync`:** per location, after the owner is found and before any history:
  - if `UncheckedSubmoduleOfAsync` returns a path → `RelinkCandidate(Ref, null, null, SubmoduleReason(p), Submodule: p)`;
  - `p` is made repo-root-relative (`Path.GetRelativePath(repoRoot, owner.Root + p)`, `/`);
  - this result wins over later locations, because it is a setup fact, not a missing rename.
- **`SubmoduleReason(p)`:** `submodule not checked out: {p} (run git submodule update --init)`.
- **Triage:**
  - `TriagedRef.Submodule` carries the path;
  - `Build` emits **one** `ChangedItem(false, Key: p, State: "REMOVED", Submodule: true)` per submodule instead of one
    per ref; its signature is `submodule:{p}=REMOVED`;
  - `why` is the reason, so it is the same for every member;
  - `TriageGrouper` doesn't chunk a group whose items are all submodule items;
  - the stdout `removed` list stays per ref, each with the full `not found; …` reason;
  - the group JSON ref item is `{"ref": p, "state": "REMOVED", "submodule": true}`, an optional key like 086's
    `target`.
- **`HistoryBootstrap.LocateAsync`:** for a missing ref, the same check runs before `PathAt` → unbaselined with the
  bare reason.
- **Notes and warnings (analysis U1).** F1 alone reaches only baselined refs, because an unbaselined missing ref is
  reported as unbaselined, not REMOVED. So:
  - triage counts, per submodule, every missing ref whose candidate carries a submodule. That covers baselined and
    unbaselined refs, and the refs of tests with no entry, which triage already checks for RELINK.
  - triage adds one `notes` line per submodule: `N reference(s) from M test(s) point into submodule <path>, which isn't
    checked out (run git submodule update --init)`. N counts (test, ref) pairs, and M distinct tests (analysis A2).
  - `source-baseline --init` (all modes) asks the same question for every missing ref it would leave out. It adds the
    same line to `warnings` (printed as `Warning:` on stderr) and leaves those refs unbaselined; plain `--init` already
    skips missing refs, so only the warning is new.
  - the skill's first-time setup withholds the `--init` offer while the note is present.
- **`relink-sources`:** `not_relinked` gets the bare reason, through the candidate.
- **`update-ack`:** unchanged: a baselined missing ref is REMOVED → `REF_REMOVED`.
- **Skill, Step 3:** reasons starting with `not found; submodule not checked out` are collected per submodule. The
  skill names each submodule once, tells the user to run `git submodule update --init` themselves, and to re-run
  triage. Those tests are left out of the REMOVED review list and the `spectra-delete` hint. The command appears only
  inline in prose, never in a fenced block: in this skill a fenced block is a command the assistant runs (analysis
  I1).

**Rationale.** Detection uses only data git already has (the index's gitlinks) and an allowed subcommand. Folding the
refs per submodule turns a thousand REMOVED entries into one actionable line.

## R4 — Casing (F4)

**Decision.** `GitPathComparer.Platform` = `OperatingSystem.IsLinux() ? StringComparer.Ordinal :
StringComparer.OrdinalIgnoreCase`.
- **`RenameHistory`** takes an optional comparer (default `Platform`) and keeps `_targets` keyed by git's exact old
  path. `Resolve(path)` returns the distinct exact keys equal to `path` under the comparer:
  - none → no rename;
  - more than one → `ambiguous rename`;
  - one → that key.
  `ForwardCandidate` applies `Resolve` at the start and at each chain step, so a later rename recorded with other
  casing still chains. Target sets stay ordinal: git records exact paths, and two targets differing in case are two
  targets.
- **`PathAt(renames, currentPath, comparer)` → `(string? Path, string? Reason)`.** At each rename, newest first, the
  distinct `New` values equal to the current path under the comparer are looked at. More than one → `ambiguous
  rename`. Otherwise the matching rename is undone, giving git's `Old` casing.
- **The new ref** is built from git's target (`owner.Root + target`), so it already has git's casing (FR-014). A test
  pins this.
- **The seam:** Core tests pass `Ordinal` and `OrdinalIgnoreCase` explicitly, so both rules are tested on every OS. The
  real-repository test runs only on case-insensitive platforms (`[CaseInsensitiveGitFact]`).

**Known residual** (spec Assumptions): a bootstrap ref with different casing and no rename since the point is read as
`<point>:<ref casing>`; tree lookup is case-sensitive, so it reports `file did not exist at <hash>`.

## R5 — Bootstrap threshold and failures (F5)

**Decision.**
- **`BaselineCommandIO.ValidateMinSimilarity(int) → string?`** returns the message `--min-similarity must be between
  50 and 100 (got N).` `relink-sources` switches to it, with the same text as today.
- **`SourceBaselineCommand`** gains `Option<int?> --min-similarity`. The handler refuses it without `--from-*`
  (`INVALID_ARGUMENTS`, `--min-similarity is only used with --from-date or --from-ref.`) and validates it with the
  helper. These checks join the existing FR-018 refusal block, before anything is read.
- **`HistoryBootstrap`** takes `minSimilarity` (default 90). `RenamesSinceAsync` runs
  `log -l0 -M{N}% --diff-filter=R …` and returns `(renames, reason)`. On failure, each ref located in that owner is
  unbaselined with `git history unavailable: {reason}`.
- **The JSON `from` object** gains `min_similarity` only with `--from-*`.
- **The forced failure in T5** comes from a `FakeGitRunner` that fails `log` calls whose last argument ends in
  `..HEAD`, with everything else going to the real runner.

## R6 — Fails-before evidence

Each regression test is written before the fix and run against `ede6680`, in a temporary worktree with the new test
files copied in. Its failure is recorded in the Notes:
- T1 submodule reason/grouping;
- T2 start-info file name;
- T3 hook environment;
- T4 casing;
- T5 threshold and failure reason.

Tests that need new APIs (`CreateStartInfo`, the comparer seam) fail to compile on `ede6680`. Their behavioural twins
(the real-repository tests) give the runtime failure.
