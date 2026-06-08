---
title: Grounding Verification
parent: User Guide
nav_order: 5
---

# Grounding Verification

Dual-model critic flow to detect hallucinated test cases.

Related: [CLI Reference](cli-reference.md) | [Configuration](configuration.md) | [Test Format](test-format.md)

---

## Overview

When AI generates test cases, it can hallucinate — invent steps, expected results, or behaviors that don't exist in your documentation. SPECTRA's grounding verification uses a second AI model (the "critic") to verify each test case against the source documentation.

## How It Works

1. **Generator** creates draft test cases from your documentation
2. **Critic** verifies each test case against the same docs. The critic runs in a fresh, isolated
   (`context: fork`) context — it sees only the test artifact and its selected source documents,
   never the generator's prompt, reasoning, tool calls, or token usage.
3. Test cases receive a verdict: `grounded`, `partial`, or `hallucinated`
4. Only grounded and partial test cases are written to disk

### Advisory verdict, fail-loud damage

The verdict is **advisory-gating**: a clear `hallucinated` verdict drops the test; `grounded`,
`partial`, and manually-marked tests pass through. But the critic's *damage* paths fail loud rather
than silently passing:

- A critic response missing its `verdict` or `score`, or otherwise unparseable, is surfaced as a
  specific error — it is **not** smoothed into a soft `partial` / `0.5` pass.
- A critic *call* that fails or times out is non-blocking (the test is marked unverified and
  generation continues) but is recorded **distinctly** from a malformed response, so a failed
  critic and a bad critic response are never conflated.

### Grounding vs. boundary/edge completeness — two distinct checks

Grounding answers **"does each claim in a generated test trace back to the docs?"** It is a
*retrospective, per-artifact* check, and it is **deliberately scoped to grounding only**: the critic
judges what is in front of it and treats anything it wasn't given as `unverified`, never as a missing
test. It does **not** ask whether the edges that *should* be tested are covered.

That second question — **"are the boundary/edge conditions the docs imply actually covered?"** — is
**completeness**, and it lives in the **analysis phase**, not the critic (Spec 062). When you run the
analyze step, `ingest-analysis` reports **boundary-coverage gaps** (min/max, off-by-one, empty/null,
overflow, timeout) implied by the docs/criteria but not covered by existing or planned tests, in a
`boundary_gaps` array alongside the `technique_breakdown`. It is *proactive* (surface the gap so the
edge case gets generated) and **advisory** — it never blocks generation and never changes the critic's
verdict.

Keeping these separate is intentional: folding completeness into the critic would erode its clean
grounding-only contract. See the [generation/analysis flow](user-guide.md) for where boundary gaps
surface in the analyze recommendation.

| Concern | Where | Question | Timing |
|---------|-------|----------|--------|
| **Grounding** | `spectra-critic` subagent | Does each claim trace to the docs? | Retrospective (after generation) |
| **Boundary/edge completeness** | Analysis phase (`ingest-analysis`) | Are the implied edges covered? | Proactive (before generation) |

## Verdicts

| Verdict | Meaning | Action |
|---------|---------|--------|
| `grounded` | All steps trace to documentation | Written as-is |
| `partial` | Some steps have assumptions | Written with warnings |
| `hallucinated` | Contains invented behaviors | Rejected |

## Grounding Metadata

Verified test cases include grounding metadata in their frontmatter:

```yaml
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T10:30:00Z
  unverified_claims: []
```

For partial verdicts, `unverified_claims` lists what couldn't be verified:

```yaml
grounding:
  verdict: partial
  score: 0.72
  unverified_claims:
    - "Step 3: assumes refund email sent within 5 minutes"
    - "Expected Result: specific error code not in docs"
```

## Verification Output

After generation, SPECTRA displays verification results:

```
Generating test cases...
✓ Generated 10 test cases

  ✓ 7 grounded
  ⚠ 2 partial — written with grounding warnings
  ✗ 1 hallucinated — rejected

✓ 9 test cases written to test-cases/checkout/
✓ Index updated

ℹ Partial test cases (review recommended):
   TC-209  Assumes refund email is sent within 5 minutes — not confirmed in docs
   TC-212  Navigation path to currency settings not documented

ℹ Rejected test cases:
   TC-220  References "fraud detection API" — not mentioned in any documentation
```

## Configuration

Configure the critic in `spectra.config.json`:

```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "model": "claude-sonnet-4-6",
      "timeout_seconds": 120,
      "max_concurrent": 5
    }
  }
}
```

> **Spec 058 — the critic is the spectra-critic subagent:** grounding verification runs as the
> **spectra-critic subagent** (`.claude/agents/spectra-critic`), a Claude Code `context: fork`
> subagent invoked by the generation skill after generation — not an in-process model call.
> `ai.critic.model` is the only critic selector. The retired `provider`, `api_key_env`, and
> `base_url` keys are ignored (`spectra validate` emits a non-blocking notice if they are still
> present). The generator still runs in-process via `ai.providers`.

> **Spec 043 — parallel verification:** `max_concurrent` (default `1`) controls how many critic verification calls run concurrently. Setting it to `5` typically cuts the critic phase to ~1/5 of sequential time on a large suite without changing any output (results are written in original input order). Clamped to `[1, 20]`. Values >10 emit a rate-limit-risk warning at run start. If you start hitting rate limits, the Run Summary panel surfaces a `Rate limits` count with a hint pointing back at this knob.

> **Spec 055 — single model selector:** `ai.critic.model` is the single source of truth for the critic model. When it is set, that value is used; when it is unset, one same-family default applies (target: Sonnet 4.6). Flipping the critic model is a config change, never a code change — which is what lets a post-migration bake-off compare model choices without touching code.

## Skip Verification

```bash
spectra ai generate checkout --skip-critic
```

Or disable globally in config:

```json
{
  "ai": {
    "critic": {
      "enabled": false
    }
  }
}
```
