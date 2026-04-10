---
title: Copilot CLI
parent: Execution Agents
nav_order: 2
---

# Using SPECTRA Execution Agent with Copilot CLI

## Overview

The SPECTRA Execution Agent works with GitHub Copilot CLI for command-line test execution.

## Setup

1. Initialize SPECTRA in your repository:
   ```bash
   spectra init
   ```

2. This creates the execution agent prompt at `.github/agents/spectra-execution.agent.md` and the bundled SKILLs under `.github/skills/`.

3. Install the MCP server (separate global tool):
   ```bash
   dotnet tool install -g Spectra.MCP
   ```

4. Ensure you have GitHub Copilot CLI installed:
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

Copilot CLI discovers the agent prompt at `.github/agents/spectra-execution.agent.md` and uses it to:

1. Connect to the SPECTRA MCP server (`spectra-mcp`)
2. List available test suites
3. Guide you through test execution
4. Record results and generate reports

## Example Usage

```bash
# The MCP client launches spectra-mcp on stdio automatically;
# you don't start the server manually for stdio-based clients.
gh copilot suggest "run high priority checkout tests"
```

## Prerequisites

- `Spectra.MCP` global tool installed (`dotnet tool install -g Spectra.MCP`)
- GitHub Copilot CLI extension installed
- Repository initialized with `spectra init`
