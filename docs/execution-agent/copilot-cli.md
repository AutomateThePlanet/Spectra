# Using SPECTRA Execution Agent with Copilot CLI

## Overview

The SPECTRA Execution Agent works with GitHub Copilot CLI for command-line test execution.

## Setup

1. Initialize SPECTRA in your repository:
   ```bash
   spectra init
   ```

2. This creates the skill file at `.github/skills/spectra-execution/SKILL.md`

3. Ensure you have GitHub Copilot CLI installed:
   ```bash
   gh extension install github/gh-copilot
   ```

## Invocation

Use `gh copilot suggest` with your request:

```bash
gh copilot suggest "run spectra tests for auth suite"
```

Or ask for help:

```bash
gh copilot suggest "what spectra test suites are available"
```

## How It Works

Copilot CLI discovers the skill from `.github/skills/spectra-execution/SKILL.md` and uses it to:

1. Connect to the SPECTRA MCP server
2. List available test suites
3. Guide you through test execution
4. Record results and generate reports

## Example Usage

```bash
# Start the MCP server in one terminal
spectra mcp start

# In another terminal, use Copilot CLI
gh copilot suggest "run high priority checkout tests"
```

## Prerequisites

- SPECTRA MCP server must be running (`spectra mcp start`)
- GitHub Copilot CLI extension installed
- Repository initialized with `spectra init`
