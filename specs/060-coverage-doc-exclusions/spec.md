# Feature Specification: Coverage-Scoped Document Exclusions

**Feature Branch**: `060-coverage-doc-exclusions`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "Let users mark documents out-of-scope for the documentation-coverage percentage, without removing them from generation/analysis."

> **Numbering note**: The originating investigation labeled this concept "050" and assumed the
> provider/SDK retirement would consume number 060, leaving this as 061. The repository's next
> available feature number was **060**, so this spec is 060. The grounding document is
> `docs/investigation/queued/050-coverage-doc-exclusions.md` (a concept label, not the real spec 050,
> which is the unrelated `from-desc-criteria-injection`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Exclude non-testable docs from the coverage percentage (Priority: P1)

A team keeps release notes, changelogs, summaries, and archived material in their `docs/` tree. These
documents are real and should stay indexed and available to generation and analysis, but nobody writes
test cases against them. Today every one of these documents counts in the denominator of the
documentation-coverage percentage, permanently dragging the number down and making the metric
meaningless as a goal.

The user adds a coverage-scoped exclusion pattern (for example, matching `docs/release-notes/**`). The
next coverage run recomputes the percentage over only the documents that are actually supposed to have
test cases. The excluded documents remain fully present everywhere else in the tool.

**Why this priority**: This is the entire feature. Without it there is no way to make the coverage
number reflect the docs that are meant to be tested. Every other story is a guardrail around this one.

**Independent Test**: Configure a coverage-scoped exclusion pattern matching a subset of docs, run
coverage, and confirm the percentage is computed over the remaining (non-excluded) documents while the
excluded documents are still present in the document map used by generation, analysis, and indexing.

**Acceptance Scenarios**:

1. **Given** a coverage-scoped exclusion pattern matching `docs/release-notes/**`, **When** coverage
   runs, **Then** the documents under `docs/release-notes/` are removed from the coverage denominator,
   and the reported percentage is computed over only the remaining in-scope documents.
2. **Given** the same exclusion pattern, **When** generation, analysis, or indexing runs, **Then** the
   excluded documents are still present and processed exactly as before (the exclusion has no effect
   outside the coverage percentage).

---

### User Story 2 - Excluded docs are visibly reported, never silently dropped (Priority: P1)

When a user excludes documents from the coverage percentage, they need to *see* which documents were
excluded. A silently shrinking denominator is indistinguishable from a bug or a lost document. The
coverage output must show excluded documents with a distinct "excluded" status so the change in the
denominator is auditable and intentional.

**Why this priority**: Equal to P1 — the fail-loud principle is a hard requirement, not a nicety. A
coverage number that quietly excludes documents with no trace is untrustworthy and could hide real
gaps. The metric is only credible if every adjustment to it is visible.

**Independent Test**: With a coverage-scoped exclusion configured, render the coverage report and
confirm each excluded document appears in the output with a distinct "excluded" status (not omitted,
not shown as "uncovered").

**Acceptance Scenarios**:

1. **Given** an excluded document, **When** the coverage report renders, **Then** the document appears
   in the report with a distinct "excluded" status, separate from "covered" and "uncovered".
2. **Given** excluded documents, **When** a user inspects the report, **Then** they can determine the
   exact set of documents removed from the denominator and the reason (pattern match), without having
   to diff against a prior run.

---

### User Story 3 - No-config behavior is unchanged (Priority: P1)

Existing workspaces that never configure a coverage-scoped exclusion must see exactly the behavior they
see today: the coverage percentage is computed over the full document map, and the output is unchanged.
The feature must be strictly additive and invisible until opted into.

**Why this priority**: Equal to P1 — a regression in the default coverage number would silently change
every existing user's metric and break the regression net. The safety of the no-config path is what
makes this feature shippable.

**Independent Test**: With no coverage-scoped exclusions configured, run coverage and confirm the
output is byte-for-byte equivalent to current behavior (full-map denominator, no "excluded" entries).

**Acceptance Scenarios**:

1. **Given** no coverage-scoped exclusions configured, **When** coverage runs, **Then** the percentage,
   denominator, and report are identical to current behavior.
2. **Given** a workspace that previously had no exclusions, **When** the feature is deployed but left
   unconfigured, **Then** no coverage number shifts and no "excluded" status appears anywhere.

---

### User Story 4 - The three exclusion concepts are non-confusable (Priority: P2)

Spectra already has two unrelated exclusion mechanisms. A user (or future maintainer) configuring this
new one must be able to tell all three apart by name and documented semantics, and the three must
behave independently — matching a document under one must not affect the others.

- **Discovery exclusion** (existing) — `source.exclude_patterns`, default `["**/CHANGELOG.md"]`. Total
  removal at discovery: an excluded document vanishes from the document map for *everything*
  (generation, analysis, coverage).
- **Analysis-skip exclusion** (existing) — `coverage.analysis_exclude_patterns`. The document is still
  indexed, but its suite is flagged `skip_analysis: true`. Consumed only by the index/migration path —
  **never** by the coverage percentage. A document matched only here still counts against coverage
  today.
- **Coverage exclusion** (this feature, new) — drops a document from the coverage denominator only,
  while it remains in the map for generation, analysis, and indexing.

**Why this priority**: The originating investigation identifies naming confusion — not implementation
difficulty — as the primary risk. Locking the distinct semantics with documentation and a test is what
prevents the new concept from being conflated with the two that already exist.

**Independent Test**: Configure a document so it is matched only by the analysis-skip exclusion (and not
by the coverage exclusion); confirm it still counts in the coverage denominator. Then add it to the
coverage exclusion and confirm it is now excluded — proving the two are independent.

**Acceptance Scenarios**:

1. **Given** a document matched by `coverage.analysis_exclude_patterns` but **not** by the new coverage
   exclusion, **When** coverage runs, **Then** the document still counts in the coverage denominator
   (unchanged from today).
2. **Given** that same document also added to the new coverage exclusion, **When** coverage runs,
   **Then** it is excluded from the denominator and reported with "excluded" status.
3. **Given** a document matched by `source.exclude_patterns`, **When** any command runs, **Then** the
   document is absent from the map entirely (unchanged from today) and never reaches the coverage
   stage.

---

### Edge Cases

- **Excluded document also has linked tests**: A document can match a coverage exclusion *and* have test
  cases referencing it. The document is excluded from the denominator regardless; its status is
  "excluded" (exclusion takes precedence over covered/uncovered classification). The associated tests
  are not affected.
- **Pattern matches every document**: If the configured patterns exclude the entire document set, the
  denominator becomes zero. The percentage follows the existing zero-denominator rule (reported as 0,
  not an error or divide-by-zero), and every document is reported as "excluded".
- **Pattern matches no document**: A configured pattern that matches nothing behaves like the no-config
  case for the denominator, but the pattern is still valid configuration (no error).
- **Empty / whitespace pattern entries**: Blank entries in the pattern list are ignored (consistent
  with existing glob-matcher behavior), not treated as match-everything.
- **Overlap with discovery exclusion**: A document already removed by `source.exclude_patterns` never
  reaches coverage, so a coverage-exclusion pattern that also matches it is a harmless no-op.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a new **coverage-scoped** exclusion pattern list in configuration,
  conceptually `coverage.coverage_exclude_patterns`, that is distinct in both name and semantics from
  the existing `source.exclude_patterns` (total removal) and `coverage.analysis_exclude_patterns`
  (analysis-skip). The chosen name MUST make the three mechanisms non-confusable.
- **FR-002**: The coverage exclusion MUST affect **only** the documentation-coverage denominator.
  Excluded documents MUST remain in the document map and continue to be processed by generation,
  analysis, and indexing exactly as before.
- **FR-003**: Excluded documents MUST be reported in the coverage output with a distinct "excluded"
  status — visibly present, not silently dropped and not misclassified as covered or uncovered — so the
  change to the denominator is auditable.
- **FR-004**: The matcher for coverage exclusions MUST reuse the existing glob-matching component
  (`ExclusionPatternMatcher`, full glob via FileSystemGlobbing) rather than introduce a new matcher. If
  the existing component must be relocated to be reachable from where coverage analysis lives, it MUST
  be relocated (not duplicated), and the relocation MUST be behavior-preserving.
- **FR-005**: With no coverage-scoped exclusion patterns configured, the coverage output MUST be
  equivalent to current behavior — same percentage, same denominator, same rendered report, no
  "excluded" entries. There MUST be no silent denominator shift for unconfigured workspaces.
- **FR-006**: The three exclusion mechanisms MUST remain semantically independent: a document matched by
  one MUST NOT be affected by the others. In particular, a document matched only by
  `coverage.analysis_exclude_patterns` MUST still count in the coverage denominator unless it is also
  matched by the new coverage exclusion.
- **FR-007**: The configuration loader MUST treat an absent coverage-exclusion list as "no exclusions"
  (empty), preserving FR-005, and MUST NOT apply any default coverage-exclusion patterns implicitly.
- **FR-008**: Documentation (`coverage` docs and `configuration` docs) MUST be updated to describe the
  new coverage-scoped exclusion and MUST include a table that distinguishes all three exclusion
  mechanisms by name, scope, and effect.

### Out of Scope

- Changing the semantics or defaults of `source.exclude_patterns` or
  `coverage.analysis_exclude_patterns`. They are only *joined* by a clearly named third concept; they
  are not modified.
- Any model/AI-driven behavior. The documentation-coverage path is purely deterministic and lexical;
  this feature stays entirely within that path.
- The other queued post-migration features. This one is independent and not blocked by the pending
  provider/SDK retirement.

### Key Entities *(include if feature involves data)*

- **Coverage exclusion pattern list**: A configured set of glob patterns (conceptually
  `coverage.coverage_exclude_patterns`). Empty/absent by default. Determines which documents are dropped
  from the coverage denominator. Independent of the discovery and analysis-skip pattern lists.
- **Document coverage detail**: The per-document record in the coverage report. Gains the ability to
  represent an "excluded" status, in addition to its existing covered/uncovered distinction, plus the
  pattern that caused the exclusion so the report is self-explanatory.
- **Documentation coverage result**: The aggregate coverage outcome (denominator, covered count,
  percentage, per-document details). Its denominator now reflects only non-excluded documents; it gains
  a visible accounting of how many documents were excluded.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After configuring a coverage-scoped exclusion that matches a known subset of documents,
  the reported coverage percentage is computed over exactly the remaining (non-excluded) documents —
  the denominator equals total documents minus matched documents.
- **SC-002**: With a coverage-scoped exclusion configured, every excluded document is verifiably present
  in the coverage report with a distinct "excluded" status; zero excluded documents are silently
  omitted.
- **SC-003**: With a coverage-scoped exclusion configured, the excluded documents remain fully present
  for generation, analysis, and indexing — a generation or analysis run over those documents behaves
  identically to a run with no coverage exclusion configured.
- **SC-004**: With no coverage-scoped exclusions configured, the coverage output is byte-for-byte
  equivalent to the pre-feature output for the same inputs (no denominator shift, no new statuses).
- **SC-005**: A document matched only by the analysis-skip exclusion still counts in the coverage
  denominator; the same document, once also added to the coverage exclusion, is excluded — demonstrating
  the three mechanisms are independent.
- **SC-006**: The existing `Spectra.Core/Coverage` regression tests pass unchanged, except for the
  specific tests whose headline coverage number legitimately moves because a configured exclusion moved
  the denominator. No no-exclusion test changes its expected value.

## Assumptions

- The configuration key for the new list lives under the existing `coverage` configuration section,
  alongside `analysis_exclude_patterns`, with a name deliberately chosen for non-confusability (e.g.,
  `coverage_exclude_patterns`). Final key naming is a planning-phase decision constrained by FR-001.
- "Byte-for-byte equivalent" (FR-005/SC-004) is evaluated for identical inputs and configuration with
  the new list empty/absent.
- The "excluded" status is surfaced in both the structured (JSON) and human-readable coverage outputs,
  consistent with how covered/uncovered are surfaced today.
- The coverage exclusion applies to both the legacy doc-only coverage path and the unified coverage
  path, since both compute the documentation-coverage percentage from the same analyzer.

## Dependencies

None. This is an independent, model-free, deterministic path that the v2 migration never touched. It is
not blocked by the pending provider/SDK retirement.
