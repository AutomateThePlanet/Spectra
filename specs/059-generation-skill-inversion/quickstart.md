# Quickstart: Generation on the seam (Spec 059)

After this spec, the `spectra-generate` skill drives every flow through deterministic CLI compile/ingest commands with the agent generating in-session. No `spectra ai generate --count`, no provider chain, no Copilot SDK.

## Bulk generation (US1)

```bash
# 1. Analyze (in-session): compile the analysis prompt, agent identifies behaviors, ingest accounts deterministically
spectra ai compile-analysis-prompt --suite checkout --doc-suite checkout-docs --output-format json
#   → agent answers in-session with behavior JSON →
echo "$BEHAVIORS" | spectra ai ingest-analysis --suite checkout --output-format json
#   → { recommended: 18, already_covered: 12, breakdown, technique_breakdown }  → skill STOPs for approval

# 2. Generate (in-session) after approval
spectra ai compile-prompt --suite checkout --count 18 --focus "negative" --output-format json
#   → agent generates 18 tests in-session →
echo "$TESTS" | spectra ai ingest-tests checkout --output-format json
#   → { success: true, persisted: 18, ids: [...] }  (fail-loud: exit 5/6 → bounded retry)

# 3. Critic (mandatory, per test): spectra-critic subagent compiles+verifies+ingests the verdict
#    gate pass → keep; gate drop → spectra delete; ingest exit 5/6 or compile exit 4 → fail-loud retry
```

## From-description single test (US2)

```bash
spectra ai compile-prompt --suite checkout \
  --from-description "User applies an expired coupon at checkout" \
  --context "Cart page, promo flow" --output-format json
#   → agent generates exactly 1 test in-session (criteria injected per Spec 050) →
echo "$TEST" | spectra ai ingest-tests checkout --output-format json
#   → then the mandatory spectra-critic step (now a real verdict, not `manual`)
```

## Verify the removal landed (US4)

```bash
# Build clean with the SDK + provider chain gone
dotnet build                       # no GitHub.Copilot.SDK, no AgentFactory.CreateAgentAsync / CopilotService / ProviderMapping

# A config without ai.providers validates
spectra validate --output-format json     # exit 0

# A legacy config WITH ai.providers validates with a non-silent note
spectra validate --output-format json     # exit 0, notes[] contains an ai.providers deprecation notice
```

## Regression net (must stay green, unchanged)

```bash
dotnet test tests/Spectra.Core.Tests          # all green — any red is a regression
dotnet test tests/Spectra.CLI.Tests --filter "FullyQualifiedName~PromptCompiler|Ingest|Critic"
```

## Success signals

- `spectra-generate.md` contains zero `spectra ai generate --count` / `--from-description` / `--analyze-only` invocations.
- Every produced test passes through the mandatory `spectra-critic` step before it counts as accepted.
- `grep -r "CreateAgentAsync\|CopilotService\|ProviderMapping\|GitHub.Copilot.SDK" src/` returns nothing.
- `Spectra.Core` + 053/055 test corpora unchanged and green.
