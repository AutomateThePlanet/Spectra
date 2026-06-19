# Feature Specification: Verdict Disposition Policy

**Feature Branch**: `071-verdict-disposition`
**Created**: 2026-06-19
**Status**: Draft
**Investigation basis**: `FINDINGS-verdict-disposition.md` (all facts below are CONFIRMED with file:line in that doc)

## Problem Context

The critic subagent correctly assigns one of three verdicts to every generated test: `grounded` (all claims trace to docs), `partial` (some claims unverifiable), or `hallucinated` (contradicts docs). The gate correctly deletes hallucinated tests. But the two passing verdicts leave nothing durable behind:

- **Partial verdicts evaporate.** The verdict JSON is a single file overwritten by each successive test. After a run, there is no record of which kept tests the critic flagged or why. TC-113/117/127/135 are indistinguishable from grounded tests on disk.
- **Grounded verdicts are also invisible.** The code to write a verification block to the test file exists but is never activated — the generate flow never supplies the data it needs.
- **Drops have no trail.** Hallucinated tests are deleted cleanly but silently. There is no record that TC-138 existed, was dropped, or why.
- **No repair path.** A partial whose only fault is a derivable-but-not-verbatim detail (TC-113's conversion factor) must either stay silently flawed or be fully regenerated — even though the critic's findings name the exact element, claim, and reason needed to patch it.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Every kept test shows its verification status (Priority: P1)

A test author reviewing the test suite needs to know, from the test file itself, whether each test was verified against documentation and what the verdict was. Today there is no such signal — kept tests look identical regardless of verdict.

**Why this priority**: This is the foundation. Verdicts that leave no trace on the artifact are indistinguishable from tests that were never verified. Everything else (repair, review, trail) depends on verdicts being durable and visible first.

**Independent Test**: After a generate run, open any kept test's `.md` file and see a `grounding:` block in the frontmatter that states `verified`, `score`, and for partial tests, the condensed findings and a `flagged_for_review` flag.

**Acceptance Scenarios**:

1. **Given** a generated test the critic verdicts `grounded`, **When** the generate flow completes, **Then** the test's `.md` frontmatter contains a `grounding:` block with `status: grounded`, `score`, and `verified` timestamp; and a full verdict JSON is present in `.spectra/verdicts/` under a per-test filename.
2. **Given** a generated test the critic verdicts `partial` with specific findings, **When** the generate flow completes, **Then** the test's `.md` frontmatter contains a `grounding:` block with `status: partial`, `score`, condensed findings (element + one-line reason for each non-grounded finding), `flagged_for_review: true`, and `repair_attempts: 0`; full verdict JSON in `.spectra/verdicts/`.
3. **Given** a generated test the critic verdicts `hallucinated`, **When** the generate flow completes, **Then** the test file is deleted (existing behavior, unchanged), `_index.json` is updated, and an entry is appended to `.spectra/dropped-tests.json` recording the id, the contradicting claim, the doc reference, and a timestamp. No `grounding:` block written (no file to write it to).
4. **Given** a batch run that produced a `.spectra/verdicts/critic-verdict.json` for the last test, **When** the run completes, **Then** each test has its OWN per-test verdict file (e.g., `critic-verdict-TC-113.json`) and the files are not overwritten by subsequent tests in the same run.

---

### User Story 2 — Partial tests get one bounded repair attempt (Priority: P2)

A test author who reviews flagged tests after a run should see fewer of them because the system already tried to fix the easy ones. A partial whose only fault is a specific unverified claim the critic named should be correctable without a full regeneration.

**Why this priority**: Repair only makes sense when the grounding block exists (P1). P2 depends on P1. The repair loop is the most novel part of this spec and the main user-facing quality improvement.

**Independent Test**: Run a generate batch that produces at least one partial test. Observe that the system automatically attempts one repair before flagging, and that successfully repaired tests show `status: grounded`, `repaired: true`, `repair_attempts: 1` in their block.

**Acceptance Scenarios**:

1. **Given** a test the critic verdicts `partial`, **When** the generate flow processes the verdict, **Then** the system compiles a repair prompt (injecting the original test, the critic's findings, and the relevant source doc sections), the agent produces a corrected test, and the re-critic runs on the corrected version.
2. **Given** the re-critic verdicts the repaired test `grounded`, **When** the repair cycle completes, **Then** the test's `.md` block shows `status: grounded`, `repaired: true`, `repair_attempts: 1`; the full verdict JSON reflects the final grounded verdict.
3. **Given** the re-critic verdicts the repaired test still `partial`, **When** the repair cycle completes, **Then** the test is left in place with `status: partial`, `flagged_for_review: true`, `repair_attempts: 1`; the system does NOT attempt a second automatic repair.
4. **Given** a test the critic verdicts `hallucinated`, **When** processing continues, **Then** the system does NOT attempt repair — it goes straight to trail-and-delete.
5. **Given** a batch run with `--no-interaction` and multiple partial tests whose repair attempts all fail, **When** the run completes, **Then** the batch did not stop or prompt at any point; each still-partial test is flagged; the final report shows counts: kept-grounded / repaired-to-grounded / flagged-partial / dropped-hallucinated.

---

### User Story 3 — Human review of flagged tests (Priority: P3)

After a batch run, a test author needs to disposition each still-partial (flagged) test: accept it as-is, delete it, or retry repair. This should be a separate deliberate step, not inline during generation.

**Why this priority**: Review only matters once flagged tests exist (P2). Separating review from generation keeps generation non-blocking and lets the author batch-review at their own pace.

**Independent Test**: With at least one test flagged (`flagged_for_review: true` in its `.md`), run the review surface and dispose each test. Verify the four stores (test file, index, criteria backlinks policy, coverage) remain consistent after each action.

**Acceptance Scenarios**:

1. **Given** a suite with flagged tests, **When** the reviewer runs the review surface, **Then** each flagged test is listed with its condensed verdict, score, and condensed findings.
2. **Given** the reviewer chooses **accept** for a flagged test, **When** the action completes, **Then** the test's `flagged_for_review` is cleared while the `status: partial` block is retained as an acknowledged record; `_index.json` is unchanged; coverage still counts the test.
3. **Given** the reviewer chooses **delete** for a flagged test, **When** the action completes, **Then** an entry is appended to `dropped-tests.json` (with `user_decided: true` to distinguish from critic-drops), the three-phase clean delete runs (file deleted, `_index.json` updated, `depends_on` stripped), and the test is gone from coverage.
4. **Given** the reviewer chooses **retry repair** for a flagged test, **When** the action completes, **Then** one new bounded repair attempt runs, and the test's block is updated to reflect the outcome (upgraded to grounded, or stays partial with `repair_attempts: 2`).
5. **Given** any review action completes, **Then** the four stores (test file, `_index.json`, criteria backlinks, coverage) are in agreement — no dangling references.

---

### User Story 4 — Stale documentation corrected (Priority: P4)

Any team member reading the codebase, docs, or skill instructions should understand the current disposition policy. Today four code comments describe the grounding write-back as working when it doesn't; doc sections describe verdicts as advisory-only with no persistence.

**Why this priority**: Docs are last — they document the final shipped state. They cannot be written accurately until Phases 1–3 are done.

**Independent Test**: After all changes ship, a new team member following only the docs can correctly predict what happens to a grounded, partial, and hallucinated test, and understands the review surface.

**Acceptance Scenarios**:

1. **Given** the docs update is applied, **When** a developer reads `TestFileWriter`'s grounding block comment, **Then** it accurately describes the live write-back (not "stays in `CreateTestWithGrounding`").
2. **Given** the docs update is applied, **When** a developer reads the `VerificationVerdict.Partial` enum doc, **Then** it accurately states the partial test is kept with a grounding block and flagged for review.
3. **Given** the docs update is applied, **When** a test author reads the CLI reference, **Then** they find `compile-repair-prompt`, `review flagged`, and `dropped-tests.json` documented with examples.
4. **Given** the docs update is applied, **Then** no comment, doc section, or skill instruction claims that "verdicts are advisory only with no persistence" or that "the grounding write-back is the CLI's job" without the write-back actually happening.

---

### Edge Cases

- What happens when the repair prompt compilation fails (e.g., `source_refs` in the test don't resolve to real doc files)? → fail loud with a specific error; treat as repair failure; flag the test; do not delete.
- What happens when the re-critic on a repaired test returns `hallucinated`? → trail-and-delete (same as any hallucinated test); the trail entry records `repair_attempts: 1`.
- What happens when the re-critic on a repaired test returns a damage verdict (empty / parse failure)? → count as repair failure; leave test flagged; do not delete.
- What happens when `.spectra/dropped-tests.json` does not yet exist? → create it before appending.
- What happens when a test is deleted during human review of a test that has `depends_on` references from other tests? → the existing `StripDependsOnAsync` path in DeleteHandler handles it; no new logic needed.
- What happens when the grounding block already exists on a test being re-verified (e.g., a manual re-run of the critic)? → overwrite the block with the latest verdict; `repair_attempts` accumulates.
- What happens when the verdict JSON for a test is missing at review time (user deleted `.spectra/verdicts/`)? → the condensed block in the `.md` still provides the summary; the full JSON is advisory detail; review proceeds on the `.md` block alone.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST write a condensed `grounding:` block to the test's `.md` frontmatter for every verdict outcome that keeps the test (grounded or partial), containing at minimum: `status`, `score`, and `verified` timestamp.
- **FR-002**: For `partial` verdicts, the `grounding:` block MUST additionally include: condensed findings (element + one-line reason per non-grounded finding), `flagged_for_review: true`, and `repair_attempts` count.
- **FR-003**: The system MUST persist a per-test verdict JSON to `.spectra/verdicts/` using a filename that incorporates the test ID (so successive tests in a batch do not overwrite each other).
- **FR-004**: Before deleting a hallucinated test, the system MUST append a drop-trail entry to `.spectra/dropped-tests.json` containing: test id, the contradicting claim(s), the doc reference(s) from the critic's findings, and a timestamp. The existing three-phase clean-delete is unchanged.
- **FR-005**: The system MUST provide a `compile-repair-prompt` CLI verb that, given a suite name and test ID, emits a plain-text repair prompt to stdout — injecting the original test artifact, the critic's findings from the persisted verdict JSON, and the relevant source doc sections. No JSON envelope; no model call.
- **FR-006**: When a test verdict is `partial`, the generate flow MUST automatically attempt one repair cycle: compile-repair-prompt → agent patches test → re-critic. The retry limit is 1; no second automatic repair attempt.
- **FR-007**: The system MUST NOT attempt repair for `hallucinated` tests. Hallucinated → trail-and-delete only.
- **FR-008**: In batch / `--no-interaction` mode, a failed repair (still partial after 1 attempt) MUST NOT halt the batch or prompt. The test is flagged and the run continues.
- **FR-009**: The final generate report MUST include counts: kept-grounded, repaired-to-grounded, flagged-partial, dropped-hallucinated.
- **FR-010**: The system MUST provide a review surface (CLI verb `spectra ai review-flagged --suite <s>`) that lists all tests with `flagged_for_review: true` in the named suite, displaying condensed verdict and findings.
- **FR-011**: The review surface MUST support per-test actions: **accept** (clear `flagged_for_review`, retain partial block), **delete** (trail-and-delete via existing DeleteHandler), **retry-repair** (one more bounded repair cycle).
- **FR-012**: Every delete action (critic-drop or human review) MUST leave `test-cases/*.md`, `_index.json`, `depends_on` references, and coverage in a consistent state. If criteria backlinks (`linked_test_ids`) are ever populated, the delete path MUST clean them in the same transaction.
- **FR-013**: The four stale dead-code comments that describe a non-existent grounding write-back path MUST be corrected to describe the live behavior.
- **FR-014**: The `.spectra/verdicts/` directory MUST be listed in `.gitignore` (or the project's equivalent scratch-file exclusion). `dropped-tests.json` MAY be gitignored (scratch) or committed (audit trail) — the plan phase resolves this; default is gitignored.

### Key Entities

- **Grounding block**: A condensed frontmatter section in each kept test's `.md` recording the verdict, score, timestamp, and (for partial) condensed findings and flags. Human-readable summary; not the full verdict JSON.
- **Verdict JSON**: Per-test file in `.spectra/verdicts/` containing the full critic response (verdict, score, all findings with element/claim/status/evidence/reason, critic model). Machine-readable; consumed by repair and review.
- **Drop trail**: `.spectra/dropped-tests.json` — append-only log of hallucinated-deleted tests (and human-decided deletions from review), with id, reason, doc reference, timestamp, and source flag.
- **Repair prompt**: Output of `compile-repair-prompt` — plain text combining the test artifact + critic's unverified/hallucinated findings + relevant source doc sections. Consumed by the agent in-session to produce a corrected test.
- **Flagged test**: A test with `flagged_for_review: true` in its grounding block — kept on disk, counted by coverage, awaiting human disposition via the review surface.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After any generate run, a team member opening a kept test's file can determine, without running any command, whether the test was verified as grounded or partial, and if partial, what specific claims were unverifiable.
- **SC-002**: After any generate run, the team has a complete count and record of tests dropped as hallucinated — including what each test claimed and which doc it contradicted — without relying on terminal history.
- **SC-003**: At least 1 in 3 partial tests (across representative batches) is upgraded to grounded by the automatic single repair attempt, reducing the number requiring manual review.
- **SC-004**: A batch generate run with `--no-interaction` completes end-to-end without halting for human input, regardless of how many partial tests fail their repair attempt.
- **SC-005**: A reviewer can action all flagged tests in a suite (accept / delete / retry) without leaving any of the four stores (test files, index, coverage, drop trail) in an inconsistent state.
- **SC-006**: After the docs update, a developer following only the docs can accurately predict the on-disk state (grounding block, verdict JSON, drop trail entry) for each of the three verdict outcomes, without reading the source code.

---

## Assumptions

- The existing `DeleteHandler` three-phase clean-delete is correct and complete for `depends_on` cleanup; this spec does not change it, only adds the trail step before it runs.
- `compile-repair-prompt` resolves source docs from the test's existing `source_refs` frontmatter field, mirroring how `compile-critic-prompt` already works. Tests without `source_refs` get a repair prompt without doc context; the repair cycle still runs.
- The critic on the re-check remains a different model family from the generator/repairer, per the existing independent-judgment principle. No change to model selection rules.
- Coverage (`spectra ai analyze --coverage`) continues to count flagged-partial tests as covered (they are real tests on disk). Annotating the count with grounded-vs-partial breakdown is out of scope for this spec.
- `dropped-tests.json` is gitignored by default (scratch, like other `.spectra/` JSON). The plan phase may elect to commit it as an audit artifact — that decision is deferred to planning.
- Phases are sequential: Phase 2 (repair) requires Phase 1's durable verdict JSON; Phase 3 (review) requires Phase 2's flagged state; Phase 4 (docs) documents the final shipped state.
