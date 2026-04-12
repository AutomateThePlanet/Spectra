---
title: AI Models & Cost Guide
parent: User Guide
nav_order: 11
---

# AI Models & Cost Guide

Choosing models, understanding costs, and optimizing token usage for test generation.

Related: [Configuration](configuration.md) | [Grounding Verification](grounding-verification.md) | [Customization](customization.md)

---

## Overview

SPECTRA uses two AI models per run: a **generator** (behavior analysis + test
creation) and a **critic** (grounding verification). Choosing the right
combination determines quality, speed, and cost. This guide is based on real
production data — 1,261 test cases generated across 7 suites for $0.00.

---

## GitHub Copilot Model Reference

All models are accessed through the `github-models` provider via the Copilot
SDK. Premium request (PR) multipliers determine how much each call costs
against your monthly allowance.

### Included Models (0× multiplier — unlimited)

| Model | Config value | Best for |
|-------|-------------|----------|
| GPT-4.1 | `gpt-4.1` | Generation, critic, general purpose |
| GPT-4o | `gpt-4o` | Legacy (being deprecated) |
| GPT-5 mini | `gpt-5-mini` | Fast critic, light tasks |

These models consume **zero** premium requests on any paid Copilot plan.

### Premium Models (consume PRs from monthly allowance)

| Model | Config value | Multiplier | Pro+ (1,500 PRs) |
|-------|-------------|-----------|-------------------|
| Claude Sonnet 4.5 | `claude-sonnet-4.5` | 1× | 1,500 calls |
| Claude Sonnet 4.6 | `claude-sonnet-4.6` | 1× | 1,500 calls |
| Claude Haiku 4.5 | `claude-haiku-4.5` | 0.33× | ~4,500 calls |
| GPT-5 | `gpt-5` | 1× | 1,500 calls |
| Claude Opus 4.5 | `claude-opus-4.5` | 3× | 500 calls |
| Claude Opus 4.6 | `claude-opus-4.6` | 3× | 500 calls |

### Copilot Plans

| Plan | Price | Monthly PRs | Included models |
|------|-------|-------------|-----------------|
| Copilot Pro | $10/mo | 300 | GPT-4.1, GPT-4o, GPT-5 mini |
| Copilot Pro+ | $39/mo | 1,500 | GPT-4.1, GPT-4o, GPT-5 mini |
| Overage | $0.04/PR | Unlimited | On-demand after allowance |

> **Student Plan**: Since March 12, 2026, Claude Sonnet, Claude Opus, and
> GPT-5.4 are removed from self-selection on the Student plan. Only Auto mode
> provides access to Anthropic models. Upgrade to Pro or Pro+ for direct
> Sonnet access.

---

## Recommended Presets

The critic should always be a **different model** from the generator for
independent hallucination detection (see [Grounding Verification](grounding-verification.md)).

### Preset 1: Best Quality (Recommended)

Sonnet generator + GPT-4.1 critic. Deep behavior analysis, cross-family
verification, zero critic cost.

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "claude-sonnet-4.5", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-4.1"
    }
  }
}
```

Cost per `--count 20` run: ~4 PRs (analysis + generation batches). Critic is
free. Real-world result: 1,261 tests across 7 suites = 75 PRs total.

### Preset 2: Zero Cost

GPT-4.1 generator + GPT-5 mini critic. Both unlimited. Good for 80% of use
cases but shallower behavior analysis (~40 behaviors vs ~200 with Sonnet).

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "gpt-4.1", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-5-mini"
    }
  }
}
```

Cost: $0 always. Unlimited tests/month.

### Preset 3: Budget Cross-Family

GPT-4.1 generator + Haiku critic. Free generation with cross-family
verification at 0.33× per critic call.

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "gpt-4.1", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "claude-haiku-4.5"
    }
  }
}
```

Cost per `--count 20` run: ~7 PRs (critic only). Generation is free.

---

## Real Production Run Data

Actual results from a full production run. Generator: Claude Sonnet 4.5. Critic:
GPT-4.1 (parallel, `max_concurrent: 5`). Both via `github-models` provider
on Copilot Pro+. Some suites were regenerated multiple times during testing.

### Run Results

| Suite | Tests Generated | Gen Time | Critic Time | Total | PRs Used |
|-------|----------------|----------|-------------|-------|----------|
| Standard Calculator | 238 | 22m26s | 23m02s | 46m19s | 13 |
| Unit Converter | 181 | 18m34s | 17m58s | 37m20s | 11 |
| Date Calculation | 398 (2 runs) | 36m07s | 43m08s | 47m49s | 23 |
| General App Features | 100 | 12m49s | 10m22s | 23m37s | 7 |
| Scientific Calculator | 135 | 11m31s | 13m19s | 18m15s | 8 |
| Programmer Calculator | 117 | 12m20s | 14m57s | 16m02s | 7 |
| Graphing Calculator | 92 | 11m06s | 10m08s | 13m44s | 6 |
| **Total** | **1,261** | **~2h05m** | **~2h13m** | **~3h23m** | **~75** |

### Token Consumption

| Suite | Input Tokens | Output Tokens | Total |
|-------|-------------|--------------|-------|
| Standard Calculator | 5,898,939 | 184,274 | 6,083,213 |
| Unit Converter | 4,157,801 | 164,090 | 4,321,891 |
| Date Calculation | 9,191,320 | 341,179 | 9,532,499 |
| General App Features | 2,447,233 | 101,976 | 2,549,209 |
| Scientific Calculator | 3,319,320 | 101,543 | 3,420,863 |
| Programmer Calculator | 2,819,811 | 111,662 | 2,931,473 |
| Graphing Calculator | 2,376,626 | 85,621 | 2,462,247 |
| **Total** | **30,211,050** | **1,090,345** | **31,301,395** |

### Per-Phase Timing

| Phase | Avg per call | Notes |
|-------|-------------|-------|
| Analysis (Sonnet) | 25–148s | Varies by doc complexity. Sonnet finds 200+ behaviors; GPT-4.1 finds ~40 |
| Generation batch (Sonnet, 20 tests) | ~110s | ~5.5s per test |
| Critic call (GPT-4.1, parallel ×5) | ~6s per call, ~1.2s effective | 5 concurrent calls reduces wall time by ~80% |

---

## Cost Comparison

### Full workload: 1,261 tests across 7 suites

| Provider | Input Cost | Output Cost | Total |
|----------|-----------|-------------|-------|
| **Copilot Pro+ (github-models)** | included | included | **$0.00** (~75 of 1,500 PRs) |
| Copilot Pro overage ($0.04/PR) | — | — | **$3.00** |
| Azure AI Foundry (Sonnet 4.5) | $90.63 | $16.36 | **$106.99** |
| Anthropic API direct | $90.63 | $16.36 | **$106.99** |

### Full monthly capacity at Pro+ (1,500 PRs)

| Provider | Monthly Cost |
|----------|-------------|
| **Copilot Pro+** | **$39** (subscription) |
| Azure AI Foundry equivalent | **~$2,169** |
| Copilot overage equivalent | **$60** (1,500 × $0.04) |

### Premium Request Budget

After generating 1,261 tests across all 7 suites (within a single billing cycle):

| Metric | Value |
|--------|-------|
| PRs consumed (total account) | 191.52 of 1,500 |
| PRs from SPECTRA runs | ~75 (Sonnet generation + analysis only) |
| PRs from VS Code / other usage | ~116 |
| PRs remaining | 1,308 (19 days left in cycle) |
| Billed amount | $0.00 |

> The 55× price difference between Copilot Pro+ and Azure pay-per-token exists
> because Copilot is a subscription model — Microsoft subsidizes heavy users
> with revenue from lighter users. SPECTRA's workload (hundreds of structured
> API calls with large system prompts) is unusually token-intensive for a
> consumer subscription.

---

## Batch Size & Timeout Tuning

Different models require different batch sizes and timeouts. Match your config
to the model's speed characteristics.

| Model | Recommended batch_size | analysis_timeout | generation_timeout |
|-------|----------------------|------------------|-------------------|
| GPT-4.1 | 20–30 | 3 min | 5 min |
| Claude Sonnet 4.5 | 20 | 3 min | 5 min |
| DeepSeek-V3.2 | 8 | 10 min | 20 min |
| GPT-4o-mini | 20–30 | 2 min | 3 min |

```json
{
  "ai": {
    "analysis_timeout_minutes": 3,
    "generation_timeout_minutes": 5,
    "generation_batch_size": 20
  }
}
```

---

## Quality Comparison: Sonnet vs GPT-4.1

Based on the same documentation (Standard Calculator suite):

| Metric | Claude Sonnet 4.5 | GPT-4.1 |
|--------|------------------|---------|
| Behaviors discovered | ~200–238 | ~39–40 |
| Analysis depth | Deep edge cases, implicit rules | Surface-level, explicit rules |
| BVA exact boundaries | Specific values | Sometimes generic |
| Decision table combinations | 4+ conditions | 2–3 conditions |
| State transition chains | 5+ states | 2–3 states |
| Step specificity | Concrete actions, exact data | More generic phrasing |
| Expected result detail | Specific error messages | General outcomes |

For simple CRUD documentation the difference is minimal. For complex business
logic with implicit rules, Sonnet produces significantly more thorough coverage.

---

## Debug Log & Monitoring

Enable debug logging to track token usage and timing per call:

```json
{
  "debug": {
    "enabled": true,
    "mode": "append"
  }
}
```

Each AI call is logged with model, provider, tokens, and elapsed time:

```
[generate] BATCH OK requested=20 elapsed=113.9s model=claude-sonnet-4.5 provider=github-models tokens_in=174233 tokens_out=7618
[critic  ] CRITIC OK test_id=TC-100 verdict=Partial score=0.80 elapsed=8.9s model=gpt-4.1 provider=github-models tokens_in=13056 tokens_out=429
```

Every run ends with a summary line:

```
[summary ] RUN TOTAL command=generate suite=standard calculator calls=250 tokens_in=5898939 tokens_out=184274 elapsed=46m19s phases=generation:12/22m26s,critic:238/23m02s
```

Use `--verbosity diagnostic` to force-enable debug for a single run without
changing the config.

---

## Overage Budget Setup

If you exhaust your monthly PRs and want to continue with premium models,
enable overage billing in GitHub Settings:

1. Go to **GitHub Settings → Billing and licensing → Budgets and alerts**
2. Set a budget for premium request overages (e.g., $10/month)
3. Additional PRs are billed at **$0.04 each**

Accounts created before August 22, 2025 have a default $0 budget — overages
are blocked unless you explicitly set a budget. Without a budget, you fall
back to included models (GPT-4.1, GPT-4o, GPT-5 mini) when your allowance
runs out.

---

## Migration from Azure / BYOK

If you're moving from Azure-hosted models to GitHub Models:

**Before (Azure OpenAI / Azure Anthropic):**

```json
{
  "ai": {
    "providers": [
      {
        "name": "azure-openai",
        "model": "DeepSeek-V3.2",
        "api_key_env": "AZURE_API_KEY",
        "base_url": "https://your-endpoint.azure.com/"
      }
    ],
    "analysis_timeout_minutes": 10,
    "generation_timeout_minutes": 20,
    "generation_batch_size": 8
  }
}
```

**After (GitHub Models via Copilot Pro+):**

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "claude-sonnet-4.5", "enabled": true }
    ],
    "analysis_timeout_minutes": 3,
    "generation_timeout_minutes": 5,
    "generation_batch_size": 20,
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-4.1"
    }
  }
}
```

Key changes: remove `api_key_env` and `base_url` (GitHub Models uses
`gh auth token`), reduce timeouts (faster models), increase batch size
(no timeout risk), switch critic to a different model family.

Authenticate with `gh auth login` and verify with `spectra auth`.
