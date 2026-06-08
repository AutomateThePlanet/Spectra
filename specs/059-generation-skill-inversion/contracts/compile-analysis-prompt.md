# Contract: `spectra ai compile-analysis-prompt` (new)

New Spec 059 command mirroring the 053/054/055 compile-* pattern. Deterministic, model-free; emits the behavior-analysis prompt the interactive agent answers in-session. Registered in `AiCommand` next to `compile-prompt`.

## Synopsis

```
spectra ai compile-analysis-prompt --suite <suite> [--doc-suite <id>] [--focus "<text>"] [--include-archived] [--output-format json]
```

## Options

| Option | Type | Default | Notes |
|--------|------|---------|-------|
| `--suite, -s` | string | (required) | Target test suite. |
| `--doc-suite` | string? | null | Doc-suite filter (Spec 040). Subject to the same pre-flight token-budget check as generation. |
| `--focus, -f` | string? | "" | Focus area folded into the analysis prompt. |
| `--include-archived` | bool | false | Include `skip_analysis` suites (Spec 040). |
| `--output-format` | enum | human | `json` wraps the prompt as `{ prompt: "..." }`. |

## Behavior

1. Resolve the documents for `--suite`/`--doc-suite` (reusing the existing document-map + token-budget pre-flight from the analyze path) and the existing tests for the suite.
2. Build the behavior-analysis prompt (relocated from `BehaviorAnalyzer`'s prompt-building half into `AnalysisPromptCompiler.Compile`). The prompt instructs the agent to identify behaviors with category + ISTQB technique tags.
3. Emit the prompt to **stdout** (human) or `{ prompt }` (json). Nothing written to disk.

## Exit codes (mirror compile-prompt)

| Code | Meaning |
|------|---------|
| 0 | Prompt compiled and emitted. |
| 4 | Refuse-to-emit — missing required input (`missing_input` named). Includes the doc-suite token-budget refusal surface (or exit 4 from the existing budget check). |
| 1 | File/config read error. |

## Determinism

No model call; identical inputs → identical prompt. Token spend is zero (the in-session generation happens after, on the subscription).

## Acceptance mapping

- FR-001/FR-003 (analyze-only on the seam), US3 AS1.
