# SPECTRA Feature Spec: Generation Session Flow

**Status:** Draft — ready for spec-kit cycle  
**Priority:** HIGH  
**Depends on:** CLI Non-Interactive spec, Phase 1 (CLI), Generation Profiles  
**Replaces:** feature-smart-test-count.md and feature-undocumented-tests.md (merged into this unified flow)

---

## 1. Problem

Test case generation is currently a single-shot operation: run command, get tests, done. But real generation is iterative — you generate, review gaps, add edge cases, describe undocumented behaviors, and repeat until coverage is satisfactory.

Three separate features (smart count, gap suggestions, undocumented tests) need to work as one continuous flow, not as isolated commands.

## 2. Solution

A **generation session** that flows through four phases:

```
Phase 1: Analysis → count testable behaviors, recommend how many to generate
Phase 2: Generation → create test cases from documentation
Phase 3: Suggestions → propose additional tests for uncovered areas
Phase 4: User-described → create tests from tester's own descriptions
Phases 3-4 loop until user is done
```

---

## 3. Interactive Flow (Direct CLI Usage)

### Phase 1: Analysis

```bash
spectra ai generate --suite checkout
```

```
  ◐ Analyzing checkout documentation...
  
  Found 3 documents (2,400 words):
    • checkout-flow.md
    • payment-methods.md
    • error-handling.md

  Identified 18 testable behaviors:
    • 8 happy path flows
    • 6 negative / error scenarios
    • 3 edge cases
    • 1 security check

  8 already covered by existing tests.
  Recommended: 10 new test cases for remaining behaviors.
  
  ◆ Generate all 10? (y/n/custom number)
```

### Phase 2: Generation

```
  ◐ Generating 10 test cases...
  ◐ Verifying against documentation (critic)...

  ✓ 8 grounded
  ⚠ 1 partial — written with grounding warning
  ✗ 1 hallucinated — rejected

  ✓ 9 tests written to tests/checkout/
```

### Phase 3: Gap Suggestions

```
  ℹ Suggested additional test cases based on gap analysis:
    1. Payment timeout after 30 seconds (edge case)
    2. Concurrent checkout from two browser sessions (edge case)
    3. Currency rounding with 3+ decimal places (boundary)

  ◆ What would you like to do?
    (a) Generate all 3 suggestions
    (b) Pick specific suggestions (e.g., "1,3")
    (c) Describe your own test case
    (d) Done — exit session
```

User picks (a) or (b):
```
  ◐ Generating 3 additional test cases...
  ✓ 3 tests written to tests/checkout/
  
  [loops back to gap check — if more gaps, show suggestions again]
```

### Phase 4: User-Described Test Case

User picks (c):
```
  ◆ Describe the behavior you want to test:
  > When user enters invalid IBAN, show inline error and disable Submit

  ◆ Any additional context? (Enter to skip)
  > Bank transfer payment method only, checkout page

  ◐ Creating test case from your description...

  Draft:
  ────────────────────────────────────────
  id: TC-215
  priority: high
  tags: [payments, validation, iban]
  component: checkout
  source_refs: []
  grounding:
    verdict: manual
    source: user-described

  # IBAN Validation on Payment Form

  ## Preconditions
  - User is logged in with items in cart
  - Checkout page, bank transfer payment method selected

  ## Steps
  1. In the IBAN field, enter an invalid IBAN (e.g., "DE123")
  2. Click outside the field or press Tab

  ## Expected Results
  - Inline error message appears below IBAN field
  - Submit button is disabled
  - Correcting to valid IBAN removes error and enables Submit
  ────────────────────────────────────────

  ◆ Save? (y/n/edit)
  
  ✓ TC-215 written to tests/checkout/
  
  [loops back to Phase 3 menu]
```

### Session Exit

User picks (d):
```
  Session summary:
    • 9 tests generated from documentation
    • 3 tests from suggestions
    • 1 test from your description
    • 13 total new tests in checkout suite
  
  Next steps:
    spectra ai analyze --coverage    # Check updated coverage
    spectra validate                 # Validate all tests
```

---

## 4. Non-Interactive CLI Arguments

Each phase can be triggered separately for SKILL/CI usage:

```bash
# Phase 1+2: Analyze and generate (default — no count = smart recommendation)
spectra ai generate --suite checkout --output-format json

# Phase 1+2: Generate specific count (skip analysis recommendation)
spectra ai generate --suite checkout --count 15 --output-format json

# Phase 3: Generate from last session's suggestions
spectra ai generate --suite checkout --from-suggestions --output-format json

# Phase 3: Generate specific suggestions by index
spectra ai generate --suite checkout --from-suggestions 1,3 --output-format json

# Phase 4: Create from description
spectra ai generate --suite checkout \
  --from-description "When user enters invalid IBAN, show error and disable Submit" \
  --context "Bank transfer payment method, checkout page" \
  --output-format json

# All phases at once (CI mode — no interaction)
spectra ai generate --suite checkout --auto-complete --output-format json
```

`--auto-complete` runs: analyze → generate all recommended → accept all suggestions → finalize. Zero prompts.

## 5. Session State

Generation session state is stored in `.spectra/session.json`:

```json
{
  "session_id": "gen-2026-04-04-103000",
  "suite": "checkout",
  "started_at": "2026-04-04T10:30:00Z",
  "expires_at": "2026-04-04T11:30:00Z",
  "analysis": {
    "total_behaviors": 18,
    "already_covered": 8,
    "breakdown": { "happy_path": 4, "negative": 3, "edge_case": 2, "security": 1 }
  },
  "generated": ["TC-201", "TC-202", "TC-203"],
  "suggestions": [
    { "index": 1, "title": "Payment timeout after 30s", "category": "edge_case", "status": "pending" },
    { "index": 2, "title": "Concurrent checkout sessions", "category": "edge_case", "status": "pending" }
  ],
  "user_described": ["TC-215"]
}
```

- Session expires after 1 hour or when a new session starts
- `--from-suggestions` reads the last session's suggestions
- `--from-description` reads the last session for suite context
- `--auto-complete` creates and consumes a session in one pass

## 6. Duplicate Detection

Before generating any test (from docs, suggestions, or user description):

1. Load existing tests in the suite via index
2. Compare titles and step descriptions using fuzzy matching
3. If >80% similarity found:
   - Interactive: "Similar test exists: TC-118 'Payment form validation'. Create anyway? (y/n/update)"
   - Non-interactive: include in JSON output as `duplicate_warning`, still create

## 7. Grounding Rules

| Source | Critic | Frontmatter |
|--------|--------|-------------|
| Documentation (Phase 2) | Full critic verification | `grounding.verdict: grounded/partial/hallucinated` |
| Suggestions (Phase 3) | Full critic verification | `grounding.verdict: grounded/partial/hallucinated` |
| User-described (Phase 4) | Skip critic | `grounding.verdict: manual`, `source: user-described` |

User-described tests skip critic because there's no documentation to verify against.

## 8. Documentation Updates

- Merge `docs/generation.md` to cover the full session flow
- Update `docs/cli-reference.md` with new flags: `--from-suggestions`, `--from-description`, `--context`, `--auto-complete`
- Update `docs/undocumented-tests.md` to reference Phase 4 of the session
- Update `README.md` to describe the iterative generation workflow

---

## 9. Spec-Kit Prompt

```
/speckit.specify Implement the SPECTRA generation session flow.

Read spec-kit/feature-generation-session.md for the complete design.

Combine analysis, generation, gap suggestions, and user-described tests
into one continuous generation session with phases.

Key deliverables:
- Phase 1: Analyze docs, count testable behaviors, recommend count
- Phase 2: Generate test cases with critic verification
- Phase 3: Suggest additional tests for uncovered areas, loop
- Phase 4: Create tests from user description, skip critic, loop
- Session state in .spectra/session.json (expires 1 hour)
- Non-interactive CLI args: --from-suggestions, --from-description,
  --context, --auto-complete
- --auto-complete for CI: runs all phases without prompts
- Duplicate detection with fuzzy matching before creation
- Interactive mode loops Phases 3-4 until user exits
- Session summary at exit with totals and next step hints

Tech: C# in Spectra.CLI, existing AI provider for analysis + generation,
session state as JSON file, fuzzy matching for duplicates,
System.CommandLine for new flags.
```
