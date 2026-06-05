# Quickstart: Verifying provider retirement

**Feature**: `058-provider-retirement` | maps to Success Criteria SC-001..SC-007

## 1. The chain is gone (SC-001)

```bash
# Zero source references to the removed symbols (build artifacts excluded):
grep -rn "CopilotService\|CopilotGenerationAgent\|ProviderMapping\|CreateAgentAsync\|ProviderChain" \
  src/ --include=*.cs | grep -v "/bin/" | grep -v "/obj/"      # → no matches

# The SDK package is gone:
grep -n "GitHub.Copilot.SDK" src/Spectra.CLI/Spectra.CLI.csproj   # → no match

dotnet build      # → succeeds, no missing-reference errors
```

## 2. Generation hands off without a model call (SC-002)

```bash
# Deterministic phases + compiled-prompt handoff; no in-process model, no network/creds needed:
dotnet run --project src/Spectra.CLI -- ai compile-prompt demo --count 5   # → prints prompt, exit 0
# `ai generate` runs its deterministic phases and ends at the handoff (no model turn).
```

## 3. Cleaned schema validates; dead keys get a notice (SC-003)

```bash
# A config WITHOUT providers/fallback/dead-critic fields validates:
dotnet run --project src/Spectra.CLI -- validate --output-format json     # → valid

# A config WITH dead keys still validates AND surfaces a non-blocking, key-naming note
# (not a silent drop, exit code unchanged).
```

Unit coverage: `ConfigLoader` cleaned-schema test (C-1..C-3) + ignore-with-notice test (C-4).

## 4. `ai.critic.model` is the only selector (SC-004)

```bash
# Unset → claude-sonnet-4-6 default; set → wins; no provider value affects it.
```

Covered by the preserved `CriticModelResolverTests` / `CopilotCriticDefaultModelTests` (Bucket B).

## 5. Surviving config behaves identically (SC-005)

```bash
# Token budget still fails fast at the limit (exit 4); debug telemetry still written.
```

Preserved `PreFlightTokenCheckerTests` + `DebugConfigTests` stay green (do not edit).

## 6. Demo repos run on the cleaned schema (SC-006)

```bash
# After hand-migration, each demo config loads clean:
dotnet run --project src/Spectra.CLI -- validate \
  # cwd = C:/SourceCode/Spectra_Demo/test_app_documentation        → valid, no dead keys
  # cwd = C:/SourceCode/AutomateThePlanet_SystemTests              → valid, no dead keys
```

## 7. Nothing that should be untouched changed (SC-007)

```bash
git status --short src/Spectra.MCP/        # → empty (FR-008)
dotnet test                                # → Core surviving-config + MCP corpus green
```

## Done-when

- All seven checks pass; `dotnet build` + `dotnet test` green; `git status src/Spectra.MCP` empty;
  both demo configs migrated and validating; docs updated; CLAUDE.md no longer calls the Copilot SDK
  the sole AI runtime.
