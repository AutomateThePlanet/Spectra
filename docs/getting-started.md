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

> **New in spec 038 (optional)**: SPECTRA can integrate with
> [Testimize.MCP.Server](testimize-integration.md) for *algorithmic* test
> data optimization (BVA, EP, pairwise, ABC). Disabled by default. Install
> with `dotnet tool install --global Testimize.MCP.Server`, set
> `testimize.enabled` to `true` in `spectra.config.json`, then verify with
> `spectra testimize check`. SPECTRA works fine without Testimize.

---

## Prerequisites

- .NET 8.0+ SDK
- Git
- VS Code with GitHub Copilot (for MCP execution)
- GitHub Copilot Chat extension (for MCP support)
- One of the following authentication options (for AI test generation):
  - GitHub CLI authenticated (`gh auth login`)
  - `GITHUB_TOKEN` environment variable
  - `OPENAI_API_KEY` or `ANTHROPIC_API_KEY` for alternative providers

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
│       └── spectra-docs/SKILL.md                # Index documentation via Claude Code
├── .github/
│   └── agents/
│       └── spectra-execution.agent.md           # Test execution agent (MCP) — not yet ported to Claude Code
└── templates/bug-report.md                      # Bug report template
```

`spectra init` also writes a `.claude/settings.json` with the tool allowlist Claude Code needs to drive SPECTRA. The allowlist content (including the MCP execution tools) is covered in the execution setup — see [Skills Integration](skills-integration.md) and the execution setup next step.

> **Manual test runs:** ask the agent to run a suite and it starts the run, launches the local web
> console (`spectra run console`), and hands you a `http://127.0.0.1:<port>/` URL. You record verdicts —
> PASS / FAIL / BLOCKED, comment, screenshot — in the browser; the agent stays on-call. See
> [CLI Reference](cli-reference.md) (`spectra run console`).

> The test **execution** agent remains a GitHub Copilot agent under `.github/agents/` for now; its port to Claude Code is scheduled for a later spec.

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
    "providers": [
      { "name": "github-models", "model": "gpt-4.1", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "model": "claude-sonnet-4-6"
    }
  }
}
```

> **Spec 058:** the critic runs as the spectra-critic subagent; `ai.critic.model` (default `claude-sonnet-4-6`) is the only critic selector — the retired `provider`/`api_key_env`/`base_url` keys are ignored. The in-process *generator* still uses `ai.providers`. `spectra init -i` offers an **AI Model Preset** menu for the generator/critic models.

## Authentication

SPECTRA requires authentication to access AI providers. Check your status:

```bash
spectra auth
```

Output:

```
SPECTRA Authentication Status
========================================

github-models        [OK] via gh-cli
openai               [NOT CONFIGURED]
                       Set the OPENAI_API_KEY environment variable
anthropic            [NOT CONFIGURED]
                       Set the ANTHROPIC_API_KEY environment variable
```

### GitHub Models (Recommended)

**Option 1: GitHub CLI (Recommended)**

```bash
gh auth login
spectra auth -p github-models
```

SPECTRA automatically detects `gh auth token` and uses it.

**Option 2: Environment Variable**

```bash
# Create a GitHub token at https://github.com/settings/tokens (scope: read:user)
export GITHUB_TOKEN="ghp_your_token_here"

# Windows PowerShell
$env:GITHUB_TOKEN = "ghp_your_token_here"
```

### OpenAI

```bash
export OPENAI_API_KEY="sk-your_key_here"
spectra auth -p openai
```

### Anthropic

```bash
export ANTHROPIC_API_KEY="sk-ant-your_key_here"
spectra auth -p anthropic
```

### Custom Environment Variables

Override the default environment variable name in config:

```json
{
  "ai": {
    "providers": [
      {
        "name": "openai",
        "model": "gpt-4.1",
        "api_key_env": "MY_CUSTOM_OPENAI_KEY",
        "enabled": true
      }
    ]
  }
}
```

### Troubleshooting Authentication

| Problem | Solution |
|---------|----------|
| "GitHub CLI is installed but not authenticated" | Run `gh auth login` |
| "GitHub CLI is not installed" | Install from https://cli.github.com/ or use `GITHUB_TOKEN` |
| "API key not found" | Verify with `echo $OPENAI_API_KEY` (or `$env:OPENAI_API_KEY` on Windows) |
| Wrong provider being used | Check provider order in `spectra.config.json` — first enabled wins |

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
