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

When AI generates test cases, it can hallucinate, inventing steps, expected results, or behaviors that don't exist in your documentation. SPECTRA's grounding verification uses a second AI model (the "critic") to verify each test case against the source documentation.

## How It Works

1. **Generator** creates draft test cases from your documentation
2. **Critic** verifies each test case against the same docs. The critic runs in a fresh, isolated
   (`context: fork`) context, so it sees only the test artifact and its selected source documents,
   never the generator's prompt, reasoning, tool calls, or token usage.
3. Test cases receive a verdict: `grounded`, `partial`, or `hallucinated`
4. Only grounded and partial test cases are written to disk

### Advisory verdict, fail-loud damage

The verdict is advisory-gating, meaning a clear `hallucinated` verdict drops the test while `grounded`,
`partial`, and manually-marked tests pass through. But the critic's *damage* paths fail loud rather
than silently passing:

- A critic response missing its `verdict` or `score`, or otherwise unparseable, is surfaced as a
  specific error. It is **not** smoothed into a soft `partial` / `0.5` pass.
- A critic *call* that fails or times out is non-blocking (the test is marked unverified and
  generation continues) but is recorded **distinctly** from a malformed response, so a failed
  critic and a bad critic response are never conflated.

### Grounding vs. boundary/edge completeness: two distinct checks

Grounding answers **"does each claim in a generated test trace back to the docs?"** It is a
*retrospective, per-artifact* check, and it is deliberately scoped to grounding only: the critic
judges what is in front of it and treats anything it wasn't given as `unverified`, never as a missing
test. It does **not** ask whether the edges that *should* be tested are covered.

That second question, **"are the boundary/edge conditions the docs imply actually covered?"**, is
completeness, and it lives in the analysis phase rather than the critic. When you run the
analyze step, `ingest-analysis` reports boundary-coverage gaps (min/max, off-by-one, empty/null,
overflow, timeout) implied by the docs/criteria but not covered by existing or planned tests, in a
`boundary_gaps` array alongside the `technique_breakdown`. It is *proactive*, surfacing the gap so the
edge case gets generated, and advisory: it never blocks generation and never changes the critic's
verdict.

Keeping these separate is intentional: folding completeness into the critic would erode its clean
grounding-only contract. See the [generation/analysis flow](user-guide.md) for where boundary gaps
surface in the analyze recommendation.

| Concern | Where | Question | Timing |
|---------|-------|----------|--------|
| **Grounding** | `spectra-critic` subagent | Does each claim trace to the docs? | Retrospective (after generation) |
| **Boundary/edge completeness** | Analysis phase (`ingest-analysis`) | Are the implied edges covered? | Proactive (before generation) |

## Arithmetic Verification

When a test's expected result is a **computed value** (a unit conversion result, formula output,
scientific-notation magnitude, or derived constant), the critic **must independently compute the
value and compare it** to what the test asserts. Documenting the *principle* (e.g. "use scientific
notation", "convert via the SI factor") is not sufficient if the *number* is wrong.

**Rule:** principle in docs AND number arithmetically correct → eligible for `grounded`. Principle
in docs BUT number arithmetically wrong → NOT `grounded` (use `partial` or `hallucinated` with an
`unverified` or `hallucinated` finding on the Expected Result element).

This is additive to the existing doc-presence rules, not a replacement.

Example: a test asserting `1×10⁻⁹ km → 1E-9 nm` would NOT be `grounded`, because the correct result is
`1000 nm` (1 km = 10¹² nm, so 1×10⁻⁹ km = 10³ nm), even if the scientific-notation convention is
documented. The arithmetic error must surface in a finding.

## Verdicts

| Verdict | Meaning | Action |
|---------|---------|--------|
| `grounded` | All steps trace to documentation AND any computed expected values are arithmetically correct | Written as-is |
| `partial` | Some steps have assumptions or an expected value is flagged | Written with warnings |
| `hallucinated` | Contains invented behaviors | Rejected |

## Grounding Metadata

Verified test cases include grounding metadata in their frontmatter:

```yaml
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-code-session
  critic: claude-sonnet-4-6
  verified_at: 2026-03-19T10:30:00Z
  unverified_claims: []
```

`generator` is always `claude-code-session`. There's no specific generator model to name, since
generation runs as a turn in whatever your Claude Code session happens to be running. `critic` names
the model that actually verified the test (from the `spectra-critic` subagent's `model:`
frontmatter).

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

There's no `spectra.config.json` block for the critic anymore. Grounding verification runs as the
`spectra-critic` subagent (`.claude/agents/spectra-critic.agent.md`), a Claude Code `context: fork`
subagent invoked by the generation skill after generation, not an in-process model call, and not
something `ai.providers`/`ai.critic` route to. (A legacy config that still carries those keys loads
unchanged; `spectra validate` surfaces a non-blocking notice naming them.)

The critic model is set by the `model:` field in `.claude/agents/spectra-critic.agent.md` (shipped as
`claude-sonnet-4-6`). Edit that file directly to change it, and re-apply the edit after
`spectra update-skills` if you've customized it. There's no separate provider/timeout/concurrency
config; Claude Code itself schedules subagent calls.

The generator isn't configurable either. Generation runs as an ordinary turn in your own Claude
Code session, driven by the `spectra-generate` skill over the compile→ingest seam. See
[Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) for the full picture.
