---
title: Skills Integration
parent: User Guide
nav_order: 8
---

# Skills Integration

How SPECTRA integrates with Claude Code through `.claude/skills/<name>/SKILL.md` files.

Related: [CLI Reference](cli-reference.md) | [Getting Started](getting-started.md)

---

## Overview

SPECTRA ships both authoring and execution orchestration as Claude Code skills under `.claude/skills/<name>/SKILL.md`. These SKILLs let Claude Code invoke CLI commands through natural language: they translate what users say into CLI commands with `--output-format json --verbosity quiet`, parse the JSON output, and present results conversationally. The bundled SKILLs are auto-discovered from embedded resources and installed/refreshed by `spectra init` and `spectra update-skills` through the same install pipeline.

The critic runs as a Claude Code subagent defined at `.claude/agents/spectra-critic.agent.md` (a subagent with a fresh context per test). The generation SKILL invokes `spectra-critic` as a **mandatory, explicit step** before a generated test is accepted.

> The port is complete: the test **execution** agent is a native Claude Code skill/agent
> (`.claude/skills/spectra-execute/`), not a GitHub Copilot agent. The in-process GitHub Copilot SDK
> generation path was removed entirely, so there is no transitional in-process path left.
> See [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) for the full
> before/after picture.

## Architecture

```
User (Claude Code) → SKILL file → deterministic CLI commands → a turn in your session → CLI ingest → response
```

1. User asks: "Generate test cases for the checkout suite"
2. The `spectra-generate` SKILL runs `spectra ai compile-prompt --suite checkout --count <n>`, which emits a deterministic prompt to stdout, with no model call yet
3. The prompt is answered as an ordinary turn in the user's own Claude Code session
4. The SKILL persists the result via `spectra ai ingest-tests`, runs the mandatory `spectra-critic` subagent verification step per test, and presents: "Generated 10 test cases (8 grounded, 1 partial, 1 rejected)"

## Bundled SKILLs

Created by `spectra init` in `.claude/skills/` (one `SKILL.md` per skill directory):

| SKILL | Path | Drives |
|-------|------|-------|
| SPECTRA Generate | `.claude/skills/spectra-generate/SKILL.md` | Behavior analysis + generation over the `compile-prompt`/`ingest-tests` seam, with the mandatory critic step |
| SPECTRA Update | `.claude/skills/spectra-update/SKILL.md` | Classification (`spectra ai update`) + doc-aware edits over the `compile-update-prompt`/`ingest-update` seam |
| SPECTRA Coverage | `.claude/skills/spectra-coverage/SKILL.md` | `spectra ai analyze --coverage --auto-link` |
| SPECTRA Dashboard | `.claude/skills/spectra-dashboard/SKILL.md` | `spectra dashboard --output ./site` |
| SPECTRA Validate | `.claude/skills/spectra-validate/SKILL.md` | `spectra validate` |
| SPECTRA List | `.claude/skills/spectra-list/SKILL.md` | `spectra list` and `spectra show` |
| SPECTRA Profile | `.claude/skills/spectra-init-profile/SKILL.md` | `spectra init-profile` |
| SPECTRA Help | `.claude/skills/spectra-help/SKILL.md` | Help and command reference (terse, flag-oriented) |
| SPECTRA Criteria | `.claude/skills/spectra-criteria/SKILL.md` | `spectra docs changed` → extraction seam → `ingest-criteria`, plus import |
| SPECTRA Docs | `.claude/skills/spectra-docs/SKILL.md` | `spectra docs index` with progress page |
| SPECTRA Prompts | `.claude/skills/spectra-prompts/SKILL.md` | `spectra prompts list/show/reset/validate` for prompt template customization |
| SPECTRA Delete | `.claude/skills/spectra-delete/SKILL.md` | Preview-then-confirm test case deletion |
| SPECTRA Suite | `.claude/skills/spectra-suite/SKILL.md` | Suite list/rename/delete |
| SPECTRA Review Flagged | `.claude/skills/spectra-review-flagged/SKILL.md` | Human review of partial/flagged verdicts (accept, retry repair, delete) |
| SPECTRA Execute | `.claude/skills/spectra-execute/SKILL.md` | Orchestrates `spectra run` + the local web console for a manual test run |
| SPECTRA Quickstart | `.claude/skills/spectra-quickstart/SKILL.md` | Workflow-oriented onboarding & walkthroughs |

## Critic Subagent

Created by `spectra init` in `.claude/agents/`:

| Agent | Path | Purpose |
|-------|------|---------|
| SPECTRA Critic | `spectra-critic.agent.md` | Independent verification of generated tests, run as a subagent with a fresh context |

The generation SKILL invokes `spectra-critic` as a mandatory explicit step before a test is accepted. Execution (`spectra-execute`) is a skill, not a subagent, and it orchestrates `spectra run` directly rather than running in an isolated context.

## Key Patterns

### JSON Output for SKILLs

Every SKILL uses these flags:
```bash
spectra <command> --output-format json --verbosity quiet
```

- `--output-format json`: Structured JSON on stdout (no colors, spinners, or ANSI codes)
- `--verbosity quiet`: Only the final result (no progress indicators)

#### Analysis JSON includes a `technique_breakdown`

The `analysis` object emitted by `spectra ai ingest-analysis` exposes both a category `breakdown`
and a `technique_breakdown` map. Keys are short ISTQB codes (`BVA`, `EP`, `DT`, `ST`, `EG`, `UC`);
values are counts. Always present (`{}` when empty) so SKILL parsers can rely on the field existing.

```json
{
  "analysis": {
    "total_behaviors": 141,
    "breakdown": { "happy_path": 42, "boundary": 38, "negative": 24, "edge_case": 18 },
    "technique_breakdown": { "BVA": 38, "EP": 24, "UC": 32, "EG": 15, "DT": 18, "ST": 14 }
  }
}
```

The `spectra-generate` SKILL renders both breakdowns to the user when
presenting the analyze recommendation.

#### `spectra testimize check` JSON

When the optional Testimize integration is in use, `spectra testimize check
--output-format json` returns a `TestimizeCheckResult` object with the
required fields `enabled`, `installed`, `healthy` plus `mode`, `strategy`,
and (when not installed) `install_command`. See [Testimize Integration](testimize-integration.md).

### Generation Flow in SKILLs

The generate SKILL drives the deterministic seam directly; there is no single `spectra ai generate`
command anymore:

```bash
# Analysis
spectra ai compile-analysis-prompt --suite {suite} --output-format json
# … a turn in your session answers it …
spectra ai ingest-analysis --suite {suite} --output-format json

# Bulk generation
spectra ai compile-prompt --suite {suite} --count {n} [--focus "{text}"] --output-format json
# … a turn in your session answers it …
spectra ai ingest-tests {suite} --output-format json

# From a plain-language description (single test)
spectra ai compile-prompt --suite {suite} --from-description "{text}" [--context "{ctx}"] --output-format json
# … a turn in your session answers it …
spectra ai ingest-tests {suite} --output-format json
```

Every `compile-*` call is followed by an `ingest-*` call for the generated test's mandatory
`spectra-critic` verification step before it's accepted.

### Intent Routing in Chat

The `spectra-generate` SKILL uses an intent-routing table to choose between `compile-prompt`'s
two modes:

| User intent | Signal | Flow |
|-------------|--------|------|
| Explore a feature area | "Generate tests for...", "Cover... module" | Analyze → generate with `--focus` |
| Create a specific test | "Add a test for...", "I need a test that verifies..." | `--from-description` (1 test, no analysis, no count question) |

The key rule: if you can read the user's request as a single test case title, the flow routes to `--from-description`. If it's a topic to explore, it routes to `--focus`. The flow never asks the user for count or scope to disambiguate, since the topic-vs-scenario shape is the only signal.

When `--from-description` runs in a project that has documentation and acceptance criteria, the CLI best-effort loads matching docs (capped at 3 docs × 8000 chars) and matching `.criteria.yaml` entries. The matching criteria are injected into the generation prompt as the **mandatory criteria-mapping instruction**, the same "you MUST map each test case to matching acceptance criteria" block the batch flow uses, so the model reliably populates the `criteria` frontmatter field.

The resulting test case has populated `source_refs` and `criteria` fields, but `grounding.verdict` stays `manual` by design, since from-description runs no independent critic and populating `criteria` is not verification. Doc context is used for terminology alignment only, never for verification. Consequently a from-description test counts toward **acceptance-criteria coverage** (its `criteria` field is populated) but is excluded from **grounded** statistics (its verdict is `manual`). See [Coverage](coverage.md) for how the two are tallied separately.

### CI Pipelines and Automation

The `compile-*`/`ingest-*` seam commands are deterministic and non-interactive by construction,
so there's no interactive prompt to suppress. What a CI pipeline needs from Claude is a way to answer
the compiled prompt: drive the seam from a headless Claude Code invocation rather than an
interactive session. `spectra validate`, `spectra ai analyze --coverage`, and the other
non-model-calling commands accept `--no-interaction --output-format json` as usual for the parts of
the pipeline that don't need a model turn at all.

## Customizing SKILLs

`SKILL.md` files are plain Markdown, and you can edit them freely to:
- Add project-specific instructions
- Change default flags
- Add custom examples for your domain

If you modify a `SKILL.md` file, `spectra update-skills` will skip it, preserving your changes.

## Updating SKILLs

When upgrading SPECTRA CLI:
```bash
spectra update-skills
```

- Unmodified files are updated to the latest version
- User-modified files are preserved (skipped with warning)
- Missing files are recreated

## Other AI Agents

`spectra init --agent <list>` installs the same skills and subagents for other AI coding agents, in each agent's own
layout. Without `--agent`, `spectra init` installs for Claude Code exactly as described above. This section is the
summary; the full guide is [AI Agents](ai-agents.md).

```bash
spectra init --agent copilot,codex         # comma-separated; claude can be in the list too
spectra init --agent auto                  # the agents already installed, else the one agent detected in the repo
spectra init --agent kiro --dry-run        # print every file it would write, the decisions and the skips
spectra init --agent kiro --regenerate     # also overwrite agent files you edited
```

Accepted names: `claude`, `copilot`, `cursor`, `devin`, `codex`, `gemini`, `antigravity`, `kiro`, `opencode`; `windsurf`
is accepted for `devin` (renamed Devin Desktop) and `agy` for `antigravity`. On a repository that is already
initialised, `--agent` adds the agents without `--force` and leaves `spectra.config.json` and the Claude files alone.

| Agent | Always-on instructions | Skills | Subagents | Starting a subagent | `spectra` allowlist |
|---|---|---|---|---|---|
| Claude Code | — | `.claude/skills/` | `.claude/agents/*.agent.md` | platform-enforced | `.claude/settings.json` |
| GitHub Copilot | `.github/instructions/spectra.instructions.md` (`applyTo: "**"`) | `.github/skills/` (reuses `.claude/skills/`) | `.github/agents/*.agent.md` (reuses `.claude/agents/`) | the `agent` tool, by name | — |
| Cursor | `.cursor/rules/spectra.mdc` (`alwaysApply`) | `.cursor/skills/` (reuses `.claude/skills/` or `.agents/skills/`) | `.cursor/agents/*.md` | by name, or `/name` | `.cursor/cli.json`: `Shell(spectra)` |
| Devin | `.devin/rules/spectra.md` (`always_on`) | `.devin/skills/` | `.devin/agents/*.md` | by name | `.devin/config.json`: `Exec(spectra)` |
| Codex CLI | SPECTRA block in `AGENTS.md` → `.spectra/spectra.instructions.md` | `.agents/skills/` | `.codex/agents/*.toml` | ask for it by name | — |
| Gemini CLI | SPECTRA block in `GEMINI.md` importing `.spectra/spectra.instructions.md` | `.agents/skills/` | `.gemini/agents/*.md` | `@name` | — |
| Antigravity CLI | `.agents/rules/spectra.md` (`always_on`) | `.agents/skills/` | `.agents/agents/<name>/agent.md` | by name | — |
| Kiro CLI | `.kiro/steering/spectra.md` (`inclusion: always`) | `.kiro/skills/` | `.kiro/agents/*.md` (with the steering file and skills as `resources`) | by name | — |
| OpenCode | SPECTRA block in `AGENTS.md` → `.spectra/spectra.instructions.md` | `.opencode/skills/` (reuses `.claude/skills/` or `.agents/skills/`) | `.opencode/agents/*.md` | `@name` | `opencode.json`: `"spectra *": "allow"` |

**What is installed.** The 16 skills (with the two reference files), the six subagents (`spectra-analyst`,
`spectra-generator`, `spectra-updater`, `spectra-repairer`, `spectra-extractor`, `spectra-critic`) and one always-on
instructions file. The skill and subagent instructions are the same as for Claude Code; only Claude-specific wording is
replaced (for example "start the subagent" instead of "with the Agent tool") and each subagent gets the agent's own
frontmatter and tool names. The critic keeps the tools it has today, including writing its verdict file.

**Shared folders are written once.** Several agents read the same skills folder (`.agents/skills/` is read by Codex,
Gemini, Antigravity, Cursor and OpenCode), so `init` picks the fewest folders that reach every selected agent and reuses
`.claude/skills/` when Claude is installed. When an agent would see two copies anyway, `init` prints a warning.

**Re-runs and your edits.** Each agent has a manifest, `.spectra/manifest.<agent>.json`, with a hash of every file
SPECTRA wrote for it. Re-running `spectra init --agent …` refreshes those files, skips any file you edited (and says so)
unless you pass `--regenerate`, and removes files a newer version no longer installs if you did not edit them.
`spectra update-skills` refreshes only the Claude install; to refresh another agent after upgrading the CLI, re-run
`spectra init --agent <agent>`.

**Your files.** `AGENTS.md` and `GEMINI.md` belong to you: SPECTRA adds one block between
`<!-- >>> spectra >>> -->` and `<!-- <<< spectra <<< -->` and never changes anything outside it. Allowlist entries are
merged into your settings file by key; a value you already set is kept, and a file with comments is left alone (add the
entry by hand). SPECTRA writes only `spectra-*` skills and subagents, `spectra.*` instruction files and
`.spectra/manifest.*.json`, so it can be installed next to BELLATRIX Agent (`bellatrix-*`, its own
`<!-- >>> bellatrix >>> -->` block) in any order.

> **Known limitation.** In Claude Code the critic runs as an isolated subagent that the platform starts as a mandatory
> step. The other agents choose when to delegate, so there the critic runs when the agent follows the skill's
> instruction to start it. A critic gate enforced by the CLI is planned for a later version.

To check an installation by hand, follow the [live verification checklist](live-verification-agents.md).

## Skipping SKILLs

For projects that don't use an AI coding agent:
```bash
spectra init --skip-skills
```

This creates only core files (config, directories, templates) without the `.claude/skills/` SKILLs or the `.claude/agents/` critic subagent. With `--agent`, `--skip-skills` installs no agent files either.
