---
title: How SPECTRA double-checks tests
parent: Architecture
nav_order: 2
description: "How the critic checks each generated test against the doc sections it cites, what grounded, partial, hallucinated, flagged and unverified mean."
---

# How SPECTRA double-checks your tests

An AI model can write a test that sounds right but isn't in your docs: a step that doesn't exist, an error message
nobody documented, a wrong number. So every test SPECTRA writes or edits is checked by a second, separate AI step,
the *critic*, before it's kept. This page explains that check. To act on its results, see
[Review flagged tests](review).

{% include flow.html name="double-check" %}

## The labels

Every checked test gets one of these labels. They are the exact words the skills, the test files and
`spectra ai audit-grounding` use.

| Label | What it means | What happens |
|---|---|---|
| **grounded** | Every step and expected result is backed by your docs. | The test is kept. |
| **partial** | Most of the test is backed by your docs, but at least one detail isn't. | One automatic repair, then a new check. If it's still partial, the test is **flagged**. |
| **hallucinated** | The test invents behaviour or contradicts your docs. | The test is deleted and the reason is recorded. |
| **flagged** | A partial test that the repair didn't fix. | It waits for you: accept, fix, regenerate or delete ([Review flagged tests](review)). |
| **unverified** | The check hasn't run for this test, or couldn't finish. | Ask your agent to run the critic again; `spectra ai audit-grounding` lists these tests. |

## What the critic looks at

- **Only the doc sections the test cites.** It never sees how the test was written: not the prompt, not the
  reasoning, not the other tests. That keeps its judgement independent.
- **Every element on its own.** Each step, the preconditions, the expected result and the test data are checked
  against the text, and every finding names the element.
- **Numbers are recalculated.** When the expected result is a computed value (a unit conversion, a formula), the
  critic works it out itself. The right method with a wrong number isn't **grounded**. Example: a test expecting
  `1×10⁻⁹ km → 1E-9 nm` fails, because the right result is `1000 nm`.
- **Vague docs aren't treated as invention.** When the docs simply don't say, the detail is reported as unverified
  and the test is at most **partial**. **hallucinated** is kept for clear inventions and contradictions.

## What it doesn't check

The critic answers "is everything in this test backed by the docs?" It doesn't ask whether the tests cover
everything they should. That is the job of the analysis before generation, which also lists boundary cases the docs
imply but no test covers yet (min/max, off-by-one, empty values, timeouts). See [Generate test cases](usage).

## Where the result is saved

The label is written into the test file, in its `grounding:` section:

```yaml
grounding:
  verdict: partial
  score: 0.72
  generator: claude-code-session
  critic: claude-sonnet-4-6
  verified_at: 2026-03-19T10:30:00Z
  unverified_claims:
    - "Step 3: assumes refund email sent within 5 minutes"
    - "Expected Result: specific error code not in docs"
```

`critic` names the model that checked the test. When your agent's main session runs the check as a fallback, it can
read `unknown` (a known issue). When you review a flagged test, your name, the date and your note are added here too;
a later check keeps them.

## When the check fails

A check that can't produce a valid result never passes quietly. A critic answer without a label or a score is
reported as an error, not counted as **partial**. A critic call that fails or times out leaves the test
**unverified**, recorded separately from a bad answer, so you can tell the two apart.

## Settings

The critic is the `spectra-critic` subagent. Its model is `ai.critic.model` in `spectra.config.json` (default
`sonnet`; also an alias such as `opus`, a full model ID, or `inherit` for your session's model). `spectra init` and
`spectra update-skills` write it into `.claude/agents/spectra-critic.agent.md`; a copy you edited is never
overwritten. To change what the critic is told, edit `.spectra/prompts/critic-verification.md`
([Tune how tests are written](generation-profiles)). Older `ai.critic` and `ai.providers` settings are ignored.

Related: [Skills and agents](skills-integration) · [Configuration](configuration) · [Test format](test-format)
