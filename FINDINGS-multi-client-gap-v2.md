# Findings v2: multi-agent install gap (re-verified on main after #47 and #58)

**Date:** 2026-10-27 · **Base:** `61ea574` (main after #47, #58, #61, #62) · **Supersedes:** `FINDINGS-multi-client-gap.md`
(2026-06-18) · **Mode:** read-only investigation · **Tags:** CONFIRMED (file:line or doc URL) | INFERRED

## 1. What changed since v1 (2026-06-18)

| v1 said | Now | Tag |
|---|---|---|
| 15 skills | **16**: `spectra-review-flagged` was added (and it has **no `name:`** frontmatter key — the only one) | CONFIRMED `Skills/Content/Skills/spectra-review-flagged.md:1-4` |
| 1 agent (`spectra-critic`) | **6** subagents: analyst, critic, extractor, generator, repairer, updater (`*.agent.md`) | CONFIRMED `Skills/Content/Agents/` |
| no reference files | `spectra-generate/reference.md`, `spectra-update/reference.md` beside their SKILL.md | CONFIRMED `SkillInstallLayout.cs:16`, `InitHandler.cs:268-273` |
| critic frontmatter `model: claude-sonnet-4-6`, `disable-model-invocation: true`, `context: fork` | critic: `tools: [{{READONLY_TOOLS}}, Write]`, `model: {{CRITIC_MODEL}}` (rendered from `ai.critic.model`, default `sonnet`); the other five: `model: inherit`. **No `context: fork` / `disable-model-invocation` anywhere.** | CONFIRMED `Agents/*.agent.md` frontmatter, `AgentContent.cs:20-25` |
| "Task tool" wording, `{{GENERATE_TOOLS}}` = `…, Task` | Claude renamed Task → **Agent** (v2.1.63); `{{GENERATE_TOOLS}}` = `Read, Write, Edit, Bash, Glob, Grep, Agent`; prose says "with the Agent tool" | CONFIRMED `SkillResourceLoader.cs:20-22` |
| Codex: AGENTS.md only, "no skill-directory convention" | Codex reads **`.agents/skills/<name>/SKILL.md`** (CWD → repo root); `name` + `description` required | CONFIRMED https://learn.chatgpt.com/docs/build-skills |
| Gemini CLI = the consumer Google agent | Consumer Gemini CLI was replaced by **Antigravity CLI (`agy`)**; Gemini CLI stays as the enterprise (Gemini Code Assist) agent | CONFIRMED https://antigravity.google/docs/cli/gcli-migration/ |
| Kiro, OpenCode, Cursor, Devin not covered | covered below | — |
| `.github/agents/` fallback in `SkillInstallLayout.AgentPath` | gone: every agent goes to `.claude/agents/` (one rule) | CONFIRMED `SkillInstallLayout.cs:19-21` |

## 2. What `spectra init` (no `--agent`) emits today — the Claude install

Pinned by `tests/Spectra.CLI.Tests/Golden/ClaudeInstallGoldenTests.cs` (40 files, `Golden/104/init-fresh.txt`).

- **Claude-owned:** `.claude/skills/<16 names>/SKILL.md`, `.claude/skills/{spectra-generate,spectra-update}/reference.md`,
  `.claude/agents/<6 names>.agent.md`, `.claude/settings.json` (merge of `Bash(spectra *)`, `Write(.spectra/**)`,
  `Edit(.spectra/**)` — `ClaudeSettingsInstaller.cs`).
- **Agent-neutral:** `spectra.config.json`, `docs/criteria/_criteria_index.yaml`, `docs/criteria/sample.criteria.yaml`,
  `.spectra/prompts/<5>.md`, `profiles/_default.yaml`, `USAGE.md`, `CUSTOMIZATION.md`, `templates/bug-report.md`,
  `.github/workflows/deploy-dashboard.yml`, `.gitignore` (marker-tracked patterns), `.spectra/skills-manifest.json`.
- The skills manifest (`.spectra/skills-manifest.json`, `{version, files{key→sha256}, gitignore_offered}`) records the
  Claude files **and** the neutral prompts/profile/USAGE/CUSTOMIZATION; `update-skills` reads it. Hashes are of the
  raw content (not line-ending-normalised) — `FileHasher.cs:14-18`.

## 3. Claude-specific elements in the emitted content

| Element | Where | Count |
|---|---|---|
| `tools: [...]` with Claude tool names (`Read, Write, Edit, Bash, Glob, Grep, Agent` / `Read, Grep, Glob, Bash`) | skill frontmatter (16), agent frontmatter (6) | 22 files |
| `model: inherit` / `model: {{CRITIC_MODEL}}` | agent frontmatter | 6 |
| missing `name:` (Agent Skills requires `name` = folder) | `spectra-review-flagged` | 1 |
| "with the Agent tool" (start a subagent) | generate, update, criteria skills; generator, repairer, updater agents | 14 hits / 6 files |
| "If you have no Agent tool, or your first Agent call fails…" (nesting fallback) | generator, repairer, updater | 3 |
| "with the Bash tool" | 15 files | 39 hits |
| "with the Write tool" / "the Write tool creates…" | 6 files | 7 hits |
| "with the Read tool", "Grep/Glob" | execute, generate | 3 + 4 |
| `.claude/skills/**` ("do not Glob `.claude/skills/**`") | generate, execute | 2 |
| "Claude Code" (product name in prose/description) | help, quickstart | 4 |
| `.claude/settings.json` (whole file format) | `ClaudeSettingsInstaller` | 1 |
| `context: fork`, `disable-model-invocation`, "Task tool", `CLAUDE.md`, `subagent_type`, `@agent-` | — | **0** (gone since v1) |

None of these is in the agent-neutral files. The `spectra` CLI commands the skills run are agent-neutral (CONFIRMED v1 §Q4).

## 4. Provider table (placement verified against official docs)

Ported from BELLATRIX Agent's `docs/support-matrix.json` / `ProviderEmitters.cs` (same names, aliases, auto-detect
markers), re-checked against each provider's docs on 2026-10-27. `S` = the shared fragment `.spectra/spectra.instructions.md`.

| Agent (`--agent`) | Instructions (always on) | Skills dirs read (native first) | Subagents | Project allowlist | Docs |
|---|---|---|---|---|---|
| `claude` | none today (unchanged) | `.claude/skills` | `.claude/agents/*.agent.md` | `.claude/settings.json` (today) | https://code.claude.com/docs/en/sub-agents |
| `copilot` | `.github/instructions/spectra.instructions.md` (`applyTo: "**"`) | `.github/skills`, `.claude/skills`, `.agents/skills` | `.github/agents/*.agent.md` (Copilot tool aliases; `agents:` allow-list); reuses `.claude/agents` when Claude is installed | none at project level → skipped | https://docs.github.com/en/copilot/concepts/agents/about-agent-skills · https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/create-custom-agents |
| `cursor` | `.cursor/rules/spectra.mdc` (`alwaysApply: true`) | `.cursor/skills`, `.agents/skills`, `.claude/skills`, `.codex/skills` | `.cursor/agents/*.md` (wins a clash with `.claude/agents`; `readonly` boolean, no tool list) | `.cursor/cli.json` `permissions.allow: ["Shell(spectra)"]` | https://cursor.com/docs/subagents · https://cursor.com/docs/cli/reference/permissions |
| `devin` (alias `windsurf`) | `.devin/rules/spectra.md` (`trigger: always_on`) | `.devin/skills`, `.github/skills` | `.devin/agents/*.md` (`allowed-tools`, documented names `read/grep/glob/exec` only) | `.devin/config.json` `permissions.allow: ["Exec(spectra)"]` | https://docs.devin.ai/cli/extensibility · https://docs.devin.ai/cli/extensibility/rules.md · https://docs.devin.ai/cli/subagents.md · https://docs.devin.ai/cli/reference/permissions.md |
| `codex` | `AGENTS.md` marker block → `S` | `.agents/skills` | `.codex/agents/*.toml` (`name`, `description`, `developer_instructions`) | none (sandbox/approval policy only) → skipped | https://learn.chatgpt.com/docs/build-skills · https://developers.openai.com/codex/subagents · https://developers.openai.com/codex/guides/agents-md |
| `gemini` (enterprise) | `GEMINI.md` marker block `@./S` | `.agents/skills`, `.gemini/skills` | `.gemini/agents/*.md` (`kind: local`, Gemini tool names) | workspace policies are documented as **no effect** → skipped | https://geminicli.com/docs/core/subagents/ · https://github.com/google-gemini/gemini-cli/blob/main/docs/cli/skills.md · https://geminicli.com/docs/reference/policy-engine |
| `antigravity` (alias `agy`) | `.agents/rules/spectra.md` (`trigger: always_on`; a rule without frontmatter is silently discarded) | `.agents/skills` | `.agents/agents/<name>/agent.md` (`tools` default `[]`; documented names only — an unknown name can hang the subagent) | none documented → skipped | https://antigravity.google/docs/rules/ · https://antigravity.google/docs/skills/ · https://antigravity.google/docs/subagents/ |
| `kiro` | `.kiro/steering/spectra.md` (`inclusion: always`) | `.kiro/skills` | `.kiro/agents/*.md` (`tools` category tags; custom agents load steering/skills only via `resources`) | per-agent only → skipped | https://kiro.dev/docs/custom-agents/creating/ · https://kiro.dev/docs/skills/ · https://kiro.dev/docs/tools/ |
| `opencode` | `AGENTS.md` marker block → `S` (shared with Codex) | `.opencode/skills`, `.agents/skills`, `.claude/skills` | `.opencode/agents/*.md` (`mode`, `permission`; file name = agent name) | `opencode.json` `permission.bash["spectra *"] = "allow"` | https://opencode.ai/v2/docs/agents/ · https://opencode.ai/docs/permissions/ |

Every provider documents a project-level skills **and** instructions mechanism, so no STOP condition applies.

**Forced delegation:** only Claude Code runs a subagent from a skill instruction as a platform-enforced, isolated step.
Everywhere else delegation is model-decided (Codex: only when asked; Gemini: `@name` or model choice; Copilot, Cursor,
Devin, Kiro, OpenCode, Antigravity: model-chosen). CONFIRMED v1 §Q3 + the subagent pages above.

## 5. Decisions (pre-approved; recorded)

1. **`--agent` omitted → claude, exactly as today**, plus one notice line naming the other agents. BELLATRIX's
   auto-detect (existing manifests, else exactly one detected agent) is available as **`--agent auto`**; it never guesses
   (none or several detected → usage error listing the choices).
2. **Claude path untouched.** The only change to existing code: `InitHandler.HandleAsync` gains `installClaude = true`,
   which guards the two Claude-only calls (`CreateBundledSkillFilesAsync`, `ClaudeSettingsInstaller`). Default = today.
3. **New emitter layer** (`src/Spectra.CLI/Install/`), used only for non-Claude agents; one emitter per provider, as in
   BELLATRIX. Claude itself is represented in the planner only to answer "is `.claude/skills` / `.claude/agents` present".
4. **Existing workspace:** `spectra init --agent <list>` on a workspace that already has `spectra.config.json` (and no
   `--force`) installs only the non-Claude agents and leaves config and Claude files alone (today `init` would refuse);
   `claude` in that list is reported as "managed by `spectra update-skills`". With `--force`, or on a fresh workspace,
   the full init runs first.
5. **Skills are provider-neutral** (one content for every non-Claude agent), because several providers share one
   directory (`.agents/skills`: Codex, Gemini, Antigravity, Cursor, OpenCode). Transform at emit time: drop the Claude
   `tools:` key, add `name:` when missing, neutral wording ("with the Agent tool" → "as a subagent", "with the Bash
   tool" → "in the shell", "the Write tool" → "your file-writing tool", "the Read tool" → "your file-reading tool",
   "Grep/Glob" → "search", `.claude/skills/**` → "the skills folder", "Claude Code" → "your AI coding agent").
6. **Subagents are per provider** (native format and tool names). All **6** subagents are emitted, not only the critic:
   the skills delegate to the analyst/generator/updater/repairer/extractor too. Body = the same neutral transform.
7. **The critic keeps the Write tool.** It writes its verdict to `.spectra/verdicts/critic-verdict-<id>.json`, which
   `ingest-verdict` reads (today's workflow). A strictly read-only critic would break that, so it gets the smallest
   documented set that keeps the write: read + search + shell + write. Consequences: Cursor gets **no** `readonly: true`
   (it blocks "file edits and state-changing shell commands"); Kiro `["read", "write", "shell"]`; Devin has no documented
   name for a write tool, so its agents stay unrestricted (BELLATRIX's rule: never list an undocumented name).
8. **`model:`** is dropped for every non-Claude agent (the value is a Claude alias). `ai.critic.model` stays Claude-only.
9. **Skill directory de-dup (set cover, as BELLATRIX):** `.claude/skills` first when Claude is selected or already
   installed, then greedily the directory read by most uncovered agents. An agent that would read two copies gets a
   warning. A reused `.claude/skills` keeps Claude's wording (it is Claude's file; the golden rule forbids changing it).
10. **Subagent reuse:** Copilot reuses `.claude/agents` when Claude is present (it reads them); Cursor always gets its
    own copy (native wins a clash); the others always get native copies.
11. **Devin skills dirs** are `.devin/skills`, `.github/skills` — not `.claude/skills` as in BELLATRIX: the Devin docs
    list only `.github/skills` as an import source for skills (subagents are imported from `.claude/`).
12. **Shared fragment** `.spectra/spectra.instructions.md` (Codex, Gemini, OpenCode); native fragments elsewhere. Same
    neutral body everywhere, including a short "how to start a subagent in each agent" table and the critic rule.
13. **Host files (R1/R3):** `AGENTS.md` / `GEMINI.md` get exactly one `<!-- >>> spectra >>> -->` block (distinct from
    BELLATRIX's `<!-- >>> bellatrix >>> -->`); bytes outside the block are never rewritten.
14. **Manifests:** `.spectra/manifest.<agent>.json` per non-Claude agent (path + CRLF-normalised SHA-256); a file shared by
    several agents is listed in each. `.spectra/skills-manifest.json` is not renamed or changed. A user-edited file is
    skipped and reported unless `--regenerate`.
15. **Permissions** only where a project-level allowlist is documented (Cursor, Devin, OpenCode), merged by key; others
    are skipped and `--dry-run` says so.
16. **`--dry-run` with `--agent`** writes nothing and prints the plan (every file per agent, decisions, skips). Without
    `--agent`, `--dry-run` keeps today's behaviour.
17. **`update-skills` is unchanged**; re-running `spectra init --agent <list>` refreshes non-Claude agents (hash-gated).
18. **Namespace (R1–R8):** only `spectra-*` skills/subagents, `spectra.*` fragments and `.spectra/manifest.*`; files of
    BELLATRIX (`bellatrix-*`, `.bellatrix/`) or anyone else are never read for writing, renamed or removed.

## 6. Known limitation (to document)

The critic's isolated-context guarantee is enforced by the platform only in Claude Code. In the other agents the critic
runs when the agent follows the skill instruction. A CLI-enforced critic gate is planned for a later version.
