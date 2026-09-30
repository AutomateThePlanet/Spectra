# Feature Specification: Review Fixes for Specs 078 and 079

**Feature Branch**: `080-review-fixes-078-079`
**Created**: 2026-09-29
**Status**: Draft
**Input**: User description: "Review fixes for specs 078 (lossless test writes) and 079 (stable criteria IDs), found by code review before merge. No new features. Every fix needs a regression test that fails before the fix. Out of scope: the DeleteHandler depends_on regex bug (tracked separately) and everything in Phase 2+ (change detection, triage, relinking)."

## Context

Code review of 078 (merged into `fix/doc-change-update-workflow`) and 079 (committed as `1ba172a` on
`079-stable-criteria-ids`, not merged) found fourteen defects. This spec fixes them before 079 is merged. It adds no
features. Every item gets a regression test that fails before its fix and passes after it.

Two facts from checking the code narrow the items down:
- **A1.** `spectra ai update` reads only the top-level `*.md` files of a suite. So the realistic trigger is a file whose
  name is not exactly `{id}.md` (for example `TC-100-login.md` with `id: TC-100`). A test in a suite subfolder is never
  read, so it can't be rewritten.
- **A5.** The central config loader treats a missing file as an error and requires `source`, `tests` and `ai` sections.
  Several of the hand-rolled loaders silently fall back to defaults instead, including on a config they could not parse,
  and some existing tests rely on commands running without a config at all. The decision is recorded under
  Clarifications.

## Clarifications

### Session 2026-09-29

- Q: What is the consistent behaviour for a missing or malformed config? → A: One central config loader with two entry
  points.
  - **Missing config.** Read-only commands (`compile-extraction-prompt`, the critic, repair-prompt and repair-batch
    loaders, `docs changed`) use built-in defaults. Write commands (`ingest-criteria`, `docs retire`,
    `docs apply-move`, `docs migrate-criteria`, `--import-criteria`) exit 1 with
    `No spectra.config.json found. Run 'spectra init' first.`
  - **Malformed or invalid config.** Every in-scope command exits 1 with one shared message. No command ever falls back
    to defaults on a config it could not parse.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Update writes to the test's own file (Priority: P1)

A QA engineer keeps tests in files whose names are not exactly the test ID. When `spectra ai update --no-review` marks
such a test orphaned, the mark goes into that same file; no second file with the same ID appears. With
`--delete-orphaned`, the test's own file is deleted.

**Why this priority**: The current behaviour creates duplicate test IDs and silently fails to delete. That corrupts the
suite, which is the kind of damage 078 set out to stop.

**Independent Test**: A suite holds `TC-100-login.md` (`id: TC-100`) whose only doc was deleted. Run the update with
`--no-review`, then with `--no-review --delete-orphaned`, and check the files in the suite.

**Acceptance Scenarios**:

1. **Given** `TC-100-login.md` with `id: TC-100` and a missing source doc, **When** the update applies, **Then**
   `TC-100-login.md` is marked orphaned and no `TC-100.md` exists.
2. **Given** the same test, **When** the update applies with `--delete-orphaned`, **Then** `TC-100-login.md` is deleted
   and no other file is touched.

---

### User Story 2 - Docs outside the repository still resolve (Priority: P1)

A team keeps its documentation in a sibling folder and points `source.local_dir` at it (`../product-docs/docs`).
`spectra ai update` finds the referenced documents there, so tests are not marked ORPHANED. The critic and repair prompt
compilers load those documents too. A ref that points outside both the repository and the source folder is still
refused, and the message says why.

**Why this priority**: Today every test in such a setup is misclassified ORPHANED, the same failure 078 fixed for
in-repo docs.

**Independent Test**: A repository whose `source.local_dir` points to a sibling folder, with tests referencing documents
there. Classify, then compile a critic prompt with bare, relative and absolute refs into that folder, and with a ref
that escapes both roots.

**Acceptance Scenarios**:

1. **Given** `source.local_dir: ../product-docs/docs` and a test referencing `checkout.md#Payment`, **When** the suite
   is classified, **Then** the test is not ORPHANED for "source not found".
2. **Given** the same setup, **When** a critic prompt is compiled for a test whose ref is an absolute path inside the
   source folder, **Then** the document is loaded.
3. **Given** a ref that resolves outside both the repository and the source folder, **When** it is resolved, **Then**
   it is refused, and the message says it is outside the repository and the configured source folder (not merely "not
   found").

---

### User Story 3 - Test-file rewrites keep every key and comment in place (Priority: P1)

When SPECTRA rewrites a test file, a YAML comment stays directly above the key it annotates, and a comment at the top of
the frontmatter stays at the top. It is not dropped when the previous key changes, and it doesn't move when keys are
reordered. No known frontmatter key can be missing from the writer's list, because the parser and writer share one list.

**Why this priority**: Losing hand-written comments is the data loss 078 was meant to prevent. A key missing from the
writer's list would be silently dropped on every rewrite.

**Independent Test**:
- A test file with a leading comment, a comment above `source_refs`, and a comment above an unknown key. Change the
  field above each comment, rewrite, and check that each comment is still directly above its own key.
- A test that fails if the parser's and writer's key lists ever differ.

**Acceptance Scenarios**:

1. **Given** a comment line directly above `source_refs`, **When** the entry before it (e.g. `depends_on`) is changed
   and the file rewritten, **Then** the comment is still present, directly above `source_refs`.
2. **Given** a comment at the very top of the frontmatter, **When** keys are reordered on rewrite, **Then** the comment
   is still the first line of the frontmatter.
3. **Given** a comment above an unknown key, **When** the file is rewritten, **Then** the comment stays with that key.
4. **Given** the known-key lists, **When** the test suite runs, **Then** it fails if any key is known to the parser but
   absent from the writer's order, or the other way round.

---

### User Story 4 - Shared (pre-079) criteria files are handled by provenance everywhere (Priority: P1)

In a workspace where two documents shared one criteria file before 079, the following all consider only the criteria
that document wrote, as recorded on each criterion's `source_doc`:
- compiling the extraction prompt for one of them
- ingesting it
- applying a move
- retiring it

When a document leaves the shared file, its criteria are removed from that file. When the file then holds nothing and
no index entry points to it, it is deleted. The other document's criteria are never copied, relabelled or offered.

**Why this priority**: Today such a document can never be re-extracted (its prompt offers foreign IDs, which ingest
rejects), a move copies the other document's criteria, and a retired document's criteria keep counting in coverage.

**Independent Test**: Seed a shared file holding criteria of `docs/a/login.md` and `docs/b/login.md`. Compile the
prompt for A, ingest A, apply-move B, and retire one of them, checking the shared file and each doc's own file after
each step.

**Acceptance Scenarios**:

1. **Given** a shared file, **When** the extraction prompt is compiled for A, **Then** its existing criteria list only
   criteria whose `source_doc` is A.
2. **Given** that prompt's IDs returned by the model, **When** A is ingested, **Then** the ingest succeeds and keeps
   them.
3. **Given** A was re-ingested into its own file, **When** the shared file is inspected, **Then** it no longer contains
   A's criteria but still contains B's.
4. **Given** a shared file, **When** B is moved and the move is applied, **Then** B's new file contains only B's
   criteria.
5. **Given** a shared file, **When** a deleted document is retired, **Then** its criteria are removed from the shared
   file and no longer count in coverage.
6. **Given** the last document has left a shared file, **When** it holds no criteria and nothing references it, **Then**
   the file is deleted.

---

### User Story 5 - Criteria results are honest and machine-readable (Priority: P2)

Tools and the skill can rely on the output:
- a rejected ingest is reported with its own outcome, not "Extracted"
- a failed prompt compilation writes only valid JSON to stderr in JSON mode
- tags that were only reordered are not reported as a change and cause no rewrite
- the normaliser never treats a `<` … `>` comparison as an HTML tag
- `docs changed` and the criteria store find criteria files with the same scan

**Why this priority**: Wrong outcomes and invalid JSON break automation. Spurious rewrites churn git. A normaliser that
eats real text can merge two different criteria.

**Independent Test**:
- Reject an ingest (exit 7) and check the outcome field.
- Make a compile fail (document not found) in JSON mode, and parse stderr as JSON.
- Re-ingest the same criteria with tags reordered, and check nothing is written.
- Normalise `total<limit and qty>5` and `total<limit and qty>6`, and check the keys differ.

**Acceptance Scenarios**:

1. **Given** a response with a foreign ID, **When** it is ingested, **Then** the failure JSON's `outcome` is a distinct
   rejection value, and nothing that switches on the outcome treats it as extracted.
2. **Given** a failed compile in JSON mode (document not found, unreadable, or invalid config), **When** stderr is read,
   **Then** it parses as one JSON document with a `warnings` field holding every warning collected before the failure
   (possibly none).
3. **Given** criteria whose tags differ only in order or case, **When** they are re-ingested, **Then** the changeset is
   empty and the criteria file is byte-identical.
4. **Given** `total<limit and qty>5` and `total<limit and qty>6`, **When** both are normalised, **Then** the keys
   differ, and `<b>x</b>` still normalises to `x`.
5. **Given** a workspace with criteria files, **When** `docs changed` reports duplicates, **Then** it uses the same
   criteria-file scan as the criteria store. A file found by one is found by the other.

---

### User Story 6 - The skill handles every criteria error, and config loads one way (Priority: P2)

The criteria skill knows what to do when `ingest-criteria`, `apply-move` or `retire` exits 1: it reports the error code
and stops for that document, retrying once on `LOCK_TIMEOUT`. Every command listed in FR-005 loads configuration
through the central loader and behaves the same way when the config is missing or malformed.

**Why this priority**: Without the instruction, the skill improvises on an error. Inconsistent config handling gives
different answers to the same broken workspace.

**Independent Test**:
- Skill-content assertions for the exit-1 handling.
- For each command in scope, run once with no config and once with malformed config, and compare exit codes and
  messages.

**Acceptance Scenarios**:

1. **Given** the criteria skill, **When** its ingest, apply-move and retire steps are read, **Then** each says: on
   exit 1, report the `error_code` and stop for that document, and on `LOCK_TIMEOUT`, retry once.
2. **Given** a malformed or invalid `spectra.config.json`, **When** any in-scope command runs (read-only or write),
   **Then** it exits 1 with the one shared message and never proceeds on defaults.
3. **Given** no `spectra.config.json`, **When** a read-only command runs (`compile-extraction-prompt`, a critic,
   repair-prompt or repair-batch compile, `docs changed`), **Then** it uses the built-in defaults.
4. **Given** no `spectra.config.json`, **When** a write command runs (`ingest-criteria`, `docs retire`,
   `docs apply-move`, `docs migrate-criteria`, `--import-criteria`), **Then** it exits 1 with
   `No spectra.config.json found. Run 'spectra init' first.`

---

### Edge Cases

- **A test file named `{id}.md`** behaves exactly as today.
- **A test file whose name does not match its ID, marked orphaned twice**: the second run changes nothing (078's
  date-keeping still holds).
- **A source dir inside the repository** (the default): resolution is unchanged, and repo-root-first order still holds.
- **A ref that is valid under both roots**: the repo-root candidate still wins (unchanged order).
- **Blank lines between frontmatter entries**: they stay attached to the entry below them, like comments. Comments or
  blank lines after the last key stay at the end of the frontmatter.
- **A shared file where some criteria have no `source_doc`**: they belong to no document. They are never offered to,
  moved with, or removed for a specific document. The file is only deleted when it holds no criteria at all and is
  unreferenced. This applies while the file is shared. Once a single index entry points at a file, it is that
  document's file (the 079 rule), and retiring or moving that document handles the whole file.
- **A key removed on rewrite** (e.g. `depends_on` emptied): the comment and blank lines directly above it are removed
  with it. Comments above other keys are unaffected.
- **Tags that differ in case only** (`UX` vs `ux`): no change; the stored casing is kept.
- **A literal `<` or `>` inside real HTML** (e.g. `<b>a < b</b>`): the tags are stripped and the comparison text kept.

## Requirements *(mandatory)*

### Functional Requirements

**Part A — 078 review**

- **FR-001 (A1)**: When `spectra ai update` applies changes, updates and orphan marks MUST be written to, and deletions
  MUST remove, the test's own file (the file it was read from). It MUST never use a path built from the test ID.
- **FR-002 (A2)**: Doc-reference resolution MUST accept a file under the configured source directory even when that
  directory lies outside the repository.
- **FR-002a (A2)**: A ref that resolves outside both the repository and the source directory MUST still be refused.
- **FR-002b (A2)**: The critic and repair prompt loaders MUST follow the same rule as FR-002/FR-002a, and their error
  message MUST state the reason for the refusal: outside both roots, or not found.
- **FR-003 (A3)**: The set of known frontmatter keys MUST come from a single definition used by both the parser and the
  writer, or a test MUST fail whenever the two differ.
- **FR-004 (A4)**: Comment lines, and blank lines, directly above a frontmatter key MUST stay attached to that key when
  the file is rewritten.
- **FR-004a (A4)**: Comments at the top of the frontmatter MUST stay at the top, and comments after the last key MUST
  stay at the end.
- **FR-004b (A4)**: FR-004 and FR-004a apply to known and unknown keys alike. Round-trip and byte-identity guarantees
  from 078 MUST still hold.
- **FR-005 (A5)**: Every in-scope command MUST load configuration through the one central config loader, which offers
  two entry points: a read-only one and a write one.
  - **Read-only commands:** `compile-extraction-prompt`, the critic, repair-prompt and repair-batch ref loaders, and
    `docs changed`.
  - **Write commands:** `ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria` and
    `ai analyze --import-criteria`.
- **FR-005a (A5)**: **Missing config.** Read-only commands MUST use the built-in defaults. Write commands MUST exit 1
  with exactly `No spectra.config.json found. Run 'spectra init' first.`
- **FR-005b (A5)**: **Malformed or invalid config.** Every in-scope command MUST exit 1 with one shared message.
  No command may ever fall back to defaults on a config it could not parse. That silent fallback is what emptied
  `existing_criteria` and churned IDs.

**Part B — 079 review**

- **FR-006 (B1)**: `docs apply-move` MUST carry over only criteria whose `source_doc` is the moved document when its
  criteria file is shared with other documents.
- **FR-007 (B2)**: When a document leaves a shared criteria file, by re-ingest to its own file or by retire, its
  criteria (by `source_doc`) MUST be removed from the shared file.
- **FR-007a (B2)**: A shared file that then holds no criteria and is referenced by no index entry MUST be deleted.
  Criteria of other documents in it MUST be kept unchanged.
- **FR-008 (B3)**: `compile-extraction-prompt` MUST offer as existing criteria only those whose `source_doc` is the
  document, when its criteria file is shared. This is the same rule ingest uses, so the model can never be offered an ID
  that ingest would reject.
- **FR-009 (B4)**: Tags MUST be compared as a case-insensitive set, ignoring order and duplicates. Criteria whose tags
  differ only in order or case MUST yield an empty changeset, and so no rewrite.
- **FR-010 (B5)**: The `spectra-criteria` skill MUST tell the session, for exit 1 from `ingest-criteria`, `apply-move`
  and `retire`, to report the `error_code` and stop for that document, and to retry once on `LOCK_TIMEOUT`.
  Skill-content tests MUST assert this.
- **FR-011 (B6)**: A rejected ingest MUST report a distinct outcome value, not `Extracted`, in its JSON output. Every
  code path that switches on the outcome MUST handle it.
- **FR-012 (B7)**: In JSON mode, the failure output of `compile-extraction-prompt` MUST be exactly one JSON document.
  Warnings MUST appear inside it as a `warnings` field rather than as separate text. The field is always present,
  and it is empty when the command fails before any warning is collected.
- **FR-013 (B8)**: `docs changed` and the criteria store MUST find criteria files through one shared scan.
- **FR-014 (B9)**: The text normaliser MUST strip only well-formed HTML tags. Text using `<` and `>` as comparison
  operators MUST be kept, so two criteria differing only there get different keys.

**General**

- **FR-015**: Every item A1–A5 and B1–B9 MUST have a regression test that fails on the pre-fix code and passes after
  the fix.
- **FR-016**: Human-readable summaries stay unsuppressed in JSON mode. Failure output stays machine-readable. Dry runs
  write nothing.
- **FR-017**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes" and `docs/cli-reference.md` MUST be updated where behaviour
  changes. At minimum this covers: out-of-repo source dirs, the rejection outcome value, compile-extraction-prompt
  failure JSON, shared-file handling, and config behaviour.

### Key Entities

- **Test file of record**: the file a test was read from. It is the only file an update may write or delete for that
  test.
- **Resolution roots**: the repository root and the configured source directory. A ref resolves only to a file under
  one of them.
- **Frontmatter entry**: one top-level key, together with the comment and blank lines directly above it.
- **Shared criteria file**: a pre-079 criteria file referenced by more than one document's index entry. Its criteria
  belong to documents by their `source_doc` (provenance).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Applying an update to a test whose file name differs from its ID creates 0 additional files, and
  `--delete-orphaned` removes exactly that test's file.
- **SC-002**: With the source directory outside the repository, 0 tests referencing existing docs there are classified
  ORPHANED for "source not found".
- **SC-003**: 100% of frontmatter comments stay directly above their key, or at the top or end, after any rewrite.
- **SC-004**: For a document on a shared file, the prompt offers 0 criteria of other documents, the first re-extraction
  succeeds, and after it the shared file contains 0 of that document's criteria.
- **SC-005**: Re-ingesting criteria whose tags differ only in order or case writes 0 files.
- **SC-006**: 100% of JSON-mode failure outputs of the criteria commands parse as a single JSON document.
- **SC-006a**: A malformed config makes 100% of in-scope commands exit 1 with the same message, and 0 commands proceed
  on defaults.
- **SC-007**: Every one of the 14 review items has a test that fails before the fix and passes after it.
- **SC-008**: Existing Core, CLI, Execution and Integration suites stay green, except the known pre-existing
  `ProfileFormatLoaderTests` failure.

## Assumptions

- The rejection outcome value is `Rejected`.
- Warnings on a successful compile continue to go to stderr as text (stdout is the prompt). FR-012 concerns the failure
  output.
- Config loading is unified only in the commands listed in FR-005. Other commands that hand-roll config loading are
  unchanged here.
- "Invalid config" means the central loader's validation fails (e.g. a missing `source`, `tests` or `ai` section). The
  shared message names the file and the first error.
- `source.local_dir` outside the repository is a supported setup. Allowing reads there does not widen access beyond
  what the user configured.
- Git is the undo path for any file rewritten or deleted by these fixes.

## Out of Scope

- The `DeleteHandler` / `SuiteDeleteHandler` `depends_on` regex bug (tracked separately).
- Everything in Phase 2 and later: change detection, triage, relinking.
- Reading tests in suite subfolders during `ai update` (the batch reader reads top-level files only; unchanged).
