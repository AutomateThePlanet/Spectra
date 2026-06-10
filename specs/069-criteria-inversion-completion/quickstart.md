# Quickstart / Verification: Criteria-extraction inversion + Copilot SDK removal

End-to-end recipe proving the feature (maps to spec Success Criteria). Run from a workspace with docs
and a `spectra.config.json`.

## A. Pre-inversion baseline (capture once, before implementing)

```bash
# Snapshot the current criteria artifacts for the byte-compat diff (SC-006)
spectra ai analyze --extract-criteria --no-interaction --output-format json --verbosity quiet
cp -r docs/criteria /tmp/criteria-baseline
```

## B. Inverted extraction works without the SDK (SC-005, US1)

```bash
# 1. changed-docs (model-free) lists the work
spectra docs changed --output-format json          # → new|changed docs only

# 2. skill loop, per changed doc (mirrors generation seam):
spectra ai compile-extraction-prompt --doc docs/checkout.md --component checkout   # prompt → stdout
#    …in-session model turn produces JSON array → /tmp/out.json…
spectra ai ingest-criteria --doc docs/checkout.md --component checkout --from /tmp/out.json --output-format json
```

- Expect: each changed doc compiled → extracted in-session → ingested; **unchanged docs are not listed
  by step 1** and never get a model turn.

## C. Byte-compat (SC-006)

```bash
diff -r /tmp/criteria-baseline docs/criteria      # MUST be clean (identical .criteria.yaml + _criteria_index.yaml)
```

## D. docs index is index-only (SC-004, US3)

```bash
spectra docs index --force --output-format json
```

- Expect: builds the doc index, makes **no** model call, result has no `criteria_extracted`/`criteria_file`,
  and `docs/requirements/_requirements.yaml` is **not** created.

## E. SDK provably gone (SC-001/002, US2)

```bash
grep -rn "config.Ai.Providers" src/        # → zero outside removed validation/display
grep -rn "Spectra.CLI.Agent.Copilot" src/  # → zero
ls src/Spectra.CLI/Agent/Copilot 2>&1       # → not found
```

## F. init asks nothing; config has no providers/critic (SC-007, US2)

```bash
( cd "$(mktemp -d)" && spectra init --no-interaction --no-review && cat spectra.config.json )
```

- Expect: no "AI Provider Setup" / model-preset / critic prompt; generated `spectra.config.json` has
  **no** `ai.providers` and **no** `ai.critic` (no dummy block).

## G. Build + tests (SC-003)

```bash
dotnet build -c Release        # green (TreatWarningsAsErrors honored)
dotnet test                    # green
```

- Only the named provider/critic-presence Core tests are edited (FR-019, documented). Any **other** Core
  test failure = real regression → investigate, do not edit. `TestPersistenceService`, `CriteriaMerger`,
  the `*Writer`s (incl. `RequirementsWriterTests`) stay untouched and green.
