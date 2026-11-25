---
title: How SPECTRA double-checks tests
parent: Architecture
nav_order: 2
description: "How the critic checks each generated test against the doc sections it cites, what grounded, partial, hallucinated, flagged and unverified mean."
---

# How SPECTRA double-checks your tests

Every generated or edited test case is checked by a separate AI step, the *critic*, before SPECTRA keeps it. This page
explains how that check works. To act on its results, see [Review flagged tests](review).

Related: [CLI Reference](cli-reference) | [Configuration](configuration) | [Test Format](test-format)

---

## Overview

When AI generates test cases, it can hallucinate, inventing steps, expected results, or behaviors that don't exist in your documentation. SPECTRA's grounding verification has a separate AI step, the critic, check each test case against the source documentation. The critic runs in its own context on the model set in `ai.critic.model`; that can be the same model family that wrote the test.

## How It Works

1. **Generator** creates draft test cases from your documentation
2. **Critic** verifies each test case against the same docs. The critic runs in a fresh, isolated
   subagent context, so it sees only the test artifact and the source text it cites (the cited
   sections; `compile-critic-prompt --full-docs` gives whole documents),
   never the generator's prompt, reasoning, tool calls, or token usage. Its instructions are the
   `critic-verification` prompt template (customise `.spectra/prompts/critic-verification.md`).
3. Test cases receive a verdict: `grounded`, `partial`, or `hallucinated`
4. A `hallucinated` test is deleted, and the reason is recorded (`spectra ai record-drop`). `grounded` and
   `partial` tests are kept.
5. A partial test gets one repair attempt. Only a repair that changed the test is judged again; a repair that left
   it unchanged keeps the verdict it already has, which routes it as before

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
grounding-only contract. See [Generate test cases](usage) for where boundary gaps
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

These are the labels the skills, the test files and `spectra ai audit-grounding` use.

| Label | Meaning | What happens |
|---------|---------|--------|
| `grounded` | Every step traces to the documentation, and any computed expected value is arithmetically correct | Kept |
| `partial` | Most of the test is backed by the docs, but at least one detail isn't | One automatic repair; if it's still partial, the test is **flagged** |
| `hallucinated` | The test invents behaviour or contradicts the docs | Deleted; the reason is recorded |
| flagged | A partial test waiting for a person: accept, fix, regenerate or delete ([Review flagged tests](review)) | Stays until reviewed |
| unverified | The critic hasn't run for this test, or couldn't finish | Run the critic again (`spectra ai audit-grounding` lists these) |

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
frontmatter). When the main session runs the critic as a fallback, `critic` can read `unknown` (a known issue).

For partial verdicts, `unverified_claims` lists what couldn't be verified:

```yaml
grounding:
  verdict: partial
  score: 0.72
  unverified_claims:
    - "Step 3: assumes refund email sent within 5 minutes"
    - "Expected Result: specific error code not in docs"
```

## What you see after generation

The `spectra-generate` skill ends with a report: per test the ID, title and label, grouped as kept grounded,
repaired to grounded, flagged partial, dropped hallucinated and unverified, plus a hint to run
[Review flagged tests](review) when any are flagged. A real run from the Calculator demo: 5 tests, 3 grounded at once,
2 partial, both repaired to grounded.

## Configuration

There's no `spectra.config.json` block for the critic anymore. Grounding verification runs as the
`spectra-critic` subagent (`.claude/agents/spectra-critic.agent.md`), a Claude Code subagent
(a fresh context per test) started by the generation and update subagents for each test they wrote, not an in-process model call, and not
something `ai.providers`/`ai.critic` route to. (A legacy config that still carries those keys loads
unchanged; `spectra validate` surfaces a non-blocking notice naming them.)

The critic model is `ai.critic.model` in `spectra.config.json` (default `sonnet`; an alias such as `opus`, a model
id, or `inherit` for your session's model). `spectra init` and `spectra update-skills` write it into
`.claude/agents/spectra-critic.agent.md`; an agent file you edited by hand is never overwritten. There's no separate
provider/timeout/concurrency config; Claude Code itself schedules subagent calls.

The generator isn't configurable either. Generation runs as an ordinary turn in your own Claude
Code session, driven by the `spectra-generate` skill over the compile→ingest seam. See
[Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) for the history (the SDK was removed).
