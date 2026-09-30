# Feature Specification: Stable Acceptance-Criteria Identity (Update Workflow Phase 1)

**Feature Branch**: `079-stable-criteria-ids`
**Created**: 2026-09-28
**Status**: Draft
**Input**: User description: "Phase 1 of the doc-change → criteria → test update workflow: stable acceptance-criteria identity. Phase 0 (spec 078: lossless test writes, classify-only `ai update`, SourceRefResolver) is done. This phase makes criterion IDs trustworthy so later phases can detect which criteria changed and which tests are affected. It does NOT select or edit tests; that is Phase 2 and later."

## Context

Criteria are refreshed per changed doc through a model-free seam:

```text
spectra docs changed → spectra ai compile-extraction-prompt → in-session extraction → spectra ai ingest-criteria
```

Change detection works; criterion identity does not. The problems, all confirmed against the code:

1. **Extraction starts from scratch.** The prompt compiler always passes an empty `existing_criteria`, although the
   template declares it "for incremental updates". The template body never uses it, and its response schema has no `id`
   field.
2. **IDs survive only an exact (case-insensitive) text match.** Extraction rewrites text into RFC 2119 form, so any
   rewording gives a new ID and the old one disappears. Tests whose `criteria:` list the old ID silently lose their link,
   and nothing reports which criteria were added, changed or removed. The doc's criteria file is simply overwritten.
3. **IDs can be reused and duplicated.**
   - A new ID is `AC-{COMPONENT}-{max+1}`, where `max` comes only from that doc's current file. If the highest-numbered
     criteria are dropped, their numbers are handed out again later, possibly to unrelated criteria.
   - Two docs with the same component (including same-named docs in different folders, since the component defaults to
     the file's base name) number independently and produce duplicate IDs.
   - A response that repeats an existing criterion's text gives both copies the same ID.
   - The update classifier builds a lookup keyed by ID and crashes on a duplicate. Coverage and the dashboard count
     duplicates twice.
   - `spectra ai analyze --import-criteria` uses a different numbering scheme against the same ID space.
4. **Criteria file names come from the doc's base name only.** `docs/a/login.md` and `docs/b/login.md` both write
   `login.criteria.yaml` and overwrite each other.
5. **Deleted docs are never handled.** `docs changed` only walks docs that exist now, so a deleted doc's index entry and
   criteria file stay forever and keep counting in coverage. A moved or renamed doc looks like a new doc, and its old
   entry lingers.
6. **`source_doc` is stored exactly as typed in `--doc`.** A backslash or differently rooted path never matches the
   forward-slash path `docs changed` uses, so the doc keeps showing as `new`.
7. **The criteria index read-modify-write has no lock.** Concurrent writers also share a fixed temp-file name.

Found while scoping:

- **`source_section` is very likely dropped when the model's response is parsed.** The field has no explicit JSON name
  mapping, and the parser matches names case-insensitively only, which does not map underscores. Both the
  existing-criteria prompt and the changeset's metadata diff depend on this field.
- **Criteria files are not byte-stable.** Every write stamps a `Generated at` timestamp in the header, so "unchanged"
  can't currently mean byte-identical.
- **An unreadable existing criteria file is silently treated as empty.** That would restart numbering and turn every
  criterion into "added".
- **The test-ID allocator this spec borrows its pattern from keeps its high-water mark in a gitignored, single-number
  file.** This phase needs a committed high-water mark per ID prefix, so the pattern is reused for locking but not for
  storage.

## Clarifications

### Session 2026-09-28

- Q: How is the storage migration triggered? → A: An explicit migrate command with `--dry-run` and a printed report.
  - Criteria ingest, retire, apply-move and criteria import run it automatically before their first write when they
    detect the old layout.
  - `docs changed` and `compile-extraction-prompt` never move files; they only warn.
  - Until migration, every read path, including `compile-extraction-prompt`'s `existing_criteria`, locates a doc's
    criteria through the index `file` entry. Reads are correct in both layouts, and the first post-upgrade extraction still
    reuses existing IDs.
- Q: Where does the human changeset summary go in JSON mode? → A: On success, it goes to standard error in JSON mode and to
  standard output in human mode. The success JSON stays the whole of standard output. Failure output is unchanged:
  machine-readable, with no human summary mixed in.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A reworded criterion keeps its ID (Priority: P1)

A QA engineer edits a doc, for example rewording a rule or tightening a limit, and runs the criteria workflow. Criteria
that still express the same requirement keep their IDs, even if the wording changed. Tests that link to those IDs stay
linked. The engineer sees which criteria were added, modified (old → new text) and removed.

**Why this priority**: Without stable IDs, every doc edit silently breaks test ↔ criterion links, and later phases can't
tell what changed. Everything else in this phase depends on it.

**Independent Test**:
1. Extract criteria for a doc.
2. Reword one requirement in the doc, add one requirement and delete another.
3. Re-extract.
4. Check that the reworded criterion kept its ID and is reported as modified with old and new text, the new one got a
   fresh ID, and the deleted one is reported as removed.

**Acceptance Scenarios**:

1. **Given** a doc with extracted criteria, **When** a criterion is reworded in the doc and re-extracted, **Then** it keeps
   its ID and is reported as modified with its old and new text.
2. **Given** the same doc, **When** a criterion is reworded only in markup or whitespace (e.g. `**must**` → `must`, extra
   spaces) and re-extracted, **Then** no change is reported and the criteria file is byte-identical.
3. **Given** a re-extraction whose response gives an existing criterion's ID, **When** that ID belongs to this doc and
   appears once, **Then** it is kept.
4. **Given** a response that uses an ID from another doc, an unknown ID, or the same ID twice, **When** it is ingested,
   **Then** the ingest fails loud with a distinct error code and nothing is persisted.
5. **Given** a response without IDs, **When** an extracted criterion's normalised text equals an existing criterion's
   normalised text, **Then** it keeps that ID. Each existing ID can be matched at most once.
6. **Given** the compiled extraction prompt for a doc that already has criteria, **When** it is compiled twice from
   identical inputs, **Then** both prompts are byte-identical and list only that doc's existing criteria.

---

### User Story 2 - IDs are unique and never reused (Priority: P1)

Every criterion ID in the repository refers to exactly one requirement, forever. Removing a criterion never frees its
number for something else, and two docs, or extraction and import, can never hand out the same ID.

**Why this priority**: A reused or duplicated ID silently re-links tests to the wrong requirement, and it crashes the
update classifier. This is as critical as stability.

**Independent Test**: Drop the highest-numbered criterion of a component, then add a new one; the new number is above
every number ever issued for that prefix. Separately, run extraction for two docs with the same component and an import;
all IDs are distinct.

**Acceptance Scenarios**:

1. **Given** a component whose highest-issued criterion was removed, **When** a new criterion is added, **Then** its number
   is above every number ever issued for that prefix.
2. **Given** two docs with the same component, **When** both are extracted, **Then** no ID appears in both.
3. **Given** an import and an extraction that target the same ID prefix, **When** both run, **Then** they never produce the
   same ID.
4. **Given** two ingests running at the same time, **When** both finish, **Then** neither index update is lost and no ID is
   issued twice.
5. **Given** a fresh clone of the repository, **When** a new criterion is added, **Then** its number is still above every
   number ever issued. The high-water mark is committed, not local.
6. **Given** a workspace that already contains duplicate IDs (from earlier versions), **When** criteria are ingested or
   `docs changed` runs, **Then** each duplicate is reported with the files involved and nothing is renumbered. `spectra ai
   update` doesn't crash on it; it reports it.

---

### User Story 3 - Every ingest shows what changed (Priority: P1)

After each doc's criteria are refreshed, the engineer sees a changeset:
- added IDs
- modified IDs, with old and new text
- removed IDs, with old text
- metadata changes on kept IDs (priority, RFC 2119 keyword, source section, tags)

For each removed ID, it also shows how many tests still reference it. The same changeset is available in machine-readable
form for the skill and for later phases.

**Why this priority**: The changeset is the hand-off to Phase 2 (which tests are affected). It's also the user's only way
to notice that a criterion their tests depend on has gone.

**Independent Test**: Ingest a response that adds, modifies, removes and re-tags criteria for a doc whose removed
criterion is referenced by two tests. Check the machine-readable result and the printed summary. Then re-ingest the same
response and check that the changeset is empty.

**Acceptance Scenarios**:

1. **Given** a response that adds, rewords, drops and re-prioritises criteria, **When** it is ingested, **Then** the result
   lists added, modified (old and new text), removed (old text) and metadata changes (by field).
2. **Given** a removed criterion that two tests list in `criteria:`, **When** the ingest completes, **Then** the result
   says 2 tests still reference it.
3. **Given** any output mode, including JSON, **When** the ingest succeeds, **Then** a human-readable changeset summary is
   printed. In JSON mode it goes to standard error and standard output is exactly the success JSON. In human mode it goes
   to standard output.
4. **Given** a re-extraction that returns the same criteria, **When** it is ingested, **Then** the changeset is empty.
5. **Given** a `--dry-run` ingest, **When** it completes, **Then** the changeset is reported and nothing is written.

---

### User Story 4 - Each doc's criteria live in their own file (Priority: P2)

Two docs with the same file name in different folders keep separate criteria files. Existing workspaces are moved to the
new layout once, without losing any ID. `source_doc` is recorded the same way however the path was typed, so a doc that
has been ingested shows as unchanged afterwards.

**Why this priority**: Collisions silently destroy one doc's criteria. The path fix stops docs being stuck as `new`.

**Independent Test**:
- Extract `docs/a/login.md` and `docs/b/login.md`; both keep their own criteria.
- Run the migration twice on an existing workspace; every ID is preserved and the second run changes nothing.
- Ingest with `--doc docs\x.md`; `docs changed` then reports the doc as unchanged.

**Acceptance Scenarios**:

1. **Given** `docs/a/login.md` and `docs/b/login.md`, **When** both are extracted, **Then** each has its own criteria file
   and neither overwrites the other.
2. **Given** an existing workspace with base-name criteria files, **When** the migration runs, **Then** every ID is
   preserved, the index points at the new locations, and `source_doc` entries are normalised.
3. **Given** a migrated workspace, **When** the migration runs again, **Then** nothing changes.
4. **Given** a workspace still in the old layout, **When** `docs changed` or `compile-extraction-prompt` runs, **Then** no
   file moves, a warning is printed, and the prompt's existing criteria are read through the index `file` entry, so the
   next extraction keeps existing IDs.
5. **Given** a workspace still in the old layout, **When** criteria ingest, retire, apply-move or import runs, **Then** the
   migration runs first, is reported in the result, and the operation then writes in the new layout.
6. **Given** an existing index where two docs already share one criteria file (a past collision), **When** the migration
   runs, **Then** it reports both docs and recommends re-extracting them. It doesn't guess which criteria belong to which
   doc.
7. **Given** an ingest with `--doc docs\x.md` (or `./docs/x.md`), **When** `docs changed` runs next, **Then** it reports
   the doc as unchanged.

---

### User Story 5 - Deleted and moved docs are handled (Priority: P2)

When a doc is deleted, `docs changed` reports it as deleted instead of ignoring it forever. The engineer can retire it
explicitly: its index entry and criteria file are removed, and its criteria are reported as removed along with the tests
that still reference them. A doc moved without changes keeps its criteria and IDs, with no model call.

**Why this priority**: Stale entries inflate coverage and hide lost requirements. The fix is an explicit, reviewable step
rather than automatic deletion.

**Independent Test**:
- Delete a doc; `docs changed` reports it deleted. A retire dry run lists its criteria and their referencing tests. The
  real retire removes the entry and file.
- Move an unchanged doc; `docs changed` reports it moved. Applying the move keeps every ID and extracts nothing.

**Acceptance Scenarios**:

1. **Given** an indexed doc that was deleted, **When** `docs changed` runs, **Then** it is reported with status `deleted`.
2. **Given** a deleted doc, **When** retire runs with `--dry-run`, **Then** it reports the changeset (every criterion
   removed, with referencing-test counts) and writes nothing.
3. **Given** a deleted doc, **When** retire runs, **Then** its index entry and criteria file are removed and the changeset
   is reported. Nothing is retired without this explicit call.
4. **Given** a doc moved to a new path with identical content, **When** `docs changed` runs, **Then** it is reported as
   `moved` (from → to), not as one deleted and one new doc.
5. **Given** a reported move, **When** it is applied, **Then** the criteria keep their IDs, `source_doc` and the criteria
   file location follow the new path, and no extraction is needed.
6. **Given** the criteria skill, **When** `docs changed` reports moved and deleted docs, **Then** the skill applies the
   moves, shows each deleted doc's retire dry run, and retires only after the user confirms. It ends with a summary of all
   changesets.

---

### Edge Cases

- **A response returns an existing ID with completely different text** (the model mislabelled it). The ID belongs to the
  doc and appears once, so it is honoured and reported as modified with old and new text for the user to see. No semantic
  second-guessing in the CLI.
- **Two extracted criteria have the same normalised text.** Only the first can match the existing ID by text; the second
  gets a new ID.
- **An existing criteria file can't be read** (corrupt YAML). The ingest fails loud instead of treating the doc as having
  no criteria. Git is the recovery path.
- **An ID was issued on another branch.** The committed high-water mark lives in a tracked file, so concurrent branches
  that both issue IDs conflict when merged. This is visible, not silent. Any duplicates that still slip through are
  reported by the duplicate check (US2 scenario 6).
- **The high-water mark is missing or lower than IDs present on disk** (hand edits, pre-existing workspaces). The effective
  mark is the maximum of the stored mark and every ID found in the repository's criteria files. It is never lower.
- **A moved doc's derived component differs from its old one** (because the file name changed). Existing IDs keep their
  original prefix; IDs are opaque once issued. New criteria in the moved doc use the current component's prefix.
- **Several deleted sources share a content hash with one new doc** (duplicated docs). Ambiguous: report it as new plus
  deleted and don't guess a move.
- **Imported criteria** (source type other than `document`, no source doc on disk) are never reported as deleted and are
  never moved or retired by these operations.
- **A doc that was never ingested successfully** (e.g. empty, so it short-circuits) stays `new`. This is unchanged
  behaviour and out of scope.
- **A retire targets a doc that still exists.** Refused with a distinct error; retire applies only to deleted docs.
- **A ref to a moved or retired criterion ID from a test.** Tests are not edited in this phase. The counts are reported so
  Phase 2 can act on them.

## Requirements *(mandatory)*

### Functional Requirements

**Existing criteria in the extraction prompt (FR1)**

- **FR-001**: Compiling an extraction prompt MUST fill `existing_criteria` with the current criteria of that doc only:
  each criterion's ID, text and source section, in a stable order.
- **FR-002**: The extraction template MUST tell the model to return an existing criterion's `id` when an extracted
  criterion expresses the same requirement, and to omit `id` otherwise. The response schema MUST allow an optional `id`.
- **FR-003**: Identical inputs (doc text, existing criteria, template) MUST produce byte-identical prompts.

**ID validation at ingest (FR2)**

- **FR-004**: Ingest MUST accept an optional `id` per criterion. An `id` is honoured only when it belongs to this doc's
  existing criteria and appears at most once in the response. Otherwise ingest MUST fail loud with a distinct,
  deterministic error code, persist nothing, and name the offending IDs.
- **FR-005**: A criterion without an `id` MUST keep an existing ID when its normalised text (FR-013) equals that existing
  criterion's normalised text. Each existing ID can be matched at most once, and explicitly returned IDs take precedence.
  Any criterion still unmatched gets a new ID (FR-007).
- **FR-006**: The `source_section` (and every other field the response schema defines) MUST be parsed from the model's
  response, not silently dropped.

**Never-reused, repository-unique IDs (FR3)**

- **FR-007**: New criterion IDs MUST come from a persisted high-water mark per ID prefix, committed with the criteria index,
  never from the current file's maximum. The effective mark MUST be the maximum of the stored mark and every ID present in
  the repository's criteria files. An issued number MUST never be issued again for that prefix, even after its criterion
  is removed.
- **FR-008**: Both criteria ingest and criteria import MUST allocate IDs from this one allocator. Import MUST NOT consume a
  number for a criterion that keeps an existing ID.
- **FR-009**: Allocation and the criteria-index update MUST run under one cross-process lock, so concurrent writers never
  lose an index update or issue the same ID. Temporary files MUST be unique per writer.
- **FR-010**: Existing duplicate criterion IDs in a workspace MUST be detected and reported with the files involved, by
  criteria ingest and by `docs changed`, and never silently renumbered.
- **FR-011**: `spectra ai update` MUST NOT crash when the criteria set contains duplicate IDs. It uses the first definition
  and reports the duplicate.

**Changeset per ingest (FR4)**

- **FR-012**: Before persisting, ingest MUST compute the doc's changeset against its previous criteria:
  - added: new IDs
  - modified: same ID, different normalised text, with old and new text
  - removed: previous IDs no longer present, with old text
  - metadata changes on kept IDs (priority, RFC 2119 keyword, source section, tags), listed by field with old and new
    values

  For each removed ID, the result MUST also count the tests whose `criteria:` still reference it, taken from the test
  indexes.
- **FR-012a**: The changeset MUST be in the machine-readable ingest result, and a human-readable summary MUST also be
  printed on success in every output mode, including JSON. It is never suppressed. In JSON mode the summary goes to
  standard error and the success JSON remains the whole of standard output. In human mode the summary goes to standard
  output.
- **FR-012c**: Failure output is unchanged: it stays machine-readable, with no human summary mixed in.
- **FR-012b**: A re-extraction that yields the same criteria MUST report an empty changeset. A dry-run ingest MUST report
  the changeset and write nothing.

**Normalised text (FR5)**

- **FR-013**: One deterministic, culture-invariant text normaliser MUST be shared across the product. It strips HTML tags
  and entities, strips Markdown emphasis, inline-code and link syntax (keeping link text), collapses whitespace and trims.
  It is used for criterion matching and "modified" detection, and later phases reuse it for section hashing.
- **FR-014**: When a kept criterion's normalised text is unchanged, its stored text MUST be kept verbatim. When a doc's
  criteria and their metadata are all unchanged, its criteria file MUST NOT be rewritten, so it stays byte-identical.

**Per-doc unique storage (FR6)**

- **FR-015**: A doc's criteria file location MUST be derived from its full repository-relative path, so no two docs can map
  to the same file.
- **FR-016**: A one-time, deterministic, idempotent migration MUST:
  - move existing base-name criteria files to the new locations, preserving every ID
  - update the index's `file` entries
  - normalise `source_doc` (FR-017)
  - seed the committed high-water marks from the IDs present

  It MUST report index entries that already share one file (a past collision) with a recommendation to re-extract both
  docs, and leave them untouched rather than guess.
- **FR-016a**: The migration MUST be available as an explicit command with `--dry-run` and a printed report.
- **FR-016b**: The write operations MUST run the migration automatically before their first write when they detect the
  old layout. These are criteria ingest, retire, apply-move and criteria import. Their result MUST say that a migration
  ran, and what it did.
- **FR-016c**: `docs changed` and `compile-extraction-prompt` MUST never move files. When they detect the old layout, they
  only warn.
- **FR-016d**: Until a workspace is migrated, every read path MUST locate a doc's criteria through the criteria index's
  `file` entry for that doc, never by deriving a file name. This includes `compile-extraction-prompt`'s
  `existing_criteria`. Reads are therefore correct in both layouts, and the first extraction after upgrading still reuses
  existing IDs.

**Normalised `source_doc` (FR7)**

- **FR-017**: `source_doc` MUST be stored as a repository-relative path with forward slashes, however `--doc` was typed
  (backslashes, `./`, absolute paths inside the repository). `docs changed` MUST compare normalised paths.

**Deleted and moved docs (FR8)**

- **FR-018**: `docs changed` MUST also report document sources in the index whose file no longer exists, with status
  `deleted`. Imported (non-document) sources are excluded.
- **FR-019**: When exactly one `new` doc's content hash equals exactly one `deleted` source's recorded hash, `docs changed`
  MUST report the pair as one `moved` entry (from → to) instead of a new doc and a deleted one. `docs changed` itself stays
  read-only.
- **FR-020**: A model-free operation MUST apply a reported move. It keeps every criterion and ID, re-points `source_doc`
  and the criteria file location, and runs no extraction. It supports `--dry-run`.
- **FR-021**: A model-free operation MUST retire a deleted doc. It removes the doc's index entry and criteria file, and
  reports every criterion as removed (the FR-012 changeset, including referencing-test counts). It supports `--dry-run`,
  refuses docs that still exist, and nothing is retired without an explicit call.

**Skill (FR9)**

- **FR-022**: The `spectra-criteria` skill MUST:
  - apply `moved` entries from `docs changed`
  - for each `deleted` entry, show the retire dry run and retire only after the user explicitly confirms
  - end with a summary of every changeset (added / modified / removed per doc, plus tests still referencing removed IDs)

  Skill-content tests MUST assert this.

**Docs and change record**

- **FR-023**: `CHANGELOG.md`, `CLAUDE.md` "Recent Changes", `docs/cli-reference.md` and the criteria/coverage docs MUST
  describe the new result fields, the error code, the move and retire operations, the migration, and the new storage
  layout.

### Key Entities

- **Acceptance criterion**: one testable requirement extracted from a doc, identified by an ID that is stable across
  rewordings and never reused. It has text, an RFC 2119 keyword, a source section, a priority, tags and a component.
- **Criteria file**: the stored criteria of exactly one doc, at a location derived from that doc's repository-relative path.
- **Criteria index**: the committed registry of criteria sources. Each entry holds the source doc (normalised path), the
  criteria file, the content hash at last extraction, a count and an outcome. The index also carries the per-prefix
  high-water marks.
- **ID high-water mark**: per ID prefix, the highest number ever issued. It only increases.
- **Criteria changeset**: for one doc and one ingest, the added, modified, removed and metadata-changed criteria, plus
  referencing-test counts for removed IDs.
- **Doc change entry**: one row of `docs changed`, with status new, changed, unchanged, deleted or moved (with a from-path).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a re-extraction after rewording criteria in a doc, 100% of criteria that still express the same
  requirement (per the model's returned IDs, or equal normalised text) keep their ID.
- **SC-002**: Across any sequence of ingests, imports, removals and fresh clones, 0 IDs are issued twice, and 0 duplicate
  IDs are created in the repository.
- **SC-003**: Every ingest reports a changeset. A no-op re-extraction reports 0 added, 0 modified, 0 removed, and leaves
  the criteria file byte-identical.
- **SC-004**: For every removed criterion, the reported count of tests still referencing it equals the number of tests
  whose `criteria:` list it.
- **SC-005**: Two docs with the same file name in different folders keep 100% of their own criteria after both are
  extracted.
- **SC-006**: The migration preserves 100% of existing IDs, and a second run changes 0 files.
- **SC-007**: A deleted doc is reported as deleted on the first `docs changed` after deletion. After retirement it no longer
  appears in the index or in coverage totals.
- **SC-008**: An unchanged moved doc keeps 100% of its criteria IDs, and applying the move triggers no extraction.
- **SC-009**: After ingesting with any spelling of the doc path, the next `docs changed` reports the doc as unchanged.
- **SC-010**: Two concurrent ingests lose 0 index updates.
- **SC-011**: Existing Core, CLI, Execution and Integration suites stay green, apart from the known pre-existing
  `ProfileFormatLoaderTests` failure. New tests cover: the allocator (no reuse, uniqueness, concurrency), ID-reuse
  validation, the normaliser, the changeset, the migration (idempotence, collisions reported), deleted/moved detection,
  retire, and skill content.

## Assumptions

- **The ID prefix is unchanged**: `AC-{COMPONENT}` with the component as today (`--component`, else derived from the doc's
  base name). Once issued, an ID is opaque: it never changes when the doc moves or the component changes.
- **The committed high-water mark is stored with the criteria index**, which is already committed. Where exactly is a plan
  decision.
- **"Same requirement" is decided by the model** (a returned ID) or by equal normalised text. The CLI does no fuzzy or
  semantic matching.
- **Move detection uses the content hash `docs changed` already records.** A move with content changes is reported as new
  plus deleted (Phase 2b adds git rename detection).
- **Retire and apply-move are explicit CLI operations.** `docs changed` never writes.
- **Test files and `_index.json` are never modified in this phase.** Referencing-test counts are read-only.
- **The criteria skill still writes one shared staging file per doc.** Parallel extraction is Phase 5.

## Out of Scope

- Selecting or editing tests from the changeset, a source-section baseline, and update triage (Phase 2a).
- Git-based rename detection, and moves with content changes (Phase 2b).
- An `ingest-criteria --all` batch mode and subagent orchestration (Phase 5).
- Changing how components are derived.
- Stale `docs index` flags and help text (Phase 6).
- Making docs that were never ingested (e.g. empty docs) stop showing as `new`.

## Constraints

- Model-free and deterministic CLI (Constitution III).
- CLI-first, with deterministic exit codes and `--dry-run` for batch and destructive operations (Constitution IV).
- No compatibility shims beyond the one-time migration (Constitution V).
