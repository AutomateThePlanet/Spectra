# Quickstart: Smart Test Count Recommendation

**Feature**: 019-smart-test-count

## What Changed

When you run `spectra ai generate` without `--count`, the system now analyzes your documentation first and recommends how many tests to generate based on identified testable behaviors.

## Quick Examples

### Auto-recommend (non-interactive)

```bash
spectra ai generate --suite checkout --no-interaction
```

The system analyzes docs, identifies behaviors, deducts existing coverage, and generates all remaining tests automatically.

### Interactive selection

```bash
spectra ai generate
```

After selecting a suite, you'll see a breakdown of testable behaviors and choose how many to generate.

### Explicit count (unchanged)

```bash
spectra ai generate --suite checkout --count 15
```

Skips analysis entirely. Generates exactly 15 tests, same as before.

### With focus

```bash
spectra ai generate --suite checkout --focus "negative scenarios"
```

Runs analysis but scopes generation to negative/error behaviors only.

## Key Files to Modify

| File | Change |
|------|--------|
| `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs` | Add analysis step when count is null |
| `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs` | **NEW** — AI behavior analysis service |
| `src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs` | **NEW** — Analysis result model |
| `src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs` | **NEW** — Behavior model |
| `src/Spectra.Core/Models/BehaviorCategory.cs` | **NEW** — Category enum |
| `src/Spectra.CLI/Interactive/CountSelector.cs` | **NEW** — Interactive menu |
| `src/Spectra.CLI/Output/AnalysisPresenter.cs` | **NEW** — Breakdown display |

## Build & Test

```bash
dotnet build
dotnet test
```
