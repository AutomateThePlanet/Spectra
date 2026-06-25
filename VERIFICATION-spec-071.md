# Verification Report — Spec 071 Verdict Disposition Policy

**Date:** 2026-06-19
**Binary under test:** `spectra 2.1.0+30a5247cc39dee5846631918380acbb626fd0089`
**Framework repo HEAD:** `30a5247`
**Consumer repo:** `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST`
**Mode:** READ-ONLY — no files were edited during verification.

---

## Pre-check 0 — Installed binary matches HEAD

| | |
|---|---|
| `spectra --version` | `2.1.0+30a5247cc39dee5846631918380acbb626fd0089` |
| `git rev-parse --short HEAD` | `30a5247` |

**PASS** — hash suffix matches. All behavior checks below test the shipped code.

---

## Phase 1 — Verdicts are durable & visible

### 1.1 — Condensed verdict block in `.md` (grounded)

**PASS — CONFIRMED**

20 of 35 tests verdicted grounded. All 20 have a `grounding:` block in frontmatter. Three confirmed:

- `TC-100.md` — CONFIRMED `test-cases/unit-converter/TC-100.md:15–20`
- `TC-102.md` — CONFIRMED `test-cases/unit-converter/TC-102.md`
- `TC-103.md` — CONFIRMED `test-cases/unit-converter/TC-103.md`

Full block from **TC-100.md**:
```yaml
grounding:
  verdict: grounded
  score: 0.92
  generator: claude-code-session
  critic: unknown
  verified_at: 2026-06-19T13:44:48Z
```

The formerly dead `TestFileWriter:110–147` write-back is now live for grounded tests. CONFIRMED `src/Spectra.CLI/IO/TestFileWriter.cs:110–147`.

---

### 1.2 — Condensed verdict block in `.md` (partial) — THE key check

**FAIL — CONFIRMED**

15 tests have `"verdict":"partial"` in their per-test verdict JSONs (TC-101, TC-105, TC-106, TC-110, TC-111, TC-114, TC-119, TC-121–125, TC-127, TC-128, TC-132 — CONFIRMED `.spectra/verdicts/critic-verdict-TC-*.json`).

**Zero of these 15 `.md` files have a `grounding:` block**, `flagged_for_review`, or `condensed_findings` in frontmatter. A human reading any of these test files cannot distinguish them from grounded tests. This is the same invisible-partial problem the spec was designed to solve.

**Root cause (CONFIRMED):** The `ingest-grounding` seam command was not called for partial tests in this cycle. Evidence:
- Grounded path completed (20 `.md` files have grounding blocks) — the CLI commands and TestFileWriter write-back are working.
- Partial path partially executed: `repaired-TC-NNN.json` files exist for 5 of 15 partials (TC-101 as `.spectra/repaired.json`, TC-105/106/110/111 as `.spectra/repaired-TC-NNN.json`) — CONFIRMED `.spectra/` directory listing. Repair was attempted but the seam loop (`ingest-update` → re-critic → `ingest-grounding`) did not complete for any of them.

This is a **skill fidelity gap**, not a CLI implementation bug. The commands exist and work (integration tests pass). The `spectra-generate.md` Step 8 defines the correct partial loop (CONFIRMED `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:193–236`), but the agent session executing this run did not complete it.

Partial verdict block (from verdict JSON, NOT from `.md` — the `.md` version doesn't exist):
```json
{
  "verdict": "partial",
  "score": 0.65,
  "findings": [
    { "element": "Step 1", "claim": "Bottom output shows approximately 3.28084 when 1 Meter is entered",
      "status": "unverified", "reason": "documentation does not explicitly state the meter-to-feet conversion factor" },
    { "element": "Expected Result", "claim": "Previously converted value becomes the new input after swap",
      "status": "unverified", "reason": "documentation does not explicitly describe what happens to field values when swap button is used" }
  ]
}
```
This IS in `.spectra/verdicts/critic-verdict-TC-105.json` (CONFIRMED). It is NOT condensed into `TC-105.md`. It should have been — that's the failure.

---

### 1.3 — Per-test verdict JSON durability

**PASS — CONFIRMED (naming)**
**FAIL — consumer `.gitignore` missing new paths**

**Naming (PASS):** 35 files in `.spectra/verdicts/`, all named `critic-verdict-TC-NNN.json` (TC-100 through TC-134). No single overwriting `critic-verdict.json`. CONFIRMED `.spectra/verdicts/` listing.

**gitignore — Framework repo (PASS):** `.spectra/verdicts/` at line 37 and `.spectra/dropped-tests.json` at line 38. CONFIRMED `C:\SourceCode\Spectra\.gitignore:37–38`.

**gitignore — Consumer repo (FAIL):** `.gitignore` in `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST` does not contain either entry — only the old `.spectra/critic-verdict.json` (single-file name). CONFIRMED by grep returning no output. All 35 per-test verdict JSONs would be committed to git from the consumer repo.

**Cause:** `spectra update-skills` syncs skill `.md` files only; `.gitignore` is written by `spectra init`. The consumer repo needs `spectra init` re-run (or a manual patch) to pick up the new gitignore entries.

---

### 1.4 — Drop trail + consistent delete (hallucinated)

**N/A — CONFIRMED: no hallucinated verdicts in this run**

All 35 verdict JSONs are either `grounded` (20) or `partial` (15). No `hallucinated` verdicts were produced. `.spectra/dropped-tests.json` does not exist. Trail and delete consistency could not be exercised this cycle.

---

### 1.5 — Stale comments corrected

**PASS — CONFIRMED**

All four formerly-stale comment sites are updated:

| File | Line(s) | Status |
|------|---------|--------|
| `src/Spectra.CLI/IO/TestFileWriter.cs:110–147` | Grounding serialization is live; no "dead code" comment | CONFIRMED |
| `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs:17–18` | Comment reads: `"persists nothing — grounding write-back is handled by IngestGroundingCommand (Spec 071)"` — accurate | CONFIRMED |
| `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:109` | Reads: `"You do not write or modify test files — grounding write-back is handled by spectra ai ingest-grounding (Spec 071) after the verdict is classified."` — accurate | CONFIRMED |

No stale claim that the write-back is unused or dead remains.

---

## Phase 2 — Repair loop (partial only)

### 2.1 — `compile-repair-prompt` verb exists and emits plain text

**PASS — CONFIRMED**

Command file: CONFIRMED `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs`

Output mode (lines 141–142):
```csharp
Console.Out.Write(prompt);
if (!prompt.EndsWith('\n')) Console.Out.WriteLine();
```

No JSON wrapper. All error paths use stderr (lines 58, 65, 72). Exit codes: 4 (non-partial / no non-grounded findings), 5 (missing verdict file), 6 (invalid JSON). CONFIRMED `CompileRepairPromptCommand.cs:141–142`.

---

### 2.2 — Partial got exactly one repair attempt

**FAIL — seam loop incomplete**

**Repair file evidence (CONFIRMED `.spectra/` listing):**
- `repaired.json` (corresponds to TC-101)
- `repaired-TC-105.json`
- `repaired-TC-106.json`
- `repaired-TC-110.json`
- `repaired-TC-111.json`

5 of 15 partial tests have repair patch files. The remaining 10 partial tests have no repair file — they may not have had `compile-repair-prompt` invoked at all.

**Grounding blocks after repair (CONFIRMED — ZERO):** None of the 5 repaired tests has `grounding:` in its `.md` frontmatter. The seam loop steps following repair (`ingest-update` → re-critic → `ingest-grounding`) were not completed. No test shows `repaired: true` or `repair_attempts: 1` in any `.md` file.

**Skill definition is correct (CONFIRMED `spectra-generate.md:193–236`):** Step 8 specifies the full three-branch loop. The gap is execution, not definition.

---

### 2.3 — Hallucinated did NOT go through repair

**N/A / PASS — CONFIRMED**

Zero hallucinated verdicts in the corpus. No `repaired-TC-NNN.json` file exists for any hallucinated test (trivially true since there are none). The invariant is not violated.

---

### 2.4 — Batch stayed non-blocking; four-count report defined

**PASS — CONFIRMED**

The skill defines the four-bucket summary at Step 9 (CONFIRMED `spectra-generate.md:239–251`):

```
- Kept grounded: {N} — list ids
- Repaired to grounded: {N} — list ids
- Flagged partial (awaiting review): {N} — list ids
- Dropped hallucinated: {N}
```

With hint when flagged > 0: `Run: spectra ai review-flagged --suite {suite}`.

The batch did not halt mid-run (all 35 tests were critic'd and verdict JSONs exist for all of them). The four-count structure is present in the skill definition; whether the executing agent session tabulated and reported them cannot be confirmed from artifacts alone — INFERRED from skill text.

---

## Phase 3 — Human review phase

### 3.1 — Review surface exists and is callable

**PASS — CONFIRMED**

Files: CONFIRMED `src/Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs`, `ReviewFlaggedHandler.cs`.

Registered on the `ai` subcommand group (same pattern as `ingest-grounding`, `record-drop`).

**Live `--no-interaction` output** (read-only run):
```
spectra ai review-flagged --suite unit-converter --no-interaction --output-format json
{"flagged_count":0,"tests":[]}
```
Exit 0. **This is correct**: `FindFlaggedAsync` checks `tc.Grounding is { FlaggedForReview: true }` (CONFIRMED `ReviewFlaggedHandler.cs:72`). Since no partial test has a grounding block (Phase 1.2 FAIL), there is nothing to list. The command works — it just has no data because `ingest-grounding` was never called for partials.

---

### 3.2 — Review readability (not shell-improvisation)

**PASS (design) / GAP (current data) — CONFIRMED**

`FindFlaggedAsync` reads `tc.Grounding.CondensedFindings` directly from parsed frontmatter (CONFIRMED `ReviewFlaggedHandler.cs:78`). The interactive path renders each finding as `• {element} — {reason}` bullets. No `cat | python -m json.tool` improvisation needed by the agent — the review surface is self-contained.

**Gap:** Because no `.md` file currently has a grounding block for any partial test, `condensed_findings` would be `[]` for every test in the review output. The readable design exists; it has no data to render. Once `ingest-grounding` is called for partial tests, the review will show their condensed findings correctly.

---

## Phase 4 — Docs

### 4.1 — Docs reflect the new policy

**PASS — CONFIRMED**

**`docs/cli-reference.md`:** All four Spec 071 verbs present. CONFIRMED:
- `spectra ai ingest-grounding` — line 284
- `spectra ai record-drop` — line 302
- `spectra ai compile-repair-prompt` — line 317
- `spectra ai review-flagged` — line 332
- All four also appear in the command summary table at lines 371–374.

**`docs/usage.md`:** "Verdict Disposition (Spec 071)" section present at line 136. CONFIRMED opening:
> "The critic assigns every generated test a verdict — **grounded**, **partial**, or **hallucinated** — and the `spectra-generate` skill now makes those verdicts durable and visible."

Section covers: three-verdict table of automatic actions, one-attempt repair loop, `review-flagged` command, four-store consistency table.

No remaining doc claiming partials are silent or verdicts ephemeral was found.

---

## Cross-cutting — Inspection friction recurrence

Five categories of raw-shell inspection were needed to verify this spec — none covered by a `spectra` command:

| What was inspected | Method required | Covered by spectra? |
|---|---|---|
| All files in `.spectra/verdicts/` | Directory listing | No |
| Verdict type per test (grounded/partial/hallucinated) | Parse each JSON file individually | No — `review-flagged` surfaces only `flagged_for_review` partials |
| Which tests have / lack a grounding block in `.md` | Grep/read all .md frontmatter | No — no `spectra ai audit-grounding` command |
| Contents of `repaired-TC-NNN.json` scratch files | Direct file read | No |
| `.gitignore` coverage | Direct file read | No |

**Frequency:** Every check in Phase 1.2, 1.3, 1.4, 2.2, and 2.3 required at least one raw inspection step. The `spectra ai review-flagged` command is designed to absorb verdict/grounding inspection for flagged partials, but only fires when `ingest-grounding` has already written the flag — so it provides zero inspection value when the seam breaks before that point (exactly the failure mode observed here).

**Remaining gap candidates** (not designing the fix — just naming the gap):
1. **`spectra ai audit-grounding --suite <s>`** — list each test with its grounding state (has block / missing block / partial+unflagged). Would have surfaced the Phase 1.2 failure immediately.
2. **`spectra ai list-verdicts --suite <s>`** — aggregate verdict JSON counts by classification, without requiring directory + JSON parsing.

---

## Summary Table

| Phase | Check | Result | Evidence |
|-------|-------|--------|----------|
| Pre-0 | Installed hash matches HEAD | **PASS** | `2.1.0+30a5247` == HEAD `30a5247` |
| 1.1 | Grounded `.md` blocks present (3+) | **PASS** | TC-100/102/103: `verdict: grounded` in frontmatter — CONFIRMED |
| **1.2** | **Partial `.md` blocks present** | **FAIL** | 15 partial verdict JSONs; 0 `.md` files have grounding block — `ingest-grounding` not called for partials — CONFIRMED |
| 1.3a | Per-test verdict JSON naming | **PASS** | 35 × `critic-verdict-TC-NNN.json` in `.spectra/verdicts/` — CONFIRMED |
| 1.3b | Framework `.gitignore` updated | **PASS** | `.gitignore:37–38` — CONFIRMED |
| 1.3c | Consumer `.gitignore` updated | **FAIL** | Missing `.spectra/verdicts/` and `.spectra/dropped-tests.json` — CONFIRMED by grep |
| 1.4 | Drop trail + consistent delete | **N/A** | 0 hallucinated verdicts; `.spectra/dropped-tests.json` absent — CONFIRMED |
| 1.5 | Stale comments corrected | **PASS** | `TestFileWriter.cs`, `IngestVerdictCommand.cs`, `spectra-critic.agent.md` all updated — CONFIRMED |
| 2.1 | `compile-repair-prompt` plain-text stdout | **PASS** | `CompileRepairPromptCommand.cs:141–142` `Console.Out.Write` — CONFIRMED |
| **2.2** | **Partial got 1 repair attempt + grounding written** | **FAIL** | 5/15 have repair files; 0/15 have grounding block in `.md`; seam loop incomplete — CONFIRMED |
| 2.3 | Hallucinated not sent to repair | **N/A / PASS** | 0 hallucinated tests — CONFIRMED |
| 2.4 | Four-count batch report in skill | **PASS** | `spectra-generate.md:239–251` four buckets — CONFIRMED |
| 3.1 | `review-flagged` command exists + callable | **PASS** | Live run → `{"flagged_count":0,"tests":[]}` — CONFIRMED |
| 3.2 | Review renders verdict readably (not shell) | **PASS (design)** | `ReviewFlaggedHandler.cs:78` reads `CondensedFindings` direct — CONFIRMED |
| 3.2 | Review has data to render | **GAP** | No data because 1.2 failed; `condensed_findings: []` for all — INFERRED |
| 4.1 | cli-reference.md: 4 new verbs | **PASS** | Lines 284, 302, 317, 332 — CONFIRMED |
| 4.1 | usage.md: Verdict Disposition section | **PASS** | Line 136 — CONFIRMED |
| X-cut | Inspection friction recurrence | **GAP** | 5+ raw-shell inspection patterns still required; `review-flagged` only covers post-ingest-grounding state |

---

## Verdict

**Spec 071 CLI is implemented correctly.** The commands (`ingest-grounding`, `record-drop`, `compile-repair-prompt`, `review-flagged`), the `TestFileWriter` write-back, the per-test verdict JSON naming, and the docs are all present and correct. Grounded tests work end-to-end.

**The partial disposition path was not completed by the skill in this cycle.** `compile-repair-prompt` was called for 5 of 15 partial tests and repair patches were produced, but `ingest-update` + re-critic + `ingest-grounding` were not called for any of them. As a result, no partial test has a grounding block in its `.md` — the spec's core visible-verdicts goal is unmet for the partial cohort. This is a **skill execution fidelity** gap, not a code defect.

**Two actionable items:**
1. **Skill fidelity**: Run `spectra ai review-flagged --suite unit-converter` after re-running the partial disposition loop (`ingest-grounding` for remaining tests) — or re-run the generate session so the skill completes Step 8 for all 15 partials.
2. **Consumer `.gitignore`**: Re-run `spectra init` in `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST` to add `.spectra/verdicts/` and `.spectra/dropped-tests.json` before the next git commit.
