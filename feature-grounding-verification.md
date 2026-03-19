# SPECTRA Feature Spec: Grounding Verification Pipeline (Critic Flow)

**Status:** Draft — ready for spec-kit cycle
**Depends on:** Phase 1 (CLI), Provider Chain

---

## 1. Problem

When an AI model generates test cases from documentation, it can hallucinate — invent steps, expected results, field names, or business rules that don't exist in the source documentation. Self-review by the same model has confirmation bias: it tends to validate its own hallucinations.

## 2. Solution: Dual-Model Critic Flow

Every generation run includes an automatic verification pass by a **different model** than the one that generated the tests. This works like independent QA — the developer doesn't test their own code.

```
Step 1: GENERATOR (e.g., Claude Sonnet / Copilot GPT-5)
  Input:  source docs + profile + SKILL.md
  Output: draft test cases

Step 2: CRITIC (e.g., Gemini Flash / GPT-4o-mini)
  Input:  draft test cases + SAME source docs (not the generator's reasoning)
  Output: verdict per test case
```

The critic receives each generated test alongside the original documentation and answers specific questions about every step and expected result: can it be traced to documentation, are there invented details, are there undocumented assumptions.

## 3. Verdicts

Three levels per test case:

| Verdict        | Meaning                                              | Action                        |
| -------------- | ---------------------------------------------------- | ----------------------------- |
| `grounded`     | All steps and expected results trace to documentation | Written to disk as-is         |
| `partial`      | Some steps are OK but there are assumptions           | Written with warning marker   |
| `hallucinated` | Contains invented behaviors or undocumented claims    | Rejected, not written to disk |

## 4. CLI Integration

The critic runs automatically as part of `spectra ai generate`. No separate command needed.

### Direct Mode

```bash
$ spectra ai generate --suite checkout --focus "payment validation"
```

```
  ◐ Generating tests...
  ◐ Verifying against documentation (Gemini Flash)...

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

### Interactive Mode

Same flow — critic runs automatically after generation, results shown inline.

### Skip Critic

For speed or when documentation is incomplete:

```bash
$ spectra ai generate --suite checkout --skip-critic
```

## 5. Critic Model Selection

The critic doesn't need to be creative — it needs to be precise and cheap. The task is NLI (natural language inference): "does claim X follow from document Y?"

Recommended defaults:

| Role       | Model                              | Why                              |
| ---------- | ---------------------------------- | -------------------------------- |
| Generator  | Claude Sonnet/Opus, Copilot GPT-5  | Powerful, creative, good at generation |
| Critic     | Gemini 2.0 Flash, GPT-4o-mini     | Cheap, fast, precise for verification  |

Cost of the critic pass is minimal — each verification sends a short test case + the relevant documentation section, not the entire doc base.

### Configuration

```json
{
  "ai": {
    "providers": [
      { "name": "copilot", "model": "gpt-5", "priority": 1 },
      { "name": "anthropic", "model": "claude-sonnet-4-5", "api_key_env": "ANTHROPIC_API_KEY", "priority": 2 }
    ],
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash",
      "api_key_env": "GOOGLE_API_KEY"
    },
    "fallback_strategy": "auto"
  }
}
```

If `critic.enabled` is false or no critic provider is configured, generation works without verification (current behavior).

## 6. Critic Prompt

The critic receives a structured prompt for each test case:

```
You are a documentation grounding verifier. You receive a test case
and the source documentation it was generated from.

For each step and expected result in the test case, answer:
1. Can this claim be directly traced to the provided documentation?
2. Are there specific details (numbers, field names, behaviors) that
   are NOT in the documentation?
3. Are there assumptions about system behavior that the documentation
   does not confirm?

Respond with a JSON verdict:
{
  "verdict": "grounded" | "partial" | "hallucinated",
  "score": 0.0-1.0,
  "findings": [
    {
      "element": "Step 3" | "Expected Result" | "Precondition",
      "claim": "the specific claim being checked",
      "status": "grounded" | "unverified" | "hallucinated",
      "evidence": "quote or reference from documentation" | null,
      "reason": "why this is unverified/hallucinated" | null
    }
  ]
}
```

## 7. Grounding Metadata in Test Frontmatter

Verified tests include grounding information in their frontmatter:

```yaml
---
id: TC-102
priority: high
type: manual
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
```

For partial verdicts:

```yaml
grounding:
  verdict: partial
  score: 0.72
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T14:30:00Z
  unverified_claims:
    - "Step 3: assumes refund email sent within 5 minutes — not confirmed in docs"
    - "Expected Result: specific error code E-4012 not found in documentation"
```

## 8. How It Fits in the Pipeline

```
spectra ai generate
  │
  ├── 1. Load profile + docs + existing index (same as now)
  ├── 2. Generator model creates draft tests (same as now)
  ├── 3. NEW: Critic model verifies each test against source docs
  ├── 4. Grounded tests → written to disk with grounding metadata
  ├── 5. Partial tests → written with warning in grounding metadata
  ├── 6. Hallucinated tests → rejected, shown in CLI output
  ├── 7. Index rebuilt
  └── 8. Gap analysis shown (same as now)
```

The critic step adds ~10-20 seconds to a batch of 20 tests (parallel verification calls to a fast model). The cost is negligible — typically under $0.01 per batch.

## 9. Spec-Kit Prompt

```
/speckit.specify Add grounding verification pipeline to SPECTRA test generation.

Read spec-kit/feature-grounding-verification.md for the complete design.

After AI generates test cases, a second "critic" model automatically verifies
each test against the source documentation. This prevents hallucinated test steps
and expected results from reaching the test suite.

Key deliverables:
- Critic step integrated into spectra ai generate (runs automatically after generation)
- Configurable critic provider in spectra.config.json under ai.critic
- Three verdicts: grounded (written), partial (written with warning), hallucinated (rejected)
- Grounding metadata in test frontmatter (verdict, score, generator, critic, unverified_claims)
- --skip-critic flag to bypass verification
- Critic prompt that checks each step/expected result against source docs
- CLI output shows verification results with ✓ ⚠ ✗ symbols

Tech: C# in Spectra.CLI, separate CopilotSession for critic model (or direct API call
for non-Copilot providers), existing AgentRuntime for generator, Spectre.Console for output.
```
