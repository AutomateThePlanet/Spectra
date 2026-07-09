---
title: Getting Started
nav_order: 1
---

# Getting Started

Install SPECTRA, authenticate, and generate your first test case suite.

Related: [CLI Reference](cli-reference.md) | [Configuration](configuration.md)

> **New in spec 037**: behavior analysis applies six ISTQB test design
> techniques (EP, BVA, DT, ST, EG, UC) and the analysis output now includes a
> technique breakdown alongside the category breakdown. Existing projects can
> adopt the new templates by running `spectra prompts reset --all` — your
> customized templates are preserved.

> **Optional**: SPECTRA can integrate with [Testimize](testimize-integration.md) for *algorithmic*
> test data optimization (BVA, EP, pairwise, ABC). Disabled by default, and runs **in-process**
> as a NuGet dependency — no separate install or MCP server (v1.48.3 moved it off the old
> `Testimize.MCP.Server` child-process model). Set `testimize.enabled` to `true` in
> `spectra.config.json`, then verify with `spectra testimize check`. SPECTRA works fine without it.

---

## Prerequisites

- .NET 8.0+ SDK
- Git
- [Claude Code](https://claude.com/claude-code), installed and signed in to your account

That's the whole list. SPECTRA (v2) makes no model calls of its own — generation, analysis, and
verification all run as turns/subagent calls inside your own Claude Code session, so there's no
separate AI provider, API key, or authentication step to configure. See
[Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) if you're upgrading
from a pre-v2 install that used to require `gh auth login` / `spectra auth`.

## Install

```bash
dotnet tool install -g Spectra.CLI
```

## Initialize a Repository

```bash
cd my-project
spectra init
```

This creates:

```
my-project/
├── spectra.config.json                          # Configuration
├── docs/                                        # Put your documentation here
│   └── _index/                                  # Document index v2 (Spec 040, auto-built)
│       ├── _manifest.yaml                       # Always loaded into AI prompts (~2-5K tokens)
│       ├── _checksums.json                      # Hash table; never sent to AI
│       └── groups/{suite}.index.md              # Per-suite index files, lazy-loaded
├── test-cases/                                  # Generated test cases go here
├── docs/criteria/
│   └── _criteria_index.yaml                     # Acceptance criteria index
├── .claude/
│   ├── agents/
│   │   └── spectra-critic.agent.md              # Critic subagent (context: fork)
│   ├── settings.json                            # Tool allowlist (see execution setup)
│   └── skills/
│       ├── spectra-generate/SKILL.md            # Generate tests via Claude Code
│       ├── spectra-coverage/SKILL.md            # Check coverage via Claude Code
│       ├── spectra-dashboard/SKILL.md           # Build dashboard via Claude Code
│       ├── spectra-validate/SKILL.md            # Validate tests via Claude Code
│       ├── spectra-list/SKILL.md                # Browse tests via Claude Code
│       ├── spectra-init-profile/SKILL.md        # Configure profile via Claude Code
│       ├── spectra-help/SKILL.md                # Help and command reference
│       ├── spectra-criteria/SKILL.md            # Manage acceptance criteria
│       ├── spectra-docs/SKILL.md                # Index documentation via Claude Code
│       └── spectra-execution/SKILL.md           # Drive test execution via Claude Code (launches the run console)
└── templates/bug-report.md                      # Bug report template
```

`spectra init` also merges `.claude/settings.json` with the permissions Claude Code needs to drive
SPECTRA: `Bash(spectra *)` plus write/edit access to the `.spectra/` scratch directory. There is no
MCP allowlist — execution is CLI-only (Spec 070). See [Skills Integration](skills-integration.md).

> **Manual test runs:** ask the agent to run a suite and it starts the run, launches the local web
> console (`spectra run console`), and hands you a `http://127.0.0.1:<port>/` URL. You record verdicts —
> PASS / FAIL / BLOCKED, comment, screenshot — in the browser; the agent stays on-call. See
> [CLI Reference](cli-reference.md) (`spectra run console`).

> The test **execution** agent is a native Claude Code skill (`.claude/skills/spectra-execution/`); it
> orchestrates the run and launches the web console rather than driving a per-test loop in chat.

Use `spectra init --skip-skills` if you don't use Claude Code.

## Add Your Documentation

Copy your existing documentation into the `docs/` folder:

```
docs/
├── features/
│   ├── authentication.md
│   ├── checkout.md
│   └── user-profile.md
├── api/
│   └── endpoints.md
└── criteria/
    └── _criteria_index.yaml
```

## Configure

Edit `spectra.config.json` to point to your docs. See [Configuration Reference](configuration.md) for the full schema.

```json
{
  "source": {
    "mode": "local",
    "local_dir": "docs/"
  },
  "tests": {
    "dir": "test-cases/"
  },
  "ai": {
    "generation_batch_size": 30,
    "generation_timeout_minutes": 5,
    "analysis_timeout_minutes": 2
  }
}
```

> There's no generator/critic model to pick here anymore. The `ai` block only paces the
> deterministic CLI side of generation (batch size, timeouts) — the model doing the actual work is
> whatever your Claude Code session is running. The `spectra-critic` subagent's model is set in
> `.claude/agents/spectra-critic.agent.md`. See [Configuration Reference](configuration.md#ai--generation-pacing-no-providermodel-routing-anymore).

## First Run

### Option 1: Claude Code (recommended)

Open Claude Code in your project and say:
- "Generate test cases for the checkout suite"
- "How's our test case coverage?"
- "Validate all test cases"

The bundled `.claude/skills/` SKILLs handle CLI invocation automatically, and the generation SKILL runs the `spectra-critic` subagent before accepting tests. See [Skills Integration](skills-integration.md).

### Option 2: CLI directly

Generation itself is **skill-driven** (it runs in your interactive Claude Code session, not as a
standalone CLI command). The CLI handles the deterministic surrounding steps:

```bash
# Build the documentation index
spectra docs index

# Ask Claude Code: "generate test cases for the checkout suite"
# → the spectra-generate skill drives: compile-analysis-prompt → (you generate in-session)
#   → ingest-analysis → compile-prompt → ingest-tests → spectra-critic verification

# Validate
spectra validate
```

### Option 3: CI/SKILL automation

```bash
# Coverage check
spectra ai analyze --coverage --output-format json

# Validation with structured errors
spectra validate --output-format json --no-interaction
```

See [CLI Reference](cli-reference.md) for all available commands.
