# Feature Specification: Filter Schema Alignment & Strict Deserialization

**Feature Branch**: `051-filter-schema-alignment`
**Created**: 2026-06-03
**Status**: Draft
**Input**: User description: "Close the silent-filter-drop class of bugs at the MCP request boundary. Make start_execution_run accept the same top-level plural filter shape as find_test_cases; keep the legacy nested filters working as deprecated. Enable strict deserialization with helpful suggestion-bearing errors so misplaced/misspelled fields produce an actionable error instead of being silently dropped. Update the execution agent prompt to show one filter shape consistent across both tools."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filtered suite run actually filters (Priority: P1)

A tester asks the execution agent "run only the high-priority tests from the checkout suite." Today the run silently includes the *entire* suite — the priority filter is dropped at the request boundary, and both the agent and the user believe filtering happened. After this change, the same request produces a run containing **only** the high-priority tests, and the reported test count reflects the filtered set, not the whole suite.

**Why this priority**: This is the user-visible defect the spec exists to fix. A filter that silently does nothing is worse than an error — the user proceeds on a false belief about what is being executed, wasting time and potentially shipping on the wrong evidence.

**Independent Test**: Start a suite-mode run requesting only high-priority tests, using the canonical (top-level plural) filter shape. Confirm the run's queued/reported test count equals the number of high-priority tests in that suite, not the suite's full size.

**Acceptance Scenarios**:

1. **Given** a suite with a mix of priorities, **When** a run is started requesting `priorities: ["high"]` at the top level, **Then** only high-priority tests are enqueued and the reported count equals the matched set.
2. **Given** a request with multiple values in one filter array (`priorities: ["high", "critical"]`), **When** the run starts, **Then** tests matching *any* of those values are included (OR within a single filter).
3. **Given** a request combining two different filters (`priorities: ["high"]` and `components: ["payments"]`), **When** the run starts, **Then** only tests matching *both* conditions are included (AND between filters).
4. **Given** the same filter values, **When** applied through `start_execution_run` versus when previewed through the test-discovery tool, **Then** both return the same matched set — one filter rule, learned once, behaves identically across tools.

---

### User Story 2 - Misplaced or misspelled fields produce an actionable error, never a silent drop (Priority: P1)

A tester (or the agent on their behalf) sends a run request with a filter field in the wrong place or under the wrong name — for example the singular `priority` at the top level, or a nested `filters` object where the discovery tool expects top-level arrays. Today such fields are silently discarded and the run proceeds unfiltered. After this change, the request is rejected with a clear, structured error that names the offending field and — for the known filter confusions — tells the sender exactly which field to use instead, so the next attempt succeeds.

**Why this priority**: Strict rejection is what converts the entire silent-failure class into a self-correcting one. Without it, every future schema drift re-opens the same trap. The actionable suggestion is what makes the strictness helpful rather than merely obstructive — the agent's very next call lands on the right field.

**Independent Test**: Send a run request containing an unmapped property (e.g. top-level singular `priority`). Confirm the response is a structured invalid-parameters error whose message names `priority` and suggests the plural `priorities`. Confirm no run is started.

**Acceptance Scenarios**:

1. **Given** a run request with top-level singular `priority`, **When** it is received, **Then** it is rejected with an error naming `priority` and suggesting the top-level `priorities` array; no run is created.
2. **Given** a run request whose nested legacy object contains a plural field it does not define (e.g. `filters: { priorities: [...] }`), **When** received, **Then** it is rejected with an error pointing the sender to the top-level `priorities` field.
3. **Given** a discovery request that wraps filters in a nested `filters` object, **When** received, **Then** it is rejected with an error explaining the discovery tool uses top-level filter arrays.
4. **Given** any tool receiving a clearly invalid (unknown) property, **When** received, **Then** it is rejected with a structured error rather than silently ignoring the property.

---

### User Story 3 - The agent is steered toward one correct filter shape (Priority: P2)

A user phrases a filtered-run request in natural language. The execution agent must translate it into a tool call. Today the agent's guidance shows the discovery tool's filter shape but routes filtered named-suite runs to a tool whose example never demonstrated the correct shape, so the agent emits the broken form. After this change, the agent's guidance presents exactly one filter shape, demonstrated identically for both the discovery and run-start tools, with an explicit note that no nested filter object is used — so the agent emits the correct call on the first try.

**Why this priority**: This removes the *source* of the most common bad request. Parts 1 and 2 make a bad request fail loudly and a good request succeed; Part 3 makes the agent produce the good request in the first place, reducing error round-trips. It is P2 because the strict-error path (US2) already prevents silent failure even if the prompt were left unchanged.

**Independent Test**: Provide the agent guidance to a fresh reader (human or model) and confirm there is exactly one filter shape shown, used consistently for both tools, with no remaining nested-filter example. Confirm the bundled/installed copy matches the source copy.

**Acceptance Scenarios**:

1. **Given** the execution agent guidance, **When** audited for filter examples, **Then** every filter example uses the top-level plural shape and none uses a nested filter object.
2. **Given** a request to "run high-priority tests from a named suite," **When** the agent constructs the call, **Then** it uses the top-level plural filter shape.
3. **Given** the source agent guidance and its bundled/installed copy, **When** compared, **Then** their filter guidance is identical.

---

### Edge Cases

- **Both shapes on one request**: the canonical top-level shape is applied, the deprecated nested shape is ignored for that request, and a warning is recorded. Behavior is deterministic, not order-dependent.
- **Empty filter arrays vs. absent filters**: an explicitly empty filter array imposes no constraint for that field (same as omitting it); the run is not silently emptied. (Assumption — see Assumptions.)
- **Legacy nested shape alone**: continues to filter correctly for at least one release; it is marked deprecated but honored.
- **Unknown property that is *not* a known filter confusion**: still rejected (strict), but with a generic "not a valid property; check the tool schema" message rather than a specific suggestion.
- **A genuinely filter-less run** (no filters of any shape): proceeds exactly as today — the whole suite/selection runs, which is the correct behavior when no filter was requested.
- **Case / naming variations** of filter values (e.g., priority casing): unchanged by this spec — value-matching semantics are out of scope; only field binding changes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The run-start operation MUST accept the same top-level plural filter fields (`priorities`, `tags`, `components`) that the test-discovery operation accepts, and MUST apply them when starting a suite-mode run.
- **FR-002**: Filter semantics MUST match the discovery operation exactly: values within a single filter field combine with OR; different filter fields combine with AND. Users learn one rule that holds across both operations.
- **FR-003**: The legacy nested filter shape (a `filters` object with singular `priority`/`component` plus `tags`) MUST continue to produce correct filtering for at least one release. It MUST be marked deprecated both in code and in the operation's published schema.
- **FR-004**: When a single request supplies both the canonical top-level filters and the legacy nested filters, the system MUST apply the top-level shape, ignore the nested shape, and record a warning. The outcome MUST be deterministic regardless of field order.
- **FR-005**: The request boundary MUST reject unmapped (unknown, misplaced, or misspelled) properties on every tool rather than silently discarding them.
- **FR-006**: A rejected unmapped-property request MUST produce a structured invalid-parameters error that names the offending property.
- **FR-007**: For the known filter-field confusions (top-level singular `priority`/`component`/`tag`; nested object containing plural fields; discovery request wrapping filters in a nested object), the error MUST additionally state the correct field to use.
- **FR-008**: A rejected request MUST NOT start a run or mutate any state; the error is returned in place of execution.
- **FR-009**: The execution agent guidance MUST present exactly one filter shape (top-level plural), demonstrated identically for both the discovery and run-start operations, and MUST NOT contain any nested-filter example. It MUST include an explicit statement that the same filter shape works for both operations and that no nested filter object is used.
- **FR-010**: The bundled/installed copy of the execution agent guidance MUST match the source copy with respect to filter guidance.
- **FR-011**: Filter *application* logic (how a resolved filter set selects tests, and how "has filters" is determined) MUST NOT change. The fix is confined to request binding and validation at the boundary.
- **FR-012**: The three concrete misshaped invocations previously documented as causing silent whole-suite enqueue MUST now either filter correctly or fail with an actionable error; none may silently enqueue the unfiltered whole suite.

### Key Entities

- **Run filter set**: the resolved, internal representation of which priorities, tags, and components constrain a run. Produced by normalizing whichever request shape arrived (canonical top-level, or legacy nested) into one form. Carries OR-within-field, AND-between-field semantics.
- **Run-start request**: the inbound request to begin executing a suite, selection, or explicit test list. Gains top-level plural filter fields as the documented shape; retains the nested filter object as a deprecated alternative.
- **Discovery request**: the inbound request to preview/list matching tests. Already exposes top-level plural filters; its filter shape is the reference the run-start request is aligned to.
- **Unmapped-property error**: a structured invalid-parameters response naming an offending property and, when the property is a known filter confusion, suggesting the correct field.
- **Execution agent guidance**: the prompt/instructions that steer the agent in translating natural-language run requests into tool calls. Exists as a source copy and a bundled/installed copy that must agree.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of suite-mode runs requested with top-level plural filters enqueue exactly the matched set; the reported test count equals the matched count, never the full-suite count, whenever a filter is present.
- **SC-002**: Filter results for identical filter inputs are identical between the run-start and discovery operations in 100% of compared cases (same OR/AND outcome).
- **SC-003**: 100% of requests containing an unmapped property are rejected with a structured error that names the property; none start a run.
- **SC-004**: For every known filter-field confusion, the error message contains the correct field name to use (verifiable by checking that the suggested field appears in the message).
- **SC-005**: The legacy nested filter shape continues to filter correctly in 100% of back-compat test cases for this release.
- **SC-006**: An audit of the execution agent guidance finds exactly one filter shape and zero nested-filter examples; the source and bundled copies agree.
- **SC-007**: The three previously-silent misshaped invocations are reproduced and none enqueues the unfiltered whole suite — each either filters correctly or returns an actionable error.
- **SC-008**: No change is detectable in filter application logic — the existing "given a resolved filter set, select tests" behavior is byte-for-byte unchanged, confirmed by the pre-existing filter-application tests continuing to pass without modification.

## Assumptions

- **Empty filter array semantics**: an explicitly empty array for a filter field is treated as "no constraint for this field," identical to omitting it. The system does not interpret an empty array as "match nothing." This mirrors the discovery operation's existing behavior.
- **Value-matching unchanged**: how filter *values* are compared to test metadata (casing, exact vs. partial, enum normalization) is unchanged. This spec only changes which request fields bind to the filter set and how invalid fields are handled.
- **One release of deprecation**: "deprecated but honored" means at least the current release continues to accept the nested shape. Actual removal is a separate, later decision gated on usage telemetry.
- **Strict rejection applies uniformly**: enabling strict unmapped-member handling affects all tools at the shared request boundary, not only the two filter-bearing tools. Other tools' existing valid requests are unaffected because they already supply only mapped fields.
- **Suggestion coverage is intentionally narrow**: only the enumerated filter-field confusions carry a specific suggestion. All other unmapped properties get a generic "check the schema" message. A general-purpose fuzzy field suggester is out of scope.
- **The filter logic is already correct**: prior investigation verified the defect is at request binding, not in filter application. This spec does not re-verify or alter the application path.

## Decisions

Recorded so they are not re-litigated during planning or review:

- **Top-level plural is canonical; nested is deprecated, not removed.** Removing the nested shape now would break any existing caller; keeping it deprecated-but-honored preserves back-compat while steering all new usage to one shape. Removal waits for telemetry confirming zero callers.
- **Top-level wins when both shapes are present.** A deterministic precedence rule (plus a warning) is clearer than erroring on the ambiguity; the canonical shape is the obvious winner.
- **Strict deserialization is enabled globally at the boundary, not per-tool.** A single boundary policy closes the whole class of bug; per-tool opt-in would leave gaps and re-invite drift.
- **Suggestions are a small hand-curated map, not a general fuzzy matcher.** The known confusions are few and high-value; a general matcher adds surface and unpredictability for marginal gain. Out of scope by decision.
- **Filter application semantics are untouched.** AND-between-fields, OR-within-field already behave correctly; changing them is not part of this fix and would risk regressions.

## Out of Scope

- A general-purpose fuzzy/Levenshtein field-suggestion system across all request types. Only the enumerated filter-field confusions get specific suggestions.
- Removing or sunsetting the legacy nested filter shape. It stays deprecated-but-honored this release.
- Any change to filter application logic — how a resolved filter set selects tests, or how "has filters" is determined.
- Any change to the test-discovery tool's filter shape (it is already canonical and is the reference, not the target).
- Changes to execution engine, test queue mechanics, run lifecycle, or reporting beyond the count reflecting the filtered set.
- Value-level matching semantics (casing, partial match, enum normalization of filter values).

## Dependencies & Sequencing

- Independent of Spec 047 (merged), Spec 048 (Coverage Guards), Spec 049 (From-Description Write & Index Parity), and Spec 050 (From-Description Criteria Injection, merged). May land in parallel with any of them.
- **Complementary with Spec 049**: 049 fixes the *missing-from-index* side of filter symptoms (from-description tests never reach the index, so filtering them returns zero); this spec fixes the opposite side (a well-indexed suite where filtering returns everything because the request never bound). Together they complete the end-to-end "from-description high-priority tests run via filter" scenario. Neither subsumes the other.

## Documentation Update Checklist

Documentation updates that land with this fix:

- Generic MCP usage doc — document the canonical filter shape; show one example used identically across both tools; note the legacy nested shape is deprecated.
- Usage troubleshooting — add a "filter ignored / whole suite ran" entry explaining that such requests now surface as actionable errors, and point to the canonical shape.
- `PROJECT-KNOWLEDGE.md` — add a Spec 051 row; record that this closes the binding-side silent-failure class for MCP requests.
- Execution-related SKILLs that invoke the run-start operation — audit filter examples and ensure the top-level shape.
