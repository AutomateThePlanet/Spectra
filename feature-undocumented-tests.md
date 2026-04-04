# SPECTRA Feature Spec: Undocumented Behavior Test Cases

**Status:** Draft — ready for spec-kit cycle  
**Depends on:** Phase 1 (CLI), Generation Agent, MCP Data Tools  
**Affects:** Generation Agent prompt, Spectra.Core (frontmatter schema), Spectra.MCP (write tools)

---

## 1. Problem

SPECTRA generates test cases exclusively from documentation. But not everything is documented:

- A developer mentions a behavior in a Slack conversation
- A production incident reveals an untested edge case
- A tester knows from experience that a specific flow is fragile
- A business rule exists in tribal knowledge but not in docs
- A regulatory requirement was communicated verbally

Currently these scenarios cannot be captured in SPECTRA without first writing documentation — which often doesn't happen.

## 2. Solution

Enable testers to describe undocumented behaviors conversationally, and have the generation agent create properly structured test cases from those descriptions. The agent asks clarifying questions, checks for duplicates, and writes the test case in full SPECTRA format with special metadata marking it as user-described (not doc-generated).

This is a **conversational flow in the generation agent**, not a CLI command.

---

## 3. Flow

### 3.1 User initiates

The tester is in Copilot Chat (or any MCP client) with the generation agent and describes a behavior:

```
"We have an undocumented behavior — when a user enters an invalid 
IBAN in the payment form, the system should show an inline error 
and disable the Submit button until a valid IBAN is entered."
```

### 3.2 Agent asks clarifying questions

The agent gathers enough detail to create a complete test case:

```
Got it — I'll create a test case for IBAN validation. A few questions:

1. Which screen/module is this in? (e.g., checkout, account settings)
2. What does the inline error message say exactly? 
   Or should I leave it as "displays inline error message"?
3. Does the Submit button re-enable immediately when a valid IBAN 
   is entered, or does the user need to click somewhere first?
4. Are there preconditions? (e.g., user must be logged in, 
   have items in cart)
5. What priority? (high / medium / low)
6. Which suite should this go in? (e.g., payment-processing, checkout)
```

The agent should ask **only what's missing** — if the user's description is detailed enough, skip unnecessary questions.

### 3.3 Duplicate check

Before creating, the agent searches existing test cases:

```
Checking existing tests...

Found 1 similar test case:
  TC-118: "Payment form validation — empty fields" (payment-processing)
  
This covers empty field validation but NOT IBAN-specific validation.
Creating a new test case for IBAN validation.
```

If an exact duplicate exists:
```
Found existing test case that covers this:
  TC-122: "IBAN validation on payment form" (payment-processing)

Would you like to:
(1) Update TC-122 with your additional details
(2) Create a new separate test case anyway
(3) Cancel — existing coverage is sufficient
```

### 3.4 Generate and review

The agent creates the test case and shows it for review:

```
Here's the draft test case:

---
id: TC-205
priority: high
type: manual
tags: [payments, validation, iban]
component: payment-processing
source_refs: []
grounding:
  verdict: manual
  source: user-described
  created_by: angelovstanton
  note: "IBAN validation behavior — not yet documented"
---

# IBAN Validation on Payment Form

## Preconditions
- User is logged in
- User has items in cart and is on the checkout payment step

## Steps
1. Navigate to checkout payment form
2. Select "Bank Transfer" as payment method
3. In the IBAN field, enter an invalid IBAN (e.g., "DE123")
4. Click outside the IBAN field (blur event)

## Expected Results
- Inline error message appears below the IBAN field
- Submit button is disabled (grayed out)
- When user corrects the IBAN to a valid value, the error 
  disappears and Submit button re-enables immediately

## Validation Checks
- Verify error message is visible and descriptive
- Verify Submit button state changes correctly
- Verify no form submission is possible with invalid IBAN

---

Does this look correct? I can adjust before saving.
```

### 3.5 Save

After user confirms:

```
✓ TC-205 written to tests/payment-processing/iban-validation.md
✓ Index updated

ℹ This test has no documentation source (source_refs: []).
  Consider updating payment documentation to include IBAN validation rules.
```

---

## 4. Frontmatter Schema Extension

### 4.1 Grounding metadata for manual tests

```yaml
grounding:
  verdict: manual
  source: user-described
  created_by: "username"
  note: "Brief explanation of why this isn't documented"
```

| Field | Type | Description |
|---|---|---|
| `verdict` | string | `"manual"` — indicates this test was created from user description, not generated from docs |
| `source` | string | `"user-described"` — distinguishes from `"doc-generated"` or `"imported"` |
| `created_by` | string | Username/identity of the person who described the behavior |
| `note` | string | Optional explanation — why this isn't documented, where the knowledge came from |

### 4.2 Validation rules

- Tests with `grounding.verdict: "manual"` are **excluded from critic verification** — there's no documentation to verify against
- Tests with empty `source_refs: []` are valid — validation should NOT flag them as errors
- Coverage analysis should report these separately: "15 test cases without documentation source"

---

## 5. Generation Agent Prompt

Add to `.github/agents/spectra-generation.agent.md`:

```markdown
## Creating Test Cases from Undocumented Behavior

When a user describes a behavior that is NOT in the documentation 
and asks for a test case:

### Step 1: Understand the behavior
Ask clarifying questions — but only what's missing:
- Which screen/module/component?
- What are the exact steps to trigger it?
- What is the expected result?
- Are there preconditions or required test data?
- What priority? (high/medium/low)
- Which suite should this go in?

If the user's description is detailed enough, skip unnecessary questions.

### Step 2: Check for duplicates
Search existing test cases for similar scenarios using find_test_cases 
or by reading the suite index. If similar exists:
- Show the existing test
- Ask: "Update this one or create new?"

### Step 3: Generate the test case
Create in standard SPECTRA format with:
- Proper YAML frontmatter (id, priority, type, tags, component)
- Markdown body with preconditions, steps, expected results
- source_refs: [] (empty — no documentation source)
- grounding.verdict: "manual"
- grounding.source: "user-described"
- grounding.created_by: current user identity
- grounding.note: brief context about why this isn't documented

### Step 4: Review
Show the complete draft to the user. Wait for confirmation 
before writing to disk.

### Step 5: Save
Write to the correct suite folder via MCP tools.
Update the suite index.
Print reminder: "This test has no documentation source. 
Consider updating docs to include this behavior."

### When NOT to use this flow
- If the user says "generate tests from docs" → use normal generation
- If the user provides a document or URL → use doc-based generation
- Only use this flow when the user explicitly describes behavior 
  that isn't documented
```

---

## 6. Coverage Analysis Impact

### 6.1 New coverage metric

Add to coverage analysis output:

```
Documentation Coverage:
  Documented tests:     43 (74.1%)
  Undocumented tests:   15 (25.9%)  ← tests with empty source_refs
  
  ⚠ 15 test cases have no documentation source.
    These may indicate documentation gaps.
```

### 6.2 Dashboard display

In the Coverage tab, show undocumented tests as a separate category:
- Color: orange (not red — they have test coverage, just no doc linkage)
- Tooltip: "Test created from user description — no documentation source"
- Filterable: user can toggle visibility of undocumented tests

---

## 7. Documentation Updates

### 7.1 New: `docs/undocumented-tests.md`

Guide covering:
- What undocumented test cases are and when to create them
- How to describe a behavior to the generation agent
- How the duplicate check works
- How undocumented tests appear in coverage analysis
- Best practice: create a doc ticket when adding undocumented tests
- Example conversation showing the full flow

### 7.2 Update: `docs/test-format.md`

Add the `grounding.verdict: "manual"` metadata to the frontmatter schema reference. Document `source: "user-described"` as a valid grounding source.

### 7.3 Update: `docs/coverage.md`

Add the "Undocumented tests" metric to the coverage analysis documentation. Explain how empty `source_refs` affects coverage percentages.

### 7.4 Update: `docs/generation-profiles.md`

Note that profiles do NOT affect undocumented test creation — the user's description takes priority over profile preferences.

### 7.5 Update: `README.md`

In Key Features, update or add:
```markdown
### 📝 Capture Undocumented Behaviors  
Describe any behavior conversationally and the generation agent creates 
a properly structured test case — even when documentation doesn't exist yet.
Undocumented tests are tracked separately in coverage analysis.
```

---

## 8. Spec-Kit Prompt

```
/speckit.specify Add ability to create test cases from undocumented 
behavior descriptions via the generation agent.

Read spec-kit/feature-undocumented-tests.md for the complete design.

When a user describes a behavior that is NOT in documentation, the 
generation agent asks clarifying questions, checks for duplicates, 
and creates a properly structured test case with special metadata 
marking it as user-described.

Key deliverables:
- Conversational flow in generation agent for capturing undocumented behaviors
- Clarifying questions (only ask what's missing)
- Duplicate check against existing test cases
- Draft review before saving
- Frontmatter extension: grounding.verdict "manual", source "user-described"
- Tests with verdict "manual" skip critic verification
- Empty source_refs is valid — no validation error
- Coverage analysis: separate "undocumented tests" metric
- Dashboard: orange category for undocumented tests
- docs/undocumented-tests.md usage guide
- Updates to test-format.md, coverage.md, README.md

Tech: Generation agent prompt updates, Spectra.Core frontmatter schema 
validation update (allow empty source_refs + manual verdict), coverage 
analyzer update (separate undocumented metric), dashboard HTML generator 
update (orange category).
```
