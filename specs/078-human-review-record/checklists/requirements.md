# Specification Quality Checklist: Human Review Records & Reviewer Feedback

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-28
**Last validated**: 2026-08-28 (after Q3 revision A → B)
**Feature**: [spec.md](../spec.md)
**Validation iterations run**: 4

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

**Status: all 16 items pass. Spec is ready for `/speckit.plan`.**

## Structural integrity (re-verified after the Q3 revision)

| Check | Result |
|---|---|
| Functional requirement numbering | FR-001 – FR-025, contiguous, no gaps or duplicates |
| Success criteria numbering | SC-001 – SC-011, contiguous |
| Dangling cross-references | none — every `FR-0xx` and `SC-0xx` mentioned in prose resolves to a defined requirement |
| Design decisions | 8 defined; Decisions 2–8 cross-referenced from requirements, scenarios, or edge cases |
| Unresolved markers | zero (`NEEDS CLARIFICATION`, `awaiting response`, `TODO`, `TBD`) |
| Mandatory template sections | all three present and in template order |
| Coverage counts | 4 user stories, 21 acceptance scenarios (7 in User Story 2), 13 edge cases |

Numbering shifted three times across the specification round as FR-010, FR-021, and FR-011 were
inserted. All cross-references were rewritten each time and re-verified above.

## Notes

### Clarifications resolved

| # | Question | Answer | Landed as |
|---|----------|--------|-----------|
| Q1 | What counts as an unusable reviewer identity? | **A** — accept whatever resolves; no failure path | Decision 6, FR-020, FR-021, FR-022 |
| Q2 | Does an earlier review record survive a repair? | **A** — overwrite with the requester's record | Decision 7, FR-013, US4 scenario 5 |
| Q3 | Auto re-raise vs. prior human acceptance? | **B** — suppress when the review post-dates the last verification *(revised from A)* | Decision 8, FR-010, FR-011, US2 scenarios 4–7 |

**Q3 revision (A → B), 2026-08-28.** Every location written around the original answer was
rewritten, not patched: Decision 8, FR-010 (now *requires* the timestamp comparison it previously
forbade), User Story 2 scenarios 4–7, three edge cases, and SC-010. Two additions were needed that
option B's summary did not anticipate:

- **FR-011 (new)** — a suppressed re-raise must be observable in the listing and audit output,
  naming the review that suppressed it. Without this, suppression converts a visible queue item into
  no signal at all, which is worse than either answer to Q3.
- **SC-011 (new)** — makes that observability measurable.

Two requirements from the original input remain **dropped** as a consequence of Q1 → A, recorded in
the spec's Resolved Clarifications section: the interactive identity prompt, and the
`--no-interaction` unresolvable-identity failure criterion. Both were unreachable.

### Accepted consequences carried into planning

**Decision 8 — timestamp suppression.** The previous entry here (reviewers re-deciding accepted
tests on every pass) no longer applies. The real trade-offs of suppression are three, and all are
recorded in Decision 8 in the spec:

1. **The comparison baseline is load-bearing, and getting it wrong silently reverts to option A.**
   The comparison must use the verification timestamp *already on the test*, not the one the pass is
   about to write. Against the latter, a review could never be newer and suppression would never
   fire — the feature would look implemented and behave exactly like Q3 → A. FR-010 pins this down
   explicitly; a plan or test that does not exercise it is not covering the requirement.
2. **Suppression lasts exactly one pass per review event, not indefinitely.** A suppressing pass still
   advances the verification timestamp, because that update is required by FR-003 and User Story 2
   scenario 2. The next pass therefore sees verification as newer and re-raises. A review defers a test
   past the next verification run, not forever — and a second deferral requires a **second review** to
   refresh `reviewed_at` past the `verified_at` the prior pass wrote. Absent that, no sequence of passes
   suppresses the same review twice; there is consequently no suppressed-to-suppressed transition, and
   the re-review path returns through the *cleared* state. This is inherent to comparing clocks rather
   than substance. The durable variant — suppress while the assessment itself is unchanged (same
   verdict, same non-grounded findings as the reviewer saw) — does not expire, because it tracks what
   the human actually ruled on. It needs no field beyond what this spec introduces and is the
   recommended follow-up if one-pass deferral proves too weak.
3. **A review can go stale.** Documentation can change with no verification pass running afterward,
   so nothing re-raises the test and the review stands on a basis that has moved. Under Q3 → A this
   was impossible. FR-011's visibility requirement limits the damage but does not remove it.

**Decision 6 — identity resolution.** Unchanged by this revision: a shared or automation machine
account can be recorded as the reviewer and nothing will object. Tightening this later is small and
self-contained.

### Validation iteration log

**Iteration 1** — two items failed:

1. *All functional requirements have clear acceptance criteria* — the listing requirement had no
   acceptance scenario. **Fixed**: added User Story 1, scenario 5.
2. *No implementation details* — Appendix A names internal code locations. **Resolved as
   deliberate**: the input asked that two assumptions be verified against code, and that
   verification produced three further findings that change what the plan must build. Confining them
   to one labelled appendix, with all functional requirements stated behaviourally, keeps the
   requirements independently testable while preserving the findings.

**Iteration 2** — all items pass except the clarification item; three questions raised.

**Iteration 3** — Q1/Q2/Q3 answers (A/A/A) folded in; all 16 items pass.

**Iteration 4** (after Q3 revised to B) — full re-run of all 16 items, not a partial check. All pass.
Items the revision touched, re-checked individually:

- *Requirements are testable and unambiguous* — passes, and is **stronger** than before. FR-010 now
  states the comparison, its baseline, and all three outcomes (review newer, verification newer,
  equal) rather than declaring no comparison happens.
- *Success criteria are measurable* — passes. SC-010 was reframed from "re-raised but shows the
  prior note" (which asserted the opposite of suppression) to "zero accepted tests return to the
  needs-attention list after one verification pass". SC-011 added for suppression visibility.
- *Edge cases are identified* — passes, and grew from 11 to 13. The equal-timestamp boundary, the
  fast-follow pass, and documentation-changing-without-verification are each now stated with their
  resolved behaviour.
- *All acceptance scenarios are defined* — passes. User Story 2 went from 4 scenarios to 7, covering
  both branches of the comparison, the equality boundary, and suppression visibility.
- *No [NEEDS CLARIFICATION] markers remain* — passes. Grep confirms zero markers; no text asserting
  "automatic marking always wins" survives anywhere in the document.
- *Feature meets measurable outcomes* — passes. FR-011 and SC-011 were added specifically so
  suppression is not an unmeasurable behaviour.
- The remaining nine items are unaffected by the Q3 change and were re-confirmed unchanged.

### Deliberate deviations from the input

Recorded so the plan phase does not treat them as oversights:

- **FR-016 / FR-019 add scope the input did not account for.** The input's requirements are written
  against a `--action accept` argument that does not exist; accept and delete are today reachable
  only as keystrokes in an interactive loop. A chat-driven flow cannot be built on that surface, so a
  non-interactive action surface is in scope. See spec Appendix A, Finding 4.
- **FR-008 targets a different code path than the input proposed.** The validation rule the input
  asked to relax has zero callers, so relaxing it would change nothing. The real enforcement point is
  the mechanical derivation of the awaiting-review marker. See Appendix A, Finding 3.
- **Both of the input's §3.1 open assumptions are resolved in the spec, not deferred to
  `/speckit.plan`.** See Appendix A, Findings 1 and 2. Finding 2 also corrects the input's premise:
  the `Spectra.MCP` project no longer exists.
- **FR-006 (note fidelity) is new.** The input did not address notes containing quotation marks,
  colons, or line breaks, which the current single-line write path would corrupt.
- **FR-011 (suppression visibility) is new**, added by the Q3 → B revision.
- **FR-010's comparison gained a pre-write `flagged_for_review == false` clause** after the Q3 → B
  revision, found while checking the transition table's reachability. Without it, a verification pass
  would suppress a flag a human had deliberately raised via `feedback` — that action writes a fresh
  `reviewed_at` and leaves `verified_at` alone, so the timestamp comparison alone passes. Only a human
  *clearance* is suppressible, never a human *raise*. `ingest-grounding.md` invariant 9 is the
  regression guard.

### Requirements without a user-scenario acceptance criterion

FR-023 (guardrail rewording) and FR-025 (documentation updates) are documentation-artifact
requirements verified by inspection of the resulting files, not by a user scenario. This mirrors how
the input framed its own guardrail criterion and is not a coverage gap.

### Next phase

`/speckit.plan`. No clarification round needed — `/speckit.clarify` would find nothing outstanding.
The plan should treat FR-010's comparison baseline as its highest-risk item, per consequence 1 above.
