# Data Model: Execution Report Enrichment

All additions are optional/nullable and omitted when empty. No existing field is renamed or removed.

## Entity: `TestResultEntry` (EDIT — `src/Spectra.Core/Models/Execution/TestResultEntry.cs`)

Per-test report record. Existing fields unchanged: `test_id`, `title`, `status`, `attempt`,
`duration_ms`, `notes`, `blocked_by`, `preconditions`, `steps`, `expected_result`, `test_data`,
`screenshot_paths`.

### New fields (all sourced from `TestCase`)

| Property (C#) | JSON name | Type | Source (`TestCase`) | Empty/absent handling |
|---------------|-----------|------|---------------------|-----------------------|
| `Priority` | `priority` | `Priority?` | `tc?.Priority` | omitted when test case absent (null) |
| `Tags` | `tags` | `IReadOnlyList<string>?` | `tc?.Tags` | `null` when empty → omitted |
| `Component` | `component` | `string?` | `tc?.Component` | omitted when null/empty |
| `Criteria` | `criteria` | `IReadOnlyList<string>?` | `tc?.Criteria` | `null` when empty → omitted |
| `SourceRefs` | `source_refs` | `IReadOnlyList<string>?` | `tc?.SourceRefs` | `null` when empty → omitted |

**Serialization attributes**: each property carries
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. `Priority` additionally carries
`[JsonConverter(typeof(JsonStringEnumConverter))]` (serialized as enum name string, consistent with
`status`).

**Validation rules**: none new. Values are copied verbatim from the parsed `TestCase`; the report does
not re-validate them. Collections are normalized to `null` when count is 0 (so empty arrays never
serialize), mirroring the existing `Steps = tc?.Steps is { Count: > 0 } ? tc.Steps : null` pattern
(`ReportGenerator.cs:49`).

## Entity: `ReportTiming` (NEW — `src/Spectra.Core/Models/Execution/ReportTiming.cs`)

Minimal run-level timing breakdown derived from the per-test results already in the report.

| Property (C#) | JSON name | Type | Meaning |
|---------------|-----------|------|---------|
| `TotalTestDurationMs` | `total_test_duration_ms` | `long` | Sum of `DurationMs` across results that have a duration |
| `AverageTestDurationMs` | `average_test_duration_ms` | `long` | `TotalTestDurationMs` / (count of results with a duration), rounded |

`sealed record`. Both fields required within the record. The record itself is attached to
`ExecutionReport` only when at least one result carries a duration; otherwise the whole `timing` object
is `null` and omitted.

## Entity: `ExecutionReport` (EDIT — `src/Spectra.Core/Models/Execution/ExecutionReport.cs`)

Existing fields unchanged: `run_id`, `suite`, `environment`, `started_at`, `completed_at`,
`duration_minutes`, `executed_by`, `status`, `summary`, `results`, `filters`.

### New field

| Property (C#) | JSON name | Type | Empty/absent handling |
|---------------|-----------|------|-----------------------|
| `Timing` | `timing` | `ReportTiming?` | `null` when no result has a duration → omitted via `[JsonIgnore(WhenWritingNull)]` |

## Population (EDIT — `src/Spectra.MCP/Reports/ReportGenerator.cs`)

In the per-result projection (`ReportGenerator.cs:36-53`), add the five fields from
`tc = testCases?.GetValueOrDefault(r.TestId)`:

```text
Priority   = tc?.Priority,
Tags       = tc?.Tags       is { Count: > 0 } ? tc.Tags       : null,
Component  = tc?.Component,
Criteria   = tc?.Criteria   is { Count: > 0 } ? tc.Criteria   : null,
SourceRefs = tc?.SourceRefs is { Count: > 0 } ? tc.SourceRefs : null,
```

After building `entries`, compute `Timing` from entries with a non-null `DurationMs`:
- if none → `Timing = null`
- else → `new ReportTiming { TotalTestDurationMs = sum, AverageTestDurationMs = sum / count }`

Attach `Timing` to the returned `ExecutionReport`.

## Relationships

```
ExecutionReport 1 ──── * TestResultEntry      (existing; entries gain 5 optional fields)
ExecutionReport 1 ──── 0..1 ReportTiming      (new; derived from entries' durations)
TestResultEntry  *.... 1 TestCase             (source of the 5 new fields; not modified)
```

No state transitions. No persistence-schema change (reports are generated artifacts).
