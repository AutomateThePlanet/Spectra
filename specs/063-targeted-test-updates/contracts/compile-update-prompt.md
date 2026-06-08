# CLI Contract: `spectra ai compile-update-prompt`

Deterministic, model-free. Emits an update prompt for **one** OUTDATED test to stdout; writes nothing to disk. Mirrors `compile-prompt` (`CompilePromptCommand`).

## Synopsis

```
spectra ai compile-update-prompt --suite <suite> --test-id <id> [--output-format json|human]
```

## Arguments & options

| Name | Required | Description |
|------|----------|-------------|
| `--suite`, `-s` | yes | Suite containing the test to update. |
| `--test-id` | yes | Id of the OUTDATED test to compile an update prompt for (one per invocation; the skill loops). |
| `--output-format` | no | `human` (default) or `json` (machine-readable refusal payloads). |

> The OUTDATED set is determined by the caller (the skill) via the reused `TestClassifier` (FR-005). This command does not classify; it assembles the prompt for the id it is given.

## Behavior

1. Validate `--suite` and `--test-id` present → else **refuse** (exit 4).
2. Load `spectra.config.json` → else error (exit 1).
3. Load the original test (`test-cases/<suite>/<id>.md`); if absent → refuse (exit 4).
4. Resolve the changed source/criteria context for the suite (same `CriteriaContextLoader` / source-doc load the update flow uses); if no changed source/criteria context can be resolved → refuse (exit 4).
5. Load profile format + the `test-update` template.
6. Assemble prompt = original test (serialized) + changed source/criteria + explicit "edit, don't regenerate; preserve id/structure/manual fields" directives.
7. Print the prompt to stdout (no disk write).

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Prompt emitted to stdout. |
| `4` | Refused — a required input is missing (suite, test-id, original test not found, or no changed source/criteria). |
| `1` | Error — config missing/unparseable or I/O failure. |

## Refusal payload (`--output-format json`)

```json
{ "refused": true, "missing_input": "test-id", "message": "A target test id is required." }
```

## Guarantees

- **Deterministic**: same inputs → byte-identical prompt. No model call.
- **No side effects**: writes nothing to disk; the prompt IS the artifact.
- **Edit framing**: the emitted prompt instructs the model to edit, not regenerate, and to preserve id, structure, and manual fields.
