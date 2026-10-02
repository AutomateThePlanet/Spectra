# Specification Quality Checklist: Source Change Baseline (Phase 2a)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-01
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

- This is a CLI feature. Command names, flags, file paths, JSON fields and error codes are the user-facing interface,
  so they appear in the spec, as in 078–083. Internal types are named only where the decision text names them
  (`TextNormalizer`, `TestClassifier`, the source-ref resolver).
- D1–D8 are settled, so no clarification markers were raised. Five interpretations that the decision text implies but
  doesn't spell out are recorded under Assumptions:
  - the fingerprint is computed over raw text, and the snapshot is named by it;
  - line endings and BOM are normalised;
  - words changed is the larger of the removed and added counts;
  - what `no_tests` measures;
  - group IDs are run-scoped, with a staleness check.
- During planning, FR-020 (`no_tests`) changed from the `docs changed` signal to the baseline's own CHANGED refs. The
  first signal is refreshed by the criteria workflow before update triage runs, so it would almost always be empty
  (research R7).
