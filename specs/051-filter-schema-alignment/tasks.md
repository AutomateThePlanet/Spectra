---

description: "Task list for Spec 051 — Filter Schema Alignment & Strict Deserialization"
---

# Tasks: Filter Schema Alignment & Strict Deserialization

**Input**: Design documents from `/specs/051-filter-schema-alignment/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Tests**: Included — the spec § Test Plan enumerates ten boundary tests, and the user-clarified decision (research.md D1) requires rewriting one existing test.

**Organization**: Grouped by the three user stories (US1 filtered runs actually filter; US2 actionable errors; US3 one agent filter shape).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: different file, no dependency on an incomplete task
- **[Story]**: `[US1]`/`[US2]`/`[US3]`; absent for Setup/Foundational/Polish
- File paths are explicit in every implementation task

## Path Conventions

- Core model: `src/Spectra.Core/Models/Execution/RunFilters.cs`
- Filter application: `src/Spectra.MCP/Execution/TestQueue.cs`
- Tool + DTO: `src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs`
- Protocol/registry: `src/Spectra.MCP/Server/McpProtocol.cs`, `src/Spectra.MCP/Server/ToolRegistry.cs`
- Agent prompts: `src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md`, `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md`
- Tests: `tests/Spectra.MCP.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green baseline and capture the exact current behavior before changing the boundary.

- [X] T001 Confirm branch `051-filter-schema-alignment`; run `dotnet build` from repo root — record baseline (expect 0 warnings / 0 errors).
- [X] T002 Run `dotnet test tests/Spectra.MCP.Tests` and record the baseline pass count (≈353). Note that `TestQueueFilterTests.Build_FilterByMultipleTags_RequiresAllTags` currently passes with AND semantics — it will be rewritten in US1.

**Checkpoint**: Baseline build + MCP tests green and recorded.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The unified `RunFilters` model and the strict-deserialization protocol primitives. US1 depends on the model; US2 depends on the protocol primitives. These are the only structural pieces; do them first.

**⚠️ CRITICAL**: US1 and US2 implementation cannot begin until this phase is complete.

### Unified filter model (serves US1)

- [X] T003 In `src/Spectra.Core/Models/Execution/RunFilters.cs`, add `IReadOnlyList<string>? Priorities` and `IReadOnlyList<string>? Components` (raw strings, per research.md D6) alongside the existing singular `Priority`/`Component`. Add a `static RunFilters From(IReadOnlyList<string>? priorities, IReadOnlyList<string>? tags, IReadOnlyList<string>? components)` factory. Extend `HasFilters` to also return true when `Priorities`/`Components` are non-empty. Do NOT change the `Tags`/`TestIds` fields' shape here.
- [X] T004 In `src/Spectra.MCP/Execution/TestQueue.cs` `ApplyFilters`, add OR-within-array branches: if `Priorities` non-empty → keep entries whose priority is in the (case-insensitive) set (mirror `FindTestCasesTool.cs:88-92`); if `Components` non-empty → keep entries whose component is in the set; keep the existing singular `Priority`/`Component` branches as the fallback when the plural field is absent. Change the tag branch from `.All(...)` (AND) to `.Any(...)` (OR) per research.md D1. Leave `TestIds` dependency recursion and the `HasFilters` early-return untouched.

### Strict deserialization primitives (serves US2)

- [X] T005 In `src/Spectra.MCP/Server/McpProtocol.cs`, add a dedicated `ParamsSerializerOptions` instance equal to the current params options plus `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`. Leave the existing envelope `SerializerOptions` (used by `ParseRequest`/`CreateResponse`) lenient and unchanged (research.md D2).
- [X] T006 In `src/Spectra.MCP/Server/McpProtocol.cs`, add a `McpInvalidParamsException` type (carries the final actionable message), a private `TryExtractUnmappedMember(JsonException, out string property, out string declaringType)` using the regex from `contracts/error-suggestions.md`, and a `SuggestFilterField(string toolName, string property, string declaringType)` implementing the closed map in `contracts/error-suggestions.md` (research.md D4).
- [X] T007 In `src/Spectra.MCP/Server/McpProtocol.cs`, change `DeserializeParams<T>` to `DeserializeParams<T>(JsonElement? paramsElement, string toolName)` using `ParamsSerializerOptions`; on `JsonException` where `TryExtractUnmappedMember` succeeds, throw `McpInvalidParamsException` with message `Property '{property}' is not valid on '{toolName}'. {suggestion ?? "Check the tool schema."}`. Null/Null-kind params still return `default` as today.
- [X] T008 In `src/Spectra.MCP/Server/ToolRegistry.cs` `InvokeAsync`, add a `catch (McpInvalidParamsException ex)` BEFORE the existing generic `catch (Exception)` that returns a structured `INVALID_PARAMS` tool error carrying `ex.Message`. Confirm no run/state is created on this path (FR-008).
- [X] T009 Update ALL remaining `DeserializeParams<T>(parameters)` call sites to pass the tool's registered name as the second argument. Files (per `grep DeserializeParams`): `FindTestCasesTool`, `GetTestExecutionHistoryTool`, `GetExecutionSummaryTool`, `GetRunHistoryTool`, `CancelAllActiveRunsTool`, `CancelExecutionRunTool`, `FinalizeExecutionRunTool`, `GetExecutionStatusTool`, `PauseExecutionRunTool`, `ResumeExecutionRunTool`, `StartExecutionRunTool`, `AddTestNoteTool`, `AdvanceTestCaseTool`, `BulkRecordResultsTool`, `GetTestCaseDetailsTool`, `RetestTestCaseTool`, `SaveClipboardScreenshotTool`, `SaveScreenshotTool`, `SkipTestCaseTool` (and any other match). Use the exact method name each tool is registered under.
- [X] T010 Run `dotnet build` — resolve compile errors from the signature change. Confirm 0 warnings (watch for CS0618 once US1 adds `[Obsolete]`; not yet present here).

**Checkpoint**: Unified `RunFilters` + OR application compile; strict deserialization wired through every tool and the registry. No DTO surface change yet.

---

## Phase 3: User Story 1 — Filtered suite run actually filters (Priority: P1) 🎯 MVP

**Goal**: `start_execution_run({ suite, priorities/tags/components })` filters the suite using OR-within-array / AND-between-fields; legacy nested shape still works; both-shapes → top-level wins + warning.

**Independent Test**: Start a suite-mode run with top-level `priorities: ["high"]`; assert `test_count` equals the high-priority count, not the full suite.

### Tests for User Story 1 (write/adjust first)

- [X] T011 [P] [US1] In `tests/Spectra.MCP.Tests/Execution/TestQueueFilterTests.cs`, REWRITE `Build_FilterByMultipleTags_RequiresAllTags` → `Build_FilterByMultipleTags_MatchesAnyTag` asserting OR semantics (`Tags = ["smoke","auth"]` now matches TC-001, TC-002, TC-003, TC-005 — every test having *either* tag). Add `Build_FilterByPriorities_OrSemantics` (`Priorities = ["high","low"]` → the 2 high + 1 low) and `Build_FilterByComponents_OrSemantics` (`Components = ["auth","payment"]` → union). These exercise the new plural fields directly on `RunFilters`.
- [X] T012 [P] [US1] In `tests/Spectra.MCP.Tests/Tools/StartExecutionRunTests.cs`, add `StartExecutionRun_TopLevelPriorities_FiltersSuite`, `StartExecutionRun_TopLevelMultiplePriorities_OrSemantics` (`["high","critical"]` → only the high tests; `critical` matches nothing), `StartExecutionRun_TopLevelMixedFilters_AndSemantics` (`priorities:["high"]` + `components:["payments"]` → intersection), `StartExecutionRun_LegacyNestedFilters_StillWorks`, and `StartExecutionRun_BothShapes_TopLevelWins_LogsWarning`. Drive through the tool's `ExecuteAsync` and assert `test_count`/queue contents and (for both-shapes) the warning.

### Implementation for User Story 1

- [X] T013 [US1] In `src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs`, add top-level `priorities`/`tags`/`components` (`List<string>?` with `[JsonPropertyName]`) to `StartExecutionRunRequest`. Mark the existing `Filters` property `[Obsolete("Use top-level priorities/tags/components instead.")]`.
- [X] T014 [US1] In the same file, add `private static RunFilters? NormalizeFilters(StartExecutionRunRequest req, List<string> warnings)` implementing research.md D7 precedence: top-level present → `RunFilters.From(...)` (+ warning if legacy `filters` also present); else legacy `filters` → lift singular→plural into `RunFilters`; else null. Wrap every read of the obsolete `req.Filters` in `#pragma warning disable CS0618` / `restore` (research.md D5). Replace the inline filter-construction in `ExecuteWithSuiteAsync` with a call to `NormalizeFilters`, and thread any warning into the response.
- [X] T015 [US1] In the same file, update `ParameterSchema`: add top-level `priorities`/`tags`/`components` array properties with descriptions matching `contracts/start_execution_run.schema.md`; change the nested `filters` object's description to mark it deprecated (`deprecated: true` style note). Keep both valid.
- [X] T016 [US1] In `FormatRunResponse` (same file), surface collected warnings (e.g. both-shapes) in the response payload's warning channel so `StartExecutionRun_BothShapes_TopLevelWins_LogsWarning` can assert it. Match how `find_test_cases` surfaces `warnings`.
- [X] T017 [US1] Run `dotnet test tests/Spectra.MCP.Tests --filter "TestQueueFilterTests|StartExecutionRunTests"`. Confirm T011/T012 tests pass and no other filter/start test regressed. Investigate any unexpected failure before proceeding (do not weaken assertions to go green).

**Checkpoint**: Suite runs filter correctly via the canonical shape; legacy shape works; both-shapes deterministic. US1 demoable.

---

## Phase 4: User Story 2 — Misplaced/misspelled fields produce an actionable error (Priority: P1)

**Goal**: Unmapped properties are rejected with a structured error naming the field and (for known filter confusions) suggesting the correct one; no run created.

**Independent Test**: Send `start_execution_run({ suite, priority: "high" })`; assert a structured invalid-params error naming `priority` and suggesting `priorities`, and that no run was created.

### Tests for User Story 2

- [X] T018 [P] [US2] In `tests/Spectra.MCP.Tests/Tools/StartExecutionRunTests.cs`, add `StartExecutionRun_UnknownField_ReturnsActionableError` (top-level singular `priority` → error message contains `priority` AND `priorities`; assert no run created) and `StartExecutionRun_NestedPluralBranch_ReturnsActionableError` (`filters: { priorities: ["high"] }` → error suggests top-level `priorities`). Drive through the registry path that surfaces `McpInvalidParamsException` as an `INVALID_PARAMS` response (or assert the thrown `McpInvalidParamsException` message directly via `DeserializeParams<StartExecutionRunRequest>(json, "start_execution_run")`).
- [X] T019 [P] [US2] In `tests/Spectra.MCP.Tests/Tools/` (new `FindTestCasesActionableErrorTests.cs` or extend an existing find-test-cases test file), add `FindTestCases_NestedFiltersObject_ReturnsActionableError` (`{ suites:["X"], filters:{ priority:"high" } }` → error explaining find_test_cases uses top-level arrays).
- [X] T020 [P] [US2] Add `tests/Spectra.MCP.Tests/Server/McpProtocolStrictTests.cs`: `DeserializerDisallow_AppliesToAllTools` (a clearly-unknown property on a representative non-filter tool → `McpInvalidParamsException`, not silent drop) and `UnmappedMemberMessage_ExtractsPropertyAndType` (validates the regex against the real .NET 8 exception message, guarding research.md's format assumption).

### Implementation for User Story 2

- [X] T021 [US2] No new production code beyond Phase 2 (T005–T009) is expected here — verify by reading that the strict path + suggestion map already cover the US2 scenarios. If `SuggestFilterField` is missing the nested-plural case (declaring type is the legacy filters type), add that branch in `McpProtocol.cs` per `contracts/error-suggestions.md`. Keep the map closed (no general fuzzy matcher).
- [X] T022 [US2] Run `dotnet test tests/Spectra.MCP.Tests --filter "StartExecutionRunTests|FindTestCases|McpProtocolStrict"`. Confirm all US2 tests pass and that the full MCP suite still builds. If any pre-existing tool test now fails because it sent an extra/unknown field, treat it as a real find: either the field is legitimate (add it to that DTO) or the test was asserting on a malformed request (fix the test) — document which in the task notes.

**Checkpoint**: Every silent-drop form is now an actionable error or a correct filter. US2 complete.

---

## Phase 5: User Story 3 — One filter shape in the agent prompt (Priority: P2)

**Goal**: The execution agent guidance shows exactly one filter shape, used identically for both tools, with no nested-filter example; source and bundled copies agree.

**Independent Test**: Audit-grep both agent files for `filters:` / nested examples; confirm zero remain and the named-suite example uses top-level `priorities`.

### Tests / audit for User Story 3

- [X] T023 [P] [US3] Audit-grep both `src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md` and `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` for `filters:`, `filters.`, `filters =`, and nested filter examples. Record every line that needs changing (the step 3-4 named-suite example and any divergent example).

### Implementation for User Story 3

- [X] T024 [US3] In `src/Spectra.CLI/Agent/Resources/spectra-execution.agent.md`: update the step 3-4 named-suite example to `start_execution_run({ suite: "checkout", priorities: ["high"], name: "Checkout high-priority smoke" })`; add the one-line callout "The same filter shape (`priorities`/`tags`/`components`) works on both `find_test_cases` and `start_execution_run`. No nested `filters` object."; remove/convert any remaining nested-shape example found in T023.
- [X] T025 [US3] Apply the identical edits to the bundled copy `src/Spectra.CLI/Skills/Content/Agents/spectra-execution.agent.md` so the two files' filter guidance matches (FR-010).
- [X] T026 [US3] Re-run the T023 audit-grep on both files; confirm zero nested-filter examples remain and both files contain the callout and the top-level named-suite example. Optionally `diff` the relevant regions to confirm parity.

**Checkpoint**: The agent is steered to emit the correct call on the first try. US3 complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T027 [US1] Add `tests/Spectra.MCP.Tests/Integration/FilteredExecutionTests.cs` test `RegressionGuard_PreviousSilentDropFormsNoLongerEnqueueWholeSuite`: for each of the prior silent-drop forms ({suite, priorities:[...]} now-valid; {suite, priority:"high"} now-error; {suite, filters:{priorities:[...]}} now-error), assert the outcome is either a correctly-filtered queue or an `INVALID_PARAMS` error — never a queue equal to the full suite size. (Tagged US1 because it guards the US1 user-visible outcome; depends on US1+US2 code.)
- [X] T028 [P] Docs: update `docs/generic-mcp.md` — document the canonical filter shape, one example used identically across both tools, note the legacy nested shape is deprecated.
- [X] T029 [P] Docs: update `docs/usage.md` troubleshooting — add a "Filter ignored / whole suite ran" entry explaining requests now surface as actionable errors, pointing at the canonical shape; note the tag AND→OR behavior change for any legacy nested caller.
- [X] T030 [P] Docs: update `PROJECT-KNOWLEDGE.md` — add the Spec 051 row; record that this closes the binding-side silent-failure class for MCP requests, and the unify-on-OR decision (tags AND→OR).
- [X] T031 [P] Docs: audit execution-related SKILLs (e.g. `src/Spectra.CLI/Skills/Content/Skills/spectra-execution.md` if present) for filter examples; ensure the top-level shape.
- [X] T032 Full sweep: `dotnet build` (Debug AND `-c Release` to catch CS0618/TreatWarningsAsErrors from the `[Obsolete]` member) then `dotnet test` across all three test projects. Confirm 0 warnings, 0 failures vs the Phase 1 baseline plus the new tests.
- [X] T033 Self-review the diff against `spec.md` File Audit and `plan.md`: confirm `FindTestCasesTool` filter logic is unchanged (only its `DeserializeParams` call gained a name), `HasFilters` meaning unchanged, `TestIds` recursion unchanged, no new dependency, no envelope-parser change. Note T027's manual-MCP smoke (quickstart.md) as a human pre-release step if not run here.

**Checkpoint**: Ready for PR.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: first.
- **Phase 2 (Foundational)**: after Setup. T003→T004 (model before application); T005→T006→T007→T008 (options→helpers→deserialize→registry catch); T009 after T007 (call sites need the new signature); T010 gates exit. BLOCKS US1 and US2.
- **Phase 3 (US1)**: after Phase 2. Tests T011/T012 before impl T013–T016; T017 gates.
- **Phase 4 (US2)**: after Phase 2. Largely validated by the Phase-2 primitives; tests T018–T020 then verify/patch T021; T022 gates. Can run in parallel with Phase 3 (different files: US1 touches RunFilters/TestQueue/StartExecutionRunTool DTO+normalize; US2 touches McpProtocol/registry + tests).
- **Phase 5 (US3)**: independent of Phases 3/4 (only agent markdown). Can start any time after Setup.
- **Phase 6 (Polish)**: after US1+US2 (T027 regression guard depends on both); docs after the behavior is final.

### Story dependencies

- **US1 (P1)**: needs Foundational model (T003/T004).
- **US2 (P1)**: needs Foundational protocol (T005–T009).
- **US3 (P2)**: no code dependency; markdown only.

### Parallel opportunities

- T003 and T005/T006 are different files → parallelizable, but keep T003→T004 and T005→T007 ordered within each track.
- US3 (T023–T026) can proceed fully in parallel with all code work.
- Docs T028–T031 are four independent files → parallel.
- Within US1 test authoring, T011 and T012 are different files → parallel.

---

## Parallel Example: Foundational two-track

```text
# Track A (model, serves US1):       T003 → T004
# Track B (protocol, serves US2):    T005 → T006 → T007 → T009 ; T008
# Converge at T010 (build).
```

## Parallel Example: Polish docs

```text
T028 [P] docs/generic-mcp.md
T029 [P] docs/usage.md
T030 [P] PROJECT-KNOWLEDGE.md
T031 [P] execution SKILL audit
```

---

## Implementation Strategy

### MVP (US1 + US2 together)

US1 and US2 are both P1 and together constitute the fix — US1 makes good requests filter, US2 makes bad requests fail loudly. Ship them in one PR; US3 (prompt) and docs ride along. There is no value in shipping US1 without US2 (a good request would work but a typo would still silently drop), nor US2 without US1 (errors with no correct shape to suggest toward).

### Suggested order

1. Phase 1 setup → 2. Phase 2 foundational (both tracks) → 3. US1 + US2 in parallel → 4. US3 prompt + Polish docs → 5. Release build + full sweep (T032) → 6. self-review (T033).

---

## Notes

- The riskiest change is enabling `Disallow` globally (T005/T007): it can surface pre-existing tools whose DTOs don't model every field a client sends. T022 explicitly budgets for triaging such failures honestly (add the field if legitimate; fix the test if the request was malformed) — do not silence by reverting strictness.
- Build in **Release** at least once (T032): the `[Obsolete]` `Filters` member + `TreatWarningsAsErrors` will fail Release unless the internal reads are `#pragma`-guarded (T014).
- Keep the suggestion map closed (research.md D4); resist adding a general fuzzy matcher.
- Per CLAUDE.md: default to no comments; structured results, never throw on validation — note the deliberate exception is the boundary `McpInvalidParamsException`, which is caught at the registry and rendered as a structured `INVALID_PARAMS` result, preserving the "tools return structured results" contract.
- Do NOT change `TestQueue` `HasFilters` meaning, `TestIds` recursion, `ExecutionEngine`, or `FindTestCasesTool` filter logic.
