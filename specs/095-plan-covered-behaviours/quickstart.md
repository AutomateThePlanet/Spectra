# Quickstart: Plan Only Uncovered Behaviours

## CLI checks (built CLI)

1. **Real init, a suite with 50 tests:**
   - Set up the workspace (e.g. `../094-generation-batch-prompts/acceptance/setup-workspace.sh`, then 50 tests in
     `test-cases/storage/` via `ingest-tests`).
   - Ingest an analysis of 15 behaviours whose titles match 3 tests: `already_covered` 3, `recommended` 12.
   - `covered` lists the 3 with their `covered_by`.
   - The batches hold the 12, with counts adding up to 12.
2. **With and without criteria:** delete `docs/criteria/` and ingest again; the counts are unchanged.
3. **Threshold:** `generation.duplicate_threshold: 0.9` → fewer covered; no key → 0.6, no warning.
4. **Prompts:** compile every batch; no covered title appears in any of them.
5. **All covered:** an analysis whose behaviours all match tests → `recommended` 0, `batches: []`.
6. **Pre-095 workspace:** put a 094 plan (without `duplicate_threshold` / `covered_by`) in
   `.spectra/generation-plan/storage.json`. `compile-prompt --batch 1` exits 0, with the "predates per-behaviour
   coverage" warning.
7. **Threshold warning:** `generation.duplicate_threshold: 0` → exit 0 and the stderr warning "generation.duplicate_threshold = 0 is outside (0, 1]; …".
8. **Covered + unresolvable source:** a behaviour matching a test title with source `docs/nope.md` → only in `covered` (not `unresolved`, no batch), one summary line.

## Manual (optional, the user)

"generate tests for the storage area" in a suite with existing tests: Step 4 lists "Already covered — not generated",
and no created test duplicates an existing title.
