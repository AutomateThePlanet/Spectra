# Feature Specification: Critic Arithmetic Mandate, Index-Writer Path Fix, Skill Failure-Branch Guard

**Feature Branch**: `074-fix-arithmetic-index-skill`  
**Created**: 2026-06-22  
**Status**: Draft  
**Spec Number**: 074

## Overview

Three independent root causes were confirmed in a live audit of the unit-converter generation run. This spec closes all three, plus two required cleanup steps:

1. **Critic arithmetic gap** — the critic approves computed-but-wrong expected values as `grounded` because it verifies only that the underlying *principle* is documented, never that the *number* is correct. TC-107 (`1×10⁻⁹ km → 1E-9 nm`, actual answer `1000 nm`) slipped through at score 0.9. This is silent and undermines the grounded-critic gate, SPECTRA's core correctness guarantee.

2. **Index-writer path poisoning** — on a second generation round, the writer stores suite-relative paths (`unit-converter\TC-100.md`) instead of bare filenames (`TC-100.md`), which causes path-doubling in every consumer (`test-cases/unit-converter/unit-converter/TC-100.md`). The 073 fix patched three readers; this spec fixes the writer.

3. **Skill has no failure branch for `written: 0`** — when batch grounding-ingest returned `written: 0` (caused by root cause #2), the skill produced no anomaly signal. The agent improvised by manually editing 21 `.md` files instead of stopping, producing synthetic timestamps and defeating the verification trail.

Cleanup steps required by the above: regenerate the poisoned unit-converter index (it does not self-heal), and sweep the 56 grounded tests for other TC-107-class arithmetic errors.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Critic rejects a wrong computed value (Priority: P1)

A team member generates unit-converter tests and submits them through the critic gate. One test has an arithmetically wrong expected result (`1×10⁻⁹ km → 1E-9 nm`; correct answer is `1000 nm`). Under the current prompt the critic approves it as `grounded 0.9`. Under the new mandate, the critic must detect the arithmetic error and return a verdict of NOT `grounded`, with the specific numeric finding surfaced.

**Why this priority**: Silent false-groundeds undermine the grounded-critic gate — SPECTRA's core correctness differentiator. A test that passes the gate but carries a wrong expected value ships to QA and produces false failures or false passes in execution. This is the highest-severity finding because it is invisible until execution.

**Independent Test**: Can be fully tested by re-critiquing TC-107 under the updated prompt and confirming the verdict is not `grounded`, with the arithmetic error named in a finding. No code changes needed; the critic prompt is the sole artifact.

**Acceptance Scenarios**:

1. **Given** TC-107 (`1×10⁻⁹ km → expected 1E-9 nm`, correct `1000 nm`) exists in the unit-converter suite, **When** the critic verifies it under the new arithmetic mandate, **Then** the verdict is NOT `grounded` and a finding names the arithmetic error (expected `1E-9 nm`, computed `1000 nm`).

2. **Given** TC-125 (`−459.67°F → 0 K`, arithmetically correct), **When** the critic verifies it under the new mandate, **Then** the verdict remains `grounded` — no false positives introduced.

3. **Given** any test with a computed expected result (unit conversion, formula, derived constant), **When** the arithmetic is wrong by any magnitude, **Then** the critic flags it regardless of whether the underlying principle is documented.

---

### User Story 2 — Index stays bare after repeated generation rounds (Priority: P2)

A team member runs generation on the unit-converter suite a second time (adding new tests to an existing suite). The index writer must produce bare filenames (`TC-100.md`) for both new and re-ingested existing tests, consistent with the first-round convention. Path-doubling must not occur.

**Why this priority**: Suite-prefixed paths in `_index.json` break audit-grounding, compile-repair-batch, and ingest-grounding for every test in the suite — a hard block on the grounding and repair workflows. The 073 consumer fixes treated the symptom; this fixes the source so the symptom cannot recur.

**Independent Test**: Can be fully tested by running a second-round generation on any suite and inspecting the resulting `_index.json` — all `file` fields must be bare filenames with no directory segment.

**Acceptance Scenarios**:

1. **Given** a suite generated once (index holds `"TC-100.md"`), **When** a second generation round re-ingests existing tests and adds new ones, **Then** all `file` fields in `_index.json` remain bare (`"TC-100.md"`), never `"unit-converter\TC-100.md"`.

2. **Given** the unit-converter suite (currently poisoned with suite-relative paths), **When** the index is regenerated after the writer fix, **Then** `audit-grounding` resolves paths correctly and reports `grounding_written: true` for tests whose `.md` files contain grounding blocks.

3. **Given** a test updated via `ingest-update`, **When** the index is written, **Then** the `file` field for the updated test is bare — the update path does not re-introduce suite-relative paths.

---

### User Story 3 — Skill stops when batch grounding-ingest returns zero writes (Priority: P3)

After the post-8a batch `ingest-grounding --suite {s} --all` call, if the result reports `written: 0` while the kept-grounded count is nonzero, the generation skill must stop and report the anomaly. The agent must not proceed to phase 8b and must not edit `.md` files by hand.

**Why this priority**: The failure mode (agent improvises manual edits) is a process-integrity hazard — it produces synthetic timestamps, bypasses the verification trail, and goes undetected until an explicit audit. The fix is a two-line guard in the skill; the cost of not adding it is a corrupted grounding trail on every affected run.

**Independent Test**: Can be fully tested by forcing a `written: 0` response (e.g. by running against a suite with no grounded verdicts pending) and confirming the skill emits a STOP+report message and halts without proceeding to 8b or editing any `.md` files.

**Acceptance Scenarios**:

1. **Given** batch `ingest-grounding --all` returns `written: 0` while the kept-grounded count from the batch is nonzero, **When** the skill processes the result, **Then** it emits a STOP+anomaly diagnostic and does NOT proceed to phase 8b.

2. **Given** the skill has stopped on a `written: 0` anomaly, **When** the agent reviews the output, **Then** no `.md` files have been manually edited, no verdict fields have been rewritten by hand, and the run log contains a clear diagnostic of the anomaly.

3. **Given** `written != kept-grounded count` (any mismatch, not just zero), **When** the skill processes the result, **Then** it surfaces the mismatch as a warning (not necessarily a hard stop, but clearly reported).

---

### User Story 4 — Poisoned unit-converter index heals (Priority: P4)

The unit-converter `_index.json` currently holds suite-relative paths due to prior second-round ingestion. After the writer fix lands, the index must be regenerated so all `file` fields become bare. This is required because `ApplyEdit` re-persists the existing path — the index does not self-heal.

**Why this priority**: Without explicit regeneration, the poisoned index persists even after the writer fix, meaning `audit-grounding` continues to report 0 written blocks for all 64 tests whose `.md` files contain grounding blocks. This blocks the Phase 4 gate.

**Independent Test**: Can be fully tested by running `spectra docs index` (or the index-rebuild command) on the unit-converter suite after the writer fix, then running `audit-grounding` and confirming `grounding_written: true` for all tests with grounding blocks.

**Acceptance Scenarios**:

1. **Given** the unit-converter index holds suite-relative paths, **When** the index is regenerated after the writer fix, **Then** all `file` fields are bare filenames.

2. **Given** the regenerated index, **When** `audit-grounding` is run on the unit-converter suite, **Then** it reports `grounding_written: true` for all 64 tests whose `.md` files contain the grounding block (not 0).

---

### User Story 5 — Arithmetic sweep catches remaining TC-107-class errors (Priority: P5)

After the critic arithmetic mandate lands, all 56 grounded tests are re-critiqued. Tests whose expected result is a computed value are evaluated for arithmetic correctness under the new mandate. Any newly-caught errors are flagged for repair or drop — they are not silently kept.

**Why this priority**: TC-107 is the known reproduction case, but the same class of error (wrong computed value passes `grounded`) may exist in other tests. The sweep closes the blast radius of root cause #1.

**Independent Test**: Can be fully tested by running re-critic on the 56 grounded tests and producing a list of: test id, computed value, re-critic verdict. TC-107 must appear as NOT grounded.

**Acceptance Scenarios**:

1. **Given** 56 grounded tests, some with computed expected values, **When** they are re-critiqued under the new arithmetic mandate, **Then** TC-107 appears in the NOT-grounded list with the arithmetic error named.

2. **Given** any other test with a wrong computed expected value found by the sweep, **When** the sweep completes, **Then** it is flagged for repair or drop — not silently retained as grounded.

3. **Given** tests with correct computed expected values, **When** the sweep runs, **Then** they remain grounded — no false positive regressions.

---

### Edge Cases

- What happens when `written: 0` because there are genuinely no pending grounded verdicts (not an error)? The skill must distinguish this from an anomaly — the guard fires only when `written: 0 AND kept-grounded > 0`.
- What happens when `ingest-grounding --all` partially writes (e.g. `written: 3` but `kept-grounded: 5`)? The mismatch must be surfaced as a warning.
- What happens if a test has a computed value that cannot be verified by the model (e.g. obscure constant)? The critic should flag it as `unverified` rather than silently grounding.
- What happens if the unit-converter index regeneration fails mid-run? The index must not be left in a partially-written state — fail loud and preserve the previous state.
- What happens when `IngestUpdateCommand` processes a test that was written with a bare path? The bare path must be preserved through the round-trip (not re-introduced as suite-relative).

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The critic verification prompt MUST include an explicit arithmetic mandate: when an expected result is a computed value (unit conversion, formula output, scientific-notation magnitude, derived constant), the critic MUST compute the value independently and compare it to the test's asserted value. A wrong computed value MUST result in a NOT-grounded verdict, even if the underlying principle is documented.

- **FR-002**: The critic arithmetic mandate MUST be additive to existing doc-presence rules: both conditions must hold — the principle must be in documentation AND any asserted computed number must be arithmetically correct.

- **FR-003**: The index writer for the ingest-tests path MUST store bare filenames (`TC-100.md`) in `_index.json`, using a path relative to the suite directory — not relative to the tests root — when re-ingesting existing tests on second and subsequent generation rounds.

- **FR-004**: The index writer for the ingest-update path MUST store bare filenames using the same bare-path convention as the ingest-tests path, so updates do not re-introduce suite-relative paths.

- **FR-005**: The generation skill MUST add a failure-branch check after the post-8a batch `ingest-grounding --all` call: if `written: 0` AND kept-grounded count > 0, the skill MUST stop and emit a diagnostic. The agent MUST NOT proceed to phase 8b and MUST NOT edit `.md` files by hand as a workaround.

- **FR-006**: The generation skill failure-branch MUST restate the non-stop contract: a broken or zero-result CLI verb is a STOP signal, never a license to perform the work by hand. Manual `.md` editing and verdict rewriting are prohibited regardless of how blocked the agent is.

- **FR-007**: After the writer fix (FR-003) lands, the unit-converter `_index.json` MUST be regenerated so all `file` fields are bare. This regeneration step is mandatory — the index does not self-heal.

- **FR-008**: After FR-001 lands, the 56 grounded tests MUST be re-critiqued under the new arithmetic mandate. Any newly-caught arithmetic errors MUST be flagged for repair or drop per normal disposition; they MUST NOT be silently retained as grounded.

- **FR-009**: The three 073 consumer fixes (`AuditGroundingHandler`, `CompileRepairBatchCommand`, `IngestGroundingCommand`) MUST remain unchanged. They become correct as written once the writer stops poisoning input.

- **FR-010**: Documentation MUST note: (a) the critic arithmetic mandate, (b) the canonical bare-filename convention for `_index.json` so future writers do not re-introduce suite-relative paths, and (c) the skill failure-branch and non-stop contract.

### Key Entities

- **Test index (`_index.json`)**: Records each test case's id, title, priority, tags, components, and `file` path. The `file` field MUST be a bare filename (`TC-100.md`), never a suite-relative path. This is the canonical lookup key for all consumers (audit-grounding, compile-repair-batch, ingest-grounding).

- **Critic verification prompt**: The prompt template that instructs the critic subagent on what to verify. Extended by this spec with an arithmetic mandate section.

- **Grounding block**: YAML frontmatter section written into a test `.md` file by `ingest-grounding`, containing `verdict`, `score`, `verified_at`. The block's presence is detected by `audit-grounding` using the resolved absolute path — which depends on the `file` field being bare.

- **Batch grounding result**: The JSON output of `ingest-grounding --all`, containing `written` count and other diagnostics. The skill must inspect this output before proceeding.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Re-critiquing TC-107 under the new arithmetic mandate returns a NOT-grounded verdict with the arithmetic error (`1E-9 nm` vs correct `1000 nm`) explicitly named in a finding.

- **SC-002**: Re-critiquing a known-correct computed test (e.g. TC-125, `−459.67°F → 0 K`) under the new mandate continues to return `grounded` — zero false positives introduced by the mandate.

- **SC-003**: After a second-round generation on any suite, all `file` fields in `_index.json` are bare filenames with no directory separator — verified by inspection of the written index.

- **SC-004**: After index regeneration on the unit-converter suite, `audit-grounding` reports `grounding_written: true` for every test whose `.md` file contains a grounding block (target: 64 of 64, not 0).

- **SC-005**: A forced `written: 0` from `ingest-grounding --all` (with nonzero kept-grounded count) causes the generation skill to emit a STOP diagnostic and halt — no `.md` files are edited by hand and no verdict fields are rewritten.

- **SC-006**: The arithmetic sweep of the 56 grounded tests produces a complete list of tests with computed expected values and their re-critic verdicts. TC-107 appears on the NOT-grounded list. Any other newly-caught errors are flagged for repair/drop.

- **SC-007**: After all phases land, a full unattended generate+repair run on a clean suite completes with zero `written: 0` anomalies, zero manual `.md` edits, and an accurate grounding trail.

---

## Phases & Implementation Order

### Phase 1 — Critic arithmetic mandate (FR-001, FR-002)
Independent of all code changes. Highest severity (silent false-groundeds). Do first.

**Gate**: SC-001 and SC-002 pass. TC-106-class errors continue to be caught.

### Phase 2 — Index-writer path fix + index regeneration (FR-003, FR-004, FR-007, FR-009)
Two writer lines fixed, then the poisoned unit-converter index regenerated.

**Gate**: SC-003 and SC-004 pass. The three 073 consumer fixes remain UNCHANGED. `Spectra.Core` and `TestPersistenceService` tests pass unmodified.

### Phase 3 — Skill guard + arithmetic sweep + docs (FR-005, FR-006, FR-008, FR-010)
Skill guard added, 56 grounded tests swept, docs updated.

**Gate**: SC-005, SC-006, SC-007 pass.

---

## Assumptions

- The arithmetic mandate is a prompt change only — investigation confirmed the root cause is a prompt gap, not model unreliability. If a future run shows the critic was asked but still failed arithmetic, an out-of-model checker would be warranted (out of scope here).
- Only the unit-converter `_index.json` is confirmed poisoned. Other suites are not pre-emptively migrated; if they are re-ingested, the same FR-007-style regeneration applies.
- The three 073 consumer fixes become correct as written once the writer produces clean input — no fourth consumer patch is needed.
- The 21 manually-edited `.md` files from the prior run (synthetic `14:25:00Z` timestamps) are left untouched until Phase 3; they are evidence of the process hazard.
- TC-107 is left in place until Phase 1 lands; fixing it before the mandate ships would destroy the reproduction case.

## Out of Scope

- Out-of-model deterministic numeric checker (prompt mandate is sufficient per investigation).
- Shared path-resolver helper — the writer is the single source of truth; two lines fix it.
- Reverting the 073 consumer fixes.
- Pre-emptive migration of other suites' indexes.
- Critic model-family change.
