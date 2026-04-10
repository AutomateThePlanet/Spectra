# Tasks: Unified Critic Provider List

**Feature**: 039-unify-critic-providers
**Branch**: `039-unify-critic-providers`
**Plan**: [plan.md](./plan.md) · **Spec**: [spec.md](./spec.md)

> Tests are included alongside implementation because spec SC-006 requires ≥5 net new tests.
>
> **MVP** = Phase 2 + Phase 3 (US1). Phases 4 and 5 (US2, US3) are alignment/safety; ship together.

---

## Phase 1: Setup

No project initialization needed.

---

## Phase 2: Foundational

No foundational tasks — both modified files are independent of each other.

---

## Phase 3: User Story 1 — Azure-Only Billing Setup (P1)

**Goal**: `azure-openai` and `azure-anthropic` are accepted as critic providers; the validator and the runtime agree.

**Independent test**: With `ai.critic.provider = "azure-openai"`, run generation; the critic initializes successfully.

### Implementation

- [X] T001 [US1] In `src/Spectra.CLI/Agent/Critic/CriticFactory.cs`, replace `SupportedProviders` with the canonical 5: `github-models`, `azure-openai`, `azure-anthropic`, `openai`, `anthropic`. Remove `azure-deepseek` and `google` from the set.
- [X] T002 [US1] In `CriticFactory.cs`, add a private static `LegacyAliases` `Dictionary<string, string>` with `{ "github" → "github-models" }` (case-insensitive comparer)
- [X] T003 [US1] In `CriticFactory.cs`, add a private static `HardErrorProviders` `HashSet<string>` containing `"google"` (case-insensitive comparer)
- [X] T004 [US1] In `CriticFactory.cs`, add a private static `ResolveProvider(string?)` helper that: lowercases input; returns the resolved name + an optional warning; returns null + an error message for hard-error/unknown values; falls back to `"github-models"` for empty input
- [X] T005 [US1] In `CriticFactory.TryCreate(CriticConfig)`, call `ResolveProvider` first; on failure return `CriticCreateResult.Failed(...)` with the actionable error message that lists all 5 supported providers; on warning emit one stderr line `⚠ Critic provider '{old}' is deprecated. Use '{new}' instead.`; on success construct `CopilotCritic` with the resolved name
- [X] T006 [US1] In `CriticFactory.TryCreateAsync(CriticConfig, CancellationToken)`, run the same `ResolveProvider` validation BEFORE the Copilot availability check (so unknown providers fail fast)
- [X] T007 [US1] In `CriticFactory.IsSupported(string)`, return true for canonical providers AND legacy `github`; return false for `google`/unknown

### Tests

- [X] T008 [P] [US1] Add `tests/Spectra.CLI.Tests/Agent/Critic/CriticFactoryProviderTests.cs` with test `TryCreate_AzureOpenAI_Succeeds` — config with `provider: "azure-openai"`, asserts `Success = true`, `ProviderName = "azure-openai"`
- [X] T009 [P] [US1] Add test `TryCreate_AzureAnthropic_Succeeds` to the same file
- [X] T010 [P] [US1] Add test `TryCreate_CaseInsensitive_NormalizesToLowercase` — config with `provider: "Azure-OpenAI"`, asserts success and `ProviderName = "azure-openai"`

---

## Phase 4: User Story 2 — Backward Compatibility for Legacy Names (P2)

**Goal**: Legacy `github` is rewritten with a stderr warning; legacy `google` is rejected with a clear error.

**Independent test**: Pre-spec configs with `"github"` continue to work (with warning); pre-spec configs with `"google"` fail with an actionable error.

**Depends on**: Phase 3 (T002, T003, T005).

### Tests

- [X] T011 [P] [US2] Add `TryCreate_LegacyGithub_MapsToGithubModels_WithDeprecationWarning` — capture stderr, config with `provider: "github"`, asserts success, `ProviderName = "github-models"`, stderr contains `deprecated` and `github-models`
- [X] T012 [P] [US2] Add `TryCreate_LegacyGoogle_FailsWithActionableError` — config with `provider: "google"`, asserts `Success = false`, error message contains all 5 supported provider names
- [X] T013 [P] [US2] Add `TryCreate_UnknownProvider_FailsWithSameError` — config with `provider: "openia"`, asserts `Success = false` and the same listing
- [X] T014 [P] [US2] Add `TryCreate_EmptyProvider_FallsBackToGithubModels` — config with `provider: ""`, asserts success with `ProviderName = "github-models"`

---

## Phase 5: User Story 3 — Documentation & Stale Defaults (P2)

**Goal**: Docs and the stale `CriticConfig` switch statements reflect the canonical 5 providers.

**Independent test**: Read `docs/configuration.md` and `docs/grounding-verification.md`; both list the same 5 critic providers with no `google`/`GOOGLE_API_KEY` references.

**Depends on**: Phase 3 (canonical set is established).

### Implementation

- [X] T015 [US3] In `src/Spectra.Core/Models/Config/CriticConfig.cs`, update the `Provider` XML doc summary from `"google", "openai", "anthropic", "github"` to `"github-models", "azure-openai", "azure-anthropic", "openai", "anthropic"` (note: legacy `github` is still recognized as an alias)
- [X] T016 [US3] In `CriticConfig.GetEffectiveModel()`, add explicit cases for `"github-models"`, `"azure-openai"`, `"azure-anthropic"` returning `"gpt-4o-mini"` (or appropriate per-provider default). Keep legacy `"github"` and `"google"` cases for read-side safety.
- [X] T017 [US3] In `CriticConfig.GetDefaultApiKeyEnv()`, add explicit cases for `"github-models" → "GITHUB_TOKEN"`, `"azure-openai" → "AZURE_OPENAI_API_KEY"`, `"azure-anthropic" → "AZURE_ANTHROPIC_API_KEY"`. Keep legacy cases.
- [X] T018 [US3] Update `docs/configuration.md`: replace any list of critic providers (`google, openai, anthropic, github`) with the canonical 5; remove `GOOGLE_API_KEY` references in the critic section; add an Azure-only billing example
- [X] T019 [US3] Update `docs/grounding-verification.md`: same edits — supported provider list, example config, remove Google references
- [X] T020 [US3] Update `CLAUDE.md` Recent Changes with a 039 entry summarizing the alignment, the legacy alias rules, and the test count

---

## Phase 6: Polish

- [X] T021 Run `dotnet build` and resolve any compile errors
- [X] T022 Run `dotnet test` and confirm all existing tests still pass; verify ≥5 net new tests
- [X] T023 Manually walk through `quickstart.md` Scenarios A–E

---

## Dependencies

```text
Phase 3 (US1) — T001..T010 (canonical set + ResolveProvider + factory wiring + accept tests)
        │
        ├─→ Phase 4 (US2) — T011..T014 (legacy alias tests; depend on T002/T003/T005 which add the alias logic)
        │
        └─→ Phase 5 (US3) — T015..T020 (docstring + stale switches + docs)

Phase 6 (Polish) — T021..T023 (build/test/walkthrough)
```

US2 and US3 are independent of each other after Phase 3; can be developed in parallel.

## Parallel Execution Examples

**Within Phase 3 tests** — independent assertions in the same file:

```text
T008, T009, T010
```

**Within Phase 4 tests** — same file:

```text
T011, T012, T013, T014
```

**Within Phase 5 docs** — different files:

```text
T018 ∥ T019 ∥ T020
```

## Implementation Strategy

- **MVP scope** = Phase 3 (US1). After T010, Azure billing setups work. Stop here and ship if time-constrained.
- **Phase 4 (US2)** is 4 test additions on top of code already written in Phase 3 — cheap to include in the same PR.
- **Phase 5 (US3)** lands docs together with the code change so users see consistent information.

## Independent Test Criteria

| Story | Criterion |
|-------|-----------|
| US1 (P1) | `TryCreate` with `azure-openai` and `azure-anthropic` succeeds |
| US2 (P2) | Legacy `github` rewrites with stderr warning; legacy `google` and unknowns hard-fail |
| US3 (P2) | `docs/configuration.md` and `docs/grounding-verification.md` list the canonical 5 with no Google references |

## Format Validation

All 23 tasks above follow `- [X] T### [P?] [US#?] description with file path`. Setup/Polish tasks omit the `[US#]` label.
