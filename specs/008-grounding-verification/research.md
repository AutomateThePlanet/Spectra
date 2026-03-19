# Research: Grounding Verification Pipeline

**Feature**: 008-grounding-verification
**Date**: 2026-03-19
**Status**: Complete

## Overview

This document captures research decisions for implementing the grounding verification pipeline in SPECTRA test generation.

---

## Research Question 1: Critic Model Integration

**Question**: How should we integrate a second AI model call without duplicating the existing agent infrastructure?

### Options Evaluated

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A. Extend IAgentRuntime | Add `VerifyTestAsync` to existing interface | Reuses infrastructure | Conflates generation and verification concerns |
| B. Separate ICriticRuntime | New interface for critic functionality | Clean separation, focused | New interface to maintain |
| C. Generic IModelRuntime | Single interface for all model calls | Maximum reuse | Over-abstraction |

### Analysis

The existing `IAgentRuntime` interface is designed specifically for test generation:

```csharp
public interface IAgentRuntime
{
    Task<GenerationResult> GenerateTestsAsync(...);
    Task<bool> IsAvailableAsync(...);
    string ProviderName { get; }
}
```

Verification has different:
- **Input shape**: Single test + source docs (not a prompt for generation)
- **Output shape**: Verdict/findings (not test cases)
- **Model selection criteria**: Cheap/fast models, not creative ones

**Decision**: **Option B - Separate ICriticRuntime interface**

```csharp
public interface ICriticRuntime
{
    Task<VerificationResult> VerifyTestAsync(
        TestCase test,
        IReadOnlyList<SourceDocument> relevantDocs,
        CancellationToken ct = default);

    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    string ModelName { get; }
}
```

**Rationale**: Clean separation between generation and verification responsibilities. Different model selection criteria. Allows critic-specific optimizations without affecting generation code.

---

## Research Question 2: Batch vs Sequential Verification

**Question**: Should verification run per-test or in batches?

### Options Evaluated

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| A. Sequential (1 test/call) | Verify each test individually | Clear findings per test, simple | More API calls |
| B. Small batch (5 tests/call) | Verify 5 tests together | Fewer calls, cheaper | Complex response parsing |
| C. Full batch (all tests/call) | Single call for all | Minimum calls | Very large prompts, attribution issues |

### Analysis

**Performance Consideration** (SC-001):
- Target: <2 seconds per test average
- Gemini Flash latency: ~500-800ms per call
- Sequential: 20 tests × 0.8s = 16s (acceptable)
- Batch-5: 4 calls × 1.2s = 4.8s (faster, but complex parsing)

**Accuracy Consideration** (SC-002):
- Per-test verification allows focused analysis
- Batched verification may miss cross-test issues but risks conflation

**Cost Consideration**:
- Gemini Flash: ~$0.0001 per 1K tokens
- 20 tests: ~40K input tokens total
- Cost difference: negligible ($0.004 vs $0.003)

**Decision**: **Option A - Sequential verification**

**Rationale**: Simpler implementation, clearer findings attribution, acceptable performance. Can add batching as optimization later if needed.

---

## Research Question 3: Provider Support

**Question**: Which AI providers should support critic functionality?

### Options Evaluated

| Provider | Model | Cost (per 1M tokens) | Latency | NLI Capability |
|----------|-------|----------------------|---------|----------------|
| Google | Gemini 2.0 Flash | $0.075 input / $0.30 output | ~500ms | Excellent |
| OpenAI | GPT-4o-mini | $0.15 input / $0.60 output | ~800ms | Excellent |
| Anthropic | Claude 3.5 Haiku | $0.25 input / $1.25 output | ~600ms | Excellent |
| GitHub | gpt-4o-mini (via GitHub) | Free tier available | ~800ms | Excellent |

### Analysis

All models are capable of NLI (Natural Language Inference) tasks required for grounding verification. The primary differentiators are:

1. **Cost**: Gemini Flash is cheapest
2. **Availability**: GitHub Models free for GitHub users
3. **Integration**: OpenAI/Anthropic already have agent implementations

**Decision**: **Support all four providers**

Implementation order:
1. Google Gemini Flash (primary recommendation)
2. OpenAI GPT-4o-mini (alternative)
3. GitHub Models (free option)
4. Anthropic Claude Haiku (premium alternative)

**Rationale**: Users have different API access and cost constraints. Reuse existing provider SDKs where possible.

---

## Research Question 4: Grounding Metadata Schema

**Question**: What fields should be included in grounding metadata?

### Requirements Analysis

From spec FR-014 through FR-017:
- FR-014: `grounding.generator` - model that created the test
- FR-015: `grounding.critic` - model that verified the test
- FR-016: `grounding.verified_at` - timestamp of verification
- FR-017: `grounding.score` - confidence value 0.0-1.0

Additional from User Story 5:
- `grounding.verdict` - grounded/partial/hallucinated
- `grounding.unverified_claims` - for partial verdicts

### Schema Design

```yaml
grounding:
  verdict: grounded | partial | hallucinated
  score: 0.0-1.0
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T14:30:00Z
  unverified_claims: []  # Only for partial verdicts
```

**Decision**: **Adopt schema from feature design document**

**Rationale**: Matches existing frontmatter patterns, all required fields per spec, extensible for future needs.

---

## Research Question 5: CLI Flag Handling

**Question**: How should `--skip-critic` interact with other flags?

### Options Evaluated

| Scenario | Behavior |
|----------|----------|
| No critic config, no flag | No verification (backward compatible) |
| Critic configured, no flag | Verification enabled |
| Critic configured, --skip-critic | Verification skipped |
| --no-interaction, critic configured | Verification enabled (CI mode) |
| --dry-run, critic configured | Verification runs but no files written |

### Analysis

Flags are orthogonal:
- `--skip-critic`: Bypasses verification step entirely
- `--no-interaction`: Runs without prompts (CI mode)
- `--dry-run`: Preview mode, no file writes

**Decision**: **Flags are independent**

```csharp
// In GenerateHandler
if (!skipCritic && config.Ai.Critic?.Enabled == true)
{
    // Run verification
}
```

**Rationale**: Simple boolean checks, no complex flag interactions, each flag controls one aspect.

---

## Research Question 6: Error Handling Strategy

**Question**: How should the system handle critic failures?

### Failure Scenarios

| Scenario | Proposed Behavior |
|----------|-------------------|
| Critic API unavailable | Warn user, offer to proceed without verification |
| Authentication failure | Clear error message, suggest config fix |
| Rate limit exceeded | Warn, write unverified tests, suggest retry |
| Malformed response | Log warning, treat as unverified |
| Timeout | Retry once, then treat as unverified |

### Decision

**Graceful degradation with user choice** (per FR-019):

```csharp
try
{
    result = await critic.VerifyTestAsync(test, docs, ct);
}
catch (CriticUnavailableException ex)
{
    _progress.Warning($"Critic unavailable: {ex.Message}");

    if (!_noInteraction)
    {
        var proceed = AnsiConsole.Confirm("Proceed without verification?");
        if (!proceed) return ExitCodes.Error;
    }

    // Write tests without grounding metadata
}
```

**Rationale**: Never block the user from generating tests. Provide clear feedback on what happened.

---

## Technical Decisions Summary

| Decision Area | Choice | Key Rationale |
|--------------|--------|---------------|
| Interface Design | Separate ICriticRuntime | Clean separation of concerns |
| Verification Mode | Per-test sequential | Simple, clear attribution |
| Provider Support | Google, OpenAI, GitHub, Anthropic | User flexibility |
| Metadata Schema | Per design document | Spec compliance |
| Flag Handling | Independent flags | Orthogonal concerns |
| Error Handling | Graceful degradation | Never block user |

---

## References

- [Feature Design Document](../../feature-grounding-verification.md)
- [Specification](./spec.md)
- [Gemini API Documentation](https://ai.google.dev/gemini-api/docs)
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
