---
title: Development
nav_order: 6
---

# Development Guide

Building, testing, and running SPECTRA locally.

Related: [Getting Started](getting-started.md) | [CLI Reference](cli-reference.md) | [Architecture](architecture/overview.md)

---

> **v2 note:** the `Spectra.MCP` project was removed entirely in Spec 070 — execution is CLI-only
> (`spectra run`). If you're looking for MCP server build/run instructions from an older version of
> this page, they no longer apply. See
> [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md).

## Quick Start

```bash
# Build and install the tool
dotnet build -c Release -p:NoWarn=CA1062
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release -p:NoWarn=CA1062
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI

# Verify
spectra --help
```

## Prerequisites

- .NET 8.0+ SDK
- Git
- [Claude Code](https://claude.com/claude-code) — needed to actually drive generation/analysis/
  criteria/update/verification turns; the CLI alone only does the deterministic bookkeeping

## Project Structure

```
src/
├── Spectra.CLI/        # CLI: authoring commands (compile-*/ingest-* seam) + spectra run
├── Spectra.Core/        # Shared library (parsing, validation, models, coverage)
├── Spectra.Execution/    # Transport-neutral execution engine, driven solely by spectra run
└── Spectra.GitHub/       # GitHub integration (future)

tests/
├── Spectra.CLI.Tests/          # CLI unit/integration tests
├── Spectra.Core.Tests/         # Core library tests
├── Spectra.Execution.Tests/    # Execution engine tests
├── Spectra.Integration.Tests/  # Cross-spec generation→persistence→execution
└── TestFixtures/                # Sample test data
    ├── docs/              # Sample documentation
    └── test-cases/        # Sample test case suites with _index.json
```

## Building

```bash
dotnet build                                    # Entire solution
dotnet build src/Spectra.CLI/Spectra.CLI.csproj # Specific project
dotnet build -c Release                         # Release mode
```

## Running Tests

```bash
dotnet test                          # All tests
dotnet test tests/Spectra.CLI.Tests/ # Specific project
dotnet test -v n                     # Verbose output
```

## Running Locally

### CLI (from source)

```bash
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures
dotnet run --project src/Spectra.CLI -- dashboard --output ./site
dotnet run --project src/Spectra.CLI -- run start checkout --priorities high
```

There is no `dotnet run … -- ai generate …` anymore — generation is skill-driven from inside
Claude Code, not a standalone CLI command. See
[Generation (in-session via the `spectra-generate` skill)](cli-reference.md#generation-in-session-via-the-spectra-generate-skill).

### Install as a Global Tool

```bash
dotnet pack src/Spectra.CLI/Spectra.CLI.csproj -c Release
dotnet tool uninstall -g Spectra.CLI 2>/dev/null
dotnet tool install -g --add-source src/Spectra.CLI/nupkg Spectra.CLI

# Run from anywhere
spectra validate
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

## Using TestFixtures

The `tests/TestFixtures/` folder contains ready-to-use sample data:

```bash
dotnet run --project src/Spectra.CLI -- validate --path tests/TestFixtures
dotnet run --project src/Spectra.CLI -- run list-suites --path tests/TestFixtures
```

Available suites in fixtures: auth (3 test cases), checkout (1 test case).

## Troubleshooting

### Build errors (CA1062 warnings)

```bash
dotnet build -p:NoWarn=CA1062
```

### "Suite not found"

Ensure your test case folder has `test-cases/` with suite subdirectories containing valid `_index.json` files.

### "Index stale"

```bash
spectra index --rebuild
```

### "No spectra.config.json found"

```bash
spectra init
```

### A generation/analysis/criteria/update turn doesn't seem to be running

There's no AI provider to check — SPECTRA makes no model calls of its own. Make sure you're
actually driving the flow from inside Claude Code (e.g. "generate test cases for the checkout
suite"), not calling `spectra ai compile-prompt`/`ingest-tests` outside a session with nothing to
answer the compiled prompt.
