# Feature Specification: Review Fixes for Spec 086 — Git-Aware Relinking

**Feature Branch**: `087-review-fixes-086`
**Created**: 2026-10-06
**Status**: Draft
**Input**: User description: "Review fixes for spec 086 (git-aware relinking, Phase 2b). No new features. Every fix needs
a regression test that fails on ede6680 and passes after. Baseline and test-file formats do not change." The full text
is in the triggering request: the facts, fixes F1–F5 (settled, not reopened here), tests T1–T6, rules, out of scope and
acceptance.

## Context

Spec 086 added read-only git support: rename detection (the RELINK bucket and `relink-sources`), history bootstrap
(`source-baseline --from-date / --from-ref`) and `docs topology`. Its review found five defects:

1. **A submodule that isn't checked out looks like deleted docs.** In a clone made without its submodules, every doc in
   the submodule is missing on disk. Its owner then resolves to the superproject, which has no renames for those paths,
   so every ref is REMOVED as "no rename found" — and the update skill offers to review or delete the tests.
2. **SPECTRA can run a `git.exe` from the workspace.** Git is started by its bare name, so on Windows a `git.exe` in
   the current folder (for example, committed to a repository being reviewed) can run instead of the real one.
3. **An inherited git environment redirects every call.** Inside a git hook (or any shell with `GIT_DIR`,
   `GIT_WORK_TREE`, `GIT_INDEX_FILE` …), SPECTRA's `git -C <submodule>` calls read the outer repository instead.
4. **A path whose casing differs from git's is never found.** On Windows and macOS a ref written
   `docs/Store-Manager/Setup.md` opens a file that git records as `docs/store-manager/setup.md`; when that file is
   renamed, the exact-case history lookup misses it.
5. **The bootstrap uses a fixed threshold and hides failures.** The "renames since" scan always uses 90% and treats a
   failed git call as "no renames", so a ref is silently read from the wrong path.

Facts (verified on `ede6680`):
- `ProcessGitRunner` and `UserIdentityResolver` (execution identity, `git config user.name`) both start git by its bare
  name.
- `GitReader`'s environment adds only `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0`; everything else is inherited.
- Rename-history keys and the bootstrap's path-at-point walk compare paths exactly; the owner-relative path keeps the
  ref's own casing.
- The bootstrap's "renames since" scan uses `-M90%` and turns a failed call into an empty list.
- A clone without `--recurse-submodules` leaves the submodule folder empty; `git ls-files -s -z` in the superproject
  records the submodule as a gitlink (mode `160000`), and `rev-parse` inside the empty folder answers for the
  superproject.

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr (shared shape, named codes);
- nothing is written on `--dry-run`;
- git is the undo path;
- **SPECTRA never modifies any git repository** (only `rev-parse`, `log`, `show`, `ls-files`, `cat-file` run).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A submodule that isn't checked out is reported as a setup problem (Priority: P1)

A user clones the tests repository without its docs submodule and runs the update workflow. Instead of hundreds of
REMOVED tests and an offer to delete them, triage reports one group per submodule saying the submodule isn't checked
out and how to initialise it. The skill tells the user to run that and re-run triage.

**Why this priority**: The current behaviour invites deleting valid tests. In the motivating repository the docs live
in a submodule, so a fresh clone hits this on every ref.

**Independent Test**: In a superproject with a docs submodule, clone it without submodules and triage: every
submodule ref is REMOVED with the submodule reason, in one group per submodule; none is RELINK; `relink-sources` lists
them as not relinked with the same reason.

**Acceptance Scenarios**:

1. **Given** a ref into a submodule whose folder has no checkout, **When** triage runs, **Then** the ref is REMOVED
   with `not found; submodule not checked out: <submodule path> (run git submodule update --init)`, and rename history
   is not consulted for it.
2. **Given** several tests in a suite with such refs into the same submodule, **Then** they form one group for that
   submodule, not one group per test.
3. **Given** a nested submodule that isn't checked out inside a checked-out submodule, **Then** the reason names the
   nested submodule's path.
4. **Given** a checked-out submodule, **Then** nothing changes: renames inside it are RELINK, and a file deleted in it
   gets the normal reasons.
5. **Given** the update skill sees this reason, **Then** it tells the user to initialise the submodule and re-run
   triage, and does not list those tests for review or offer to delete them.
6. **Given** `update-ack` or `relink-sources` on such a ref, **Then** `update-ack` refuses it like any REMOVED ref, and
   `relink-sources` lists it under `not_relinked` with the submodule reason.

---

### User Story 2 - SPECTRA never runs a git program from the workspace (Priority: P1)

Wherever SPECTRA starts git — rename detection, bootstrap, topology, or the execution identity — it runs the git found
on the system PATH, by absolute path, never a program that happens to sit in the current folder.

**Why this priority**: Running an untrusted executable from a reviewed repository is a code-execution risk.

**Independent Test**: The executable lookup, given PATH entries, the current folder and which files exist, never picks
a file from the current folder or from an empty, `.` or relative PATH entry, and picks the first absolute match.
Neither git caller starts git by its bare name.

**Acceptance Scenarios**:

1. **Given** a `git.exe` in the current folder and a real git later on PATH, **Then** the real git runs.
2. **Given** PATH entries that are empty, `.` or relative, **Then** they are skipped.
3. **Given** no git anywhere on PATH, **Then** git is unavailable (`no git`), exactly as today, and the execution
   identity falls back to the OS user name as today.

---

### User Story 3 - An inherited git environment never redirects a call (Priority: P1)

A user runs SPECTRA from a git hook or a shell where git environment variables are set. SPECTRA's git calls still read
the repository they name.

**Why this priority**: Inside a hook every call reads the wrong repository, so submodule renames vanish and owners are
wrong — silently.

**Independent Test**: With the redirecting variables set in the test process, every recorded git call's environment
lacks them and still has optional locks and prompts off; a real repository run with a simulated pre-commit-hook
environment finds the submodule's renames.

**Acceptance Scenarios**:

1. **Given** `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE` and the rest of the F3 list set, **When** any SPECTRA command
   runs git, **Then** none of them reaches the git process.
2. **Given** a simulated pre-commit hook (the superproject's `GIT_DIR` and `GIT_INDEX_FILE` set), **When** triage runs
   on a ref renamed inside a submodule, **Then** it is RELINK with the submodule's target.

---

### User Story 4 - Renames are found when only the path's casing differs (Priority: P2)

On Windows and macOS a ref whose casing differs from git's record of the file is still relinked when the file is
renamed, and the new ref uses git's casing.

**Why this priority**: Doc sets written for case-insensitive file systems routinely mix casing; on those platforms the
ref works today and should keep working after a rename.

**Independent Test**: On a case-insensitive platform (or through the comparer seam), a ref
`docs/Store-Manager/Setup.md` whose file git recorded as `docs/store-manager/setup.md` and then renamed is RELINK with
git's target casing; two history paths differing only in case are ambiguous.

**Acceptance Scenarios**:

1. **Given** Windows or macOS and exactly one history path equal to the missing path ignoring case, **Then** that path
   is used and the lookup continues as today.
2. **Given** two or more distinct history paths equal to it ignoring case, **Then** the result is `ambiguous rename`.
3. **Given** Linux, **Then** matching stays exact.
4. **Given** a history bootstrap, **Then** the path-at-point walk follows the same rule.

---

### User Story 5 - The bootstrap uses the chosen threshold and reports a failed history read (Priority: P2)

`source-baseline --init --from-date / --from-ref` takes the same `--min-similarity` option as `relink-sources`, and a
history read that fails leaves the affected refs unbaselined with the reason, instead of reading them from the wrong
path.

**Why this priority**: A silent wrong baseline hides exactly the changes the bootstrap exists to show.

**Independent Test**: A doc renamed with edits below 90% similarity is followed back with `--min-similarity 60`; a
forced failure of the history read leaves the refs unbaselined with `git history unavailable: …`.

**Acceptance Scenarios**:

1. **Given** `--min-similarity 60`, **Then** the "renames since" scan uses 60%.
2. **Given** a value outside 50–100, **Then** it is refused exactly as `relink-sources` refuses it.
3. **Given** the scan fails for an owner, **Then** each ref of that owner that needed it is unbaselined with
   `git history unavailable: <git reason>`.
4. **Given** a ref whose path didn't exist at the commit, **Then** the reason stays `file did not exist at <hash>`.

---

### Edge Cases

- **The submodule folder is missing entirely** (not just empty): the nearest existing folder is its parent; the gitlink
  is found at one of the missing path's parent folders, so the submodule reason applies.
- **A gitlink whose folder is checked out**, but the referenced file is missing in it: normal behaviour (the submodule
  is the owner; RELINK or the normal REMOVED reasons).
- **A nested submodule not checked out inside a checked-out one**: the checked-out submodule records the nested
  gitlink; the reason names the nested submodule's path, relative to the repository root.
- **A test with refs into two unchecked-out submodules**: one REMOVED item per submodule; the test's group is keyed on
  both.
- **History bootstrap of a ref in an unchecked-out submodule**: left unbaselined with the submodule reason (no history
  is read for it).
- **`git.exe` reached through a PATH entry in quotes** (`"C:\Program Files\Git\cmd"`): the quotes are removed before the
  entry is checked.
- **A PATH entry with an unexpanded variable** (`%SystemRoot%\…`): not absolute, so skipped.
- **Environment variable names on Windows are case-insensitive**: `git_dir` is removed like `GIT_DIR`.
- **A missing path whose casing matches one history path exactly and another one ignoring case** (Windows/macOS): two
  distinct matches → `ambiguous rename`.
- **`--min-similarity` on `source-baseline` without `--from-*`**: refused (`INVALID_ARGUMENTS`); the threshold only
  applies to the history scan.

## Requirements *(mandatory)*

### Functional Requirements

**F1 — Unchecked-out submodule**

- **FR-001**: For a ref whose file is missing, before any rename lookup, SPECTRA checks whether the path lies in a
  submodule that isn't checked out: the repository owning the nearest existing folder records a gitlink (mode
  `160000`) at that folder or at one of the missing path's parent folders, and that submodule folder has no checkout.
  The gitlinks are read once per repository per command (`ls-files -s -z`).
- **FR-002**: Such a ref is never a relink candidate. Its reason is `submodule not checked out: <submodule path> (run
  git submodule update --init)`, with the submodule path relative to the repository root and `/` separators. Triage
  reports it as REMOVED with `not found; ` before that reason (the 086 convention).
- **FR-003**: In triage, all such refs of a test that point into the same submodule form one changed item keyed on the
  submodule, so the tests of a suite with refs into the same submodule form one group per submodule. These groups are
  not chunked by the group size.
- **FR-004**: `update-ack` refuses a test with such a ref like any REMOVED ref (`REF_REMOVED`). `relink-sources` lists
  the ref under `not_relinked` with the submodule reason. A history bootstrap leaves the ref unbaselined with it.
- **FR-004a** (analysis U1, A2): Triage adds one `notes` line per submodule that isn't checked out:
  `N reference(s) from M test(s) point into submodule <path>, which isn't checked out (run git submodule update
  --init)`. N counts (test, ref) pairs — a ref listed by three tests counts 3 — over every missing ref into that
  submodule, baselined or not (including refs of tests with no baseline entry); M is the number of distinct tests.
  `source-baseline --init` — plain, `--from-date` and `--from-ref` — adds the same line, worded the same way, as a
  warning (JSON `warnings` and a `Warning:` line on stderr), counted over the tests in its scope, and leaves those refs
  unbaselined.
- **FR-005**: The `spectra-update` skill treats this reason as a setup problem: it names each submodule once, tells the
  user to run `git submodule update --init` (the user runs it; the skill never does, and the command appears only
  inline in prose, never in a fenced block) and re-run triage, and does not list those tests for review or offer
  `spectra-delete`. In the first-time setup, while triage's `notes` contain the submodule line, the skill tells the user
  to initialise the submodule and re-run triage, and does not offer `source-baseline --init` until the note is gone.

**F2 — Git executable**

- **FR-006**: One shared lookup in the shared library finds the git executable: the first match on PATH, considering
  only absolute PATH entries (after removing surrounding quotes) and skipping empty, `.` and relative entries; on
  Windows only `git.exe` (no `.cmd` / `.bat`, no PATHEXT). The current folder is never searched. The result is cached
  per process.
- **FR-007**: Not found → git is unavailable with the reason `no git`, as today.
- **FR-008**: Every place SPECTRA starts git — the git reader's runner and the execution identity lookup — starts the
  resolved absolute path, never the bare name, with no shell and arguments passed one by one.

**F3 — Git environment**

- **FR-009**: Every git process SPECTRA starts gets an environment without `GIT_DIR`, `GIT_WORK_TREE`, `GIT_INDEX_FILE`,
  `GIT_OBJECT_DIRECTORY`, `GIT_ALTERNATE_OBJECT_DIRECTORIES`, `GIT_COMMON_DIR`, `GIT_NAMESPACE`,
  `GIT_CEILING_DIRECTORIES`, `GIT_DISCOVERY_ACROSS_FILESYSTEM`, `GIT_CONFIG`, `GIT_CONFIG_PARAMETERS`,
  `GIT_CONFIG_COUNT`, and any `GIT_CONFIG_KEY_*` / `GIT_CONFIG_VALUE_*`, compared ignoring case on Windows.
- **FR-010**: `GIT_OPTIONAL_LOCKS=0` and `GIT_TERMINAL_PROMPT=0` stay set on every call of the git reader.
- **FR-011**: One helper builds this environment for both callers (the git reader's runner and the execution identity
  lookup).

**F4 — Casing**

- **FR-012**: On Windows and macOS (the platforms where SPECTRA already compares paths ignoring case), a path is looked
  up in an owner's rename history ignoring case: exactly one distinct history path that matches → it is used (git's
  casing) and the lookup continues as today, including at each step of a chain; two or more distinct matches →
  `ambiguous rename`. Linux keeps exact matching.
- **FR-013**: The bootstrap's path-at-point walk follows the same rule; an ambiguous match leaves the ref unbaselined
  with `ambiguous rename`.
- **FR-014**: A relinked ref uses the target's casing as git records it.

**F5 — Bootstrap threshold and failures**

- **FR-015**: `source-baseline --init --from-date / --from-ref` accepts `--min-similarity <N>` (50–100, default 90) with
  the same validation and message as `relink-sources`, and uses it for the "renames since" scan. `--min-similarity`
  without `--from-date` or `--from-ref` is refused (`INVALID_ARGUMENTS`).
- **FR-016**: If the "renames since" scan fails for an owner, each ref of that owner that needs it is left unbaselined
  with `git history unavailable: <git reason>`; it is never read as if there were no renames.
- **FR-017**: A ref whose path didn't exist at the commit keeps `file did not exist at <hash>`.

**Compatibility**

- **FR-018**: The baseline file, snapshots, triage detail file and test-file formats don't change. JSON shapes don't
  change apart from two optional keys: `"submodule": true` on the ref item of an F1 group (like 086's `target`), and
  `min_similarity` in `source-baseline`'s `from` object. Otherwise only reason strings and the grouping of F1 refs
  differ.
- **FR-019**: The allowed git subcommands stay `rev-parse`, `log`, `show`, `ls-files`, `cat-file`.
- **FR-020**: Without git, every command behaves as on `ede6680`. The 083/084 init-template and pre-existing-workspace
  guard tests and the 086 git tests stay green, apart from deliberate reason-text changes listed in the tasks' notes.

### Required Tests

Every fix has a regression test that fails on `ede6680` and passes after. Git-dependent tests are skipped with a reason
when git is absent.

| ID | Covers | Content |
|---|---|---|
| T1 | FR-001–FR-005 | A superproject with a docs submodule, cloned without submodules (empty folder): every ref REMOVED with the F1 reason, one triage group per submodule, never RELINK, `relink-sources` lists them in `not_relinked`; a nested submodule not checked out; a checked-out submodule unaffected; a missing file in a checked-out submodule keeps the normal reasons; `update-ack` refuses; a fresh clone with no baselines at all → the triage note and the `--init` warning (plain and `--from-date`). Skill content: the F1 reason → initialise-and-re-run instruction (inline, never fenced), no review list, no `spectra-delete` hint, and the `--init` offer withheld while the note is present |
| T2 | FR-006–FR-008 | The lookup as a pure function over PATH entries, the current folder and a file-existence check (table): a `git.exe` in the current folder is never chosen; empty, `.`, relative entries skipped; quoted entry accepted; first absolute match wins; none → unavailable. Source guard: neither git caller starts git by the bare name |
| T3 | FR-009–FR-011 | With every F3 variable set in the test process, every recorded git call's environment lacks them and keeps `GIT_OPTIONAL_LOCKS=0` / `GIT_TERMINAL_PROMPT=0`; the execution identity uses the same helper; a real-repository run from a simulated pre-commit-hook environment finds the submodule's renames |
| T4 | FR-012–FR-014 | Windows/macOS (or the comparer seam): `docs/Store-Manager/Setup.md` vs git's `docs/store-manager/setup.md`, renamed → RELINK with git's target casing; two history paths differing only in case → ambiguous; bootstrap path-at-point follows the rule. Linux: exact matching unchanged |
| T5 | FR-015–FR-017 | A doc renamed with edits below 90% is followed back with `--min-similarity 60`; a forced failing history call → `git history unavailable: …`; option validation and refusals match `relink-sources` |
| T6 | FR-018–FR-020 | The 083/084 guard tests, the 086 git fixture tests and the no-git workspace behaviour stay green; the allowed subcommands are unchanged |

### Key Entities

- **Gitlink**: a submodule recorded in a repository's index (mode `160000`) at a path; "checked out" when its folder
  contains a `.git` entry.
- **Git executable lookup**: the absolute path of the git program, or unavailable; resolved once per process.
- **Git environment**: the inherited environment minus the redirecting variables, plus the two fixed settings.
- **Path comparer**: exact on Linux, ignoring case on Windows and macOS; used for rename-history lookups.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a clone without its docs submodule, 100% of refs into it are reported with the submodule reason, in
  exactly one group per suite and submodule, or — when they have no baseline — counted in one triage note and one
  `source-baseline --init` warning per submodule; 0 are RELINK, 0 are offered for deletion, and the skill offers no
  `--init` while the note is present.
- **SC-002**: 0 git processes are started from the current folder or from an empty, `.` or relative PATH entry.
- **SC-003**: 0 git calls receive any of the redirecting variables; renames are found identically inside and outside a
  hook environment.
- **SC-004**: On Windows and macOS, 100% of renames whose only difference from the ref is casing are relinked, with
  git's casing in the new ref.
- **SC-005**: 0 bootstrap refs are read from a wrong path because a history read failed; each such ref names the
  failure.
- **SC-006**: Every fix has at least one test that fails on `ede6680` and passes after.
- **SC-007**: Existing suites stay green except the known `ProfileFormatLoaderTests` failure.
- **SC-008**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` (the F1 reason, `--min-similarity`
  on `source-baseline`) and `docs/usage.md` (the submodule setup note) are updated.

## Assumptions

- **"No checkout"** means the submodule folder doesn't exist or contains no `.git` entry (file or folder). A folder
  with a `.git` entry counts as checked out even if it is at another commit.
- **Reason prefix**: as in 086, triage prefixes `not found; `; `relink-sources`' `not_relinked[].reason` and the
  bootstrap's `unbaselined[].reason` carry the bare reason (`submodule not checked out: …`). This is "the same reason"
  in F1.
- **Grouping** stays per suite (groups belong to a suite and `update-ack --group` takes one suite); "one group per
  submodule" is per suite, and those groups are not chunked so a large submodule doesn't produce dozens of groups.
- **`--min-similarity` without `--from-*`** is refused rather than ignored, matching the 086 rule that `--from-*`
  options without their partner are refused.
- **Execution identity**: when no git is found, it falls back to the OS user name, as it does today when git is
  missing.
- **Known residual (F4)**: a bootstrap ref whose casing differs from git's and whose file was not renamed since the
  point is still read under the ref's casing, and git's object lookup is case-sensitive, so it is reported as `file did
  not exist at <hash>`. F4 covers rename lookups and the path-at-point walk only.
- **No new config key** and no new command or option other than `--min-similarity` on `source-baseline`.

## Out of Scope

- Phase 3 and later.
- The deferred 085 review findings.
- Heading-rename relinking.
- Any change to the baseline or test-file format.
- Running `git submodule update` or any other repository-changing command for the user.
