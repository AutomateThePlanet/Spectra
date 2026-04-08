# Getting Started

Install SPECTRA, authenticate, and generate your first test suite.

Related: [CLI Reference](cli-reference.md) | [Configuration](configuration.md)

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
dotnet tool install -g spectra
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
│   └── _index.md                                # Document index (auto-built if docs exist)
├── tests/                                       # Generated tests go here
├── docs/criteria/
│   └── _criteria_index.yaml                     # Acceptance criteria index
├── .github/
│   ├── agents/
│   │   ├── spectra-execution.agent.md           # Test execution agent (MCP)
│   │   └── spectra-generation.agent.md          # Test generation agent (CLI)
│   └── skills/
│       ├── spectra-generate/SKILL.md            # Generate tests via Copilot Chat
│       ├── spectra-coverage/SKILL.md            # Check coverage via Copilot Chat
│       ├── spectra-dashboard/SKILL.md           # Build dashboard via Copilot Chat
│       ├── spectra-validate/SKILL.md            # Validate tests via Copilot Chat
│       ├── spectra-list/SKILL.md                # Browse tests via Copilot Chat
│       ├── spectra-init-profile/SKILL.md        # Configure profile via Copilot Chat
│       ├── spectra-help/SKILL.md                # Help and command reference
│       ├── spectra-criteria/SKILL.md            # Manage acceptance criteria
│       └── spectra-docs/SKILL.md                # Index documentation via Copilot Chat
└── templates/bug-report.md                      # Bug report template
```

Use `spectra init --skip-skills` if you don't use GitHub Copilot Chat.

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
    "dir": "tests/"
  },
  "ai": {
    "providers": [
      { "name": "github-models", "model": "gpt-4o", "enabled": true }
    ]
  }
}
```

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
        "model": "gpt-4o",
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

### Option 1: Copilot Chat (recommended)

Open VS Code with Copilot Chat and say:
- "Generate test cases for the checkout suite"
- "How's our test coverage?"
- "Validate all test cases"

The bundled SKILLs handle CLI invocation automatically. See [Skills Integration](skills-integration.md).

### Option 2: CLI directly

```bash
# Build the documentation index
spectra docs index

# Interactive generation session (analyze → generate → suggest → loop)
spectra ai generate

# Or direct mode with specific suite
spectra ai generate checkout --count 10

# Validate
spectra validate
```

### Option 3: CI/SKILL automation

```bash
# Full automated generation with JSON output
spectra ai generate checkout --auto-complete --output-format json

# Coverage check
spectra ai analyze --coverage --output-format json

# Validation with structured errors
spectra validate --output-format json --no-interaction
```

See [CLI Reference](cli-reference.md) for all available commands.
