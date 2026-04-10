# Quickstart: Verifying Unified Critic Provider List

**Feature**: 039-unify-critic-providers
**Audience**: Reviewer / QA

## Scenario A — Azure-only billing setup (US1)

Set the critic provider to `azure-openai` in `spectra.config.json`:

```json
{
  "ai": {
    "providers": [{"name": "azure-openai", "model": "gpt-4.1-mini", "enabled": true}],
    "critic": {
      "enabled": true,
      "provider": "azure-openai",
      "model": "gpt-4o"
    }
  }
}
```

Run any generation. **Expect**: critic initializes successfully, no validation error.

## Scenario B — Legacy `github` alias (US2)

```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "provider": "github",
      "model": "gpt-4o-mini"
    }
  }
}
```

Run generation. **Expect**:
- Generation succeeds
- A one-line stderr warning: `⚠ Critic provider 'github' is deprecated. Use 'github-models' instead.`
- Critic uses `github-models`

## Scenario C — Legacy `google` hard error (US2)

```json
{
  "ai": {
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash"
    }
  }
}
```

Run generation. **Expect**:
- Generation fails with a clear error listing the five supported providers
- Exit code is non-zero only if the critic is required; otherwise generation continues without critic verification (existing behavior for failed critic init)

## Scenario D — Unknown provider

```json
{
  "ai": {
    "critic": { "enabled": true, "provider": "openia" }
  }
}
```

**Expect**: same error message as Scenario C, listing the five supported providers.

## Scenario E — Documentation parity (US3)

Open `docs/configuration.md` and `docs/grounding-verification.md`. **Expect**:
- Critic provider list shows exactly the canonical 5
- No `google` references
- No `GOOGLE_API_KEY` references for the critic
- At least one example shows an Azure-only billing setup

## Acceptance gate

- [ ] Scenario A succeeds
- [ ] Scenario B emits the deprecation warning and continues
- [ ] Scenario C fails fast with the actionable error
- [ ] Scenario D fails fast with the actionable error
- [ ] Scenario E confirms doc parity
- [ ] `dotnet test` passes with at least 5 net new tests
