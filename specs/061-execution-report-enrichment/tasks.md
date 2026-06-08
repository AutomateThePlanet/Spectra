---

description: "Task list for Execution Report Enrichment"
---

# Tasks: Execution Report Enrichment

**Input**: Design documents from `/specs/061-execution-report-enrichment/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/report-schema.md

**Tests**: INCLUDED — the spec's "Tests" section explicitly requests net-new tests for population,
rendering, and backward-compat.

**Organization**: Grouped by user story. JSON output is satisfied at the Foundational phase (automatic
serialization); each user story adds the Markdown/HTML rendering + tests for its fields.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: US1 / US2 / US3 (maps to spec.md user stories)
- All paths are absolute-from-repo-root; the solution is a single multi-project .NET solution.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm a green baseline before making additive changes.

- [x] T001 Confirm baseline: run `dotnet build` and `dotnet test tests/Spectra.MCP.Tests` and record that they pass before any edits.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the shared schema fields and population that BOTH US1 and US2 depend on. After this
phase, JSON output already carries all five per-test fields (FR-001, FR-003); only MD/HTML rendering
remains per-story.

⚠️ MUST complete before US1 and US2.

- [x] T002 Add five optional per-test properties to `TestResultEntry` in `src/Spectra.Core/Models/Execution/TestResultEntry.cs`: `Priority` (`Priority?`, with `[JsonPropertyName("priority")]`, `[JsonConverter(typeof(JsonStringEnumConverter))]`, `[JsonIgnore(WhenWritingNull)]`), `Tags` (`IReadOnlyList<string>?`, `[JsonPropertyName("tags")]`, `[JsonIgnore(WhenWritingNull)]`), `Component` (`string?`, `"component"`), `Criteria` (`IReadOnlyList<string>?`, `"criteria"`), `SourceRefs` (`IReadOnlyList<string>?`, `"source_refs"`) — each `[JsonIgnore(WhenWritingNull)]`. Add the `Spectra.Core.Models` using for `Priority` if needed.
- [x] T003 Populate the five fields in `ReportGenerator.Generate` in `src/Spectra.MCP/Reports/ReportGenerator.cs` from `tc = testCases?.GetValueOrDefault(r.TestId)`: `Priority = tc?.Priority`, `Component = tc?.Component`, and normalize collections to null when empty (`Tags = tc?.Tags is { Count: > 0 } ? tc.Tags : null`, same for `Criteria`, `SourceRefs`) — mirroring the existing `Steps` pattern at `ReportGenerator.cs:49`.

**Checkpoint**: `dotnet build` passes; a generated JSON report now includes `priority`/`tags`/`component`/`criteria`/`source_refs` per entry (omitted when empty).

---

## Phase 3: User Story 1 — Test classification context (Priority: P1) 🎯 MVP

**Goal**: Per-test **priority, tags, component** appear in JSON, Markdown, and HTML; omitted when empty.

**Independent Test**: Generate a report for tests carrying priority/tags/component and confirm all
three formats show them, and that a test with no tags/component omits them.

- [x] T004 [US1] In `src/Spectra.MCP/Reports/ReportWriter.cs` (Markdown, `WriteMarkdownAsync` ~:226-235): add a `Priority` column to the "All Results" table header and rows (`| Test ID | Title | Status | Priority | Attempt | Duration |`); add a private `PriorityText(Priority?)` helper returning the enum name or `-` when null.
- [x] T005 [US1] In `src/Spectra.MCP/Reports/ReportWriter.cs` (Markdown, Failed Tests detail block ~:162-189): emit bullet lines for **Component** and **Tags** (`- **Component**: …`, `- **Tags**: a, b`) only when present.
- [x] T006 [US1] In `src/Spectra.MCP/Reports/ReportWriter.cs` (HTML `RenderTestContent` ~:793-884): render **Priority**, **Component**, **Tags** as detail rows (e.g. `<div class="detail-row"><strong>Priority:</strong> …</div>`, tags joined/escaped), each omitted when empty; include them in the `hasContent` guard at :795-799.
- [x] T007 [US1] In `src/Spectra.MCP/Reports/ReportWriter.cs` (HTML "All Results" table): add `<th>Priority</th>` (~:766-771); add a `<td>` priority cell to the passing-row template (~:331-337); change non-passing `colspan="4"` → `colspan="5"` and add a priority cell to the `<summary>` (~:357-370); update CSS `grid-template-columns` (~:621) and `th/td:nth-child` width rules (~:608-610) for the new column.
- [x] T008 [P] [US1] In `tests/Spectra.MCP.Tests/Reports/ReportGeneratorTests.cs`: add tests that `Generate` populates `Priority`/`Tags`/`Component` from a supplied `testCases` dictionary, and leaves them null when the test case is absent or the collection is empty.
- [x] T009 [P] [US1] Create `tests/Spectra.MCP.Tests/Reports/ReportWriterEnrichmentTests.cs`: assert JSON contains `priority`/`tags`/`component` when present and omits them when empty; assert the Markdown "All Results" table has a `Priority` column; assert HTML output contains the priority/component/tags rendering for a non-passing test.

**Checkpoint**: User Story 1 fully works across all three formats and is independently testable.

---

## Phase 4: User Story 2 — Coverage linkage (Priority: P2)

**Goal**: Per-test **acceptance-criteria IDs** and **source-doc references** appear in JSON, Markdown,
and HTML; omitted when empty. (Schema/population already done in Phase 2.)

**Independent Test**: Generate a report for tests linking criteria/source docs and confirm all three
formats show them; tests without linkage omit them.

- [x] T010 [US2] In `src/Spectra.MCP/Reports/ReportWriter.cs` (Markdown Failed Tests detail block ~:162-189): emit bullet lines for **Criteria** and **Source Docs** (joined IDs/refs) only when present.
- [x] T011 [US2] In `src/Spectra.MCP/Reports/ReportWriter.cs` (HTML `RenderTestContent`): render **Criteria** and **Source Docs** as detail rows, omitted when empty; include in the `hasContent` guard.
- [x] T012 [P] [US2] In `tests/Spectra.MCP.Tests/Reports/ReportGeneratorTests.cs`: add tests that `Generate` populates `Criteria`/`SourceRefs` from `testCases` and normalizes empty collections to null.
- [x] T013 [P] [US2] In `tests/Spectra.MCP.Tests/Reports/ReportWriterEnrichmentTests.cs`: assert JSON contains `criteria`/`source_refs` when present and omits when empty; assert HTML renders them for a non-passing test.

**Checkpoint**: User Story 2 works across all three formats, independently of US3.

---

## Phase 5: User Story 3 — Run-level timing breakdown (Priority: P3)

**Goal**: A minimal run-level `timing` breakdown (total + average executed test duration) appears in
JSON, Markdown, and HTML; omitted when no test has a duration.

**Independent Test**: Generate a report where tests have durations and confirm `timing` appears in all
formats; a report with no durations omits it.

- [x] T014 [US3] Create `src/Spectra.Core/Models/Execution/ReportTiming.cs`: `sealed record ReportTiming` with `TotalTestDurationMs` (`long`, `[JsonPropertyName("total_test_duration_ms")]`) and `AverageTestDurationMs` (`long`, `[JsonPropertyName("average_test_duration_ms")]`).
- [x] T015 [US3] Add optional `Timing` property (`ReportTiming?`, `[JsonPropertyName("timing")]`, `[JsonIgnore(WhenWritingNull)]`) to `ExecutionReport` in `src/Spectra.Core/Models/Execution/ExecutionReport.cs`.
- [x] T016 [US3] In `src/Spectra.MCP/Reports/ReportGenerator.cs`: after building `entries`, compute `Timing` from entries with non-null `DurationMs` (sum + average rounded); set `Timing = null` when none; assign it on the returned `ExecutionReport`.
- [x] T017 [US3] In `src/Spectra.MCP/Reports/ReportWriter.cs`: render timing in the Markdown header block (~:124, e.g. `**Total Test Time**` / `**Avg per Test**` using `FormatDuration`) and in the HTML `meta-info` header (~:703-708), each only when `report.Timing` is not null.
- [x] T018 [P] [US3] In `tests/Spectra.MCP.Tests/Reports/ReportGeneratorTests.cs`: add tests that `Timing` is computed from durations and is null when no result has a duration.
- [x] T019 [P] [US3] In `tests/Spectra.MCP.Tests/Reports/ReportWriterEnrichmentTests.cs`: assert JSON contains `timing` with correct total/average when durations exist and omits it otherwise; assert MD/HTML show the timing lines.

**Checkpoint**: All three user stories complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T020 Update execution-report documentation to describe the new optional fields: refresh `docs/investigation/04-execution.md` report-field references and any report-schema doc; align with `specs/061-execution-report-enrichment/contracts/report-schema.md`.
- [x] T021 Run the full regression net: `dotnet build` then `dotnet test` — confirm the MCP engine/tool and state-machine tests are green (SC-005), and the new report tests pass.

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002–T003)** → user stories.
- **US1 (T004–T009)**, **US2 (T010–T013)**, **US3 (T014–T019)** all depend on Foundational. US3 also
  adds its own schema (T014–T015) independent of US1/US2.
- **Story independence**: US1, US2, US3 are independently testable and deliverable. US1 is the MVP.
- **File-sharing caveat**: US1 and US2 both edit `src/Spectra.MCP/Reports/ReportWriter.cs` (and
  `ReportGenerator.cs` is touched in Foundational). The rendering tasks within/across US1↔US2 that
  edit `ReportWriter.cs` are **sequential** (not `[P]`); only the test files (different files) are
  `[P]`.

## Parallel Opportunities

- Within US1: `T008` and `T009` (different test files) can run in parallel after T004–T007.
- Within US2: `T012` and `T013` in parallel after T010–T011.
- Within US3: `T018` and `T019` in parallel after T014–T017.
- Across stories: test-writing for a completed story can overlap with the next story's rendering work,
  but edits to `ReportWriter.cs` must be serialized.

## Implementation Strategy

- **MVP = Phase 1 + Phase 2 + Phase 3 (US1)**: delivers per-test priority/tags/component across all
  three formats — the highest-value slice — with JSON already carrying criteria/source-refs from
  Foundational.
- **Incremental**: add US2 (coverage linkage rendering), then US3 (run-level timing) as separate,
  independently shippable increments.
- **Regression discipline**: do not modify the MCP engine, tool surface, or state-machine tests; if
  one breaks, investigate as a regression (the change is additive only).
