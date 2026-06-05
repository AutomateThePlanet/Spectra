# Phase 0 Research: Provider retirement + config cleanup + demo repos

**Date**: 2026-06-05 | **Branch**: `058-provider-retirement`

This is the convergence spec of the 052–058 migration. Specs 053–057 each shipped **additively**
("CLI surface"): they built the Claude Code replacement next to the legacy in-process path and left
the old path live. This spec removes the old paths, rewires the handlers onto the already-built
deterministic seams, deletes the GitHub Copilot SDK, and collapses the config.

The findings below come from four parallel read-only investigations of the live tree.

---

## D-0: The spec is UNBLOCKED — every in-process model caller has a landed replacement

All prerequisite specs are merged on `claude-code-v2` (git-confirmed), and the directory numbers
equal the conceptual numbers:

| Dir | What it built | Replacement seam it created |
|---|---|---|
| `053-prompt-compiler` | Generation handoff inversion | `PromptCompiler`, `GeneratedTestIngestor`, `compile-prompt`, `ingest-tests` |
| `054-criteria-extraction-rehoming` | Extraction re-homing + extractor unification | `ExtractionPromptCompiler`, `CriteriaIngestor`, `ingest-criteria` |
| `055-critic-subagent` | Critic as `context: fork` subagent | `.claude/agents/spectra-critic.agent.md`; `CriticModelResolver` single source |
| `056-orchestration-port-skills` | Authoring skills → `.claude/skills/` | `spectra-generation` skill drives compile→generate→ingest |
| `057-execution-agent-port` | Execution agent → Claude Code | (independent; no provider-chain coupling) |

**Decision**: This spec is a *cutover + teardown*, not a blocked-on-prereqs build. Each in-process
caller is removed and its handler rewired onto the matching landed seam. (Note: the spec.md
"Dependencies" prose labels these 052–055; the real on-disk dirs are 053–055 — a cosmetic label
offset, substance unchanged.)

---

## D-1: The removal target is a closed SDK cluster + two keystones to rewire

**Single SDK dependency**: `GitHub.Copilot.SDK` v0.2.1 — `src/Spectra.CLI/Spectra.CLI.csproj:13`
(the only `PackageReference` to it; `Spectra.Core`/`Spectra.MCP` do not reference it). Removable once
every `using GitHub.Copilot.SDK` is gone.

**Closed cluster — delete wholesale** (all callers are other removal-targets or rewired handlers):
- `Agent/Copilot/CopilotService.cs` — session factory + `GetCriticModel`
- `Agent/Copilot/ProviderMapping.cs` — only called by `CopilotService`
- `Agent/Copilot/GenerationAgent.cs` (`CopilotGenerationAgent`)
- `Agent/Copilot/BehaviorAnalyzer.cs`
- `Agent/Copilot/CriteriaExtractor.cs`
- `Agent/Copilot/RequirementsExtractor.cs`
- `Agent/Copilot/GroundingAgent.cs` (`CopilotCritic`)
- `Provider/ProviderChain.cs` — thin wrapper over `AgentFactory.CreateAgentAsync`, no external value

**Survives in `Agent/Copilot/`** (language-neutral, no SDK): `DocumentTools`, `TestIndexTools`,
`AnalyzerInputBuilder`, `DocSuiteSelector`, `CriteriaExtractionResult`,
`RequirementsExtractionResult`, `IExtractionDelayProvider`. These must keep compiling — verify no
member of theirs depends on a deleted type; relocate the namespace if the `Copilot/` folder is
emptied of SDK code (decision: keep the folder, just remove SDK files).

**Keystones — rewire, do not delete the symbol outright**:
- `AgentFactory` (`Agent/AgentFactory.cs`) — 7 call-sites: `GenerateHandler` ×2 (`:641`, `:1361`),
  `UserDescribedGenerator:88`, `ProviderChain:59` (being deleted), `InitHandler:251`,
  `AuthHandler:40`,`:46`. Its `CreateAgentAsync` (in-process agent construction) is removed; its
  auth/availability surface (`GetAuthStatusAsync`, `GetAvailableProviders`) is either removed with
  its callers or retargeted (see D-4 auth).
- `CriticFactory` (`Agent/Critic/CriticFactory.cs`) — 1 call-site `GenerateHandler:2075`, which
  already has a graceful "verification unavailable → continue" fallback. Remove the `CopilotCritic`
  construction; the critic now runs as the 055 subagent. `CriticModelResolver` **survives as-is**
  (pure function, no SDK) and remains the single `ai.critic.model` source.

**Decision**: delete the cluster files; gut `AgentFactory`/`CriticFactory` to remove SDK
construction; delete `ProviderChain`; rewire the four command handlers (below). Keep
`CriticModelResolver`.

---

## D-2: The handler rewire — onto seams that already exist

The four commands that drive in-process model calls each have a landed deterministic replacement;
the rewire removes the in-process branch and leans on the seam.

| Handler | In-process today | Landed v2 seam | Rewire end-state |
|---|---|---|---|
| `GenerateHandler` (batch `ExecuteDirectModeAsync`, interactive `ExecuteInteractiveModeAsync`) | `AgentFactory.CreateAgentAsync` → `agent.GenerateTestsAsync` (`:762`, `:1403`); `BehaviorAnalyzer` (`:447/498/1276`); `CriticFactory` (`:2075`) | `compile-prompt` + `ingest-tests` (053); `spectra-generation` skill (056); critic subagent (055) | Keep deterministic preflight/gap/profile/compile; **remove** the in-process generate loop, in-process analysis, and in-process critic. The `spectra-generation` skill orchestrates compile→generate→ingest. |
| `UserDescribedGenerator` | `AgentFactory.CreateAgentAsync` → `agent.GenerateTestsAsync` (single test) | `compile-prompt`/`ingest-tests` seam; `BuildPrompt` reusable | Remove in-process call; the from-description flow compiles + hands off (skill / seam). |
| `AnalyzeHandler` (`--extract-criteria`) | `CriteriaExtractor` (`:460`,`:956`) | `ExtractionPromptCompiler` + `CriteriaIngestor` + `ingest-criteria` (054) | Remove in-process extractor; extraction is skill-driven via the compile→ingest-criteria seam. |
| `DocsIndexHandler` | `RequirementsExtractor:285` | 054 extractor unification | Remove in-process extractor; index criteria extraction goes through the same deterministic seam. |

**Open implementation decision (defaulted, documented)**: what does `spectra ai generate --count N`
*do* when invoked directly (not from the skill) after the in-process loop is gone? **Default**: it
runs its deterministic phases and ends at the compiled-prompt handoff — it does not call a model.
The model turn belongs to the Claude Code session (the `spectra-generation` skill calls
`compile-prompt`, generates, then `ingest-tests`). The standalone `compile-prompt`/`ingest-tests`
commands remain the scriptable seam. This matches ARCHITECTURE-v2 (skills own the model turn; the
CLI is deterministic) and the constitution's CLI-First + "AI never writes files directly"
principles. No new orchestration code is introduced by this spec.

---

## D-3: Config — fields, readers, loader, and the ignore-with-notice mechanism

**Remove / relax** (`src/Spectra.Core/Models/Config/`):
- `AiConfig.Providers` (`:10`, `required IReadOnlyList<ProviderConfig>`) → **drop the field** (the
  interactive session selects the model; no code needs a generator provider after the rewire).
  Readers to clean: `ConfigLoader:181/188` (the `MISSING_PROVIDERS` validation), `ConfigHandler:203/206`
  (display), and ~12 `Providers?.FirstOrDefault()` model-for-reporting reads across
  `GenerateHandler`/`AnalyzeHandler`/`DocsIndexHandler`/`UserDescribedGenerator`/`ProviderResolver`
  — most of those reader sites are inside in-process branches being removed anyway.
- `AiConfig.FallbackStrategy` (`:13`) → **remove**. Only reader is `ConfigHandler:210` (display).
- `CriticConfig.Provider` / `ApiKeyEnv` / `BaseUrl` (`:24/36/42`) → **remove**. Readers:
  `CriticFactory.ResolveProvider`, `CriticConfig.GetEffectiveModel`/`GetDefaultApiKeyEnv`,
  `CopilotService` (being deleted). `GetEffectiveModel`/`GetDefaultApiKeyEnv` per-provider switches
  are superseded by `CriticModelResolver` — remove them. `CriticConfig.IsValid()` (requires
  `Provider` when enabled) must be relaxed to not require `Provider`.
- `ProviderConfig` (whole type) → **orphaned** once `Providers` is gone — delete it and its
  serialization test usage.

**Survive untouched** (regression net — do not edit these tests):
- `AnalysisConfig.MaxPromptTokens` (`:21`, default 96 000) — enforced by `PreFlightTokenChecker`
  (exit code 4). The cost lever; unchanged.
- `DebugConfig.Enabled` — telemetry, read by `GenerateHandler`/`UpdateHandler`/`DebugLogger`.
  Unchanged.

**Loader behavior** (`src/Spectra.Core/Config/ConfigLoader.cs`): `JsonSerializerOptions` =
`{ PropertyNameCaseInsensitive, ReadCommentHandling=Skip, AllowTrailingCommas }` — **unknown keys are
silently ignored by default** (no `UnmappedMemberHandling`). So a config still carrying dead keys
will *not* throw; it will silently ignore them. FR-006 requires *non-silent* — so we must detect the
dead keys and emit a notice.

**Ignore-with-notice mechanism (reuse Spec 048 pattern)**: results expose optional notice fields
serialized only when non-null:
- `DocsIndexResult.CriteriaWarning` (`[JsonIgnore(WhenWritingNull)]`) built by
  `ComputeCriteriaWarning`.
- `GenerateResult.Notes` (`IReadOnlyList<string>?`) accumulated and built by `BuildNoCriteriaNote`.

**Decision**: add a pure helper (e.g. `ConfigLoader.DetectDeprecatedKeys(rawJson)`) that inspects the
raw JSON via `JsonDocument`/`JsonNode` for `ai.providers`, `ai.fallback_strategy`,
`ai.critic.provider`, `ai.critic.api_key_env`, `ai.critic.base_url`, and returns the list of present
dead keys. Surface them through a non-blocking note on the command result (a `config_notes` /
reuse of the existing `Notes` channel) naming the keys — not a silent drop, not a failure, exit code
unchanged. Removal of the keys is documented; no automatic file rewrite (the two demo configs are
hand-migrated under FR-009).

**Template seed** (`src/Spectra.CLI/Templates/spectra.config.json`) ships `ai.providers`,
`ai.fallback_strategy`, and `ai.critic.provider` — these MUST be removed from the seed so `spectra
init` produces a cleaned-schema config.

---

## D-4: `spectra auth` and `init` coupling

`AuthHandler` calls `CopilotService.ResolveCopilotCliPath()` (`:27`) and
`AgentFactory.GetAuthStatusAsync()`/`GetAvailableProviders()` (`:40`,`:46`). `InitHandler:251` calls
`AgentFactory.GetAuthStatusAsync()`.

In v2 the CLI no longer authenticates a model provider (the Claude Code session owns auth). **Decision**:
- `spectra auth` — its purpose (probe Copilot SDK auth) is obsolete. Retire the command's provider
  probe: either remove the command or reduce it to a deterministic "auth is handled by your Claude
  Code session" message. **Default**: reduce to an informational no-op (keeps the command name
  stable, removes the SDK dependency). Confirm there is no other consumer.
- `InitHandler` — drop the `GetAuthStatusAsync` call (init no longer reports provider auth status);
  init already installs skills/agents (056/057) and writes `.claude/settings.json` (057).

This keeps CLI-First (named commands, deterministic exit codes) while severing the SDK.

---

## D-5: Test surface (three buckets)

Total live tests ≈ 1 864 (Core ≈ 496, CLI ≈ 1 050, MCP ≈ 318).

**Bucket A — rewrite/delete (assert removed surfaces)**:
- `tests/Spectra.Core.Tests/Config/ConfigLoaderTests.cs` — ~7 tests assert `ai.providers` parsing /
  `MISSING_PROVIDERS` / fixtures carrying `providers`. Rewrite fixtures to the cleaned schema; replace
  the `MISSING_PROVIDERS` test with "missing providers now validates."
- `tests/Spectra.Core.Tests/Models/Config/CriticConfigTests.cs` — the per-provider
  `GetEffectiveModel` theory (superseded by `CriticModelResolver`) — delete/rewrite.
- Any `ProviderMapping`/`AgentFactory.CreateAgentAsync`/`ProviderConfig`-deserialization tests — remove.
- Copilot-cluster unit tests that exercise deleted classes: `BehaviorAnalyzer*Tests`,
  `Agent/Copilot/CriteriaExtractor*Tests`, `RequirementsExtractorUnificationTests`,
  `UserDescribedGeneratorTests` (in-process portions), `AnalyzeHandlerRetryTests`,
  `DocsIndexCriteriaTimeoutTests`, integration `EndToEndScenarios`/`OriginalBugRegression` portions
  that drive the in-process path — audit each: rewrite to the seam or remove the in-process
  assertion. **These intersect "do-not-touch" only if they live in Core/persistence — they do not;
  they are CLI/integration tests, safe to edit.**

**Bucket B — DO NOT TOUCH (must stay green)**:
- `CriticModelResolverTests` (the `ai.critic.model` single-selector contract),
  `CopilotCriticDefaultModelTests` (already asserts the single same-family default — it tests the v2
  behavior; keep).
- `Spectra.Core` surviving-config tests: `DebugConfigTests`, `AnalysisConfig`/
  `PreFlightTokenCheckerTests`, `CoverageConfigDefaultsTests`, `BrandingConfigTests`,
  `ExecutionConfigBackCompatTests`.
- The entire `Spectra.MCP.Tests` corpus (~318) — engine untouched (FR-008).
- **Memory constraint**: never edit Core/persistence tests — honored; Bucket A edits are CLI/Core-config
  *schema* tests being intentionally rewritten per the spec's "Rewrite" list, not the persistence net.

**Bucket C — net-new**:
- Cleaned-schema validation: a config with no `ai.providers`/`fallback_strategy`/dead critic fields
  validates and the generate flow proceeds (FR-003).
- Old-config ignore-with-notice: a config still carrying dead keys validates, the run proceeds, and a
  non-blocking note names the dead keys (FR-006).
- Demo-repo smoke: the two migrated demo configs load/validate on the cleaned schema (FR-009).
- `response_format` absence assertion (FR-007) — a guard test that no such key exists / is honored.

---

## D-6: `response_format` is a verified no-op (FR-007)

No `response_format` key exists anywhere in the config models or template (`06` §3, confirmed). The
"retirement" is asserting continued absence, not removing code. A single guard test suffices.

---

## Decisions summary

1. **Cutover, not blocked** — all replacement seams (053/054/055) are landed; remove in-process, rewire to seams.
2. **Delete** the closed Copilot SDK cluster + `ProviderChain` + the `GitHub.Copilot.SDK` package; **keep** the language-neutral `Copilot/` helpers and `CriticModelResolver`.
3. **Gut** `AgentFactory`/`CriticFactory` to remove SDK construction; rewire the four handlers onto the deterministic seams; `ai generate` ends at the compiled-prompt handoff (no in-process model turn).
4. **Collapse config**: drop `ai.providers`/`ai.fallback_strategy`/`ProviderConfig` and critic `provider`/`api_key_env`/`base_url`; relax `ConfigLoader` + `CriticConfig.IsValid`; clean the template seed; keep `max_prompt_tokens` + `debug.enabled`.
5. **Ignore-with-notice**: detect dead keys in raw JSON, surface a non-blocking, key-naming note (Spec 048 pattern); no silent drop, no auto-rewrite.
6. **Auth/init**: retire the SDK auth probe; reduce `spectra auth` to informational, drop `init`'s auth-status call.
7. **Tests**: rewrite Bucket A (CLI/Core-config schema), preserve Bucket B (surviving-config + MCP + critic-model), add Bucket C; hand-migrate the two demo configs.
8. **`response_format`**: assert continued absence (no-op).
