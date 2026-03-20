# Test Case Format

How SPECTRA stores manual test cases as Markdown files with YAML frontmatter.

Related: [CLI Reference](cli-reference.md) | [Generation Profiles](generation-profiles.md) | [Coverage](coverage.md)

---

## Overview

Tests are Markdown files with YAML frontmatter, stored in `tests/{suite}/`:

```markdown
---
id: TC-102
priority: high
tags: [payments, negative]
component: checkout
source_refs: [docs/features/checkout/payment-methods.md]
requirements: [REQ-042]
automated_by:
  - tests/e2e/CheckoutTests.cs
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T10:30:00Z
---

# Checkout with expired card

## Preconditions
- User is logged in
- Cart has at least one item

## Steps
1. Navigate to checkout
2. Enter expired card details (exp: 01/2020)
3. Click "Pay Now"

## Expected Result
- Payment is rejected
- Error message displays: card expired
```

## Core Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique test ID (e.g., `TC-102`). Globally unique across all suites. |
| `priority` | string | Yes | `high`, `medium`, or `low` |
| `title` | string | No | Derived from the first `#` heading if not in frontmatter |
| `tags` | string[] | No | Categorization tags (e.g., `[smoke, payments]`) |
| `component` | string | No | Feature area or component name |
| `depends_on` | string[] | No | Test IDs that must pass first |

## Documentation & Traceability Fields

| Field | Type | Description |
|-------|------|-------------|
| `source_refs` | string[] | Paths to documentation files this test was generated from |
| `requirements` | string[] | Requirement IDs this test covers (e.g., `[REQ-042]`) |
| `automated_by` | string[] | Paths to automation code files that implement this test |

These fields power [coverage analysis](coverage.md):
- `source_refs` → Documentation Coverage
- `requirements` → Requirements Coverage
- `automated_by` → Automation Coverage (can be auto-populated via `--auto-link`)

## Grounding Fields

Added automatically when [grounding verification](grounding-verification.md) is enabled:

| Field | Type | Description |
|-------|------|-------------|
| `grounding.verdict` | string | `grounded`, `partial`, or `hallucinated` |
| `grounding.score` | decimal | Confidence score (0.0–1.0) |
| `grounding.generator` | string | Model that generated the test |
| `grounding.critic` | string | Model that verified the test |
| `grounding.verified_at` | datetime | When verification occurred |
| `grounding.unverified_claims` | string[] | Claims the critic couldn't verify (partial verdicts) |

## Body Structure

After the frontmatter, tests use standard Markdown:

- **`# Title`** — Test case name (first H1 heading)
- **`## Preconditions`** — Setup requirements before test execution
- **`## Steps`** — Numbered list of test steps
- **`## Expected Result`** — What should happen after executing the steps
- **`## Test Data`** *(optional)* — Specific data values needed

## Test ID Allocation

Test IDs are unique globally across all suites. When generating tests:

1. SPECTRA scans all `_index.json` files to find existing IDs
2. New tests continue from the global maximum (e.g., if TC-150 exists anywhere, new tests start at TC-151)
3. This prevents ID collisions when tests are moved between suites

## Suite Index (`_index.json`)

Each suite directory contains an `_index.json` with metadata for all tests:

```json
{
  "suite": "checkout",
  "generated_at": "2026-03-18T12:00:00Z",
  "test_count": 2,
  "tests": [
    {
      "id": "TC-001",
      "title": "Checkout with valid Visa card",
      "priority": "high",
      "file": "TC-001.md",
      "tags": ["smoke", "checkout"],
      "source_refs": ["docs/features/checkout.md"],
      "requirements": ["REQ-042"],
      "automated_by": ["tests/e2e/CheckoutTests.cs"]
    }
  ]
}
```

The `requirements` and `automated_by` fields are only included when populated.
