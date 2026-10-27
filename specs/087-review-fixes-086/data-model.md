# Data Model: Spec 087

No file format changes: the baseline, snapshots, triage detail and test files are as in 086.

## New (Spectra.Core.Git)

| Type | Shape | Notes |
|---|---|---|
| `GitExecutable` | `static string? Resolve(IEnumerable<string?> pathEntries, string currentDirectory, bool isWindows, Func<string,bool> fileExists)`; `static string? Path` (cached) | F2. Absolute PATH entries only; `git.exe` on Windows |
| `GitEnvironment` | `static IReadOnlyList<string> RedirectingVariables`; `static bool IsRedirecting(string name)`; `static IReadOnlyDictionary<string,string> Settings`; `static void Apply(IDictionary<string,string?> env, IReadOnlyDictionary<string,string>? extra = null)` | F3. Shared by `ProcessGitRunner` and `UserIdentityResolver` |
| `GitPathComparer` | `static StringComparer Platform` | F4. Ordinal on Linux, OrdinalIgnoreCase elsewhere |

## Changed

| Type | Change |
|---|---|
| `ProcessGitRunner` | `public ProcessStartInfo? CreateStartInfo(workingDir, args, env)` (null when git isn't found → `NotInstalled`) |
| `GitReader.Environment` | = `GitEnvironment.Settings` |
| `OwnerResolver` | `Task<IReadOnlySet<string>> GitlinksAsync(GitOwner)`; `Task<string?> UncheckedSubmoduleOfAsync(GitOwner, string fullPath)`; `static IReadOnlySet<string> ParseGitlinks(byte[] lsFilesStage, StringComparer)` |
| `RenameHistory` | ctor `(renames, minSimilarity, shallow, StringComparer? comparer = null)`; `ForwardCandidate` case-aware; `PathAt(renames, path, comparer) → (string? Path, string? Reason)` |
| `UserIdentityResolver` | `internal static ProcessStartInfo? CreateGitStartInfo()`; null → OS user name |
| `RelinkCandidate` | + `string? Submodule` (repo-relative) |
| `RenameResolver` | F1 check per location before history; `static string SubmoduleReason(string path)` |
| `TriagedRef` | + `string? Submodule` |
| `ChangedItem` | + `bool Submodule` (signature `submodule:<path>=REMOVED`) |
| `TriageGrouper` | a group whose items are all submodule items isn't chunked |
| `HistoryBootstrap` | `minSimilarity`; the F1 reason for missing refs; `git history unavailable: …`; `PathAt` ambiguity |
| `SourceBaselineHandler/Command` | `--min-similarity` (int?), refused without `--from-*` |
| `BaselineCommandIO` | `ValidateMinSimilarity(int) → string?` |

## Reasons (new or changed)

| Reason (bare) | Where |
|---|---|
| `submodule not checked out: <path> (run git submodule update --init)` | triage (prefixed `not found; `), `relink-sources` `not_relinked`, bootstrap `unbaselined` |
| `git history unavailable: <git reason>` | bootstrap `unbaselined` |
| `N reference(s) from M test(s) point into submodule <path>, which isn't checked out (run git submodule update --init)` | triage `notes`; `source-baseline --init` `warnings` (all modes), one per submodule. N = (test, ref) pairs, baselined or not; M = distinct tests (analysis U1, A2) |
| `ambiguous rename` | now also for two history paths that differ only in case (Windows/macOS), and in the bootstrap's path-at-point walk |
