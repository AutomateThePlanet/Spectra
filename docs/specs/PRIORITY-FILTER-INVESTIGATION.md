# Priority / Tag / Component Filter Ignored — Investigation

**Symptom:** Asking the MCP server to filter tests by priority (or tag /
component) — e.g. "run only the high-priority tests from suite X" — enqueues
**ALL** tests in the suite, not the filtered subset. The filter is silently
dropped (no error).

**Distinct from prior work:** `docs/specs/CURRENT-STATE-INVESTIGATION.md`
covered the *zero-results* symptom (tests missing from the index). This is the
inverse — *too many* (everything) — and was traced independently.

**Scope:** Observation only. No fixes proposed.

---

## Q1 — Every path that can apply a priority/tag/component filter

| # | Entry point | Accepts priority filter? | Filter actually applied? | Evidence (`path:line`) |
|---|---|---|---|---|
| 1 | `find_test_cases` | Yes — top-level `priorities` / `tags` / `components` (arrays) | **Yes** | `src/Spectra.MCP/Tools/Data/FindTestCasesTool.cs:88-104` |
| 2 | `start_execution_run` **suite mode** | Yes — nested `filters.priority` / `filters.tags` / `filters.component` | **Yes** (builds `RunFilters` → engine → queue applies) | `StartExecutionRunTool.cs:138-152`; `Execution/TestQueue.cs:32,221-272` |
| 3 | `start_execution_run` **selection mode** | Via saved-selection config (not request filters) | **Yes** (filters before engine) | `StartExecutionRunTool.cs:235`; `ListSavedSelectionsTool.cs:91-121` |
| 4 | `start_execution_run` **test_ids mode** | No — runs exactly the given ids; `request.Filters` is never passed | N/A — no filtering by design | `StartExecutionRunTool.cs:157-207` (engine call at `:201-204` omits filters) |

### Path-by-path detail

**1. `find_test_cases`** — `FindTestCasesTool.cs`
- Filter fields read from the request: `Priorities`, `Tags`, `Components`,
  `HasAutomation` (DTO `FindTestCasesRequest`, `:243-265`).
- Applied at `:88-92` (priority), `:94-98` (tags), `:100-104` (component),
  `:106-111` (automation). AND between types, OR within each array. **Filters
  are genuinely applied here.**

**2. `start_execution_run` suite mode** — `ExecuteWithSuiteAsync`,
`StartExecutionRunTool.cs:121-155` — THE PRIME SUSPECT, examined closely:
- Suite tests are loaded at `:130` (`_indexLoader(request.Suite)`).
- A `RunFilters` IS built from `request.Filters` at `:138-146` and IS passed to
  the engine at `:148-152` (`_engine.StartRunAsync(..., filters)`).
- The engine forwards it to `TestQueue.Build(runId, testEntries, filters)`
  (`ExecutionEngine.cs:59`), which calls `ApplyFilters`
  (`TestQueue.cs:32`, body `:221-272`): priority `:230-234`, tags `:236-241`,
  component `:243-247`, test_ids+deps `:249-269`.
- **Conclusion: suite mode DOES accept filters AND DOES apply them — *when the
  filter actually populates `request.Filters`*.** The hypothesis "suite mode
  enqueues the whole suite unfiltered" is FALSE at the code level. The defect is
  upstream, in whether `request.Filters` gets populated at all (see Q2).

**3. selection mode** — `ExecuteWithSelectionAsync`,
`StartExecutionRunTool.cs:209-250`
- Loads all tests `:228-233`, then `ListSavedSelectionsTool.ApplyFilters(allTests,
  selectionConfig)` at `:235`. `ApplyFilters` (`ListSavedSelectionsTool.cs:91-121`)
  filters on the *saved selection's* priorities/tags/components — not on
  request-supplied filters. Filtered set passed to engine at `:244-247`.
  Filters correctly applied (pre-engine).

**4. test_ids mode** — `ExecuteWithTestIdsAsync`,
`StartExecutionRunTool.cs:157-207`
- Resolves the exact ids `:177-197`, calls the engine at `:201-204` **without**
  a `RunFilters`. No metadata filtering — by design, the caller already chose
  the ids. (A `filters` block sent alongside `test_ids` would be ignored, but
  this is not the reported symptom.)

---

## Q2 — MCP request → parameter binding (the divergence)

**Deserialization is shared and lenient.** All tools call
`McpProtocol.DeserializeParams<T>` (`StartExecutionRunTool.cs:69`,
`FindTestCasesTool.cs:46`). It uses `SerializerOptions`
(`McpProtocol.cs:13-18,73-81`) which sets `PropertyNamingPolicy` +
`DefaultIgnoreCondition` but **does NOT set `UnmappedMemberHandling.Disallow`**
(grep: no occurrence of `UnmappedMemberHandling` anywhere in the repo).
→ **System.Text.Json silently ignores any JSON property that doesn't map to a
DTO field. No error is raised.** This is the mechanism that turns a misplaced
filter into a silent no-op.

**The two tools advertise *different shapes* for the same concept:**

| Concept | `find_test_cases` | `start_execution_run` |
|---|---|---|
| priority | `priorities` — **array, top-level** | `filters.priority` — **single string, nested** |
| tags | `tags` — array, top-level | `filters.tags` — array, nested |
| component | `components` — **array, top-level** | `filters.component` — **single string, nested** |

Evidence:
- `find_test_cases` schema `FindTestCasesTool.cs:28-30`; DTO
  `FindTestCasesRequest` `:243-265` (`priorities`/`tags`/`components`,
  all top-level lists).
- `start_execution_run` schema `StartExecutionRunTool.cs:35-46` (a nested
  `filters` object with singular `priority`, `tags`, `component`, `test_ids`);
  DTO `StartExecutionRunRequest` `:277-296` (only `Suite`, `TestIds`,
  `Selection`, `Name`, `Environment`, `Filters` — **no top-level
  priority/tag/component**); nested DTO `StartExecutionRunFilters` `:298-311`
  (`priority` singular `:300-301`, `tags` `:303-304`, `component` singular
  `:306-307`, `test_ids` `:309-310`).

**Prime-suspect answer:** `start_execution_run` *does* have filter params for
the suite path — but only **nested under `filters` and with singular
`priority`/`component`**. Any of these shapes silently drops the filter:
- top-level `priority` / `priorities` (the `find_test_cases`-style placement) —
  not a field on `StartExecutionRunRequest` → dropped → `Filters == null`.
- nested but plural `filters.priorities` / `filters.components` — not a field on
  `StartExecutionRunFilters` → dropped → `Filters` non-null but all members null.

In every dropped case `RunFilters.HasFilters` is `false`
(`RunFilters.cs:29-33`), so `TestQueue.ApplyFilters` returns the entries
unchanged (`TestQueue.cs:223-226`) → **whole suite enqueued.**

---

## Q3 — Agent-side behavior

Execution agent prompt:
`src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md` (bundled copy
`src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` is identical
for the relevant lines). Note: `.github/agents/spectra-execution.agent.md`
referenced in the task is **NOT FOUND** in this repo — that path is the install
target in a consuming project; the source-of-truth templates are the two paths
above.

**Routing for "run high-priority tests from suite X" (a suite IS named):**
- Main workflow step 3 (`:39`): "Ask which suite and **any filters (priority,
  tags, component)**". Step 4 (`:40`): "**Call `start_execution_run` with chosen
  suite and filters**". → routes to **suite mode**, NOT `find_test_cases`.
- The "Smart Test Selection" workflow (`:194-263`) is gated on "the user asks to
  run tests **without specifying a suite**" (`:196`). It is the path that uses
  `find_test_cases` (`:214`, with `priorities`/`tags`/`components`) and then
  `start_execution_run` with `test_ids` (`:231`, `:252`). It does **not** apply
  when a suite is named.

**The prompt only ever models the `find_test_cases` shape.** It shows
`priorities: ["high"]` (`:261-262`), `tags: ["payment"]` (`:250`), and lists
`priorities, tags, components` (`:216`). It **never shows the nested
`filters: { priority: "high" }` JSON shape** that suite mode requires — step 4
just says "with chosen suite and filters" with no example.

**Consequence:** when a suite is named, the agent is told to call
`start_execution_run` with a filter, but the only filter shape it has been shown
is the top-level plural `priorities`/`tags`/`components` form. Passing that to
`start_execution_run` lands the filter in a field the DTO doesn't have → Q2's
silent drop → the agent *believes* it filtered, but the tool ran the whole
suite. The path that genuinely filters (`find_test_cases`) is only reached when
the user omits the suite.

---

## Q4 — Data flow on paper

Concrete case: suite **`checkout`** has 20 tests, 5 of them `priority: high`.
User: *"run the high-priority tests from checkout."*

**Path A — `find_test_cases` → `test_ids` (only taken when no suite is named):**
1. `find_test_cases({ suites:["checkout"], priorities:["high"] })`
   → `FindTestCasesTool.cs:88-92` keeps the 5 high tests → `matched: 5`.
2. Agent calls `start_execution_run({ test_ids:[<5 ids>], name:"…" })`
   → `ExecuteWithTestIdsAsync` (`:157-207`) → engine gets exactly those 5.
   **Reaches `StartRunAsync` with 5 tests. ✅ correct.**

**Path B — suite mode, filter nested correctly:**
1. `start_execution_run({ suite:"checkout", filters:{ priority:"high" } })`
   → `ExecuteWithSuiteAsync` builds `RunFilters { Priority = High }`
   (`:138-146`) → `StartRunAsync(..., filters)` (`:148`) → `TestQueue.Build`
   → `ApplyFilters` priority branch (`TestQueue.cs:230-234`) keeps 5.
   **Reaches `StartRunAsync`/queue with 5 tests. ✅ correct.**

**Path C — suite mode, filter mis-shaped (THE BUG):** any of
- `start_execution_run({ suite:"checkout", priority:"high" })` (top-level singular)
- `start_execution_run({ suite:"checkout", priorities:["high"] })` (`find_test_cases` shape)
- `start_execution_run({ suite:"checkout", filters:{ priorities:["high"] } })` (nested but plural)

→ the unmapped property is silently discarded (`McpProtocol.cs:13-18`, no
`UnmappedMemberHandling.Disallow`). `request.Filters` is `null` (cases 1-2) or a
`StartExecutionRunFilters` with every member null (case 3). In
`ExecuteWithSuiteAsync`, `filters` is either `null` or a `RunFilters` whose
`HasFilters` is `false` (`RunFilters.cs:29-33`). `TestQueue.ApplyFilters` then
returns all entries unchanged (`TestQueue.cs:223-226`).
**Reaches `StartRunAsync`/queue with all 20 tests. ❌ returns everything — the
reported symptom.**

Path A and Path B return the correct 5. Path C returns all 20.

---

## Confirmed root cause

**A combination of (c) and (d), NOT (a) or (b):**

- **(d) primary** — the filter is passed in a field `start_execution_run`
  doesn't read. The tool's suite-mode filter lives **nested under `filters` with
  singular `priority`/`component`** (`StartExecutionRunTool.cs:35-46,277-311`),
  whereas `find_test_cases` uses **top-level plural `priorities`/`components`
  arrays** (`FindTestCasesTool.cs:28-30,243-265`). The MCP deserializer silently
  ignores unmapped properties (`McpProtocol.cs:13-18`), so a misplaced filter
  vanishes with no error → `RunFilters.HasFilters == false`
  (`RunFilters.cs:29-33`) → `TestQueue.ApplyFilters` returns all entries
  (`TestQueue.cs:223-226`) → whole suite runs.

- **(c) contributing** — the agent prompt routes "filter + named suite" to suite
  mode (`spectra-execution.agent.md:39-40`) but only ever demonstrates the
  `find_test_cases` filter shape (`:216,250,261-262`) and never the nested
  `filters:{…}` shape, steering the model to emit exactly the misplaced shape
  that (d) drops. The genuinely-filtering path (`find_test_cases` → `test_ids`)
  is only reached when no suite is named (`:196`).

- **NOT (a):** suite mode *does* expose filter params (nested `filters`,
  `:35-46`/`:294-311`).
- **NOT (b):** when the filter is correctly nested+singular, it *is* applied
  end-to-end (`:138-152` → `TestQueue.cs:221-272`). The filtering logic itself
  is correct; tests in `tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs`
  exercise the happy path with directly-constructed `RunFilters` and so never
  surface the binding-shape gap.

### Confidence

**High** that the mechanism is real and reproducible (silent drop of a
misplaced/misnamed filter → whole suite). **Medium** that it is the *sole* cause
of every user report, since an agent that happens to emit the exact
`filters:{priority:"…"}` shape would filter correctly — but because the prompt
never models that shape and the two tools diverge in naming, misplacement is the
likely default.

**Strongest evidence:** the schema/DTO divergence
(`StartExecutionRunTool.cs:35-46` + `:277-311` vs `FindTestCasesTool.cs:28-30` +
`:243-265`) combined with the lenient deserializer (`McpProtocol.cs:13-18`,
no `UnmappedMemberHandling`) and an agent prompt that only ever shows the
`find_test_cases` shape (`spectra-execution.agent.md:216,250,261-262`) while
routing named-suite filtering to suite mode (`:39-40`).

---

## Concrete MCP-call repro

Assume suite `checkout` = 20 tests, 5 with `priority: high`.

**Triggers the bug (test_count should be 5, returns 20):**
```json
{ "tool": "start_execution_run", "arguments": { "suite": "checkout", "priorities": ["high"] } }
```
```json
{ "tool": "start_execution_run", "arguments": { "suite": "checkout", "priority": "high" } }
```
```json
{ "tool": "start_execution_run", "arguments": { "suite": "checkout", "filters": { "priorities": ["high"] } } }
```
Each returns `data.test_count = 20` (the whole suite) — the `priority` filter is
silently dropped.

**Correct invocation (returns 5):**
```json
{ "tool": "start_execution_run", "arguments": { "suite": "checkout", "filters": { "priority": "high" } } }
```
Returns `data.test_count = 5`.

**Contrast — `find_test_cases` filters correctly with the top-level shape:**
```json
{ "tool": "find_test_cases", "arguments": { "suites": ["checkout"], "priorities": ["high"] } }
```
Returns `data.matched = 5`.

The difference between the broken and correct `start_execution_run` calls — same
intent, only the field name/placement differs, no error on the broken one — is
the crux of the symptom.
