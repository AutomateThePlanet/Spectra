# Feature Specification: Git-Aware Relinking and History Bootstrap (Phase 2b)

**Feature Branch**: `086-git-relink-sources`
**Created**: 2026-10-06
**Status**: Draft
**Input**: User description: "Phase 2b of the doc-change → criteria → test update workflow: git-aware relinking and
history bootstrap." The full text is in the triggering request: the facts, decisions D1–D8, the commands, the skill,
tests T1–T7, the rules, out of scope and acceptance. The decisions are settled and are not reopened here.

## Context

Phase 2a (specs 084 and 085) detects, without git, what changed in each doc section a test references. Two gaps
remain, and both need history:

1. **Moved or renamed doc files show as REMOVED.** Their tests can only be reviewed or deleted. In the RMH consumer
   repo, about 1,100 of 2,600 `source_refs` point at files a GitBook restructure renamed.
2. **Baselines can only start from today's content.** A doc change made before a test's first baseline is invisible.

This phase adds read-only git support for exactly these two cases. Everything else stays model-free and works
without git: when git is missing, or a doc isn't in a repo, the commands behave as under 085 and say why.

Facts (verified on `2ea8f16`):
- SPECTRA's only git call today is `git config user.name`, for execution identity.
- `_index.json` entries carry `source_refs`, so changing a test's refs means rewriting the test file and regenerating
  the suite index. The 078 lossless writer keeps the rest of the file byte-identical.
- Baselines are keyed by the ref as written and store no commit information.
- Docs may live in the tests repo, in a git submodule (RMH: `docs/requirements/gitbook-repo`), in nested submodules,
  in a `source.local_dir` outside the repo, or outside git entirely.

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr (shared shape, named codes);
- nothing is written on `--dry-run`;
- git is the undo path;
- **SPECTRA never modifies any git repository.**

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tests whose doc files were renamed are relinked, not lost (Priority: P1)

A documentation restructure renames and moves hundreds of doc files. Triage now reports the affected tests in a RELINK
bucket, naming each target file, instead of REMOVED. After the user confirms, one command rewrites each test's ref to
the new path, keeping the anchor exactly as written. The baseline entry moves with the ref and keeps its old
fingerprint, so any content change made in the rename commit still shows up at the next triage.

**Why this priority**: In the motivating repo this affects about 40% of all refs. Without it, those tests can only be
reviewed or deleted by hand.

**Independent Test**:
1. In a throwaway git repo, rename `docs/a.md` to `docs/guides/a.md` in a commit.
2. Triage shows the test as RELINK with the target.
3. `relink-sources` rewrites the ref to `docs/guides/a.md#Section`, and the next triage shows UNCHANGED (or CHANGED if
   the rename commit also edited the section).

**Acceptance Scenarios**:

1. **Given** a test whose ref's file was renamed in git, **When** triage runs, **Then** the test is in the RELINK
   bucket, grouped with other tests renamed to the same target, and `why` names the target path.
2. **Given** that test, **When** `relink-sources` runs, **Then**:
   - only the ref's path changes, and the `#anchor` stays exactly as written;
   - the rest of the test file is byte-identical;
   - the suite index is regenerated;
   - the baseline entry moves from the old key to the new key with its old fingerprint.
3. **Given** the rename commit also edited the referenced section, **When** triage runs after relinking, **Then** the
   ref is CHANGED (LOAD or LIGHT); **given** it didn't, the ref is UNCHANGED.
4. **Given** a chain of renames (`a → b → c`), **Then** the ref is relinked to `c`.
5. **Given** a path renamed to two different places, or a chain ending at a file that no longer exists, **Then** the
   ref is not relinked. It stays REMOVED with the reason `not found; ambiguous rename` or
   `not found; rename chain ends at a missing file`.
6. **Given** a test with `status: orphaned` whose refs all resolve after relinking, **Then** its `status`,
   `orphaned_reason` and `orphaned_date` are removed.
7. **Given** `--dry-run`, **Then** the command reports every old → new ref and writes nothing anywhere.
8. **Given** a doc renamed before any baseline existed (the ref never baselined, or its test with no entry),
   **When** triage runs, **Then** the test is in RELINK (flagged `unbaselined` when it has no entry, counted once);
   **when** `relink-sources` runs, the ref is rewritten and no baseline key is created.

---

### User Story 2 - The update skill offers relinking before anything else (Priority: P1)

When triage reports RELINK tests, the `spectra-update` skill shows the dry-run result (old → new per test), asks the
user, and relinks only after a yes. Then it continues with the remaining buckets, which now include any content
changes the renames carried.

**Why this priority**: Relinking rewrites many test files at once. It must be visible and confirmed, and it must
happen before the content review so that review sees the right sections.

**Independent Test**: The skill content runs `relink-sources --dry-run`, asks for confirmation, then runs it without
`--dry-run`, and never relinks without the user's yes.

**Acceptance Scenarios**:

1. **Given** a triage with RELINK tests, **When** the skill runs, **Then** it shows the dry-run old → new list and
   waits for the user's answer.
2. **Given** the user says yes, **Then** it runs `relink-sources` and re-runs triage before the bucket question.
3. **Given** the user says no, **Then** nothing is relinked, and the RELINK tests are reported as left for later.

---

### User Story 3 - Existing tests can be baselined at a past date (Priority: P2)

A team adopting the baseline wants doc changes made since the tests were written to show up. With
`source-baseline --init --from-date 2026-03-01`, missing baseline entries are filled from each doc's content as of
that date, following renames backwards. The next triage then reports what changed since.

**Why this priority**: Without it, the first baseline silently accepts every change made before it. It's opt-in, and
only matters when adopting the feature.

**Independent Test**:
1. In a throwaway repo, commit a doc, record the date, then change a number in it.
2. `source-baseline --init --from-date <date>` on a test without an entry records the old content.
3. Triage reports LOAD with the number change.

**Acceptance Scenarios**:

1. **Given** tests without baseline entries, **When** `--init --from-date D` runs, **Then** each missing entry is
   filled from the doc as of the last commit at or before D in the doc's own repo.
2. **Given** `--from-ref <rev> --owner <path>`, **Then** docs owned by that repo use that commit, and docs in other
   repos use current content, with a warning naming them.
3. **Given** a doc that was renamed after D, **Then** its content at D is read from its old path.
4. **Given** a ref whose file or anchor didn't exist at D, **Then** it is left unbaselined, with a reason.
5. **Given** a test that already has an entry, **Then** it is never changed. `--force` together with `--from-*` is
   refused.

---

### User Story 4 - Users can see which repositories own their docs (Priority: P3)

`spectra docs topology` lists each repository that owns referenced docs: its root, whether it is a submodule, its
current commit, whether its history is shallow, and how many referenced docs it owns. It also lists referenced docs
that are in no repository. This explains why relinking or bootstrap is or isn't available for a doc.

**Why this priority**: Diagnostic only. It makes the "no git" reasons understandable, especially with submodules.

**Independent Test**: In a fixture with docs in the main repo, a submodule, a nested submodule and outside git, the
command lists each owner and the unowned docs correctly.

**Acceptance Scenarios**:

1. **Given** docs in the repo, a submodule and a nested submodule, **Then** each is listed under its own owner, with
   `is_submodule` set correctly.
2. **Given** a doc outside any repo, or in a repo git refuses to read ("dubious ownership"), **Then** it is listed as
   unowned, with the reason.

---

### Edge Cases

- **Git not installed, or the docs not in any repo:**
  - triage never shows RELINK, and missing files are REMOVED with the reason `not found; no git` (git not installed)
    or `not found; not in a git repository` (the doc isn't in any repo);
  - `relink-sources` reports every missing ref as not relinked, with `no git` or `not in a git repository`, and
    changes nothing;
  - `--from-*` uses current content with a warning.
  None of this is an error.
- **Shallow clone:** renames older than the shallow boundary can't be seen. Triage reports `not found; no rename
  found at 90% similarity (shallow history)`, and topology shows `shallow: true`.
- **The doc's folder was deleted too:** the owner repo is found from the nearest existing parent folder.
- **A bare legacy ref** (`checkout.md`, resolved against `source.local_dir`): history is checked at both locations the
  resolver would try, in the same order (repo root, then source dir). The first location with a rename candidate
  wins.
- **The test already lists the target:** the old ref is dropped instead of creating a duplicate, and the baseline
  keeps the target's existing entry (D4).
- **The renamed file's new location is outside the allowed roots** (repo root and source dir): it is not a candidate.
  Triage reports `not found; renamed outside the allowed roots`.
- **The rename happened before the first baseline** (the ref was never baselined, or the test has no entry at all):
  the ref is still RELINK (FR-012). Relinking rewrites the ref and moves no key; the new ref stays unbaselined until
  the next `--init`.
- **A case-only rename on a case-insensitive file system:** the file still resolves, so nothing is relinked.
- **A rename threshold outside 50–100:** refused with `INVALID_ARGUMENTS`.
- **The ref's file exists, but its anchor is missing** (a heading rename): out of scope (D5). It stays REMOVED with
  `anchor not found`.
- **A lock left by another tool** (`index.lock`): SPECTRA never creates one. Its calls run with optional locks off and
  never write, so they don't contend with a concurrent `git` in the same repo.
- **Non-ASCII paths** (`docs/über.md`): they round-trip through git output unchanged.

## Requirements *(mandatory)*

### Functional Requirements

**Git access (D1, D2)**

- **FR-001**: One component makes every git call. It runs only the subcommands `rev-parse`, `log`, `show`,
  `ls-files` and `cat-file`, and nothing that writes to a repository.
- **FR-002**: Every call:
  - runs with optional locks disabled (`GIT_OPTIONAL_LOCKS=0`) and unquoted paths (`core.quotepath=false`);
  - uses NUL-separated output where the subcommand supports it;
  - is decoded as UTF-8.
  No call creates or modifies any file under any `.git` directory.
- **FR-003**: Git unavailable for a path is never an error: git missing, the path not in a repo, a repo git refuses
  ("dubious ownership"), or a failing call. Git features are skipped for that path, with a reason: `no git`,
  `not in a git repository`, `repository not readable (dubious ownership)`, or `git failed: <message>`. The command
  continues.
- **FR-004**: A doc's **owner** is the nearest repository containing it, found from the doc's folder, or the nearest
  existing parent folder when the folder is gone, and cached per folder. Submodules and nested submodules are ordinary
  owners. Paths are given to git relative to their owner.

**Rename detection (D3)**

- **FR-005**: For a ref whose file is missing, the candidate is found from the owner's history, at the similarity
  threshold (default 90%, overridable on `relink-sources` with `--min-similarity` 50–100):
  1. collect every rename of that path anywhere in the history;
  2. **a path with two or more distinct rename targets is ambiguous and never a candidate**, even when the renames
     happened at different times (for example `a.md → b.md`, then `a.md` re-created and renamed to `c.md`);
  3. otherwise follow the chain (`a → b → c`) from its single target until a path that exists now. "Most recent" only
     orders the walk along an unambiguous chain.
  Each owner's history is scanned once per command, and the result is cached as an old → new map.
- **FR-006**: There is **no candidate** when:
  - the path was renamed to more than one place (`ambiguous rename`);
  - the chain ends at a missing file (`rename chain ends at a missing file`);
  - the target is outside the allowed roots (`renamed outside the allowed roots`);
  - no rename is recorded at the threshold (`no rename found at <N>% similarity`, plus `(shallow history)` for
    shallow owners). Triage always uses the default 90%, so its REMOVED reason reads `not found; no rename found at
    90% similarity` (FR-013).
- **FR-007**: The candidate's new ref keeps the original ref's style:
  - the same base (repo-root-relative or source-dir-relative), when the target is under it, otherwise
    repo-root-relative;
  - `/` separators;
  - the `#anchor` exactly as written.

**Relinking (D4, D6)**

- **FR-008**: `spectra ai relink-sources (--suite <s> | --all-suites) [--min-similarity N] [--dry-run]` relinks
  every ref that has a candidate:
  1. rewrite the ref in the test file, changing only that value;
  2. regenerate the suite index;
  3. move the baseline key, **only if one exists**; a ref that was never baselined, or a test with no entry, gets no
     key, and the new ref stays unbaselined until the next `source-baseline --init`;
  4. clear the orphan status where FR-010 applies.
  This applies to every ref whose file is missing and has a candidate, whether or not the ref or its test has a
  baseline entry.
  It takes the baseline lock. With `--dry-run` it reports the same plan and writes nothing, not even the lock file.
- **FR-009**: When the old key exists, moving it keeps the **old fingerprint**; the entry is never re-baselined. If
  the test already has an entry for the new key, that one is kept and the old key is dropped. When there is no old
  key, no entry or key is created. If the test already lists the new
  ref, the old ref is dropped from the test instead of duplicating it.
- **FR-010**: A test with `status: orphaned` whose refs **all** resolve after relinking has `status`,
  `orphaned_reason` and `orphaned_date` removed. Resolving means no ref is REMOVED: each file exists and each anchor
  matches. Any other test's status is untouched.
- **FR-011**: The output lists, per test, the old → new refs, and per unrelinkable missing ref, its reason. The JSON
  document goes to stdout, and the human summary is printed in every mode (stderr in JSON mode). Errors use the shared
  failure shape. Exit code 0 when the command completes, whatever it finds.

**Triage (RELINK bucket)**

- **FR-012**: `update-triage` adds the state and bucket **RELINK**: a ref whose file is missing and has a candidate,
  **whether or not the ref or its test has a baseline entry**. RELINK ranks above REMOVED, so the bucket order is
  RELINK > REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED > LIGHT > COSMETIC > UNCHANGED.
  - In a test **with** an entry, an unbaselined ref whose file is missing and has a candidate is RELINK, not listed
    as an unbaselined ref.
  - The refs of a test **with no entry** are evaluated for RELINK only; such a test is not otherwise triaged. When
    it has a RELINK ref, it is listed once, under RELINK. It gets the same detail record as any triaged test (empty
    baseline refs, `current` filled for every ref) plus `unbaselined: true`, so the `update-ack --group` staleness
    check works for it. It is removed from `unbaselined.full` (stdout and detail file), and therefore from
    `counts.unbaselined` and the human UNBASELINED line, so the bucket counts still add up. Otherwise it stays in the
    unbaselined list as under 085.
  - A baselined test whose only unbaselined refs are RELINK refs is removed from the `partial` list.
- **FR-013**: For a RELINK ref, `why` names the target path (`renamed to docs/guides/a.md`). RELINK tests are grouped
  by target. REMOVED keeps its meaning ("gone, no candidate"). A missing file's REMOVED reason always starts with
  `not found` and adds the FR-003 / FR-006 reason after a semicolon: `not found; no git`, `not found; not in a git
  repository`, `not found; no rename found at 90% similarity`, `not found; ambiguous rename`, and so on. Other
  REMOVED reasons (`anchor not found`, `outside allowed roots`, `empty ref`) are unchanged.
- **FR-014**: `update-ack` refuses a test with a RELINK ref, as it does REMOVED (`REF_REMOVED`), and the message points
  at `relink-sources`, on both paths (`--ids` and `--group`). The `--ids` check also asks the rename resolver about
  every missing ref that has no baseline key, including all refs of a test with no entry, and refuses any with a
  relink candidate. `update-ack` never creates an entry for a test that still has a RELINK ref.

**History bootstrap (D7)**

- **FR-015**: `source-baseline --init` gains `--from-date <ISO date>`. For each owner, it uses the last commit at or
  before that date.
- **FR-016**: `source-baseline --init` gains `--from-ref <rev> --owner <path>`, which pins that owner to the given
  commit. Docs of other owners, and docs in no repository, use current content, with a warning naming them.
- **FR-017**: For each ref of a test **without** an entry (or each missing key of a partial entry):
  1. take the file at that commit, following renames backwards from the current path;
  2. take the section, using the 085 matcher;
  3. fingerprint it and write the snapshot as usual.
  A ref whose file or anchor didn't exist at that commit is left unbaselined, with a reason. Criteria fingerprints
  always use current content.
- **FR-018**: History bootstrap never changes an existing fingerprint. Refused with `INVALID_ARGUMENTS` (exit 4), with
  the same codes and messages as the contract, nothing written:
  - `--force` together with `--from-date` or `--from-ref`;
  - `--from-ref` without `--owner`;
  - `--owner` without `--from-ref`;
  - `--from-date` or `--from-ref` without `--init`;
  - `--from-date` together with `--from-ref`;
  - an unparseable date.
  An `--owner` that isn't a readable repository, or a `--from-ref` that doesn't resolve in it, is refused with
  `GIT_REF_UNAVAILABLE` (exit 4), and nothing is written.

**Topology**

- **FR-019**: `spectra docs topology [--output-format json]` (read-only) lists:
  - each owner repository: `root`, `is_submodule`, `head` commit, `shallow`, and the number of referenced docs it owns;
  - the referenced docs that have no owner, with the FR-003 reason.
  "Referenced docs" are the files that the refs of all tests in all suites resolve to.

**Guards**

- **FR-020**: No new configuration is required. Any new optional key goes in the existing `update` section and
  follows the 083 rule. The init-template and pre-existing-workspace guard tests stay green.
- **FR-021**: In a workspace with no git, every command behaves exactly as under 085, except that the REMOVED reason
  of a missing file gains the git reason (FR-013): `not found; no git` when git isn't installed, `not found; not in a
  git repository` when the doc isn't in any repo.
- **FR-022**: Output is deterministic: the same repository state gives byte-identical relink and triage output.

**Skill**

- **FR-023**: `spectra-update`, after triage, when RELINK tests exist:
  1. runs `relink-sources --dry-run` and shows old → new per test;
  2. asks the user, and relinks only on a yes;
  3. re-runs triage;
  4. continues with the bucket question.
  It never relinks without the user's yes. Exit-code handling and the NON-STOP contract are as in the other skills.
  For a first-time setup (tests without baseline entries), relinking comes **before** `source-baseline --init`:
  when triage has RELINK groups, the summary step does not offer `--init`; the offer moves to after the RELINK step
  and becomes `source-baseline --init --from-date <date>`, so the bootstrap follows the renames back to the
  pre-rename content, with plain `--init` as the fallback when there is no git or the user declines a date. With no
  RELINK groups, the summary step's `--init` offer is unchanged.
- **FR-024**: The skill's REMOVED step tells the user that a doc renamed with heavier edits can be found with
  `relink-sources --min-similarity <N>` (50–100), run with `--dry-run` first.

### Key Entities

- **Owner repository**: the nearest git repository containing a doc, with its root, submodule flag, head commit and
  shallow flag.
- **Rename map**: per owner, old path → new path from history, with the commit of each rename. Chains are followed
  forward for relinking and backward for bootstrap.
- **Relink candidate**: a missing ref, its target path, the new ref (same anchor, same style), or the reason there is
  none.
- **Bootstrap point**: per owner, the commit used for history bootstrap (from a date or a pinned ref).

## Required Tests

Fixtures are throwaway git repos created by the tests. Every git-dependent test is skipped with a clear reason when
git is absent.

| ID | Covers | Content |
|---|---|---|
| T1 | FR-001–FR-003 | Only the allowed subcommands are ever invoked (asserted on the recorded argument lists); optional locks are off; after every command no `index.lock` exists under any `.git` (including submodules); non-ASCII paths round-trip |
| T2 | FR-003, FR-004, FR-019 | Topology: docs in the same repo, a submodule, a nested submodule, outside git, and a simulated "dubious ownership" repo → the right owner, or unavailable with the reason |
| T3 | FR-005–FR-007 | Renames: plain; with edits above and below the threshold; a chain `a → b → c`; renamed to two places (ambiguous); renamed into a file later deleted; inside a submodule; no git → REMOVED with `not found; no git` |
| T4 | FR-008–FR-011 | Relink end to end: only the ref path changes (anchor kept, the rest of the file byte-identical); index regenerated; baseline key moved with its old fingerprint; the next triage is CHANGED when the rename commit edited the section, UNCHANGED when it didn't; orphan fields cleared only when all refs resolve; `--dry-run` writes nothing anywhere |
| T5 | FR-015–FR-018 | Bootstrap: `--from-date` and `--from-ref` fill only missing entries, follow renames backwards, leave refs that didn't exist then unbaselined with a reason; a doc changed after the date shows as CHANGED at the next triage; the refusal codes |
| T6 | FR-020, FR-021 | The 083/084 init-template and pre-existing-workspace tests stay green; a workspace without git behaves as under 085 (no RELINK; a missing file's REMOVED reason is `not found; no git`) |
| T7 | FR-022 | The same repository state gives byte-identical relink and triage output |
| — | FR-012–FR-014, FR-023, FR-024 | RELINK precedence and grouping; ack refusal; skill content (dry-run, confirmation, re-triage) |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a repository with 500 renamed doc files and 2,500 tests, `relink-sources --all-suites --dry-run`
  finishes in under 30 seconds, with one history scan per owner repository.
- **SC-002**: 100% of refs to files renamed by a plain rename or an unambiguous chain are relinked. 0 refs are
  relinked on an ambiguous or broken chain.
- **SC-003**: After relinking, 0 test files differ outside the rewritten ref values and the cleared orphan fields.
- **SC-004**: A content change made in the rename commit is reported at the next triage in 100% of T4 cases; none is
  silently accepted.
- **SC-005**: After a history bootstrap, a doc change made after the bootstrap date is reported as CHANGED.
- **SC-006**: 0 files are created or modified under any `.git` directory by any SPECTRA command (T1).
- **SC-007**: Every existing test passes unchanged without git, except one deliberate reason-text change (FR-013):
  `UpdateTriageCommandTests.RenamedHeadingOrDeletedFile_IsRemoved` expects `not found; not in a git repository`
  instead of `not found`. Existing suites stay green except the known `ProfileFormatLoaderTests` failure.
- **SC-008**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` and `docs/usage.md` describe
  `relink-sources`, the RELINK bucket, `--from-date` / `--from-ref`, `docs topology` and the git requirements.

## Assumptions

- **D7's "last commit at or before the date"** is computed with `log -1 --before=<date>`, not `rev-list`, so that git
  access stays within D1's five allowed subcommands. The result is the same commit.
- **"Resolve" in D6** means at section level: the file exists and the anchor matches (no REMOVED ref). A file that
  exists with a missing heading still counts as unresolved, so the orphan status stays.
- **Docs in no repository during a `--from-date` bootstrap** fall back to current content with a warning, the same as
  other owners under `--from-ref`, instead of being left unbaselined.
- **An explicit `--from-ref` / `--owner` that can't be used** is refused rather than ignored, because silently
  baselining current content would hide exactly the changes the user asked to see.
- **No new config key** is added in this phase. The threshold is a per-command option only.
- **Topology reports a shallow flag**, using `rev-parse --is-shallow-repository`, so that the `(shallow history)`
  reason is explainable.

## Out of Scope

- Heading-rename relinking (D5).
- Using git renames in `docs changed` or for criteria moves.
- Section text and diffs in the update prompt (Phase 3).
- Re-verification after edits (Phase 4).
- The deferred 085 review findings.
- Any operation that writes to a git repository.
