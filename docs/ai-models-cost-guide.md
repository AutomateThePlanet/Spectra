---
title: AI Models & Cost Guide
parent: User Guide
nav_order: 11
---

# AI Models & Cost Guide

Understanding how SPECTRA spends AI usage since the Claude Code migration (v2), and how to
control it.

Related: [Configuration](configuration.md) | [Grounding Verification](grounding-verification.md) |
[Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md)

---

## The cost model changed completely in v2

Every pre-v2 version of this guide described a GitHub Copilot premium-request (PR) multiplier
table: SPECTRA ran its own AI SDK session, chose a provider/model from `ai.providers`, and billed
against your Copilot plan's monthly PR allowance.

**That entire billing surface is gone.** Spec 069 deleted the GitHub Copilot SDK, `ai.providers`,
`ai.critic` (as a config block), and `spectra auth` outright. SPECTRA no longer opens its own AI
session and makes no model calls of its own for generation, analysis, criteria extraction, or
updates. There is nothing left in `spectra.config.json` to route requests to a provider — legacy
configs that still carry `ai.providers`/`ai.critic` load cleanly, but the keys do nothing.

**All inference is your interactive Claude Code session.** SPECTRA's CLI only does the
deterministic bookkeeping either side of a model call:

```
spectra ai compile-prompt ...   →   you generate the answer, in-session   →   spectra ai ingest-tests ...
```

The middle step is an ordinary Claude Code turn (or, for grounding verification, a subagent call).
There is no separate "SPECTRA AI cost" anymore — it is exactly your normal Claude Code usage, and
it is billed, throttled, and reported the same way every other piece of work in your session is:
against your Claude subscription's usage limits (Pro / Max / Team / Enterprise), or against your
API/Console token balance if you run Claude Code with API billing. See
[Claude's pricing page](https://www.anthropic.com/pricing) for current plan and per-token rates —
SPECTRA has no visibility into or influence over those numbers.

---

## What actually makes a model call

| Seam | Command sequence | What runs the model |
|------|------------------|----------------------|
| Behavior analysis | `compile-analysis-prompt` → **turn** → `ingest-analysis` | Your session's active model |
| Test generation | `compile-prompt` → **turn** → `ingest-tests` | Your session's active model |
| From-description test | `compile-prompt --from-description` → **turn** → `ingest-tests` | Your session's active model |
| Criteria extraction | `docs changed` → `compile-extraction-prompt` → **turn** → `ingest-criteria` | Your session's active model |
| Targeted update | `compile-update-prompt` → **turn** → `ingest-update` | Your session's active model |
| Grounding verification | `compile-critic-prompt` → **`spectra-critic` subagent call** → `ingest-verdict` | The subagent's pinned model |

The first five rows all run as ordinary turns in whatever model your Claude Code session is
currently using — pick it the same way you'd pick a model for any other piece of work (`/model`,
or your plan's default). The batch size for generation is `ai.generation_batch_size` (default 30
tests per turn) — fewer, larger turns cost less overhead than many small ones, at the cost of a
longer single response.

The critic is different: it always runs as a `context: fork` subagent (`.claude/agents/spectra-critic.md`
in your project, seeded from SPECTRA's bundled copy), invoked once per generated test. `context:
fork` means each critic call starts from a clean, isolated context (just the test artifact + its
source docs) — it doesn't grow your main session's context window, but each call still counts as
normal Claude Code usage. The critic's model is a static `model:` field in that agent file (shipped
as `claude-sonnet-4-6`); there is no `spectra.config.json` knob for it anymore — edit the agent
file directly (and re-apply your edit after `spectra update-skills` if you've customized it).

---

## Choosing a session model

Because generation and analysis literally run as your session's own turns, "choosing a generation
model" now means choosing what model your Claude Code session is running when you drive
`spectra-generate`, `spectra-criteria`, or `spectra-update`.

| Tier | Best for | Trade-off |
|------|----------|-----------|
| Haiku | Straightforward CRUD docs, light re-verification, fast iteration | Shallower behavior analysis — fewer implicit edge cases surfaced |
| Sonnet | Default for most suites — the balance of depth and speed | Good default; matches the critic's own family |
| Opus | Complex business logic, deep implicit rules, large multi-doc suites | Slower and more expensive per turn; reserve for suites where Haiku/Sonnet analysis feels shallow |

There is no dual-provider requirement anymore. Prior guidance to pick a *different* model family
for the critic than the generator (for independent hallucination detection) no longer applies —
Spec 058/ARCHITECTURE-v2 §32 found a same-family critic (Sonnet verifying Sonnet-authored tests)
reads as *more* useful in practice, because it shares the same standard of evidence. The critic
stays pinned to the Sonnet family by default for that reason, independent of whatever model your
own session uses.

---

## Tuning levers that still exist

Everything provider/model-routing related is gone from `spectra.config.json`. What's left under
`ai` is purely about shape and pacing, not cost selection:

```json
{
  "ai": {
    "generation_batch_size": 30,
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2
  }
}
```

- **`generation_batch_size`** (default 30) — tests requested per generation turn. Larger batches
  mean fewer round-trips through `compile-prompt` → turn → `ingest-tests`, at the cost of a bigger
  single response for the model to produce in one turn.
- **`generation_timeout_minutes`** / **`analysis_timeout_minutes`** — these bound the deterministic
  CLI side of the seam (parsing, validation, index writes), not the model turn itself, since the
  turn now runs in your interactive session rather than an SDK call SPECTRA is waiting on.

---

## Tracking what you've spent

SPECTRA can no longer report per-call token usage the way it did through v1 — it isn't the process
making the model calls anymore, so it has nothing to observe. `.spectra-debug.log`'s old `RUN
TOTAL … tokens_in=… tokens_out=…` line and the Copilot-SDK-era `AssistantUsageEvent` capture were
both retired along with the SDK.

To see what a generation/analysis/criteria/update run actually cost, use Claude Code's own usage
surfaces — your plan's usage indicator, or the API/Console token dashboard if you're on
pay-per-token billing — the same way you'd track the cost of any other work in the session.

---

## Migrating from a pre-v2 config

If you're upgrading from a release before the Claude Code migration:

**Before (v1, GitHub Copilot SDK):**

```json
{
  "ai": {
    "providers": [
      { "name": "github-models", "model": "claude-sonnet-4.5", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "model": "claude-sonnet-4-6"
    }
  }
}
```

**After (v2, Claude Code session):**

```json
{
  "ai": {
    "generation_batch_size": 30,
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2
  }
}
```

You don't need to edit anything by hand — a pre-v2 `spectra.config.json` loads unchanged (the
`providers`/`critic` keys are simply ignored) and `spectra init`/`spectra validate` no longer ask
about or warn on AI providers. `spectra auth` and `gh auth login` are gone; there is nothing to
authenticate — you're already authenticated as whatever account is running your Claude Code
session. See [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) for the
full picture of what changed beyond cost.
