# Contributing to SPECTRA

Thank you for your interest in contributing to SPECTRA.

## Project Structure

```
src/
├── Spectra.CLI/           # AI test generation CLI
├── Spectra.MCP/           # MCP execution server
├── Spectra.Core/          # Shared library (parsing, validation, models)
└── Spectra.GitHub/        # GitHub integration (Octokit)

spec-kit/                  # Architecture specs, ADRs
tests/                     # Sample test cases (also used for testing)
docs/                      # Sample documentation (used for testing generation)
```

## Development Setup

1. Install .NET 8.0+ SDK
2. Install GitHub Copilot CLI (for AI features)
3. Clone the repo
4. `dotnet build` from the repo root

## How to Contribute

### Reporting Issues
- Use GitHub Issues
- Include steps to reproduce
- Include your .NET version and OS

### Pull Requests
- Fork the repo and create a feature branch
- Follow existing code style
- Add tests for new functionality
- Update spec-kit documentation if the change affects architecture
- Keep PRs focused — one concern per PR

### Architecture Changes
- Propose significant changes as an ADR (Architecture Decision Record) in `spec-kit/adr/`
- Discuss in an Issue before implementing

## Code of Conduct

Be respectful, constructive, and collaborative.
