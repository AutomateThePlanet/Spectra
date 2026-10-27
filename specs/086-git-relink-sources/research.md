# Research: Spec 086 — Git-Aware Relinking and History Bootstrap

## R0 — What exists at `2ea8f16` (checked)

| Fact | Where | Consequence |
|---|---|---|
| The only git call is `git config user.name` | `Spectra.Execution/Identity/UserIdentityResolver.cs` | No git abstraction to reuse. The new `GitReader` lives in Core and is the only git caller this spec adds |
| `TestFileWriter` emits a **changed** list value canonically (`RenderList(key, t.SourceRefs)`); unchanged values are reproduced verbatim from `TestCaseLayout` | `CLI/IO/TestFileWriter.cs:162,187` | Changing one ref through the writer re-emits the whole `source_refs` list, which can re-quote untouched refs. FR-008/SC-003 (only the ref value changes) needs a targeted text edit (R6) |
| `TestCase` has `Status`, `OrphanedReason`, `OrphanedDate`; the 078 writer drops removed keys losslessly | `Core/Models/TestCase.cs:109–119` | Orphan clearing is `with { Status = null, OrphanedReason = null, OrphanedDate = null }` through the writer (R6) |
| `SourceRefResolver` tries the repo root, then `source.local_dir`; refs outside both never resolve | `Core/Coverage/SourceRefResolver.cs` | Missing-file candidates are checked at the same locations in the same order (FR edge case), and targets must be under an allowed root (FR-006) |
| `TriageEngine` classifies a missing section as REMOVED, with the reason `not found` / `anchor not found` / `outside allowed roots` | `CLI/Baseline/TriageEngine.cs`, `BaselineContext.cs` | RELINK is decided in the engine when the reason is `not found`, by asking the rename resolver (lazily, so workspaces with no missing files never call git) |
| `Bucket` enum order = precedence; `Wire()` names | `Core/Baseline/BucketRules.cs` | Add `Relink` above `Removed`; add `RefState.Relink` |
| `BaselineRecorder.Complete` adds missing keys only | `CLI/Baseline/BaselineRecorder.cs` | History bootstrap = `Complete` with fingerprints computed from historical content (R7) |
| Probe on git 2.53 (Windows): `log -M90% --diff-filter=R --name-status -z --format=%x1e%H%x00%ct` gives `\x1e<hash>\0<ct>\0\nR100\0old\0new\0…`, with raw UTF-8 paths (`docs/ü.md`), and `-l0` is accepted | probe | The parser format (R3) |

## R1 — `GitReader` (FR-001–FR-003)

**Decision.**
- **`Spectra.Core.Git.GitReader`** owns every git invocation. It receives an `IGitProcessRunner` through its
  constructor: the default runs `Process`, and tests use a recording or faking runner.
- **Analysis U2:** the test hook lives in Spectra.CLI as `Spectra.CLI.Baseline.GitAccess.RunnerOverride` (internal,
  visible to `Spectra.CLI.Tests`). `GitAccess.CreateReader()` builds every `GitReader` the CLI uses, so Core has no
  test hooks and no new `InternalsVisibleTo`.
- **Allowed subcommands:** `rev-parse`, `log`, `show`, `ls-files`, `cat-file`. Any other is an
  `InvalidOperationException` before a process starts, which the tests assert.
- **Every call is `git -C <dir> -c core.quotepath=false <sub> …`** with:
  - `GIT_OPTIONAL_LOCKS=0`;
  - `GIT_TERMINAL_PROMPT=0`, so no credential prompt ever blocks;
  - stdout read as bytes and decoded as strict UTF-8;
  - stderr captured;
  - a timeout, 60 s per call.
- **Result:** `GitResult(bool Ok, string? Unavailable, byte[] Stdout)`. "Unavailable" reasons are mapped from:
  - the executable not found (`Win32Exception`) → `no git`;
  - exit code with `not a git repository` → `not in a git repository`;
  - `detected dubious ownership` → `repository not readable (dubious ownership)`;
  - anything else → `git failed: <first stderr line>`.
- **No exceptions escape for git problems.**

**Rationale.** One seam makes D1 enforceable and testable: T1 asserts the recorded argument lists, and the
environment is set on every call. `GIT_TERMINAL_PROMPT=0` is a read-only safety addition.

**Alternatives.** LibGit2Sharp: a native dependency, and its own rename detection differs from git's. Rejected
(Principle V; the user specified the git CLI).

## R2 — Owners (FR-004, FR-019)

**Decision.**
- **`OwnerResolver.OwnerOf(fullPath)`:**
  1. walk up to the nearest existing directory;
  2. cache per directory;
  3. run `rev-parse --show-toplevel --show-superproject-working-tree --is-shallow-repository HEAD` in one call.
  Its output lines are the top-level, the superproject (empty unless it's a submodule), `true`/`false` and the head
  hash. `Owner(Root, IsSubmodule, Shallow, Head)`.
- **The repo-relative path** is `Path.GetRelativePath(owner.Root, fullPath)` with `/` separators.
- **Nested submodules** need no special handling: the nearest top-level is the innermost repo.

**Fallback.** Some git versions print `--show-superproject-working-tree` differently, or not at all, when it's empty.
So parse defensively: lines that are 40 hex characters are the head; `true` or `false` is the shallow flag; the
first path is the top-level; a second path is the superproject.

## R3 — Rename map and candidates (FR-005–FR-007)

**Decision.**
- **One scan per owner, cached per command:** `log -l0 -M<t>% --diff-filter=R --name-status -z
  --format=%x1e%H%x00%ct HEAD`, parsed into ordered `Rename(Commit, Time, Old, New, Score)` records, newest first.
  `-l0` lifts the rename-detection limit, so a 1,100-file restructure commit isn't silently treated as delete + add.
- **Forward map**, old → set of distinct news. For a missing path P, its candidate:
  - no entry → none, `no rename found at <N>% similarity`, plus `(shallow history)` when the owner is shallow;
  - **two or more distinct targets anywhere in the history → none, `ambiguous rename`** (analysis A1: this includes a
    path re-created and renamed again later; "most recent" only orders the walk along an unambiguous chain);
  - otherwise follow the chain from the single new path until a path that exists in the working tree (at most 100
    steps, with a cycle guard). A chain that runs out without reaching an existing file → none, `rename chain ends
    at a missing file`.
- **Allowed roots:** the existing target's full path must be under the repo root or `source.local_dir`; otherwise
  none, `renamed outside the allowed roots`.
- **Candidate locations:** for a missing ref, use the same order as `SourceRefResolver`: repo-root-relative, then
  source-dir-relative for bare refs. The first location that has an owner and a candidate wins. If no location has an
  owner, the reason comes from the first location (`no git` / `not in a git repository` / …).
- **New ref text** (FR-007): if the original was source-dir-relative and the target is under the source dir, the new
  ref is source-dir-relative; otherwise it is repo-root-relative. `/` separators; `#anchor` copied verbatim.
- **The threshold is passed to git** (`-M<t>%`), so the score filtering is git's, not ours.

**Rationale.** A single forward map serves triage and relink. A distinct-target count catches both
renamed-to-two-places cases: two branches merged, and a re-created path renamed again.

## R4 — RELINK in triage (FR-012–FR-014)

**Decision.**
- **State and bucket:** `RefState.Relink` and `Bucket.Relink`, placed above `Removed`, with wire name `RELINK`.
- **Where it's decided:**
  - `TriageEngine.TriageTestAsync`: when a baselined ref's current section is missing with reason `not found`, ask
    `RenameResolver.CandidateFor(ref)`. A candidate gives `Relink` with `why = ["renamed to <target>"]`; otherwise
    `Removed` with the reason `not found; <candidate reason>` (FR-013), e.g. `not found; no git`.
  - **Refs without a baseline entry (analysis H1).** 084's `BaselineRecorder.ComputeAsync` skips refs whose file is
    missing, so a ref broken before the first `--init` is never baselined. Triage therefore also asks the resolver
    about:
    - an unbaselined ref of a test with an entry, when its file is missing: a candidate gives `Relink` (no baseline
      fingerprint) and the ref is not listed as unbaselined; no candidate keeps today's unbaselined handling;
    - the refs of a test with no entry (`TriageEngine.cs:78-83` today lists it as unbaselined and skips it): only for
      RELINK. With a RELINK ref, the test gets a verdict in the RELINK bucket and the same detail record as any
      triaged test (empty baseline refs, `current` filled, so `TRIAGE_STALE` works for `--group`) plus
      `unbaselined: true`; it is removed from `UnbaselinedIds` (so from `unbaselined.full`, `counts.unbaselined` and
      the human line). Otherwise it stays unbaselined as under 085.
    - a baselined test whose only unbaselined refs are RELINK refs is removed from `PartialIds`.
- **`relink-sources`** acts on the same refs. When a ref has no baseline key (or its test no entry), nothing moves:
  the new ref stays unbaselined until the next `--init` (FR-008, FR-009).
  - The resolver is created lazily on the first missing file, so workspaces with no missing files, and no-git
    workspaces, run no git command beyond that first failed probe.
- **Grouping:** `ChangedItem` gains an optional `Target`, and the signature includes it, so RELINK groups split by
  target.
- **`removed` stdout list:** only REMOVED refs, with the reason.
- **`update-ack`:** `RefuseRemovedAsync` already refuses missing sections (`--ids` path); its message now names
  `relink-sources` when the ref has a candidate. Today it skips tests with no entry and checks only refs in
  `entry.Refs` (`UpdateAckHandler.cs:252-264`); it now also asks the resolver about every missing ref with no
  baseline key, including all refs of a test with no entry, and refuses any with a candidate, so it never creates
  an entry for a test that still has a RELINK ref (analysis U1). The `--group` path (`UpdateAckHandler.cs:188-193`)
  checks the bucket name; `RELINK` is added there with the same message (analysis C5).

## R5 — `relink-sources` (FR-008–FR-011)

**Decision.** `Commands/Baseline/RelinkSourcesCommand` and its `Handler`:
1. Parse options: `--suite` / `--all-suites` (exactly one), `--min-similarity` (50–100, default 90), `--dry-run`.
2. Load config with `LoadForWriteAsync(Source | Tests)`.
3. Unless dry-run, take the baseline lock; then for each suite:
   - scan the suite (085 `ScanSuiteAsync`);
   - for each test, and each ref whose file is missing, ask for a candidate;
   - compute the new ref list: replace in place, de-duplicating when the target is already listed (FR-009);
   - decide orphan clearing: after relinking, every ref's section resolves, using `BaselineContext.GetSectionAsync`.
4. Then apply:
   - write the changed test files (R6) and regenerate the suite index (`IndexGenerator` + `IndexWriter`) from a
     rescan;
   - move the baseline keys (R6), only where an old key exists; never create an entry or key;
   - save the baseline with the scan (085 prune rule).
5. Output:

```json
{"command":"relink-sources","success":true,"scope":…,"dry_run":false,"min_similarity":90,
 "relinked":[{"suite":"s","test":"TC-1","refs":[{"from":"docs/a.md#X","to":"docs/guides/a.md#X"}],"orphan_cleared":false}],
 "not_relinked":[{"suite":"s","test":"TC-2","ref":"docs/gone.md#Y","reason":"no rename found at 90% similarity"}],
 "counts":{"tests_relinked":1,"refs_relinked":1,"refs_not_relinked":1},"warnings":[]}
```

   The human table is printed in every mode (stderr in JSON mode). Exit 0. Errors use the shared shape:
   `CONFIG_*`, `LOCK_TIMEOUT`, `BASELINE_UNREADABLE`, `SUITE_NOT_FOUND`, `INVALID_ARGUMENTS`.

## R6 — Writing the relinked test (FR-008–FR-010, SC-003)

**Decision.** `TestRefRewriter.Rewrite(fileText, layout, oldRef → newRef, dropRefs, clearOrphan) → string`:
- **The ref value:** take the `source_refs` frontmatter entry's raw text (`TestCaseLayout`). For each old ref, find
  its scalar, plain, `"…"` or `'…'`, and replace only the scalar's content with the new ref in the **same quoting
  style**, escaping for that style. A new value that the style can't represent (for example a plain scalar that would
  need quoting) switches that one item to double quotes.
- **A dropped ref** (de-duplication) removes its whole `- …` line.
- **Orphan clearing** goes through the 078 writer: `parsed with { Status = null, OrphanedReason = null,
  OrphanedDate = null }`. That writer drops those keys and reproduces every other key verbatim. It runs on the
  rewritten text, so the ref edit is preserved.
- **Verification:**
  1. re-parse the result;
  2. `SourceRefs` must equal the expected list exactly, and every other parsed field must equal the original;
  3. if not, fall back to `TestFileWriter` with `with { SourceRefs = expected }` (canonical list emission), and add a
     warning naming the file.
- **Writing:** temp file + move, through the existing atomic helper.

**Rationale.** It's the only way to guarantee that untouched refs keep their quoting. Verification makes the text edit
safe: a mistake can never persist silently.

**Baseline move:** for each relinked ref with a baseline fingerprint under the old key:
- if the entry has no key for the new ref, store the old fingerprint under the new key;
- drop the old key either way.

Snapshots are untouched; they're keyed by fingerprint.

## R7 — History bootstrap (FR-015–FR-018)

**Decision.**
- **Bootstrap point per owner:**
  - `--from-date D`: `log -1 --format=%H --before=<D> HEAD`. No output → refs in that owner are unbaselined with
    `no commit at or before <D>`.
  - `--from-ref R --owner P`: `rev-parse --verify --quiet R^{commit}` in owner P. Failure → refusal
    `GIT_REF_UNAVAILABLE` (exit 4), nothing written. Other owners use current content, with one warning listing
    them.
- **Path at the point:** `log -l0 -M90% --diff-filter=R --name-status -z --format=%x1e%H <point>..HEAD` gives the
  renames after the point. Apply them newest → oldest as new → old on the current path; that is the path at the
  point.
- **Content:** `cat-file --batch` over stdin, one process per owner, streaming `<point>:<path>` requests. A
  `missing` response → unbaselined, `file did not exist at <short-hash>`. Then the 085 `SectionExtractor.Extract`;
  AnchorNotFound → unbaselined, `anchor did not exist at <short-hash>`.
- **Fingerprint and snapshot** (`ComparisonText`) as usual; criteria use current content.
- **Merge rule:** `BaselineRecorder.Complete`, so only missing keys are filled and nothing is overwritten.
- **Docs in no repository:** current content, with a warning listing them.
- **Validation:**
  - `--force` with `--from-*`, `--from-ref` without `--owner`, both `--from-date` and `--from-ref`, an `--owner`
    without `--from-ref`, and a `--from-*` without `--init` → `INVALID_ARGUMENTS`;
  - an unparseable date (`DateTimeOffset.TryParse`, invariant culture, ISO 8601) → `INVALID_ARGUMENTS`.
- **Output:** the existing `source-baseline` JSON gains `from: {date|ref, owner?}` and per-suite `unbaselined:
  [{test, ref, reason}]`.

**Rationale.** `cat-file --batch` keeps a 2,500-ref bootstrap at one process per owner. `<point>..HEAD` gives exactly
the renames that happened after the point, so going backwards is exact.

## R8 — `docs topology` (FR-019)

**Decision.**
- `Commands/Docs/DocsTopologyCommand` (read-only).
- Collect every test's refs (all suites), resolve the files (`SourceRefResolver`), and group them by owner
  (`OwnerResolver`).
- JSON: `{owners:[{root (repo-relative when inside the repo, else absolute), is_submodule, head, shallow,
  referenced_docs}], unowned:[{doc, reason}], warnings}`, ordered by root, then doc.
- Human table in every mode. Exit 0. Config: `LoadForReadAsync(Source | Tests)`.

## R9 — Tests and fixtures (T1–T7)

**Decision.**
- **`GitFixture`** (CLI tests) creates throwaway repos with real `git`: `init`, `commit`, `mv`, and
  `submodule add` with `-c protocol.file.allow=always`. These fixture commands are the test's own and are not
  SPECTRA's.
- **Skipping without git:** the test projects use xunit 2.9.3, which has no dynamic skip, and no skip package is
  referenced. So new `[GitFact]` / `[GitTheory]` attributes (subclasses of `FactAttribute` / `TheoryAttribute`) set
  `Skip = "git is not installed; this test needs a real git repository"` in their constructor when
  `git --version` fails. This adds no dependency.
- **T1:** a `RecordingGitRunner` wraps the real runner and records `(args, env)`. After each command, a recursive scan
  for `index.lock` under every `.git` (including `.git/modules/*`) finds none. A non-ASCII path (`docs/über.md`)
  survives a rename.
- **T2, dubious ownership:** a `FakeGitRunner` that returns git's real dubious-ownership stderr for one path, because
  real ownership can't be simulated portably.
- **Performance:** 500 docs renamed in one commit, 2,500 tests (5 per doc, 10 suites), timed `relink-sources
  --all-suites --dry-run` under 30 s. The recording runner asserts exactly one rename scan (`log … --diff-filter=R`)
  per owner.
- **No-git guard (T6):** a `FakeGitRunner` that reports `no git` for everything. The existing 084/085 baseline tests
  run with real git present but outside any repo (temp dirs). Every existing test passes unchanged without git,
  except one deliberate reason-text change (FR-013), described below.

**Note on the existing tests.** In 084/085 tests, a missing doc in a temp dir now gives the reason `not found; not
in a git repository` instead of `not found`. One existing assertion
(`UpdateTriageCommandTests.RenamedHeadingOrDeletedFile_IsRemoved`, `UpdateTriageCommandTests.cs:143`,
`reason == "not found"`) changes to `not found; not in a git repository`. This is spec-driven (FR-013), and the change is listed in the Notes.
