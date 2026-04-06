# SPECTRA Feature Spec: Smart Test Count Recommendation

**Status:** Draft — ready for spec-kit cycle  
**Depends on:** Phase 1 (CLI), Generation Profile system  
**Affects:** Spectra.CLI (`spectra ai generate`), Generation Agent prompt

---

## 1. Problem

Currently `spectra ai generate --suite checkout --count 15` requires the user to specify how many test cases to generate. This leads to two issues:

- **Too few:** User asks for 5, but documentation describes 20 testable behaviors — gaps remain unnoticed
- **Too many:** User asks for 30, but there are only 12 distinct testable behaviors — AI pads with duplicates or low-value tests
- **Uninformed choice:** The user has no data to decide the right number

## 2. Solution

When `--count` is not specified, the AI analyzes the source documentation before generating and proposes a count with a breakdown by category. The user can accept, adjust, or specify a custom number.

When `--count` IS specified, behavior is unchanged — generate exactly that many.

---

## 3. CLI Behavior

### 3.1 With --count (unchanged)

```bash
spectra ai generate --suite checkout --count 15
```

Generates exactly 15 test cases. No analysis step. Current behavior preserved.

### 3.2 Without --count (new behavior — non-interactive)

```bash
spectra ai generate --suite checkout
```

```
  ◐ Analyzing checkout documentation...

  Found 3 documents (2,400 words total):
    • checkout-flow.md
    • payment-methods.md
    • error-handling.md

  Identified 18 testable behaviors:
    • 8 happy path flows
    • 6 negative / error scenarios
    • 3 edge cases / boundary conditions
    • 1 security / permission check

  Generating 18 test cases...
  ◐ Generating tests...
  ◐ Verifying against documentation (critic)...
  ✓ 18 tests written to tests/checkout/
```

In non-interactive mode (no TTY or `--no-interaction` flag), generate all identified testable behaviors automatically. Print the analysis before generating.

### 3.3 Without --count (new behavior — interactive mode)

```bash
spectra ai generate
```

```
  [after suite selection]
  ◐ Analyzing documentation...

  Identified 18 testable behaviors:
    • 8 happy path flows
    • 6 negative / error scenarios
    • 3 edge cases / boundary conditions
    • 1 security / permission check

  ◆ How many test cases to generate?
    (1) All 18 — full coverage of identified behaviors
    (2) 8 — happy paths only
    (3) 14 — happy paths + negative scenarios
    (4) Custom number
    (5) Let me describe what I want
```

User selects, generation proceeds.

### 3.4 Post-generation gap notification

After generating, if fewer tests were created than identified behaviors (e.g., user chose 8 out of 18):

```
  ✓ 8 tests written to tests/checkout/

  ℹ 10 testable behaviors not yet covered:
    • 6 negative / error scenarios
    • 3 edge cases
    • 1 security check

  Next steps:
    spectra ai generate --suite checkout    # Generate remaining tests
```

---

## 4. Analysis Step Implementation

### 4.1 How it works

The analysis step is a lightweight pre-scan before generation:

1. Load all source documents for the target suite (from `docs/` mapped via `source_refs` or document index)
2. Send document summaries (not full docs) to the AI provider with a structured prompt
3. AI returns categorized list of testable behaviors
4. Display the breakdown
5. Use the count for the generation step

### 4.2 Analysis prompt

```
Analyze the following documentation and identify all distinct testable 
behaviors. Categorize each behavior into one of these categories:

- HAPPY_PATH: Normal successful user flows
- NEGATIVE: Error handling, invalid inputs, failure scenarios
- EDGE_CASE: Boundary conditions, unusual combinations, limits
- SECURITY: Permission checks, access control, authentication
- PERFORMANCE: Load, timeout, concurrent access (if mentioned)

For each behavior, provide:
- category: one of the categories above
- title: short description (max 80 chars)
- source: which document it comes from

Return as JSON array. Count only DISTINCT testable behaviors — 
do not duplicate similar scenarios.

Documentation:
{document_summaries}
```

### 4.3 Response format

```json
{
  "total": 18,
  "breakdown": {
    "happy_path": 8,
    "negative": 6,
    "edge_case": 3,
    "security": 1
  },
  "behaviors": [
    {
      "category": "happy_path",
      "title": "Successful checkout with credit card",
      "source": "checkout-flow.md"
    },
    {
      "category": "negative", 
      "title": "Payment declined — insufficient funds",
      "source": "error-handling.md"
    }
  ]
}
```

### 4.4 Cost and performance

- Analysis uses the same AI provider as generation
- Send document summaries (title + first 200 chars per section), not full documents
- Typically one API call, ~500-1000 input tokens
- Adds ~3-5 seconds to the generation workflow
- Result is cached in the generation session — not stored permanently

---

## 5. Interaction with existing features

### 5.1 Existing tests dedup

Before proposing the count, check existing tests in the suite. If 10 out of 18 behaviors already have test cases:

```
  Identified 18 testable behaviors.
  8 already covered by existing tests.
  
  Recommended: 10 new test cases for remaining behaviors.
```

### 5.2 Generation profiles

The analysis respects the active profile. If the profile says "focus on negative scenarios", the breakdown reflects that weighting.

### 5.3 Grounding verification (critic)

The analysis step does NOT bypass the critic. After generating the recommended count, the critic still verifies each test against documentation.

### 5.4 --focus flag

```bash
spectra ai generate --suite checkout --focus "negative scenarios"
```

With `--focus`, the analysis still runs but generation prioritizes the focused category:

```
  Identified 18 testable behaviors.
  Focus: negative scenarios (6 identified)
  
  Generating 6 negative scenario test cases...
```

---

## 6. Generation Agent Prompt

In `.github/agents/spectra-generation.agent.md`, add section:

```markdown
## Smart Test Count

When asked to generate test cases without a specific count:

1. Analyze the source documentation for the target suite
2. Identify and categorize all distinct testable behaviors:
   - Happy path / positive flows
   - Negative / error scenarios
   - Edge cases / boundary conditions
   - Security / permission checks
3. Check existing tests for duplicates
4. Present the breakdown:
   "Found 18 testable behaviors (8 already covered):
    • 4 happy paths remaining
    • 6 negative scenarios remaining
    Recommend generating 10 test cases."
5. Ask: "Generate all 10, or adjust?"
6. After generation, show remaining gaps if any
```

---

## 7. Documentation Updates

### 7.1 Update: `docs/cli-reference.md`

Document the new behavior of `--count`:
- With `--count N`: generates exactly N (unchanged)
- Without `--count`: AI analyzes docs and recommends a count
- `--no-interaction`: auto-generates all identified behaviors

### 7.2 Update: `docs/generation-profiles.md`

Mention that profiles affect the analysis breakdown weighting.

### 7.3 Update: `README.md`

Update the AI Test Generation feature description to mention smart count recommendation.

---

## 8. Spec-Kit Prompt

```
/speckit.specify Add smart test count recommendation to spectra ai generate.

Read spec-kit/feature-smart-test-count.md for the complete design.

When --count is not specified, the AI analyzes source documentation 
and proposes how many test cases to generate, broken down by category 
(happy path, negative, edge cases, security). User can accept, adjust, 
or specify a custom number.

Key deliverables:
- Analysis step before generation (lightweight doc pre-scan)
- Categorized breakdown display in CLI output
- Interactive mode: let user choose count or category subset
- Non-interactive mode: auto-generate all identified behaviors
- Dedup check against existing tests before proposing count
- Post-generation gap notification
- --count flag preserves current behavior (no analysis)
- Update CLI reference and README documentation

Tech: C# in Spectra.CLI, existing AI provider for analysis call,
structured JSON response parsing, Spectre.Console for interactive display.
```
