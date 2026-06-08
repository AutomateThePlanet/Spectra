# Feature Specification: Boundary-coverage gap detection (analysis phase)

**Feature Branch**: `062-boundary-coverage-gaps`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "Spec 063 — Boundary-coverage gap detection (analysis phase). Flag boundary/edge-case coverage gaps at generation time, via the analysis phase — not the grounding critic. Grounds: docs/investigation/queued/048-critic-boundary-values.md (verdict SURVIVES-WITH-REWRITE, seam (b) = analysis phase)."

> **Numbering note**: The source request labeled this "063" assuming the provider/SDK retirement landed as spec 060. The repository's next available number is **062**, so this feature is **062-boundary-coverage-gaps**. The "048" in the grounding investigation is a *concept label* only — the real spec 048 is the unrelated `criteria-coverage-guards`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Surface missing boundary cases before they are generated (Priority: P1)

A developer runs behavior analysis over a suite's documents and acceptance criteria (and, where relevant, the existing tests for that suite). The source material implies boundary conditions — a field with a maximum length, a numeric range with min/max limits, a value that may be empty or null, an operation that can time out or overflow. The analysis output explicitly names the boundary conditions that *ought* to be tested but are **not** covered by the planned or existing tests, so the developer can choose to generate those edge cases instead of discovering the gap late (in QA, in production, or never).

**Why this priority**: This is the entire point of the feature — turning under-testing of edges into a visible, actionable signal at the moment generation is being planned. Without it, the other stories have nothing to validate or display. Delivered alone, this is a viable MVP: the developer sees the gaps and acts on them.

**Independent Test**: Run analysis against source material that implies at least one boundary condition (e.g., "username must be 3–20 characters") with no existing test covering that boundary. Confirm the analysis output identifies the uncovered boundary as a gap, naming the field/condition. This is fully testable in isolation and delivers the core value.

**Acceptance Scenarios**:

1. **Given** criteria/docs implying boundary conditions (ranges, limits, empty/null, timeouts, overflow, off-by-one), **When** analysis runs, **Then** the output identifies boundary-coverage gaps that are not addressed by the planned or existing tests, each naming the specific condition and source.
2. **Given** source material with **no** boundary conditions present, **When** analysis runs, **Then** **no** boundary-coverage gaps are reported — no spurious "missing boundary" noise.
3. **Given** a boundary condition that **is** already covered by an existing or planned test, **When** analysis runs, **Then** that boundary is **not** reported as a gap.

---

### User Story 2 - Boundary gaps are carried through the typed result, fail-loud (Priority: P2)

When the analysis output is ingested, the boundary-coverage gaps are validated and carried in the typed analysis result alongside the existing technique breakdown. A well-formed gap payload is preserved end-to-end; a malformed boundary-gap payload causes a specific, loud failure rather than being silently dropped.

**Why this priority**: The signal from Story 1 is only trustworthy if it survives ingestion intact and if corruption is surfaced rather than swallowed. This protects the developer from a false sense of "no gaps" when the truth is "the gap data was malformed and discarded." It depends on Story 1 producing the gaps, hence P2.

**Independent Test**: Feed the ingest step an analysis payload containing a well-formed boundary-gap list and confirm the typed result exposes those gaps. Then feed a malformed boundary-gap payload and confirm ingestion fails with a specific error identifying the boundary-gap problem, not a silent drop or a generic parse error that hides it.

**Acceptance Scenarios**:

1. **Given** an analysis payload with a well-formed list of boundary-coverage gaps, **When** the analysis output is ingested, **Then** the gaps are carried in the typed result alongside the technique breakdown, with each gap's fields preserved.
2. **Given** an analysis payload whose boundary-gap section is malformed, **When** the analysis output is ingested, **Then** ingestion fails loud with a specific error attributable to the boundary-gap payload — never a silent drop.
3. **Given** an analysis payload with no boundary-gap section at all (e.g., legacy output), **When** ingestion runs, **Then** the result has an empty boundary-gap set and ingestion still succeeds (additive, backward-compatible).

---

### User Story 3 - Advisory only; the grounding critic stays untouched (Priority: P3)

The boundary-gap output is purely informational: it informs what the developer might generate next, but it never silently alters the generated tests and never blocks generation. Separately, the grounding critic remains entirely unchanged — it still judges only grounding (does each claim trace to the documents), with its existing verdict vocabulary, and gains no completeness or boundary dimension.

**Why this priority**: These are guardrails that protect the existing architecture's clean separation (grounding vs. completeness) and the developer-driven generation flow. They are essential to "do no harm" but produce no new user-visible capability on their own, hence P3.

**Independent Test**: (a) Run a generation flow where analysis reports boundary gaps and confirm the set of generated tests is identical to a run with the gaps suppressed, and that a non-empty gap list does not change the exit/blocking behavior. (b) Run the critic before and after this feature and confirm its verdict vocabulary and ingest behavior are byte-for-byte the same — the critic test corpus stays green.

**Acceptance Scenarios**:

1. **Given** analysis reports one or more boundary-coverage gaps, **When** generation proceeds, **Then** the generated tests are not silently mutated and generation is not blocked by the presence of gaps.
2. **Given** this feature is in place, **When** the critic runs, **Then** the critic is unchanged — still grounding-only, same verdict vocabulary, no completeness/boundary dimension added to its contract or its ingest boundary.

---

### Edge Cases

- **Ambiguous boundary**: source implies a limit but not its exact value (e.g., "reasonable maximum"). The gap may be reported as a named condition without a precise value, but MUST NOT invent a specific numeric boundary that the source does not state.
- **Boundary partially covered**: e.g., the max is tested but the min is not. Only the uncovered side is reported as a gap.
- **Existing-suite-only run**: analysis runs over existing tests without new planned tests. Gaps are computed against whatever coverage is known; absence of planned tests does not suppress detection.
- **Large gap list**: many implied boundaries across many fields. Output remains advisory and ordered/grouped so it is readable, not truncated silently. If any cap is applied, the omission is surfaced, not hidden.
- **Legacy analysis output**: output predating this feature (no boundary-gap section) ingests cleanly as "no gaps reported."
- **Conflicting source statements**: two documents imply different limits for the same field. Both implied boundaries surface (or are reported as conflicting), rather than one silently winning.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Boundary-coverage gap detection MUST live in the **analysis phase** (the compile-analysis-prompt → in-session analysis → ingest-analysis seam), not in the grounding critic. The grounding critic's contract, verdict vocabulary, prompt builder, and verdict ingest boundary MUST remain untouched.
- **FR-002**: The analysis prompt MUST instruct the in-session analysis to identify boundary/edge conditions implied by the documents and acceptance criteria — including min/max, off-by-one, empty/null, overflow, and timeout — and to flag those not covered by the planned or existing tests. This MUST extend the existing Boundary Value Analysis reasoning already present in the analysis technique set, not bolt on a separate parallel path.
- **FR-003**: The analysis result MUST carry the boundary-coverage gaps as a typed field alongside the existing technique breakdown. The ingest step MUST validate the boundary-gap payload fail-loud: a malformed payload produces a specific error attributable to the boundary gaps, never a silent drop. A missing boundary-gap section (legacy/empty output) MUST be treated as "no gaps" and MUST NOT fail ingestion.
- **FR-004**: Detection MUST be conservative about false positives. A gap MUST be reported only where a boundary condition is actually implied by the source material; speculative "you might want to test X" suggestions that are not grounded in an implied boundary MUST NOT be emitted. A boundary already covered by an existing or planned test MUST NOT be reported as a gap.
- **FR-005**: The boundary-gap output MUST be advisory/informational. It MUST NOT silently alter the generated tests and MUST NOT block generation. The presence of gaps MUST NOT change exit/blocking semantics of the generation flow.
- **FR-006**: Each reported boundary-coverage gap MUST be self-describing enough to act on — at minimum identifying the field/parameter or behavior, the kind of boundary (e.g., max-length, min/max range, empty/null, overflow, timeout, off-by-one), and the source it was implied by — so the developer can decide whether to generate the corresponding test.
- **FR-007**: When the analysis recommendation is presented to the developer, the boundary-coverage gaps MUST be surfaced alongside the existing category and technique breakdowns (not hidden in raw output), so the gap signal is visible at the same point the developer reviews what will be generated.

### Key Entities *(include if feature involves data)*

- **Boundary-coverage gap**: A single uncovered boundary condition surfaced by analysis. Key attributes: the field/parameter or behavior it concerns, the boundary kind (min/max, off-by-one, empty/null, overflow, timeout), a short description of the missing edge, and the source document/criterion that implies it. Has no persisted lifecycle — it is advisory output of one analysis run.
- **Analysis result (recommendation)**: The existing typed output of the analysis phase, carrying coverage counts, the category breakdown, and the per-ISTQB-technique breakdown. This feature adds the boundary-coverage gap set to it as an additive, typed field.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For source material that implies at least one uncovered boundary condition, analysis reports that boundary as a gap in 100% of the defined detection test cases (min/max, off-by-one, empty/null, overflow, timeout).
- **SC-002**: For source material with no boundary conditions, analysis reports zero boundary-coverage gaps — no false positives in the no-condition test cases.
- **SC-003**: A malformed boundary-gap payload fails ingestion with a specific, attributable error in 100% of the malformed-input test cases; a missing boundary-gap section never fails ingestion.
- **SC-004**: The set of generated tests, and whether generation is blocked, is identical with and without boundary gaps present — confirming the output is advisory and non-mutating.
- **SC-005**: The grounding critic test corpus and the `Spectra.Core` test corpus remain unchanged and fully green after this feature lands — confirming grounding and completeness stay cleanly separated.

## Assumptions

- The "developer" persona is the user running the Spectra generation/analysis flow in the interactive session; there is no separate end-user UI for this signal.
- The analysis phase runs in the interactive session (no in-process model, no external generation SDK), so this feature is **not blocked** by the pending provider/SDK retirement.
- The boundary-gap field is **additive** to the existing analysis result shape; existing consumers that ignore it continue to work, and legacy analysis output (without the field) remains valid input to ingestion.
- "Covered by planned or existing tests" is judged from the coverage context already assembled for the analysis phase (existing tests for the suite plus the behaviors planned in the same analysis run); no new coverage data source is introduced.
- This feature surfaces the gap only; actually generating the missing boundary tests remains the developer-driven generation flow (explicitly out of scope below).

## Dependencies

- No hard dependencies. Extends the existing analysis-phase plumbing.
- Independent of the other queued post-migration features.
- Not blocked by the provider/SDK retirement (the analysis phase already runs model-free on the CLI side and in-session for the model step).

## Out of Scope

- Any change to the grounding critic — adding a completeness/boundary dimension to the critic (seam (a)) is explicitly rejected. The critic, its prompt builder, and its verdict ingest boundary are untouched by design.
- Auto-generating the missing boundary tests as a forced step. This feature surfaces the gap; generation remains user-driven.
- Persisting boundary gaps, tracking them across runs, or reporting on their resolution over time.
- The other three queued post-migration features (independent of this one).
