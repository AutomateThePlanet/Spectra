# Feature Specification: Update Prompt Context (Phase 3)

**Feature Branch**: `088-update-prompt-context`
**Created**: 2026-10-07
**Status**: Draft
**Input**: User description: "Phase 3 of the doc-change → criteria → test update workflow: give the edit prompt what it
needs." The full text is in the triggering request: the facts, decisions D1–D9 (settled, not reopened here), the skill,
tests T1–T9, rules, out of scope and acceptance.

## Context

Phases 2a/2b (specs 084–087) tell the user which tests a doc change affects, and why. The step that actually edits a
test is still the pre-084 one:

1. **The edit prompt contains no doc text.** The model edits a test without seeing the section that changed.
2. **The criteria block can be every criterion in the repository**, when nothing narrower matches.
3. **One prompt per test.** Ten tests that share one changed section produce ten prompts, each carrying the same
   material.
4. **Staged edits are never cleaned up.** A later batch ingest re-applies leftovers from an earlier update or repair.

This phase makes the update prompt carry exactly what the edit needs — the test's doc sections as they are now, the
previous version, what changed, and only the criteria the test links — once per group of tests that share a change.

Facts (verified on `eb1151c`):
- `compile-update-prompt --suite --test-id` passes no source text, and fills the criteria block from a loader whose last
  resort is every criterion in the repository. It refuses (exit 4) only when no criteria exist anywhere.
- The `test-update` template has the placeholders `test_case`, `current_source`, `acceptance_criteria`,
  `profile_format`.
- `ingest-update` persists one test; `--all` ingests every `.spectra/updates/{suite}/updated-*.json` and never deletes
  them.
- Baselines hold one fingerprint per ref and per linked criterion. Snapshots hold each baselined section's
  **normalised** text (lowercased, markup removed). Criterion text is not snapshotted.
- The triage detail file holds the groups (id, bucket, refs, tests) and, per test, the refs' baseline and current
  fingerprints and the changed-word runs (added/removed words only).
- `update-ack --group` already validates a group against the detail file (`TRIAGE_MISSING`, `GROUP_NOT_FOUND`,
  `TRIAGE_STALE`).

Rules that still apply:
- human-readable tables and summaries are never suppressed in JSON mode;
- failure output is one machine-readable JSON document on stderr (shared shape, named codes);
- nothing is written on `--dry-run`;
- git is the undo path;
- the CLI is model-free and deterministic.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The edit prompt shows the doc change the test must follow (Priority: P1)

A user runs the update workflow on a test whose referenced section changed (for example, a quota went from 30 to 45).
The prompt the assistant edits from shows that section as it is now, the version the test was written against, and what
changed between them, plus only the acceptance criteria the test links. The assistant edits exactly what the change
implicates.

**Why this priority**: Without the doc text the model guesses; the edit is only as good as what it is shown.

**Independent Test**: For a test with one changed ref and one unchanged ref, the compiled prompt contains the changed
section's current text, its previous (normalised) text and the change summary, the unchanged section marked unchanged,
and exactly the test's linked criteria — no other criteria.

**Acceptance Scenarios**:

1. **Given** a ref whose section changed since the baseline, **Then** the prompt shows its current section, its
   previous version (labelled normalised), and what changed (the changed words and the triage reason, e.g.
   `numbers: 30→45`).
2. **Given** a ref that is unchanged, **Then** the prompt shows its current section, marked unchanged.
3. **Given** a ref with no baseline entry, or whose snapshot is missing, **Then** the prompt shows the current section
   and says the previous version is unavailable.
4. **Given** a ref that doesn't resolve now (REMOVED, RELINK, submodule not checked out), **Then** the prompt lists it
   with that reason and no text.
5. **Given** an anchor-less ref, **Then** the whole file is its section.
6. **Given** linked criteria, **Then** each appears with its current text, marked "changed since baseline" when its
   fingerprint changed, or "removed" when the ID no longer exists; **given** none linked, **Then** no criteria appear —
   never a suite-wide or repository-wide list.
7. **Given** a test with nothing to reconcile against (no resolvable ref and no linked existing criterion), **Then**
   compilation is refused (exit 4) with that reason.

---

### User Story 2 - One prompt per group of tests that share a change (Priority: P1)

Triage groups tests that share the same changes. The assistant compiles one prompt per group: each shared section and
criterion once, the output format once, then every test of the group. It edits them together.

**Why this priority**: Groups are the unit the user reviews; repeating the same section ten times wastes context and
invites inconsistent edits.

**Independent Test**: A group of tests sharing one changed section compiles to one prompt in which that section,
its previous version and its change appear once, each linked criterion once, and each test once under its own heading.

**Acceptance Scenarios**:

1. **Given** `--group G2` from the last triage, **Then** one prompt holds each distinct section once, the union of the
   tests' linked criteria once, the output format once, and each test once under `### TEST <id>`, in a deterministic
   order.
2. **Given** a group that is missing, unknown, or changed since the triage, **Then** compilation is refused with the
   same codes as `update-ack --group`.
3. **Given** `--test-id`, **Then** the same layout is used with one test.
4. **Given** a prompt larger than the configured prompt budget, **Then** compilation is refused (`PROMPT_TOO_LARGE`)
   naming the size and suggesting `--test-id` or a smaller group size.
5. **Given** a custom template without the new placeholders, **Then** `--test-id` still works with a warning to reset
   the template, and `--group` is refused with the reset hint.

---

### User Story 3 - Edited tests are ingested in one call, and "no change needed" counts as reviewed (Priority: P1)

The assistant writes one staged file per edited test and ingests them all at once. Tests it returned unchanged are not
rewritten; they are recorded as reviewed, so they leave triage. Staged files that were ingested are removed, so a later
batch never re-applies them; a file that failed stays and is reported.

**Why this priority**: Without cleanup, leftovers from an earlier update or repair are silently re-applied; without the
"unchanged" path, a correct test returned as-is either churns its file or stays in triage for ever.

**Independent Test**: Three staged files — one edited, one identical to the original, one invalid — ingested in one
call: one test written, one reported unchanged with its baseline refreshed and its file untouched, one failure listed
with its staging file kept; a second batch ingests nothing.

**Acceptance Scenarios**:

1. **Given** staged edits, **When** the batch ingest runs, **Then** each valid edit is persisted and its staging file
   deleted.
2. **Given** an edit identical to the original in every editable field, **Then** the test file is not written, the
   result reports it `unchanged`, its baseline entry is refreshed (as `update-ack` would), and its staging file is
   deleted.
3. **Given** an invalid staged file, **Then** it is reported as failed with the reason and kept.
4. **Given** the batch was run, **When** it runs again, **Then** it ingests nothing.
5. **Given** `--keep-baseline` (critic repairs), **Then** baselines are never rebuilt, as in 085, including for
   unchanged edits.

---

### User Story 4 - The update skill edits group by group (Priority: P2)

The `spectra-update` skill compiles one prompt per selected group, edits in-session, stages one file per test, ingests
them in one call, and reports each test as updated, unchanged (acknowledged) or failed. Failed tests get at most two
retries with `--test-id`.

**Why this priority**: It is the user-facing flow that uses the three stories above; without it they are unused.

**Independent Test**: The skill content describes the per-group loop, the staging path, the batch ingest, the
unchanged→acknowledged report, and the bounded per-test retry, under the NON-STOP contract.

**Acceptance Scenarios**:

1. **Given** selected groups, **Then** the skill runs `compile-update-prompt --group` per group, stages one file per
   test, and runs `ingest-update {suite} --all` once per group.
2. **Given** a test that failed, **Then** the skill retries it at most twice with `--test-id`, then reports it and
   continues.
3. **Given** RELINK, REMOVED or submodule groups, **Then** their handling is unchanged (they never enter the edit loop).

---

### Edge Cases

- **A test listed in a group refs a section another test of the group also refs**: the section appears once.
- **Two refs of one test resolve to the same section** (same file and anchor written differently): the section appears
  once per distinct ref as written (refs are what the test lists), in ref order.
- **A changed section whose snapshot is missing**: current text and "previous version unavailable"; the change summary
  says no diff is available.
- **A criterion linked by the test that exists but was never baselined**: current text, not marked changed.
- **A group in which one test has nothing to reconcile**: the group is refused naming that test, so the others can be
  compiled with `--test-id` (see Assumptions).
- **A staged file for a test that no longer exists**: reported as failed (`no original`), kept.
- **A staged file whose id doesn't match its file name**: failed, kept.
- **A custom template that has `tests` but not `source_diff`** (or the reverse): treated as missing the new
  placeholders.

## Requirements *(mandatory)*

### Functional Requirements

**Doc content (D1)**

- **FR-001**: For each ref of each test in the prompt, in the test's ref order, the prompt shows the "current section":
  the section's raw text as it is now (the 085 matcher; the whole file for an anchor-less ref).
- **FR-002**: For a ref with a baseline fingerprint whose snapshot exists, the prompt shows the "previous version" — the
  snapshot text, labelled as normalised (lowercased, markup removed) so the model compares meaning, not formatting.
  Otherwise the prompt says the previous version is unavailable.
- **FR-003**: For a ref that changed since its baseline, the prompt shows "what changed": the changed-word runs and the
  triage reasons (e.g. `numbers: 30→45`, `modal: must→should`), computed the same way triage computes them. An
  unchanged ref is marked unchanged and shows its current section only.
- **FR-004**: A ref that doesn't resolve now is listed with its triage reason (REMOVED with its `not found; …` or
  `anchor not found` reason, RELINK with its target, submodule not checked out) and no text.

**Criteria (D2)**

- **FR-005**: The prompt lists only the criteria the test links, in ID order, each with its current text, marked
  "changed since baseline" when its fingerprint differs from the baseline, or "removed" when the ID no longer exists.
  No suite-wide or repository-wide fallback is ever used by the update prompt.

**Refusal (D3)**

- **FR-006**: Compilation is refused (exit 4, `NOTHING_TO_RECONCILE`) only when a test has no resolvable ref and no
  linked criterion that exists, with that reason. Otherwise a prompt is produced.

**Group prompt (D4, D8)**

- **FR-007**: `compile-update-prompt --suite <s> --group <gid>` reads the group from the last triage, with the same
  validation and error codes as `update-ack --group` (`TRIAGE_MISSING`, `GROUP_NOT_FOUND`, `TRIAGE_STALE`). Exactly one
  of `--test-id` and `--group` is required.
- **FR-008**: The prompt holds each distinct section (current, previous, what changed) once per distinct ref and
  previous version — tests baselined at different times get one block each — the union of the tests' linked criteria
  once, the output format once, and then each test once under `### TEST <id>`. Each test's block names the refs and
  criteria it links.
- **FR-009**: `--test-id` produces the same layout with one test.
- **FR-010**: Output is deterministic: identical inputs give byte-identical prompts — sections in order of first
  appearance across tests sorted by ID and their refs in order, criteria by ID, tests by ID.

**Template (D5)**

- **FR-011**: The built-in `test-update` template gains the `source_diff` and `tests` placeholders and the rules: edit
  only what the change implicates; if nothing in a test is implicated, return it unchanged; output one JSON array with
  one object per test, ids unchanged.
- **FR-012**: A custom template without both new placeholders still works for `--test-id` (filled as today) with a
  warning to run `spectra prompts reset test-update`; `--group` is refused (`TEMPLATE_OUTDATED`) with that hint.

**Size (D7)**

- **FR-013**: The compiled prompt's estimated size is checked with the existing token estimator and budget
  (`ai.analysis.max_prompt_tokens`, default 96,000). Over the limit → exit 4 `PROMPT_TOO_LARGE`, naming the estimated and allowed sizes and suggesting
  `--test-id` or a smaller `update.group_size`. No new config.

**Ingest (D6)**

- **FR-014**: `ingest-update {suite} --all` deletes each staging file after its test is persisted. A file that fails
  stays, and the result lists it with the reason. (`ingest-update` has no `--dry-run`; none is added.)
- **FR-015**: An edit identical to the original in every editable field is not written. It is reported `unchanged`, its
  staging file is deleted, and its baseline entry is rebuilt from current content (as `update-ack` does) — unless
  `--keep-baseline` is given, which never rebuilds (085). For this comparison only, each field's text has CRLF / CR
  normalised to LF and trailing whitespace trimmed per line and per field; whitespace inside a line still counts. The
  persisted content is never altered by it (analysis U1).
- **FR-016**: The batch result reports per test `written`, `unchanged` or `failed` (with the staging file and reason),
  and counts for each; the human summary is printed in every mode.
- **FR-017**: The single-test form (`--test-id … --from <file>`) reports an identical edit the same way (`unchanged`,
  baseline rebuilt unless `--keep-baseline`).

**Skill**

- **FR-018**: The `spectra-update` skill's edit loop is per group: compile with `--group`, edit in-session, write one
  staged file per test with the Write tool, run `ingest-update {suite} --all`, and report updated / unchanged
  (acknowledged) / failed per test. A failed test is retried at most twice with `--test-id`, then reported, and the loop
  continues. RELINK, REMOVED and submodule handling are unchanged. The NON-STOP contract applies.

**Compatibility**

- **FR-019**: The critic is unchanged (D9). Baseline, snapshot, triage detail and test-file formats don't change.
- **FR-020**: The 083/084 init-template and pre-existing-workspace guard tests and the 084–087 suites stay green.

### Required Tests

| ID | Covers | Content |
|---|---|---|
| T1 | FR-001–FR-005 | One test: changed ref (current + previous + what changed), unchanged ref (current only, marked), unbaselined ref (current, "previous unavailable"), missing snapshot, REMOVED / RELINK / submodule refs (reason, no text), whole-file ref; criteria unchanged / changed / removed / none linked; no suite- or repo-wide criteria ever appear |
| T2 | FR-006 | Nothing to reconcile → exit 4 with the reason; anything resolvable → a prompt |
| T3 | FR-007–FR-009 | Group: shared sections and criteria appear once (counted), every test once under `### TEST <id>`, deterministic order; stale or unknown group → the `update-ack` codes |
| T4 | FR-011, FR-012 | Built-in template has the placeholders; a custom template without them → `--test-id` works with a warning, `--group` refused with the reset hint |
| T5 | FR-014–FR-017 | `--all` with three staged files (edited, unchanged, invalid): one written, one unchanged with its baseline rebuilt and no file write, one failed and kept; the written and unchanged staging files deleted; a second `--all` ingests nothing; `--keep-baseline` never rebuilds |
| T6 | FR-013 | A group over the budget → `PROMPT_TOO_LARGE`; the same tests with `--test-id` compile |
| T7 | FR-010 | The same workspace → byte-identical prompts for `--test-id` and `--group` |
| T8 | end to end | The real init template: init, ingest a test, change its section, triage, compile `--group`, stage an edit, `ingest-update --all` → next triage shows the test UNCHANGED |
| T9 | FR-018 | Skill content: the per-group loop, staging, `--all`, the unchanged→acknowledged report, the bounded retry |

### Key Entities

- **Section block**: one ref's current section, previous version (or "unavailable"), what changed (or "unchanged"), or
  its unresolved reason.
- **Criterion block**: a linked criterion's ID, current text and state (unchanged, changed since baseline, removed).
- **Test block**: one test, its refs and linked criteria by name, under `### TEST <id>`.
- **Staged edit**: `.spectra/updates/{suite}/updated-{id}.json`, one per test, consumed by the batch ingest.
- **Ingest outcome**: written, unchanged (reviewed, baseline refreshed) or failed (kept, with the reason).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a realistic group of 10 tests sharing one changed section, the rendered variable part of the group
  prompt (the `source_diff`, criteria and tests blocks together) is at most 1.3 × the sum of its raw parts: each
  distinct section's current text, previous version and what-changed, each linked criterion's text, and each test's
  JSON. The template instructions and the output schema are excluded — the criterion measures repetition only
  (analysis A1).
- **SC-002**: 100% of update prompts contain the current text of every resolvable ref of their tests; 0 contain a
  criterion the tests don't link.
- **SC-003**: After a batch ingest, 0 staging files of written or unchanged tests remain; a second batch writes nothing.
- **SC-004**: A test returned unchanged leaves triage without its file changing (0 bytes differ).
- **SC-005**: The same workspace produces byte-identical prompts on repeated runs.
- **SC-006**: In the end-to-end check, a test edited through the group flow shows UNCHANGED at the next triage.
- **SC-007**: Existing suites stay green except the known `ProfileFormatLoaderTests` failure.
- **SC-008**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` and `docs/usage.md` describe the
  group prompt, its content, the refusals and the batch ingest.

## Assumptions

- **"What changed" is computed live** from the snapshot and the current section, the same way triage computes it, so
  `--test-id` works without a prior triage. `--group` still validates the group against the last triage (staleness).
- **A group in which a test has nothing to reconcile** is refused as a whole (`NOTHING_TO_RECONCILE`, naming the tests),
  so the user can compile the others with `--test-id`; groups share their changed items, so this is rare.
- **`--group` is accepted for any bucket**; the skill only uses it for the edit buckets (LOAD, CRITERIA_CHANGED, LIGHT).
- **"Editable fields"** are the fields `ingest-update` can change (the ones its edit applies); id and the protected
  fields guarded by the drift check are excluded.
- **The prompt goes to stdout as today**; refusals are one JSON document on stderr in the shared shape.
- **New refusal codes** (exit 4): `NOTHING_TO_RECONCILE`, `PROMPT_TOO_LARGE`, `TEMPLATE_OUTDATED`.
- **Refs written differently that resolve to the same section** are listed per ref as written; de-duplicating them by
  content is out of scope.

## Out of Scope

- Critic changes and re-verification after edits (Phase 4).
- Subagents (Phase 5) and generation prompts (Phase 5b).
- The deferred 085 review findings.
- Heading-rename relinking.
