# Contract: Command behavior after the cut

**Feature**: `058-provider-retirement`

How the affected CLI commands behave once the in-process model path is removed. The deterministic
seams referenced here already exist (Spec 053 `compile-prompt`/`ingest-tests`, Spec 054
`ingest-criteria`, Spec 055 critic subagent).

## `spectra ai generate`

| Aspect | Before | After |
|---|---|---|
| Deterministic preflight (token budget, gap analysis, profile, criteria load, prompt compile) | runs | **unchanged** |
| Generative turn | `AgentFactory.CreateAgentAsync` → `agent.GenerateTestsAsync` (in-process model) | **removed** — the compiled prompt is the handoff; the model turn belongs to the `spectra-generation` skill / the `compile-prompt`+`ingest-tests` seam |
| In-process behavior analysis (`BehaviorAnalyzer`) | runs | **removed** — analysis is the skill's in-session work; `--analyze-only` deterministic outputs unaffected |
| In-process critic (`CriticFactory`→`CopilotCritic`) | optional verify | **removed** — verification is the `spectra-critic` subagent |
| Persist/index (`TestPersistenceService`) | runs | **unchanged** |
| Exit codes | existing | unchanged for deterministic phases; no model-auth exit path remains |

**End-state**: `ai generate` performs its deterministic phases and ends at the compiled-prompt
handoff without calling a model. No silent capability loss — the model turn moved to the Claude Code
session (skill) and the scriptable `compile-prompt`/`ingest-tests` seam.

## `spectra ai generate --from-description`

`UserDescribedGenerator` drops its in-process `AgentFactory.CreateAgentAsync` call; the
deterministic `BuildPrompt` is retained and the flow compiles + hands off via the same seam.

## `spectra ai analyze --extract-criteria`

Drops the in-process `CriteriaExtractor`. Criteria extraction is skill-driven through the Spec 054
seam (`ExtractionPromptCompiler` → skill extracts → `ingest-criteria` deterministic import). The
existing `--import-criteria FILE` / `--list-criteria` deterministic paths are unaffected.

## `spectra docs index`

Drops the in-process `RequirementsExtractor`. Criteria extraction during indexing routes through the
same Spec 054 deterministic seam. The index build itself (manifest, per-suite files, checksums) is
unchanged.

## `spectra auth`

The Copilot SDK auth probe (`CopilotService.ResolveCopilotCliPath`, `AgentFactory.GetAuthStatusAsync`)
is obsolete in v2. **After**: the command is reduced to an informational, deterministic message that
model auth is handled by the user's Claude Code session — no SDK call, stable exit code 0. (If a
cleaner removal is warranted during implementation, retiring the command outright is acceptable
provided no other consumer depends on it.)

## `spectra init`

Drops the `AgentFactory.GetAuthStatusAsync()` provider-auth status line. Init continues to install
skills/agents and write `.claude/settings.json` (Specs 056/057) and now emits a cleaned-schema
`spectra.config.json` (no dead keys).

## `spectra config` (show)

`ConfigHandler` stops printing the `providers` list and `fallback_strategy` (removed fields); critic
display shows only `model` (+ behavioral keys).

## Non-goals

No new commands, no new flags, no MCP change, no automatic rewrite of user config files (the
ignore-with-notice behavior plus hand-migration of the two demo configs covers FR-006/FR-009).
