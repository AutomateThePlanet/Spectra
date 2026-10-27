# Contracts: Plan Only Uncovered Behaviours

## `spectra ai ingest-analysis --suite S --from FILE [--focus F]` (changed)

- `already_covered` is the number of analysed behaviours that existing tests in suite S cover. A behaviour is covered
  when a test's title is ≥ `generation.duplicate_threshold` similar (default 0.6). Before 095 this was the suite's test
  count whenever the suite had any test or criterion.
- `recommended` = uncovered behaviours.
- New: `covered: [{id, title, covered_by}]`.
- The plan batches only uncovered behaviours. All covered → `batches: []`, `planned_total: 0`.
- Human summary (stderr in JSON mode) adds `covered: <title> (<covered_by>)` lines.
- `unresolved` lists only uncovered behaviours whose source didn't resolve; a covered behaviour appears only under
  `covered`, whatever its source.
- Batch counts: each batch's `count` equals its number of uncovered behaviours (one per behaviour); Σ = `recommended`.
- `generation.duplicate_threshold` outside (0, 1] → a stderr warning, "generation.duplicate_threshold = <v> is outside (0, 1]; every behaviour (≤ 0) or none (> 1) is treated as covered." Exit code unchanged.
- Exit codes and every other field are unchanged.

## `spectra ai compile-prompt --suite S --batch ID` (changed)

- A batch never contains a covered behaviour.
- A 094 plan (no `duplicate_threshold`) compiles as before, with the warning "the generation plan predates
  per-behaviour coverage; re-run the analysis for corrected counts" in `warnings`.

## Config

`generation.duplicate_threshold` (existing, default 0.6) is now also the coverage threshold of the analysis. It stays
optional and is not range-checked.

## spectra-analyst

Adds one `covered: <title> (<covered_by>)` line per covered behaviour to its final message, verbatim from the CLI.

## spectra-generate

- Step 4 shows "Already covered — not generated:" followed by the `covered:` lines. If `recommended` is 0, everything
  is covered (as today).
- Step 9 lists the covered behaviours.
- The generator's warning "the generation plan predates per-behaviour coverage…" is reported once, with an offer to
  re-run the analysis.
