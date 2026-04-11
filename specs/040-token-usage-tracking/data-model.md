# Phase 1 — Data Model

## DebugConfig (Spectra.Core.Models.Config)

```csharp
public sealed class DebugConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("log_file")]
    public string LogFile { get; init; } = ".spectra-debug.log";
}
```

- Added to `SpectraConfig` as `[JsonPropertyName("debug")] public DebugConfig Debug { get; init; } = new();`
- Default: disabled, path `.spectra-debug.log` relative to repo root.
- Validation: `LogFile` is treated as a relative path; absolute paths are accepted as-is.

---

## TokenUsage (Spectra.Core.Models)

```csharp
public sealed record TokenUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}
```

- Immutable value record.
- Replaces the old `Spectra.CLI.Agent.TokenUsage` (which had `InputTokens`/`OutputTokens` and was unused).

---

## PhaseUsage (Spectra.CLI.Services)

```csharp
public sealed record PhaseUsage(
    string Phase,        // "analysis" | "generation" | "critic" | "update" | "criteria"
    string Model,
    string Provider,
    int Calls,
    int TokensIn,
    int TokensOut,
    TimeSpan Elapsed)
{
    public int TotalTokens => TokensIn + TokensOut;
}
```

- Aggregate row produced by `TokenUsageTracker.GetSummary()`.
- One row per `(Phase, Model, Provider)` tuple.

---

## TokenUsageTracker (Spectra.CLI.Services)

State:
- `List<PhaseUsageEntry>` of raw recorded calls (one per AI response).
- `object _lock` for thread safety.

API:
- `void Record(string phase, string model, string provider, int? tokensIn, int? tokensOut, TimeSpan elapsed)`
  - `tokensIn`/`tokensOut` may be null when the API didn't return usage; tracker stores them, but they don't contribute to numeric totals (counted as `0`).
  - `Calls` always increments.
- `IReadOnlyList<PhaseUsage> GetSummary()` — aggregates by `(phase, model, provider)`, sums `Calls`/`TokensIn`/`TokensOut`/`Elapsed`.
- `PhaseUsage GetTotal()` — grand total across all phases (Phase = "TOTAL", Model = "", Provider = "").
- `decimal? EstimateCostUsd()` — delegates to `CostEstimator`.

Lifecycle:
- One instance per command run, created by `GenerateHandler`/`UpdateHandler` at start, attached to the result at end.
- Not registered in DI as a singleton because state must reset per run; instead injected via constructor or passed explicitly to agents.

---

## CostEstimator (Spectra.CLI.Services)

```csharp
public static class CostEstimator
{
    public static (decimal? UsdAmount, string DisplayMessage) Estimate(
        IReadOnlyList<PhaseUsage> phases);
}
```

Rules (FR-026 to FR-029):
- If any phase's provider is `github-models`, return `(null, "Included in Copilot plan (rate limits apply)")`.
- If any phase's model is missing from the rate table, return `(null, $"Cost estimate unavailable for {model}")`.
- Otherwise sum across phases: `(tokensIn / 1_000_000m * inputRate) + (tokensOut / 1_000_000m * outputRate)`, return `(amount, $"${amount:F2} ({primaryProvider} rates)")`.

Initial rate table (per FR-026):

| Model | Input/1M | Output/1M |
|---|---|---|
| gpt-4o | 2.50 | 10.00 |
| gpt-4o-mini | 0.15 | 0.60 |
| gpt-4.1 | 2.00 | 8.00 |
| gpt-4.1-mini | 0.40 | 1.60 |
| gpt-4.1-nano | 0.10 | 0.40 |
| claude-sonnet-4-20250514 | 3.00 | 15.00 |
| claude-3-5-haiku-latest | 0.80 | 4.00 |
| deepseek-v3.2 | 0.30 | 0.88 |

Lookup is case-insensitive.

---

## RunSummary (Spectra.CLI.Results)

```csharp
public sealed class RunSummary
{
    // Generate-specific (nullable for update)
    public int? DocumentsProcessed { get; init; }
    public int? BehaviorsIdentified { get; init; }
    public int? TestsGenerated { get; init; }
    public VerdictBreakdown? Verdicts { get; init; }
    public int? BatchSize { get; init; }
    public int? Batches { get; init; }

    // Update-specific (nullable for generate)
    public int? TestsScanned { get; init; }
    public int? TestsUpdated { get; init; }
    public int? TestsUnchanged { get; init; }
    public ClassificationBreakdown? Classifications { get; init; }
    public int? Chunks { get; init; }

    // Both
    public double DurationSeconds { get; init; }
}
```

JSON property names use `snake_case` via `JsonPropertyName`.

---

## TokenUsageReport (Spectra.CLI.Results)

```csharp
public sealed class TokenUsageReport
{
    public IReadOnlyList<PhaseUsageDto> Phases { get; init; } = [];
    public PhaseUsageDto Total { get; init; } = new();
    public decimal? EstimatedCostUsd { get; init; }
    public string CostDisplay { get; init; } = "";
}

public sealed class PhaseUsageDto
{
    public string Phase { get; init; } = "";
    public string Model { get; init; } = "";
    public string Provider { get; init; } = "";
    public int Calls { get; init; }
    public int TokensIn { get; init; }
    public int TokensOut { get; init; }
    public int TotalTokens { get; init; }
    public double ElapsedSeconds { get; init; }
}
```

Built once at end of run by `TokenUsageReportBuilder.Build(tracker)`.

---

## GenerateResult / UpdateResult additions

```csharp
public sealed class GenerateResult : CommandResult
{
    // ... existing fields ...
    public RunSummary? RunSummary { get; init; }
    public TokenUsageReport? TokenUsage { get; init; }
}
```

Same shape on `UpdateResult`.

---

## State transitions

None — all entities are immutable value records or DTOs. `TokenUsageTracker` is the only mutable component and its only state transition is "append one entry under lock".
