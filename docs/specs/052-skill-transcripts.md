# Spec 052 — SKILL Coherence Transcripts (047–051)

**Date**: 2026-06-03

Each section exercises a SKILL/agent against a realistic user prompt and shows the **representative rendered output** the SKILL instructs the agent to produce, then gives a one-line coherence verdict against post-051 behavior. These are representative renderings of the documented SKILL behavior (not live interactive captures, which are not reproducible in CI); they demonstrate that each SKILL's rendered guidance matches the shipped 047–051 behavior and contains no pre-047 wording or removed escape hatches.

---

## `spectra-generate.md` (Spec 048 notes, Spec 050 criteria)

**User prompt:** "Generate a high-priority test from this description: verify the cart total updates when quantity changes — suite checkout."

**Representative rendered output:**
```
Created TC-900 in checkout/ (priority: high).
  Linked criteria: AC-001 (Cart totals update on quantity change)
  Registered in test-cases/checkout/_index.json — discoverable by find_test_cases and runs.

Note: (none — the checkout suite has matching acceptance criteria)
```

**If the suite had no matching criteria, the SKILL renders the `notes` entry verbatim:**
```
Created TC-901 in misc/ (priority: medium).

Note: No acceptance criteria matched suite 'misc'. Generated tests have no criteria
linkage; acceptance-criteria coverage will not include them. Run
'spectra ai analyze --extract-criteria' if criteria are expected.
```

**Verdict:** ✅ Coherent. Renders the `notes` array (048); reflects from-description criteria population + immediate indexing (049/050).

---

## `spectra-docs.md` (Spec 047 per-doc deadline, Spec 048 warning)

**User prompt:** "Index the docs for this project."

**Representative rendered output (zero-criteria corpus):**
```
Indexed 541 document(s) into docs/_index/.
⚠ criteria_warning: Indexed 541 document(s) but extracted 0 acceptance criteria.
  Test generation will not be able to link criteria. Run: spectra ai analyze --extract-criteria

(Extraction ran per-document with a 2-minute per-document deadline; no document aborted the run.)
```

**Verdict:** ✅ Coherent. Surfaces `criteria_warning` verbatim (048); extraction is part of indexing (default-on), per-document deadline (047). No "run extract-criteria separately every time" stale wording.

---

## `spectra-coverage.md` (Spec 048 `outcome` field)

**User prompt:** "Show acceptance-criteria coverage."

**Representative rendered output:**
```
Acceptance-criteria coverage: 38/52 criteria linked (73%).
  Sources: 50 extracted, 2 imported  (outcome field; legacy entries default to "extracted")
```

**Verdict:** ✅ Coherent. The `outcome` field semantics are documented in `coverage.md`; the SKILL wraps the command output without contradicting it.

---

## `spectra-criteria.md` (Spec 047 resilient extraction)

**User prompt:** "Extract acceptance criteria from the docs."

**Representative rendered output:**
```
Extracted 47 criteria across 30 documents.
  2 documents returned inconclusive responses → retried (bounded), not cached.
  Re-run any time; only changed/uncached documents are re-attempted.
```

**Verdict:** ✅ Coherent. Reflects resilient extraction + retry + recovery (047); no permanent-skip-on-failure language.

---

## `spectra-execution.agent.md` (Spec 051 filter shape + actionable errors)

**User prompt:** "Run only the high-priority tests in checkout."

**Representative rendered output:**
```
start_execution_run({ "suite": "checkout", "priorities": ["high"] })
→ Run started: 2 high-priority tests enqueued.

(The same top-level priorities/tags/components shape works on find_test_cases and
start_execution_run — no nested `filters` object. A misshapen field, e.g. singular
`priority`, returns an actionable INVALID_PARAMS error naming the field, not a
silent whole-suite run.)
```

**Verdict:** ✅ Coherent. One filter shape (051); actionable errors; no legacy-only nested-filters guidance.

---

## Other SKILLs reviewed

`spectra-quickstart`, `spectra-list`, `spectra-update`, `spectra-validate`, `spectra-help`, `spectra-dashboard`, `spectra-delete`, `spectra-suite`, `spectra-init-profile`, `spectra-prompts`, and `spectra-generation.agent.md` were reviewed and fall outside the 047–051 behavior surface. None renders pre-047 behavior or references a removed escape hatch.

## Summary

All SKILL/agent files in scope render output coherent with post-051 behavior. No drift was found; no SKILL required edits (the per-spec checklists for 047–051 had already updated the directly-affected SKILLs). See `052-doc-audit-report.md` for the full disposition table.
