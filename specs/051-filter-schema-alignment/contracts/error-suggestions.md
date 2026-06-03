# Contract: unmapped-member error & filter-field suggestions

**Branch**: `051-filter-schema-alignment`
**Date**: 2026-06-03

## Strict deserialization

Tool parameters are deserialized with `UnmappedMemberHandling.Disallow` (a dedicated options instance; the JSON-RPC envelope parser stays lenient — research.md D2). Any property on a tool's params object that does not map to a known field throws, rather than being silently ignored.

## Error extraction

System.Text.Json raises a `JsonException` whose message follows:

```
The JSON property '{property}' could not be mapped to any .NET member contained in type '{declaringType}'.
```

`McpProtocol` extracts `{property}` and the short `{declaringType}` with a regex:

```
The JSON property '([^']+)' could not be mapped to any \.NET member contained in type '([^']+)'
```

If extraction fails (message format differs), the boundary still returns a structured `INVALID_PARAMS` error with the raw message — never a silent drop and never an unhandled 500/internal error.

## Suggestion function

`Suggest(toolName, property, declaringType)` → string? (closed set, research.md D4):

| toolName | property | declaringType | Suggestion |
|----------|----------|---------------|------------|
| `start_execution_run` | `priority` | request type | `Use 'priorities' (array) at the top level.` |
| `start_execution_run` | `component` | request type | `Use 'components' (array) at the top level.` |
| `start_execution_run` | `tag` | request type | `Use 'tags' (array) at the top level.` |
| `start_execution_run` | `priorities` / `components` / `tag` | *nested filters* type | `Use top-level '{plural}', not nested under 'filters'.` |
| `find_test_cases` | `filters` | request type | `find_test_cases uses top-level 'priorities'/'tags'/'components', not a nested 'filters' object.` |
| any | any other | any | *(none — generic message)* |

## Thrown exception

`McpInvalidParamsException` (new, `Spectra.MCP/Server`) carries the final message:

```
Property '{property}' is not valid on '{toolName}'. {suggestion ?? "Check the tool schema."}
```

## Rendered MCP error

`ToolRegistry.InvokeAsync` catches `McpInvalidParamsException` and returns a structured tool error with code `INVALID_PARAMS` and the exception message. Guarantees:

- **No run is created** and **no state mutates** when a request is rejected (FR-008).
- The error is returned in place of execution; the generic `InternalError` catch is *not* used for this case.
- The behavior is uniform across **all** tools (FR-005) — every tool deserializes via the strict path; only the suggestion text is tool-specific.

## Examples

| Input to `start_execution_run` | Outcome |
|--------------------------------|---------|
| `{ "suite": "X", "priorities": ["high"] }` | OK — filters to high-priority tests. |
| `{ "suite": "X", "priority": "high" }` | `INVALID_PARAMS`: "Property 'priority' is not valid on 'start_execution_run'. Use 'priorities' (array) at the top level." |
| `{ "suite": "X", "filters": { "priorities": ["high"] } }` | `INVALID_PARAMS`: "...Use top-level 'priorities', not nested under 'filters'." |
| `{ "suite": "X", "filters": { "priority": "high" } }` | OK — legacy nested shape, honored (deprecated). |

| Input to `find_test_cases` | Outcome |
|----------------------------|---------|
| `{ "suites": ["X"], "filters": { "priority": "high" } }` | `INVALID_PARAMS`: "...find_test_cases uses top-level 'priorities'/'tags'/'components', not a nested 'filters' object." |
| `{ "suites": ["X"], "priorities": ["high"] }` | OK. |
