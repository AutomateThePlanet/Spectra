# Contract: `start_execution_run` filter schema

**Branch**: `051-filter-schema-alignment`
**Date**: 2026-06-03

## Canonical request shape (documented)

Top-level plural filter arrays, identical to `find_test_cases`:

```json
{
  "suite": "checkout",
  "priorities": ["high"],
  "tags": ["smoke"],
  "components": ["payments"],
  "name": "Checkout high-priority smoke",
  "environment": "staging"
}
```

- `priorities`, `tags`, `components`: arrays of strings. **OR within each array**; **AND between arrays**.
- All filter fields optional. An empty array imposes no constraint for that field.
- Filtering applies in **suite mode** (with `suite`). `test_ids` and `selection` modes are unchanged by this contract.

## Legacy request shape (deprecated, still honored this release)

```json
{
  "suite": "checkout",
  "filters": { "priority": "high", "tags": ["smoke"], "component": "payments" }
}
```

- `filters` is marked `deprecated: true` in the published schema description and `[Obsolete]` in code.
- Singular `priority`/`component` are lifted to the canonical plural set internally.
- Honored for at least this release; removal is out of scope.

## Precedence

When both shapes appear on one request, the **top-level shape wins**, the nested `filters` is ignored, and a warning is recorded. Deterministic regardless of field order.

## Published JSON schema (tool `ParameterSchema`)

```jsonc
{
  "type": "object",
  "properties": {
    "suite":       { "type": "string", "description": "Suite name (mutually exclusive with test_ids and selection)" },
    "test_ids":    { "type": "array", "items": { "type": "string" }, "description": "Run specific tests from any suites" },
    "selection":   { "type": "string", "description": "Run a saved selection by name" },
    "name":        { "type": "string", "description": "Run name (required for test_ids and selection modes)" },
    "environment": { "type": "string", "description": "Target environment" },

    "priorities":  { "type": "array", "items": { "type": "string" }, "description": "Filter by priority (OR within array). Same shape as find_test_cases." },
    "tags":        { "type": "array", "items": { "type": "string" }, "description": "Filter by tags (OR within array). Same shape as find_test_cases." },
    "components":  { "type": "array", "items": { "type": "string" }, "description": "Filter by component (OR within array). Same shape as find_test_cases." },

    "filters": {
      "type": "object",
      "description": "DEPRECATED — use top-level priorities/tags/components instead. Still honored this release.",
      "deprecated": true,
      "properties": {
        "priority":  { "type": "string" },
        "tags":      { "type": "array", "items": { "type": "string" } },
        "component": { "type": "string" },
        "test_ids":  { "type": "array", "items": { "type": "string" } }
      }
    }
  }
}
```

## Strictness

Any property **not** listed above is rejected with a structured `INVALID_PARAMS` error (see `error-suggestions.md`). Notably:
- top-level singular `priority`/`component`/`tag` → rejected, suggests the plural top-level field.
- `filters: { priorities: [...] }` (plural inside the legacy object) → rejected, suggests top-level `priorities`.

## Response (unchanged)

On success: `run_id`, `suite`, `test_count` (== matched count when a filter is present, never the full-suite count), `first_test`, plus run status/progress. Warnings (e.g. both-shapes) are surfaced in the response's warning channel.
