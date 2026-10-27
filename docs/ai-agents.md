---
title: AI Agents
parent: User Guide
nav_order: 8.5
---

# Using SPECTRA with other AI agents

SPECTRA does not run a model of its own. Every analysis, generation, critic and update step runs inside the AI coding
agent you already use, driven by SPECTRA's skills and subagents. Claude Code is the default. From version 2.7.0,
`spectra init --agent` installs the same workflow for eight more agents: GitHub Copilot, Cursor, Devin, Codex CLI,
Gemini CLI, Antigravity CLI, Kiro CLI and OpenCode.

Related: [Getting Started](getting-started.md) | [Skills Integration](skills-integration.md) |
[CLI Reference](cli-reference.md#spectra-init) | [Live verification checklist](live-verification-agents.md)

---

## Quick start

```bash
dotnet tool install -g Spectra.CLI
spectra init --agent cursor            # pick your agent; comma-separated for several
```

Then open your agent in the repository and ask it, in plain language: **"Generate test cases for checkout"**. The
`spectra-generate` skill takes over: it analyses the documentation, asks you to confirm the plan, generates the tests in
batches and has the `spectra-critic` subagent verify every test against the documentation.

| You use | Install with |
|---|---|
| Claude Code | `spectra init` (or `--agent claude`) |
| GitHub Copilot | `spectra init --agent copilot` |
| Cursor | `spectra init --agent cursor` |
| Devin (formerly Windsurf) | `spectra init --agent devin` |
| Codex CLI | `spectra init --agent codex` |
| Gemini CLI | `spectra init --agent gemini` |
| Antigravity CLI | `spectra init --agent antigravity` |
| Kiro CLI | `spectra init --agent kiro` |
| OpenCode | `spectra init --agent opencode` |

The team can mix agents in one repository: `spectra init --agent claude,copilot,codex` installs all three, and the
test cases, criteria and coverage stay the same files whichever agent wrote them.

## Choosing agents

`--agent` takes one name or a comma-separated list:
`claude`, `copilot`, `cursor`, `devin`, `codex`, `gemini`, `antigravity`, `kiro`, `opencode`.
Two aliases are accepted: `windsurf` for `devin` (Windsurf is now Devin Desktop; `init` prints a notice) and `agy` for
`antigravity`. These are the same names BELLATRIX Agent uses, so one value works for both tools.

`--agent auto` picks the agents for you and never guesses:

1. If SPECTRA is already installed for some agents (Claude's skills, or a `.spectra/manifest.<agent>.json`), it picks
   exactly those. This is the easy way to refresh every installed agent after upgrading the CLI.
2. Otherwise it looks for each agent's own files in the repository and picks the agent only if exactly one is found:

   | Agent | Picked as | Detected by |
   |---|---|---|
   | Claude Code | `--agent claude` | `.claude/`, `CLAUDE.md` |
   | GitHub Copilot | `--agent copilot` | `.github/copilot-instructions.md`, `.github/agents/` |
   | Cursor | `--agent cursor` | `.cursor/` |
   | Devin | `--agent devin` | `.devin/`, `.windsurf/` |
   | Codex CLI | `--agent codex` | `.codex/` |
   | Gemini CLI | `--agent gemini` | `.gemini/`, `GEMINI.md` |
   | Antigravity CLI | `--agent antigravity` | `.agents/agents/`, `.agents/rules/`, `.agents/mcp_config.json`, `.agent/` |
   | Kiro CLI | `--agent kiro` | `.kiro/` |
   | OpenCode | `--agent opencode` | `opencode.json`, `opencode.jsonc`, `.opencode/` |

3. If none or several are found, it stops with exit code 2 and lists the names to pass with `--agent`.

Without `--agent`, `spectra init` installs for Claude Code exactly as in earlier versions and prints one line naming
the other agents.

## Fresh or existing repository

- **New repository** (no `spectra.config.json`): `spectra init --agent <list>` runs the standard setup first (config,
  `docs/`, `test-cases/`, prompt templates, profiles, the dashboard workflow), then installs each agent. The Claude
  Code files are written only when `claude` is in the list.
- **Already initialised:** `spectra init --agent <list>` adds the agents and leaves `spectra.config.json` and the Claude
  Code files alone; no `--force` is needed. `claude` in the list is reported as "already initialised" (refresh it with
  `spectra update-skills`).
- **`--force`** re-runs the full setup, as it always did, and then installs the agents.
- **`--skip-skills`** sets up the workspace only and installs no agent files.

## What is installed for each agent

Every agent gets the same three things, in its own layout:

- **The 16 `spectra-*` skills** (with the generate and update reference files): the step-by-step workflows for
  generating, updating, criteria, coverage, the dashboard, execution and the rest.
- **Six subagents:** `spectra-analyst`, `spectra-generator`, `spectra-updater`, `spectra-repairer`, `spectra-extractor`
  and `spectra-critic`.
- **One always-on instructions file:** what SPECTRA is, that every generated test is verified by `spectra-critic`, which
  folders only `spectra` commands may write, and how to start a subagent in that agent.

| Agent | Always-on instructions | Skills | Subagents | `spectra` command allowlist |
|---|---|---|---|---|
| Claude Code | (none) | `.claude/skills/` | `.claude/agents/*.agent.md` | `.claude/settings.json` |
| GitHub Copilot | `.github/instructions/spectra.instructions.md` (`applyTo: "**"`) | `.github/skills/` (reuses `.claude/skills/`) | `.github/agents/*.agent.md` (reuses `.claude/agents/`) | (none documented) |
| Cursor | `.cursor/rules/spectra.mdc` (`alwaysApply`) | `.cursor/skills/` (reuses `.claude/skills/` or `.agents/skills/`) | `.cursor/agents/*.md` | `.cursor/cli.json`: `Shell(spectra)` |
| Devin | `.devin/rules/spectra.md` (`always_on`) | `.devin/skills/` | `.devin/agents/*.md` | `.devin/config.json`: `Exec(spectra)` |
| Codex CLI | SPECTRA block in `AGENTS.md`, pointing to `.spectra/spectra.instructions.md` | `.agents/skills/` | `.codex/agents/*.toml` | (none documented) |
| Gemini CLI | SPECTRA block in `GEMINI.md`, importing `.spectra/spectra.instructions.md` | `.agents/skills/` | `.gemini/agents/*.md` | (no effect at project level) |
| Antigravity CLI | `.agents/rules/spectra.md` (`always_on`) | `.agents/skills/` | `.agents/agents/<name>/agent.md` | (none documented) |
| Kiro CLI | `.kiro/steering/spectra.md` (`inclusion: always`) | `.kiro/skills/` | `.kiro/agents/*.md` | (per agent only) |
| OpenCode | SPECTRA block in `AGENTS.md`, pointing to `.spectra/spectra.instructions.md` | `.opencode/skills/` (reuses `.claude/skills/` or `.agents/skills/`) | `.opencode/agents/*.md` | `opencode.json`: `"spectra *": "allow"` |

Commit these files with the repository, the same way you commit `.claude/`, so everyone on the team gets the same
skills.

### How the content is adapted

The skill and subagent instructions are the same text Claude Code gets. Only the Claude-specific parts change when they
are written for another agent:

- Wording: "with the Agent tool" becomes "as a subagent", "with the Bash tool" becomes "in the shell", "the Write tool"
  becomes "your file-writing tool", and so on.
- Frontmatter: each subagent gets the fields and tool names that agent documents. A tool name an agent does not
  document is never written; in that case the subagent keeps the agent's default tools.
- Claude model names (`model: sonnet`) are left out; the other agents use their own model selection.
  `ai.critic.model` in `spectra.config.json` applies to Claude Code only.
- The critic keeps the tools it has in Claude Code, including writing its verdict file
  (`.spectra/verdicts/critic-verdict-<id>.json`), which `spectra ai ingest-verdict` reads. That is why the critic is
  not marked read-only in Cursor and gets `read`, `write` and `shell` in Kiro.

The Claude Code install itself is never changed by `--agent`: same files, same content, same `.claude/settings.json`.

### Shared folders are written once

Several agents read the same folders. `.agents/skills/` is read by Codex, Gemini, Antigravity, Cursor and OpenCode,
and `AGENTS.md` by Codex and OpenCode. `init` writes the fewest skill folders that reach every selected agent: it
reuses `.claude/skills/` when Claude Code is installed, then picks the folder most of the remaining agents read. The
`decision:` lines in the output say which folder each agent uses.

When no layout avoids it, an agent may see each skill twice (for example Cursor with both Claude Code and Codex
installed). `init` prints a `warning:` line; the copies are identical, so this is cosmetic.

## Re-running, upgrading and your edits

Each agent has a manifest, `.spectra/manifest.<agent>.json`, with the path and a hash of every file SPECTRA wrote for it.
Re-running the command uses it:

```text
[spectra init] codex (Codex CLI): 0 written, 25 unchanged, 0 skipped (edited by you; --regenerate overwrites), 0 removed — manifest .spectra/manifest.codex.json
  instructions: .spectra/spectra.instructions.md
  AGENTS.md: SPECTRA block unchanged
```

- A file SPECTRA wrote and you did not change is refreshed when a newer version has new content.
- A file you edited is **skipped** and listed; pass `--regenerate` to overwrite it with the bundled version.
- A file a newer version no longer installs is removed, unless you edited it.
- A file already at that path that SPECTRA did not write is skipped the same way; `--regenerate` overwrites it too.

After upgrading the CLI, refresh everything:

```bash
dotnet tool update -g Spectra.CLI
spectra update-skills                  # the Claude Code install
spectra init --agent auto              # every other agent already installed
```

`spectra update-skills` refreshes only the Claude Code files.

### Preview with `--dry-run`

`spectra init --agent kiro --dry-run` writes nothing. It prints, per agent, every file it would write, leave unchanged,
skip or remove, plus the decisions and warnings. Use it before installing into a repository that already has agent
files of its own.

## Files that belong to you

- **`AGENTS.md` and `GEMINI.md`:** SPECTRA adds one block between `<!-- >>> spectra >>> -->` and
  `<!-- <<< spectra <<< -->` and never changes anything outside it. Edit the rest of the file freely.
- **Settings files** (`.cursor/cli.json`, `.devin/config.json`, `opencode.json`): the `spectra` allowlist entry is
  merged in by key; values you already set are kept. A settings file with comments (JSONC) is left alone; `init` says
  so, and you can add the entry by hand.
- **Names:** SPECTRA writes only `spectra-*` skills and subagents, `spectra.*` instruction files and
  `.spectra/manifest.*.json`. It never renames or removes anything else.

### Next to BELLATRIX Agent

BELLATRIX Agent installs into the same agent folders with its own names (`bellatrix-*`, `.bellatrix/`, its own
`<!-- >>> bellatrix >>> -->` block). Both can be installed in either order and re-run any number of times; neither
changes the other's files. A typical setup for a team that writes manual tests with SPECTRA and automates them with
BELLATRIX:

```bash
spectra init --agent claude,copilot
bellatrix init --agent claude,copilot --language csharp --domains web
```

## Removing an agent

There is no uninstall command. To remove SPECTRA from one agent, delete the files listed in
`.spectra/manifest.<agent>.json` (skip any that another agent's manifest also lists, such as a shared `.agents/skills/`
folder), delete the SPECTRA block from `AGENTS.md` or `GEMINI.md` if that agent used one, and then delete the manifest.

## Known limitation: the critic outside Claude Code

In Claude Code the skill starts `spectra-critic` as an isolated subagent, and the platform enforces that step. The other
agents decide for themselves when to delegate (Codex only when asked; Gemini on `@name` or by its own choice), so there
the critic runs when the agent follows the skill's instruction to start it.

To check that every test was verified:

```bash
spectra ai audit-grounding --suite checkout
```

Every new test should show a verdict (`grounded`, `partial` or `hallucinated`) and no `critic` under `action_needed`.
If the critic was skipped, ask the agent to run it: "Run spectra-critic for the tests without a verdict" (in Gemini CLI
and OpenCode, `@spectra-critic`). Do not write verdict files by hand. A critic gate enforced by the CLI is planned for a
later version.

## Troubleshooting

| You see | Meaning | Do this |
|---|---|---|
| `Unknown --agent 'x'` | Not a supported name | Use one of the names above (or `auto`) |
| `No AI agent detected` | `--agent auto` found no agent files | Pass `--agent <name>` |
| `Several AI agents detected (…)` | `--agent auto` will not guess | Pass the ones you want, as the message shows |
| `skipped (edited by you)` | You changed a file SPECTRA wrote | Keep your edit, or add `--regenerate` |
| `allowlist: skipped — …` | The settings file has comments or is not plain JSON | Add the entry from the table by hand |
| `warning: … may list each SPECTRA skill twice` | Two installed agents share no single skills folder | Nothing; the copies are identical |
| The agent does not use the skills | The agent did not load the folder | Restart the agent session in the repository; check its skills list |

To check an installation step by step in a real session, follow the
[live verification checklist](live-verification-agents.md).
