---
title: Use other AI agents
parent: Guides
nav_order: 7
description: "Use SPECTRA with GitHub Copilot, Cursor, Devin, Codex, Gemini, Antigravity, Kiro or OpenCode. Install once per agent; the prompts and the results stay the same."
---

# Use SPECTRA with other AI agents

SPECTRA's skills and subagents work in nine AI coding agents. Claude Code is the default. You install SPECTRA once
for each agent your team uses; the prompts, test cases and coverage are the same whichever agent you use.

## Install for your agent

| You use | Run |
|---|---|
| Claude Code | `spectra init` |
| GitHub Copilot | `spectra init --agent copilot` |
| Cursor | `spectra init --agent cursor` |
| Devin (formerly Windsurf) | `spectra init --agent devin` |
| Codex CLI | `spectra init --agent codex` |
| Gemini CLI | `spectra init --agent gemini` |
| Antigravity CLI | `spectra init --agent antigravity` |
| Kiro CLI | `spectra init --agent kiro` |
| OpenCode | `spectra init --agent opencode` |

- **Several agents:** list them: `spectra init --agent claude,copilot,codex`.
- **Not sure:** `spectra init --agent auto` picks the agent whose folder is in your repository, and stops if it finds
  none or several.
- **See first:** add `--dry-run` to list every file without writing anything.
- **Already set up:** running it again adds the agent and leaves your configuration alone.

Commit the files it writes, so everyone on the team gets the same skills.

## What to say

The same as in Claude Code:

> "Extract acceptance criteria"
>
> "Generate test cases for checkout"
>
> "Review the flagged tests in checkout"

See [Generate test cases](usage) and the other guides. Each agent gets the same 16 skills and 6 subagents in its own
folders ([AI agent installs](ai-agents-reference)).

## Check the critic step

In Claude Code, the skill always starts the critic as a separate subagent. Other agents decide for themselves when
to start a subagent, so the critic runs only if the agent follows the skill's instruction. After your first
generation, check that every new test was checked:

```bash
spectra ai audit-grounding --suite checkout
```

Every new test should show **grounded**, **partial** or **hallucinated**. A test that shows **unverified** wasn't
checked: ask the agent "Run spectra-critic for the tests without a verdict" (in Gemini CLI and OpenCode,
`@spectra-critic`).

## After upgrading SPECTRA

```bash
dotnet tool update -g Spectra.CLI
spectra update-skills          # refreshes the Claude Code files only
spectra init --agent auto      # refreshes every other agent already installed
```

Files you edited are kept and listed; `--regenerate` replaces them with the new version.

<details markdown="1">
<summary>Under the hood</summary>

The skill and subagent text is the same for every agent; only agent-specific wording, tool names and frontmatter
change. Each agent's files are listed in `.spectra/manifest.<agent>.json`. The model setting `ai.critic.model`
applies to Claude Code only. Folders, detection rules, shared folders and troubleshooting:
[AI agent installs](ai-agents-reference). To verify an installation in a real session:
[Live verification in each AI agent](DEVELOPMENT.md#live-verification-in-each-ai-agent-maintainers).

</details>
