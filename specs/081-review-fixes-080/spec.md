# Feature Specification: Review Fixes for Spec 080 (Part C)

**Feature Branch**: `081-review-fixes-080`
**Created**: 2026-09-30
**Status**: Draft
**Input**: User description: "Part C — from the 080 review: C1 shared-file ownership compares per-criterion source_doc
only lexically; C2 config validation rejects configs lacking sections a command doesn't use; C3 the normaliser's tag
whitelist misses common doc markup and rejects valueless attributes; C4 compile-extraction-prompt's failure JSON
differs from the other criteria commands; C5 detaching trailing blank lines can drop content of a keep-chomped block
scalar."

## Context

A code review of spec 080 found five more defects: one in shared criteria files, one in config loading, one in text
comparison, one in failure output and one in test-file rewrites. Spec 080 was merged into
`fix/doc-change-update-workflow` in `1c8d8de`.

This spec fixes them. It adds no features. Every fix gets a regression test that fails on `1c8d8de` and passes after
the fix. The same rules as 080 apply:
- Human-readable summaries are never suppressed in JSON mode.
- Failure output stays machine-readable.
- The CLI stays model-free and deterministic.
- `--dry-run` writes nothing.
- Git is the undo path.

## Clarifications

### Session 2026-09-30

- Q: When a config lacks a section the command does read (C2 / FR-004), what happens? → A: The same rule as a
  missing file. Each command declares the sections it reads, and sections it doesn't read (e.g. `ai`) are never
  required. When a section the command reads is missing:
  - read commands (`compile-extraction-prompt`, `compile-critic-prompt`, `compile-repair-prompt`,
    `compile-repair-batch`, `docs changed`) use that section's built-in defaults and print a warning naming the
    section;
  - write commands (`ingest-criteria`, `docs retire`, `docs apply-move`, `docs migrate-criteria`,
    `--import-criteria`) exit 1 with `spectra.config.json is invalid: missing '<section>' section`.

  Malformed JSON stays exit 1 everywhere.
- Plan-time amendments (research R1, R3): FR-003 also covers the index lookup in `docs changed` and in the write
  commands' criteria workspace. FR-007 leaves out element names that are also common placeholder words in docs.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Old shared criteria files keep their owners (Priority: P1)

A team upgraded from before 2.6. Two documents shared one criteria file, and its criteria record their `source_doc`
the way it was typed back then: as an absolute path, with backslashes or with `./`. Each document still owns its own
criteria:
- re-extracting it offers them and keeps their IDs;
- moving or retiring it takes them out of the shared file.

**Why this priority**: Today such a document owns none of its criteria. Every ID changes on the next extraction,
tests lose their `criteria:` links, and retiring or moving the document leaves its criteria in the shared file.

**Independent Test**: Seed a shared file where A's criteria carry `source_doc` as an absolute path inside the
repository and B's carry a relative path. Compile A, ingest A, and retire B. Check the IDs and the file contents after
each step.

**Acceptance Scenarios**:

1. **Given** a shared file whose criteria for `docs/a/login.md` store `source_doc` as an absolute path into the
   repository, **When** the extraction prompt is compiled for `docs/a/login.md`, **Then** those criteria are offered as
   its existing criteria.
2. **Given** the same file, **When** A is re-ingested with those IDs, **Then** the ingest succeeds, the IDs are kept,
   and A's criteria are taken out of the shared file.
3. **Given** the same file, **When** B (stored `docs\b\login.md` or `./docs/b/login.md`) is retired or moved, **Then**
   only B's criteria are removed or moved.
4. **Given** a criterion whose `source_doc` points outside the repository, **When** ownership is decided, **Then** it
   belongs to no document, as a criterion without `source_doc` does today.

---

### User Story 2 - Config sections a command doesn't use don't block it (Priority: P1)

A user's `spectra.config.json` has no `ai` section, because nothing in the model-free workflow needs one. The criteria
and compile commands run. A read command that needs a missing section runs on that section's defaults and says so. A
write command that needs a missing section refuses and names the section. A config that is actually malformed still
fails loudly with the shared message.

**Why this priority**: Since 080, these commands reject such configs, although none of them reads `ai`. That breaks
existing workspaces for no reason.

**Independent Test**: For each in-scope command, run it with a config that lacks one section at a time (`ai`,
`source`, `tests`). Check that it runs when the command doesn't use the missing section, and follows the chosen rule
when it does. Run it once more with malformed JSON and check that it still exits 1.

**Acceptance Scenarios**:

1. **Given** a config with `source`, `tests` and `coverage` but no `ai`, **When** any in-scope command runs, **Then**
   it is not rejected for the missing `ai` section.
2. **Given** a config without `source` or `tests`, **When** a read command that reads the missing section runs
   (e.g. `compile-critic-prompt` without `tests`), **Then** it uses that section's built-in defaults and prints a
   warning naming the section.
3. **Given** a config without `tests`, **When** a write command that reads it runs (`ingest-criteria`,
   `docs retire`), **Then** it exits 1 with `spectra.config.json is invalid: missing 'tests' section` and writes
   nothing.
4. **Given** a config without `source` or `tests`, **When** a command that reads neither runs (e.g.
   `compile-extraction-prompt`, `docs apply-move`, `docs migrate-criteria`), **Then** it runs normally with no
   warning.
5. **Given** malformed JSON, **When** any in-scope command runs, **Then** it exits 1 with
   `spectra.config.json is invalid: …`, unchanged from 080.

---

### User Story 3 - Markup-only edits are not changes (Priority: P2)

A technical writer wraps a paragraph in `<details>`/`<summary>`, adds a `<figure>` with a `<figcaption>`, or sets
`<td nowrap>`. Re-extracting criteria from the edited document reports no modified criteria, because only markup
changed. Comparisons written with `<` and `>` in plain text (`total<limit and qty>5`, `<Limit>`) are still text.

**Why this priority**: Markup-only edits currently show up as modified criteria. Reviewers see false changes, and the
criteria files churn in git.

**Independent Test**: Normalise pairs of texts that differ only in the listed markup and check that their comparison
keys are equal. Normalise the 080 comparison cases again and check that they are unchanged.

**Acceptance Scenarios**:

1. **Given** text wrapped in any of the added elements (e.g. `<details><summary>Refunds</summary>…</details>`,
   `<figure>…<figcaption>…</figcaption></figure>`, `<section>`, `<center>`, `<font color=red>`), **When** it is
   normalised, **Then** the markup is removed and the text kept.
2. **Given** a tag with a valueless boolean attribute (`<td nowrap>`, `<details open>`, `<input disabled>`),
   **When** it is normalised, **Then** the tag is removed.
3. **Given** `total<limit and qty>5`, `value <Limit> applies`, `if a<b and c>d then`, **When** they are normalised,
   **Then** the angle-bracket text is kept exactly as in 080.

---

### User Story 4 - One failure shape for every criteria command (Priority: P2)

An agent or CI job that reads criteria-command failures in JSON mode sees the same fields from every criteria command:
`command`, `success`, `error_code`, `errors` and `warnings`. It needs one parser, and the `spectra-criteria` skill
reads the same fields at every step.

**Why this priority**: `compile-extraction-prompt` reports failures as `{error_code, message, warnings}` or
`{refused, missing_input, message, warnings}`. Retire, apply-move, migrate-criteria and ingest-criteria report
`{command, success, error_code, errors}`. Consumers have to handle both.

**Independent Test**: Trigger a failure in each criteria command in JSON mode, and check that stderr is one JSON
document with the five common fields. Check the skill text for the unified field names.

**Acceptance Scenarios**:

1. **Given** any failure of `compile-extraction-prompt`, `ingest-criteria`, `docs retire`, `docs apply-move` or
   `docs migrate-criteria` in JSON mode, **When** stderr is parsed, **Then** it is one JSON document with `command`,
   `success: false`, `error_code`, `errors` (at least one message) and `warnings` (an array, possibly empty).
2. **Given** a `compile-extraction-prompt` refusal (exit 4), **When** it is parsed, **Then** it has the common fields
   and still says which input was missing.
3. **Given** a rejected ingest, **When** it is parsed, **Then** it has the common fields and still carries `outcome`.
4. **Given** the `spectra-criteria` skill, **When** its failure handling is read, **Then** every step reads
   `error_code` and `errors[0]`, never `message`.

---

### User Story 5 - Rewrites keep trailing lines of a keep-chomped value (Priority: P2)

A test's frontmatter has a value written as a keep-chomped block scalar (`notes: |+`), whose trailing blank lines are
part of the value. When the key below it is removed on rewrite, the value still has all its trailing lines.

**Why this priority**: 080 attaches trailing blank lines to the key below. For `|+` and `>+` values those lines are
content, so removing the next key silently changes the value, which is the data loss 078 exists to prevent.

**Independent Test**: A test file with a `|+` value (top-level and nested in `custom`) followed by a removable key.
Remove that key, rewrite, re-parse, and compare the value.

**Acceptance Scenarios**:

1. **Given** a top-level key whose value is a `|+` or `>+` block scalar ending in blank lines, followed by another
   key, **When** the following key is removed on rewrite, **Then** the re-parsed value is identical, trailing blank
   lines included.
2. **Given** a keep-chomped block scalar nested inside a mapping value (e.g. under `custom`) at the end of the entry,
   **When** the next top-level key is removed, **Then** the nested value is identical after re-parsing.
3. **Given** a keep-chomped value followed by blank lines and then a column-0 comment above the next key, **When**
   the file is rewritten, **Then** the comment still moves with the key below it, as in 080.
4. **Given** values with clip (`|`) or strip (`|-`) chomping, **When** files are rewritten, **Then** behaviour is
   unchanged from 080.

---

### Edge Cases

- **Relative, `./` and backslash `source_doc`** (C1): these already matched in 080 and still do.
- **Absolute `source_doc` outside the repository** (C1): it belongs to no document. It is never offered, moved or
  removed for a specific document.
- **An index entry whose `source_doc` is absolute** (C1): the document lookup uses the same repo-aware normalisation.
  This matters for `compile-extraction-prompt`, which reads without migrating.
- **A config missing several sections** (C2): each missing section is judged separately under the clarified rule.
- **A boolean attribute name used as a plain word** (C3): `<b open>` is a tag, and the text between `<` and `>` goes.
  `a<b and c>d` stays text, because `and` and `c` are not boolean attribute names.
- **Uppercase tags and attributes** (C3): matched case-insensitively, as in 080.
- **A refusal in the unified shape** (C4): `error_code` is `MISSING_INPUT` and `missing_input` is kept as an extra
  field.
- **Human-mode failures** (C4): unchanged. The warnings first, then the error line, on stderr.
- **Keep-chomped value with an explicit indentation indicator** (C5): `|2+`, `|+2`, `>+1` and similar are keep-chomped
  too.
- **Keep-chomped value that is the last entry** (C5): its trailing blank lines stay with it, not with the
  frontmatter's trailing text.

## Requirements *(mandatory)*

### Functional Requirements

**C1 — ownership**

- **FR-001**: Deciding which criteria in a shared file belong to a document MUST compare repo-aware normalised paths
  on both sides. A stored `source_doc` that is absolute and inside the repository, or relative with `\` or `./`, MUST
  match the same document as its repository-relative form.
- **FR-002**: A stored `source_doc` that resolves outside the repository, or is empty, MUST belong to no document.
- **FR-003**: FR-001 applies wherever 080 uses ownership: the compile's existing criteria, ingest's previous criteria,
  apply-move, retire, and removing a document's criteria from a shared file. The same repo-aware comparison MUST be
  used to find a document's index entry in `compile-extraction-prompt`, in `docs changed`, and in the criteria
  workspace the write commands use, which matters under `--dry-run` because it skips migration.

**C2 — config sections**

- **FR-004**: Each in-scope command MUST declare the config sections it reads. A section it doesn't read (for example
  `ai`, which none of them reads) MUST never be required.
- **FR-004a**: When a section the command reads is missing, a read command MUST use that section's built-in defaults
  and print a warning naming the section, on stderr. Where the command's JSON output already has a `warnings` field,
  the warning goes there too. Read commands: `compile-extraction-prompt`, `compile-critic-prompt`,
  `compile-repair-prompt`, `compile-repair-batch`, `docs changed`.
- **FR-004b**: When a section the command reads is missing, a write command MUST exit 1 with
  `spectra.config.json is invalid: missing '<section>' section`, write nothing, and report the message on the
  command's usual failure path. Write commands: `ingest-criteria`, `docs retire`, `docs apply-move`,
  `docs migrate-criteria`, `ai analyze --import-criteria`.
- **FR-004c**: The `coverage` section keeps its current behaviour. It is optional for every command, and its
  built-in defaults apply without a warning, because it has never been required.
- **FR-005**: Malformed JSON, and values of the wrong type in any section, MUST still exit 1 with
  `spectra.config.json is invalid: …` in every in-scope command. No command proceeds on defaults after failing to
  parse the file.
- **FR-006**: The missing-config rules from 080 are unchanged: read commands use defaults, and write commands exit 1
  with `No spectra.config.json found. Run 'spectra init' first.`

**C3 — normaliser**

- **FR-007**: The text normaliser MUST also strip well-formed tags of these HTML elements: `figure`, `figcaption`,
  `details`, `summary`, `section`, `article`, `aside`, `caption`, `col`, `colgroup`, `tfoot`, `center`, `font`,
  `picture`, `source`, `big`, `tt`, `wbr`, `samp`, `dfn`, `bdi`, `bdo`, `ruby`, `rt`, `rp`, `input`, `video`, `audio`,
  `iframe`. Names that are also common placeholder words in docs (`header`, `footer`, `nav`, `main`, `time`, `data`,
  `label`, `button`) stay text (research R3).
- **FR-008**: A tag MAY carry valueless attributes whose name is a known HTML boolean attribute (for example `nowrap`,
  `open`, `hidden`, `disabled`, `checked`, `selected`, `readonly`, `required`, `multiple`, `autoplay`, `controls`,
  `loop`, `muted`, `reversed`, `compact`, `noshade`, `allowfullscreen`, `async`, `defer`), mixed with valued
  attributes in any order. Other bare words MUST NOT make an angle-bracket span a tag.
- **FR-009**: Every B9 case from 080 MUST normalise exactly as it does on `1c8d8de`, including
  `total<limit and qty>5`, `value <Limit> applies` and `if a<b and c>d then`.

**C4 — failure shape**

- **FR-010**: In JSON mode, every failure of `compile-extraction-prompt`, `ingest-criteria`, `docs retire`,
  `docs apply-move` and `docs migrate-criteria` MUST be one JSON document on stderr with `command`, `success`
  (`false`), `error_code` (a string, or `null` for a config error), `errors` (non-empty array of messages) and
  `warnings` (array, possibly empty).
- **FR-011**: Command-specific fields MAY be added: `outcome` on ingest failures, and `missing_input` on
  compile-extraction-prompt refusals, whose `error_code` is `MISSING_INPUT`. The fields `refused` and `message` are
  replaced by the common fields.
- **FR-012**: Exit codes and human-mode output MUST be unchanged.
- **FR-013**: The `spectra-criteria` skill MUST read `error_code` and `errors[0]` for every failure it handles,
  including compile refusals. Its content tests MUST check that.

**C5 — keep-chomped block scalars**

- **FR-014**: Trailing blank lines of a frontmatter entry whose value ends with a keep-chomped block scalar (`|+`, `>+`,
  with or without an indentation indicator, at any nesting depth) MUST stay with that entry, never with the next
  key's prelude or the trailing text.
- **FR-015**: Column-0 comment lines after such a value, and the lines that follow them, MUST still attach to the key
  below, as in 080. The byte-for-byte reproduction and round-trip guarantees from 078/080 MUST still hold.

**Cross-cutting**

- **FR-016**: Every item C1–C5 MUST have a regression test that fails on `1c8d8de` and passes after the fix.
- **FR-017**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes" and `docs/cli-reference.md` MUST be updated where behaviour
  changes: C2 config acceptance, C3 normalisation, and the C4 failure shape.

### Key Entities

- **Criterion provenance**: the `source_doc` stored on each criterion in a criteria file. Its normalised
  repository-relative form decides which document owns the criterion.
- **Config section requirement**: which top-level config sections each in-scope command reads (see the Assumptions).
- **Criteria failure document**: the one JSON object a criteria command writes to stderr on failure in JSON mode.
- **Keep-chomped block scalar**: a YAML block value with the `+` chomping indicator. Its trailing line breaks are part
  of the value.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a shared file whose criteria store absolute in-repository `source_doc` paths, re-extracting the
  document changes 0 of its criterion IDs, and retiring it leaves 0 of its criteria in the shared file.
- **SC-002**: A config without an `ai` section is accepted by 10 of 10 in-scope commands. A malformed config is
  rejected by 10 of 10 with the shared message. For every command and every section it reads, a missing section gives
  defaults plus a warning (read commands) or exit 1 naming the section (write commands), in 100% of cases.
- **SC-003**: For each added element and each boolean attribute in the lists, a markup-only edit gives the same
  comparison key 100% of the time, and 100% of the 080 comparison cases normalise as before.
- **SC-004**: 100% of JSON-mode failures of the five criteria commands parse as one JSON document with the five common
  fields.
- **SC-005**: After removing the key that follows a keep-chomped value, the re-parsed value is identical in 100% of
  cases (top-level and nested).
- **SC-006**: Every one of the five items has a test that fails on `1c8d8de` and passes after the fix.
- **SC-007**: The existing Core, CLI, Execution and Integration suites stay green, except the known pre-existing
  `ProfileFormatLoaderTests` failure.

## Assumptions

- **Command scope** is the same ten commands as 080 FR-005. Declared sections, not counting the always-optional
  `coverage` (FR-004c):

  | Command | Reads | Missing section → |
  |---|---|---|
  | `compile-extraction-prompt` | — | nothing to check |
  | `compile-critic-prompt`, `compile-repair-prompt`, `compile-repair-batch` | `source`, `tests` | defaults + warning |
  | `docs changed` | `source` | defaults + warning |
  | `ingest-criteria`, `docs retire` | `tests` (reference counts) | exit 1 |
  | `docs apply-move`, `docs migrate-criteria`, `ai analyze --import-criteria` | — | nothing to check |

  No command reads `ai`. If implementation shows that a command reads a section not listed here, the table is updated
  and that command follows the same rule.
- **C4 applies to the five criteria commands.** `ai analyze --import-criteria` keeps its `ErrorResult` on stdout, as
  the rest of `ai analyze` does. `docs changed`, `compile-critic-prompt`, `compile-repair-prompt` and
  `compile-repair-batch` keep their plain-text errors. Both choices are unchanged from 080.
- **C4 changes compile-extraction-prompt's JSON failure fields** (`message` → `errors`, `refused` → `error_code:
  MISSING_INPUT`). 080 has not been released, so this is treated as a correction within 2.6.0, not a breaking change.
- **The C3 boolean-attribute list** is a fixed set in the normaliser. Valueless attributes outside it keep the span as
  text, which is how `a<b and c>d` stays text.
- **C1 relies on the existing repo-aware normalisation** that `--doc` arguments already use (repo-relative, `/`,
  absolute paths inside the repository accepted).

## Out of Scope

- The DeleteHandler `depends_on` regex bug (tracked separately).
- Moving the other `ai analyze` modes, `ai update`, `docs index` or any other command onto the shared config loader.
- Phase 2+ of the doc-change workflow (change detection, triage, relinking).
- Changing how migration treats collided files beyond the ownership comparison.
