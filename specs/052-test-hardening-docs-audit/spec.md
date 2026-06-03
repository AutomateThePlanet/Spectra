# Feature Specification: Test Hardening & Documentation Audit (047–051)

**Feature Branch**: `052-test-hardening-docs-audit`  
**Created**: 2026-06-03  
**Status**: Draft  
**Input**: User description: "Harden the test coverage and documentation across the entire 047–051 work package. Each individual spec landed with its own unit and scoped integration tests, but the SEAMS between specs (cross-spec user workflows), the ORIGINAL user-reported symptoms (as explicit named regression guards), the SCALE scenario that triggered Spec 047, and the OVERALL documentation narrative were not the responsibility of any single spec. This spec is a ship-readiness pass: cross-spec integration tests against the Calculator demo + Spectra_Demo, named regression tests for each originally-reported bug, a large-corpus extraction scale test, a full documentation audit and consolidation, SKILL coherence verification, and a single consolidated CHANGELOG entry. No new features, no new bug fixes — only hardening and docs."

## Overview

Specs 047 through 051 fixed a tightly-coupled cluster of reliability defects: silent failures in acceptance-criteria extraction (047), missing non-blocking coverage guards (048), from-description tests not being registered in the index (049), from-description tests missing criteria injection (050), and silent filter drops at the MCP request boundary (051). Each shipped with tests scoped to its own code. This feature is a **ship-readiness pass** that closes the three gaps no single spec owned: cross-spec workflow coverage, named regression guards for the original user symptoms, and a coherent end-to-end documentation narrative. It adds **no new functionality and no new bug fixes** — only tests, documentation, and a consolidated changelog.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Named regression guards for every original symptom (Priority: P1)

A maintainer reviewing CI output must be able to recognize, at a glance, when one of the originally-reported user bugs has come back. Today, if the high-priority filter regresses, CI reports a failure with an internal code name that requires git archaeology to connect to the user-facing complaint. This story adds tests whose displayed names *are* the user's own words.

**Why this priority**: This is the smallest, highest-leverage slice. It directly closes the loop on each reported bug, can be delivered and demonstrated on its own, and surfaces forgotten edge cases before the larger suites are built on top of it. It is the MVP — even shipped alone it makes every original defect individually recognizable in CI.

**Independent Test**: Run the regression suite and confirm each of the five originally-reported symptoms has a passing test whose displayed name is the symptom phrasing (e.g. "Original bug: high priority filter from a suite returns whole suite"), not the internal method name. Temporarily reverting any one of the 047–051 fixes makes exactly the matching named test fail.

**Acceptance Scenarios**:

1. **Given** the regression suite, **When** it runs in CI, **Then** there is one passing test per originally-reported symptom and each test's displayed name is the user-facing symptom phrasing.
2. **Given** the criteria-extraction parse-failure fix (047) is reverted, **When** the suite runs, **Then** the "cache poisoning on parse failure" named test fails and no unrelated named test fails.
3. **Given** the filter-binding fix (051) is reverted, **When** the suite runs, **Then** the "high priority filter from a suite returns whole suite" named test fails.

---

### User Story 2 - Cross-spec workflows verified end to end (Priority: P1)

A real user's journey crosses spec boundaries: "create a from-description high-priority test, then run only the high-priority tests from that suite" touches index registration (049), criteria injection (050), and filter binding (051) in one continuous flow. No existing test exercises that whole chain. This story adds end-to-end tests that drive the product as a user would — wiring a temporary project, running the commands, and asserting the observable result — against demo fixtures.

**Why this priority**: Cross-spec seams are exactly where integration defects hide and where each spec's own tests are blind. This is co-critical with the named regression guards because together they form the confidence basis for shipping the whole block.

**Independent Test**: Run the end-to-end suite against fixture data and confirm each listed workflow passes by exercising two or more of specs 047–051 in sequence, asserting on user-observable outcomes (exact test count, exact id present, populated criteria field, warning/note presence) rather than internal state.

**Acceptance Scenarios**:

1. **Given** a fresh fixture project, **When** a user creates a from-description high-priority test and then starts an execution run filtered to high priority for that suite, **Then** the new test appears in the index, has its criteria field populated, and is the test enqueued by the filtered run (exact count and id asserted).
2. **Given** a corpus whose extractions are all inconclusive, **When** the user indexes documents, **Then** indexing exits successfully and the result carries the zero-criteria warning.
3. **Given** a suite with no matching criteria, **When** a user generates against it, **Then** the result carries the no-criteria note, and the note is present even at the quietest verbosity.
4. **Given** a from-description test was just created, **When** the user searches for test cases, **Then** the new test is returned without any manual index rebuild.
5. **Given** the documented misshapen filter requests, **When** each is sent, **Then** each either filters correctly or returns an actionable error — none silently enqueues the whole suite.

---

### User Story 3 - Large-corpus extraction scale guard (Priority: P2)

The defect that triggered 047 only manifested at scale: on a large real project, a corpus-wide time budget silently killed extraction for the whole document set. A unit test on a tiny corpus cannot reproduce it. This story adds an automated scale guard that simulates realistic provider latency across many documents and proves that per-document deadlines are enforced individually rather than corpus-wide.

**Why this priority**: It is the one test that would have caught the original scale bug before it shipped, but it is slower and more specialized than the P1 suites, so it is sequenced after them and isolated into its own category so fast-feedback runs can exclude it.

**Independent Test**: Run the scale guard with a synthetic multi-document corpus and per-call latency injected. Confirm at least some documents are extracted (not a whole-corpus abort) and that deadlines are applied per document. The guard fails if a corpus-wide deadline, cache poisoning, or missing retry from 047 is reintroduced.

**Acceptance Scenarios**:

1. **Given** a synthetic corpus of many realistically-sized documents with per-call latency injected, **When** indexing with criteria extraction runs, **Then** at least some documents are successfully extracted and indexing does not abort the whole corpus.
2. **Given** the same run, **When** timing is observed, **Then** the deadline that bounds extraction is per-document, not a single budget shared across the corpus.
3. **Given** the scale guard, **When** the fast-feedback test selection runs, **Then** the guard can be excluded by category yet still runs in the full CI pass.

---

### User Story 4 - Coherent documentation and SKILL narrative (Priority: P2)

A user reading the docs or invoking a SKILL must see behavior consistent with the post-051 product: criteria extraction is on by default, from-description tests are indexed and carry criteria, filters work through execution runs, and filter errors are actionable. Today the docs still carry pre-047 guidance in places, and the SKILL files were each touched only by their own spec. This story audits every doc and SKILL file, records the disposition of each, and updates the stale ones.

**Why this priority**: Documentation coherence requires the global view that only this consolidation spec has, but it does not gate the test confidence that the P1/P2 test stories provide, so it follows them.

**Independent Test**: Open the doc-audit report and confirm every audited doc and SKILL file is listed with a disposition (confirmed-current / updated / superseded); confirm every file marked "updated" actually differs in this change set; spot-check that no doc or SKILL still describes pre-047 behavior or references a removed escape hatch.

**Acceptance Scenarios**:

1. **Given** the doc-audit report, **When** it is reviewed, **Then** every doc file in scope and every SKILL file in scope appears exactly once with a disposition.
2. **Given** a file marked "updated" in the report, **When** the change set is inspected, **Then** that file is actually modified.
3. **Given** any SKILL file in scope, **When** it is exercised against a realistic user prompt and the transcript is captured, **Then** the rendered output reflects post-051 behavior (default-on extraction, indexed from-description tests with criteria, single filter shape, actionable filter errors) and contains no pre-047 guidance.

---

### User Story 5 - Single consolidated release narrative (Priority: P3)

A user upgrading across the 047–051 block needs one place that explains, in their terms, what changed. Five separate fragmented changelog notes force them to assemble the story themselves. This story produces one consolidated CHANGELOG entry organized by Fixed / Added / Changed, and records the cross-cutting engineering lesson (the silent-failure pattern) in the project knowledge base so future work avoids the same class of defect.

**Why this priority**: It is purely a write-up step that depends on all behavior changes being confirmed observable by the preceding stories, so it is sequenced last.

**Independent Test**: Confirm the changelog contains exactly one consolidated entry for the 047–051 block, organized by Fixed / Added / Changed, written from the user's perspective; confirm the project-knowledge file has a row for this feature and a recorded learning entry naming the silent-failure pattern as a class to watch for.

**Acceptance Scenarios**:

1. **Given** the changelog, **When** it is read, **Then** there is exactly one entry covering the 047–051 block, grouped into Fixed / Added / Changed, with each line attributed to its originating spec and phrased for end users.
2. **Given** the project-knowledge file, **When** it is read, **Then** it has a row for this feature and a learning entry describing the silent-failure pattern (lenient deserialization, returning a value for a required field, swallowing errors and returning empty) as a recurring class to watch for.

---

### Edge Cases

- **No integration test project exists yet.** The repository currently has unit-style test projects but no dedicated cross-spec/integration project. The cross-spec and named-regression suites must land in a clearly-named home that is wired into the build and CI (see Assumptions).
- **Scale guard slows fast feedback.** The scale guard must be categorized so it can be excluded from fast-feedback runs while still executing in the full CI pass; excluding it must not silently drop it from CI entirely.
- **A fixture lacks the data a workflow needs.** If a demo fixture cannot support a given cross-spec workflow (e.g. no extractable criteria), the test must construct the minimum required fixture state deterministically rather than depend on ambient project state.
- **An audited file is already current.** Files that need no change must still be listed in the audit report with a "confirmed-current" disposition so coverage is provably complete, not silently skipped.
- **A SKILL transcript reveals drift.** If exercising a SKILL surfaces pre-047 wording, the drift must be fixed and the corrected transcript captured as evidence — a transcript showing stale behavior is not an acceptable deliverable.
- **Reverting a fix must fail exactly one named guard.** Each named regression test must be specific enough that reverting its target fix fails that test and not a cascade of unrelated ones.

## Requirements *(mandatory)*

### Functional Requirements

#### Part A — Cross-spec end-to-end tests

- **FR-001**: The system MUST provide an automated end-to-end test for the from-description → indexed → criteria-populated → filtered-run workflow that asserts the new high-priority test appears in the index, has a populated criteria field, and is the exact test (by count and id) enqueued by a high-priority filtered run.
- **FR-002**: The system MUST provide an end-to-end test that runs criteria extraction and then batch generation and asserts the generated tests carry a criteria field matching the extracted criteria.
- **FR-003**: The system MUST provide an end-to-end test that, with one document made to fail then succeed, confirms the failing document is not cached and is re-attempted on a subsequent run.
- **FR-004**: The system MUST provide an end-to-end test that creates a from-description test and confirms it is discoverable via test-case search with no manual index rebuild.
- **FR-005**: The system MUST provide an end-to-end test that sends each of the documented misshapen filter requests and asserts each either filters correctly or returns an actionable error, and that none enqueues the whole suite.
- **FR-006**: The system MUST provide an end-to-end test that indexes a corpus whose extractions are all inconclusive and asserts indexing exits successfully while surfacing the zero-criteria warning.
- **FR-007**: The system MUST provide an end-to-end test that generates against a suite with no matching criteria and asserts the no-criteria note is present in the result, including at the quietest verbosity setting.
- **FR-008**: Each cross-spec test MUST exercise two or more of specs 047–051 in one flow and MUST assert on user-observable outcomes against demo fixture data, not on internal implementation state.

#### Part B — Named regression guards

- **FR-009**: The system MUST provide a regression test, named after the symptom "extract-criteria on generation not working," that fails if from-description/generation no longer populates the criteria field.
- **FR-010**: The system MUST provide a regression test, named after the symptom "high priority filter from a suite returns whole suite," that fails if a priority-filtered run no longer filters.
- **FR-011**: The system MUST provide a regression test, named after the symptom "from-description test has different format and is missing from index," that fails if a from-description test is shaped differently from peers or is absent from the index.
- **FR-012**: The system MUST provide a regression test, named after the symptom "first big-project index produced zero criteria silently," that fails if a zero-criteria index no longer warns.
- **FR-013**: The system MUST provide a regression test, named after the symptom "cache poisoning on parse failure," that fails if a parse-class failure is permanently cached.
- **FR-014**: Each named regression test's displayed name MUST be the user-facing symptom phrasing rather than an internal code or method name.

#### Part C — Scale guard

- **FR-015**: The system MUST provide a scale test that generates a synthetic corpus of configurable document count (default 30) with realistic per-document content size.
- **FR-016**: The scale test MUST simulate realistic per-call provider latency.
- **FR-017**: The scale test MUST assert that at least some documents are extracted (the run does not abort the whole corpus) and that extraction deadlines are enforced per document, not corpus-wide.
- **FR-018**: The scale test MUST be categorized so it can be excluded from fast-feedback runs while still executing in the full CI pass.

#### Part D — Documentation audit

- **FR-019**: The system MUST produce a doc-audit report that lists every audited documentation file with a disposition of confirmed-current, updated, or superseded.
- **FR-020**: The audit MUST cover, at minimum, the usage, coverage, test-format, CLI-reference, generic-MCP, skills-integration, and getting-started docs, plus the project-knowledge and changelog files, and MUST identify additional doc files by search rather than relying solely on that list.
- **FR-021**: The audit MUST identify and remove stale pre-047 guidance (e.g. advising a separate extraction run after every index, or claiming filtering works only through one tool), reconcile coherence gaps where two docs describe the same behavior differently, and add upgrade/migration notes for observable behavior changes.
- **FR-022**: Every file the audit report marks "updated" MUST be actually modified in this change set.

#### Part E — SKILL coherence

- **FR-023**: Every SKILL file in scope MUST be reviewed for coherence with post-051 behavior: the generate SKILL renders the notes array and reflects that from-description now populates criteria and is indexed; the docs SKILL surfaces the zero-criteria warning and default-on extraction; the execution SKILL shows one filter shape and reflects actionable filter errors; the coverage SKILL explains the criteria-source outcome field; the criteria SKILL reflects resilient extraction and recovery paths.
- **FR-024**: Each SKILL file in scope MUST be exercised against a realistic user prompt and its transcript captured as evidence.
- **FR-025**: No SKILL file in scope may render pre-047 behavior or reference a removed escape hatch.

#### Part F — Consolidated changelog & knowledge

- **FR-026**: The changelog MUST contain exactly one consolidated entry for the 047–051 block, organized into Fixed / Added / Changed, written from the user's perspective, with each item attributed to its originating spec.
- **FR-027**: The project-knowledge file MUST gain a row for this feature and a learning entry that names the silent-failure pattern (lenient deserialization, returning a value for a required field, swallowing errors and returning empty) as a class of defect to watch for in future work.

#### Cross-cutting

- **FR-028**: This feature MUST NOT introduce new product functionality or new bug fixes; if a test exposes a genuine defect, that defect is out of scope and MUST be recorded for a separate fix rather than patched here.
- **FR-029**: The entire test suite, including the scale guard, MUST pass on a fresh checkout in CI.

### Key Entities *(include if feature involves data)*

- **Cross-spec end-to-end test**: A test that drives the product as a user would across a temporary fixture project and asserts user-observable outcomes spanning two or more of specs 047–051.
- **Named regression guard**: A test whose displayed name is an originally-reported user symptom, mapped one-to-one to the fix that resolved it.
- **Scale guard**: A categorized, latency-simulating test over a synthetic multi-document corpus that proves per-document (not corpus-wide) extraction deadlines.
- **Doc-audit report**: A deliverable listing every audited doc and SKILL file with its disposition (confirmed-current / updated / superseded).
- **SKILL transcript**: Captured evidence of a SKILL exercised against a realistic prompt, demonstrating post-051 behavior.
- **Consolidated changelog entry**: One user-facing release note for the 047–051 block grouped by Fixed / Added / Changed.
- **Silent-failure-pattern learning**: A recorded knowledge entry describing the recurring defect class the 047–051 block exposed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the originally-reported symptoms (all five) have a passing named regression test whose displayed name is the symptom phrasing.
- **SC-002**: 100% of the cross-spec workflows in scope have a passing end-to-end test that exercises two or more of specs 047–051 against fixture data.
- **SC-003**: Reverting any single fix from specs 047–051 causes at least one named regression test or cross-spec test to fail, and a maintainer can identify the broken user-facing behavior from the failing test's name alone, without consulting git history.
- **SC-004**: The scale guard fails if any of the three 047 protections (per-document deadline, no cache poisoning, retry of inconclusive extraction) is reintroduced as a regression, and it can be excluded from fast-feedback runs while still running in full CI.
- **SC-005**: 100% of audited doc and SKILL files appear in the audit report with a disposition, and every file marked "updated" is actually changed in this change set.
- **SC-006**: Zero doc or SKILL files in scope describe pre-047 behavior or reference a removed escape hatch, verified against captured SKILL transcripts.
- **SC-007**: The changelog contains exactly one consolidated 047–051 entry grouped by Fixed / Added / Changed, and the project-knowledge file contains both the feature row and the silent-failure-pattern learning entry.
- **SC-008**: The full test suite, including the scale guard, passes on a fresh checkout in CI with no new product code changes.

## Assumptions

- **No new integration test project exists today.** The cross-spec (`EndToEndScenarios`) and named-regression (`OriginalBugRegression`) suites will land in a dedicated, clearly-named test home wired into the build and CI. Whether that is a new `tests/Spectra.Integration.Tests/` project or a new namespace inside an existing test project is an implementation decision for the planning phase; the requirement is only that it is discoverable, runs in CI, and is recognizably the cross-spec home.
- **SKILL files live under `src/Spectra.CLI/Skills/Content/Skills/` (and agent files under `Content/Agents/`).** The source description also references `.github/skills/`, which does not exist in this repository; the audit scope is the actual on-disk SKILL/agent content under `Skills/Content/`, plus any other SKILL surface found by search.
- **The scale guard's default corpus size (30) and per-call latency (≈3s simulated) are tuning defaults**, adjustable so the full CI pass stays within a reasonable wall-clock budget while still proving per-document deadline enforcement.
- **Demo fixtures (Spectra_Demo and the Calculator demo) provide sufficient document and suite content** for the cross-spec workflows; where a fixture lacks required data, the test constructs the minimum deterministic fixture state itself.
- **"Misshapen filter requests" refers to the documented Path C cases** from the prior priority-filter investigation; the test reproduces those exact request shapes.
- **The audit report and SKILL transcripts are stored as feature deliverables** under either `docs/specs/` (e.g. `052-doc-audit-report.md`, `052-skill-transcripts.md`) or the `specs/052-test-hardening-docs-audit/` directory, consistent with how prior specs stored deliverables.

## Dependencies

- **Hard dependency**: specs 047, 048, 049, 050, and 051 must all be merged before this work begins — this is the integration pass for the whole block. (All five appear in the current `specs/` directory and recent history.)
- **No dependents**: later features may build on the test harness produced here but are not blocked by it.

## Out of Scope

- New product functionality of any kind.
- New bug fixes (any genuine defect a test uncovers is recorded for a separate spec, not fixed here).
- Performance optimization beyond the named scale guard.
- Telemetry or observability features.
