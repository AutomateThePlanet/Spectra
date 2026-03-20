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
      "provider": "google",
      "model": "gemini-2.0-flash",
      "timeout_seconds": 30
    }
  }
}
```

Supported critic providers: `google`, `openai`, `anthropic`, `github`

Default API key environment variables:
- Google: `GOOGLE_API_KEY`
- OpenAI: `OPENAI_API_KEY`
- Anthropic: `ANTHROPIC_API_KEY`
- GitHub: `GITHUB_TOKEN`

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
