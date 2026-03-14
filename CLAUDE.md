# Spectra Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-03-13

## Active Technologies
- C# 12, .NET 8+ + ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage), System.Text.Json (serialization) (002-mcp-execution-server)
- SQLite database (`.execution/spectra.db`) for execution state; file system for reports (002-mcp-execution-server)

- C# 12, .NET 8+ + System.CommandLine (CLI), Microsoft.Extensions.AI (AI tools), Markdig (Markdown parsing), YamlDotNet (frontmatter), GitHub Copilot SDK (AI runtime) (001-ai-test-generation-cli)

## Project Structure

```text
src/
├── Spectra.CLI/              # .NET CLI application
│   ├── Commands/             # Command handlers
│   ├── Agent/                # Copilot SDK integration
│   ├── Source/               # Document map builder
│   ├── Index/                # _index.json operations
│   ├── Validation/           # Test validation, dedup
│   ├── Review/               # Interactive terminal UI
│   ├── Provider/             # Multi-provider chain
│   ├── Config/               # Configuration loader
│   └── IO/                   # File writers
├── Spectra.Core/             # Shared library
│   ├── Models/               # TestCase, Suite, Config models
│   ├── Parsing/              # Markdown + YAML parser
│   ├── Validation/           # Schema validation
│   └── Index/                # Index read/write
└── Spectra.GitHub/           # GitHub integration (future)

tests/
├── Spectra.Core.Tests/       # Unit tests
├── Spectra.CLI.Tests/        # Integration tests
└── TestFixtures/             # Sample data
```

## Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Run CLI
dotnet run --project src/Spectra.CLI -- <command>
```

## Code Style

- **Language:** C# 12, .NET 8+
- **Naming:** PascalCase for types/methods, camelCase for locals
- **Async:** All I/O operations are async with `Async` suffix
- **Nullability:** Nullable reference types enabled
- **Tests:** xUnit with structured results (never throw on validation errors)

## Recent Changes
- 002-mcp-execution-server: Added C# 12, .NET 8+ + ASP.NET Core (MCP server), Microsoft.Data.Sqlite (state storage), System.Text.Json (serialization)

- 001-ai-test-generation-cli: Added C# 12, .NET 8+ + System.CommandLine (CLI), Microsoft.Extensions.AI (AI tools), Markdig (Markdown parsing), YamlDotNet (frontmatter), GitHub Copilot SDK (AI runtime)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
