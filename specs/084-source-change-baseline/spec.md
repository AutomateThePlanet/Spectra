# Feature Specification: Source Change Baseline — Deterministic Change Detection Without Git (Phase 2a)

**Feature Branch**: `084-source-change-baseline`
**Created**: 2026-10-01
**Status**: Draft
**Input**: User description: "Phase 2a of the doc-change → criteria → test update workflow: deterministic change
detection without git." The full text is in the triggering request: the problem, decisions D1–D8, the per-ref states,
the commands, the skill, tests T1–T9, the rules, out of scope and acceptance. The decisions are settled and are not
reopened here.

## Context

Today `spectra ai update` decides whether a test is OUTDATED from word overlap with the **current** document
(`TestClassifier`). Nothing records what the document said when the test was written. So it can't tell "this section
changed" from "this test was always worded loosely":
- a real change that keeps the vocabulary (`10GB` → `50GB`) stays UP_TO_DATE;
- a paraphrased test stays OUTDATED forever.

This phase records, for each test, a fingerprint of every document section it references (`source_refs`) and of every
acceptance criterion it links (`criteria`). Later runs report exactly what changed since. It ranks each change by how
likely it is to need a test edit, and groups tests that share the same changes so they can be reviewed together.

Everything is model-free and deterministic, and no git history is used. Git topology, relinking moved files and
section-scoped edit prompts are later phases (2b, 3).

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr, in the shared shape with named error codes;
- the CLI stays model-free and deterministic;
- nothing is written on `--dry-run`;
- git is the undo path;
- any new config key is checked against the embedded init template and a pre-existing workspace.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See which tests are affected by documentation changes since they were written (Priority: P1)

A QA engineer edits product documentation, or pulls someone else's edits. They want to know which tests need another
look, and why, without a model guessing from vocabulary.

They run one triage command for a suite or for all suites. It reports each affected test by how serious the change is:
- the referenced section was removed;
- a linked criterion was removed;
- a substantive ("load-bearing") change;
- a linked criterion's wording changed;
- a light wording change;
- a cosmetic change.

Tests that share the same changes are grouped, and each group says why it was flagged, for example "numbers: 30→45".

**Why this priority**: This is the whole point of the phase. Without it the update workflow can't separate a real
change from a test that was always loosely worded.

**Independent Test**:
1. Create a workspace with one test whose `source_refs` points at `docs/storage.md#Quotas`, and record its baseline.
2. Change "10GB" to "50GB" in that section and run triage.
3. The test is reported as LOAD with `why` naming the number change.
4. A second test whose wording paraphrases an unchanged section is reported as UNCHANGED.

**Acceptance Scenarios**:

1. **Given** a baselined test referencing a section, **When** a number in that section changes (`10GB` → `50GB`),
   **Then** triage reports the test in the LOAD bucket, and `why` names `numbers: 10gb→50gb`.
2. **Given** a baselined test whose wording differs from its unchanged source section, **When** triage runs, **Then**
   the test is UNCHANGED and appears in no bucket other than the UNCHANGED count.
3. **Given** a baselined test, **When** only markup, whitespace or letter case changes in its section, **Then** the
   ref is COSMETIC, the baseline is refreshed for that ref (unless `--no-auto-ack`), and the test does not appear in
   any group that needs review.
4. **Given** a baselined test, **When** a sentence in its section changes wording without any number or modal word
   changing, and no more than `light_threshold_words` words change, **Then** the test is in the LIGHT bucket.
5. **Given** a baselined test, **When** "must" becomes "should" in its section, **Then** the test is LOAD, and `why`
   names the modal change.
6. **Given** a baselined test, **When** the heading its anchor points at is renamed or deleted, or its file is
   deleted, **Then** the test is REMOVED and listed in `removed` with the ref.
7. **Given** a baselined test linking criterion `AC-12`, **When** the criterion's text changes, **Then** the test is
   CRITERIA_CHANGED; **when** `AC-12` no longer exists in any criteria file, **then** it is CRITERIA_REMOVED.
8. **Given** ten tests that all reference the same two changed sections and `group_size` 4, **When** triage runs,
   **Then** they form three groups of 4, 4 and 2, with the same bucket, suite and refs.
9. **Given** any workspace, **When** triage runs in JSON mode, **Then** stdout is one compact JSON document, the full
   detail (word diffs) is in `.spectra/update-triage.json`, and the human summary table is on stderr.

---

### User Story 2 - Baselines are recorded automatically, and existing workspaces can adopt them (Priority: P1)

The baseline must exist before a change can be detected. When the generation workflow writes a new test, or the update
workflow writes an edited test, the test's current references and criteria are recorded with no extra step. A
workspace created before this phase has no baselines. One bootstrap command adopts the current content as the
baseline, and from then on triage is meaningful.

**Why this priority**: Without automatic recording, the baseline would drift from the tests. Without a bootstrap,
existing users couldn't use the feature at all.

**Independent Test**:
1. Ingest a generated test and check that the suite's `_source-baseline.json` has an entry for it.
2. In a workspace without baseline files, triage reports every test as UNBASELINED and nothing else.
3. Run `source-baseline --init --all-suites`; triage then reports every test as UNCHANGED.

**Acceptance Scenarios**:

1. **Given** a generated test with `source_refs` and `criteria`, **When** `ingest-tests` persists it, **Then** the
   suite's baseline has an entry for it, with one hash per ref and one per criterion, and a snapshot exists for each
   ref.
2. **Given** an edited test, **When** `ingest-update` persists it, **Then** its baseline entry is replaced with the
   current content of its current refs and criteria.
3. **Given** a workspace with tests and no baseline files, **When** triage runs, **Then** every test is UNBASELINED,
   no test is reported as changed, and no file except the triage scratch file is written.
4. **Given** that workspace, **When** `source-baseline --init --all-suites` runs and then triage runs, **Then** every
   test is UNCHANGED.
5. **Given** a suite whose tests already have entries, **When** `source-baseline --init` runs without `--force`,
   **Then** no existing hash changes; with `--force`, every entry is recomputed from current content.
6. **Given** `--dry-run` on any of these commands, **Then** nothing is written anywhere, and the output reports what
   would be written.

---

### User Story 3 - Acknowledge tests that were reviewed and need no edit (Priority: P2)

After reviewing a group, the user often decides the tests are still correct; for example, a reworded paragraph didn't
change the behaviour. They acknowledge those tests, by ID or by group. The baseline moves to the current content, and
the tests stop appearing in triage. Tests that are not acknowledged keep their old baseline, so they show up again on
the next triage. Nothing is silently deferred.

**Why this priority**: Without an acknowledgement step, reviewed tests would reappear forever, or the user would have
to make a pointless edit.

**Independent Test**:
1. Change a section and triage; the test is reported.
2. Run `update-ack --suite s --ids TC-1`.
3. Triage again; the test is UNCHANGED, and the test file is byte-identical.

**Acceptance Scenarios**:

1. **Given** a reported test, **When** `update-ack --suite s --ids TC-1` runs, **Then** its baseline entry (refs and
   criteria) is refreshed to current content, its test file is untouched, and the next triage reports it as
   UNCHANGED.
2. **Given** the last triage reported group `G2`, **When** `update-ack --suite s --group G2` runs and the group's
   sections and criteria still have the content triage saw, **Then** every test in the group is acknowledged.
3. **Given** a group whose sections changed again after triage, **When** `update-ack --group` runs, **Then** it is
   refused (`TRIAGE_STALE`), and nothing is written.
4. **Given** a reported test that is not acknowledged and not edited, **When** triage runs again, **Then** it is
   reported again with the same state.
5. **Given** a test whose ref is REMOVED, **When** `update-ack` names it, **Then** it is refused (`REF_REMOVED`),
   nothing is written, and the next triage still reports it as REMOVED.

---

### User Story 4 - The update skill drives triage instead of word overlap (Priority: P2)

A user asks Claude to update the tests after a doc change. The `spectra-update` skill:
1. runs triage and shows the summary;
2. lists REMOVED and CRITERIA_REMOVED tests for review, without editing them, and tells the user how to clear each
   one: fix the ref by hand and then `update-ack`, or, if the feature itself was removed, delete the test with the
   `spectra-delete` skill;
3. asks which buckets to process: LOAD only (recommended; LOAD and CRITERIA_CHANGED), LOAD + LIGHT (adds LIGHT),
   chosen groups (from those three buckets only), or report only;
4. runs the existing compile-update-prompt / edit / ingest-update loop for each test in the selected groups;
5. acknowledges the tests the user reviewed and kept as they are;
6. reports the counts.

"Update all" is one `update-triage --all-suites`.

**Why this priority**: The skill is how most users reach the update workflow. It must stop using `spectra ai update`
and its word-overlap classifier.

**Independent Test**: The skill content names `update-triage`, `update-ack`, the bucket options with LOAD-only
recommended, and the CONFIG_MISSING / CONFIG_INVALID handling. It doesn't call `spectra ai update`.

**Acceptance Scenarios**:

1. **Given** the installed skill, **When** it is read, **Then** its steps are triage → summary → removed list → bucket
   question → per-test update loop → ack → counts, and no step runs `spectra ai update`.
2. **Given** a triage or ack call that exits non-zero, **When** the skill handles it, **Then** it reports the
   `error_code`, stops for that suite, and retries once only on `LOCK_TIMEOUT`, as the `spectra-criteria` skill does.
3. **Given** the NON-STOP contract, **When** no single `spectra` call covers a step, **Then** the skill stops and
   reports instead of improvising with shell loops, interpreter pipes or hand edits.

---

### User Story 5 - The baseline lives safely next to tests, and its output is deterministic (Priority: P3)

The baseline is committed to git next to the tests. It must never be mistaken for a test, an index or a suite by any
existing command. It moves with its suite on rename and goes with it on delete. Repeated runs on the same workspace
give byte-identical files, so diffs stay local and reviewable.

**Why this priority**: These are guard properties. Breaking them would corrupt other commands or create git noise,
but they add no new capability.

**Independent Test**:
1. In a workspace with baseline files, run list, validate, index rebuild, run start, show, dashboard, analyze coverage
   and review-flagged.
2. None of them reports the baseline as a test, suite, index or parse error.
3. Run triage and the baseline writers twice on the same workspace; the outputs and files are byte-identical.

**Acceptance Scenarios**:

1. **Given** suites containing `_source-baseline.json`, **When** any command that enumerates suite folders or suite
   files runs, **Then** its results are identical to those without the file.
2. **Given** `spectra suite rename a b`, **Then** `test-cases/b/_source-baseline.json` exists with the same content.
   **Given** `spectra suite delete b`, **then** the file is gone with the suite.
3. **Given** an unchanged workspace, **When** triage runs twice (with `--no-auto-ack`, or after auto-ack has settled),
   **Then** stdout JSON and `.spectra/update-triage.json` are byte-identical across runs.
4. **Given** one test's entry changes, **Then** the git diff of the baseline file touches only that test's lines.

---

### Edge Cases

- **Anchor variants.** `#Create-customer-records`, `#create-customer-records`, `#create_customer_records` and
  `#Create%20customer%20records` all match the heading "Create customer records" (D2). Percent-escapes are decoded
  before the key is built.
- **Duplicate headings.** Two headings with the same key: the first in document order wins. Triage and the writers
  record an `ambiguous_anchor` note naming the ref.
- **Headings inside fenced code** (```` ``` ```` or `~~~` fences) are not headings, as boundaries or as anchor
  targets.
- **Empty anchor** (`file.md#`) is treated as no anchor, so the whole file is the section.
- **Last section** runs to the end of the file.
- **Nested headings.** A level-2 section includes its level-3 and deeper subsections. It ends at the next level-1 or
  level-2 heading.
- **Line endings and BOM.** A section that differs only in CRLF vs LF line endings, or a leading BOM, has the same
  fingerprint. Checkouts on different OSes never ping-pong the baseline (see Assumptions).
- **A ref added to a test by hand** with no baseline hash: the ref is reported as unbaselined for that test. The
  test's other refs are still evaluated, and the test appears in `unbaselined` as partial.
- **A ref removed from a test by hand** whose hash is still in the baseline: the stale key is ignored by triage. It
  is dropped the next time the entry is recomputed (FR-026a).
- **A test deleted** (`spectra delete`) or moved out of the suite by hand: its entry is ignored by triage. The next
  write of that suite's baseline drops it.
- **Missing snapshot** for a changed ref, for example a fresh clone of a branch whose snapshots were never committed:
  the ref is CHANGED / LOAD with `why` = `no diff: snapshot missing`. This is not an error.
- **Huge sections.** If either side of a diff is over 5,000 words, no diff is computed: CHANGED / LOAD with
  `why` = `no diff: over 5000 words`.
- **A ref whose file is outside the allowed roots**, or that never resolved: REMOVED, with the resolver's reason.
- **A criteria ID that exists twice** (a reported duplicate): the first occurrence wins, as in 079, and a note is
  recorded.
- **An unreadable or malformed `_source-baseline.json`**: every command that reads it fails with
  `BASELINE_UNREADABLE`, naming the file, and writes nothing. It is never treated as empty, because that would
  overwrite it.
- **A concurrent writer** holding `.spectra/baseline.lock` past the timeout: `LOCK_TIMEOUT`, and nothing is written.
- **`update` config section** missing, `null`, or without the new keys: the defaults apply silently. `group_size` < 1
  or `light_threshold_words` < 0, or a non-integer: `CONFIG_INVALID` naming the key.
- **A suite with no tests**: it is reported with zero counts, and no baseline file is created.
- **`--suite` naming a suite that doesn't exist**: refused with `SUITE_NOT_FOUND`.

## Requirements *(mandatory)*

### Functional Requirements

**Sections and fingerprints**

- **FR-001**: A source ref `file#anchor` identifies a section:
  - the file is resolved with the existing source-ref resolution (repo root, then `source.local_dir`, the same allowed
    roots);
  - the section starts at the first heading whose key equals the anchor's key, including the heading line;
  - it ends just before the next heading of the same or a higher level, or at the end of the file.
  A ref with no anchor, or an empty anchor, identifies the whole file.
- **FR-002**: Headings are ATX headings (`#` to `######` followed by a space) outside fenced code blocks. Fences open
  and close with ```` ``` ```` or `~~~`.
- **FR-003**: The key of an anchor or a heading's text:
  1. percent-decode it (anchors only);
  2. lowercase it with the invariant culture;
  3. keep only letters and digits.
  When several headings share the anchor's key, the first wins, and the result carries an `ambiguous_anchor` note.
- **FR-004**: A ref's **fingerprint** is `sha256:` followed by 64 lowercase hex digits. It is computed over the
  section's raw text, encoded as UTF-8, after a leading BOM is removed and line endings are normalised to LF.
- **FR-005**: A ref's **comparison text** is `TextNormalizer.Normalize` of the section's raw text.
- **FR-006**: A criterion's fingerprint is `sha256:` plus the hex digest of the normalised text of that criterion's
  `text`. The criterion is found by ID across every criteria file. If an ID appears more than once, the first
  occurrence wins and a note is recorded.

**Storage**

- **FR-007**: Each suite's baseline is the single file `{tests_dir}/{suite}/_source-baseline.json`. It is committed
  to git and never a folder. The shape is:

  ```json
  {"version": 1, "tests": {"<id>": {"refs": {"<ref as written>": "sha256:…"}, "criteria": {"<criterion id>": "sha256:…"}}}}
  ```

  - test IDs, refs and criterion IDs are sorted ordinally;
  - the file is UTF-8 without BOM, with a fixed indentation, LF line endings and a trailing newline;
  - each test's entry sits on its own lines, so a change to one test is a local diff.
- **FR-008**: Whenever a writer records a ref fingerprint, it ensures that `.spectra/source-snapshots/{hex}.txt`
  exists. `{hex}` is the fingerprint's hex digits, and the file holds the ref's comparison text. A snapshot is
  content-addressed by the fingerprint of the section it was taken from. It is committed to git, shared across
  suites, and never pruned automatically. A missing snapshot is never an error.
- **FR-009**: Every baseline and snapshot write:
  - takes the cross-process lock `.spectra/baseline.lock`, waiting up to the same timeout as the criteria lock, then
    `LOCK_TIMEOUT`;
  - writes atomically, so a crash never leaves a half-written file;
  - skips a file whose content would be byte-identical.
  Writes to a baseline file also drop entries for tests that no longer exist in that suite.
- **FR-010**: `spectra init` adds `.spectra/baseline.lock` and `.spectra/update-triage.json` to the SPECTRA section of
  `.gitignore`. Baseline files and `.spectra/source-snapshots/` are not ignored.
- **FR-011**: Every command that enumerates suite folders or suite files never treats `_source-baseline.json` as a
  test, an index, a suite or a parse error. That includes list, validate, index rebuild, run, show, dashboard,
  analyze, review-flagged, suite list, doctor, delete and the generate/update compilers and ingestors.
  `spectra suite rename` moves the file with the suite, and `spectra suite delete` removes it with the suite.

**States**

- **FR-012**: For each test with a baseline entry, every current `source_refs` entry that has a baseline fingerprint
  gets exactly one state:

  | State | Rule |
  |---|---|
  | REMOVED | The file does not resolve, or the anchor matches no heading |
  | UNCHANGED | The current fingerprint equals the baseline fingerprint |
  | COSMETIC | Fingerprints differ, the baseline's snapshot exists, and its text equals the current comparison text |
  | CHANGED / LOAD | Fingerprints differ and any LOAD feature (FR-013) holds |
  | CHANGED / LIGHT | Fingerprints differ, and the comparison texts differ with no LOAD feature |

  A current ref with no baseline fingerprint is *unbaselined*, not changed. A baseline ref the test no longer has is
  ignored.
- **FR-013**: The word diff is computed between the snapshot text and the current comparison text, split into words.
  A word is a whitespace-separated token with leading and trailing punctuation trimmed. The diff is a longest-common-
  subsequence diff of the word sequences. The LOAD features are:
  - **numbers**: a removed or added word contains a digit;
  - **modal**: a removed or added word is one of `must`, `should`, `may`, `cannot`, `required`, `only`, `not` (so
    "must not" is covered by `must` and `not`);
  - **size**: the larger of the words-removed and words-added counts exceeds `light_threshold_words`;
  - **no diff**: the snapshot is missing, or either side has more than 5,000 words.
- **FR-014**: For each criterion ID in the test's current `criteria` that has a baseline fingerprint:
  - CRITERIA_REMOVED if the ID exists in no criteria file;
  - CRITERIA_CHANGED if its fingerprint differs from the baseline;
  - otherwise unchanged.
  A current criterion ID with no baseline fingerprint is unbaselined.
- **FR-015**: A test's bucket is its worst state, in the order REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED >
  LIGHT > COSMETIC > UNCHANGED. A test with no baseline entry is UNBASELINED, which stands apart from that order and
  is never reported as changed. A test whose entry lacks some current refs or criteria is bucketed on the rest, and
  is also listed as partially unbaselined.

**Triage**

- **FR-016**: `spectra ai update-triage (--suite <s> | --all-suites) [--no-auto-ack] [--dry-run]` computes the states
  for every test in scope. Exactly one of `--suite` and `--all-suites` is required.
- **FR-017**: Grouping. Tests in the same suite, with the same bucket and the same set of changed items, form one
  group. A changed item is a ref in a non-UNCHANGED state with that state, or a criterion in a criteria state.
  - Groups are chunked at `update.group_size` (default 10), in test-ID order.
  - Groups are ordered by suite (ordinal), then bucket precedence (worst first), then the changed-item set (ordinal),
    then chunk.
  - Group IDs are `G1`, `G2`, … in that order, numbered across the whole run.
  - COSMETIC and UNCHANGED tests form no groups.
- **FR-018**: COSMETIC refs are auto-acknowledged: the baseline fingerprint is set to the current one and the snapshot
  is written. This happens unless `--no-auto-ack` or `--dry-run` is given. Only the COSMETIC refs' fingerprints
  change; every other key of the entry is kept, including stale ones (see FR-026a).
- **FR-019**: Stdout in JSON mode is one compact document:
  - per suite and bucket, the counts;
  - `groups`: `{id, bucket, suite, refs, criteria, tests, why}`. `refs` and `criteria` are the changed items with
    their states. `why` is a short list of the features found, for example `numbers: 30→45`, `modal: must→should`,
    `words: 15`, `no diff: snapshot missing`, `anchor not found`;
  - `removed`: `{suite, test, ref|criterion, reason}`;
  - `unbaselined`: counts per suite (full and partial). The IDs are only in the detail file;
  - `no_tests`: generation hints (FR-020);
  - `notes`: ambiguous anchors and duplicate criteria;
  - `auto_acked`: a count;
  - `detail_file`.
  For a workspace of 2,500 tests with about 10% changed, stdout stays under about 2K tokens (SC-004).
- **FR-020**: `no_tests` lists, for every document with at least one ref in a CHANGED state in this triage, the
  headings of that document that no triaged test references.
  - A whole-file ref covers every heading of its document.
  - A section ref covers its heading and every heading nested under it.
  - Each entry is `{doc, heading}`, ordered by document then position.
  (Planning note: the `spectra docs changed` signal was rejected. It compares against the criteria index, which the
  criteria workflow refreshes before update triage runs, so it would almost always be empty here.)
- **FR-021**: The full detail goes to `.spectra/update-triage.json`, a gitignored scratch file. It includes:
  - every test's state per ref and criterion, with current and baseline fingerprints;
  - the word diff per changed ref (removed and added runs);
  - the unbaselined IDs;
  - the group membership, with the fingerprints triage saw.
  It is not written on `--dry-run`.
- **FR-022**: The human summary table is always printed: on stderr in JSON mode, on stdout otherwise. It shows per
  suite the bucket counts, the groups with their `why`, the removed list and the unbaselined counts.
- **FR-023**: Triage output is deterministic. Given the same workspace, stdout, the detail file and any baseline
  writes are byte-identical. No timestamps or machine paths appear in any of them.

**Acknowledge and bootstrap**

- **FR-024**: `spectra ai update-ack --suite <s> (--ids a,b | --group <gid>) [--dry-run]`:
  - recomputes the named tests' entries from current content (refs and criteria, with snapshots) and writes them;
  - never writes any test file;
  - `--ids` naming a test that doesn't exist in the suite is refused (`TEST_NOT_FOUND`), and nothing is written;
  - a test with a ref in the REMOVED state can't be acknowledged: it is refused (`REF_REMOVED`, exit 4), naming the
    test and the refs, and nothing is written. A REMOVED ref is cleared by changing the test so it no longer lists
    that ref. The usual path is for the user to fix or drop the ref in the test file and then run `update-ack --ids`,
    which recomputes the entry (FR-026a). An `ingest-update` that changes the refs also clears it, and so will
    Phase 2b's `relink-sources`. If the feature itself was removed, deleting the test (`spectra delete`) takes it
    out of triage; its entry becomes stale and is pruned on the next write (FR-009). A criterion in the CRITERIA_REMOVED state is refused the same way
    (`CRITERION_REMOVED`).
- **FR-025**: `--group` resolves the group from `.spectra/update-triage.json`:
  - no detail file, or a file for another scope → `TRIAGE_MISSING`;
  - the group is not in it → `GROUP_NOT_FOUND`;
  - any member's current fingerprints differ from those triage recorded → `TRIAGE_STALE`.
  Each refusal names the problem, and nothing is written. `--group` on a group whose bucket is REMOVED or
  CRITERIA_REMOVED is refused as in FR-024 (`REF_REMOVED` / `CRITERION_REMOVED`).
- **FR-026**: `spectra ai source-baseline --init (--suite <s> | --all-suites) [--force] [--dry-run]`:
  - creates entries from current content for tests with none;
  - adds missing refs and criteria to partial entries;
  - never changes an existing fingerprint unless `--force` is given, in which case every entry in scope is
    recomputed.
  It never touches test files.
- **FR-026a**: A stale key is a ref or criterion ID that is still in a baseline entry but that the test no longer
  lists. It is dropped only when the whole entry is recomputed: by `ingest-tests`, `ingest-update`, `update-ack`, or
  `source-baseline --init --force`. Auto-ack and `source-baseline --init` without `--force` keep entries as they are,
  apart from the keys they add or refresh. Triage always ignores stale keys.
- **FR-027**: Two writers besides these:
  - `ingest-tests` records an entry for every test it persists;
  - `ingest-update` replaces the edited test's entry.
  Both record only on success, after the test is persisted, and both take the lock. A baseline failure after a
  successful persist is reported as an error with its code. The test file stays written, and git is the undo.
  Baseline failures from these two commands use the shared failure shape (`command`, `success`, `error_code`,
  `errors`, `warnings`, and `persisted` when relevant). Their existing content and schema failures (exit 5 and 6)
  keep their current `{success, error_code, errors}` shape, unchanged by this phase. Moving those to the shared shape
  is a later cleanup.
- **FR-028**: Every writer reports what it wrote, in its JSON result and human output: entries created, updated and
  unchanged, refs auto-acked, and snapshots written. On `--dry-run` it reports what it would write and writes
  nothing: no baseline, no snapshot, no detail file, and no lock file.

**Config and errors**

- **FR-029**: The `update` config section gains two optional keys: `group_size` (integer ≥ 1, default 10) and
  `light_threshold_words` (integer ≥ 0, default 12).
  - A missing or `null` `update` section or key → the defaults, silently, per the 083 config rule.
  - The init template is not required to carry them.
  - An out-of-range or non-integer value → `CONFIG_INVALID` naming the key.
- **FR-030**: `update-triage`, `update-ack` and `source-baseline` are write commands under the 083 rule:
  - no config file → `CONFIG_MISSING`;
  - a malformed config → `CONFIG_INVALID`;
  - they read `source` and `tests`, so a missing `source` or `tests` section → defaults plus one warning each.
- **FR-031**: Every failure is one JSON document on stderr in the shared shape `{command, success, error_code, errors,
  warnings}`, with exit 1 for environment errors and the refusal exit code (4, as `docs retire` / `docs apply-move`)
  for refusals.
  - Environment errors: `CONFIG_MISSING`, `CONFIG_INVALID`, `BASELINE_UNREADABLE`, `LOCK_TIMEOUT`.
  - Refusals: `SUITE_NOT_FOUND`, `TEST_NOT_FOUND`, `TRIAGE_MISSING`, `GROUP_NOT_FOUND`, `TRIAGE_STALE`,
    `REF_REMOVED`, `CRITERION_REMOVED`.
  - Usage errors (both or neither of `--suite`/`--all-suites`, both or neither of `--ids`/`--group`) are refusals
    with `INVALID_ARGUMENTS`.

**Skill and legacy**

- **FR-032**: The `spectra-update` skill is rewritten as in User Story 4:
  - triage, then the summary;
  - REMOVED and CRITERIA_REMOVED listed for review only. The skill never edits them and never runs
    `compile-update-prompt` for them, because there is no current section to reconcile against. It tells the user
    which ref or criterion to fix in the test file, and to run `update-ack --ids` afterwards. If the feature itself
    was removed, it tells the user the test can be deleted with the `spectra-delete` skill (`spectra delete <id>
    --dry-run`, then confirm). It never edits the `.md` file and never runs the delete itself (NON-STOP contract);
  - a bucket question with these options:
    - **LOAD only** (recommended): the LOAD and CRITERIA_CHANGED buckets. A changed criterion is a changed
      requirement, and its current text exists to reconcile against;
    - **LOAD + LIGHT**: those plus LIGHT;
    - **choose groups**: only groups in the LOAD, CRITERIA_CHANGED or LIGHT buckets are offered;
    - **report only**: no edits.
    REMOVED and CRITERIA_REMOVED groups are never processed by the edit loop, whichever option is chosen;
  - per selected group, the existing compile-update-prompt / edit / ingest-update loop per test;
  - `update-ack` for the tests the user reviewed and kept;
  - the counts.
  It follows the `spectra-criteria` skill's exit-code handling (report `error_code`, stop for that suite, retry once
  on `LOCK_TIMEOUT`, `CONFIG_MISSING` / `CONFIG_INVALID` named) and the NON-STOP contract. It never calls
  `spectra ai update`.
- **FR-033**: `spectra ai update` and `TestClassifier` are unchanged in behaviour. The docs label them as legacy, with
  `update-triage` as the replacement.

### Rule tables (the tests mirror these, so rules and tests can't drift)

**Anchor matching (FR-003)**, for the heading "Create customer records":

| Anchor | Key | Matches |
|---|---|---|
| `#Create-customer-records` (GitBook) | `createcustomerrecords` | yes |
| `#create-customer-records` (GitHub) | `createcustomerrecords` | yes |
| `#Create-Customer-Records` (generator style) | `createcustomerrecords` | yes |
| `#create_customer_records` | `createcustomerrecords` | yes |
| `#Create%20customer%20records` | `createcustomerrecords` | yes |
| `#create-customer-record` | `createcustomerrecord` | no → REMOVED |

**Bucket precedence (FR-015)**: REMOVED > CRITERIA_REMOVED > LOAD > CRITERIA_CHANGED > LIGHT > COSMETIC > UNCHANGED;
UNBASELINED stands apart.

### Key Entities

- **Source section**: the part of a document a ref identifies, either a heading through to its end or the whole
  file. It has raw text, a fingerprint and comparison text.
- **Baseline entry**: per test, the fingerprints of its refs and criteria at the time it was last written, edited or
  acknowledged. There is one baseline file per suite.
- **Snapshot**: the comparison text of a section, named by that section's fingerprint. It is shared across suites and
  used to compute word diffs.
- **Ref state / criterion state**: the per-item result of comparing the baseline to current content (FR-012, FR-014).
- **Bucket**: a test's worst state (FR-015).
- **Group**: tests in the same suite and bucket with the same set of changed items, chunked at `group_size`, with a
  run-scoped ID and a `why`.
- **Triage detail**: the scratch record of one triage run, used by `update-ack --group` and for diffs.

## Required Tests

| ID | Covers | Content |
|---|---|---|
| T1 | FR-001, FR-002 | Section extraction table: heading levels 1–6, a heading inside a fenced code block (both fence kinds), the last section in the file, no anchor and an empty anchor → whole file, nested subsections included, duplicate headings (first wins, note) |
| T2 | FR-003 | The anchor-matching table above, plus punctuation variants; no match → REMOVED |
| T3 | FR-004, FR-012, FR-013 | State table: one row per state and per LOAD feature: number, each modal word, the size threshold at and over the limit, a case-only change (COSMETIC), markup-only and whitespace-only (COSMETIC), CRLF-only (UNCHANGED), a missing snapshot (CHANGED / LOAD, no diff), and a 5,001-word side (LOAD, no diff) |
| T4 | FR-015, FR-017 | Bucket precedence table over every pair of states; grouping (same changed-item set → one group; different bucket → separate groups; chunking at `group_size`; ordering and `G` numbering) |
| T5 | FR-018, FR-024–FR-028 | `ingest-tests` and `ingest-update` record entries; `update-ack` (`--ids`, `--group`, stale, missing) and `source-baseline --init` (with and without `--force`) behave as specified and leave test files byte-identical; `--dry-run` writes nothing anywhere (directory snapshot before and after); auto-ack of COSMETIC refreshes only those refs |
| T6 | FR-029, FR-030 | Real template end to end: `spectra init` in a temp dir with the embedded template, ingest a test, edit its doc section, `update-triage` reports it with zero config warnings, `update-ack` clears it |
| T7 | FR-015, FR-026 | Pre-existing workspace: tests with no baseline file → all UNBASELINED, and no file other than the detail file changes; `source-baseline --init` then gives a triage with every test UNCHANGED |
| T8 | FR-011 | Guard: with `_source-baseline.json` in every suite, list, validate, index rebuild, run start, show, dashboard, analyze, review-flagged, suite list and doctor give the same results as without it; suite rename moves it, suite delete removes it |
| T9 | FR-007, FR-023 | Determinism: the same workspace gives byte-identical triage stdout, detail file and baseline files over repeated runs; a one-test change is a local diff in the baseline file |
| — | FR-031, FR-032 | Error codes in the shared failure shape; skill content (steps, recommended option, error handling, no `spectra ai update`) |
| — | SC-001 | The legacy classifier (UP_TO_DATE) and triage (LOAD) on the same number-change fixture |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A number change that keeps the vocabulary (`10GB` → `50GB`) is reported as LOAD in 100% of the T3
  cases, where today's classifier reports it UP_TO_DATE.
- **SC-002**: A test that paraphrases an unchanged section is never reported as changed (0 false positives in the
  T3/T7 cases).
- **SC-003**: `update-triage --all-suites` on a 2,500-test workspace finishes in under 30 seconds.
- **SC-004**: On that workspace, with about 10% of tests affected, the stdout JSON is at most about 2,000 tokens
  (8,000 characters).
- **SC-005**: Repeated runs on an unchanged workspace produce byte-identical output and baseline files (T9).
- **SC-006**: After `spectra init` with the real template, the whole flow (ingest → edit doc → triage → ack) runs
  with zero config warnings (T6). A pre-existing workspace needs no config edit (T7).
- **SC-007**: No existing command's results change because baseline files are present (T8). Existing suites stay
  green, except the known `ProfileFormatLoaderTests` failure.
- **SC-008**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` and `docs/usage.md` describe the
  three commands, the states, the storage, the config keys and the legacy status of `spectra ai update`.

## Assumptions

- **Fingerprint over raw text.**
  - The baseline fingerprint is computed over the raw section text, so COSMETIC (raw differs, normalised equal) is
    observable.
  - The snapshot is named by that fingerprint and holds the normalised text, which is the "snapshot's source" of the
    COSMETIC rule.
  - So a missing snapshot means cosmetic and real changes can't be told apart, which is why it counts as LOAD.
- **Line endings and BOM** are normalised before fingerprinting. Otherwise Windows and Linux checkouts of the same
  commit (with `core.autocrlf`) would disagree on every fingerprint, and auto-ack would rewrite the baseline back and
  forth. The D3 residual (a case-only change is cosmetic) still applies.
- **Only ATX headings** count. Setext headings (`===` / `---` underlines) are not section boundaries. This is a
  documented residual.
- **Words changed** is the larger of the removed and added counts, so a one-word replacement counts as 1, not 2.
- **`no_tests` uses the baseline's own evidence.** No baseline exists for unreferenced sections. So a document counts
  as changed when one of its referenced sections is CHANGED, and its unreferenced headings are listed. It is a hint,
  not a guarantee that the listed section itself changed.
- **Group IDs are run-scoped** (`G1…`) and resolved through the latest detail file, with a staleness check. So an ack
  can never swallow a change triage didn't show.
- **Partial entries are completed** by `source-baseline --init` without `--force`. Adding a missing key doesn't
  change any recorded fingerprint.
- **Stale entries** (tests deleted or moved by hand) are dropped when a writer next rewrites that suite's file. This
  keeps the file honest without a separate prune command.
- **Exit codes** follow the criteria commands: 1 for environment errors, 4 for refusals.
- **`update-triage` exits 0** whenever it completes, whatever it finds. Findings are data, not failures.

## Out of Scope

- Git topology, rename detection, `relink-sources`, building a baseline from git history (Phase 2b).
- Section text and diffs inside the update prompt; group prompts (Phase 3).
- Re-verification after edits (Phase 4); subagents (Phase 5).
- Removing `spectra ai update` / `TestClassifier`.
- Pruning snapshots.
- Setext headings, and HTML `<h1>`–`<h6>` headings, as section boundaries.
