# Quickstart: Critic as a `context: fork` Subagent (+ Gating Semantics)

This feature adds a **model-free** critic verification surface (compile + ingest), a `context: fork`
critic subagent skill, and collapses the critic-model selection onto a single config key — all
**additively**, with the existing in-process critic still working.

## The model-free choreography (what the subagent does)

```bash
# 1. Compile the critic prompt deterministically (no model call, no token spend)
spectra ai compile-critic-prompt --test ./tc-900.json --docs ./docs/checkout > critic.prompt

# 2. The spectra-critic context:fork subagent runs the model turn over critic.prompt
#    (fresh context: it sees ONLY the test artifact + selected source docs)

# 3. Ingest the verdict — fail loud on damage, advisory gate on verdict
echo '{"verdict":"hallucinated","score":0.1,"findings":[]}' | spectra ai ingest-verdict
echo $?   # 0  → outcome=Verdict, drop=true (hallucinated drops — gating unchanged)
```

## Fail-loud damage (the gating-semantics change)

```bash
# Missing 'verdict' → fail loud, exit 6 (NOT a silent Partial/0.5 soft pass)
echo '{"score":0.5}' | spectra ai ingest-verdict ; echo $?   # 6

# Empty response → exit 5
printf '' | spectra ai ingest-verdict ; echo $?              # 5

# Well-formed grounded verdict → exit 0, passes the gate
echo '{"verdict":"grounded","score":0.95,"findings":[]}' | spectra ai ingest-verdict ; echo $?  # 0
```

A critic *call failure* (exception/timeout) is different from damage: on the retained in-process
path it yields an `Unverified`-style result (test passes, non-blocking) and is recorded **distinctly**
from a parse-miss — it never reaches `ingest-verdict` as a verdict.

## Single critic-model selector

```jsonc
// spectra.config.json — ai.critic.model is now the single source of truth
{ "ai": { "critic": { "enabled": true, "model": "claude-sonnet-4-6" } } }
```

When `ai.critic.model` is unset, a single same-family default applies — there is no longer a
provider-keyed default switch.

## Validation walkthrough (maps to acceptance scenarios)

| Scenario | Command | Expected |
|----------|---------|----------|
| US1 — model-free compile | `compile-critic-prompt --test t.json` (no provider) | exit 0, prompt emitted, byte-identical on repeat |
| US1 — refuse on missing artifact | `compile-critic-prompt` | exit 4 |
| US2 — hallucinated drops | `ingest-verdict` with `verdict=hallucinated` | exit 0, `drop=true` |
| US2 — grounded passes | `ingest-verdict` with `verdict=grounded` | exit 0, `drop=false` |
| US3 — damage fails loud | `ingest-verdict` with missing `verdict`/`score` | exit 6, specific error, no `Partial`/`0.5` |
| US3 — empty fails loud | `ingest-verdict` with empty input | exit 5 |
| US4 — single selector | set `ai.critic.model`, resolve | configured value used; no provider switch reachable |

## Regression net (must stay green, untouched)

```bash
dotnet test tests/Spectra.Core.Tests        # grounding-model tests — DO NOT TOUCH
dotnet test tests/Spectra.CLI.Tests         # incl. verdict-gating drop-vs-pass — DO NOT TOUCH the gating tests
```

If a grounding-model or verdict-gating test breaks, investigate a regression — do not edit the test.
