# Feature Specification: Re-verify After Update (Phase 4)

**Feature Branch**: `090-reverify-after-update`
**Created**: 2026-10-08
**Status**: Draft
**Input**: User description: "Phase 4 of the doc-change → criteria → test workflow. After an update edit the test is
re-verified by the critic, and stale verdicts can never be written onto it. Failed staging files stop leaking into later
runs. `spectra delete` becomes safe for the update workflow. It also takes four 089-review findings (B1, D1–D3) and the
`--dry-run` gap (089 analyze D1)." The full text is in the triggering request: the background, FR-A1–A5, FR-B1–B2,
FR-C1–C4, FR-D1–D3, the constraints, out of scope and success criteria.

## Context

Phases 2–3 (specs 084–089) tell the user which tests a doc change affects and let the model edit them group by group.
What happens after an edit is still weak:

1. **An edited test keeps a verdict given to different content.** The update flow never runs the critic, and an edit
   keeps the original's `grounding` block, so a test can say "grounded" for text the critic never saw.
2. **Verdicts aren't tied to what they judged.** A verdict file written for one version of a test can be ingested onto
   a later version.
3. **A Manual verdict can be overwritten** by `ingest-grounding --test`.
4. **Failed staging files leak** (089-review finding 1): a failed `updated-<id>.json` / `repaired-<id>.json` stays under
   the same name and is read again by every later batch.
5. **`ingest-update` has no `--dry-run`** (089 analyze D1, constitution IV).
6. **`spectra delete` is unsafe for the update workflow**: its `depends_on` cleanup rewrites list lines anywhere in a
   file (body included), collapses blank lines across the whole file (breaking 078 lossless writes) and reads ids with a
   regex; it leaves the deleted test's baseline entry, staging files and verdict file behind; legacy `ai update` deletes
   with a bare file delete.
7. **`update-skills`' gitignore handling** (089-review findings 2–4) re-adds patterns a user removed, creates a
   `.gitignore` where none was wanted, counts a pattern as present when it only appears inside another line, and trims
   the file's trailing bytes.

Facts (verified on `fc82b7a`):
- `UpdatedTestIngestor.ApplyEdit` keeps the original's grounding (critic or Manual) on a written edit.
- The critic flow is `compile-critic-prompt --suite S --test ID` → the `spectra-critic` subagent writes
  `.spectra/verdicts/critic-verdict-<id>.json` → `ingest-verdict --suite S --all` → `ingest-grounding --suite S --all
  [--repaired --repair-attempts N]`. `ingest-verdict --from <file>` (single) takes no suite or test id.
- `ingest-grounding --all` skips a test that already has any grounding block; `--test` overwrites without a Manual
  check. Hallucinated verdicts are refused by the write-back (the generation flow drops and deletes those tests).
- `TestCase.DependsOn` is a single value. A `depends_on` written as a block list or a flow list fails to parse
  (`INVALID_YAML`), so no SPECTRA command can read such a test today.
- The triage group resolver already reports `TRIAGE_STALE` for a group naming a deleted test.

Rules that still apply: human summaries are never suppressed in JSON mode (stderr); failures are one machine-readable
JSON document on stderr in the shared shape with named codes; nothing is written on `--dry-run`; git is the undo path;
SPECTRA never modifies a git repository; no config section is ever required (083); every rule is checked against a
workspace made by the real `spectra init` and against a pre-090 workspace with baselines, verdict files and grounding
blocks.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - An edited test is re-verified, and a stale verdict never lands on it (Priority: P1)

A user updates tests after a doc change. Each test the model edited loses its old critic verdict at the moment it is
written, the critic re-checks it, and its new verdict is what the test shows. A verdict produced for an older version
of the test is refused instead of being written onto the new one. A human's Manual verdict is never replaced.

**Why this priority**: A "grounded" mark that was earned by different content is a false quality signal — worse than
none. This is the core of Phase 4.

**Independent Test**: Edit a grounded test through `ingest-update`; its grounding block and verdict file are gone and
`audit-grounding` shows it unverified. A verdict compiled for the old content is then refused as stale; a fresh critic
pass grounds it.

**Acceptance Scenarios**:

1. **Given** a test with a critic grounding block and a verdict file, **When** an edit is written (single or `--all`,
   with or without `--keep-baseline`), **Then** the block is removed in the same write, the verdict file (and its
   fingerprint record) is deleted, and `audit-grounding` reports the test as unverified.
2. **Given** a test with a Manual verdict, **When** an edit is written, **Then** the Manual block is kept byte-for-byte
   and the result lists the test under `manual_verdict_kept`.
3. **Given** an edit reported `unchanged` or `unchanged_pending`, **Then** the grounding block and verdict file are
   untouched.
4. **Given** a verdict whose recorded fingerprint differs from the test's current content, **When** `ingest-verdict` or
   `ingest-grounding` runs, **Then** it is refused as `VERDICT_STALE` (exit 4 in single mode; `skipped_stale` with the
   ids in `--all`), and nothing is written.
5. **Given** a verdict without a fingerprint record (created before 090), **Then** it is accepted as today.
6. **Given** a test with a Manual verdict, **When** `ingest-grounding` runs for it, **Then** it is refused as
   `MANUAL_VERDICT` (single) or counted in `skipped_manual` (`--all`), and the Manual block is unchanged.
7. **Given** the update skill after a group's ingest, **When** tests were `written`, **Then** it re-verifies them
   (critic per test → `ingest-verdict --all` → `ingest-grounding --all`), repairs a partial or hallucinated result once,
   never drops or deletes a hallucinated test, asks the user what to do with one that stays hallucinated, and reports
   grounded / partial-flagged / hallucinated / unverified counts with ids.

---

### User Story 2 - A failed staging file is never read again by a later batch (Priority: P1)

A staged edit fails. It is kept for inspection under a name the batch never reads, so the next `--all` doesn't fail on
it again (or ingest it in a later, unrelated run). Retrying means writing a fresh staged file.

**Why this priority**: A leaked failure turns every later batch into exit 8 and can ingest an old edit into a later
run; it is the 089 review's first finding.

**Independent Test**: A failing `updated-TC-101.json` becomes `failed-TC-101.json`; the next `--all` doesn't report it;
a fresh `updated-TC-101.json` that succeeds removes the `failed-` file.

**Acceptance Scenarios**:

1. **Given** a staged file that fails, **When** `--all` runs, **Then** it is renamed to `failed-<id>.json` in the same
   folder (replacing an older one), and `results` gives that path.
2. **Given** a `failed-<id>.json`, **When** `--all` runs again, **Then** it is not read or reported.
3. **Given** a `failed-<id>.json` and a fresh staged file for the same id, **When** that file is written, unchanged or
   unchanged_pending, **Then** `failed-<id>.json` is deleted.
4. **Given** either folder (updates or repairs), **Then** the same rules apply. Exit 8 keeps its 089 meaning.

---

### User Story 3 - `ingest-update` can be previewed (Priority: P2)

A user or script runs `ingest-update --dry-run` (single or `--all`) and sees exactly what a real run would do — each
file's outcome and the exit code — with nothing written, recorded, deleted or renamed.

**Why this priority**: Constitution IV requires `--dry-run` on batch operations; it was deferred from 089.

**Independent Test**: A staging folder with one file of each outcome; `--dry-run` reports the same outcomes and exit
code as the following real run, and the workspace is byte-identical after the dry run.

**Acceptance Scenarios**:

1. **Given** staged files that would be written, unchanged, unchanged_pending and failed (incl. `MULTIPLE_TESTS`,
   `ID_MISMATCH`, `DRIFT_DETECTED`), **When** `--all --dry-run` runs, **Then** it reports those outcomes and exit codes
   exactly as the real run does, and no file, baseline, staging file or verdict changes.
2. **Given** the single form, **When** `--dry-run` runs, **Then** it reports the outcome it would have and writes
   nothing.

---

### User Story 4 - Deleting a test leaves nothing broken or stale behind (Priority: P1)

A user deletes a test (directly, or through the delete skill the update flow points to). Tests that depended on it have
only their `depends_on` value changed — nothing else in their files moves. The deleted test's baseline entry, staging
files and verdict file go with it.

**Why this priority**: Today a delete can rewrite unrelated lines in other tests' bodies and leaves stale state that the
update workflow then trips over.

**Independent Test**: Delete TC-101 where TC-102 has `depends_on: TC-101` and TC-103 has a body line `- TC-101`; only
TC-102's `depends_on` changes (the key is dropped), TC-103 is byte-identical, and TC-101's baseline entry, staging and
verdict files are gone.

**Acceptance Scenarios**:

1. **Given** a dependent with scalar `depends_on: TC-101`, **When** TC-101 is deleted, **Then** the `depends_on` key is
   dropped through the 078 writer and every other byte (body, other keys, comments, blank lines, newline style) is
   unchanged.
2. **Given** a test whose body or `criteria:` contains `- TC-101`, **Then** it is not touched.
3. **Given** a dependent whose `depends_on` is a block or flow list (such tests don't parse today), **Then** the file
   is left byte-identical and the delete reports a warning naming the file and the deleted id.
4. **Given** a deleted test, **Then** its baseline entry (under the baseline lock), its `updated-`/`repaired-`/`failed-`
   staging files and its verdict file and fingerprint record are removed; snapshots are not touched.
5. **Given** `--dry-run`, **Then** the plan lists the files, dependents, list-form warnings, baseline entries, staging
   and verdict files, and nothing changes.
6. **Given** legacy `ai update` deleting an orphaned test, **Then** it goes through the same delete service.

---

### User Story 5 - Upgrading never overrides the user's `.gitignore` choices (Priority: P2)

A user who removed a SPECTRA pattern from `.gitignore`, or never had a `.gitignore`, runs `spectra update-skills`. Only
patterns never offered before are added, a missing `.gitignore` is not created, and the file's existing bytes are kept.

**Why this priority**: 089 made `update-skills` touch `.gitignore`; three review findings show it can override the
user and rewrite their file.

**Independent Test**: A `.gitignore` where the user removed `.spectra/progress.json` and keeps LF line endings without a
final newline; `update-skills` adds only never-offered patterns, after one LF, without touching the rest; a second run
adds nothing.

**Acceptance Scenarios**:

1. **Given** a pattern offered before and later removed by the user, **When** `update-skills` runs, **Then** it is not
   added again.
2. **Given** no `.gitignore`, **Then** none is created and the output says `no .gitignore; add: …` with the patterns.
3. **Given** a line equal to a pattern, or to the pattern without its trailing `/`, **Then** the pattern counts as
   present; a comment, a negation, or a longer line containing the pattern does not.
4. **Given** a `.gitignore` with CRLF line endings and no final newline, **When** a pattern is added, **Then** the
   existing bytes are unchanged, one CRLF is added first, and the new lines use CRLF.
5. **Given** a workspace from before 090 (no offered record), **Then** every pattern of the 089 list except
   `.spectra/updates/` and `.spectra/repairs/` counts as already offered.
6. **Given** a new workspace, **When** `spectra init` runs, **Then** it writes the full list (as today) and records it.

---

### Edge Cases

- **A test edited by hand** (not through `ingest-update`) after its verdict was compiled: its content no longer matches
  the recorded fingerprint, so the verdict is refused as stale — the critic must run again.
- **Writing a grounding block** must not make the verdict it came from look stale: the fingerprint covers what the
  critic judged, not the grounding block.
- **`compile-critic-prompt` for a whole suite** (no `--test`) records a fingerprint per test it emits.
- **`ingest-verdict --from` stdin or a file outside `.spectra/verdicts/`**: there is no fingerprint record to find, so
  the verdict is accepted as today (documented).
- **A Manual-verdict test that is edited**: kept, listed under `manual_verdict_kept`; the update skill doesn't send it
  to the critic and lists it in the summary as "manual verdict kept — review by hand".
- **Hallucinated after the update edit**: the update flow never drops or deletes it; after one repair attempt it stops
  for that test and asks the user (edit again, restore the previous version from git — the user runs that — or delete
  through the delete skill).
- **The user declines re-verification**: the edited tests are listed as unverified in the summary; their grounding
  blocks are already removed, so `audit-grounding` shows them unverified.
- **Repairs in the generation flow** (`--keep-baseline`): a partial test has no grounding block before its repair (8a
  writes only grounded ones), so FR-A1 removes only its verdict file; the re-critic writes a new one and the existing
  `written == 0` guard, `--repaired` and `--repair-attempts` behave as before.
- **A failed file with the same id twice in one batch** can't happen (one file per id per folder); an older
  `failed-<id>.json` is replaced.
- **`--dry-run` with a baseline that can't be read** fails the same way the real run would (exit 1), and writes nothing.
- **`update-skills` with no `.gitignore`**: patterns are only recorded as offered when they are appended or found
  present, so a `.gitignore` created later still gets them on the next run.
- **A negation of a pattern** (`!.spectra/updates/`) is the user's choice: the pattern is not added (appending it after
  the negation would override the user) and is recorded as offered.
- **Deleting a test that has a Manual verdict**: deleted as any test (git is the undo).

## Requirements *(mandatory)*

### Functional Requirements

**A — Re-verification after update edits**

- **FR-A1**: When `ingest-update` writes an edit (single or `--all`, with or without `--keep-baseline`), it MUST remove
  the test's critic grounding block in the same write and delete the test's verdict file and fingerprint record. A
  Manual verdict MUST be kept byte-for-byte and the result MUST list the test under `manual_verdict_kept`. `unchanged`
  and `unchanged_pending` MUST NOT touch grounding or verdict files. `audit-grounding` then reports the test as
  unverified (FR-A7).
- **FR-A2**: `compile-critic-prompt` MUST record, per test it emits, a fingerprint of the test content the critic judges
  (not including its grounding block). `ingest-verdict` and `ingest-grounding` (single and `--all`) MUST refuse a
  verdict whose recorded fingerprint differs from the test's current content: `VERDICT_STALE` (exit 4 in single mode;
  in `--all`, a `skipped_stale` count and the list of ids). A verdict with no fingerprint record MUST be accepted as
  today.
- **FR-A3**: `ingest-grounding` MUST NOT overwrite a Manual verdict: `MANUAL_VERDICT` refusal (exit 4) in single mode,
  `skipped_manual` (count + ids) in `--all`. No replace option is added.
- **FR-A4**: The update skill MUST, after each group's ingest and for the tests reported `written`, offer once per run
  (default yes) to re-verify them now. When re-verifying:
  - for each such test (except `manual_verdict_kept` ones): `compile-critic-prompt --suite <s> --test <id>` and the
    `spectra-critic` subagent; then `ingest-verdict --suite <s> --all` and `ingest-grounding --suite <s> --all`;
  - grounded → done;
  - partial → one repair attempt as in the generation flow (stage in `.spectra/repairs/`, `ingest-update <s> --all
    --keep-baseline`, re-critic, `ingest-grounding --suite <s> --all --repaired --repair-attempts 1`); still partial →
    flagged for review (existing behaviour);
  - hallucinated → never dropped or deleted by this flow; one repair attempt; still hallucinated → stop for that test
    and ask the user: edit again, restore the previous version from git (the user runs it), or delete through the
    delete skill;
  - declined → the tests are listed as unverified;
  - the final summary MUST report grounded / partial-flagged / hallucinated / unverified (and manual-kept) counts with
    ids. The skill's NON-STOP contract and its rule never to run delete itself stay.
- **FR-A5**: The update prompt template and context (088/089) MUST NOT change.
- **FR-A6**: `compile-repair-prompt` MUST accept partial and hallucinated verdicts and refuse grounded ones (the existing
  refusal shape). The generation flow is unaffected: it still record-drops hallucinated tests before any repair.
- **FR-A7**: `audit-grounding` MUST also list every indexed test that has no verdict file, using the test file's
  grounding block as the source of truth (`.spectra/verdicts/` is gitignored, so a fresh clone has blocks but no verdict
  files):
  - a critic grounding block → that block's verdict, `grounding_written: true`, `action_needed` as for that verdict today;
  - a Manual verdict → `verdict: "manual"`, `grounding_written: true`, `action_needed: "none"`;
  - no block → `verdict: "none"`, `grounding_written: false`, `action_needed: "critic"`.
  The JSON gains an `unverified` count (tests with verdict `"none"`). Entries for tests that have a verdict file are
  unchanged (same fields and values as on `fc82b7a`), and the exit code is unchanged.

**B — Staging**

- **FR-B1**: A staged file that fails in `ingest-update --all` (either folder) MUST be renamed to `failed-<id>.json` in
  the same folder, replacing an older one; `--all` MUST never read `failed-*`; `results` MUST give the kept path. A
  later `written`, `unchanged` or `unchanged_pending` outcome for that id MUST delete its `failed-<id>.json`. Exit 8
  keeps its 089 meaning. The skills retry by writing a fresh `updated-<id>.json` / `repaired-<id>.json`.
- **FR-B2**: `ingest-update` (single and `--all`) MUST accept `--dry-run`: it reports the outcome each file would get —
  including `MULTIPLE_TESTS`, `ID_MISMATCH`, `DRIFT_DETECTED`, `unchanged` / `unchanged_pending` from the same pending
  check — with the same JSON shape and exit code as a real run, and writes, records, deletes and renames nothing. It
  takes no lock beyond what reading needs.

**C — Safe delete**

- **FR-C1**: Delete's `depends_on` cleanup MUST change only the `depends_on` frontmatter value of dependent tests. For a
  scalar `depends_on` naming a deleted test it MUST drop the key through the 078 lossless writer, so the body, other
  keys, comments, blank lines and newline style stay byte-identical. The dependent's id and the id lookup MUST come from
  the parsed frontmatter, never from a regex over the file. Body lines and `criteria:` entries MUST never be touched.
  A dependent whose `depends_on` is a block or flow list (unparseable today) MUST be left byte-identical and reported as
  a warning naming the file and the deleted id (decision 2026-10-08: scalar only).
- **FR-C2**: Deleting tests MUST also remove their entries from `{tests_dir}/{suite}/_source-baseline.json` (under the
  baseline lock), their `updated-`, `repaired-` and `failed-` staging files in both folders, and their verdict file and
  fingerprint record. Snapshots MUST be left alone. `--dry-run` MUST list all of these.
- **FR-C3**: Legacy `ai update` MUST delete orphaned tests through the same delete service (index, `depends_on`,
  baseline, staging, verdicts), never with a bare file delete. The legacy classifier is otherwise unchanged.
- **FR-C4**: The update skill MUST keep delegating deletes to the delete skill; its "never run delete from this skill"
  rule stays.

**D — Gitignore**

- **FR-D1**: `update-skills` MUST add only gitignore patterns it has never offered to this workspace, recording the
  offered set in the workspace's skills manifest, so a pattern the user removed stays removed. It MUST NOT create a
  `.gitignore`; with none it MUST report `no .gitignore; add: …` and record nothing. It MUST name the patterns it added.
  A workspace with no offered record (before 090) MUST treat every pattern of the 089 list except `.spectra/updates/`
  and `.spectra/repairs/` as already offered. `spectra init` MUST still write the full list and record it as offered.
- **FR-D2**: The presence check MUST compare whole trimmed lines and ignore comment lines; a line equal to the pattern,
  or to the pattern without its trailing `/`, counts as present. A negation of the pattern means the user opted out:
  the pattern is not added and is recorded as offered.
- **FR-D3**: Appending MUST keep the existing bytes (no trimming), add one newline first only when the file doesn't end
  with one, and use the file's own newline style (LF or CRLF, as the 078 writer detects it).

**Cross-cutting**

- **FR-E1**: New refusal codes (`VERDICT_STALE`, `MANUAL_VERDICT`) MUST use the shared failure JSON shape.
- **FR-E2**: Generation-flow behaviour MUST be unchanged except where FR-A1–A3 and FR-A6 deliberately apply; its
  NON-STOP contract and `written == 0` guard text stay verbatim.
- **FR-E3**: No config section becomes required; the 083 guard tests stay green.
- **FR-E4**: Every rule MUST be verified on a workspace made by the real `spectra init` and on a pre-090 workspace with
  baselines, verdict files and grounding blocks.
- **FR-E5**: `docs/cli-reference.md`, `docs/usage.md`, `CHANGELOG.md` and a short CLAUDE.md "Recent Changes" entry MUST
  be updated.

### Required Tests

| Test | Covers | Must fail on `fc82b7a`? |
|---|---|---|
| T1 edit drops critic grounding + verdict, keeps Manual (single, `--all`, `--keep-baseline`; unchanged / pending untouched) | FR-A1 | — |
| T2 stale verdict refused (`ingest-verdict`, `ingest-grounding`, single + `--all`); pre-090 verdict accepted; grounding write doesn't make it stale | FR-A2 | — |
| T3 Manual never overwritten (single refusal, `--all` skip) | FR-A3 | — |
| T4 generation repair loop still works (partial → repair → re-critic → `--repaired`; `written == 0` guard text) | FR-A1, FR-E2 | — |
| T5 update skill re-verify step and summary; template unchanged | FR-A4, FR-A5, FR-C4 | — |
| T6 failed file renamed, never re-read, cleared by a later success; both folders | FR-B1 | **yes** (089 finding 1) |
| T7 `--dry-run` outcomes and exit code equal the real run; workspace byte-identical | FR-B2 | — |
| T8 delete: scalar dependent dropped losslessly; body / `criteria:` untouched; list form warned and untouched; ids from frontmatter | FR-C1 | — |
| T9 delete removes baseline entry, staging, verdict + record; `--dry-run` lists them | FR-C2 | — |
| T10 legacy `ai update` orphan delete via the service | FR-C3 | — |
| T11 `update-skills` offered set; no `.gitignore` created; pre-090 record default | FR-D1 | **yes** (089 finding 2) |
| T12 whole-line presence check (incl. trailing `/`, comments, negations, substrings) | FR-D2 | **yes** (089 finding 3) |
| T13 append keeps bytes, newline style, single separator | FR-D3 | **yes** (089 finding 4) |
| T14 `audit-grounding` lists tests without a verdict file (none / block's verdict / manual; `unverified`; unchanged entries) | FR-A7 | — |
| T15 `compile-repair-prompt` accepts hallucinated, refuses grounded | FR-A6 | — |

### Key Entities

- **Verdict fingerprint record**: per test, the fingerprint of the content the critic was given, written when the critic
  prompt is compiled and stored next to the verdict file.
- **Staging file states**: `updated-<id>.json` / `repaired-<id>.json` (to ingest), `failed-<id>.json` (kept for
  inspection, never read by a batch).
- **Offered gitignore set**: the patterns `update-skills` / `init` have offered to this workspace, kept in its skills
  manifest.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After an update edit is written, 0 tests show a critic verdict that was given to their previous content;
  every edited test is either re-verified in the same run or listed as unverified.
- **SC-002**: 0 stale verdicts (fingerprint mismatch) are written onto a test; 0 Manual verdicts are overwritten.
- **SC-003**: A failed staging file is reported by exactly one batch; later batches report it 0 times.
- **SC-004**: A `--dry-run` of `ingest-update` matches the real run's outcomes and exit code in 100% of the test rows,
  and changes 0 bytes in the workspace.
- **SC-005**: Deleting a test changes 0 bytes outside the `depends_on` values of its scalar dependents, the index and
  the baseline; it leaves 0 baseline entries, staging files or verdict files for the deleted test.
- **SC-006**: `update-skills` re-adds 0 patterns a user removed, creates 0 `.gitignore` files, and changes 0 existing
  bytes of a `.gitignore`.
- **SC-007**: Each 089-review fix (B1, D1–D3) has a test that fails on `fc82b7a`. Test totals: Core ≥ 1219, CLI ≥ 1928
  with only the known `ProfileFormatLoaderTests` failure, Execution 228, Integration 8; zero build warnings.

## Assumptions

- The fingerprint record is written by `compile-critic-prompt` (a small scratch file next to the verdict files, under
  `.spectra/verdicts/`, already gitignored); the plan chooses its exact name and content.
- Single-mode `ingest-verdict --from <file>` finds the record from the verdict file's name; for stdin or another path
  there is no record and the verdict is accepted as today.
- "Removes the critic grounding block" means the whole `grounding:` frontmatter entry; a Manual verdict is any block
  whose verdict is `manual`.
- `--dry-run` for `ingest-update` follows the existing pattern of other commands (same JSON plus `dry_run: true`).
- Snapshots are content-addressed and may be shared, so delete never removes them; an unused snapshot is harmless.
- Legacy `ai update` keeps its classifier and flags; only its delete path changes.

## Out of Scope

- 089-review findings 5–6 (Phase 6); the six 085-review findings; the 078 parse cost; `ProfileFormatLoaderTests`;
  `spectra show` with a bare filename; critic section-only input; update/criteria subagents (Phase 5).
- Making block/flow `depends_on` lists parseable (decision 2026-10-08: delete handles scalar only and warns on lists).
- `ingest-verdict --all` and `ingest-grounding --all` have no `--dry-run` (constitution IV); pre-existing; 090 closes it
  only for `ingest-update` (089 analyze D1). Deferred to Phase 5 with the other batch commands.
- `spectra validate` reporting success while printing parse errors (found while verifying FR-C1; separate finding).
