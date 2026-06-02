# Contract: `CriteriaExtractor` public surface

**Project**: `Spectra.CLI` (`src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs`)
**Audience**: `AnalyzeHandler` (in-process caller). No external/network contract.

## Before (current `main`)

```csharp
public sealed class CriteriaExtractor
{
    public Task<IReadOnlyList<AcceptanceCriterion>> ExtractFromDocumentAsync(
        string documentPath,
        string documentContent,
        string? component,
        CancellationToken ct = default);

    // unchanged:
    public Task<IReadOnlyList<AcceptanceCriterion>> SplitAndNormalizeAsync(...);
}
```

Returns empty list for five indistinguishable reasons (input empty / AI empty / no delimiters / null deserialize / parse exception). The caller can't tell "real empty" from "parse failure."

## After

```csharp
public sealed class CriteriaExtractor
{
    public Task<CriteriaExtractionResult> ExtractFromDocumentAsync(
        string documentPath,
        string documentContent,
        string? component,
        CancellationToken ct = default);

    // SplitAndNormalizeAsync unchanged in return type — different call site, no caching gate.
}

public enum ExtractionOutcome { Extracted, EmptyResponse, ParseFailure }

public sealed record CriteriaExtractionResult(
    ExtractionOutcome Outcome,
    IReadOnlyList<AcceptanceCriterion> Criteria)
{
    public bool IsCacheable => Outcome == ExtractionOutcome.Extracted;
}
```

## Return-site mapping (must be exhaustive)

| Condition in `ExtractFromDocumentAsync` / `ParseResponse` | Outcome | Criteria |
|---|---|---|
| `documentContent` is null/whitespace (`:58`) | `Extracted` | `[]` |
| AI response text is null/whitespace (`:92`) | `EmptyResponse` | `[]` |
| `responseText` has no `[` or `]` (`:217`) | `ParseFailure` | `[]` |
| `JsonSerializer.Deserialize<...>(...)` returns `null` (`:227`) | `ParseFailure` | `[]` |
| Any exception thrown inside `ParseResponse` (`:244-247`) | `ParseFailure` (and log via `ErrorLogger`) | `[]` |
| `items` deserialised, 0 entries after `.Where(non-empty Text)` | `Extracted` | `[]` |
| `items` deserialised, N entries after filter | `Extracted` | N entries |

## Logging requirement (FR-004)

The catch-all path MUST call:

```csharp
Spectra.CLI.Infrastructure.ErrorLogger.Write(
    "criteria",
    $"doc={documentPath}",
    ex);
```

before returning `ParseFailure`. The existing `RecordAndLog` success log is unchanged.

## Behavioural invariants the caller relies on

1. **Never throws on parse/transport problems.** Parse exceptions are caught and surfaced as `ParseFailure`. Exceptions from the SDK (network, cancellation, etc.) still propagate; the caller has its own `try/catch`.
2. **`Criteria` is empty whenever `Outcome != Extracted`.** Construction sites guarantee this.
3. **`IsCacheable` is the only signal the caller needs.** The caller will not branch on `Outcome` directly except for the diagnostic message.

## What is *not* in this contract

- `SplitAndNormalizeAsync` continues to return `IReadOnlyList<AcceptanceCriterion>`. (See research.md D7.)
- No new prompt change, no new SDK call, no provider-config change.
- No persisted file format change.
