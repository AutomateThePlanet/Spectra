# Implementation Plan: Authoring Orchestration Port (skills → `.claude/skills/`)

**Branch**: `056-orchestration-port-skills` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/056-orchestration-port-skills/spec.md`

## Summary

Port the **authoring** orchestration — the generation agent + 13 authoring skills bundled under
`src/Spectra.CLI/Skills/Content/` — from GitHub Copilot's agent/skill format to Claude Code's
`.claude/skills/<name>/SKILL.md`, shipped through the **unchanged** `SkillResourceLoader` /
`SkillsManifest` install-and-hash pipeline. Four halves:

1. **Install retarget** (FR-001): `InitHandler` and `UpdateSkillsHandler` write the authoring set to
   `.claude/skills/<name>/SKILL.md` (and the invoked critic subagent to `.claude/agents/`) instead of
   `.github/skills/` / `.github/agents/`. Same pipeline, same hashing, same update-detection — only
   the **target path** and the **content** change. The execution agent is **excluded** (it stays on
   its current `.github/` install until the next spec).
2. **De-Copilot the content** (FR-002): every ported artifact drops `model: GPT-4o` and
   `disable-model-invocation`, and its `{{…TOOLS}}` placeholder is resolved to Claude Code's tool
   model (the `SkillResourceLoader` tool-list constants are repointed for the ported set so no
   unexpanded placeholder ships). The `runInTerminal`/`awaitTerminal` "do NOTHING while waiting"
   discipline and the "do NOT ask clarifying questions" confirmation-avoidance lines are **translated
   in spirit** to Claude Code's terminal/confirmation model, not copied verbatim (FR-005).
3. **Generation skill drives the loop + mandates the critic** (FR-003/FR-004): the ported generation
   skill's procedure compiles the prompt, takes the generated content, calls the model-free
   ingest/validate boundary, regenerates on a fail-loud validation **or** critic-damage error using
   the specific error (bounded by the existing retry limit), and invokes the `context: fork` critic
   subagent (shipped by the preceding spec) as a **mandatory, explicit, non-skippable** step before
   persistence.
4. **Runtime declaration refresh** (FR-006/FR-007): `CLAUDE.md` loses "GitHub Copilot SDK (sole AI
   runtime)" and describes the Claude-Code-only model; the converged `.claude/skills/` surface becomes
   the documented authoring path of record.

**Scope decision (precedent-matched, additive)**: Like 053/054/055 (all merged "CLI surface"), the
**literal removal of the in-process C# model call** from the live generation/critic paths is
**deferred** — here it is coupled to the provider-retirement spec that follows, because the in-process
call cannot be torn out without retiring the provider chain (`Agent/Copilot/`) that powers it. This
spec makes the `.claude/skills/` surface the **path of record** and keeps the existing in-process path
working, avoiding a window where authoring has no model path. The reused `SkillResourceLoader` /
`SkillsManifest` mechanism, the model-free CLI verbs the skills call, and `Spectra.Core` are unchanged.

## Technical Context

**Language/Version**: C# 12, .NET 8 (install handlers + loader); Markdown/YAML-frontmatter (the ported
skill content itself)
**Primary Dependencies**: System.CommandLine, System.Text.Json, Spectre.Console; the bundled content
is embedded-resource Markdown loaded via reflection by `SkillResourceLoader`
**Storage**: File-based — installed skills land under `.claude/skills/<name>/SKILL.md` (authoring) and
`.claude/agents/` (invoked critic subagent) in the consuming project's working directory; the
`.spectra/` manifest tracks them by content hash (unchanged mechanism)
**Testing**: xUnit — `Spectra.CLI.Tests` (install-target + manifest, no-Copilot-ism, generation-skill
mandates-critic, loop-choreography). The `Spectra.Core` suite and the CLI-verb tests for the commands
the skills call are the untouched regression net
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS); installed artifacts consumed by an
interactive Claude Code session
**Project Type**: Single project (CLI + Core libraries) — no new project
**Performance Goals**: Install/update is file I/O bound and unchanged in cost; content translation adds
no runtime cost
**Constraints**: No Copilot-ism may ship in a ported skill (0 `model: GPT-4o`, 0
`disable-model-invocation`, 0 unexpanded `{{…TOOLS}}`); the mandatory critic step must have no
skip/auto-invoke path; the generation loop must be bounded by the existing retry limit (no unbounded
loop, no silent persistence at the limit); the execution agent must not be relocated or translated
(scope boundary); the reused install pipeline/hashing is byte-for-byte unchanged in mechanism
**Scale/Scope**: ~14 content files translated (1 generation agent → generation SKILL + 13 authoring
skills), ~2 modified handlers (`InitHandler`, `UpdateSkillsHandler`), ~1 modified loader
(`SkillResourceLoader` tool-list constants), `CLAUDE.md` refresh; ~1 rewritten test file
(`InitCommandTests`) + ~3 net-new test files; zero data-model changes; the execution agent and the
literal in-process C# removal are explicitly out of scope

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ Installed skills are git-trackable files in the consuming repo (`.claude/skills/`), same as today's `.github/skills/`. No external storage. The bundled content is committed embedded resources. |
| **II. Deterministic Execution** | ✅ Install is deterministic file I/O via the unchanged manifest/hash mechanism. The generation skill drives the **deterministic** model-free CLI verbs (compile/ingest) the preceding specs delivered; no state moves into the orchestrator. |
| **III. Orchestrator-Agnostic** | ✅ The port targets Claude Code, but the skills call only the model-free, self-contained CLI verbs — any orchestrator can drive them. The critic runs `context: fork` (isolated). The MCP execution surface is untouched. |
| **IV. CLI-First** | ✅ No new chat loop — the skills are thin choreography over named CLI verbs with deterministic exit codes. `init` / `update-skills` keep their exit-code contract; only the install target path changes. |
| **V. Simplicity (YAGNI)** | ⚠️ Retargets the install path and translates content while the in-process C# path is retained (two runtimes coexist for one more spec) — the established additive precedent (053/054/055), not speculative. Tracked below. The change *removes* Copilot-isms and a stale runtime declaration, net-simplifying the authoring surface. |

**Result**: PASS. The single soft flag (in-process path retained one more spec) is the explicit,
precedent-matching additive decision tracked in Complexity Tracking — not unjustified complexity. The
port itself is a net simplification: it deletes three classes of Copilot-ism and consolidates the
authoring surface onto one runtime's format.

## Project Structure

### Documentation (this feature)

```text
specs/056-orchestration-port-skills/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (artifact taxonomy — no persisted data model)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── install-target.md          # init/update-skills → .claude/skills/ + .claude/agents/ contract
│   ├── skill-format.md            # ported SKILL.md format + no-Copilot-ism contract
│   ├── generation-skill.md        # loop-choreography + mandatory-critic contract
│   └── claude-md-refresh.md       # CLAUDE.md runtime-declaration contract
├── checklists/
│   └── requirements.md  # (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Commands/Init/
│   └── InitHandler.cs                # MODIFY — authoring skills → .claude/skills/<name>/SKILL.md;
│                                     #          critic subagent → .claude/agents/; generation agent →
│                                     #          .claude/skills/spectra-generation/SKILL.md; execution
│                                     #          agent install left on .github/ (excluded). Log lines updated.
├── Commands/UpdateSkills/
│   └── UpdateSkillsHandler.cs        # MODIFY — same retarget for the authoring set; execution agent untouched
├── Skills/
│   ├── SkillResourceLoader.cs        # MODIFY — resolve {{…TOOLS}} to Claude Code tool model for the ported
│   │                                 #          set (repoint the tool-list constants); no unexpanded token ships
│   └── Content/
│       ├── Agents/
│       │   └── spectra-generation.agent.md  # TRANSLATE — drop GPT-4o / disable-model-invocation; resolve tools;
│       │                                    #             generation skill drives loop + MANDATES critic step;
│       │                                    #             terminal/confirmation discipline → Claude Code spirit
│       └── Skills/                          # TRANSLATE (13) — drop GPT-4o / disable-model-invocation; resolve
│           ├── spectra-coverage.md          #   tools; terminal/confirmation discipline → Claude Code spirit
│           ├── spectra-criteria.md
│           ├── spectra-dashboard.md
│           ├── spectra-delete.md
│           ├── spectra-docs.md
│           ├── spectra-generate.md          #   (generation skill body — drives compile→generate→ingest loop,
│           │                                #    mandates critic, bounded retry)
│           ├── spectra-help.md
│           ├── spectra-init-profile.md
│           ├── spectra-list.md
│           ├── spectra-prompts.md
│           ├── spectra-quickstart.md
│           ├── spectra-suite.md
│           ├── spectra-update.md
│           └── spectra-validate.md
└── (spectra-execution.agent.md is NOT modified or relocated — next spec)

CLAUDE.md                              # MODIFY — drop "GitHub Copilot SDK (sole AI runtime)"; describe
                                       #          Claude-Code-only runtime; .claude/skills/ as path of record

tests/Spectra.CLI.Tests/
├── Commands/
│   └── InitCommandTests.cs            # REWRITE — assert .claude/skills/ + .claude/agents/ install targets
│                                      #           (was .github/); execution agent still on .github/
├── Skills/
│   ├── ClaudeSkillsInstallTests.cs    # NEW — install produces .claude/skills/<name>/SKILL.md through the
│   │                                  #       existing pipeline; 0 land under .github/ for the authoring set (FR-001)
│   ├── NoCopilotIsmsTests.cs          # NEW — 0 ported skills contain model: GPT-4o / disable-model-invocation /
│   │                                  #       unexpanded {{…TOOLS}} (FR-002)
│   └── GenerationSkillContractTests.cs # NEW — generation skill mandates the critic step (non-skippable) and
│                                      #       drives the bounded compile→generate→ingest→regenerate loop (FR-003/FR-004)
```

**Structure Decision**: Single-project layout, no new project. The change is concentrated in two
install handlers, one resource loader, the bundled content files, and `CLAUDE.md` — the
`SkillResourceLoader` / `SkillsManifest` *mechanism* (manifest store, hashing, update-detection,
embedded-resource reflection) is reused unchanged; only the install **target path** and the installed
**content** change. The ported authoring skills land under `.claude/skills/<name>/SKILL.md`; the
critic subagent (invoked, shipped by the preceding spec) is relocated to `.claude/agents/` so the
generation skill can invoke it; the execution agent is deliberately left on its current `.github/`
install to keep this spec's scope boundary crisp (SC-005) and is ported by the next spec.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Two runtimes coexist one more spec: the `.claude/skills/` authoring surface is shipped while the in-process C# generation/critic model call is **retained** | Precedent-matched *additive* scope: removing the in-process call now would break `spectra ai generate`'s live model path before the provider chain is retired. The literal removal is coupled to the provider-retirement spec (the call cannot be torn out without retiring `Agent/Copilot/`). Mirrors exactly what 053/054/055 shipped. | "Full removal now" rejected: breaks the working generate flow, contradicts the series precedent and this spec's Assumptions. "Dual `.github/` + `.claude/` install" rejected: SC-001 requires 0 authoring skills under `.github/`; dual-install leaves stale Copilot artifacts and two sources of truth. |
| Execution agent excluded from the retarget (stays on `.github/`) while the rest moves to `.claude/` — a transient mixed install state | The execution agent is the independent pista, ported by the next spec; relocating/translating it here would overstep scope (SC-005) and entangle the MCP-allowlist setup that belongs to that spec. | "Move everything to `.claude/` now" rejected: pulls the execution-agent content port and the `mcp__spectra__*` allowlist into this spec, violating the stated scope boundary and the series' one-pista-per-spec shape. |
| `SkillResourceLoader` tool-list constants repointed to Claude Code's tool model rather than dropping the `tools:` frontmatter entirely | Resolving `{{…TOOLS}}` to a concrete Claude Code tool set keeps the loader's existing placeholder mechanism intact (reused-verbatim pipeline) while guaranteeing no unexpanded token ships (FR-002). | "Drop `tools:` frontmatter outright" rejected for the ported set in this spec: it would diverge the loader's behavior per-artifact and complicate the still-`.github/` execution agent that shares the loader; repointing the constant is the smaller, uniform change. |
