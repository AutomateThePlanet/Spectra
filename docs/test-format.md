---
title: Test Format
parent: User Guide
nav_order: 3
---

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
description: Verify that expired credit cards are rejected at checkout
estimated_duration: 5m
source_refs: [docs/features/checkout/payment-methods.md]
criteria: [AC-CHECKOUT-042]
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
| `description` | string | No | Short description of what the test verifies (indexed and searchable) |
| `estimated_duration` | string | No | Estimated execution time (e.g., `5m`, `1h 30m`) |
| `depends_on` | string[] | No | Test IDs that must pass first |

## Documentation & Traceability Fields

| Field | Type | Description |
|-------|------|-------------|
| `source_refs` | string[] | Paths to documentation files this test was generated from |
| `criteria` | string[] | Acceptance criteria IDs this test covers (e.g., `[AC-CHECKOUT-042]`) |
| `automated_by` | string[] | Paths to automation code files that implement this test |

These fields power [coverage analysis](coverage.md):
- `source_refs` → Documentation Coverage
- `criteria` → Acceptance Criteria Coverage
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
      "description": "Verify standard Visa card checkout flow",
      "priority": "high",
      "file": "TC-001.md",
      "tags": ["smoke", "checkout"],
      "estimated_duration": "5m",
      "source_refs": ["docs/features/checkout.md"],
      "criteria": ["AC-CHECKOUT-042"],
      "automated_by": ["tests/e2e/CheckoutTests.cs"]
    }
  ]
}
```

The `description`, `estimated_duration`, `criteria`, and `automated_by` fields are only included when populated.

---

## Test ID allocation and the high-water-mark (Spec 040, v1.52.0+)

Test IDs are **globally unique across all suites**. Concurrent generation runs and stale `_index.json` files cannot produce overlapping ID ranges.

### How it works

`spectra ai generate` (and any other path that allocates new IDs) goes through `Spectra.Core.IdAllocation.PersistentTestIdAllocator`, which:

1. Acquires an exclusive cross-process lock at `.spectra/id-allocator.lock` (10 s timeout).
2. Reads the persisted high-water-mark from `.spectra/id-allocator.json`.
3. Computes the effective starting point as the maximum of:
   - The high-water-mark.
   - The largest ID in any suite's `_index.json`.
   - The largest ID found by walking `test-cases/**/*.md` frontmatter directly.
   - The configured `tests.id_start` (minus 1) as a floor.
4. Allocates `[effective+1 ... effective+count]`.
5. Writes the new HWM atomically (temp+rename).
6. Releases the lock.

### Persistent state

Both files are workspace-local, gitignored, and regenerable:

- **`.spectra/id-allocator.json`** — schema:
  ```json
  {
    "version": 1,
    "high_water_mark": 247,
    "last_allocated_at": "2026-04-30T14:30:00Z",
    "last_allocated_command": "ai generate"
  }
  ```
  If corrupted, missing, or recorded by an unknown future version, the allocator treats it as "absent" and re-seeds from the index + filesystem scan. The "deleted IDs never reused" guarantee is restored on the next allocation.

- **`.spectra/id-allocator.lock`** — empty file used as the cross-process mutex. Released automatically on process exit (including crash).

### Diagnosing problems

```bash
spectra doctor ids                                  # read-only audit
spectra doctor ids --fix                            # renumber duplicates
```

The audit reports duplicates with file paths and mtimes, mismatches between `_index.json` and on-disk frontmatter, the current HWM, and the next ID that would be allocated. Under `--fix`, the older file (by mtime) keeps the duplicated ID; later occurrences are renumbered at HWM+1, HWM+2, etc. `depends_on` references inside test files are updated; in-source `[TestCase("TC-NNN")]` literals are reported as `unfixable_references` for manual review.

