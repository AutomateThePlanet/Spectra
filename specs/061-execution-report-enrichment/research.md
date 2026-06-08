# Research: Execution Report Enrichment

All open questions from the spec were resolved by reading the live code. No `NEEDS CLARIFICATION`
remained after grounding.

## R1 — Is the source data already available to the report generator?

**Decision**: Yes. Populate the new fields from the `testCases` dictionary `ReportGenerator.Generate`
already receives.

**Rationale**: `FinalizeExecutionRunTool` loads the full `TestCase` for every distinct result test id
via `_testCaseLoader` and passes the dictionary to `Generate`
(`src/Spectra.MCP/Tools/RunManagement/FinalizeExecutionRunTool.cs:89-104`). `ReportGenerator` already
reads `tc?.Preconditions`, `tc?.Steps`, `tc?.ExpectedResult`, `tc?.TestData` from it
(`src/Spectra.MCP/Reports/ReportGenerator.cs:47-51`). The five new fields — `Priority`, `Tags`,
`Component`, `Criteria`, `SourceRefs` — all exist on `TestCase`
(`src/Spectra.Core/Models/TestCase.cs:25,30,35,65,96`). No new plumbing into the engine is needed
(satisfies FR-003, FR-005).

**Alternatives considered**: Threading new data through the MCP server — rejected; the data is already
there and the constitution forbids unnecessary plumbing (Principle V).

## R2 — How should the new fields be typed and serialized for backward compatibility?

**Decision**: Add optional/nullable properties to `TestResultEntry`, each with
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, matching the existing pattern
(`TestResultEntry.cs:29,34,39,...`). Collections are populated as `null` when empty so they are
omitted rather than serialized as `[]`.

- `priority` → `Priority?` with `[JsonConverter(typeof(JsonStringEnumConverter))]` (consistent with how
  `status` is serialized in this report family — enum name as string).
- `tags` → `IReadOnlyList<string>?`
- `component` → `string?`
- `criteria` → `IReadOnlyList<string>?` (acceptance-criteria IDs)
- `source_refs` → `IReadOnlyList<string>?` (source-doc references)

**Rationale**: `record` types here are `required`-heavy; all existing construction sites use object
initializers, so adding optional `init` properties does not break them (they default to `null`). JSON
consumers (the dashboard) tolerate additive fields. Snake-case naming is automatic via
`JsonNamingPolicy.SnakeCaseLower` (`ReportWriter.cs:20`) — `SourceRefs` → `source_refs`,
`ExpectedResult`-style names already prove the convention. Satisfies FR-001, FR-002, FR-007.

**Alternatives considered**: Serializing `priority` as a lowercase string ("high") to match the
`_index.json` convention — rejected for intra-report consistency; the report already serializes its
enums (`status`, filter `priority`) via `JsonStringEnumConverter`, so we match the report, not the
index.

## R3 — Where do the new fields render in Markdown and HTML?

**Decision**: Follow the report's existing altitude (see plan "Rendering Altitude Decision").

- **JSON**: automatic for every entry.
- **Markdown** (`ReportWriter.cs:111-243`): add a `Priority` column to the "All Results" table (every
  test); render `Component`, `Tags`, `Criteria`, `Source Docs` as bullet lines in the per-test detail
  blocks (the "Failed Tests" section), each omitted when empty.
- **HTML** (`ReportWriter.cs:262-788`, `RenderTestContent` ~:793-884): add the five fields inside
  `RenderTestContent` (omit when empty), and add a `Priority` column to the "All Results" table so
  passing tests also surface priority.
- **Run-level timing**: MD header block + HTML `meta-info`, omitted when unavailable.

**Rationale**: The existing report only deep-renders non-passing tests in MD/HTML while JSON carries
everything; matching that altitude is minimal and consistent. Priority gets a table column because it
is a scalar and the primary triage field in User Story 1, and the table is the only per-test surface
for passing tests in MD/HTML.

**Alternatives considered**: (a) Making every passing row expandable to show all five fields —
rejected as a larger structural change to row rendering with no clear added value over JSON for
passing tests. (b) Adding all five as table columns — rejected; list-valued fields (tags, criteria,
source-refs) bloat a fixed-layout table.

## R4 — HTML "All Results" table column addition mechanics

**Decision**: Adding the `Priority` column requires coordinated edits because the table is
`table-layout: fixed` and non-passing rows use `colspan` + a CSS grid summary:
1. Add `<th>Priority</th>` to the `<thead>` (`ReportWriter.cs:766-771`).
2. Passing rows (`ReportWriter.cs:331-337`): add a `<td>` for priority.
3. Non-passing rows (`ReportWriter.cs:357-370`): change `colspan="4"` → `colspan="5"` and add a
   priority cell to the `<summary>` grid.
4. CSS: update `.table-details summary { grid-template-columns: 100px 1fr 100px 80px }`
   (`ReportWriter.cs:621`) to include the new column, and the `th:nth-child`/`td:nth-child` width
   rules (`ReportWriter.cs:608-610`).

**Rationale**: Enumerated precisely so the change is mechanical and low-risk. Priority value rendered
via a small helper (e.g. `PriorityText(t.Priority)`) returning `"-"` (or empty) when null.

**Alternatives considered**: A priority badge inside the title cell to avoid column-count math —
viable but less table-native; kept as fallback if grid edits prove fragile.

## R5 — Run-level enrichment scope (FR-006, MAY)

**Decision**: Add a single minimal, derived `timing` breakdown to `ExecutionReport`: total executed
test duration and average per executed test, computed from `Results[].DurationMs` already in the
report. Optional/nullable; `null` when no results carry a duration.

**Rationale**: Satisfies FR-006 non-trivially with zero new plumbing — it is a pure function of data
already present. Environment/CI metadata was considered but the run already exposes `environment`; any
richer CI metadata would require new plumbing into the engine, which FR-003 forbids. Timing is the
highest-value, lowest-risk run-level addition.

**Alternatives considered**: (a) No run-level addition (FR-006 is a MAY) — acceptable but leaves the
run-level capability undemonstrated. (b) CI/environment metadata — rejected (needs plumbing). Chose
the derived timing breakdown as the bounded middle.

## R6 — Test & fixture impact

**Decision**: Net-new tests in `Spectra.MCP.Tests/Reports`; no rewrites.

**Rationale**: There are **no golden-file** MD/HTML report fixtures. The only report assertions are
existence checks (`FinalizeExecutionRunTests.cs:170-179` asserts files are generated, not their
content). `ReportGeneratorTests`/`ReportGeneratorFixesTests` assert specific fields and will be
extended additively. The MCP engine/state-machine tests are the untouchable regression net and are
not affected by an additive schema/rendering change (FR-005, SC-005).

**Alternatives considered**: Introducing golden-file snapshot tests — out of scope and heavier than
the established per-field assertion style.
