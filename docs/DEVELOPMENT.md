---
title: Development
nav_order: 6
---

# Development Guide

Building, testing, and running SPECTRA locally.

Related: [Getting Started](getting-started.md) | [CLI Reference](cli-reference.md) | [Architecture](architecture/overview.md)

---

## Quick Start

```bash
# Build and install tools
dotnet build -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release -p:NoWarn=CA1062
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI
dotnet tool install -g --add-source src/Spectra.MCP/nupkg Spectra.MCP

# Verify
spectra --help
spectra-mcp --help
```

## Prerequisites

- .NET 8.0+ SDK
- Git

## Project Structure

```
src/
├── Spectra.CLI/       # AI test generation CLI
├── Spectra.MCP/       # MCP execution server
├── Spectra.Core/      # Shared library (parsing, validation, models, coverage)
└── Spectra.GitHub/    # GitHub integration (future)

tests/
├── Spectra.CLI.Tests/     # CLI unit/integration tests
├── Spectra.MCP.Tests/     # MCP server tests
├── Spectra.Core.Tests/    # Core library tests
└── TestFixtures/          # Sample test data
    ├── docs/              # Sample documentation
    └── tests/             # Sample test suites with _index.json
```

## Building

```bash
dotnet build                                    # Entire solution
dotnet build src/Spectra.MCP/Spectra.MCP.csproj # Specific project
dotnet build -c Release                         # Release mode
```

## Running Tests

```bash
dotnet test                          # All tests
dotnet test tests/Spectra.MCP.Tests/ # Specific project
dotnet test -v n                     # Verbose output
```

## Running Locally

### CLI (from source)

```bash
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures
dotnet run --project src/Spectra.CLI -- dashboard --output ./site
dotnet run --project src/Spectra.CLI -- ai generate checkout --count 5
```

### MCP Server (from source)

```bash
# Against TestFixtures
dotnet run --project src/Spectra.MCP -- "tests/TestFixtures"

# Against your project
dotnet run --project src/Spectra.MCP -- /path/to/your/project
```

The MCP server uses stdio transport. It expects JSON-RPC messages on stdin and writes responses to stdout.

### Install as Global Tools

```bash
# Pack and install
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release
dotnet pack src/Spectra.MCP/Spectra.MCP.csproj -c Release

dotnet tool uninstall -g Spectra.CLI 2>/dev/null
dotnet tool uninstall -g Spectra.MCP 2>/dev/null

dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI
dotnet tool install -g --add-source src/Spectra.MCP/nupkg Spectra.MCP

# Run from anywhere
spectra validate
spectra-mcp /path/to/project
```

### Dashboard (from source)

```bash
# Generate
dotnet run --project src/Spectra.CLI -- dashboard --output ./site

# Serve
cd site && python -m http.server 8080
# or: npx serve site
# or: dotnet serve -d site -p 8080
```

### MCP Server Configuration

**VS Code (project-level)** — `.vscode/mcp.json`:

```json
{
  "servers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["."]
    }
  }
}
```

**VS Code (from source)** — `.vscode/mcp.json`:

```json
{
  "servers": {
    "spectra": {
      "command": "dotnet",
      "args": [
        "run", "--project",
        "C:/SourceCode/Spectra/src/Spectra.MCP/Spectra.MCP.csproj",
        "--", "${workspaceFolder}"
      ]
    }
  }
}
```

**Claude Desktop** — `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["C:/path/to/your/test/project"]
    }
  }
}
```

**Claude Code** — `.mcp.json`:

```json
{
  "mcpServers": {
    "spectra": {
      "command": "spectra-mcp",
      "args": ["."]
    }
  }
}
```

## Using TestFixtures

The `tests/TestFixtures/` folder contains ready-to-use sample data:

```bash
dotnet run --project src/Spectra.MCP -- tests/TestFixtures
```

Available suites in fixtures: auth (3 tests), checkout (1 test).

## Troubleshooting

### Build errors (CA1062 warnings)

```bash
dotnet build -p:NoWarn=CA1062
```

### "Suite not found"

Ensure your test folder has `tests/` with suite subdirectories containing valid `_index.json` files.

### "Index stale"

```bash
spectra index --rebuild
```

### MCP server exits immediately

Check that:
1. The base path argument is valid
2. The `.execution/` directory can be created
3. No other process is using the SQLite database

### VS Code doesn't see the MCP server

1. Verify `.vscode/mcp.json` exists
2. Verify `spectra-mcp` is in PATH (`where spectra-mcp`)
3. Test manually: `echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | spectra-mcp .`
4. Reload VS Code window after changing MCP config

### "No spectra.config.json found"

```bash
spectra init
```

### AI provider not available

1. For GitHub Copilot: Ensure you're signed in to GitHub in VS Code
2. For BYOK: Set the API key environment variable (`OPENAI_API_KEY` or `ANTHROPIC_API_KEY`)
