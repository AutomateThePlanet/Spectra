# Quickstart: AI Test Generation CLI

**Date**: 2026-03-13
**Feature**: 001-ai-test-generation-cli

This guide walks through the basic usage of the Spectra CLI for AI-powered test generation.

---

## Prerequisites

- .NET 8.0+ runtime installed
- GitHub Copilot CLI installed and authenticated, OR
- API key for OpenAI/Anthropic (BYOK mode)

## Installation

```bash
# Install as global .NET tool
dotnet tool install -g spectra

# Verify installation
spectra --version
```

---

## Initialize a Repository

```bash
# Navigate to your project
cd my-project

# Initialize SPECTRA
spectra init
```

This creates:
```
my-project/
├── spectra.config.json    # Configuration file
├── docs/                  # Source documentation (create if needed)
├── tests/                 # Generated test cases
└── .github/
    └── skills/
        └── test-generation/
            └── SKILL.md   # AI agent skill definition
```

---

## Configure AI Providers

Edit `spectra.config.json` to configure your AI provider:

### Option 1: GitHub Copilot (default)

If you have Copilot CLI authenticated, no additional configuration needed.

### Option 2: BYOK (Bring Your Own Key)

```json
{
  "ai": {
    "providers": [
      {
        "name": "anthropic",
        "model": "claude-sonnet-4-5",
        "api_key_env": "ANTHROPIC_API_KEY",
        "enabled": true,
        "priority": 1
      }
    ],
    "fallback_strategy": "auto"
  }
}
```

Set your API key:
```bash
export ANTHROPIC_API_KEY="sk-..."
```

---

## Add Documentation

Create documentation in `docs/` that describes your system:

```markdown
<!-- docs/features/checkout/checkout-flow.md -->
# Checkout Flow

## Overview
Users can complete purchases through our checkout flow.

## Happy Path
1. User adds items to cart
2. User clicks "Checkout"
3. User enters shipping address
4. User selects payment method
5. User reviews order
6. User clicks "Place Order"
7. Order confirmation displayed

## Payment Methods
- Visa, Mastercard, Amex
- PayPal
- Apple Pay

## Error Handling
- Invalid card: Show "Card declined" message
- Expired card: Show "Card expired" message
- Insufficient funds: Show "Payment failed" message
```

---

## Generate Tests

```bash
# Generate tests for a suite
spectra ai generate --suite checkout

# With options
spectra ai generate --suite checkout --count 20 --tags smoke,payments
```

### Interactive Review

After generation, you'll see a summary:

```
Generated 18 tests for suite: checkout

Summary:
  ✓ 15 valid tests
  ⚠ 2 potential duplicates
  ✗ 1 invalid (missing expected result)

Options:
  (r)eview one by one    (a)ccept all valid    (v)iew duplicates
  (e)xport to file       (q)uit

>
```

Press `a` to accept all valid tests, or `r` to review each one.

### CI Mode (Non-Interactive)

```bash
spectra ai generate --suite checkout --no-review
```

---

## Validate Tests

```bash
# Validate all tests
spectra validate

# Validate specific suite
spectra validate --suite checkout
```

Expected output:
```
Validating tests...
  ✓ checkout: 42 tests valid
  ✓ auth: 18 tests valid

Validation passed: 60 tests in 2 suites
```

Use in CI pipelines:
```yaml
# .github/workflows/validate.yml
- name: Validate tests
  run: spectra validate
```

---

## View Tests

```bash
# List all suites
spectra list

# Show a specific test
spectra show TC-102
```

---

## Update Tests When Docs Change

When your documentation changes:

```bash
# Analyze and update tests
spectra ai update --suite checkout
```

This will:
1. Compare tests against current documentation
2. Identify outdated, orphaned, or redundant tests
3. Propose updates for your review

---

## Analyze Coverage

```bash
# Get coverage report
spectra ai analyze --suite checkout

# Export as JSON
spectra ai analyze --suite checkout --format json --output coverage.json
```

---

## Test Case Format

Generated tests use this Markdown format:

```markdown
---
id: TC-102
priority: high
tags: [payments, negative]
component: checkout
source_refs: [docs/features/checkout/checkout-flow.md]
---

# Checkout with expired card

## Preconditions
- User is logged in
- Cart contains at least one item

## Steps
1. Navigate to checkout
2. Enter expired card details (exp: 01/2020)
3. Click "Pay Now"

## Expected Result
- Payment is rejected
- Error message displays: "Card expired"
- User remains on checkout page

## Test Data
- Card number: 4111 1111 1111 1111
- Expiry: 01/2020
```

---

## Troubleshooting

### "Suite is locked"

Another process is generating tests. Wait for it to complete or:
```bash
# Remove stale lock (use with caution)
rm tests/checkout/.spectra.lock
```

### "Provider failed"

Check your API key configuration:
```bash
# Test with specific provider
spectra ai generate --suite checkout --provider anthropic -vv
```

### Validation Errors

Use verbose mode to see details:
```bash
spectra validate -v
```

---

## Next Steps

- Read the [Architecture Specification](../../final-architecture-v3.md) for full details
- Configure suite-specific settings in `spectra.config.json`
- Set up CI validation with `spectra validate`
