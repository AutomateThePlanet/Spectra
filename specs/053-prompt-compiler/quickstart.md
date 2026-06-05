# Quickstart — Inverted generation handoff (Spec 053)

The new model-free seam. A skill (Spec 055) drives this; here is the manual walkthrough.

## 1. Compile a grounded prompt (no model, no tokens)

```bash
spectra ai compile-prompt reporting --count 5 --focus "export to PDF"
```

- Prints the fully-grounded prompt (doc + criteria + profile + Testimize) to stdout.
- Writes nothing to disk.
- If the criteria context can't be resolved, it refuses and tells you which input was missing (exit 4) — fix grounding before generating.

## 2. Generate (the interactive Claude Code agent's turn)

The agent reads the compiled prompt in its own context and produces a JSON array of test objects. This step costs the user's subscription tokens, not a CLI model call.

## 3. Ingest at the fail-loud boundary

```bash
# from a file the agent wrote:
spectra ai ingest-tests reporting --from ./generated.json
# or pipe it:
cat ./generated.json | spectra ai ingest-tests reporting
```

- **Valid** ⇒ persists every test (`.md` + regenerated `_index.json`) via the unchanged `TestPersistenceService`; exit 0.
- **Invalid** ⇒ persists nothing, prints a specific `error_code` (`MALFORMED_JSON`, `TRUNCATED`, `SCHEMA_INVALID`, …) and messages; non-zero exit.

## 4. Retry (skill choreography — not the CLI)

On a non-zero ingest, the skill feeds the `error_code` + messages back to the agent ("regenerate; test TC-012 failed INVALID_ID") and re-runs step 2→3, up to a configured max attempts, then stops and reports failure. The CLI's only job is to return an error specific enough to act on.

## Verifying the guarantees

```bash
# determinism: same inputs, identical output
spectra ai compile-prompt reporting --count 5 > a.txt
spectra ai compile-prompt reporting --count 5 > b.txt
diff a.txt b.txt        # expect: no differences

# zero persistence on bad input
echo 'not json' | spectra ai ingest-tests reporting   # non-zero; git status shows no changes
```

## Tests

```bash
dotnet test tests/Spectra.CLI.Tests   # includes Generation/ token-free tests
dotnet test tests/Spectra.Core.Tests  # regression net — must stay green & unchanged
```
