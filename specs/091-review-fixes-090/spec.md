# Feature Specification: Review Fixes for Spec 090 — Re-verification After Update Edits

**Feature Branch**: `091-review-fixes-090`
**Created**: 2026-10-09
**Status**: Draft
**Input**: User description: "Review fixes for spec 090 (re-verification after update edits). No new features. Each
fix needs a regression test that fails on 5c10c44 (the 090 merge) and passes after the fix." (F1–F4 as quoted in the
request.)

## Context

Spec 090 tied each critic verdict to the test content it judged (a fingerprint record written when the critic prompt
is compiled), dropped a test's critic verdict when an update edit is written, and added a re-verify step to the update
flow with one repair attempt. Its review found four defects:

1. **An old verdict can pass as fresh after a failed critic run.** When a test changes outside `ingest-update` (a hand
   edit, `relink-sources`, a restore from git), its old verdict file stays. If a later critic run compiles the prompt
   (recording the new fingerprint) but writes no verdict, the old verdict is paired with a record that matches the
   current content and is written as the test's grounding.
2. **`--all --repaired` marks unrepaired tests as repaired.** After a repair, both skills write grounding with
   `--all --repaired`, which covers every test in the suite with a fresh verdict and no block — including a test whose
   repair failed to ingest and partial verdicts left over from an interrupted run.
3. **Kept hallucinated tests count as errors.** A still-hallucinated test the update flow keeps for the user makes every
   later `ingest-grounding --all` on the suite report `errors`, without ids, until the user resolves it.
4. **The critic agent's text is out of date.** It says compiling the critic prompt "writes nothing".

Facts (verified on `5c10c44`):
- `compile-critic-prompt --suite S --test ID` writes `.spectra/verdicts/critic-input-<id>.json` = `{id, suite, file,
  fingerprint}`; the critic subagent then writes `.spectra/verdicts/critic-verdict-<id>.json`.
- The freshness check compares only the recorded fingerprint with the test's current content; the verdict file is
  never compared with the record. A verdict carries no timestamp (`verdict`, `score`, `findings`), so a critic re-run on
  unchanged content can produce a byte-identical file.
- `ingest-grounding --suite S --all` walks the suite's verdict files and, per test: skips a missing test file silently;
  lists a Manual verdict under `manual`; skips a test that has a block; lists a stale verdict under `stale`; without
  `--repaired` skips a partial verdict; otherwise writes. A refused write — including every hallucinated verdict — and
  an unreadable verdict file count as `errors`.
- The generation skill (Step 8b) re-critiques each repaired test whose repair ingest reported `written` or `unchanged`,
  record-drops and deletes a still-hallucinated one, then runs `ingest-grounding --all --repaired --repair-attempts 1`
  once. The update skill (Step 5a) does the same per test, but keeps a still-hallucinated test and asks the user.
- The flagged-test review skill writes grounding per test (`--test`) and is not affected by F2.

Rules that still apply:
- human summaries are never suppressed in JSON mode (stderr);
- failure output is one JSON document in the shared shape, with named codes;
- git is the undo path; SPECTRA never modifies a git repository;
- no config section is ever required (083 rule); every rule is checked against a workspace made by the real
  `spectra init` and against a pre-091 workspace with 090 records, verdict files and grounding blocks;
- the generation skill's NON-STOP contract and its written==0 guard text stay verbatim (075 regression gate).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An old verdict never becomes a test's grounding after a failed critic run (Priority: P1)

A QA engineer edits a test by hand, then runs the critic on it. The critic subagent times out and writes nothing.
The next grounding batch must not take the verdict from before the hand edit as the test's grounding.

**Why this priority**: It silently records "grounded" for content the critic never saw — the exact failure 090 set out
to prevent.

**Independent Test**: Write a verdict, change the test by hand, compile the critic prompt again and write no new
verdict: all four ingest commands treat the old verdict as stale. Then write a new verdict byte-identical to the old one
after the compile: it is accepted.

**Acceptance Scenarios**:

1. **Given** a test with a verdict file written before a hand edit, **When** the critic prompt is compiled again and no
   new verdict is written, **Then** `ingest-verdict --from <verdict file>` and `ingest-grounding --test` refuse it with
   `VERDICT_STALE` (exit 4, a message saying the verdict is older than the last critic prompt for that test), and
   `ingest-verdict --all` and `ingest-grounding --all` list it under `stale` and write nothing for it.
2. **Given** the same test, **When** a new verdict is written after the compile — even byte-identical to the old one —
   **Then** it is fresh and `ingest-grounding` writes it.
3. **Given** a workspace from before 091 whose fingerprint record has no compile time, **When** any of the four
   commands runs, **Then** the 090 rule applies unchanged: fingerprint match → fresh, mismatch → stale.
4. **Given** a verdict with no fingerprint record (from before 090), **When** any command runs, **Then** it is
   accepted, as today.

---

### User Story 2 - Only tests that were actually re-critiqued are marked repaired (Priority: P1)

After a repair attempt, the skill writes grounding only for the tests it re-critiqued. A test whose repair failed to
ingest, and partial verdicts from an interrupted run, are never stamped `repaired`.

**Why this priority**: A `repaired: true` block on an unrepaired test hides a partial verdict from review and misreports
the repair outcome.

**Independent Test**: TC-130's repair fails to ingest, TC-131's lands and is re-critiqued; both have fresh partial
verdicts. A repaired grounding batch limited to TC-131 writes only TC-131's block.

**Acceptance Scenarios**:

1. **Given** TC-130 (repair failed, old partial verdict still fresh) and TC-131 (repaired and re-critiqued), **When**
   `ingest-grounding --suite S --all --repaired --repair-attempts 1 --ids TC-131` runs, **Then** only TC-131 gets a
   block and TC-130 has none.
2. **Given** any suite, **When** `ingest-grounding --all --repaired` runs without `--ids`, **Then** it refuses with
   `IDS_REQUIRED` (exit 4, shared failure shape) and writes nothing.
3. **Given** `--ids TC-131,TC-999` where TC-999 is not in the suite, **When** the batch runs, **Then** TC-999 is listed
   under `not_found` and TC-131 is processed normally.
4. **Given** `--all` without `--repaired`, **When** it runs with or without `--ids`, **Then** it behaves as today,
   limited to the listed ids when `--ids` is given.
5. **Given** the generation and update skills, **When** they write grounding after a repair, **Then** they pass `--ids`
   with exactly the tests re-critiqued after the repair attempt.
6. **Given** the update skill and a repair whose ingest still reports `failed` after the bounded retries, **When** it
   handles that test, **Then** it reports "repair failed: <error_code>", runs no critic and writes no repaired
   grounding, and lists the test as unverified (it was partial) or waiting for the user (it was hallucinated).

---

### User Story 3 - A kept hallucinated test is reported by id, not as an error (Priority: P2)

After the update flow keeps a still-hallucinated test for the user to decide, later grounding batches on that suite —
including generation runs — report the test by id as waiting for review, and report `errors: 0`.

**Why this priority**: Unexplained errors on every later batch make real failures invisible; the impact is reporting,
not data.

**Independent Test**: Keep TC-140 with a hallucinated verdict and run `ingest-grounding --all`: `errors: 0`,
`skipped_hallucinated: 1` naming TC-140. An unreadable verdict file in the same batch still counts as an error.

**Acceptance Scenarios**:

1. **Given** TC-140 exists with a fresh hallucinated verdict and no block, **When** `ingest-grounding --all` runs,
   **Then** the result has `errors: 0`, `skipped_hallucinated: 1`, `hallucinated: ["TC-140"]`, and the human summary
   names it ("TC-140: hallucinated, waiting for review").
2. **Given** a verdict file that can't be read or parsed, or a write that fails, **When** the batch runs, **Then** it
   still counts as `errors`.
3. **Given** `ingest-grounding --test TC-140`, **When** it runs, **Then** it refuses the hallucinated verdict as today.
4. **Given** a test dropped by the generation flow (its file deleted), **When** the batch runs, **Then** it is skipped
   as today and not listed as hallucinated.

---

### User Story 4 - The critic agent describes what compiling its prompt does (Priority: P3)

The critic agent's instructions say that compiling the critic prompt writes only the fingerprint record (with its
compile time), never the verdict, and calls no model.

**Why this priority**: Documentation accuracy; no behaviour depends on it, but a wrong statement invites the agent to
misread a changed `.spectra/verdicts/` folder.

**Independent Test**: A text guard on the bundled critic agent fails if it says compiling the prompt writes nothing.

**Acceptance Scenarios**:

1. **Given** the bundled critic agent text, **When** it is read, **Then** step 1 says the compile writes only the
   fingerprint record, never the verdict, and calls no model.

---

### Edge Cases

- A verdict file whose last-write time is within 2 s of the record's compile time (before or after), equal to it, or
  later counts as fresh.
- `ingest-verdict` with a verdict from standard input or from a file outside `.spectra/verdicts/` has no record to
  compare with and is accepted, as today. `ingest-grounding --test <id> [--from <file>]` is checked by test id, against
  the record of `<id>` and the last-write time of the file actually read.
- A test with a Manual verdict, or one that already has a block, is reported as today even when listed in `--ids`.
- `--ids` containing a dropped test (deleted from the suite) reports it under `not_found`; this is informational, not a
  failure.
- `--ids` with duplicates or surrounding spaces is treated as the set of trimmed ids; an empty `--ids` value counts as
  missing (so `--repaired` refuses with `IDS_REQUIRED`).
- `--test` together with `--all`, and `--ids` without `--all`, are invalid combinations: exit 1, a plain stderr message
  in the style of "Either --test <id> or --all must be specified.", nothing written. (Today `--all` wins when both
  `--test` and `--all` are given; no skill, doc or test combines them.)
- A hallucinated verdict that is also stale is reported as stale (staleness is checked first); a hallucinated verdict
  for a test that already has a block is reported as already written, as today.
- A record written by 090 is never rewritten by an ingest command; it gains a compile time only when the critic prompt
  is compiled again.
- Compiling the whole suite (`compile-critic-prompt --suite S` without `--test`) records `compiled_at` for every test;
  verdicts written before it are stale and need a new critic run (including partial verdicts waiting for repair).

## Requirements *(mandatory)*

### Functional Requirements

**F1 — verdicts older than the last critic prompt are stale**

- **FR-001**: The fingerprint record written when the critic prompt is compiled MUST also store when it was written
  (`compiled_at`, UTC). Compiling still deletes and renames nothing and never writes a verdict.
- **FR-002**: A verdict file whose last-write time is earlier than its record's `compiled_at` by more than 2 seconds
  (the file-system timestamp allowance) MUST be treated as stale
  (`VERDICT_STALE`), with a message saying the verdict is older than the last critic prompt for that test. The
  fingerprint rule from 090 still applies on top: a verdict is fresh only if it is not older than the record by more
  than 2 s and the recorded fingerprint matches the current test.
- **FR-003**: FR-002 MUST apply in `ingest-verdict` (single `--from` and `--suite --all`) and `ingest-grounding`
  (`--test` and `--suite --all`), with the same outcomes as a 090 stale verdict (exit 4 in single mode; listed under
  `stale` / counted in `skipped_stale` in batch mode; nothing written for it).
- **FR-004**: A record without `compiled_at` (written by 090) MUST keep 090's rule (fingerprint only). A verdict with no
  record MUST be accepted, as today.
- **FR-005**: The rule MUST compare times, not file contents, so a new verdict written after the compile is fresh even
  when byte-identical to an older one.

**F2 — repaired grounding only for re-critiqued tests**

- **FR-006**: `ingest-grounding --all` MUST accept `--ids a,b` (comma-separated). Only the listed tests are
  considered; listed ids that are not in the suite's index are reported in a `not_found` list (JSON and human summary)
  and do not fail the batch.
- **FR-006a**: `ingest-grounding` MUST reject `--test` together with `--all`, and `--ids` without `--all`, as invalid
  option combinations: exit 1, a plain stderr message, nothing written. This is a deliberate change: `--all` used to
  win when both `--test` and `--all` were given (no skill, doc or test combines them).
- **FR-007**: `ingest-grounding --all --repaired` without `--ids` MUST refuse with `IDS_REQUIRED` (exit 4, shared
  failure shape) and write nothing.
- **FR-008**: Without `--repaired`, `--ids` is optional and `--all` keeps its current behaviour, apart from FR-006a.
- **FR-009**: The generation skill (Step 8b) and the update skill (Step 5a) MUST pass `--ids` with exactly the tests
  re-critiqued after a repair attempt (those whose repair ingest reported `written` or `unchanged`; in the generation
  flow, excluding tests dropped in the re-critic phase). The generation skill's NON-STOP contract and written==0 guard
  text stay verbatim. The flagged-test review skill (single `--test`) is unchanged. In the update skill, a repair whose
  ingest still reports `failed` after the bounded retries gets no critic run and no repaired grounding: the skill
  reports "repair failed: <error_code>" and lists the test as unverified (partial) or waiting for the user
  (hallucinated, with the same choices as a test that stays hallucinated).

**F3 — kept hallucinated tests**

- **FR-010**: In `ingest-grounding --all`, a hallucinated verdict for a test whose file exists MUST be counted as
  `skipped_hallucinated`, with its ids listed under `hallucinated`, not as `errors`. The human summary names each one,
  e.g. "TC-140: hallucinated, waiting for review".
- **FR-011**: Unreadable or unparseable verdict files and failed writes MUST still count as `errors`. Single `--test`
  keeps refusing a hallucinated verdict. A test whose file is gone is skipped as today.
- **FR-012**: The generation skill MUST NOT treat `skipped_hallucinated` as a failure.

**F4 — critic agent text**

- **FR-013**: The critic agent's step 1 MUST say that compiling the critic prompt writes only the fingerprint record
  (with `compiled_at`), never the verdict, and calls no model.

**Cross-cutting**

- **FR-014**: No config section becomes required; the 083 guard tests stay green.
- **FR-015**: Every fix MUST be verified on a workspace made by the real `spectra init` and on a pre-091 workspace with
  090 records, verdict files and grounding blocks.
- **FR-016**: `docs/cli-reference.md` (`--ids`, `IDS_REQUIRED`, `skipped_hallucinated`/`hallucinated`, `not_found`, the
  `compiled_at` rule), `docs/usage.md`, `CHANGELOG.md` and a short CLAUDE.md "Recent Changes" entry MUST be updated.
- **FR-017**: Existing batch JSON fields keep their names and meanings; new fields are additions.

### Required Tests

Each fix (F1–F4) has at least one test that fails on `5c10c44` and passes after the fix; rows marked *guard* must pass
both before and after. Tests set file times explicitly; none sleeps.

| Test | Covers | Fails on 5c10c44 because |
|---|---|---|
| T1 old verdict after a failed critic run, in all four commands | FR-001–FR-003 | the old verdict matches the newly recorded fingerprint and is written |
| T2 byte-identical verdict written after the compile | FR-005 | guard: passes on `5c10c44` and must stay green (the fix must not reject it) |
| T3 090-format record without `compiled_at` | FR-004 | guard: passes on `5c10c44` and must stay green (fingerprint rule only) |
| T4 repair failed for TC-130, landed for TC-131; `--ids TC-131` | FR-006 | `--ids` doesn't exist; TC-130 is stamped repaired |
| T5 `--all --repaired` without `--ids` | FR-007 | it writes every fresh verdict |
| T6 unknown id | FR-006 | `--ids` / `not_found` don't exist |
| T6a invalid combinations (`--test` with `--all`; `--ids` without `--all`) | FR-006a | `--test` with `--all` ran the batch on 5c10c44 (deliberate change); the `--ids` half is proven by T013's exact-message assertion, since on 5c10c44 `--ids` is an unknown option and already exits 1 |
| T7 kept hallucinated TC-140; a real failure still counts | FR-010, FR-011 | TC-140 counts as `errors` |
| T8 skill and agent text guards | FR-009, FR-012, FR-013 | the skills run `--all --repaired` without `--ids`, the update skill re-critiques a failed repair; the agent says "writes nothing" |

### Key Entities

- **Fingerprint record**: per test — id, suite, test file, content fingerprint, and (new) the UTC time the critic
  prompt was compiled.
- **Verdict file**: per test — the critic's verdict; its last-write time is compared with the record's compile time.
- **Grounding batch result**: adds `not_found`, `skipped_hallucinated` and `hallucinated` to the existing counts and
  lists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After a hand edit and a critic run that writes no verdict, 0 of the 4 ingest commands write or accept the
  old verdict.
- **SC-002**: A verdict written after the compile is accepted in 100% of cases, including when byte-identical to an
  older verdict.
- **SC-003**: After a repair round in which some repairs fail, 0 tests outside the re-critiqued set get a
  `repaired: true` block.
- **SC-004**: A suite with kept hallucinated tests and no real failures reports `errors: 0` and names every kept test.
- **SC-005**: Every 091 fix has a regression test that fails on `5c10c44`. Test totals: Core ≥ 1219, CLI ≥ 2001 with
  only the known `ProfileFormatLoaderTests` failure, Execution 228, Integration 8; zero new build warnings.

## Assumptions

- File times on the workspace's file system are reliable enough to order "prompt compiled" before "verdict written";
  the plan may allow for the file system's timestamp resolution, as long as a verdict from before the hand edit (which
  precedes the compile by far more) is still stale.
- The record's `compiled_at` is the time the record was written, in UTC.
- `not_found` is informational: the batch exit code is unchanged by it.
- `hallucinated` is the list name for the new ids, next to the existing `manual` and `stale` lists.
- Test ids in `--ids` are matched the same way as index lookups today (case-insensitive).

## Out of Scope

- 090-review findings 4–6 (audit-grounding doesn't show staleness; the gitignore offer record is per clone; delete
  parses every test file) — Phase 6.
- `--dry-run` for `ingest-verdict` / `ingest-grounding --all`.
- The `depends_on` list-form mismatch; the earlier deferred items (085 review findings, 078 parse cost,
  `ProfileFormatLoaderTests`, `spectra show` with a bare filename).
- Any new feature beyond `--ids`, `not_found`, `skipped_hallucinated`/`hallucinated` and `compiled_at`.
