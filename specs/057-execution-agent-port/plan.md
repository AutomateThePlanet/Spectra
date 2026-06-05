# Implementation Plan: Execution Agent Port (independent pista)

**Branch**: `057-execution-agent-port` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/057-execution-agent-port/spec.md`

## Summary

Port the **execution agent** (orchestration prose, not the engine) to Claude Code and **consolidate
three redundant bundled copies into one canonical Claude Code skill**. Five parts, all client-side —
**zero `Spectra.MCP` change**:

1. **De-Copilot + re-author the canonical execution agent** (`Skills/Content/Agents/spectra-execution.agent.md`):
   drop `model: GPT-4o` / `disable-model-invocation`; remove the `github/get_copilot_space` /
   `github/list_copilot_spaces` tools and the "Copilot Spaces Documentation" section; replace
   doc-lookup with **native file reads** (read the test's `source_refs` docs directly); translate the
   `runInTerminal`/`awaitTerminal` "do NOTHING while waiting" discipline and the
   `askQuestion`/`askForConfirmation` ban to Claude Code in spirit; **preserve the verdict pause
   verbatim in spirit** (FR-002/FR-003/FR-004).
2. **Relocate the install to `.claude/`** (FR-007): `SkillInstallLayout` routes the execution agent to
   `.claude/skills/spectra-execution/SKILL.md` (main-session skill, like the generation skill) instead
   of the `.github/` default the preceding spec left it on.
3. **Consolidate / delete the duplicate copies**: retire `InitHandler.InstallAgentFilesAsync` (it
   installed the two `Agent/Resources/` copies to `.github/agents/` + `.github/skills/`); delete
   `Agent/Resources/spectra-execution.agent.md`, `Agent/Resources/SKILL.md`, and the now-unused
   `AgentResourceLoader` (referenced only by `InitHandler`); remove the two `EmbeddedResource` lines
   from the csproj. After this there is exactly **one** execution-agent artifact, so SC-002 ("0
   Copilot-isms across every bundled copy") holds by construction.
4. **Remove the dead config** (FR-003): delete `ExecutionConfig.CopilotSpace` / `CopilotSpaceOwner` —
   no C# reads them; `System.Text.Json` ignores unknown keys so existing configs that still carry
   `execution.copilot_space` deserialize unchanged (SC-004).
5. **Add the client-side MCP allowlist** (FR-005): `spectra init` writes / merges
   `.claude/settings.json` so `permissions.allow` contains `mcp__spectra__*` (idempotent, preserves
   existing entries), and the Spectra repo's own `.claude/settings.json` gets the same — distinct from
   the existing `Bash(spectra-mcp:*)` entry in `settings.local.json`.

**Scope decision (complete port, no deferral)**: Unlike pista A, there is **no in-process model call**
in the execution agent and the MCP engine is already client-agnostic (grep-proven, `04` §1) — so this
spec **finishes** its surface in one pass. The entire `Spectra.MCP` server (`ToolRegistry`, 25 tools,
`ExecutionEngine`/`TestQueue`/state machine, `SaveScreenshotTool`, SQLite store) is **reused verbatim**.

## Technical Context

**Language/Version**: C# 12, .NET 8 (install handler, config model, settings writer); Markdown/YAML
(the execution agent prose); JSON (`.claude/settings.json`)
**Primary Dependencies**: System.CommandLine, System.Text.Json (settings merge + config), Spectre.Console
**Storage**: File-based — the execution agent installs to `.claude/skills/spectra-execution/SKILL.md`;
the allowlist to `.claude/settings.json`; both git-trackable in the consuming workspace. The MCP run
state (in-memory `TestQueue` + SQLite results) is server-held and **untouched**
**Testing**: xUnit — `Spectra.CLI.Tests` (install-target, de-Copilot, allowlist-present, verdict-pause,
config-removal). The **entire `Spectra.MCP.Tests` corpus is the untouched regression net** — a break
there is a regression in something that must not change
**Target Platform**: Cross-platform CLI; the installed agent is consumed by an interactive Claude Code
session driving the MCP server
**Project Type**: Single project (CLI + Core + MCP libraries) — no new project; **no MCP change**
**Performance Goals**: N/A — content + install change only; no runtime path altered
**Constraints**: 0 server-side changes in `Spectra.MCP`; 0 Copilot-isms in the (single) execution
artifact; the verdict pause must remain (no auto-advance, no fabricated verdict); the allowlist must be
distinct from `Bash(spectra-mcp:*)` and must not suppress the human-verdict wait; config removal must
not break deserialization of legacy configs
**Scale/Scope**: ~1 content file re-authored, ~1 layout helper + ~1 install handler modified, ~3 files
deleted (2 resources + 1 loader) + 2 csproj lines, ~1 config model trimmed, 1 settings writer added,
1 repo settings file added; ~4 rewritten `InitCommandTests` + ~3 net-new test files; **0 `Spectra.MCP`
changes**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ The execution skill and the allowlist are git-tracked files in the consuming repo. No external storage. |
| **II. Deterministic Execution** | ✅ The MCP engine — the deterministic state machine — is **reused verbatim, no change**. Server still owns all state; the agent stays stateless between turns. |
| **III. Orchestrator-Agnostic** | ✅ This *demonstrates* the principle: the same client-agnostic 25-tool server is driven by a different orchestrator with **zero server change**. The allowlist is purely client-side; the server enforces nothing. |
| **IV. CLI-First** | ✅ `init` / `update-skills` keep their exit-code contract; only the install target and an added settings-merge step change. No new chat loop. |
| **V. Simplicity (YAGNI)** | ✅✅ Net **reduction**: three redundant execution-agent copies → one; deletes a dead loader, two dead resources, two dead config fields, and a duplicate `.github/` install path. Removes complexity rather than adding it. |

**Result**: PASS — and unusually clean: this spec *removes* duplication and dead code while changing no
server behavior. No Complexity Tracking entries required (no violations to justify).

## Project Structure

### Documentation (this feature)

```text
specs/057-execution-agent-port/
├── plan.md              # This file
├── research.md          # Phase 0 output (D1–D6)
├── data-model.md        # Phase 1 output (artifact taxonomy — no persisted model)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── execution-skill.md         # de-Copilot + file-reads + verdict-pause contract
│   ├── install-and-consolidate.md # .claude/ relocation + delete duplicates contract
│   ├── mcp-allowlist.md           # .claude/settings.json permissions.allow contract
│   └── config-removal.md          # ExecutionConfig field removal + back-compat contract
├── checklists/
│   └── requirements.md  # (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Skills/
│   ├── Content/Agents/
│   │   └── spectra-execution.agent.md   # MODIFY — de-Copilot; remove Spaces tools+section; doc-lookup
│   │                                    #          → native file reads; translate terminal/confirm
│   │                                    #          discipline; PRESERVE verdict pause. Canonical copy.
│   ├── SkillInstallLayout.cs            # MODIFY — route spectra-execution.agent.md →
│   │                                    #          .claude/skills/spectra-execution/SKILL.md
│   └── ClaudeSettingsInstaller.cs       # NEW — pure, idempotent merge of mcp__spectra__* into
│                                        #        .claude/settings.json permissions.allow
├── Commands/Init/InitHandler.cs         # MODIFY — retire InstallAgentFilesAsync + Execution*Path
│                                        #          consts + log lines; call ClaudeSettingsInstaller
├── Agent/
│   ├── AgentResourceLoader.cs           # DELETE — only InitHandler used it
│   └── Resources/
│       ├── spectra-execution.agent.md   # DELETE — redundant duplicate copy
│       └── SKILL.md                     # DELETE — redundant duplicate copy
└── Spectra.CLI.csproj                   # MODIFY — remove the two Agent\Resources EmbeddedResource lines

src/Spectra.Core/
└── Models/Config/ExecutionConfig.cs     # MODIFY — remove CopilotSpace / CopilotSpaceOwner fields

.claude/settings.json                    # NEW (repo) — mcp__spectra__* allowlist (dogfood; distinct
                                         #              from settings.local.json's Bash(spectra-mcp:*))

tests/Spectra.CLI.Tests/
├── Commands/InitCommandTests.cs         # REWRITE — execution agent/skill tests → .claude/skills/
│                                        #           spectra-execution/SKILL.md; drop retired-path tests
└── Skills/
    ├── ExecutionAgentPortTests.cs       # NEW — de-Copilot (0 GPT-4o/Spaces/disable across the single
    │                                    #       copy), file-reads-replace-Spaces, verdict-pause guardrails
    └── McpAllowlistTests.cs             # NEW — ClaudeSettingsInstaller merge idempotency; mcp__spectra__*
                                         #       present + distinct from Bash(spectra-mcp:*)

tests/Spectra.Core.Tests/
└── (ExecutionConfig back-compat) ...    # NEW/EXTEND — legacy config with execution.copilot_space still
                                         #               deserializes (unknown keys ignored)

# NO CHANGES anywhere under src/Spectra.MCP/ or tests/Spectra.MCP.Tests/ (regression net).
```

**Structure Decision**: Single-project layout, no new project, **no MCP change**. The work is
concentrated in the one canonical execution-agent file, the install layout/handler, a small settings
writer, the config model, and the csproj — plus deletions of the redundant duplicates. The execution
agent becomes a main-session `.claude/skills/spectra-execution/SKILL.md` (mirroring how the generation
agent became a skill in the preceding spec); the critic stays a subagent and the authoring skills stay
where the preceding spec put them. The `Spectra.MCP` server and its test corpus are the untouched
regression net.

## Complexity Tracking

*No entries — this spec introduces no constitutional violations. It is a net reduction in complexity
(three execution-agent copies collapse to one; a dead loader, two dead resources, two dead config
fields, and a duplicate install path are removed), with zero server-side change.*
