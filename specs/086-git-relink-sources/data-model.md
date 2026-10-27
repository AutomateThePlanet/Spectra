# Data Model: Spec 086

The files don't change shape. Baselines, snapshots and the triage detail keep their 084/085 formats; relinking only
moves a baseline key. No config change.

## Core — `Spectra.Core.Git` (new)

| Type | Shape | Notes |
|---|---|---|
| `IGitProcessRunner` | `Task<GitProcessResult> RunAsync(string workingDir, IReadOnlyList<string> args, IReadOnlyDictionary<string,string> env, byte[]? stdin, CancellationToken)` | The test seam. `GitProcessResult(int ExitCode, byte[] Stdout, string Stderr, bool NotInstalled)` |
| `GitReader` | `new GitReader(IGitProcessRunner runner)`; `RunAsync(string dir, string subcommand, params string[] args) → GitResult` | Enforces the allowed list, `-C`, `-c core.quotepath=false`, the env and the UTF-8 decoding. No test hooks in Core |
| `GitResult` | `(bool Ok, string? Unavailable, byte[] Stdout)` + `Text` (UTF-8) | `Unavailable` is the FR-003 reason |
| `GitOwner` | `(string Root, bool IsSubmodule, bool Shallow, string Head)` | |
| `OwnerResolver` | `OwnerOf(string fullPath) → (GitOwner? Owner, string? Reason)` | Cached per directory |
| `GitRename` | `(string Commit, long Time, string Old, string New, int Score)` | Owner-relative paths, `/` |
| `RenameHistory` | per owner: `IReadOnlyList<GitRename>` newest first; `ForwardCandidate(string path, Func<string,bool> exists) → (string? Target, string? Reason)`; `PathAt(IEnumerable<GitRename> renamesSincePoint, string currentPath)` | Chains and ambiguity |

## CLI

| Type | Responsibility |
|---|---|
| `GitAccess` | `internal static IGitProcessRunner? RunnerOverride` (the test hook); `CreateReader()` builds the `GitReader` every CLI component uses |
| `RenameResolver` | Lazily created per command. `CandidateFor(string sourceRef) → RelinkCandidate`, using `SourceRefResolver` locations, `OwnerResolver` and the cached `RenameHistory` per owner |
| `RelinkCandidate` | `(string Ref, string? NewRef, string? Target, string? Reason)` — Target is repo-relative |
| `TestRefRewriter` | Text-level ref rewrite and orphan clearing with re-parse verification (research R6) |
| `RelinkSourcesHandler` / `Command` | The command (research R5) |
| `HistoryBootstrap` | Bootstrap points per owner; content at the point via `cat-file --batch`; feeds `BaselineRecorder.Complete` |
| `DocsTopologyHandler` / `Command` | The topology report |

## Changed types

| Type | Change |
|---|---|
| `RefState` | + `Relink` |
| `Bucket` | + `Relink` (highest precedence); `Wire()` → `RELINK` |
| `ChangedItem` | + `string? Target`, included in `Signature` |
| `TriageEngine` | A missing file → RELINK when a candidate exists, baselined or not; tests with no entry are evaluated for RELINK only; REMOVED reasons from the resolver |
| Triage detail test entry | A RELINK test with no entry gets the full record (empty baseline refs, `current` filled) + `unbaselined: true`; it is removed from `unbaselined.full` / `counts.unbaselined`. A test whose only unbaselined refs are RELINK leaves `partial` |
| `UpdateAckHandler` | `RefuseRemovedAsync` (`--ids`, now also missing refs with no key and tests with no entry) and the `--group` bucket check refuse RELINK with a message naming `relink-sources`; never creates an entry for a test with a RELINK ref |
| `SourceBaselineCommand/Handler` | + `--from-date`, `--from-ref`, `--owner`; validation; `from` and `unbaselined` in the JSON |
| `DocsCommand` | Registers `topology` |
| `AiCommand` | Registers `relink-sources` |

## State transitions (one ref)

```
file missing ──(candidate in history)──► RELINK ──relink-sources──► (ref path changed, baseline key moved with old fingerprint)
     │                                                                 └─ next triage: UNCHANGED, or COSMETIC/LIGHT/LOAD if the rename commit edited it
     └─(no candidate / no git)──► REMOVED (reason)
```
