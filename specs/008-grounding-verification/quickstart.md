# Quickstart: Grounding Verification Pipeline

**Feature**: 008-grounding-verification
**Date**: 2026-03-19

## Overview

The grounding verification pipeline uses a "critic" model to verify AI-generated tests against source documentation before writing them to disk.

---

## Configuration

### Enable Critic Verification

Add the `critic` section to your `spectra.config.json`:

```json
{
  "ai": {
    "providers": [
      { "name": "copilot", "model": "gpt-4o", "priority": 1 }
    ],
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash",
      "api_key_env": "GOOGLE_API_KEY"
    }
  }
}
```

### Supported Providers

| Provider | Model | Environment Variable |
|----------|-------|---------------------|
| `google` | `gemini-2.0-flash` | `GOOGLE_API_KEY` |
| `openai` | `gpt-4o-mini` | `OPENAI_API_KEY` |
| `anthropic` | `claude-3.5-haiku` | `ANTHROPIC_API_KEY` |
| `github` | `gpt-4o-mini` | `GITHUB_TOKEN` |

### Set API Key

```bash
# For Google Gemini
export GOOGLE_API_KEY="your-api-key"

# For OpenAI
export OPENAI_API_KEY="sk-..."
```

---

## Usage

### Generate with Verification (Default)

When critic is enabled, verification runs automatically:

```bash
spectra ai generate checkout --focus "payment validation"
```

**Output**:

```
  ◐ Generating tests...
  ◐ Verifying against documentation (gemini-2.0-flash)...

  ✓ 18 grounded
  ⚠  4 partial — written with grounding warnings
  ✗  2 hallucinated — rejected

  ✓ 22 tests written to tests/checkout/
  ✓ Index updated

  ℹ Partial tests (review recommended):
    TC-209  Assumes refund email is sent within 5 minutes — not confirmed in docs
    TC-212  Navigation path to currency settings not documented
    TC-215  "3 retry attempts" — specific number not in documentation
    TC-218  Assumes cart persists across sessions — not confirmed

  ℹ Rejected tests:
    TC-220  References "fraud detection API" — not mentioned in any documentation
    TC-222  Describes offline payment mode — no documentation source found
```

### Skip Verification

For rapid iteration or incomplete documentation:

```bash
spectra ai generate checkout --skip-critic
```

**Output**:

```
  ◐ Generating tests...

  ℹ Verification skipped (--skip-critic)

  ✓ 24 tests written to tests/checkout/
  ✓ Index updated
```

### Disable Verification Globally

Set `enabled: false` in config:

```json
{
  "ai": {
    "critic": {
      "enabled": false
    }
  }
}
```

---

## Verdicts

| Verdict | Symbol | Action | When |
|---------|--------|--------|------|
| `grounded` | ✓ | Written with metadata | All claims trace to docs |
| `partial` | ⚠ | Written with warnings | Some claims unverified |
| `hallucinated` | ✗ | Rejected | Invented content detected |

---

## Test File Output

### Grounded Test

```yaml
---
id: TC-102
priority: high
tags: [payments, negative]
component: checkout
source_refs: [docs/features/checkout/payment-methods.md]
grounding:
  verdict: grounded
  score: 0.94
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T14:30:00Z
  unverified_claims: []
---

# Reject payment with invalid CVV

## Preconditions
- User is logged in
- Cart contains items

## Steps
1. Navigate to checkout
2. Enter credit card with invalid CVV (e.g., "000")
3. Click "Pay"

## Expected Result
- Payment is rejected
- Error message displays: "Invalid CVV"
```

### Partial Test

```yaml
---
id: TC-209
priority: medium
tags: [refunds, email]
grounding:
  verdict: partial
  score: 0.72
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T14:30:00Z
  unverified_claims:
    - "Step 3: assumes refund email is sent within 5 minutes — not confirmed in docs"
---

# Verify refund confirmation email

## Steps
1. Complete a refund request
2. Check email inbox
3. Verify refund email arrives within 5 minutes  ⚠ UNVERIFIED

## Expected Result
- Email contains refund amount and transaction ID
```

---

## Error Handling

### Critic API Unavailable

```
  ⚠ Critic unavailable: Connection timeout

  ? Proceed without verification? [Y/n]
```

- In interactive mode: User chooses to proceed or abort
- In `--no-interaction` mode: Tests written without verification

### Authentication Failure

```
  ✗ Critic authentication failed

  Check your GOOGLE_API_KEY environment variable.
  See: https://ai.google.dev/gemini-api/docs/api-key
```

---

## Cost Estimate

| Model | Input | Output | 20 Tests |
|-------|-------|--------|----------|
| Gemini Flash | $0.075/1M | $0.30/1M | ~$0.002 |
| GPT-4o-mini | $0.15/1M | $0.60/1M | ~$0.004 |

Verification adds negligible cost (<$0.01 per batch).

---

## CI/CD Integration

```bash
# Non-interactive mode with verification
spectra ai generate checkout --no-interaction

# Non-interactive mode, skip verification for speed
spectra ai generate checkout --no-interaction --skip-critic
```

Exit codes:
- `0`: Success (all tests grounded or partial)
- `1`: Error (generation failed, auth failed, etc.)

Note: Hallucinated tests are rejected but don't cause non-zero exit. Use grounded test count in CI assertions if needed.
