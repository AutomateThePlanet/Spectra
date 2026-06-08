# Feature Specification: Targeted test updates (inverted update seam)

**Feature Branch**: `063-targeted-test-updates`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "When docs change, edit the affected parts of existing tests through an inverted compile→generate→ingest seam — preserving id, structure, and manual fields deterministically — instead of regenerating from scratch (or, as today, not at all)."

> **Numbering note**: The grounding document labels this work "Spec 064"; the repository's auto-numbering assigned `063`. The "049" concept label in the grounding doc refers to `docs/investigation/queued/049-targeted-update-instructions.md` and is unrelated to the real shipped spec 049 (`from-description-index-parity`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Doc-aware targeted edit of an outdated test (Priority: P1)

A team member changes a source document. They run the update workflow. The system identifies which existing tests are now out of date against the changed documentation and, for each one, **edits the affected parts of that test in place** — fixing only what the documentation change requires — rather than regenerating the whole test from scratch or leaving it stale. The test keeps its identifier and overall structure; only the content implicated by the doc change is revised.

**Why this priority**: This is the core capability the feature exists to deliver. Today no doc-aware rewrite exists by any path — the update flow only classifies tests locally and never revises their content against changed docs. Without this story there is no feature. It is independently valuable on its own: even with nothing else, a user gets outdated tests brought back in line with current documentation.

**Independent Test**: Take a test that the classifier marks OUTDATED relative to a changed source document, run the update flow, and confirm the persisted test (a) reflects the documentation change, (b) retains its original identifier, and (c) was edited rather than recreated.

**Acceptance Scenarios**:

1. **Given** an OUTDATED test and a changed source document, **When** the update flow runs, **Then** the test is edited (not regenerated) and its identifier is unchanged.
2. **Given** an OUTDATED test whose change touches only one section, **When** the update flow runs, **Then** the sections not implicated by the documentation change remain as they were.
3. **Given** a set of tests where some are UP_TO_DATE and some are OUTDATED, **When** the update flow runs, **Then** only the OUTDATED tests are edited and the UP_TO_DATE tests are left untouched.

---

### User Story 2 - Manual content is never lost to an update (Priority: P1)

A test carries human-authored content — a manual verdict (a test deliberately marked as manual rather than automatable) and/or manual notes a person added. When that test is updated because its source doc changed, those human-authored fields are preserved exactly, regardless of what the editing step produces. The update fixes the doc-driven content without ever discarding human intent.

**Why this priority**: Silently overwriting human-authored content during an automated update is a trust-destroying data-loss bug. This guarantee must hold from day one or users cannot safely run updates over tests they have hand-curated. It is independently testable and independently valuable: it protects existing manual investment even if no other refinement ships.

**Independent Test**: Update a test that has a manual verdict and manual notes, deliberately have the editing step return output that omits or alters those fields, and confirm the persisted test still carries the original manual verdict and notes.

**Acceptance Scenarios**:

1. **Given** a test with a manual verdict, **When** it is updated, **Then** the manual verdict is preserved regardless of the editing step's output.
2. **Given** a test with manual notes, **When** it is updated, **Then** the manual notes are preserved regardless of the editing step's output.
3. **Given** a test whose identifier the editing step attempts to change, **When** the update is persisted, **Then** the identifier from the original test is kept and no new identifier is allocated.

---

### User Story 3 - Out-of-scope drift is surfaced, not silently accepted (Priority: P2)

When an update edits a test, the system compares the edited result against the original and **surfaces any change to a field that the documentation change did not implicate** — failing loudly rather than quietly persisting unexpected drift. The user is told what unexpectedly changed instead of discovering it later.

**Why this priority**: This is the safety net that makes whole-test editing trustworthy. Whole-test editing is simpler than surgical field-merging, but it risks the editing step quietly altering things it should not have. The drift guard converts that risk into a visible, fail-loud event. It builds on Stories 1 and 2 (there must be an edit to guard) so it is P2, but it is essential to the design's credibility.

**Independent Test**: Force the editing step to change a field unrelated to the documentation change, run the update, and confirm the system reports the unexpected drift and does not silently persist it.

**Acceptance Scenarios**:

1. **Given** an edit that changes a field unrelated to the documentation change, **When** the result is validated before persistence, **Then** the unexpected drift is surfaced fail-loud and is not silently persisted.
2. **Given** an edit confined to the fields the documentation change implicates, **When** the result is validated, **Then** validation passes and the test is persisted.

---

### User Story 4 - Invalid edits fail loudly and retry (Priority: P2)

When the editing step produces output that does not pass validation (malformed structure, missing required content, schema violation), the system fails loudly with a specific, actionable error and retries the edit a bounded number of times with that error fed back in — mirroring how generation already handles invalid output. It does not silently persist a broken test, and it does not retry forever.

**Why this priority**: Robustness of the boundary. Without bounded fail-loud retry, a single bad edit either corrupts a test or hangs the flow. It depends on the seam from Stories 1–3 existing, so it is P2.

**Independent Test**: Feed the validation boundary an invalid edited test, confirm it rejects with a specific error, confirm the flow retries with that error, and confirm it stops after the bounded number of attempts rather than looping indefinitely.

**Acceptance Scenarios**:

1. **Given** invalid edited output, **When** validation runs, **Then** it fails loud with a specific error and the original test is not overwritten.
2. **Given** a validation failure, **When** the flow retries, **Then** the specific error is provided to the retry so the next edit can correct it.
3. **Given** repeated validation failures, **When** the bounded retry limit is reached, **Then** the flow stops retrying and reports the failure rather than looping.

---

### Edge Cases

- **No outdated tests**: When the classifier finds nothing OUTDATED, the update flow performs no edits and reports that there was nothing to update.
- **Test outdated by a deleted/missing source**: If a test is outdated because its source content is gone (orphaned) rather than changed, that is the existing orphan path and is out of scope for the edit seam — the edit seam acts only on tests that have changed source/criteria to edit against.
- **Editing step returns the test unchanged**: If the edit produces no material change, the system treats it as a no-op edit (identifier preserved, nothing meaningfully altered) rather than an error.
- **Manual verdict added by the edit**: The invariant protects a *pre-existing* manual verdict/notes from the original; the feature does not require honoring a manual verdict newly introduced by the editing step.
- **A test both outdated and carrying manual content**: Stories 1 and 2 compose — the test is edited against the doc change while its pre-existing manual fields are re-asserted from the original.
- **Concurrent/overlapping updates touching the same test**: Persistence goes through the single existing write-and-index path so a test file is never written without its index being kept in parity; the feature must not introduce a path that writes a test without updating discovery indexes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a deterministic way to compile an update prompt for each outdated test that includes the existing test artifact, the changed source/criteria it must be reconciled against, and explicit instructions to *edit, not regenerate*, and to preserve the test's identifier, structure, and manual fields.
- **FR-002**: The system MUST provide a fail-loud ingest boundary that validates an edited test and persists it through the existing single write-and-index path, **without allocating a new identifier** (this is an edit of an existing test, not creation of a new one).
- **FR-003**: The ingest boundary MUST deterministically protect invariants rather than trusting the editing step to do so: (a) the identifier is taken from the original test; (b) a pre-existing manual verdict and manual notes are re-asserted from the original and never overwritten; (c) a drift guard surfaces changes to fields not implicated by the update rather than accepting them silently.
- **FR-004**: The update workflow MUST drive the loop — compile update prompt → edit in session → ingest — with bounded, fail-loud retry that feeds the specific validation error back into each retry, and that stops after a bounded number of attempts.
- **FR-005**: The existing test classifier MUST be reused unchanged as the **selector** that determines which tests need updating. It is the input to the new seam, not the rewrite mechanism; the feature MUST NOT change what it classifies or how.
- **FR-006**: Tests classified as up to date MUST be left untouched by the update flow.
- **FR-007**: Manual content (a pre-existing manual verdict and manual notes) MUST be preserved through an update regardless of the editing step's output.
- **FR-008**: Persistence of an updated test MUST keep the test file and its discovery/index records in parity — no path may write an updated test file without updating the corresponding index.
- **FR-009**: User-facing documentation and skill guidance that currently claims the update command "rewrites affected test cases" MUST be corrected to accurately describe the new edit-in-session flow, because that claim is false under today's behavior and would remain misleading if not rewritten.

### Key Entities *(include if feature involves data)*

- **Existing test (the original)**: The test being updated. Source of truth for the invariants the update must preserve — its identifier, its structure, and any human-authored content (manual verdict, manual notes). Never re-created; only edited.
- **Changed source/criteria**: The documentation content (and/or derived acceptance criteria) that changed and that the test must be reconciled against. The reason the test was flagged outdated and the scope boundary for what the edit is permitted to touch.
- **Classification outcome**: The per-test verdict (up to date / outdated / orphaned / redundant) produced by the existing classifier. Selects which tests enter the edit seam (the outdated ones); not modified by this feature.
- **Update prompt**: The compiled, deterministic instruction set handed to the editing step — original test + changed source/criteria + edit-don't-regenerate / preserve-invariants directives.
- **Edited test (the candidate)**: The whole-test result of the editing step, before validation. Untrusted: validated, invariant-protected, and drift-checked at ingest before it may replace the original.
- **Drift report**: The set of unexpected, out-of-scope field changes detected by comparing the edited candidate against the original. When non-empty, it is surfaced fail-loud and blocks silent persistence.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For an outdated test reconciled against a changed source document, 100% of update runs keep the original test identifier (no new identifier is ever allocated for an edit).
- **SC-002**: For a test carrying a pre-existing manual verdict and/or manual notes, 100% of update runs preserve those manual fields exactly, even when the editing step's output omits or alters them.
- **SC-003**: 100% of edits that change a field not implicated by the documentation change are surfaced to the user as drift and blocked from silent persistence; 0% are silently accepted.
- **SC-004**: 100% of invalid edited outputs are rejected with a specific, actionable error and never overwrite the original test; retries are bounded and the flow terminates rather than looping.
- **SC-005**: Tests classified as up to date are modified in 0% of update runs (they remain byte-for-byte unchanged).
- **SC-006**: After an update persists an edited test, the discovery/index records match the persisted file in 100% of cases (no orphaned or stale index entries introduced by the update path).
- **SC-007**: User-facing documentation and skill guidance no longer contain the inaccurate "rewrites affected test cases" claim and instead describe the edit-in-session flow; a reader following the docs can correctly predict what an update does.
- **SC-008**: The reused classifier and the reused write-and-index path retain their existing test coverage unchanged and green — any breakage there is treated as a regression in a reused component, not an expected change.

## Assumptions

- The existing test classifier already reliably identifies which tests are outdated against changed documentation; this feature consumes that signal and does not attempt to improve classification.
- The existing single write-and-index persistence path is the correct and only sanctioned way to persist a test such that discovery/index parity is maintained; the update seam routes through it rather than writing files directly.
- "Manual fields to preserve" means a *pre-existing* manual verdict and manual notes carried by the original test. Manual content newly introduced by the editing step is out of scope for the preservation invariant.
- The editing step runs in the interactive session (the same inversion model used for generation), so this feature does not depend on any in-process model or external provider SDK and is not blocked by the pending provider/SDK retirement.
- "Whole-test edit with deterministic invariant protection at ingest" is the chosen mechanism. Per-field surgical merge is a documented fallback to adopt only if the drift guard proves too noisy in practice; it is not built as part of this feature.
- The scope boundary for "fields implicated by the documentation change" is determined at ingest time by comparing the edited candidate to the original in the context of the changed source/criteria. If that boundary proves impractical to determine precisely, the drift guard errs toward surfacing (fail-loud) rather than silently accepting.

## Dependencies

- Reuses the established inverted-seam pattern (compile prompt → edit/generate in session → ingest), the single write-and-index persistence path, and the existing test classifier. This feature stands up the *update* counterpart of that pattern, which does not yet exist.
- Independent of the other queued post-migration features.
- Not blocked by the pending provider/SDK retirement (runs in the interactive session with no in-process model).

## Out of Scope

- Changing what the test classifier classifies or how it classifies (reused as-is as the selector only).
- The generation, criteria-extraction, and critic seams (this feature adds the parallel *update* seam only).
- Per-field surgical merge as the primary mechanism (documented fallback only).
- Orphaned/redundant/deletion handling beyond what already exists — the edit seam acts only on tests that have changed source/criteria to edit against.
