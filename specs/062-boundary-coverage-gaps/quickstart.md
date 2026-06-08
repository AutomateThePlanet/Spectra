# Quickstart: Boundary-coverage gap detection

## What it does

When you run the **analyze** step of generation, Spectra now surfaces **boundary-coverage gaps** — boundary conditions implied by your docs/criteria (min/max, off-by-one, empty/null, overflow, timeout) that aren't covered by existing or planned tests — so you can choose to generate those edge cases. The signal is **advisory**: it never mutates tests or blocks generation, and the grounding critic is unchanged.

## Try it

1. Compile the analysis prompt for a suite whose docs imply a boundary (e.g., a field with a length limit):

   ```bash
   spectra ai compile-analysis-prompt --suite signup --doc-suite signup-docs --output-format json
   ```

2. Run the analysis in-session (the agent reads the docs and emits JSON). The output now includes a top-level `boundary_gaps` array alongside `behaviors`:

   ```json
   {
     "behaviors": [ ... ],
     "boundary_gaps": [
       { "field": "username", "kind": "max-length", "description": "21-char input (max 20) untested", "source": "docs/signup.md" }
     ]
   }
   ```

3. Ingest it:

   ```bash
   spectra ai ingest-analysis --suite signup --from .spectra/analysis.json --output-format json
   ```

   The recommendation carries `boundary_gaps` alongside `technique_breakdown`.

## Verify the behavior

| Scenario | Expected |
|----------|----------|
| Docs imply an uncovered boundary | `boundary_gaps` lists it (field, kind, description, source) |
| Docs imply no boundary | `boundary_gaps: []` — no spurious entries |
| Boundary already covered by a test | not listed |
| `boundary_gaps` omitted entirely (legacy) | ingest succeeds, `boundary_gaps: []` |
| `boundary_gaps` malformed (not array / missing required field) | ingest **fails** exit 6 with a specific `boundary_gaps...` error |
| Gaps present | `recommended` / `breakdown` / `technique_breakdown` unchanged; generation not blocked |

## What did NOT change

- The grounding critic (`spectra-critic`), its prompt builder, and `VerdictIngestor` — same verdict vocabulary (`grounded`/`partial`/`hallucinated`/`manual`).
- `Spectra.Core` — no changes.
- Exit codes (0 success, 5 empty, 6 parse/malformed) and all existing flags.
