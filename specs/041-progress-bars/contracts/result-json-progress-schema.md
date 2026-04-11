# Contract: `progress` field in `.spectra-result.json`

**Producer**: `Spectra.CLI` `GenerateHandler`, `UpdateHandler` via `ProgressManager.Update(result)`.
**Consumers**: `ProgressPageWriter` (writes `.spectra-progress.html`); SKILL/CI scripts that watch the result file.

## Field

`progress` is OPTIONAL and present only while a run is in flight. It MUST be absent from the final result file (after `Complete()` or `Fail()`).

## Schema

```json
{
  "progress": {
    "phase":         "generating | verifying | updating",  // required
    "testsTarget":   40,           // required, integer ≥ 0
    "testsGenerated": 24,          // required, integer ≥ 0, ≤ testsTarget
    "testsVerified":  0,           // required, integer ≥ 0, ≤ testsGenerated
    "currentBatch":   3,           // required, integer ≥ 1, ≤ totalBatches
    "totalBatches":   5,           // required, integer ≥ 1
    "lastTestId":     "TC-124",    // optional, string or absent
    "lastVerdict":    "grounded"   // optional; one of grounded|partial|hallucinated; absent unless phase = verifying
  }
}
```

## Stability Guarantees

- Field names are camelCase (matches existing `JsonResultWriter` policy).
- Consumers MUST tolerate the entire `progress` object being absent.
- Consumers MUST tolerate `lastTestId` / `lastVerdict` being absent.
- Adding new optional fields to `progress` in future revisions is non-breaking.
- Removing or renaming fields is breaking and requires a spec increment.

## Refresh Cadence

- Generation phase: written after each batch completes (existing per-batch write site, no extra I/O).
- Verification phase: written after each individual critic call completes (per-test write — acceptable since critic calls take 4–6s).
- Update phase: written after each proposal application.
- Final write (with `progress` absent): one write at end of run.

## Reading Safety

`ProgressPageWriter` writes `.spectra-result.json` via temp file + atomic move (existing path in `ProgressManager.FlushWriteFile`). Readers performing whole-file reads will always see a consistent snapshot — either the previous version or the new one, never a half-written file.
