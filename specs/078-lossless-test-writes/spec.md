# Feature Specification: Lossless Test Writes and Doc-Reference Resolution (Update Workflow Phase 0)

**Feature Branch**: `078-lossless-test-writes`
**Created**: 2026-09-24
**Status**: Draft
**Input**: User description: "Phase 0 of the doc-change → criteria → test update workflow: stop test-file data loss, fix doc-reference resolution in `spectra ai update`, and remove a duplicate doc read in the criteria skill. This is a damage-control phase. It changes no workflow design; later phases (stable criteria IDs + changeset, change-driven test selection, edit-prompt content, post-edit verification, subagent batching) are separate specs."

## Context

This is Phase 0 (damage control) of the doc-change → criteria → test update workflow. It fixes five confirmed defects. It does
not redesign the workflow.

- **P1: test-file writes lose frontmatter.** Every path that rewrites an existing test file (ingest-tests, ingest-update,
  ingest-grounding, orphan marking, batch write) drops fields the parser reads: `description`, `environment`,
  `related_work_items`, `custom`, `automated_by`, `requirements`, `bugs`. This defeats ingest-update's explicit carry-over of
  `automated_by` / `requirements` / `bugs` / `custom`. Losing `automated_by` also corrupts automation coverage in
  `spectra ai analyze --coverage`. The reverse gap was confirmed during scoping: the writer emits `status`, `orphaned_reason`
  and `orphaned_date`, but the parser never reads them back, so any later rewrite of an orphaned test silently un-orphans it.
- **P2: orphan marking rebuilds the test by hand.** It drops `criteria`, `scenario_from_doc`, `grounding`, `description`,
  `automated_by`, `requirements` and `bugs`, on top of what P1 loses.
- **P3: the update skill's "classify" step writes files.** `spectra ai update --suite <s> --diff --no-interaction` auto-applies,
  so each ORPHANED test is rewritten (losing data per P1/P2) and `_index.json` is regenerated. The skill's step is described
  only as classification. Confirmed during scoping: adding `--dry-run` to that command makes it exit after printing the diff
  and **before** the JSON result and `.spectra-result.json` are produced. The skill reads that file next, so a skill-only
  `--dry-run` fix would break the skill.
- **P4: `spectra ai update` cannot resolve default-format doc references.** The default generation profile writes
  `source_refs: ["docs/file.md#Section-Name"]`. The update command joins the ref onto the source directory (`docs/docs/...`),
  keeps the `#fragment`, and looks the raw ref up exactly. Such tests get no doc text and are classified ORPHANED ("Source
  document not found or empty"). Together with P3, one skill run can orphan most of a suite and strip its metadata. The critic
  prompt compiler already resolves refs correctly.
- **P5: the criteria skill reads each changed document twice.** The compiled extraction prompt already embeds the full
  document text, but the skill also tells the session to read the file with its file tools.

## Clarifications

### Session 2026-09-24

- Q: Content the typed model doesn't capture (unknown frontmatter keys, unknown body sections): preserve it or fail loud?
  → A: Preserve it verbatim.
  - Unknown keys are emitted after the known keys, in their original relative order.
  - Unknown body sections keep their original position relative to known sections where possible. Otherwise they are
    appended in original order. Content is byte-preserved.
  - ingest-update carries them over from the original.
- Q: Fix classification writes in the skill only, or also in the CLI? → A: Split `--no-interaction` from `--no-review` in
  `spectra ai update`.
  - `--no-interaction` alone classifies and reports only.
  - Applying without review requires the global `--no-review` flag.
  - `--dry-run` still emits the full JSON and `.spectra-result.json`.
  - Every skill call site passes `--dry-run` explicitly: `spectra-update` Step 1 and the delegation rows in
    `spectra-generate` and `spectra-execute`.
  - CHANGELOG carries a breaking-change note, and `docs/cli-reference.md` is updated.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rewriting a test never loses data (Priority: P1)

A QA engineer has tests enriched over time: `automated_by` links to automation code, `requirements` and `bugs` links, `custom`
metadata, a description, environments, criteria and a grounding block. They run any SPECTRA command that rewrites a test
(ingest an update, write back a grounding verdict, mark a test orphaned, batch write). Afterwards every field and value they
had is still in the file, and automation coverage reports are unchanged.

**Why this priority**: This is silent, cumulative data loss in the files of record. Every other workflow step writes through
these paths, so later phases cannot be trusted until this is fixed.

**Independent Test**: Take a test file with every supported frontmatter field and every body section populated. Run it
through each writer path and compare the re-parsed test with the original.

**Acceptance Scenarios**:

1. **Given** a test file with every supported frontmatter field populated, **When** it is rewritten by ingest-update, **Then**
   every field and value is preserved except the ones the edit intentionally changed.
2. **Given** the same file, **When** a grounding verdict is written back (ingest-grounding), **Then** only the `grounding`
   block changes.
3. **Given** the same file, **When** it is rewritten by the shared persistence path used by ingest-tests, **Then** parsing
   the result yields a test equal to the input.
4. **Given** any parsed test, **When** it is written twice in a row, **Then** the two outputs are byte-identical.
5. **Given** a test with `automated_by` set, **When** it is rewritten by any writer path, **Then** the automation coverage
   report still links that test to its automation.
6. **Given** a test already marked `status: orphaned`, **When** it is rewritten by any writer path, **Then** `status`,
   `orphaned_reason` and `orphaned_date` survive the rewrite.

---

### User Story 2 - Update classifies tests against the docs they actually reference (Priority: P1)

A QA engineer updates documentation and runs the update workflow on a suite. Its tests were generated with the default
profile, so they reference `docs/<file>.md#<Section>`. Each test is classified against the text of its referenced document.
Only tests whose referenced documents are really gone are reported as orphaned.

**Why this priority**: Today almost every default-profile test is misclassified ORPHANED. That makes the update workflow's
output useless and, combined with P3, destructive.

**Independent Test**: Build a suite whose tests use `docs/<file>.md#<Section>` refs to existing documents, run
`spectra ai update` on it, and count ORPHANED "source not found" classifications (expected: zero). Then delete one test's
only referenced document and re-run (expected: that test alone is ORPHANED).

**Acceptance Scenarios**:

1. **Given** a suite whose tests reference existing documents as `docs/<file>.md#<Section>`, **When** `spectra ai update`
   classifies it, **Then** zero tests are classified ORPHANED for "source not found".
2. **Given** a test whose only referenced document was deleted, **When** the suite is classified, **Then** that test is
   classified ORPHANED.
3. **Given** a test with two referenced documents where one exists and one was deleted, **When** the suite is classified,
   **Then** the test is **not** classified ORPHANED for a missing source.
4. **Given** equivalent refs written with backslashes, as rooted paths, as bare legacy names (`file.md`) and with
   `#fragments`, **When** they are resolved by `spectra ai update` and by the critic prompt compiler, **Then** both resolve
   each ref to the same file.
5. **Given** a ref to a missing document, **When** the critic prompt compiler runs, **Then** it still fails loud, as it does
   today.

---

### User Story 3 - Classifying a suite makes no changes (Priority: P1)

A QA engineer (or the `spectra-update` skill acting for them) runs the classification step to see which tests are
up-to-date, outdated, orphaned or redundant. The step reports the classification, including the human-readable tables, and
changes nothing on disk. Any change to test files happens only in a later, explicit step.

**Why this priority**: A step described as "classify" must not make unreviewed, lossy writes. This is the trigger that turns
P1, P2 and P4 into mass data loss.

**Independent Test**: On a clean working tree, run the skill's classification command for a suite that contains ORPHANED
tests. Confirm the working tree is still clean, the classification tables were printed, and the machine-readable result the
skill reads next was produced.

**Acceptance Scenarios**:

1. **Given** a clean working tree and a suite containing ORPHANED tests, **When** the skill's classification command runs,
   **Then** the working tree is unchanged: no test file and no `_index.json` is modified.
2. **Given** the same run, **When** it completes, **Then** the human-readable classification and diff tables were printed.
3. **Given** the same run, **When** it completes, **Then** the machine-readable result the skill reads next exists and
   contains the classification counts.
4. **Given** a suite containing ORPHANED tests, **When** `spectra ai update --suite <s> --no-interaction` runs without
   `--no-review`, **Then** nothing is written, the tables are printed, and the result is produced.
5. **Given** the same suite, **When** `spectra ai update --suite <s> --no-interaction --no-review` runs, **Then** the
   ORPHANED tests are marked (losslessly, per Story 4) and the index is regenerated.
6. **Given** the bundled `spectra-update`, `spectra-generate` and `spectra-execute` skills, **When** their `ai update`
   classification call sites are read, **Then** each one passes `--dry-run`.

---

### User Story 4 - Orphan marking changes only orphan fields (Priority: P2)

When a user applies an update and a test is marked orphaned, the test file keeps its title, steps, expected result, criteria,
grounding, links and all other metadata. Only `status`, `orphaned_reason` and `orphaned_date` change.

**Why this priority**: Orphaned tests are candidates for human review, not deletion. They must stay intact so the reviewer
can decide. This depends on Story 1 but is a separate defect in the orphan path.

**Independent Test**: Mark a fully populated test orphaned through the update command's apply path, then diff the file
before and after.

**Acceptance Scenarios**:

1. **Given** a fully populated test, **When** the update command's apply path marks it orphaned, **Then** the diff between
   the before and after files touches only `status`, `orphaned_reason` and `orphaned_date`.
2. **Given** a test already marked orphaned, **When** the apply path runs again and the test is still orphaned, **Then** the
   original `orphaned_date` is kept.

---

### User Story 5 - Criteria extraction reads each document once (Priority: P3)

A user runs the criteria workflow after changing documents. Each changed document's text enters the session once, through
the compiled extraction prompt. The session does not read the file again separately.

**Why this priority**: This is wasted context and cost, not a correctness problem. It is cheap to fix and belongs with this
cleanup.

**Independent Test**: Inspect the bundled `spectra-criteria` skill content. The extraction step names the compiled prompt as
the only source of document text and contains no instruction to read the document file.

**Acceptance Scenarios**:

1. **Given** the bundled criteria skill, **When** its in-session extraction step is read, **Then** it tells the session to
   use the compiled prompt as the only source of document text and does not tell it to read the document file.

---

### Edge Cases

- **Frontmatter keys the typed model does not recognise** (hand-added or from newer tools). Today they are silently dropped
  on rewrite. They are now preserved after the known keys in their original relative order (FR-004).
- **Body content outside the recognised sections** (for example a hand-added `## Notes` section). It is kept in its original
  position relative to the recognised sections. If that position can't be determined, it is appended in original order
  (FR-004).
- **ingest-update** returns an edited test that lacks the unknown content. The unknown content is carried over from the
  original (FR-004).
- **Non-interactive runs.** `--no-interaction` without `--no-review` only classifies and reports. `--no-review` with or
  without `--no-interaction` applies. `--dry-run` never writes, even with `--no-review`.
- **Nested `custom` metadata** (maps, lists, numbers, booleans, multi-line strings) must round-trip with the same values and
  types.
- **Strings needing YAML quoting or escaping** (colons, quotes, `#`, leading dashes, newlines) in any field must round-trip
  unchanged.
- **Empty collections vs. absent keys**: re-parsing must yield the same test either way. The fixed emission policy decides
  whether empty collections are written, and it must be deterministic.
- **The first rewrite of a hand-authored file** may normalise formatting (key order, quoting). Byte-identity is required from
  then on: writing the parsed result again is a no-op.
- **A ref that resolves outside the repository** (`../`), a ref with only a fragment (`#Section`), or an empty ref counts as
  missing. It is reported and never throws.
- **Refs differing only by separator or fragment** (`docs\a.md`, `docs/a.md#X`, `docs/a.md#Y`) resolve to one file and load
  it once.
- **A bare legacy ref** (`file.md`) that exists both at the repo root and under the configured source directory: resolution
  order is fixed (FR-011), so the result is deterministic.
- **Case sensitivity**: resolution follows the host file system. No case folding is added.

## Requirements *(mandatory)*

### Functional Requirements

**Lossless test-file round-trip (P1)**

- **FR-001**: Writing a parsed test MUST preserve every field the test parser reads: all typed frontmatter fields (`id`,
  `priority`, `tags`, `component`, `description`, `preconditions`, `environment`, `estimated_duration`, `depends_on`,
  `source_refs`, `scenario_from_doc`, `related_work_items`, `custom`, `grounding`, `automated_by`, `requirements`,
  `criteria`, `bugs`) and every recognised body section (title, preconditions, steps, expected result, test data).
- **FR-002**: The parser MUST read back the lifecycle fields the writer emits (`status`, `orphaned_reason`, `orphaned_date`),
  so parse → write → parse yields an equal test for orphaned tests too.
- **FR-003**: For any parsed test, parse → write → parse MUST yield a test equal to the original in every field.
  Frontmatter keys MUST be emitted in one fixed, documented order, so identical input always gives byte-identical output.
- **FR-004**: Content the typed model does not capture MUST be preserved verbatim by every writer path:
  - **Unknown top-level frontmatter keys** are emitted after all known keys, in their original relative order, with their
    values unchanged.
  - **Unknown body sections** (content outside the recognised sections) are kept in their original position relative to
    the recognised sections where that position can be determined. Otherwise they are appended after the recognised
    sections in their original order. Their content is byte-preserved.
  - **ingest-update** carries unknown keys and unknown body sections over from the original test, because the edited test
    returned by the session does not contain them.
- **FR-004a**: Round-trip equality (FR-003) and byte-identity include the preserved unknown content. Parse → write → parse
  MUST yield the same unknown keys, values, sections and positions.
- **FR-005**: Every path that rewrites test files MUST produce its output through the single lossless serialisation:
  ingest-tests and ingest-update (via the shared persistence service), ingest-grounding write-back, the update command's
  apply path (updates and orphan marking), batch write, and the review-flagged accept path (found during planning).

**Orphan marking (P2)**

- **FR-006**: Marking a test orphaned MUST start from the original parsed test and change only `status`, `orphaned_reason`
  and `orphaned_date`. It MUST NOT rebuild the test field by field.
- **FR-007**: Re-marking a test that is already orphaned MUST keep its existing `orphaned_date`.

**Classification without writes (P3)**

- **FR-008**: Running the `spectra-update` skill's classification step MUST NOT modify any test file or any `_index.json`.
- **FR-009**: In `spectra ai update`, `--no-interaction` and `--no-review` MUST mean different things:
  - `--no-interaction` alone MUST classify and report only. It MUST NOT write any test file or `_index.json`.
  - Applying changes without interactive review MUST require the existing global `--no-review` flag (Constitution IV),
    with or without `--no-interaction`.
  - An interactive run without either flag keeps today's review-then-apply behaviour.
  - This is a **breaking change**: CI that relies on `--no-interaction` to apply MUST add `--no-review`.
- **FR-009a**: `--dry-run` MUST still produce the full machine-readable result (JSON output and `.spectra-result.json`) with
  the classification counts. It MUST NOT exit early after the diff. It writes nothing else.
- **FR-009b**: Every classify-only run (dry run or `--no-interaction` without `--no-review`) MUST still print the
  human-readable classification and diff tables in every output mode. The tables MUST NOT be removed or suppressed.
- **FR-009c**: Every bundled skill call site that runs `spectra ai update` for classification MUST pass `--dry-run`
  explicitly:
  - `spectra-update` skill, Step 1
  - the "Update test cases" delegation row in the `spectra-generate` skill
  - the "Update test cases" delegation row in the `spectra-execute` skill
- **FR-010**: A classify-only run MUST exit with the same deterministic exit code as an applying run with the same
  classification outcome.

**Doc-reference resolution (P4)**

- **FR-011**: SPECTRA MUST have one model-free, deterministic resolver that turns a test's `source_refs` into document files.
  It MUST:
  - strip any `#fragment`
  - normalise path separators
  - resolve the ref relative to the repository root first, then fall back to the configured source directory for legacy
    bare refs such as `file.md`
  - treat refs that escape the repository, or are empty after stripping, as missing
  - return the resolved and missing refs separately, without throwing
- **FR-012**: `spectra ai update` classification MUST use this resolver, so a test's referenced document text is found
  whenever the referenced file exists on disk.
- **FR-013**: A test MUST be classified ORPHANED for a missing source only when none of its referenced files exist after
  resolution. Tests with at least one resolvable ref are classified against the resolved documents' text.
- **FR-014**: The critic prompt compiler MUST use the same resolver. It MUST keep failing loud when any referenced document
  is missing, with its current exit behaviour.
- **FR-015**: Every other consumer that turns `source_refs` into file paths MUST be audited. This includes documentation
  coverage analysis, gap analysis, the coverage snapshot builder and the compile-repair commands. Consumers that turn refs
  into paths MUST adopt the resolver. Consumers deliberately left alone (for example ones that compare refs as identifiers
  and never open files) MUST be listed with the reason in the plan.

**Criteria skill duplicate read (P5)**

- **FR-016**: The bundled `spectra-criteria` skill's in-session extraction step MUST NOT tell the session to read the
  document file separately. The compiled extraction prompt MUST be named as the only source of document text. Skill-content
  tests that assert on this text MUST be updated to match.

**Documentation and change record**

- **FR-017**: `CHANGELOG.md` MUST record:
  - the fixes
  - that fields already lost in existing workspaces are not recovered, with Git history as the recovery path
  - a **breaking-change** note: CI that relies on `spectra ai update --no-interaction` to apply changes must add
    `--no-review`

  `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` and `docs/usage.md` MUST be updated wherever user-visible behaviour
  changes: at minimum the `--no-interaction` / `--no-review` split, dry-run output, and ref resolution.

### Key Entities

- **Test case file**: a Markdown file with YAML frontmatter, and the file of record for a test. Holds typed metadata
  (identity, priority, links, criteria, grounding, lifecycle status) and body sections (title, preconditions, steps,
  expected result, test data).
- **Source reference**: a string in a test's `source_refs` that points to a documentation file, optionally with a
  `#section` fragment. It may use either separator and may be repo-relative, rooted, or a bare legacy name.
- **Resolution result**: for one test, the list of refs that resolved to existing files (with the resolved path) and the
  list of refs that are missing. It drives classification and fail-loud decisions.
- **Classification result**: the per-test classification (up-to-date, outdated, orphaned, redundant) with reasons and
  counts. It is printed as tables and emitted as the machine-readable result.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a test with every supported field populated, 100% of fields and values survive each of the five writer
  paths (ingest-tests, ingest-update, ingest-grounding, orphan marking, batch write). Intentional edits are excluded.
- **SC-002**: Writing any parsed test twice produces byte-identical files in 100% of cases.
- **SC-003**: On a suite whose tests use default-profile refs to existing documents, 0 tests are classified ORPHANED for
  "source not found" (today: effectively all of them).
- **SC-004**: A test whose only referenced document was deleted is classified ORPHANED in 100% of runs.
- **SC-005**: After the skill's classification step, `git status` shows 0 modified files, the classification tables are
  printed, and the machine-readable result is present.
- **SC-005a**: `spectra ai update --no-interaction` without `--no-review` modifies 0 files. Adding `--no-review` restores
  applying.
- **SC-005b**: A test file with unknown frontmatter keys and unknown body sections keeps 100% of that content, byte for
  byte, through every writer path.
- **SC-006**: Marking a test orphaned changes exactly 3 frontmatter fields (`status`, `orphaned_reason`, `orphaned_date`)
  and nothing else.
- **SC-007**: The same set of refs (backslash, rooted, bare legacy, fragment variants) resolves to identical files in
  `spectra ai update` and in the critic prompt compiler.
- **SC-008**: During criteria extraction, each changed document's text enters the session once, not twice.
- **SC-009**: All existing Core, CLI and Execution test suites stay green. New coverage includes: writer round-trip over
  every field, resolver unit tests, an update-command orphan-marking test, and a default-profile ref regression test.

## Assumptions

- "Equal test" in round-trip checks compares every typed field of the test model, including lifecycle fields (FR-002). It
  excludes the file path, which is derived from location.
- The fixed key order is chosen during planning and documented. Existing files are not migrated eagerly: they are
  normalised the next time a writer path touches them.
- "Repository root" means the workspace root SPECTRA already uses to resolve `spectra.config.json`. "Configured source
  directory" means the documentation source directory from that config.
- Classifying against a resolved document uses the whole file. Section-aware classification that uses the `#fragment` is
  Phase 3 work.
- The apply decision in `spectra ai update` depends only on the explicit `--no-review` / `--no-interaction` flags and the
  interactive reviewer. Auto-detected non-interactive terminals (redirected input or output) keep today's review
  behaviour. This phase does not add an apply path for them.
- `--no-review` is the existing global option. No new flag is introduced.
- Changing what the machine-readable result lists (for example OUTDATED test ids) is Phase 2 work. This phase only ensures
  the existing result is still produced when nothing is written.

## Out of Scope

- Criteria ID stability, the changeset and deleted-doc handling (Phase 1).
- Listing OUTDATED ids in `.spectra-result.json`, change-driven test selection, and criteria-aware labels in the human
  tables (Phase 2).
- Edit-prompt content: doc sections and linked criteria only (Phase 3).
- Resetting grounding after edits, `ingest-update --all` exit codes and staging cleanup, and routing orphan deletion
  through `spectra delete` (Phase 4).
- Subagent batching (Phase 5).
- Recovering fields already lost in existing workspaces. Git is the undo path.

## Constraints

- Model-free and deterministic (Constitution III). CLI-first with deterministic exit codes (Constitution IV).
- No compatibility shims and no new abstractions beyond the shared doc-reference resolver (Constitution V).
