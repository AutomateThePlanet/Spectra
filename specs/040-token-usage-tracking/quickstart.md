# Quickstart — Spec 040: Token Usage Tracking

## Verify the feature end-to-end (manual)

### 1. Default off — no debug log written

```bash
# Fresh repo with default config (no `debug` section)
spectra ai generate checkout --count 4
ls .spectra-debug.log   # should NOT exist
```

### 2. Enable via config

```jsonc
// spectra.config.json
{
  "debug": {
    "enabled": true,
    "log_file": ".spectra-debug.log"
  }
}
```

```bash
spectra ai generate checkout --count 4
grep "BATCH OK" .spectra-debug.log
# Each line should end: model=<name> provider=<name> tokens_in=<n> tokens_out=<n>
```

### 3. One-shot diagnostic override

```bash
# debug.enabled left at false
spectra ai generate checkout --count 4 --verbosity diagnostic
ls .spectra-debug.log   # exists for this run
```

### 4. Run Summary panel in terminal

```bash
spectra ai generate checkout --count 8
# After run completes, the terminal shows:
#
# Run Summary
# ===========
#   Documents processed   ...
#   Behaviors identified  ...
#   Tests generated       8 (X grounded, Y partial, Z rejected)
#   Batch size            8 (1 batch)
#   Duration              X:XX
#
#   Token Usage
#   Phase       Model        Calls  Tokens In  Tokens Out  Total   Time
#   Analysis    ...              1        ...        ...    ...    ...
#   Generation  ...              1        ...        ...    ...    ...
#   Critic      ...              8        ...        ...    ...    ...
#   TOTAL                       10        ...        ...    ...    ...
#
#   Estimated cost: ...
```

### 5. Quiet mode suppresses panel

```bash
spectra ai generate checkout --count 4 --verbosity quiet
# No Run Summary panel; tokens still tracked internally and written to .spectra-result.json
```

### 6. JSON output includes token_usage

```bash
spectra ai generate checkout --count 4 --output-format json --verbosity quiet > result.json
jq '.run_summary, .token_usage' result.json
# Should print run_summary object and token_usage object with phases[], total, estimated_cost_usd
```

### 7. .spectra-result.json mirror

```bash
jq '.token_usage.total' .spectra-result.json
```

### 8. github-models cost message

```bash
# With provider=github-models in config
spectra ai generate checkout --count 4
# Final line of summary should read:
#   Estimated cost: included in Copilot plan (rate limits apply)
```

### 9. azure-openai cost estimate

```bash
# With provider=azure-openai and a known model (e.g. gpt-4.1-mini)
spectra ai generate checkout --count 4
# Final line should read:
#   Estimated cost: $X.XX (azure-openai rates)
```

### 10. Update command Run Summary

```bash
spectra ai update checkout
# After run, terminal shows the Update variant with: Tests scanned/updated/unchanged + Update phase row
```

## Automated test verification

```bash
dotnet test --filter "FullyQualifiedName~TokenUsageTrackerTests"
dotnet test --filter "FullyQualifiedName~DebugConfigTests"
dotnet test --filter "FullyQualifiedName~DebugLoggerTests"
dotnet test --filter "FullyQualifiedName~RunSummaryPresenterTests"
dotnet test --filter "FullyQualifiedName~CostEstimatorTests"
dotnet test --filter "FullyQualifiedName~GenerateResultTokenUsageTests"
```

All should pass. Existing generate/update/critic test suites should also still pass with no modifications.
