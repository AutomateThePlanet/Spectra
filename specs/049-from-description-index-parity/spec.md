# Feature Specification: From-Description Write & Index Parity

**Feature Branch**: `049-from-description-index-parity`
**Created**: 2026-06-02
**Status**: Draft
**Input**: User description: "Spec 049 — Wire the from-description generation flow through the same index-update path the batch flow uses, and centralize test write + index update into a single orchestration helper so no generation path can write a test file without registering it in `_index.json`. Backfill already-written, unindexed tests via `spectra index --rebuild`."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - From-description tests are immediately discoverable and runnable (Priority: P1)

A QA engineer adds a one-off test via `spectra ai generate --suite checkout --from-description "verify guest checkout shows shipping estimate"`. As soon as the command completes, that test is visible to every discovery and execution surface the team uses: it appears in suite listings, it shows up in `find_test_cases` filter results, it is included when an execution run is started for the suite, and it is counted by saved-selection summaries. The user does not need to run any additional indexing or refresh step.

**Why this priority**: This is the core bug. Today, a test created via `--from-description` is silently invisible to every downstream consumer because each consumer reads only from the suite index file, which the from-description flow never updates. Without this fix the `--from-description` feature is effectively non-functional for any user who intends to actually execute the test they just created.

**Independent Test**: Run `spectra ai generate --suite <suite> --from-description "..."` against a fresh or existing suite, then invoke MCP `find_test_cases` (and/or `list_available_suites`) and confirm the new test is returned. Also start an execution run for the suite and confirm the new test is enqueued.

**Acceptance Scenarios**:

1. **Given** an existing suite with N indexed tests, **When** the user creates one more test via `--from-description`, **Then** the suite's index file contains N+1 entries and the new entry exposes the test's id, title, priority (lowercase), tags, and component identical to the values written into the test file's frontmatter.
2. **Given** the user has just created a `priority: high` test via `--from-description`, **When** they query for tests with `priorities: ["high"]`, **Then** the new test appears in the result set.
3. **Given** the user has just created a `priority: high` test via `--from-description`, **When** the `smoke` saved selection (filter `priorities: ["high"]`) is evaluated, **Then** the new test is counted as a member of that selection.
4. **Given** a `--from-description` command for an existing suite, **When** generation succeeds, **Then** none of the previously-indexed tests in that suite are removed, renamed, or have their existing attributes changed.
5. **Given** a `--from-description` command is re-run and the generator emits a test with the same id as one already on disk, **When** the command completes, **Then** the index contains exactly one entry for that id (not duplicated).

---

### User Story 2 - Existing unindexed tests can be recovered without regeneration (Priority: P2)

A team has been using `--from-description` for weeks before discovering the missing-from-index bug. They have dozens of valid test files on disk that are invisible to discovery. They run `spectra index --rebuild` once and every existing test file becomes discoverable — no AI re-generation, no manual editing, no data loss.

**Why this priority**: Without a backfill path, every team that hit the bug must either re-generate their from-description tests (paying for AI calls and losing curated content) or hand-edit the index. The rebuild command turns recovery into a single, safe operation. It is P2 (not P1) because new tests created after the P1 fix do not need it — it is purely for legacy data.

**Independent Test**: Place a hand-written test `.md` (or simulate a pre-fix from-description test) in a suite directory with no corresponding entry in that suite's index, run `spectra index --rebuild`, and confirm the index now contains an entry matching the file's frontmatter.

**Acceptance Scenarios**:

1. **Given** a suite directory containing one or more test files whose ids are not present in the suite's index, **When** the user runs `spectra index --rebuild`, **Then** the resulting index contains one entry per test file on disk, with each entry's fields sourced from that file's frontmatter.
2. **Given** a suite directory that has been fully and correctly indexed, **When** the user runs `spectra index --rebuild`, **Then** the rebuilt index is semantically equivalent to the prior index (no test entries lost, no spurious entries added).
3. **Given** a workspace with multiple suites, **When** the user runs `spectra index --rebuild`, **Then** every suite's index file is regenerated from the test files in its directory.

---

### User Story 3 - The "write a test" path cannot drift again (Priority: P3)

A maintainer adding a new test-generation flow in the future (for example, a CSV import path or a template-driven generator) cannot accidentally introduce the same bug class. There is a single, obvious entry point for persisting a generated test; any path that does not use it is immediately identifiable in review.

**Why this priority**: Architectural durability. The immediate bug can be fixed with a two-line addition to the from-description handler, but that leaves the trap in place for the next contributor. Funneling both flows through one helper converts a discipline problem ("remember to update the index") into a structural guarantee ("the only public write path also indexes"). P3 because it does not change user-observable behavior on its own — its value is realized over time as new generation flows are added.

**Independent Test**: Search the codebase for direct test-file write call sites outside the central persistence helper. After this work, the count is zero (excluding the helper's own internals and the rebuild path, which intentionally writes only the index, not test files).

**Acceptance Scenarios**:

1. **Given** the batch generation flow and the from-description generation flow, **When** either persists a generated test, **Then** both go through the same single persistence entry point.
2. **Given** the persistence entry point is invoked, **When** it returns success, **Then** both the test file is written to disk and the suite's index file reflects the test's id.
3. **Given** the persistence entry point is invoked and the file write succeeds but the index update fails (or vice versa), **When** the caller observes the result, **Then** the failure is surfaced (not silently swallowed) so the inconsistency is visible rather than latent.

---

### Edge Cases

- **Re-running `--from-description` for the same logical test**: The generator may emit the same id (collision-avoided) or a fresh id. In either case the index must end up with exactly one entry per id present on disk.
- **Empty suite (suite directory exists but has no test files yet)**: Generating a from-description test into an empty suite produces a valid index containing only that one test.
- **Suite directory does not yet exist**: From-description generation creates the suite directory, the test file, and a fresh index in a single command.
- **Concurrent generation into the same suite**: Two concurrent generation commands targeting the same suite must not corrupt the index. (The persistent ID allocator from Spec 046 already guarantees unique ids; this spec inherits the same locking discipline for index writes — last-writer-wins on the index file is acceptable provided the full suite set is regenerated each time, not partial.)
- **Test file with malformed or missing frontmatter encountered during `index --rebuild`**: The rebuild reports the failed file and continues with the remainder rather than aborting the entire rebuild.
- **Test file present on disk whose id collides with an existing index entry from a different file**: `index --rebuild` reports the collision; resolution is deferred to the existing `spectra doctor ids --fix` tooling (Spec 046) and is out of scope here.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The from-description generation flow MUST update the target suite's index file as part of completing a successful generation; no separate user step is required.
- **FR-002**: After the from-description flow updates the index, the new entry's id, title, priority, tags, and component MUST match the values present in the generated test file's frontmatter; the priority MUST be stored in the index in lowercase, consistent with batch-generated entries.
- **FR-003**: A from-description generation MUST regenerate the suite's index from the full set of tests for that suite (existing tests already on disk plus the newly generated one), so previously-indexed entries are preserved.
- **FR-004**: A re-run of from-description generation that yields a test with the same id as an existing on-disk test MUST result in exactly one index entry for that id.
- **FR-005**: Both the batch generation flow and the from-description generation flow MUST persist tests through the same single, central persistence entry point. No other generation flow may write a test file without also updating that suite's index through this entry point.
- **FR-006**: `spectra index --rebuild` MUST regenerate each suite's index file from the test files of record (the `.md` files in the suite directory), not from any prior index contents.
- **FR-007**: When `spectra index --rebuild` encounters a test file with malformed or missing frontmatter, it MUST report that file as failed and continue processing the remaining files (consistent with how Spec 047 surfaces failed documents during corpus extraction).
- **FR-008**: After a successful from-description generation of a test with `priority: high`, the test MUST be returned by discovery queries filtering on `priorities: ["high"]` and MUST be counted as a member of the bundled `smoke` saved selection.
- **FR-009**: Batch-generation observable behavior MUST be unchanged by the centralization refactor: the index produced by a batch run after this change is semantically equivalent to the index that would have been produced before this change for the same inputs.
- **FR-010**: The fix MUST be confined to the test-write/orchestration layer; the filter logic, the index data model, and the MCP tools that consume the index MUST NOT be modified by this spec.
- **FR-011**: If the persistence entry point encounters a failure during either the test-file write or the index update, the failure MUST be surfaced to the caller (and ultimately to the user) rather than silently swallowed.

### Key Entities

- **Suite Index** (one per suite, file of record for discovery): The structured listing of tests in a suite, consumed by every MCP discovery and execution tool. Has one entry per test in the suite, each carrying that test's id, title, priority (lowercase), tags, and component.
- **Test File** (one per test, file of record for content): The on-disk artifact containing the test's full content and frontmatter. The frontmatter is the source of truth for each index entry's fields.
- **Test Persistence Operation** (logical, not user-visible): The atomic-from-the-caller's-perspective sequence of writing one or more test files and updating the suite's index. Both generation flows invoke this operation; no generation flow writes a test file outside it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of tests created via `--from-description` are discoverable via `find_test_cases` and executable via `start_execution_run` immediately after the generation command completes, with no additional indexing step required.
- **SC-002**: When a user creates a `priority: high` test via `--from-description`, that test is returned by a `priorities: ["high"]` filter query in the next operation, with zero additional commands required between creation and query.
- **SC-003**: Running `spectra index --rebuild` against a workspace where every from-description test was previously unindexed results in 100% of those tests becoming discoverable, in a single command, with no test files modified.
- **SC-004**: The number of distinct code paths that write a test file to disk is exactly one after this change (the central persistence entry point), as verified by static search; today there are two.
- **SC-005**: Batch generation produces an index semantically equivalent to today's batch-generated index for the same inputs (regression guard — no behavioral change to the working path).
- **SC-006**: Adding a from-description test to a suite that already contains N tests results in an index of exactly N+1 entries; no pre-existing entry is dropped or altered.
- **SC-007**: Re-running `--from-description` for the same id results in exactly one index entry for that id (no duplication, no growth in the index on repeated runs).
