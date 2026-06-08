# Contract: `spectra ai compile-prompt --from-description` (extension)

Extends the existing Spec 053 `compile-prompt` command (`Commands/Generate/CompilePromptCommand.cs`) with a single-test from-description mode. Deterministic, model-free, writes nothing to disk.

## Synopsis

```
spectra ai compile-prompt --suite <suite> --from-description "<text>" [--context "<text>"] [--output-format json]
```

## New options

| Option | Type | Default | Notes |
|--------|------|---------|-------|
| `--from-description` | string? | null | When set, switches to single-test mode. |
| `--context` | string? | null | Optional extra context (page/module/flow) folded into the user prompt. |

Existing `--suite/-s`, positional `suite`, `--count/-n`, `--focus/-f`, `--output-format` are unchanged.

## Behavior

1. When `--from-description` is present:
   - `requestedCount` is forced to **1** (any `--count` is ignored in this mode).
   - The user-prompt is shaped from the description (+ context) using the same logic as today's `UserDescribedGenerator.BuildPrompt` (relocated/shared).
   - Criteria are resolved via `GenerateHandler.LoadCriteriaContextAsync` and injected by `PromptCompiler` exactly as in bulk mode — the Spec 050 mandatory-mapping block appears when matching criteria exist.
2. When `--from-description` is absent: behavior is **byte-identical to today** (bulk compile).
3. Output: compiled prompt to **stdout**; nothing written to disk.

## Exit codes (unchanged from compile-prompt)

| Code | Meaning |
|------|---------|
| 0 | Prompt compiled and emitted to stdout. |
| 4 | Refuse-to-emit — a required input is missing (`--from-description` mode still requires `--suite`; a missing required compiler input is named in `missing_input`). |
| 1 | File/config read or parse error. |

## Determinism

Identical `(suite, from-description, context, criteria, profile)` inputs MUST produce byte-identical prompt output. No timestamps/random ordering. No model call, no token spend.

## Persistence

None by this command. The agent generates in-session; persistence is performed by the **unchanged** `ingest-tests` command (which already accepts a 1-element test array and runs full fail-loud validation + `TestPersistenceService` write+index).

## Acceptance mapping

- FR-001 (from-description on seam), FR-003 (seam extended additively), US2 AS1/AS2 (routes through seam; criteria injection preserved).
