# Quickstart: Criteria Extraction Re-homing + Extractor Unification

## What changed (user-visible)

Two new model-free CLI commands let an interactive agent perform the extractive turn while the CLI
stays deterministic — mirroring the 053 generation surface. The existing
`ai analyze --extract-criteria` and `docs index` commands keep working exactly as before.

## The agent-driven extraction flow

```bash
# 1) CLI compiles the extraction prompt for one document — deterministic, no model call.
spectra ai compile-extraction-prompt --doc docs/payment.md > /tmp/prompt.txt

# 2) The interactive Claude Code agent runs that prompt in its own context (your subscription),
#    and writes its JSON response to a file (or pipes it).

# 3) CLI ingests the agent's response — classifies + persists only genuine extractions.
spectra ai ingest-criteria --doc docs/payment.md --from /tmp/agent-response.json
#   exit 0 → criteria persisted to docs/criteria/payment.criteria.yaml + index updated
#   exit 5 → EmptyResponse  (agent returned nothing usable) — re-prompt
#   exit 6 → ParseFailure   (unparseable)                   — re-prompt with the error
```

Empty-source short-circuit (no model turn):
```bash
spectra ai compile-extraction-prompt --doc docs/placeholder.md
#   → notice: empty source, outcome Extracted [] — no prompt emitted, exit 0
```

## Determinism check (FR-002)
```bash
spectra ai compile-extraction-prompt --doc docs/payment.md > a.txt
spectra ai compile-extraction-prompt --doc docs/payment.md > b.txt
diff a.txt b.txt   # → identical (byte-for-byte)
```

## `docs index` is on the unified contract (FR-004)
No command change. Internally, a single empty / malformed / slow document no longer throws or aborts
the corpus — it is reported as a failed/inconclusive document and the run continues:
```bash
spectra docs index
#   good docs → criteria cached; empty/slow docs → counted in failed_documents; command exits cleanly
```

## Verification checklist
- [ ] `compile-extraction-prompt` emits a prompt and writes nothing to disk; identical input ⇒ identical output.
- [ ] `compile-extraction-prompt` on a missing `--doc` exits `4` naming `document_path`.
- [ ] `ingest-criteria` persists on `Extracted`; exits `5`/`6` and persists nothing on Empty/Parse.
- [ ] `ingest-criteria` never writes a non-`Extracted` outcome to the index (no cache poisoning).
- [ ] `docs index` over a mixed corpus completes without throwing; failed docs surface in the count.
- [ ] `RequirementsExtractor` returns a typed outcome (no throw) on empty input and on timeout.
- [ ] Protected net green: `Spectra.Core` parsing/requirements + `CriteriaExtractionResult`/`ClassifyResponse` tests unchanged.

## Run the tests
```bash
dotnet test tests/Spectra.CLI.Tests
dotnet test tests/Spectra.Core.Tests   # must stay green & unmodified
```
