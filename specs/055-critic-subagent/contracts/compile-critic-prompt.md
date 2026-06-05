# Contract: `spectra ai compile-critic-prompt`

Deterministic, **model-free** compilation of the critic verification prompt. Mirrors
`compile-prompt` (053) and `compile-extraction-prompt` (054). No model/provider session is opened.

## Synopsis

```
spectra ai compile-critic-prompt --test <file> [--docs <file|dir>] [--output-format human|json]
```

## Inputs

| Option | Required | Description |
|--------|----------|-------------|
| `--test` | yes | Path to the candidate test artifact (the test to verify), as JSON or a test markdown file. |
| `--docs` | no | Source document(s) to ground against (file or directory). Empty/absent → the prompt is emitted with "*No relevant documentation provided.*" (NOT a refusal). |
| `--output-format` | no | `human` (default) emits the prompt to stdout; `json` wraps it as `{ "prompt": "..." }`. |

## Behavior

1. Resolve the test artifact. If absent, or it has no id/title → **refuse to emit** (FR-002).
2. Select ≤5 relevant source documents (by the reused `CriticPromptBuilder` selection rules) and
   truncate each to 8000 chars.
3. Assemble `{system}\n\n---\n\n{user}` via the reused-verbatim `CriticPromptBuilder`.
4. Emit the prompt. Identical `(test, docs)` input → **byte-identical** output (FR-002).

No model is called; the subagent skill (or any agent) performs the model turn over the emitted
prompt.

## Exit codes

| Code | Condition |
|------|-----------|
| 0 | Prompt compiled and emitted. |
| 4 | Refused to emit — required input missing (no test artifact / no id+title). Names the missing input. |
| 1 | Environment error (e.g. unreadable `--test` file). |

## Examples

```bash
# Compile the critic prompt for one generated test
spectra ai compile-critic-prompt --test ./tc-900.json --docs ./docs/checkout > critic.prompt

# Missing artifact → refuse, exit 4
spectra ai compile-critic-prompt
echo $?   # 4
```

## Guarantees

- **Deterministic**: no timestamps, GUIDs, or unordered enumeration in the output.
- **Model-free**: returns synchronously with no provider configured.
- **Isolation-preserving**: the emitted prompt contains only the artifact + selected source docs —
  never generator prompt/reasoning/tool-calls/tokens (FR-002).
