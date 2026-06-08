# Feature Specification: Execution Report Enrichment

**Feature Branch**: `061-execution-report-enrichment`
**Created**: 2026-06-08
**Status**: Draft
**Input**: User description: "Add richer per-test and run-level fields to the execution reports (JSON/Markdown/HTML)."

> **Numbering note**: The concept was labeled "062" in the queued investigation, which assumed the
> provider/SDK retirement would land as spec 060. The feature-creation script auto-detected the next
> available number in this repo as **061**, so this spec is `061-execution-report-enrichment`. The
> grounding investigation file is `docs/investigation/queued/051-execution-report-enrichment.md`
> (where "051" is a *concept* label — the real spec 051 is the unrelated `filter-schema-alignment`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See test classification context in the report (Priority: P1)

After a manual-test run, a tester (or QA lead reviewing the results) opens the generated report and,
for each test, immediately sees the test's **priority, tags, and component** alongside its status and
timing — without having to open the underlying test-case files and cross-reference them by hand.

**Why this priority**: This is the core of the feature and delivers the most value with the least
risk. Priority/tags/component are the fields most often needed to triage and group results (e.g.
"which high-priority tests failed?", "how did the checkout component do?"). They already exist on the
source test case; the report simply drops them today.

**Independent Test**: Generate a report for a run whose tests carry priority/tags/component values and
confirm those fields appear, correctly populated, in the JSON, Markdown, and HTML outputs. Delivers
standalone value even if Stories 2 and 3 are never built.

**Acceptance Scenarios**:

1. **Given** a run whose tests have a priority, tags, and a component, **When** a report is generated,
   **Then** each per-test entry in the JSON, Markdown, and HTML report shows that test's priority,
   tags, and component.
2. **Given** a test with no tags and no component, **When** the report is generated, **Then** those
   fields are omitted gracefully (not rendered as empty lists, "null", or blank rows).
3. **Given** an existing JSON consumer (e.g. the dashboard) reading a report, **When** the new fields
   are present, **Then** the consumer continues to work because the additions are purely additive and
   optional.

---

### User Story 2 - See coverage linkage per test (Priority: P2)

The reviewer can see, per test, **which acceptance-criteria IDs and source-doc references** that test
covered, so a passing/failing result can be tied back to the requirements and documentation it
verifies — turning the run report into evidence of what was (and wasn't) validated.

**Why this priority**: High value for traceability and audit, but secondary to the at-a-glance
classification fields in Story 1. These fields are also already present on the source test case.

**Independent Test**: Generate a report for a run whose tests link acceptance criteria and source
docs, and confirm those linkages appear per test across all three formats, omitted when absent.

**Acceptance Scenarios**:

1. **Given** a test linked to one or more acceptance-criteria IDs and source-doc references, **When**
   a report is generated, **Then** each per-test entry lists those criteria IDs and source refs in
   JSON, Markdown, and HTML.
2. **Given** a test with no linked criteria and no source refs, **When** the report is generated,
   **Then** those fields are omitted, not shown as empty.

---

### User Story 3 - Run-level context at a glance (Priority: P3)

The reviewer sees useful run-level context (e.g. environment/CI metadata and a timing breakdown)
surfaced in the report header, complementing the existing run-id/suite/environment/status/summary.

**Why this priority**: Optional polish. The exact run-level field set is intentionally left open for
the planning phase; the per-test enrichments in Stories 1 and 2 are the mandatory core. This story
should only proceed if the run already carries the data (no new data plumbing into the engine).

**Independent Test**: Generate a report and confirm any added run-level fields appear in the report
header across formats and are omitted when the underlying data is absent.

**Acceptance Scenarios**:

1. **Given** a completed run with available run-level context, **When** a report is generated,
   **Then** the chosen run-level fields appear in the report header across JSON, Markdown, and HTML.
2. **Given** run-level context that is unavailable, **When** the report is generated, **Then** those
   fields are omitted gracefully.

---

### Edge Cases

- **Test case unavailable at report time**: a result whose source test case is missing from the data
  passed to the report generator MUST still render — every new field is optional, so the entry simply
  omits the enrichments rather than failing.
- **Empty collections**: tags, criteria, and source-refs that are present-but-empty MUST be treated
  the same as absent (omitted), so empty arrays never appear as noise in any format.
- **Old report fixtures / older consumers**: a report constructed without the new data (or an
  archived fixture from before this change) MUST still validate and render; omitted fields simply do
  not appear.
- **Multiple attempts per test**: enrichment fields derive from the test case, so all attempts of the
  same test show the same classification/linkage values (consistent with how steps/preconditions are
  already populated per attempt today).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each per-test report entry MUST be able to carry the following fields, sourced from the
  underlying test case already available to the report generator: **priority**, **tags**,
  **component**, **linked acceptance-criteria IDs**, and **source-doc references**. These five
  per-test fields are the mandatory core of this feature.
- **FR-002**: Every new field MUST be optional — populated when the source data exists and omitted
  entirely when it is absent or empty — so existing report construction sites and JSON consumers (the
  dashboard) are unaffected.
- **FR-003**: The new per-test fields MUST be populated from the test-case data the report generator
  already receives; this feature MUST NOT add any new data plumbing into, or change the behavior of,
  the execution engine or its tools.
- **FR-004**: All three report formats MUST surface the new fields — JSON, Markdown, and HTML — with
  empty/absent fields omitted gracefully in every format (no empty rows, blank labels, or "null"
  text).
- **FR-005**: The change MUST be purely additive: no existing report field is renamed or removed, and
  the execution engine remains client-agnostic and unchanged beyond populating and rendering the new
  fields.
- **FR-006**: Run-level report context (e.g. environment/CI metadata, timing breakdown) MAY be added
  under the same optional-field discipline. The exact run-level field set is to be scoped during
  planning; if added, it MUST follow FR-002 through FR-005. If no suitable run-level data is already
  available without new plumbing, this requirement MAY be satisfied as a no-op.
- **FR-007**: Existing report fixtures that gain the new fields MUST be updated deliberately, and a
  report built without the new data MUST continue to validate (backward compatibility).

### Key Entities *(include if feature involves data)*

- **Per-test report entry**: the per-test record in a report. Today carries identity, status,
  attempt, timing, notes, blocking, and content (preconditions/steps/expected-result/test-data/
  screenshots). Gains optional **priority, tags, component, acceptance-criteria IDs, source-doc
  references**.
- **Run-level report**: the top-level report record (run id, suite, environment, started/completed,
  duration, executor, status, summary, per-test results, filters). May gain optional run-level
  context fields (scoped at planning).
- **Test case (source)**: the parsed test definition that already holds priority, tags, component,
  criteria, and source-refs. It is the source of the new fields; it is not modified by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a run whose tests carry priority/tags/component/criteria/source-refs, 100% of those
  fields appear, correctly populated, on the corresponding per-test entries in all three report
  formats (JSON, Markdown, HTML).
- **SC-002**: A reviewer can determine a test's priority, component, and the acceptance criteria it
  covered directly from the report, with zero need to open the underlying test-case files.
- **SC-003**: Every new field is omitted when its source data is absent or empty — no report in any
  format renders an empty list, blank label, or literal "null" for a missing enrichment.
- **SC-004**: A report (or archived fixture) produced without the new data still validates and
  renders, and the existing dashboard JSON consumer continues to function unchanged — i.e. zero
  breakage of existing fixtures, construction sites, or consumers attributable to this change.
- **SC-005**: No existing report field is renamed or removed, and the execution engine, its tools, and
  the state machine exhibit no behavior change (the regression net of engine/tool/state-machine tests
  holds).

## Assumptions

- The five named per-test fields map to existing test-case data: priority, tags, component,
  acceptance-criteria IDs, and source-doc references. (Other test-case fields such as requirement IDs,
  the scenario-from-doc excerpt, or related work items are candidate enrichments but are **not** in
  the mandatory scope; planning may consider them.)
- "Optional and omitted when empty" is the established convention for report fields and is the pattern
  every new field follows.
- The report is produced deterministically by the execution server with no model/AI involvement;
  enrichment is data-mapping plus rendering only.
- Run-level enrichment is best-effort: it proceeds only if the run already carries the data, since
  FR-003 forbids new data plumbing into the engine.

## Out of Scope

- Any change to the execution loop, state machine, or tool surface — the engine is reused verbatim.
- Any model/AI involvement in report generation (reports are deterministic).
- New data plumbing into the execution server to obtain fields it does not already have.
- The other queued post-migration features (independent of this one).

## Dependencies

- None. This targets the deterministic report path that the v2 migration reused unchanged. It is not
  blocked by the pending provider/SDK retirement and shares no seam with the other queued features.
