# Feature Specification: Review Fixes for Spec 088 — Update Prompt Context

**Feature Branch**: `089-review-fixes-088`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Review fixes for spec 088 (update prompt context). No new features, no baseline or
prompt-format change. Each fix needs a regression test that fails on 6ea7520 (the 088 merge) and passes after the
fix." The full text is in the triggering request: the background, fixes F1–F6 (settled, not reopened here), the
constraints, out of scope and success criteria.

## Context

Spec 088 gave the update edit prompt the changed doc sections and linked criteria, compiled one prompt per triage
group, and taught `ingest-update --all` to report per staged file, treat an identical edit as "reviewed" and clean up
consumed staging files. Its review found six defects:

1. **Update and repair share one staging folder.** A file kept after a failed ingest is picked up by the *other*
   flow's next batch and ingested in that flow's baseline mode: a leftover critic repair ingested by the update flow
   rebuilds the baseline (marking pending doc changes as reviewed, undoing the 085 protection), and a leftover update
   edit ingested by a repair leaves its doc change pending.
2. **Extra tests in one staged file are silently dropped.** Only the first test of a staged array is used. A file
   holding a whole group's array persists one test, reports success and is deleted.
3. **"Unchanged" accepts even substantive changes.** A test returned identical to the original has its baseline
   rebuilt whatever is pending, so a model that misses a numeric or modal change makes that change vanish from triage.
4. **Criteria with nothing to compare are labelled "unchanged".** A linked criterion with no baseline value (no
   baseline entry, or the criterion missing from it) is shown to the model as unchanged.
5. **A batch with failed files exits 0.** Callers can't tell from the exit code that edits were not applied.
6. **The staging folders are not gitignored by `spectra init`.**

Facts (verified on `6ea7520`):
- `ingest-update <suite> --all` reads `.spectra/updates/<suite>/updated-<id>.json`. The update skill writes there and
  ingests with `--all` (baseline rebuilt); the generation skill's repair loop (Step 8b) writes there too and ingests
  with `--all --keep-baseline`. The flagged-test review skill uses `--test-id --from .spectra/repaired.json
  --keep-baseline` and is not affected.
- Failed staging files are kept for retry; consumed ones are deleted.
- `ingest-update` exits 0 / 1 / 5 (content, incl. `DRIFT_DETECTED`) / 6 (`SCHEMA_INVALID`); baseline refusals exit 4.
  `--all` exits 0 even when files failed.
- The batch JSON is `{command, mode:"batch", suite, written, unchanged, skipped_no_original, errors, results[{test,
  file, outcome, error_code?, errors?}], baseline, warnings}`.
- Triage's per-test classification (the same rules and `update.light_threshold_words` threshold as `update-triage`)
  can be computed for a single test.
- `spectra init`'s gitignore list has no staging-folder entry. The repository's own `.gitignore` already lists
  `.spectra/repairs/` (unused so far) but not `.spectra/updates/`.
- Exit code 8 is already used, command-namespaced, by `suite delete` (external dependencies).

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode (stderr in JSON mode);
- failure output is one machine-readable JSON document on stderr (shared shape, named codes);
- nothing is written on `--dry-run`;
- git is the undo path; SPECTRA never modifies a git repository;
- no config section is ever required (083 rule); every rule is checked against a workspace made by the real
  `spectra init` and against a pre-089 workspace with existing baselines, not only fixtures.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Update edits and critic repairs never ingest each other's leftovers (Priority: P1)

A user runs a generation with critic repairs; one repair fails and its staged file is kept. Later they run the update
workflow. The update batch ingests only the update edits, and the next repair batch ingests only repairs — each in its
own baseline mode. Pending doc changes are never marked reviewed by a leftover repair, and an update edit never skips
its baseline rebuild.

**Why this priority**: Silently marking doc changes as reviewed defeats the whole change-detection workflow; it is the
085 protection undone.

**Independent Test**: Leave a failed repair file and a failed update file in place, then run both batches back to
back: each reads only its own folder, ingests only its own files in its own mode, and leaves the other's file
untouched.

**Acceptance Scenarios**:

1. **Given** a kept repair file for TC-101 and a pending LOAD change on TC-101, **When** the update batch runs,
   **Then** TC-101's repair file is not read, TC-101's baseline is untouched, and the file is still there.
2. **Given** a kept update edit for TC-102, **When** the repair batch runs, **Then** the update edit is not read and
   is still there.
3. **Given** a valid repair file in the repair folder, **When** the repair batch runs, **Then** it is ingested with
   the baseline kept, reported per file and deleted, exactly as 088's batch rules describe.

---

### User Story 2 - A staged file with several tests fails loud (Priority: P1)

A model writes the whole group's array into one staged file. Instead of one test silently persisting and the rest
vanishing, the file fails with a code naming how many tests it held and which, nothing is written for it, and the file
is kept for a retry.

**Why this priority**: Data loss with a success report — the edits for every test but the first disappear.

**Independent Test**: Stage a 10-element group array as one file next to a valid single-test file and run the batch:
the array file fails with `MULTIPLE_TESTS` and is kept, no test from it is written, and the valid file is ingested.

**Acceptance Scenarios**:

1. **Given** a file with 10 test objects, **When** the batch runs, **Then** its outcome is `failed` with
   `MULTIPLE_TESTS`, the errors name the count (10) and the ids, no test file changes, and the staging file is kept.
2. **Given** the same content passed to the single form, **When** it runs, **Then** it exits 5 with `MULTIPLE_TESTS`
   and nothing is written.
3. **Given** a file with an empty array, **When** it is ingested, **Then** the code is `NO_TESTS` as before.

---

### User Story 3 - Returning a test unchanged doesn't hide a substantive change (Priority: P1)

The model returns a test unchanged although its doc section's number changed. Instead of the change silently leaving
triage, ingest reports the test as "returned unchanged — LOAD change still pending", leaves its baseline as it was,
and the skill asks the user whether to acknowledge it or edit again. Small wording changes (LIGHT, COSMETIC) and
genuinely unchanged tests are still recorded as reviewed.

**Why this priority**: A missed LOAD change disappearing from triage is exactly the failure the workflow exists to
prevent.

**Independent Test**: Return identical edits for a LOAD test and a LIGHT test: the LOAD test is `unchanged_pending`
and still LOAD in the next triage dry run; the LIGHT test is `unchanged` and leaves triage.

**Acceptance Scenarios**:

1. **Given** a test whose pending bucket is LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED or RELINK, **When** an
   identical edit is ingested without `--keep-baseline`, **Then** the outcome is `unchanged_pending` with the bucket and
   the triage reasons, nothing is written, its baseline entry is byte-identical, and (in a batch) its staging file is
   deleted.
2. **Given** a test whose pending bucket is UNCHANGED, COSMETIC or LIGHT, **When** an identical edit is ingested,
   **Then** the outcome is `unchanged` and its baseline is rebuilt, as in 088.
3. **Given** a test with no baseline entry, **When** an identical edit is ingested, **Then** it is recorded as today.
4. **Given** `--keep-baseline`, **When** an identical edit is ingested, **Then** behaviour is as in 088 (`unchanged`,
   nothing recorded).
5. **Given** `unchanged_pending` results with bucket LOAD or CRITERIA_CHANGED, **When** the update skill reports
   them, **Then** it lists each test with its bucket and reasons and asks the user per group to acknowledge or edit
   again; it never acknowledges on its own.
6. **Given** an `unchanged_pending` result with bucket REMOVED, CRITERIA_REMOVED or RELINK (possible only when the
   documentation changed after triage, since those groups never enter the edit loop), **When** the skill reports it,
   **Then** it says the documentation changed again since triage, offers no acknowledgement (`update-ack` would refuse:
   `REF_REMOVED` for REMOVED and for RELINK, `CRITERION_REMOVED` for CRITERIA_REMOVED), and tells the user to re-run
   `update-triage`, after which the test is handled by the relink step (RELINK) or the REMOVED review
   (REMOVED / CRITERIA_REMOVED).

---

### User Story 4 - Criteria with no baseline value are labelled as such (Priority: P2)

A test links a criterion that was never baselined. The edit prompt no longer calls it "unchanged"; it says it isn't
baselined and that the test should be compared with the current text — the same wording sections already use.

**Why this priority**: A misleading label invites the model to skip a check; the impact is smaller than P1 items
because the criterion text is still shown.

**Independent Test**: Build the prompt context for criteria in four situations (no entry; entry without the criterion;
baselined and equal; baselined and different) and check the four labels.

**Acceptance Scenarios**:

1. **Given** no baseline entry, or an entry without that criterion, **When** the prompt is compiled, **Then** the
   criterion line is marked "not baselined — compare the test with the current text".
2. **Given** a baselined criterion whose text is the same, **Then** it is unmarked (unchanged); **given** a different
   text, **Then** it is marked "changed since baseline"; **given** a removed id, **Then** "removed" as before.
3. **Given** identical inputs, **When** the prompt is compiled twice, **Then** the output is byte-identical.

---

### User Story 5 - A batch with failed files says so in its exit code (Priority: P2)

A script or skill runs the batch; some files fail. The command exits 8 — "completed, some files failed" — with the
same JSON, so the caller reads `results` and retries the failed tests. Whole-command errors keep exit 1.

**Why this priority**: Exit 0 hides failures from any caller that doesn't parse the JSON.

**Independent Test**: A batch with one failed file exits 8; with every file failing exits 8; with only written,
unchanged and `unchanged_pending` outcomes exits 0.

**Acceptance Scenarios**:

1. **Given** at least one failed file (any code), **When** the batch completes, **Then** it exits 8, the JSON shape is
   unchanged, consumed files are deleted and failed ones kept.
2. **Given** no failed file, **Then** it exits 0 (`unchanged_pending` is not a failure).
3. **Given** a config, lock or baseline error, **Then** it exits 1 as before.
4. **Given** exit 8, **When** either skill handles it, **Then** it reads `results` and retries the failed tests; it
   does not stop.

---

### User Story 6 - The staging folders are gitignored (Priority: P3)

A new workspace's `.gitignore` gets the update and repair staging folders from `spectra init`; an existing workspace
gets them from `spectra update-skills`, the command users already run after upgrading. Scratch edits are never
committed.

**Why this priority**: Hygiene; nothing breaks without it, but scratch files end up in commits.

**Independent Test**: The real `spectra init` writes both entries; `spectra update-skills` on a pre-089 workspace adds
them once.

**Acceptance Scenarios**:

1. **Given** a new workspace, **When** `spectra init` runs, **Then** `.gitignore` contains `.spectra/updates/` and
   `.spectra/repairs/`.
2. **Given** a workspace initialised before 089, **When** `spectra update-skills` runs, **Then** both entries are added
   exactly once, and a further run adds nothing. The rest of its `.gitignore` is kept as it was.
3. **Given** an existing workspace, **When** plain `spectra init` runs, **Then** it still refuses because the config
   exists (unchanged); `init --force` also applies the list, as before.

---

### Edge Cases

- **Pre-089 repair leftovers** already in `.spectra/updates/` can't be told apart from update edits; the update batch
  will read them. Documented in the CHANGELOG (with how to check and remove them), not detected.
- **A repair batch with no repair folder** reports nothing to ingest and exits 0, as an empty update batch does.
- **The single form (`--test-id --from`)** reads only the named file; it is unaffected by the folder split. It applies
  the `MULTIPLE_TESTS` check and the pending-bucket check (without `--keep-baseline`).
- **A test with a baseline entry but some refs or criteria never baselined**: its bucket comes from the baselined
  parts; an UNCHANGED/COSMETIC/LIGHT result is recorded (the unbaselined parts get entries), a pending one is not.
- **COSMETIC changes** count as reviewed: an identical edit for a COSMETIC test is `unchanged` and recorded.
- **An `unchanged_pending` file is consumed** (deleted): the edit carried nothing; the change stays in triage, which is
  where the user acts on it.
- **Every file failed**: exit 8, not 1 — the batch itself completed.
- **A baseline write failure after persisting** stays exit 1 with the existing failure shape; no staging file is
  deleted (088 rule).
- **`MULTIPLE_TESTS` with ids that include the file's own id**: still fails; the count and every id are named.
- **Two objects with the same id** in one file: `MULTIPLE_TESTS` (the count is of objects, not distinct ids).

## Requirements *(mandatory)*

### Functional Requirements

**F1 — one staging folder per flow**

- **FR-001**: `ingest-update <suite> --all` MUST read staged edits only from `.spectra/updates/<suite>/updated-<id>.json`.
- **FR-002**: `ingest-update <suite> --all --keep-baseline` MUST read staged repairs only from
  `.spectra/repairs/<suite>/repaired-<id>.json`.
- **FR-003**: Neither form MUST ever read, ingest, delete or otherwise touch a file in the other flow's folder.
- **FR-004**: The repair batch MUST apply the same per-file outcomes, reporting, deletion of consumed files and
  keeping of failed files as the update batch (088 FR-016), with the file name's `<id>` taken after `repaired-`.
- **FR-005**: The generation skill's repair loop MUST write repairs to `.spectra/repairs/<suite>/repaired-<id>.json`;
  its instructions and its CLI flags table MUST name that path. No skill writes repairs to the update folder.

**F2 — one test per staged file**

- **FR-006**: A staged file (batch) and the content of the single form MUST hold exactly one test object. More than
  one MUST fail with the new code `MULTIPLE_TESTS`, naming the number of objects and their ids; nothing is written for
  that content.
- **FR-007**: In the single form `MULTIPLE_TESTS` MUST exit 5 (content invalid) with the existing failure shape; in a
  batch it MUST be a per-file `failed` outcome, the file kept, other files unaffected.
- **FR-008**: Zero objects MUST stay `NO_TESTS`.

**F3 — pending changes survive an unchanged edit**

- **FR-009**: Without `--keep-baseline`, before recording an edit judged unchanged for a test that has a baseline
  entry, ingest MUST classify the test exactly as triage would (same rules, same `update.light_threshold_words`).
- **FR-010**: If that classification is UNCHANGED, COSMETIC or LIGHT, the outcome MUST be `unchanged` and the baseline
  rebuilt (088 behaviour).
- **FR-011**: If it is LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED or RELINK, the outcome MUST be
  `unchanged_pending`: nothing written, the test's baseline entry unchanged, the result carrying `bucket` and the
  triage reasons (`why`). In a batch the staging file is consumed (deleted).
- **FR-012**: A test without a baseline entry, and every case with `--keep-baseline`, MUST behave as in 088.
- **FR-013**: The single form MUST report `unchanged_pending` in its JSON (with `bucket` and `why`) and exit 0; its
  human output MUST say the change is still pending and how to resolve it.
- **FR-014**: The batch JSON MUST add an `unchanged_pending` count and the outcome (with `bucket` and `why`) in
  `results`, keeping every existing field. The per-test human summary (stdout; stderr in JSON mode) MUST print e.g.
  `TC-104: returned unchanged — LOAD change still pending (acknowledge with update-ack or edit again)`.
- **FR-015**: The update skill MUST, after each batch, list the `unchanged_pending` tests with bucket and reasons.
  - For LOAD and CRITERIA_CHANGED it MUST ask the user per group to acknowledge (`spectra ai update-ack --suite <s>
    --ids …`) or edit again. Editing again is `spectra ai compile-update-prompt --suite <s> --test-id <id>`, an
    in-session edit, a Write to `.spectra/updates/<s>/updated-<id>.json`, and `spectra ai ingest-update <s> --all`; it
    counts toward the same at most two retries per test as a failure. It MUST NOT acknowledge without the user's
    answer.
  - For REMOVED, CRITERIA_REMOVED and RELINK (edit groups never contain them, so the documentation changed after
    triage) it MUST say so, MUST NOT offer an acknowledgement (`update-ack` refuses with `REF_REMOVED` for REMOVED and
    RELINK, `CRITERION_REMOVED` for CRITERIA_REMOVED), and MUST tell the user to re-run `update-triage`; the test is
    then handled by the relink step or the REMOVED review.

**F4 — criteria without a baseline value**

- **FR-016**: A linked criterion that exists but has no baseline value to compare with (no baseline entry, or the
  entry lacks that criterion) MUST get its own state, rendered as `not baselined — compare the test with the current
  text`, in the same line form as the other criterion states.
- **FR-017**: Baselined and equal stays unchanged (unmarked); baselined and different stays "changed since baseline";
  a missing id stays "removed". Identical inputs still give byte-identical prompts.

**F5 — exit code for a batch with failures**

- **FR-018**: `ingest-update --all` (either folder) MUST exit 8 when at least one staged file failed — any code,
  including `NO_ORIGINAL`, `UNREADABLE`, `ID_MISMATCH`, `MULTIPLE_TESTS`, the ingest codes and `PERSIST_FAILED` — even
  when every file failed. The JSON and the deletion/keeping rules are unchanged.
- **FR-019**: It MUST exit 0 when no file failed (`unchanged_pending` is not a failure), and 1 for whole-command
  errors (config, lock, `BASELINE_UNREADABLE`, `BASELINE_WRITE_FAILED`), as today.
- **FR-020**: The update skill and the generation skill's repair loop MUST treat exit 8 as "read `results`, retry the
  failed tests", never as a stop. The NON-STOP contract and the generation skill's `written == 0` guard (around
  `ingest-grounding`) MUST stay as they are. In the repair loop, re-staging and re-ingesting a repair file after an
  ingest failure completes the same repair attempt: it does not increase the count passed to `ingest-grounding
  --repaired --repair-attempts N`, and it does not trigger another critic pass on its own.
- **FR-021**: The command reference MUST document exit 8 next to 5 and 6.

**F6 — gitignore**

- **FR-022**: `spectra init` MUST add `.spectra/updates/` and `.spectra/repairs/` to its idempotent gitignore list.
  The repository's own `.gitignore` MUST list both.
- **FR-022a**: `spectra update-skills` MUST apply the same idempotent gitignore list (one shared list, so the two can't
  drift), adding only missing entries and reporting `.gitignore` as updated when it added any. Plain `spectra init` on
  an existing workspace keeps refusing (decision 2026-10-08: existing workspaces are upgraded by `update-skills`).

**Cross-cutting**

- **FR-023**: No baseline-file, test-file or prompt-structure format changes beyond the new criterion label (FR-016).
- **FR-024**: No config section becomes required; the 083 rule and its guard tests stay green.
- **FR-025**: Every fix MUST be verified on a workspace made by the real `spectra init` and on a pre-089 workspace
  with existing baselines.
- **FR-026**: `docs/cli-reference.md`, `docs/usage.md`, `CHANGELOG.md` (including the pre-089 leftover edge case) and
  the CLAUDE.md "Recent Changes" entry MUST be updated.

### Required Tests

Each must fail on `6ea7520` and pass after the fix.

| Test | Covers | Fails on 6ea7520 because |
|---|---|---|
| T1 folder split | FR-001–FR-004 | the repair batch reads `.spectra/updates/`; a kept repair file is ingested (and rebuilds the baseline) by the update batch |
| T2 multiple tests | FR-006–FR-008 | the first test is persisted and the file deleted |
| T3 pending bucket (one row per bucket class; LOAD stays LOAD in the next triage dry run, LIGHT becomes UNCHANGED) | FR-009–FR-014 | the baseline is rebuilt for every unchanged edit |
| T4 criterion labels (no entry; entry without the criterion; equal; different) | FR-016–FR-017 | an unbaselined criterion is labelled unchanged |
| T5 exit 8 | FR-018–FR-019 | `--all` exits 0 |
| T6 gitignore (real init; `update-skills` on a pre-089 workspace adds once, keeps the rest) | FR-022, FR-022a | the entries are missing; `update-skills` never touches `.gitignore` |
| T7 skills | FR-005, FR-015, FR-020 | the repair loop writes to the update folder; no `unchanged_pending` handling; no exit-8 handling |

Deliberately changed assertions (listed with reasons in tasks.md): the 088 tests that expect exit 0 from a batch with
a failed file, the 088 tests that expect `unchanged` (baseline rebuilt) for an identical edit of a test whose change is
LOAD, and the 088 builder test that expects a never-baselined criterion to be unchanged.

### Key Entities

- **Staging folder**: per flow and suite — update edits (`.spectra/updates/<suite>/updated-<id>.json`) and critic
  repairs (`.spectra/repairs/<suite>/repaired-<id>.json`).
- **Staged outcome**: per file — `written`, `unchanged`, `unchanged_pending` (with bucket and reasons) or `failed`
  (with a code, incl. the new `MULTIPLE_TESTS`).
- **Linked criterion state**: unchanged, changed since baseline, not baselined (new), removed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a workspace with leftover files in both staging folders, running the update batch and the repair
  batch back to back ingests 100% of each flow's own files in its own mode and 0 files of the other flow.
- **SC-002**: A staged file with N > 1 tests results in 0 tests written and the file kept, for N = 2 and N = 10.
- **SC-003**: For every bucket that requires an edit (LOAD, CRITERIA_CHANGED, REMOVED, CRITERIA_REMOVED, RELINK), a
  test returned unchanged is still in that bucket in the next triage; for UNCHANGED, COSMETIC and LIGHT it leaves
  triage.
- **SC-004**: No never-baselined criterion is ever labelled unchanged in an edit prompt.
- **SC-005**: Any batch with at least one failed file exits non-zero (8); any batch without one exits 0.
- **SC-006**: All six fixes have a regression test that fails on `6ea7520`. Test totals: Core ≥ 1219, CLI ≥ 1870 with
  only the known `ProfileFormatLoaderTests` failure, Execution 228, Integration 8; zero build warnings.

## Assumptions

- The repair folder name and file prefix are `.spectra/repairs/<suite>/` and `repaired-<id>.json`, as given; the
  repository's existing `.spectra/repairs/` gitignore entry already matches.
- The classification used for F3 is triage's, computed against the baseline loaded inside the same lock as the ingest,
  with COSMETIC not auto-acknowledged (a COSMETIC test still counts as reviewable, so it is recorded).
- The flagged-test review skill (`--test-id --from .spectra/repaired.json --keep-baseline`) stays as it is; its
  single-form ingest gains only the `MULTIPLE_TESTS` check.
- Exit 8 for `ingest-update` is command-namespaced, like exit 8 for `suite delete`; no shared exit-code constant
  changes meaning.
- Making the deleted `unchanged_pending` file recoverable isn't needed: it equalled the original test.

## Out of Scope

- The six findings deferred from the 085 review; the 078 parse cost; `ProfileFormatLoaderTests`; `spectra show` with a
  bare filename.
- Phase 4: re-running the critic after edits, `ingest-grounding` replace, the delete-handler pattern, deleting
  orphans.
- Detecting or migrating pre-089 repair leftovers in `.spectra/updates/` (documented only).
- `ingest-update --all` has no `--dry-run` (constitution IV); pre-existing since 077, noted in 088; deferred to Phase 4
  together with the other `--all` exit/staging items.
- Any new feature, flag, config key or prompt layout change beyond the criterion label.
