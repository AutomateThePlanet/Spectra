# CLI Contract: `spectra ai compile-prompt`

Deterministic, model-free prompt compiler. Emits a grounded generation prompt to stdout and writes nothing to disk.

## Synopsis

```
spectra ai compile-prompt [suite] [--suite|-s <name>] [--count|-n <int>] [--focus|-f <text>]
                          [--doc-suite <id>] [--output-format json|human]
```

## Inputs

| Input | Source | Required | Notes |
|-------|--------|----------|-------|
| suite | positional or `--suite` | yes | Target suite; drives criteria/profile resolution. |
| count | `--count` | no (default 5) | MUST resolve to > 0. |
| focus | `--focus` | no | Behaviors/focus text → prompt `behaviors`. |
| criteria context | resolved internally (`LoadCriteriaContextAsync`) | yes | Refuse-to-emit if unresolved/empty. |

## Behavior

1. Resolve criteria context, profile format, and (optional) Testimize dataset exactly as the generate handler does today.
2. Call `PromptCompiler.Compile(...)`.
3. **Success** ⇒ print the compiled prompt to **stdout**; exit `0`. Writes nothing to disk (no test files, no index, no temp files).
4. **Refuse-to-emit** (missing required input) ⇒ print nothing to stdout; emit the missing-input name + message to **stderr** (and `missing_input` JSON field under `--output-format json`); exit non-zero.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Prompt compiled and emitted. |
| 4 | Refuse-to-emit: a required input was missing (name reported). |
| 1 | Unexpected error. |

## Guarantees (testable)

- **Deterministic**: identical inputs ⇒ byte-identical stdout across runs.
- **Token-free**: no model/network call.
- **No I/O beyond declared reads**: filesystem under `test-cases/` and indexes are unchanged after the command.
