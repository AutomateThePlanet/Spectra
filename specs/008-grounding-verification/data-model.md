# Data Model: Grounding Verification Pipeline

**Feature**: 008-grounding-verification
**Date**: 2026-03-19
**Status**: Complete

## Overview

This document defines the data models for the grounding verification pipeline.

---

## Core Entities

### VerificationVerdict (Enum)

Classification result from the critic model.

```csharp
namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Verdict assigned by the critic model after verification.
/// </summary>
public enum VerificationVerdict
{
    /// <summary>
    /// All claims in the test are traceable to documentation.
    /// Test is written to disk as-is with grounding metadata.
    /// </summary>
    Grounded,

    /// <summary>
    /// Some claims are verified but others are unverified assumptions.
    /// Test is written with warning marker and unverified_claims list.
    /// </summary>
    Partial,

    /// <summary>
    /// Test contains invented behaviors or undocumented claims.
    /// Test is rejected and NOT written to disk.
    /// </summary>
    Hallucinated
}
```

---

### GroundingMetadata (Record)

Persistent record of verification result stored in test frontmatter.

```csharp
namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Grounding metadata written to test frontmatter after verification.
/// </summary>
public sealed record GroundingMetadata
{
    /// <summary>
    /// Verification verdict: grounded, partial, or hallucinated.
    /// </summary>
    public required VerificationVerdict Verdict { get; init; }

    /// <summary>
    /// Confidence score from critic model (0.0 to 1.0).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Model name that generated the test (e.g., "claude-sonnet-4-5").
    /// </summary>
    public required string Generator { get; init; }

    /// <summary>
    /// Model name that verified the test (e.g., "gemini-2.0-flash").
    /// </summary>
    public required string Critic { get; init; }

    /// <summary>
    /// Timestamp when verification was performed.
    /// </summary>
    public required DateTimeOffset VerifiedAt { get; init; }

    /// <summary>
    /// List of claims that could not be verified against documentation.
    /// Only populated for Partial verdicts.
    /// </summary>
    public IReadOnlyList<string> UnverifiedClaims { get; init; } = [];
}
```

**YAML Serialization**:

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

---

### CriticFinding (Record)

Individual assessment of a specific claim within a test.

```csharp
namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Assessment of a single claim within a test case.
/// </summary>
public sealed record CriticFinding
{
    /// <summary>
    /// What part of the test this finding applies to.
    /// Examples: "Step 3", "Expected Result", "Precondition"
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    /// The specific claim being checked.
    /// </summary>
    public required string Claim { get; init; }

    /// <summary>
    /// Status of this claim: grounded, unverified, or hallucinated.
    /// </summary>
    public required FindingStatus Status { get; init; }

    /// <summary>
    /// Quote or reference from documentation supporting the claim.
    /// Null if unverified or hallucinated.
    /// </summary>
    public string? Evidence { get; init; }

    /// <summary>
    /// Reason why the claim is unverified or hallucinated.
    /// Null if grounded.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Status of an individual finding.
/// </summary>
public enum FindingStatus
{
    /// <summary>
    /// Claim can be traced to documentation.
    /// </summary>
    Grounded,

    /// <summary>
    /// Claim cannot be verified but isn't clearly wrong.
    /// </summary>
    Unverified,

    /// <summary>
    /// Claim contradicts or invents beyond documentation.
    /// </summary>
    Hallucinated
}
```

---

### VerificationResult (Class)

Full response from critic model for a single test verification.

```csharp
namespace Spectra.Core.Models.Grounding;

/// <summary>
/// Complete verification result for a test case.
/// </summary>
public sealed class VerificationResult
{
    /// <summary>
    /// Overall verdict for the test.
    /// </summary>
    public required VerificationVerdict Verdict { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Individual findings for each claim in the test.
    /// </summary>
    public required IReadOnlyList<CriticFinding> Findings { get; init; }

    /// <summary>
    /// Model that performed the verification.
    /// </summary>
    public required string CriticModel { get; init; }

    /// <summary>
    /// Time taken for verification.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Any errors encountered during verification.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Whether verification completed successfully.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Convert to grounding metadata for frontmatter.
    /// </summary>
    public GroundingMetadata ToMetadata(string generatorModel) => new()
    {
        Verdict = Verdict,
        Score = Score,
        Generator = generatorModel,
        Critic = CriticModel,
        VerifiedAt = DateTimeOffset.UtcNow,
        UnverifiedClaims = Findings
            .Where(f => f.Status != FindingStatus.Grounded)
            .Select(f => $"{f.Element}: {f.Reason ?? f.Claim}")
            .ToList()
    };
}
```

---

### CriticConfig (Class)

Configuration for the critic model.

```csharp
namespace Spectra.Core.Models.Config;

/// <summary>
/// Configuration for the grounding verification critic.
/// </summary>
public sealed class CriticConfig
{
    /// <summary>
    /// Whether critic verification is enabled.
    /// Default: false (backward compatible).
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// Provider name: "google", "openai", "anthropic", "github".
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// Model name (e.g., "gemini-2.0-flash", "gpt-4o-mini").
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Environment variable containing the API key.
    /// </summary>
    [JsonPropertyName("api_key_env")]
    public string? ApiKeyEnv { get; init; }

    /// <summary>
    /// Optional base URL override for the API.
    /// </summary>
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Timeout for verification calls in seconds.
    /// Default: 30 seconds.
    /// </summary>
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 30;
}
```

**JSON Configuration**:

```json
{
  "ai": {
    "providers": [...],
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash",
      "api_key_env": "GOOGLE_API_KEY"
    }
  }
}
```

---

## Updated Entities

### AiConfig (Modified)

Add Critic property to existing AiConfig.

```csharp
namespace Spectra.Core.Models.Config;

public sealed class AiConfig
{
    [JsonPropertyName("providers")]
    public required IReadOnlyList<ProviderConfig> Providers { get; init; }

    [JsonPropertyName("fallback_strategy")]
    public string FallbackStrategy { get; init; } = "auto";

    // NEW: Critic configuration for grounding verification
    [JsonPropertyName("critic")]
    public CriticConfig? Critic { get; init; }
}
```

### TestCase (Modified)

Add optional GroundingMetadata property.

```csharp
namespace Spectra.Core.Models;

public sealed class TestCase
{
    // ... existing properties ...

    /// <summary>
    /// Grounding verification metadata (if verified).
    /// </summary>
    public GroundingMetadata? Grounding { get; init; }
}
```

---

## Entity Relationships

```
┌─────────────────┐
│   CriticConfig  │ ─── Configuration
└────────┬────────┘
         │ uses
         ▼
┌─────────────────┐         ┌─────────────────┐
│  ICriticRuntime │ ──────▶ │VerificationResult│
└────────┬────────┘         └────────┬────────┘
         │ produces                  │ contains
         │                           ▼
         │                  ┌─────────────────┐
         │                  │  CriticFinding  │ (0..n)
         │                  └─────────────────┘
         │
         │ converts to
         ▼
┌─────────────────┐
│GroundingMetadata│ ─── Written to TestCase frontmatter
└─────────────────┘
```

---

## Serialization Notes

### YAML Frontmatter

Grounding metadata is serialized as nested YAML:

```yaml
---
id: TC-102
priority: high
tags: [payments, negative]
grounding:
  verdict: grounded
  score: 0.94
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T14:30:00Z
  unverified_claims: []
---
```

### JSON (Critic Response)

The critic returns findings as JSON:

```json
{
  "verdict": "partial",
  "score": 0.72,
  "findings": [
    {
      "element": "Step 3",
      "claim": "User receives refund email within 5 minutes",
      "status": "unverified",
      "evidence": null,
      "reason": "No specific time mentioned in documentation"
    }
  ]
}
```

---

## Validation Rules

| Entity | Rule | Error Message |
|--------|------|---------------|
| GroundingMetadata | Score must be 0.0-1.0 | "Grounding score must be between 0.0 and 1.0" |
| GroundingMetadata | Generator/Critic non-empty | "Generator and critic model names are required" |
| CriticConfig | Provider must be valid | "Unknown critic provider: {provider}" |
| CriticConfig | Model required if enabled | "Critic model must be specified when enabled" |
| VerificationResult | Findings required | "Verification must include at least one finding" |
