# Phase 1 Data Model: Filter Schema Alignment & Strict Deserialization

**Branch**: `051-filter-schema-alignment`
**Date**: 2026-06-03

## Overview

No persisted data model changes — no index, frontmatter, or on-disk format is touched. The entities below are in-memory request/transfer shapes at the MCP boundary plus the shared `RunFilters` model. The only behavioral semantic change is tag matching (AND → OR) per research.md D1.

## Entities

### `RunFilters` (Spectra.Core/Models/Execution/RunFilters.cs) — MODIFIED

The resolved, internal filter set applied when building a run queue.

| Field | Type | Status | Semantics |
|-------|------|--------|-----------|
| `Priority` | `Priority?` (enum) | KEPT (legacy) | Single-value match. Used by legacy nested-shape lift and existing unit tests. |
| `Priorities` | `IReadOnlyList<string>?` | **NEW** | OR-within-array, case-insensitive string match against entry priority (mirrors `find_test_cases`). |
| `Tags` | `IReadOnlyList<string>?` | KEPT, **semantics changed** | Was AND; now **OR-within-array** (test must have *any* listed tag). |
| `Component` | `string?` | KEPT (legacy) | Single-value match. |
| `Components` | `IReadOnlyList<string>?` | **NEW** | OR-within-array, case-insensitive. |
| `TestIds` | `IReadOnlyList<string>?` | UNCHANGED | Include listed tests + their dependencies (recursive). |

**`HasFilters`** (extended): true when any of `Priority`, `Priorities`, `Tags`, `Component`, `Components`, `TestIds` is present/non-empty. Its *meaning* ("are there any constraints?") is unchanged — only the set of fields it inspects grows.

**Factory**: `static RunFilters From(IReadOnlyList<string>? priorities, IReadOnlyList<string>? tags, IReadOnlyList<string>? components)` — builds a filter set from the canonical top-level arrays. Stores raw strings (D6); no enum parse.

**Application precedence within `ApplyFilters`** (per field): if the plural field is non-empty, apply OR-within-array; else if the singular legacy field is present, apply single match. Tags and TestIds have one form each. AND-between-fields is preserved (each present field further narrows the set).

**Validation rules**: empty array == no constraint for that field (Assumption in spec; mirrors `find_test_cases`). No value is enum-validated for the plural path — unknown values simply match nothing.

### `StartExecutionRunRequest` (Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs) — MODIFIED

Inbound DTO for `start_execution_run`.

| Field (JSON) | Type | Status | Notes |
|--------------|------|--------|-------|
| `suite` | string? | unchanged | Suite mode. |
| `test_ids` | string[]? | unchanged | Explicit-IDs mode. |
| `selection` | string? | unchanged | Saved-selection mode. |
| `name` | string? | unchanged | Required for test_ids/selection modes. |
| `environment` | string? | unchanged | |
| `priorities` | string[]? | **NEW** | Canonical top-level filter (matches `find_test_cases`). |
| `tags` | string[]? | **NEW** | Canonical top-level filter. |
| `components` | string[]? | **NEW** | Canonical top-level filter. |
| `filters` | `StartExecutionRunFilters?` | **`[Obsolete]`** | Legacy nested shape; still honored this release; `deprecated: true` in schema. |

With `UnmappedMemberHandling.Disallow`, any property not in this list (e.g. top-level singular `priority`) is rejected with an actionable error.

### `StartExecutionRunFilters` (legacy nested) — UNCHANGED shape, deprecated

`priority` (string), `tags` (string[]), `component` (string), `test_ids` (string[]). Lifted to `RunFilters` via `NormalizeFilters` (singular → plural).

### `NormalizeFilters(StartExecutionRunRequest)` → `RunFilters?` — NEW (in StartExecutionRunTool)

Resolution order (research.md D7):
1. If any of `priorities`/`tags`/`components` present → `RunFilters.From(...)`. If the legacy `filters` is *also* present, record a warning ("both shapes provided; using top-level").
2. Else if legacy `filters` present → lift singular→plural into `RunFilters` (`priority` → `Priorities:[priority]`, `component` → `Components:[component]`, `tags`/`test_ids` carried over).
3. Else → no filters (null / `HasFilters == false`).

### `FindTestCasesRequest` — UNCHANGED

Already exposes canonical top-level `priorities`/`tags`/`components`. It is the reference shape; only its `DeserializeParams` call gains a `toolName` argument.

### `McpInvalidParamsException` (Spectra.MCP/Server) — NEW

Thrown by `DeserializeParams<T>` on an unmapped member. Carries the structured, actionable message (offending property + suggestion). Caught by `ToolRegistry.InvokeAsync` and rendered as an `INVALID_PARAMS` tool error. Not persisted; transport-only.

## State transitions

None. No state machine, run lifecycle, or queue-state semantics change. A rejected request produces an error in place of execution and creates **no** run (FR-008).

## Relationships

```text
start_execution_run request (canonical OR legacy shape)
      │  DeserializeParams<StartExecutionRunRequest>(args, "start_execution_run")   [strict]
      ▼
StartExecutionRunRequest ──NormalizeFilters──▶ RunFilters (unified)
      │                                              │
      │                                       TestQueue.ApplyFilters (OR-within-array, AND-between-fields)
      ▼                                              ▼
ExecutionEngine.StartRunAsync ───────────────▶ filtered queue (test_count == matched)

unmapped member ──▶ McpInvalidParamsException ──(ToolRegistry.InvokeAsync)──▶ INVALID_PARAMS error (no run created)
```
