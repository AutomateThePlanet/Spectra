# Contract: Execution Report Schema (additive)

The execution report is the contract this feature touches. It is emitted in three formats by
`ReportWriter`. This document specifies the **additions** only; all existing fields are unchanged.

## JSON contract (authoritative; consumed by the dashboard)

Serialized snake-case via `JsonNamingPolicy.SnakeCaseLower`. New keys are omitted when their value is
null/empty.

### Per-test entry (`results[]`) — added keys

```jsonc
{
  "test_id": "TC-001",
  "title": "Verify successful login",
  "status": "Passed",
  "attempt": 1,
  "duration_ms": 5120,
  // ── added (each omitted when absent/empty) ──
  "priority": "High",                       // enum name string; omitted if test case unavailable
  "tags": ["smoke", "auth"],                // omitted when no tags
  "component": "Authentication",            // omitted when null/empty
  "criteria": ["AC-12", "AC-13"],           // acceptance-criteria IDs; omitted when none
  "source_refs": ["docs/auth/login.md"]     // source-doc refs; omitted when none
}
```

### Run-level (top object) — added key

```jsonc
{
  "run_id": "…",
  "suite": "checkout",
  "summary": { /* … */ },
  "results": [ /* … */ ],
  // ── added (omitted when no result has a duration) ──
  "timing": {
    "total_test_duration_ms": 15360,
    "average_test_duration_ms": 5120
  }
}
```

### Backward-compatibility guarantees

- **Purely additive**: no existing key renamed or removed.
- **Optional**: every new key is omitted when its source value is null/empty (`JsonIgnore(WhenWritingNull)`;
  empty collections normalized to null before serialization). An older report (or a report built
  without test-case data) is a valid instance of the new schema — it simply lacks the new keys.
- **Consumer tolerance**: the dashboard and any JSON reader that ignores unknown keys continue to work
  unchanged.

## Markdown contract (additions)

- **All Results** table gains a `Priority` column:
  `| Test ID | Title | Status | Priority | Attempt | Duration |` (value `-` when absent).
- **Failed Tests** per-test detail blocks gain bullet lines for **Component**, **Tags**, **Criteria**,
  **Source Docs** — each line emitted only when the field is present.
- **Header** gains **Total Test Time** / **Avg per Test** lines when `timing` is present.

## HTML contract (additions)

- `RenderTestContent` gains rendering for **Priority**, **Component**, **Tags**, **Criteria**,
  **Source Docs** (shown in failed/skipped/blocked cards and non-passing expandable rows), each
  omitted when absent.
- **All Results** table gains a `Priority` column (so passing rows surface priority too).
- Header `meta-info` gains a timing item when `timing` is present.

## Non-goals (unchanged behavior)

- No change to MCP tool inputs/outputs, the execution engine, or the state machine.
- No change to report file naming, output directory, or the snake-case serialization convention.
- No model/AI involvement — rendering remains deterministic.
