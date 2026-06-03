# Implementation Plan: Filter Schema Alignment & Strict Deserialization

**Branch**: `051-filter-schema-alignment` | **Date**: 2026-06-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/051-filter-schema-alignment/spec.md`

## Summary

Close the silent-filter-drop class of bug at the MCP request boundary in three coordinated parts:

- **Part A** — `start_execution_run` accepts the same top-level plural filter shape (`priorities`/`tags`/`components`) as `find_test_cases`, normalizing whichever shape arrived into one internal `RunFilters`. The legacy nested `filters: { priority, tags, component }` stays accepted but deprecated.
- **Part B** — the MCP parameter deserializer enables `UnmappedMemberHandling.Disallow` and translates the resulting error into a structured invalid-params response that names the offending field and, for known filter confusions, suggests the correct one.
- **Part C** — the execution agent prompt (both source and bundled copies) shows exactly one filter shape, demonstrated identically for both tools.

**Resolved design decision (clarified with user):** Unify on `find_test_cases` semantics — `RunFilters` gains plural `Priorities`/`Components`, and run-mode tag matching changes from AND to OR so all filters obey "OR-within-array, AND-between-fields." The one existing test that pinned tag-AND (`TestQueueFilterTests.Build_FilterByMultipleTags_RequiresAllTags`) is rewritten to expect OR. The spec's "no application-logic change" (FR-011/AC-8) is reinterpreted narrowly: `HasFilters` meaning and `TestIds` dependency-inclusion are unchanged; per-field matching becomes OR-within-array. Recorded in research.md (D1).

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: System.Text.Json (MCP serialization), ASP.NET Core (MCP server)
**Storage**: File-based test indexes (`test-cases/{suite}/_index.json`); no storage change in this feature
**Testing**: xUnit (`tests/Spectra.MCP.Tests/`); boundary integration tests added under `Tools/`, `Server/`, `Integration/`; existing `Execution/TestQueueFilterTests.cs` updated for OR-tag semantics
**Target Platform**: Cross-platform .NET 8 MCP server (stdio/HTTP)
**Project Type**: Multi-project .NET solution; this feature touches `Spectra.MCP` (DTOs, protocol, registry), `Spectra.Core` (`RunFilters` model), and `Spectra.CLI` (agent prompt resources)
**Performance Goals**: No measurable change — strict deserialization adds a property-name check per request; filtering cost is unchanged
**Constraints**: Release builds set `TreatWarningsAsErrors=true`, so internal use of the `[Obsolete]` `Filters` property MUST be wrapped in `#pragma warning disable CS0618` to avoid breaking the Release build. The JSON-RPC envelope parser MUST stay lenient (clients may add envelope fields); only the tool-*params* deserializer becomes strict.
**Scale/Scope**: 19 `DeserializeParams` call sites gain a `toolName` argument (mechanical); 3 production source files of substance (`StartExecutionRunTool`, `RunFilters`, `McpProtocol`) plus `ToolRegistry`; 2 agent-prompt copies; ~10 new boundary tests; 1 existing test rewritten.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applies? | Compliance | Notes |
|-----------|----------|------------|-------|
| I. GitHub as Source of Truth | Yes | ✅ | No storage, index, or file-format change. Filters are request-shape only. |
| II. Deterministic Execution | Yes | ✅ | Strengthens determinism: the silent-drop non-determinism (request believed-filtered but wasn't) is removed; both-shapes precedence is a deterministic rule (top-level wins + warning). No state-machine change. |
| III. Orchestrator-Agnostic Design | Yes | ✅ | Tool responses stay minimal and self-contained. Actionable error messages make the boundary *more* orchestrator-agnostic (any LLM self-corrects from the suggestion). No bidirectional sync. |
| IV. CLI-First Interface | Yes | ✅ | No CLI command/flag change. MCP tool schema change is additive (new top-level filters) plus a deprecation marker; both shapes still validate. Tools remain self-contained. |
| V. Simplicity (YAGNI) | Yes | ✅ (1 note) | Hand-curated suggestion map (few entries) is chosen over a general fuzzy matcher (explicitly declined — Out of Scope). Legacy shape deprecated-not-removed per back-compat. See Complexity Tracking for the singular+plural `RunFilters` coexistence note. |

**Gate result**: PASS. One minor justified item tracked below; no unjustified violation.

### Post-design re-check (after Phase 1)

Re-evaluated after `research.md`, `data-model.md`, `contracts/`, `quickstart.md`:

- **I/II/III/IV** unchanged — confirmed additive request-shape + boundary-validation only; no engine/state/storage change. ✅
- **V** — the suggestion map stays small and closed (research.md D4); singular+plural coexistence in `RunFilters` is the minimum that preserves existing callers/tests while adding OR capability (Complexity Tracking). No general matcher introduced. ✅

**Re-check result**: PASS.

## Project Structure

### Documentation (this feature)

```text
specs/051-filter-schema-alignment/
├── plan.md              # This file
├── spec.md              # /speckit.specify output
├── research.md          # Phase 0 — decisions (incl. the unify-on-OR clarification)
├── data-model.md        # Phase 1 — RunFilters + request DTO shapes
├── quickstart.md        # Phase 1 — repro + reviewer checklist
├── contracts/
│   ├── start_execution_run.schema.md   # canonical + deprecated filter shapes
│   └── error-suggestions.md            # unmapped-member error + suggestion contract
├── checklists/
│   └── requirements.md  # /speckit.specify validation (all pass)
└── tasks.md             # /speckit.tasks output
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/Execution/
│       └── RunFilters.cs            # MODIFIED — add plural Priorities/Components; From(...) factory; tags now OR; HasFilters extended
├── Spectra.MCP/
│   ├── Server/
│   │   ├── McpProtocol.cs           # MODIFIED — strict params options (Disallow); DeserializeParams<T>(args, toolName); McpInvalidParamsException; unmapped-member extraction + suggestion
│   │   └── ToolRegistry.cs          # MODIFIED — InvokeAsync catches McpInvalidParamsException → structured INVALID_PARAMS
│   └── Tools/
│       ├── RunManagement/
│       │   └── StartExecutionRunTool.cs   # MODIFIED — top-level plural fields on request DTO; NormalizeFilters; schema update; [Obsolete] legacy Filters; pass toolName
│       ├── Data/FindTestCasesTool.cs      # MODIFIED — pass toolName (schema already canonical; filter logic unchanged)
│       └── **/*.cs                         # MODIFIED (mechanical) — every remaining DeserializeParams call passes its toolName
└── Spectra.CLI/
    ├── Agent/Resources/spectra-execution.agent.md      # MODIFIED — one filter shape; named-suite example; callout; remove nested example
    └── Skills/Content/Agents/spectra-execution.agent.md# MODIFIED — mirror of the above

tests/
└── Spectra.MCP.Tests/
    ├── Execution/TestQueueFilterTests.cs   # MODIFIED — tag test rewritten AND→OR; add plural-priority/component OR tests
    ├── Tools/StartExecutionRunTests.cs      # MODIFIED/ADD — top-level filters, legacy, both-shapes, actionable-error cases
    ├── Server/McpProtocolStrictTests.cs     # ADD — Disallow applies across tools; suggestion extraction
    └── Integration/FilteredExecutionTests.cs# MODIFIED/ADD — RegressionGuard: prior silent-drop forms never enqueue whole suite
```

**Structure Decision**: Changes are confined to the MCP request boundary (`Spectra.MCP` protocol/registry/tool DTOs), one shared model (`Spectra.Core` `RunFilters`), and the CLI-bundled agent prompt. No new project, no new dependency. Filter *application* (`TestQueue.ApplyFilters`) is edited only to add OR-within-array branches for the new plural fields and to flip tag matching to OR — the minimal change required by the user-clarified unify decision.

## Complexity Tracking

| Item | Why needed | Simpler alternative rejected because |
|------|------------|--------------------------------------|
| `RunFilters` keeps singular `Priority`/`Component` *and* adds plural `Priorities`/`Components` | The legacy nested shape and a body of existing unit tests construct `RunFilters` with the singular fields; the new top-level shape needs plural OR. Keeping both preserves back-compat with zero churn to passing priority/component tests. | Replacing singular with plural outright would force rewriting every existing priority/component filter test and break any in-flight legacy caller — larger blast radius for no user-visible gain this release. Removal of the singular fields is deferred to the same future cleanup that removes the nested shape. |
