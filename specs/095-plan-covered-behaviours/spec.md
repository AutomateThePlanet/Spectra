# Feature Specification: Plan Only Uncovered Behaviours (094 Review Fix)

**Feature Branch**: `095-plan-covered-behaviours`
**Created**: 2026-10-12
**Status**: Draft
**Input**: User description: "095-plan-covered-behaviours: Review fix for spec 094 plus the pre-existing coverage-count
bug it depends on. One rule decides, per behaviour, whether existing tests already cover it. The generation plan
batches only uncovered behaviours, and `already_covered` / `recommended` are counted from the same rule. No new
features. Each fix needs a regression test that fails on db20415 (the 094 merge)." (Background, F1, F2, constraints and
scope as quoted in the request.)

## Context

094 made the analysis save a generation plan whose batches are generated section by section. Two counting rules now
disagree:
- The plan batches **every** analysed behaviour, including ones the suite's existing tests already cover, so batch
  prompts ask the generator to write near-duplicates of existing tests.
- `recommended` (which the batch counts are shares of) subtracts an "already covered" figure that, whenever the suite
  has any test or criterion, is simply **the number of tests in the suite**. In a suite with 50 tests, an analysis
  that finds 12 genuinely new behaviours recommends 0, and the plan then gives each batch the minimum of 1 test.

The fix is one rule, applied per behaviour, that both the counts and the plan use.

Facts (verified on `db20415`):
- Without a coverage snapshot, "already covered" counts behaviours whose title is at least 0.6 similar to an existing
  test's title (the title similarity measure of the duplicate detector). With a snapshot (any test or criterion in
  the suite), it is the suite's test count. `recommended = max(0, behaviours − already covered)`.
- The 0.6 threshold is hard-coded; config already has `generation.duplicate_threshold` (default 0.6), not validated.
- The analysis prompt's coverage context ("EXISTING COVERAGE — DO NOT DUPLICATE") is separate and stays.
- A 094 plan stores behaviours without any coverage information.

Rules that still apply: the 094 plan, batch and count rules for the behaviours that are batched; analyze → present →
STOP for approval; no config section ever required (083); human summaries on stderr in JSON mode; byte-identical plans
for identical inputs; every fix has a test that fails on `db20415`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The recommendation counts what is actually missing (Priority: P1)

A QA engineer asks for more tests in a suite that already has 50. The analysis finds 12 behaviours no existing test
covers and some that are covered. The recommendation says 12, and the already-covered figure is the number of
behaviours that existing tests cover — not the size of the suite.

**Why this priority**: Today such a suite gets a recommendation of 0, so the new behaviours are never proposed.

**Independent Test**: Ingest an analysis of 15 behaviours into a suite with 50 tests whose titles match 3 of the
behaviours: `already_covered` is 3, `recommended` is 12, with or without criteria in the suite.

**Acceptance Scenarios**:

1. **Given** a suite with 50 tests and an analysis of 15 behaviours of which 3 match existing test titles, **When**
   the analysis is ingested, **Then** `already_covered` is 3 and `recommended` is 12.
2. **Given** the same suite with acceptance criteria (a coverage snapshot exists) and without them, **When** the same
   analysis is ingested, **Then** both give the same counts.
3. **Given** `generation.duplicate_threshold` set to 0.9, **When** a behaviour's title is 0.8 similar to a test's,
   **Then** that behaviour is not covered; with the default 0.6 it is.
4. **Given** a behaviour equally similar to two tests, **When** it is covered, **Then** `covered_by` is the lower test id.

---

### User Story 2 - The plan never asks for tests that already exist (Priority: P1)

The approved plan batches only uncovered behaviours. Covered behaviours are shown at the approval step as "already
covered — not generated", with the test that covers each, and no batch prompt names them.

**Why this priority**: The 094 finding — near-duplicates of existing tests, and the batch counts spent on them.

**Independent Test**: Ingest an analysis in which some behaviours are covered: the plan keeps all behaviours with
their `covered_by`, but batches (and the `unsourced` batch) contain only the uncovered ones, their counts add up to
the new `recommended`, and a `--batch` prompt never contains a covered behaviour's title.

**Acceptance Scenarios**:

1. **Given** an analysis with 3 covered and 12 uncovered behaviours, **When** the plan is built, **Then** the batches
   hold exactly the 12 uncovered behaviours and their counts follow the 094 count rule over `recommended` = 12.
2. **Given** every behaviour is covered, **When** the plan is built, **Then** there are no batches and the skill says
   everything is already covered.
3. **Given** a covered behaviour whose source does not resolve, **When** the plan is built, **Then** it is not in the
   `unsourced` batch.
4. **Given** the analysis result, **When** the user sees the recommendation, **Then** each covered behaviour is listed
   with the test that covers it, as "already covered — not generated"; the final report lists them too.

---

### User Story 3 - Existing workspaces keep working (Priority: P2)

A user who analysed with 094 and upgrades still has a 094 plan on disk. It loads and compiles as it did; the user is
told to re-run the analysis to get the corrected counts.

**Why this priority**: The 083 lesson — every rule is checked against a real `spectra init` workspace and a pre-095
one.

**Independent Test**: Put a plan written by 094 in place and compile one of its batches: exit 0, the batch compiles
with all its behaviours as uncovered, and the result carries a warning suggesting a re-run of the analysis.

**Acceptance Scenarios**:

1. **Given** a 094 plan file (no coverage information), **When** a batch is compiled, **Then** it compiles as in 094,
   with a warning to re-run the analysis for the new counts; the generator relays it and the skill reports it once.
2. **Given** a workspace from the real `spectra init` without `generation.duplicate_threshold`, **When** an analysis is
   ingested, **Then** the default 0.6 is used and nothing warns.

---

### Edge Cases

- The suite has no tests: no behaviour is covered; `recommended` = all behaviours (as today).
- A behaviour title is empty: never covered (the similarity of an empty title is 0).
- A behaviour equals an existing title except for case, spacing or punctuation: covered (similarity 1 after the
  existing normalisation).
- Two behaviours are covered by the same test: both are covered; each lists that test.
- The focus filter on the analysis: coverage is decided after the filter, over the remaining behaviours.
- `generation.duplicate_threshold` outside (0, 1]: used as given (the existing config rules don't validate it) — above
  1 nothing is covered, at or below 0 every behaviour of a suite with tests is covered; documented and warned on
  stderr, not refused (FR-004).
- A covered behaviour whose source doesn't resolve: listed only under `covered` — not under `unresolved`, not in any
  batch.
- An existing test without a title: never covers anything.
- A plan from 094 for a suite whose tests changed since: still loads; counts are corrected only by re-running the
  analysis (the warning says so).

## Requirements *(mandatory)*

### Functional Requirements

**F1 — one per-behaviour coverage rule**

- **FR-001**: A behaviour MUST be covered when an existing test in the suite has a title whose similarity to the
  behaviour's title — the duplicate detector's title similarity measure, as today — is at least
  `generation.duplicate_threshold` (default 0.6). Its `covered_by` MUST be the id of the most similar test; ties go to
  the lowest id (ordinal comparison).
- **FR-002**: The rule MUST apply with and without a coverage snapshot. The suite's test count MUST no longer be used
  as the covered count; the snapshot still feeds the analysis prompt's coverage context.
- **FR-003**: `already_covered` MUST be the number of covered behaviours and `recommended` the number of uncovered
  behaviours (after the focus filter). The category and technique breakdowns stay over all behaviours, as today.
- **FR-004**: `generation.duplicate_threshold` MUST be read where present; its absence never warns or fails, and no
  new range check is added. A value outside (0, 1] gives a non-refusing stderr warning on the analysis ingest — "generation.duplicate_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered." — with no exit-code change (analyze L1).
- **FR-005**: Deciding coverage for 100 behaviours against 2,500 existing tests MUST take under 2 seconds (one
  similarity check per behaviour–test pair at most; measured and recorded).

**F2 — the plan batches only uncovered behaviours**

- **FR-006**: The plan MUST keep every behaviour with its `covered_by` (none when uncovered). Batches, the `unsourced`
  batch and the count rule MUST use only uncovered behaviours, so the batch counts reconcile with the new
  `recommended`. When every behaviour is covered there are no batches. Because `recommended` = uncovered behaviours
  and every batch holds at least one of them, each batch's count equals its number of uncovered behaviours (one test
  per behaviour, as guidance) and the counts add up to `recommended`; the 094 proportional / `one_per_batch` logic stays
  in the plan builder but can't be reached from the analysis ingest (analyze M1). A user's explicit total is still
  split by the skill as in 093/094.
- **FR-007**: The analysis ingest result MUST add `covered` (one entry per covered behaviour: id, title, `covered_by`);
  the human summary (stderr in JSON mode) lists them. `unresolved` lists only uncovered behaviours whose source
  didn't resolve: a covered behaviour appears only under `covered`, whatever its source (analyze M2). Existing fields keep their names; `already_covered` and
  `recommended` change meaning as in FR-003.
- **FR-008**: `compile-prompt --batch` MUST never name a covered behaviour. Count mode (from-description, `unsourced`,
  the 094 template fallback) is unchanged apart from the corrected `recommended`.
- **FR-009**: The analyst MUST relay one `covered: <title> (<covered_by>)` line per covered behaviour, verbatim from
  the CLI. The generate skill MUST show them at the approval step as "already covered — not generated" and list them
  in the final report.

**Compatibility**

- **FR-010**: A plan written by 094 (no coverage information) MUST still load. Its behaviours are treated as uncovered
  (as 094 did), and `compile-prompt --batch` adds the warning "the generation plan predates per-behaviour coverage;
  re-run the analysis for corrected counts". The generator relays it; the skill reports it once per run and suggests
  re-running the analysis.
- **FR-011**: No config section becomes required (083). Verified on a workspace from the real `spectra init` and on a
  pre-095 workspace (a 094 plan file, a suite with existing tests).

### Required Tests

Each fails on `db20415`, except the 094-plan compatibility guard:
- per-behaviour coverage: the threshold from config, the `covered_by` tie rule, snapshot and no snapshot giving the
  same counts;
- the 50-tests / 12-gaps case → `recommended` 12;
- the plan batches only uncovered behaviours; covered unresolved behaviours stay out of `unsourced`; all covered → no
  batches;
- the `covered` JSON entries and summary lines;
- `--batch` prompts never contain a covered behaviour's title;
- a 094 plan loads and compiles, with the re-run warning (guard: loads on both; the warning is new);
- analyst and skill text guards for the `covered:` lines and "already covered — not generated";
- the performance row (2,500 tests × 100 behaviours < 2 s);
- each batch's count equals its uncovered behaviours, summing to `recommended` (M1);
- a covered behaviour with an unresolvable source is only in `covered` (M2);
- the threshold warning outside (0, 1] (L1);
- the ordinal tie rule on unpadded ids (L3).

The 044 assertion `Build_RecommendedCount_FlooredAtZero_WhenSnapshotCoversAll` (recommended 0 because the suite's test
count exceeds the behaviours) is replaced by rows proving the new rule — a deliberate change, listed in the tasks Notes.

### Key Entities

- **Behaviour** (094): gains `covered_by` — the id of the existing test that covers it, or none.
- **Generation plan** (094): keeps all behaviours; batches only uncovered ones; a 094 plan without coverage
  information still loads.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a suite with 50 tests, an analysis finding 12 uncovered and 3 covered behaviours recommends exactly 12
  and plans batches whose counts add up to 12 (was: 0 recommended, 1 per batch).
- **SC-002**: No batch prompt names a behaviour that an existing test covers (0 of the covered titles appear in any
  `--batch` prompt).
- **SC-003**: The same analysis gives the same `already_covered` / `recommended` with and without acceptance criteria
  in the suite.
- **SC-004**: Coverage for 100 behaviours against 2,500 tests is decided in under 2 seconds.
- **SC-005**: A plan written by 094 still compiles in a pre-095 workspace, and a new `spectra init` workspace runs the
  analysis without a config warning.

## Assumptions

- Title similarity stays the coverage measure (no semantic or model-judged coverage); its limits (a reworded title of
  a covered behaviour counts as uncovered) are unchanged from today's no-snapshot path.
- "Lowest id" is the ordinal comparison of test ids, which orders zero-padded ids (`TC-099` < `TC-100`) naturally.
- The suite's tests are the ones the analysis ingest already loads (the suite folder); tests of other suites never
  cover a behaviour.
- A 094 plan is recognised by its behaviours lacking the coverage field; the plan format stays version 1 with an
  optional field, so 094 and 095 plans load with the same reader.

## Out of Scope

- Semantic (model-judged) coverage.
- A way to force generation for covered behaviours.
- The other 094 finding (`PLAN_STALE` also blocks `count_fallback`) — Phase 6.
- The rest of the Phase 6 list.
