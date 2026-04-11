# Phase 1 Data Model: Progress Bars

## ProgressSnapshot (record)

Lives in `src/Spectra.CLI/Progress/ProgressSnapshot.cs` (NEW). Attached to `GenerateResult` and `UpdateResult` via an optional `Progress` field. Serialized as `progress` in `.spectra-result.json`.

### Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `Phase` | enum `ProgressPhase` | yes | `Generating` \| `Verifying` \| `Updating` |
| `TestsTarget` | int | yes | Total tests this run intends to produce/process |
| `TestsGenerated` | int | yes | Cumulative count after each completed batch (0 in `Updating` phase) |
| `TestsVerified` | int | yes | Cumulative count after each completed critic call (0 outside `Verifying` phase) |
| `CurrentBatch` | int | yes | 1-based index of the in-progress (or just-completed) unit. For `Generating`: batch index. For `Verifying`: stays at the final batch index. For `Updating`: proposal index. |
| `TotalBatches` | int | yes | Total units. For `Generating`: `ceil(TestsTarget / GenerationBatchSize)`. For `Updating`: total proposal count. |
| `LastTestId` | string? | no | Most recent test handled (test ID, e.g. `TC-118`) |
| `LastVerdict` | string? | no | Critic verdict for `LastTestId`: `grounded` \| `partial` \| `hallucinated`. Null outside `Verifying`. |

### Lifecycle / State Transitions

```
null
  │
  │ handler creates snapshot at start of generation
  ▼
Phase=Generating, TestsGenerated=0, TestsVerified=0, CurrentBatch=1, TotalBatches=N
  │
  │ after each batch completes: TestsGenerated += batch.RequestedCount, CurrentBatch++
  ▼
Phase=Generating, TestsGenerated=TestsTarget, CurrentBatch=N (terminal generation state)
  │
  │ critic phase begins (only if not --skip-critic)
  ▼
Phase=Verifying, TestsVerified=0
  │
  │ after each critic call: TestsVerified++, LastTestId, LastVerdict updated
  ▼
Phase=Verifying, TestsVerified=TestsTarget
  │
  │ ProgressManager.Complete() or .Fail()
  ▼
null  (cleared from final result file)
```

For `UpdateHandler`: snapshot starts as `Phase=Updating`, advances per proposal apply, cleared on completion.

### Validation Rules

- `TestsGenerated <= TestsTarget`
- `TestsVerified <= TestsGenerated` (you cannot verify what wasn't generated)
- `CurrentBatch <= TotalBatches`
- `LastVerdict ∈ {grounded, partial, hallucinated, null}`
- `Phase` ↔ `LastVerdict` consistency: `LastVerdict` MUST be null when `Phase != Verifying`

These are invariants of the producing handler — not validated at deserialization (the consumer is the progress page, which is tolerant of missing fields).

## Glossary Note

**"Chunks" vs "Proposals"**: The original spec text referenced update "chunks". The actual `UpdateHandler` has no chunk concept — it does one classification batch and then a per-proposal apply loop. The Update progress bar tracks proposals; `CurrentBatch` / `TotalBatches` carry proposal counts in `Phase=Updating`. FR-005 is satisfied by per-proposal advancement.

## Result File Shape (full example)

In-flight (mid-generation):

```json
{
  "status": "generating",
  "command": "generate",
  "suite": "checkout",
  "progress": {
    "phase": "generating",
    "testsTarget": 40,
    "testsGenerated": 24,
    "testsVerified": 0,
    "currentBatch": 3,
    "totalBatches": 5,
    "lastTestId": "TC-124",
    "lastVerdict": null
  }
}
```

In-flight (mid-verification):

```json
{
  "status": "generating",
  "command": "generate",
  "suite": "checkout",
  "progress": {
    "phase": "verifying",
    "testsTarget": 40,
    "testsGenerated": 40,
    "testsVerified": 18,
    "currentBatch": 5,
    "totalBatches": 5,
    "lastTestId": "TC-118",
    "lastVerdict": "grounded"
  }
}
```

Final (after `Complete()`): `progress` field is omitted (`JsonIgnoreCondition.WhenWritingNull`); `runSummary` is present instead.
