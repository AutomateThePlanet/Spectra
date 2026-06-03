# Phase 0 Research: Filter Schema Alignment & Strict Deserialization

**Branch**: `051-filter-schema-alignment`
**Date**: 2026-06-03
**Spec**: [spec.md](./spec.md)

## Status

One genuine spec-internal contradiction was surfaced and resolved with the user before planning (D1). All other decisions follow directly from the spec body. No open `NEEDS CLARIFICATION` remain.

## Decisions

### D1 — Unify run filters on `find_test_cases` semantics (clarified with user)

**Context**: The spec simultaneously requires (FR-002) that `start_execution_run` filter semantics "match the discovery operation exactly" — OR-within-array, including tags as OR — and (FR-011/AC-8) that filter application logic "MUST NOT change" with "pre-existing filter-application tests pass unmodified." These cannot both hold:

- Current `RunFilters` (`Spectra.Core/Models/Execution/RunFilters.cs`) is **singular**: `Priority? Priority`, `string? Component`. It cannot represent `priorities: ["high","critical"]`.
- Current `TestQueue.ApplyFilters` (`Spectra.MCP/Execution/TestQueue.cs:221-272`) matches tags with **AND** (`Tags.All(...)`), while `find_test_cases` (`FindTestCasesTool.cs:94-98`) matches tags with **OR** (`Tags.Any(...)`).
- An existing test, `TestQueueFilterTests.Build_FilterByMultipleTags_RequiresAllTags`, pins the AND behavior.

**Decision** (user-selected, "Unify on find_test_cases (OR)"): Add plural `Priorities`/`Components` to `RunFilters`; change run-mode tag matching from AND to OR. Rewrite the single tag-AND test to expect OR. Reinterpret FR-011/AC-8 narrowly: `HasFilters` *meaning* and `TestIds` dependency-inclusion are unchanged; per-field matching becomes OR-within-array. The result is one filter rule across both tools, which is the spec's stated central goal ("users learn one rule").

**Rationale**: The spec body, the File Audit ("`FromLegacyNested(...)` lift singular→plural"), and FR-002 overwhelmingly intend plural OR semantics. The "no application-logic change" line is the outlier and literally describes the *old* behavior the feature exists to change. Preserving a single contradicting test at the cost of the feature's central promise is the wrong trade.

**Alternatives considered**:
- *Additive, keep tags AND* — multi-value priorities/components work but tags differ between the two tools; violates FR-002's "exactly" and the one-rule promise. Rejected by user.
- *Binding-only, single value* — keep `RunFilters` singular, take first value, error on multiple. Smallest change, fully preserves AC-8, but fails the OR acceptance scenario (`StartExecutionRun_TopLevelMultiplePriorities_OrSemantics`). Rejected by user.

**Documented consequence**: Existing run callers who passed `filters: { tags: ["a","b"] }` expecting AND will now get OR results. This is intended unification. The behavior change is called out in the docs update (usage troubleshooting) and the PROJECT-KNOWLEDGE entry.

### D2 — Strict deserialization scoped to tool *params*, not the JSON-RPC envelope

**Decision**: Introduce a second `JsonSerializerOptions` instance in `McpProtocol` for tool-parameter deserialization with `UnmappedMemberHandling.Disallow`. The existing options used by `ParseRequest`/`CreateResponse` (the JSON-RPC envelope) stay lenient.

**Rationale**: FR-005 targets unmapped properties "on every tool" — i.e., tool params, where the silent filter drop happens. The JSON-RPC envelope (`McpRequest`: jsonrpc/id/method/params) should stay forward-compatible: clients legitimately add envelope-level fields, and rejecting them would break interoperability (Principle III). Strictness belongs at the params boundary only.

**Alternatives considered**:
- *Single shared strict options* (as the spec pseudocode literally shows) — would also reject unknown envelope fields, risking JSON-RPC interop breakage. Rejected; the spec's intent is the params boundary, not the transport envelope.

### D3 — `DeserializeParams<T>(JsonElement?, string toolName)`; typed `McpInvalidParamsException`; suggestion at deserialize time

**Decision**: Change `McpProtocol.DeserializeParams<T>` to take the calling tool's name. On an unmapped-member `JsonException`, extract the offending property name *and* its declaring type from the exception message, compute a suggestion, and throw a typed `McpInvalidParamsException` carrying a structured, actionable message. `ToolRegistry.InvokeAsync` catches `McpInvalidParamsException` and emits an `INVALID_PARAMS` structured error (it already wraps `ExecuteAsync` in try/catch and knows the method/tool name). All 19 existing call sites pass their tool name (mechanical).

**Rationale**: The tool name is required to produce a tool-specific suggestion, and System.Text.Json does not expose the unmapped property structurally — it must be parsed from the message (`The JSON property '{prop}' could not be mapped to any .NET member contained in type '{type}'.`). Passing the name into `DeserializeParams` makes the full actionable error available *and* unit-testable per tool (a test can call `DeserializeParams<StartExecutionRunRequest>(json, "start_execution_run")` and assert the message), which the spec's Test Plan requires. Catching in `InvokeAsync` keeps the structured-response formatting in one chokepoint.

**Alternatives considered**:
- *Catch raw `JsonException` only in `InvokeAsync`, no signature change* — avoids touching 19 call sites but makes the per-tool error untestable without going through the registry, and pushes message-parsing into the dispatch layer. The spec explicitly proposes the `toolName` parameter; matched.
- *Suggestion keyed on `(toolName, field)` only* — insufficient to distinguish top-level singular `priority` (suggest plural) from nested `filters: { priorities }` (suggest top-level), because both surface different declaring types. Decision keys on `(toolName, field, declaringType)`.

### D4 — Suggestion map is small, closed, and keyed on declaring type

**Decision**: A hand-written suggestion function covers exactly the known filter confusions:
- `start_execution_run` + top-level singular `priority`/`component`/`tag` → "Use 'priorities'/'components'/'tags' (array) at the top level."
- `start_execution_run` + plural field inside the legacy `filters` object (declaring type is the nested filters type) → "Use top-level '{field}', not nested under 'filters'."
- `find_test_cases` + `filters` → "find_test_cases uses top-level 'priorities'/'tags'/'components', not a nested 'filters' object."
- Anything else → generic "'{prop}' is not a valid property on '{tool}'. Check the tool schema."

**Rationale**: The known confusions are few and high-value; a general-purpose fuzzy/Levenshtein suggester adds surface and unpredictable output for marginal gain — explicitly Out of Scope in the spec (Principle V).

### D5 — `[Obsolete]` legacy `Filters`, with `#pragma` guard for the Release build

**Decision**: Mark the legacy nested `StartExecutionRunRequest.Filters` property `[Obsolete("Use top-level priorities/tags/components instead.")]` and mark the nested schema branch `deprecated: true` in its description. Internal reads of `Filters` (in `NormalizeFilters`) are wrapped in `#pragma warning disable/restore CS0618`.

**Rationale**: `Directory.Build.props` sets `TreatWarningsAsErrors=true` for Release; without the pragma, the unavoidable internal read of the obsolete member would fail the Release build. Debug's `NoWarn` does not include CS0618, so the pragma is needed for both configurations to be safe. Deprecate-not-remove preserves back-compat (Out of Scope: removal).

### D6 — `RunFilters.From(...)` takes raw string lists; priority matched as string (like `find_test_cases`)

**Decision**: `RunFilters.From(priorities, tags, components)` stores the plural filters as raw `IReadOnlyList<string>` and `ApplyFilters` matches them case-insensitively via a `HashSet<string>.Contains` — mirroring `FindTestCasesTool` exactly. The legacy singular `Priority` stays a parsed `Priority?` enum (back-compat).

**Rationale**: `find_test_cases` never enum-parses priority; it string-matches. The spec's own OR test uses `priorities: ["high","critical"]`, and `critical` is **not** a member of the `Priority` enum (`High`/`Medium`/`Low`). Enum-parsing would throw on `critical`; string-matching simply yields no match for it (union = the `high` tests), which is the test's expected behavior and matches the reference tool. Storing raw strings is therefore both correct and simpler.

**Alternatives considered**:
- *Parse plural priorities to `Priority` enums* — throws on values outside the enum, diverging from `find_test_cases` leniency and breaking the OR test. Rejected.

### D7 — Both-shapes precedence: top-level wins, warning recorded

**Decision**: When a single request carries both top-level plural filters and the legacy nested `filters`, apply the top-level shape, ignore the nested, and surface a warning (in the run response `warnings`/notes channel, consistent with how `find_test_cases` surfaces warnings). Deterministic regardless of JSON field order.

**Rationale**: A deterministic precedence rule is clearer than erroring on the ambiguity; the canonical shape is the obvious winner (FR-004).

## Non-decisions (out of scope, confirmed)

- General fuzzy field suggestion across all DTOs — declined (D4).
- Removing the legacy nested shape or the singular `RunFilters` fields — deferred to a future cleanup gated on usage telemetry.
- Changing `HasFilters` meaning or `TestIds` dependency-inclusion — unchanged (D1 scope boundary).
- Changing `find_test_cases` (already canonical; it is the reference, not the target).
- Value-level matching semantics (casing/partial/enum normalization) beyond the AND→OR tag change required by D1.

## Unknowns

None. The exception-message format for `UnmappedMemberHandling.Disallow` in .NET 8 is the documented `The JSON property '{prop}' could not be mapped to any .NET member contained in type '{type}'.`; the extraction regex is specified in `contracts/error-suggestions.md` and will be validated by a unit test (`McpProtocolStrictTests`) rather than assumed.
