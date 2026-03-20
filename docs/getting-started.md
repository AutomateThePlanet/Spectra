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
├── spectra.config.json       # Configuration
├── docs/                     # Put your documentation here
│   └── _index.md             # Document index (auto-built if docs exist)
├── tests/                    # Generated tests go here
├── docs/requirements/
│   └── _requirements.yaml    # Requirements definition (commented template)
└── .github/skills/...        # AI skill definition
```

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
└── requirements/
    └── business-rules.md
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

```bash
# Build the documentation index
spectra docs index

# Generate tests
spectra ai generate checkout --count 10

# Validate
spectra validate
```

See [CLI Reference](cli-reference.md) for all available commands.
