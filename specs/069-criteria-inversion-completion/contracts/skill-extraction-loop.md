# Contract: `spectra-criteria` skill extraction loop (FR-003/FR-004)

Mirrors the generation seam (Specs 053/059): deterministic CLI compile → in-session model turn →
fail-loud CLI ingest, looped per changed document by the **skill** (not the CLI).

## Choreography (extraction recipe)

1. **Find work** — `spectra docs changed --output-format json`. Read the `changed[]` list. If empty,
   report "criteria up to date" and stop (no model turn).
2. **Per doc** (`new|changed` only):
   a. `spectra ai compile-extraction-prompt --doc <path> [--component <c>] --output-format json`
      → prompt on stdout (or an empty-source short-circuit: `{ short_circuit: true, outcome: "Extracted",
      criteria: [] }` → skip the model turn, go to ingest-with-empty or just skip).
   b. **In-session model turn**: read `<path>` with file tools, produce the JSON array of criteria per the
      compiled prompt. Main session — **no subagent** (FR-004).
   c. Write the JSON to a temp file and `spectra ai ingest-criteria --doc <path> [--component <c>]
      --from <file> --output-format json`.
      - exit `0` → persisted (report `persisted` count + `ids`).
      - exit `5` (empty) / `6` (parse) → **fail loud**; retry the turn up to a bounded number of times
        (e.g. 2), then surface the failure for that doc and continue with the rest.
3. **Summarize** — docs processed, criteria persisted (new/updated), docs skipped (unchanged), any
   per-doc failures.

## Invariants preserved by the CLI (not the model)

- **Incremental skip** — step 1 ensures unchanged docs never reach a model turn (FR-005).
- **Outcome gate / anti-cache-poisoning** — `ingest-criteria` persists only `Extracted`; empty/parse
  write nothing (FR-012).
- **ID scheme + serialization** — owned by `CriteriaIngestor`/`CriteriaFileWriter` (FR-013/014); byte-
  compatible with the old path.

## Import recipe (FR-008)

`spectra ai analyze --import-criteria <file> [--replace]` — deterministic pass-through (no model call,
no splitting). The skill no longer mentions `--skip-splitting` (removed/no-op).

## Out of the skill

The skill never writes criteria files directly — all persistence goes through `ingest-criteria`
(Principle IV: AI writes nothing directly).
