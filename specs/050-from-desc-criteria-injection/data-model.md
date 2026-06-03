# Phase 1 Data Model: From-Description Criteria Injection

**Branch**: `050-from-desc-criteria-injection`
**Date**: 2026-06-02

## Status

**No new entities, no schema changes, no migration.** This fix is purely behavioral — it ensures an existing field (`criteria:` on `TestCase`) is populated more reliably and an existing field (`grounding.verdict`) keeps its existing semantics. Nothing in the persisted data model, JSON shapes, YAML frontmatter schema, or enum definitions changes.

This document exists to record that fact and to inventory the existing entities the fix relies on, so reviewers can confirm at a glance that no data-model work is hidden in the implementation.

## Existing entities (unchanged; for reference)

### `TestCase` (in `Spectra.Core/Models/`)

The persisted shape of a generated test. Relevant fields for this spec:

| Field | Type | Behavior post-fix |
|-------|------|-------------------|
| `Id` | string (`TC-XXX`) | Unchanged — allocated by `PersistentTestIdAllocator`. |
| `Criteria` | list of strings (criterion IDs) | **More reliably populated** for from-description tests. The model returns the IDs it mapped; the writer serializes them as today. No schema change. |
| `Grounding.Verdict` | `VerificationVerdict` enum | **Stays `Manual`** for from-description tests (deliberate; see Decision D3). |
| `Grounding.Generator` | string | Unchanged — provider model name. |
| `Grounding.Critic` | string | Unchanged — `"user-described"` sentinel for this flow. |
| All other fields (Title, Priority, Steps, ExpectedResult, Tags, Component, SourceRefs, etc.) | various | Unchanged. |

### `VerificationVerdict` enum

```text
Manual | Grounded | Partial | Ungrounded
```

**Not modified.** Spec § Decisions explicitly rejects adding a new value (e.g., `criteria-mapped`). The enum's domain is verification verdicts, not population indicators.

### `GenerationResult` (returned from `IAgentRuntime.GenerateTestsAsync`)

Existing record carrying `Tests`, `SkippedDuplicates`, `Errors`, `TokenUsage`, `CoverageGapsRemaining`. **Not modified.** Each `TestCase` in `Tests` now arrives with `Criteria` populated when the model maps any (a consequence of the prompt change, not a shape change).

### `_index.json` (per-suite test index)

**Not modified.** Spec 049 already ensures from-description tests are routed through `TestPersistenceService` so they land in the suite's `_index.json`. This spec adds nothing to that path.

### `docs/criteria/_criteria_index.yaml` and per-doc `.criteria.yaml`

**Not modified.** This fix consumes the existing criteria index via the existing `LoadCriteriaContextAsync` helper in `GenerateHandler`; it does not write to the criteria index.

## State transitions

**No state machine touched.** The MCP execution engine is not in scope. The CLI generation pipeline is a straight-through flow: load criteria → build prompt → call agent → write test. No state diagram or transition table applies.

## Validation rules

No new validation rules. Existing rules continue to apply:

- A `TestCase`'s `criteria` field, when present, MUST be a list of strings.
- A test whose YAML frontmatter `criteria:` lists a criterion ID that does not exist in the criteria index is flagged by existing `spectra validate` heuristics; this fix does not change those heuristics or thresholds.
