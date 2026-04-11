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

When AI generates test cases, it can hallucinate — invent steps, expected results, or behaviors that don't exist in your documentation. SPECTRA's grounding verification uses a second AI model (the "critic") to verify each test against the source documentation.

## How It Works

1. **Generator** creates draft test cases from your documentation
2. **Critic** (different model) verifies each test against the same docs
3. Tests receive a verdict: `grounded`, `partial`, or `hallucinated`
4. Only grounded and partial tests are written to disk

## Verdicts

| Verdict | Meaning | Action |
|---------|---------|--------|
| `grounded` | All steps trace to documentation | Written as-is |
| `partial` | Some steps have assumptions | Written with warnings |
| `hallucinated` | Contains invented behaviors | Rejected |

## Grounding Metadata

Verified tests include grounding metadata in their frontmatter:

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
Generating tests...
✓ Generated 10 tests

  ✓ 7 grounded
  ⚠ 2 partial — written with grounding warnings
  ✗ 1 hallucinated — rejected

✓ 9 tests written to tests/checkout/
✓ Index updated

ℹ Partial tests (review recommended):
   TC-209  Assumes refund email is sent within 5 minutes — not confirmed in docs
   TC-212  Navigation path to currency settings not documented

ℹ Rejected tests:
   TC-220  References "fraud detection API" — not mentioned in any documentation
```

## Configuration

Configure the critic in `spectra.config.json`:

```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-5-mini",
      "timeout_seconds": 120,
      "max_concurrent": 5
    }
  }
}
```

> **Spec 043 — parallel verification:** `max_concurrent` (default `1`) controls how many critic verification calls run concurrently. Setting it to `5` typically cuts the critic phase to ~1/5 of sequential time on a large suite without changing any output (results are written in original input order). Clamped to `[1, 20]`. Values >10 emit a rate-limit-risk warning at run start. If you start hitting rate limits, the Run Summary panel surfaces a `Rate limits` count with a hint pointing back at this knob.

> **Spec 041:** `gpt-5-mini` is the new default critic model (was `gpt-4o-mini`). It's included free on any paid Copilot plan and, when paired with a `gpt-4.1` generator, provides cross-architecture verification without burning premium requests. For Claude generators, the default critic rotates to `claude-haiku-4-5`. Per-provider defaults resolved by `CriticConfig.GetEffectiveModel()`: `github-models` / `openai` / `azure-openai` → `gpt-5-mini`; `anthropic` / `azure-anthropic` → `claude-haiku-4-5`.

Supported critic providers (spec 039 — same set as the generator):
`github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`.

Default API key environment variables:
- `github-models`: `GITHUB_TOKEN`
- `azure-openai`: `AZURE_OPENAI_API_KEY`
- `azure-anthropic`: `AZURE_ANTHROPIC_API_KEY`
- `openai`: `OPENAI_API_KEY`
- `anthropic`: `ANTHROPIC_API_KEY`

> **Legacy values**: `provider: "github"` is accepted as a soft alias for
> `github-models` (with a one-line deprecation warning on stderr). The legacy
> value `provider: "google"` is no longer supported — the Copilot SDK runtime
> cannot route to Google. Update your config to one of the canonical five
> providers above.

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
