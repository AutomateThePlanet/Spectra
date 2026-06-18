# Findings: Multi-Client Agent/Skill Emission Gap

**Date:** 2026-06-18  
**Repo:** `C:\SourceCode\Spectra`  
**Mode:** Read-only. No code edited. Web-sourced client conventions cited by URL.  
**Tags:** CONFIRMED (file:line or cited URL) | INFERRED

---

## Q1 — What `spectra init` emits today (exhaustive inventory)

### A. Claude-specific assets

#### Skills — `.claude/skills/`
Written by `InitHandler.CreateBundledSkillFilesAsync` (`InitHandler.cs:252–276`) via `SkillInstallLayout.SkillPath(root, name)` = `Path.Combine(root, ".claude", "skills", skillName, "SKILL.md")` (`SkillInstallLayout.cs:13`). CONFIRMED.

15 skill files installed:

| Install path | Skill key |
|---|---|
| `.claude/skills/spectra-generate/SKILL.md` | spectra-generate |
| `.claude/skills/spectra-update/SKILL.md` | spectra-update |
| `.claude/skills/spectra-coverage/SKILL.md` | spectra-coverage |
| `.claude/skills/spectra-dashboard/SKILL.md` | spectra-dashboard |
| `.claude/skills/spectra-validate/SKILL.md` | spectra-validate |
| `.claude/skills/spectra-list/SKILL.md` | spectra-list |
| `.claude/skills/spectra-init-profile/SKILL.md` | spectra-init-profile |
| `.claude/skills/spectra-help/SKILL.md` | spectra-help |
| `.claude/skills/spectra-criteria/SKILL.md` | spectra-criteria |
| `.claude/skills/spectra-docs/SKILL.md` | spectra-docs |
| `.claude/skills/spectra-prompts/SKILL.md` | spectra-prompts |
| `.claude/skills/spectra-quickstart/SKILL.md` | spectra-quickstart |
| `.claude/skills/spectra-delete/SKILL.md` | spectra-delete |
| `.claude/skills/spectra-suite/SKILL.md` | spectra-suite |
| `.claude/skills/spectra-execute/SKILL.md` | spectra-execute |

#### Agents — `.claude/agents/`
Same method, via `SkillInstallLayout.AgentPath(root, name)` (`SkillInstallLayout.cs:19–25`). CONFIRMED.

| Install path | Source file |
|---|---|
| `.claude/agents/spectra-critic.agent.md` | spectra-critic.agent.md |

Any other agent filename falls through to `.github/agents/<name>` (line 24). Currently no other agents exist — the generation and execution agents were merged into their flow skills.

#### Settings — `.claude/settings.json`
Written by `ClaudeSettingsInstaller.EnsureInstalledAsync` (`InitHandler.cs:83`, `ClaudeSettingsInstaller.cs:23–68`). Idempotent merge into `permissions.allow` of exactly three entries: `"Bash(spectra *)"`, `"Write(.spectra/**)"`, `"Edit(.spectra/**)"`. **No `mcp__spectra__*` entries.** CONFIRMED (`ClaudeSettingsInstaller.cs:17–21`; test `Init_SettingsJson_HasNoMcpEntry` at `InitClaudeSettingsTests.cs:74–82`).

#### `VsCodeMcpConfigInstaller` — CONFIRMED GONE
No class or reference found anywhere in `src/`. Removed in Spec 070. CONFIRMED.

---

#### Portability assessment of Claude-specific skill content

| Asset | Claude-specific constructs | Portable as-is? |
|---|---|---|
| 12 of 15 skill SKILL.md files | `tools: [{{GENERATE_TOOLS}}]` resolves to `"Read, Write, Edit, Bash, Glob, Grep, Task"` — Claude Code tool names | Prose portable; **tool names are client-specific** |
| `spectra-generate.md` | Line 193: `"Invoke it with the Task tool"` — Claude Code Task tool; line 29: `"do not Glob .claude/skills/**"` — hardcoded Claude path | **Content not portable** without transform |
| `spectra-execute.md` | Line 19: `"do not Glob .claude/skills/**"` | **Partial portability** (minor fix) |
| `spectra-critic.agent.md` | Frontmatter: `model: claude-sonnet-4-6`, `disable-model-invocation: true`, `context: fork` — all Claude Code-specific frontmatter keys with no cross-client equivalent | **Not portable** — Claude-only primitives |
| `.claude/settings.json` | Entire file format is Claude Code-specific | **Not portable** — each client has its own config format |

---

### B. Client-neutral assets

CONFIRMED from `InitHandler.cs` call order:

| Path | Method / line | Notes |
|---|---|---|
| `docs/` (dir) | `CreateDirectoriesAsync` line 163 | |
| `test-cases/` (dir) | same, line 165 | |
| `docs/criteria/` (dir) | same, line 167 | |
| `templates/` (dir) | same, line 168 | |
| `docs/criteria/_criteria_index.yaml` | `CreateAcceptanceCriteriaTemplateAsync` line 186 | skip-if-exists |
| `docs/criteria/sample.criteria.yaml` | same, line 200 | skip-if-exists |
| `spectra.config.json` | `CreateConfigFileAsync` line 245 | from embedded template |
| `.spectra/skills-manifest.json` | `SkillsManifestStore.SaveAsync` (inside CreateBundledSkillFilesAsync) | hash manifest |
| `USAGE.md` | `CreateUsageGuideAsync` line 279 | skip-if-exists unless force |
| `.spectra/prompts/behavior-analysis.md` | `CreatePromptTemplatesAsync` line 302 | 5 templates |
| `.spectra/prompts/test-generation.md` | same | |
| `.spectra/prompts/criteria-extraction.md` | same | |
| `.spectra/prompts/critic-verification.md` | same | |
| `.spectra/prompts/test-update.md` | same | |
| `profiles/_default.yaml` | `CreateDefaultProfileAndCustomizationAsync` line 332 | skip-if-exists unless force |
| `CUSTOMIZATION.md` | same, line 356 | skip-if-exists unless force |
| `templates/bug-report.md` | `CreateBugReportTemplateAsync` line 232 | skip-if-exists |
| `.gitignore` (appended patterns) | `UpdateGitIgnoreAsync` line 374 | idempotent |
| `docs/_index/_manifest.yaml` (+ group files) | `BuildInitialIndexAsync` line 130 | best-effort; only if docs/*.md exist |

`.gitignore` patterns added (`InitHandler.cs:382–390`): `.execution/*.db`, `.execution/*.db-*`, `.spectra.lock`, `.spectra/.pid`, `.spectra/.cancel`, `.spectra/id-allocator.lock`, `.spectra/id-allocator.json`.

---

### C. Other-client assets

| Path | Source | Tag |
|---|---|---|
| `.github/workflows/deploy-dashboard.yml` | `CreateDeployWorkflowAsync` line 509; constant `DeployWorkflowPath` line 25 | CONFIRMED |
| `.github/agents/<name>` | `SkillInstallLayout.AgentPath` fallback line 24 — no agents currently reach this path | CONFIRMED (path exists; no files emitted) |

**Nothing** is emitted under `.codex/`, `.gemini/`, or `.copilot/`. CONFIRMED — no such paths in `InitHandler.cs` or `SkillInstallLayout.cs`.

---

## Q2 — Canonical source of skills/agents

### Source locations

CONFIRMED:
- Skills: `src/Spectra.CLI/Skills/Content/Skills/*.md` — 15 files (all current skills)
- Agents: `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md` — 1 file
- The deleted `spectra-generation.agent.md` and `spectra-execution.agent.md` appear as `D` in git status (merged into flow skills per skill-pair-merge plan).

### Loading mechanism: EmbeddedResource

CONFIRMED (`SkillResourceLoader.cs:10–97`):
- Skills: scans assembly for resources with prefix `Spectra.CLI.Skills.Content.Skills.` (line 11), strips `.md` suffix.
- Agents: scans for prefix `Spectra.CLI.Skills.Content.Agents.` (line 12), keeps filename as key.
- Loaded via `assembly.GetManifestResourceStream()` (line 85) — not `File.ReadAllText`. Not a file copy.

### Copy-vs-transform: ONE transform (placeholder replacement)

Content is **not** copied verbatim. `SkillResourceLoader.LoadAndReplace` performs placeholder substitution before install:

| Placeholder | Replacement value |
|---|---|
| `{{GENERATE_TOOLS}}` / `{{GENERATION_TOOLS}}` | `"Read, Write, Edit, Bash, Glob, Grep, Task"` |
| `{{READONLY_TOOLS}}` | `"Read, Grep, Glob, Bash"` |
| `{{EXECUTION_TOOLS}}` | long VS Code tool list — vestigial, not used by any current skill |

No other transforms occur. `InstallAgentFileAsync` (`InitHandler.cs:529`) receives the already-resolved string and writes it as-is.

**This placeholder layer is exactly where a multi-target compiler would hook**: the `{{GENERATE_TOOLS}}` value would become client-specific.

### Degree of `.claude/` hardcoding: HIGH

CONFIRMED hardcoded literal `.claude` strings at:
- `SkillInstallLayout.cs:13`: `Path.Combine(root, ".claude", "skills", skillName, "SKILL.md")`
- `SkillInstallLayout.cs:21`: `Path.Combine(root, ".claude", "agents", "spectra-critic.agent.md")`
- `ClaudeSettingsInstaller.cs:25`: `Path.Combine(projectRoot, ".claude", "settings.json")`

**No `TargetClient` enum, no `IInstallLayout` interface, no path factory abstraction exists.** The routing is a single `switch` on agent filename (`SkillInstallLayout.cs:19–25`) and a single hardcoded skill path. The entire install surface is three literal strings.

---

## Q3 — Current client conventions (web-sourced, 2026)

### OpenAI Codex CLI

| Dimension | Convention | Tag |
|---|---|---|
| Skills/instructions | `AGENTS.md` files. Hierarchy: `~/.codex/AGENTS.md` → repo root `AGENTS.md` → subdirectory `AGENTS.md` (concatenated root-to-leaf). **No skill-directory convention** — flat file, not `<name>/SKILL.md` directories. | CONFIRMED — [developers.openai.com/codex/guides/agents-md](https://developers.openai.com/codex/guides/agents-md) |
| Subagents | `.codex/agents/*.toml` (project) or `~/.codex/agents/*.toml` (personal). TOML format with `name`, `description`, `developer_instructions` required fields; optional `model`, sandbox, MCP servers. | CONFIRMED — [developers.openai.com/codex/subagents](https://developers.openai.com/codex/subagents) |
| MCP config | `.codex/config.toml` (project, trusted repos) or `~/.codex/config.toml` (global). MCP servers under `[mcp_servers.<id>]` tables. | CONFIRMED — [developers.openai.com/codex/mcp](https://developers.openai.com/codex/mcp) |
| **Forced subagent invocation** | **No.** "Codex only spawns a subagent when you explicitly ask it to do so." `AGENTS.md` has no mechanism to mandate a subagent call. `agents.max_depth` controls nesting ceiling, not forced invocation. | CONFIRMED — [developers.openai.com/codex/subagents](https://developers.openai.com/codex/subagents) |

### Google Gemini CLI

| Dimension | Convention | Tag |
|---|---|---|
| Instructions | `GEMINI.md` files. Hierarchy: `~/.gemini/GEMINI.md` → project root `GEMINI.md` → ancestor directories up to `.git`. All concatenated. Filename configurable via `context.fileName` in settings (can alias to `AGENTS.md`). | CONFIRMED — [google-gemini.github.io/gemini-cli/docs/cli/gemini-md.html](https://google-gemini.github.io/gemini-cli/docs/cli/gemini-md.html) |
| Skills | `~/.gemini/skills/` (user) or `.gemini/skills/` (project). Alias: `~/.agents/skills/` or `.agents/skills/`. Does **NOT** include `.claude/skills`. Each skill is a subdirectory with a `SKILL.md` file (same format as Claude Code / Copilot). | CONFIRMED — [github.com/google-gemini/gemini-cli/blob/main/docs/cli/skills.md](https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/skills.md) |
| Subagents | `.gemini/agents/*.md` (project) or `~/.gemini/agents/*.md` (user). Markdown with mandatory YAML frontmatter: `name`, `description`, `kind`, `tools`, `model`, `temperature`, `max_turns`. | CONFIRMED — [geminicli.com/docs/core/subagents/](https://geminicli.com/docs/core/subagents/) |
| MCP config | `~/.gemini/settings.json` (global) or `.gemini/settings.json` (project). Key: `mcpServers`. JSON format. | CONFIRMED — [geminicli.com/docs/tools/mcp-server/](https://geminicli.com/docs/tools/mcp-server/) |
| **Forced subagent invocation** | **No.** Model-decided by default (model matches task to subagent description). In-session `@subagent-name` syntax only. `GEMINI.md` has no mechanism to mandate a subagent call from a skill body. | CONFIRMED — [geminicli.com/docs/core/subagents/](https://geminicli.com/docs/core/subagents/) |

### GitHub Copilot CLI

| Dimension | Convention | Tag |
|---|---|---|
| Instructions | `.github/copilot-instructions.md` (repo-wide), `.github/instructions/*.instructions.md` (path-specific). Also reads `AGENTS.md`, `CLAUDE.md`, `GEMINI.md` at repo root. Personal: `~/.copilot/copilot-instructions.md`. | CONFIRMED — [docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions) |
| **Skills** | Project: `.github/skills/`, **`.claude/skills/`**, or `.agents/skills/` — **all three are official**. Personal: `~/.copilot/skills/` or `~/.agents/skills/`. Each skill is a subdirectory with `SKILL.md` and YAML frontmatter (`name`, `description`, optional `license`, `allowed-tools`). | CONFIRMED — [docs.github.com/en/copilot/concepts/agents/about-agent-skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills) |
| Subagents | `.github/agents/<name>.agent.md` with YAML frontmatter (`name`, `description`, `tools`, `mcp-servers`, `model`, `target`). | CONFIRMED — [docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents) |
| MCP config | `~/.copilot/mcp-config.json` (user-level, primary). `.github/mcp.json` (workspace, after folder trust). Also `.copilot/mcp-config.json` at project root. | CONFIRMED — [docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers) |
| **Forced subagent invocation** | **No.** "Copilot will decide when to use your skills based on your prompt and the skill's description." No SKILL.md frontmatter field forces mandatory invocation. The `agents` field controls which agents are *available*, not which are *forced*. Per-session `/skill-name` is user-typed, cannot be encoded as always-on in a file. | CONFIRMED — [docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills](https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills) |

### Key finding: Does Copilot CLI read `.claude/skills`?

**Yes — CONFIRMED.** Official GitHub Docs quote:

> "Copilot supports: Project skills, stored in your repository (`.github/skills`, `.claude/skills`, or `.agents/skills`)"
> — [docs.github.com/en/copilot/concepts/agents/about-agent-skills](https://docs.github.com/en/copilot/concepts/agents/about-agent-skills)

**However:** Known bug [copilot-cli#1080](https://github.com/github/copilot-cli/issues/1080) — CLI binary fails to discover skills from `.claude/skills` in some environments. VS Code Copilot Chat and Claude Code work fine; the CLI binary is inconsistent. Resolution status unclear. Practical implication: emit to `.claude/skills/` remains the primary path but `.github/skills/` may be needed as a fallback for reliable Copilot CLI detection.

### Forced-invocation comparison

| Client | Mandatory subagent from file? | Mechanism |
|---|---|---|
| **Claude Code** | **Yes** | `context: fork` in `.claude/agents/<name>.agent.md`; skill body instructs model to always call via Task tool; model follows as sequential workflow step |
| OpenAI Codex CLI | **No** | Model-decided only; `AGENTS.md` has no mandate syntax |
| Google Gemini CLI | **No** | Model-decided; in-session `@name` only; GEMINI.md cannot mandate |
| GitHub Copilot CLI | **No** | Model-decided; no SKILL.md field enforces mandatory; `/skill-name` is per-session user syntax |

---

## Q4 — Gap quantified

### Portable as-is (same file, different folder)

**Copilot CLI reads `.claude/skills/` — zero additional emit required for skills.**  
SPECTRA's 15 skill SKILL.md files are already being picked up by Copilot CLI (via its official `.claude/skills/` read path) with no init changes. The skills install path is already the Copilot-compatible path.

The content portability gaps are localized:

| Issue | Affected files | Scope |
|---|---|---|
| Claude tool names in `{{GENERATE_TOOLS}}` / `{{READONLY_TOOLS}}` | All skills using those placeholders | Placeholder replacement (1 line per client in `SkillResourceLoader`) |
| `.claude/skills/**` path references in prose | `spectra-generate.md:29`, `spectra-execute.md:19` | Minor wording fix in 2 skill files |
| `Task tool` invocation for critic | `spectra-generate.md:193` | Architecture problem — see blockers below |

**Gemini CLI** requires a new emit path (`.gemini/skills/`) — `init` would need to write the same content to a second location.

**Codex CLI** uses a completely different format (`AGENTS.md` flat file, not skill directories). It cannot consume `.claude/skills/` or `.gemini/skills/`. Support would require generating a single concatenated `AGENTS.md` from all skill bodies — a different output shape, not just a folder change.

### Needs per-client transformation

| Asset | What's needed | Effort |
|---|---|---|
| `{{GENERATE_TOOLS}}` placeholder | Per-client tool name mapping (Copilot: Copilot tools; Gemini: Gemini tools; Codex: Codex tools) | Add to `SkillResourceLoader` — low effort per client |
| `spectra-critic.agent.md` | No other client has `context: fork` + `disable-model-invocation`. A Copilot equivalent would go in `.github/agents/` but with different frontmatter keys and no forced-invocation guarantee | Partial portability — structure works, guarantees don't |
| `.claude/settings.json` | Each client has its own permissions/settings format | Per-client installer class (same pattern as `ClaudeSettingsInstaller`) — isolated, low risk |
| Codex `AGENTS.md` | Requires a separate concatenation pass over skill bodies into a single flat file | New emit mode — moderate effort |

### Minimal change to add one additional client (Copilot CLI)

Since Copilot CLI already reads `.claude/skills/`, the skills are already working (modulo bug #1080). The genuinely minimal delta:

1. **Fix `{{GENERATE_TOOLS}}` for Copilot**: add a Copilot-specific tool name set to the placeholder map — this affects all skills at once.
2. **Remove `.claude/skills/**` path mentions** from `spectra-generate.md` and `spectra-execute.md` prose (2 line edits — these are misleading to non-Claude clients).
3. **Emit `.github/agents/spectra-critic.agent.md`** as a Copilot subagent: the `.github/agents/` fallback path already exists in `SkillInstallLayout.AgentPath` (line 24). Add a new `switch` case for a Copilot-format critic file, or emit a stripped version without Claude-specific frontmatter.
4. **No `.claude/settings.json` equivalent for Copilot** is needed for skills to work — settings.json is only for suppressing Claude Code approval dialogs.

Estimated init changes: `SkillInstallLayout.cs` (1 new switch case), `SkillResourceLoader.cs` (1 new placeholder value per client), 2 skill prose edits, 1 new agent file for `.github/agents/`. No new installer class needed unless Copilot permissions config is desired.

### Real blockers for non-Claude quality

| Blocker | Real? | Evidence |
|---|---|---|
| **Forced critic invocation** | **Yes — hard blocker for quality parity.** No other client has a `context: fork`-equivalent mandatory subagent mechanism. The critic gate on non-Claude clients becomes advisory: the model may follow the skill's prose instruction to run the critic, but nothing enforces it. The dual-model grounding guarantee is Claude Code-only. | CONFIRMED — Q3 findings; all three non-Claude clients are model-decided |
| **Tool name mismatch** | **Yes — but solvable.** `{{GENERATE_TOOLS}}` resolves to Claude tool names. Other clients use different tool APIs. | Solvable via placeholder; `SkillResourceLoader.cs` already has the hook |
| **Billing model for Copilot CLI** | **Not a blocker.** Skills run in the user's paid Copilot session — no separate SPECTRA billing concern. | INFERRED |
| **CLI seam assumption (`spectra ai …`)** | **Not a blocker.** All skills call `spectra` CLI commands which are client-agnostic (`Bash` tool by any name). As long as the global tool is installed, non-Claude clients work the same way. | CONFIRMED — skill bodies invoke `spectra` commands, not Claude-specific APIs |
| **Copilot CLI bug #1080 (skill discovery)** | **Soft blocker.** Official path works; CLI bug may require `.github/skills/` dual-emit as a fallback. Affects CLI only (not VS Code Copilot Chat). | CONFIRMED bug exists; resolution unclear |

### Dependency on other open work

**Multi-client support is independent of all other open work.** It does not depend on:
- Seam-polish (critic invocation / verdict file cleanup) — orthogonal
- Console UX fixes — orthogonal
- Skill-pair-merge (already done in current working tree) — complete

The only prerequisite is that skill content is stable enough to not require parallel client-specific rewrites. Given the current skill set is post-merge and post-seam-polish-brief, the content surface is settling.

### Go/no-go summary

| Scope | Assessment |
|---|---|
| **Copilot CLI (minimal)** | Largely works today via `.claude/skills/` read. Incremental polish (tool names, critic agent format, bug #1080 fallback) — low effort, spec-sized but small. |
| **Gemini CLI** | New emit path required (`.gemini/skills/`). Subagent format portable with minor frontmatter changes. No forced-critic — quality degraded. Moderate effort. |
| **Codex CLI** | Incompatible format (AGENTS.md flat file). Full new emit path and concatenation pass. No subagent directory convention. No forced-critic. High effort, low quality ceiling. |
| **Critic quality gate on non-Claude** | Cannot be made mandatory on any non-Claude client from a configuration file. Users of non-Claude clients get a soft-suggestion critic, not a hard gate. This is a design-level gap, not an implementation gap — no amount of code fixes it. |

The spec (if pursued) should cover Copilot CLI first (smallest gap, dual-client claim already partially true), treat Gemini as stretch, and explicitly document the critic-gate quality degradation as a known limitation for all non-Claude clients. Spec number must come from the repo auto-detect script.

---

## Sources

- OpenAI Codex CLI AGENTS.md: https://developers.openai.com/codex/guides/agents-md
- OpenAI Codex CLI subagents: https://developers.openai.com/codex/subagents
- OpenAI Codex CLI MCP: https://developers.openai.com/codex/mcp
- Gemini CLI GEMINI.md: https://google-gemini.github.io/gemini-cli/docs/cli/gemini-md.html
- Gemini CLI skills: https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/skills.md
- Gemini CLI subagents: https://geminicli.com/docs/core/subagents/
- Gemini CLI MCP: https://geminicli.com/docs/tools/mcp-server/
- GitHub Copilot — About agent skills (`.claude/skills` listed): https://docs.github.com/en/copilot/concepts/agents/about-agent-skills
- GitHub Copilot — Adding skills: https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-skills
- GitHub Copilot — Custom instructions: https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-custom-instructions
- GitHub Copilot — Custom agents: https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents
- GitHub Copilot — MCP servers: https://docs.github.com/en/copilot/how-tos/copilot-cli/customize-copilot/add-mcp-servers
- Copilot CLI skill discovery bug: https://github.com/github/copilot-cli/issues/1080
