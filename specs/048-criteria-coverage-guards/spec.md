# Feature Specification: Criteria Coverage Guards

**Feature Branch**: `048-criteria-coverage-guards`
**Created**: 2026-06-02
**Status**: Draft
**Input**: User description: "Add non-blocking guards so users are never silently left without acceptance criteria. (1) Make the on-disk state distinguish a genuine empty extraction from an inconclusive one. (2) `spectra docs index` emits a clear, non-blocking warning when it indexed documents yet produced zero criteria. (3) Test generation emits a quiet, non-blocking note when the target suite has no matching acceptance criteria. No blocking prompts anywhere. Depends on Spec 047 (already merged)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Docs-index user is warned when extraction produced zero criteria (Priority: P1)

A QA engineer runs `spectra docs index` against a project's documentation. The command indexes the documents but, on this run, every per-document extraction was inconclusive (transient AI hiccups, malformed responses, etc., which after Spec 047 are correctly not cached). Today, the only signal is a single easily-missed log line; the user's mental model is "I indexed, so criteria exist," so they walk away believing extraction succeeded and only discover the gap later when coverage looks wrong. After this change, the command emits a prominent, non-blocking warning at the end of the run when it indexed at least one document but produced zero acceptance criteria across the entire corpus, and it names the exact recovery command. The command still exits successfully — it does not block or prompt.

**Why this priority**: This is the most visible failure mode of the current system on real-world large projects: it is exactly the scenario Spec 047's per-document retry was designed to make recoverable, but without this warning the user never knows recovery is needed. Highest user-visible payoff.

**Independent Test**: Run `docs index` against a corpus where every per-document extraction is inconclusive (use a stub AI that returns parse failures). Verify the warning is emitted to the console, the JSON result includes the corresponding warning field, and the process exits with the success exit code. Then run against a corpus where at least one document produces criteria and verify the warning is not emitted.

**Acceptance Scenarios**:

1. **Given** `docs index` indexed at least one document but the corpus-wide acceptance-criteria total is zero, **When** the command completes, **Then** a prominent warning is shown that names the recovery command (`spectra ai analyze --extract-criteria`), the command exits with success status, and the structured (JSON) result carries a corresponding warning field.
2. **Given** `docs index` indexed documents and at least one acceptance criterion was produced anywhere in the corpus, **When** the command completes, **Then** no zero-criteria warning is emitted and the structured result contains no zero-criteria warning field.
3. **Given** every indexed document is legitimately empty (an extraction outcome that found no testable criteria but did succeed), but no other criteria exist, **When** the command completes, **Then** the warning is emitted exactly as in Scenario 1 — the user is offered the recovery command, since from a coverage perspective the corpus produced no criteria either way.
4. **Given** the user passes `--skip-criteria`, **When** the command completes, **Then** no zero-criteria warning is emitted regardless of corpus state, because extraction was intentionally suppressed.

---

### User Story 2 - Generation user sees a note when no criteria match the target suite (Priority: P1)

A QA engineer runs test generation against a suite (either the batch flow or the from-description flow). On this run, no acceptance criteria match the target suite — either because none have been extracted yet, or because none correspond to the suite's component. Today, generation proceeds silently: tests are written with empty criteria links, and the user only realizes there is no acceptance-criteria coverage when they inspect coverage reports later. After this change, the run completes normally but the result includes a clear, non-blocking note explaining that no acceptance criteria matched, that coverage linkage will be absent for these tests, and what to run if criteria were expected. The note appears in the structured result regardless of console verbosity, so automated callers always see it.

**Why this priority**: Equally important as Story 1: it closes the parallel "silent" gap on the generation side. The user knows immediately at generation time that coverage linkage will be missing, rather than discovering it through a separate coverage check.

**Independent Test**: Run generation against a suite for which the criteria index contains no matching entries, and confirm the structured result's notes collection contains the expected message and that generation otherwise completed successfully (tests written, normal status). Then run with at least one matching criterion present and confirm the note is absent. Repeat both for the from-description flow. Run once more with `--verbosity quiet` and confirm the structured result still carries the note even though human-facing output is suppressed.

**Acceptance Scenarios**:

1. **Given** a generation run (batch flow) targets a suite with zero matching acceptance criteria, **When** the run completes, **Then** the structured result contains a non-blocking note naming the suite and the recovery command, the run reports normal completion status, and tests are written without criteria links.
2. **Given** a generation run (from-description flow) targets a suite with zero matching acceptance criteria, **When** the run completes, **Then** the same note is present with the same content shape, and the run completes normally.
3. **Given** a generation run targets a suite that has matching acceptance criteria, **When** the run completes, **Then** no no-criteria note is present in the result.
4. **Given** the user runs generation with `--verbosity quiet`, **When** the conditions for the note are met, **Then** the structured result still includes the note (only the human-facing console line is suppressed).
5. **Given** the conditions for the note are met, **When** the run completes, **Then** no interactive prompt is shown and no user response is required to finish the run.

---

### User Story 3 - On-disk record makes "affirmed empty" distinguishable from "inconclusive" (Priority: P2)

A QA engineer or downstream tooling reads the master acceptance-criteria index for a document and sees a criteria count of zero. Today, that record cannot be told apart from a silent failure: a count of zero could mean the AI found no testable criteria on a successful run, or it could mean an earlier run swallowed a parse failure into an empty list. After Spec 047, only genuinely successful results are persisted — but the on-disk record itself does not yet say so, so any future guard that warns on "zero criteria" cannot distinguish "this doc is affirmed-empty, don't warn" from "this doc never produced a real result, do warn." This story records the extraction outcome alongside the count, so guards and external tooling can rely on the record itself rather than re-deriving the distinction.

**Why this priority**: This is supporting infrastructure for Stories 1 and 2 (and for any future criteria-coverage guard). Lower priority because the *visible* user value lives in Stories 1 and 2; this story makes those guards correct in edge cases and future-proofs the data model. It is still independently testable and deliverable.

**Independent Test**: Trigger an extraction run that produces a genuine empty result for at least one document (legitimately no testable criteria found). Read the master criteria index and verify the corresponding record carries an explicit outcome marker indicating an affirmed extraction. Read an older index entry written before this change (a "legacy" record with no outcome field) and verify it is interpreted as an affirmed extraction by default, so existing on-disk data continues to behave correctly.

**Acceptance Scenarios**:

1. **Given** a document for which extraction succeeds with a genuine empty result, **When** the master criteria index is updated, **Then** the persisted record for that document carries an explicit outcome marker meaning "affirmed extracted."
2. **Given** a master criteria index entry written by a previous version that did not record an outcome, **When** the current version reads that entry, **Then** the entry is interpreted as an affirmed extraction (no migration step required, no false warnings).
3. **Given** a document whose criteria count is zero with the affirmed-extracted marker, **When** future guards evaluate the corpus, **Then** that document does not contribute toward a "missing extraction" warning condition.

---

### Edge Cases

- **`--skip-criteria` flag on `docs index`**: no zero-criteria warning is emitted; the user explicitly opted out of extraction this run.
- **A corpus that legitimately produces zero criteria across all documents** (each individual document is affirmed-empty): from a coverage standpoint, no criteria exist; the warning is still useful to surface this state, but the user can ignore it once verified. The wording must remain helpful, not accusatory.
- **A run where some documents are affirmed-empty and others produced criteria**: corpus total is non-zero; no warning. The mixed-state case behaves the same as the all-produced case.
- **Generation run with `--no-interaction` and no matching criteria**: the note is present in the structured result; generation completes normally with no prompt.
- **Generation run with `--verbosity quiet` and no matching criteria**: the structured result carries the note; the console line is suppressed.
- **Legacy `_criteria_index.yaml` entries written before this spec**: continue to deserialize without error and are treated as affirmed-extracted; no manual migration is required.
- **Extraction runs that fail mid-corpus** (per Spec 047): documents that did not produce a cacheable result are not persisted, so they do not appear in the index with a misleading outcome.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The persisted per-document record in the master acceptance-criteria index MUST carry an explicit "extraction outcome" marker for every entry the system writes from this point forward.
- **FR-002**: The system MUST treat any record without an explicit outcome marker (i.e., entries written by prior versions) as equivalent to an affirmed-extracted outcome on read. No manual migration step shall be required.
- **FR-003**: A criteria count of zero combined with an affirmed-extracted outcome MUST be treated as a legitimate empty result by all guards introduced in this work; it MUST NOT trigger any failure-style warning for that document specifically.
- **FR-004**: `spectra docs index` MUST continue to extract acceptance criteria by default. The `--skip-criteria` flag MUST continue to suppress extraction unchanged.
- **FR-005**: When `spectra docs index` has indexed at least one document and the corpus-wide acceptance-criteria total for the run is zero, the command MUST emit a prominent non-blocking warning at the end of the run that names the recovery command (`spectra ai analyze --extract-criteria`).
- **FR-006**: When the warning condition in FR-005 holds, the command's structured (JSON) result MUST include a dedicated warning field carrying the same message. When the condition does not hold, that field MUST NOT be present (so consumers can detect the state with a simple presence check).
- **FR-007**: The command's exit status MUST NOT change because of the warning: a run that would otherwise succeed continues to exit success when the warning is emitted.
- **FR-008**: When the user passes `--skip-criteria`, the zero-criteria warning MUST NOT be emitted regardless of corpus state.
- **FR-009**: When a test-generation run (either the batch flow or the from-description flow) finds no acceptance criteria matching the target suite after criteria loading and component-match filtering, the run's structured result MUST include a non-blocking note that names the affected suite and the recovery command.
- **FR-010**: The note from FR-009 MUST be present in the structured result regardless of the console verbosity flag; only the human-facing console rendering of the note may be suppressed under `--verbosity quiet`.
- **FR-011**: When the note from FR-009 is added, the generation run MUST continue to execute and report its normal completion status (tests written, summary counts, etc.); the note MUST NOT cause the run to fail, abort, or change exit code behaviour.
- **FR-012**: No guard introduced by this work shall introduce a blocking prompt, interactive question, or any control-flow stop that requires user input. All signals are output-only.
- **FR-013**: The packaged user-facing assets that surface command results (the bundled SKILL renderings for `spectra docs` and `spectra ai generate`) MUST render the warning and the note when present, so users running through those entry points see them.
- **FR-014**: When the warning or note is emitted, the message MUST name the exact command the user should run to recover (`spectra ai analyze --extract-criteria`), so the user does not have to guess.

### Key Entities

- **Extraction Outcome (record-level)**: A small label persisted alongside each per-document entry in the master acceptance-criteria index, identifying *why* the entry has the criteria count it has. Only one value is written by current code (an affirmed extraction); the field exists so the data model can grow without re-deriving the distinction heuristically.
- **Corpus Criteria Total (per-run)**: The sum of acceptance criteria produced across all documents in a single `docs index` extraction phase. Used to gate the zero-criteria warning.
- **Generation No-Match Note**: A short non-blocking note attached to a generation run's structured result whenever the target suite ends up with zero matching acceptance criteria. Conveys what happened, what it implies for coverage linkage, and how to recover. Surfaced both in the structured result and (unless quietened) in the human-facing rendering.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a corpus where indexing produced documents but extraction yielded zero criteria, 100% of users see a prominent warning naming the recovery command at the end of the run (today: visible signal is a single easy-to-miss line; target: prominent warning).
- **SC-002**: On a corpus where extraction yielded any criteria at all, the zero-criteria warning is emitted 0% of the time (no false positives).
- **SC-003**: On a generation run where the target suite has zero matching criteria, 100% of structured results contain the non-blocking note explaining the situation (today: 0%).
- **SC-004**: The note in SC-003 is present in 100% of structured results regardless of the console verbosity flag.
- **SC-005**: No guard introduced by this work prompts the user or requires interactive input — 0% of test runs under non-interactive flags (`--no-interaction`) experience a blocking pause due to a guard.
- **SC-006**: Legacy master-criteria-index entries written before this change continue to be readable without error and are interpreted as affirmed-extracted entries — 100% backward compatibility on read with no user-visible migration step.
- **SC-007**: Time from "extraction produced no criteria" to "user knows to run `spectra ai analyze --extract-criteria`" drops to a single command invocation, since the warning names the recovery command directly (today: typically requires reading docs or asking).

## Assumptions

- Spec 047 (Resilient Criteria Extraction) has already merged, which guarantees that only genuine extraction outcomes are persisted to the criteria index. This work relies on that guarantee for the "entry present ⇒ affirmed extracted" invariant.
- The existing `_criteria_index.yaml` schema can be extended additively (new optional field with a documented default) without requiring a file-format version bump or migration step.
- The existing progress/result reporting surfaces (`_progress.Warning` for the docs-index human channel and the structured-result writer for JSON) are the appropriate places to wire the new signals. No new reporting surface is introduced.
- "Component-match filtering" for the generation flow uses the existing criteria-to-suite matching logic. This work does not change matching behaviour; it only adds a notice when the match set is empty.
- The bundled SKILL surfaces (`spectra-docs.md`, `spectra-generate.md`) are the right place to render the new signals to interactive users. Renderings outside of bundled SKILLs (third-party integrations, custom dashboards) consume the structured result and surface the fields themselves.
- The wording of the warning and note can use a single hard-coded recovery command (`spectra ai analyze --extract-criteria`); no need to detect or vary the command based on environment.

## Out of Scope

- **Blocking prompts of any kind.** This is a hard design constraint, not a deferred enhancement.
- **An interactive "extract criteria now?" affordance.** Users who want to recover run the named command themselves.
- **Per-suite criteria heuristics or fuzzy matching.** The note fires whenever the existing matching logic returns an empty set; this spec does not change matching.
- **A configurable threshold for "low" criteria counts.** Only the literal zero case is treated specially in this work.
- **Schema versioning of `_criteria_index.yaml`.** The field added in this work is additive and backward-compatible by design; no version stamp is introduced.
- **Re-extraction triggered automatically by the warning.** The user runs the named recovery command themselves.
- **Surfacing the warning or note in non-bundled (third-party) UIs.** Those consumers read the structured result, which carries the fields; specifying their rendering is out of scope.

## Dependencies

- **Spec 047 (Resilient Criteria Extraction, merged in v1.52.1)**: provides the typed `ExtractionOutcome` and the invariant that only genuine extraction results are persisted as cacheable sources. Part A of this work relies on that invariant to claim that "entry present ⇒ affirmed extracted." Without Spec 047, the invariant would not hold and the persisted outcome field could not be defaulted to affirmed-extracted safely.
- **Independent of Specs 049 (From-Description Write & Index Parity) and 050 (From-Description Criteria Injection).** Can land in parallel.
