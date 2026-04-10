# Contributing to SPECTRA

Thank you for your interest in contributing to SPECTRA.

## Development Setup

1. Install .NET 8.0+ SDK
2. Clone the repo
3. `dotnet build` from the repo root
4. `dotnet test` to verify setup

See [Development Guide](docs/DEVELOPMENT.md) for detailed build and run instructions.

### AI Runtime

SPECTRA uses the GitHub Copilot SDK as its sole AI runtime. All AI generation and verification flows through the Copilot SDK, which supports multiple providers (github-models, azure-openai, azure-anthropic, openai, anthropic) via configuration.

## How to Contribute

### Reporting Issues
- Use GitHub Issues
- Include steps to reproduce
- Include your .NET version and OS

### Pull Requests
- Fork the repo and create a feature branch
- Follow existing code style
- Add tests for new functionality
- Update documentation in `docs/` if the change affects user-facing behavior
- Use the spec-kit workflow for new features (`/speckit.specify`, `/speckit.plan`, `/speckit.tasks`, `/speckit.implement`) — specs land under `specs/NNN-feature/`
- Keep PRs focused — one concern per PR

### Documentation

User-facing documentation lives in `docs/`. See the [documentation index](README.md#documentation) for the full list. Each piece of information should live in one place — don't duplicate content between files.

### Architecture Changes
- Propose significant changes via the spec-kit workflow — start with `/speckit.specify`, then `/speckit.plan`
- Discuss in an Issue before implementing

## Code of Conduct

Be respectful, constructive, and collaborative.
