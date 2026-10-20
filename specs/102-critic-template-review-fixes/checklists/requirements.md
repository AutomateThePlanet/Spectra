# Specification Quality Checklist: Review Fixes for 101 and the Critic Prompt Template

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-19
**Feature**: [spec.md](../spec.md)

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

## Notes

- Like 084–101, the spec names CLI commands, flags, skill and agent files and their result lines: they are the
  product's user-facing interface. Unlike a feature spec, this is a review-fix spec, so it also names the classes where
  each finding was found (`CriticPromptBuilder`, `CriticSourceLoader`, `SourceRefResolver`, `CriticPromptCompiler`).
  They locate the defect; the spec prescribes outcomes, not a design (e.g. F2 asks for "the resolver's comparer, one
  source of truth", not a particular API).
- Every fact in the request was re-checked on `26bfe88` while writing the spec, and one was added: the bundled
  template's body contains every line of the hard-coded string (comments aside), so F1 adds checks and loses none.
- Six decisions were made in the spec rather than left open (see "Decisions"): one critic prompt (the fallback's fate
  decided in planning), placeholders kept, unchanged repairs keep their verdict with the existing retry rule, the
  `--repaired` list keeps unchanged ids, the `created:` keyword, and the tool name from the current docs.
  `/speckit.clarify` can revisit them. The F3 decision is the one deliberate outcome change (101 FR-026, unchanged
  repairs only).
- Validated in one pass; no items failed.
- Analysis remediation 2026-10-20 (all eleven findings accepted). Spec changes:
  - FR-005 / US1-4: a new golden `full-docs-102.txt`, with `full-docs-6a4c41a.txt` kept;
  - FR-010 / Decisions / US3-7: the fallback runs the critic only for `written` ids;
  - FR-019: F6 is checked by the compiler, with no test;
  - SC-004: the text rows are the merge criterion; the live run is optional;
  - US5-4: the block's end marker;
  - Out of Scope: the review-flagged `tools:` line.

  Tasks gained the 16,000-character size rule for `spectra-generate` (C1), a placeholder row, a CLI end-to-end row for
  F2, and the updater's two existing line forms. All items still pass.
- Second analysis 2026-10-20 (three findings accepted):
  - C2 supersedes C1. The F3/F4 routing stays in SKILL.md, written short, because reference.md is never read during
    Steps 7–9. If the skill is over the cap, space is freed only outside Steps 7–9 (the Key rule's explanation first,
    then "Analyze first").
  - G3: a test row for the update skill's repair fallback.
  - U3: the size and placement rule is in Assumptions and referenced from FR-020.

  All items still pass.
- Third analysis 2026-10-20 (C3, L2 accepted):
  - T018 and T022 now prescribe exact edits (≈ +95 characters, with T029). There is no separate keyword-rule line and no
    separate unchanged-repair sentence.
  - The target is ≤ 15,900. The freeing steps are the Key rule explanation (~200), then the "(Spec …)" annotations (~90);
    "Analyze first" is not touched.
  - The numbers are recorded in research R4.

  All items still pass.
- Fourth analysis 2026-10-20 (C4 accepted). An in-memory dry run showed the C3 wording reaching ≈ 16,030 characters,
  over the cap.
  - Freeing now comes first, inside T018: the Key rule (−128), then the "(Spec …)" annotations (−86), for 15,636.
  - The fallback bullet is shorter; T018 is ≈ +140, for a final ≈ 15,791 against a target of ≤ 15,900.
  - T020, T023 and T031 record the size. T031 stops and reports if the size is over 15,900.

  All items still pass.
