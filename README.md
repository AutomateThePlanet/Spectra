# SPECTRA

**AI-native test generation and execution framework.**
From documentation to deterministic test execution.

---

SPECTRA reads your product documentation, generates comprehensive manual test suites, keeps them in sync as your system evolves, and executes them through a deterministic AI-orchestrated protocol.

It doesn't replace your existing tools — it adds an AI layer. Bugs go to Azure DevOps, notifications to Teams, tests live in GitHub.

## Architecture

```
docs/                        <- Source documentation
  |
docs/_index.md               <- Pre-built document index (incremental)
  |
AI Test Generation CLI       <- GitHub Copilot SDK (sole AI runtime)
  |                            Supports: github-models, azure-openai,
tests/                       <-          azure-anthropic, openai, anthropic
  |
MCP Execution Engine         <- Deterministic state machine
  |
LLM Orchestrator             <- Copilot Chat, Claude, any MCP client
  | (as needed)
Azure DevOps / Jira / Teams  <- Bug logging, notifications via their MCPs
```

| Subsystem | Purpose | Independent? |
|-----------|---------|:------------:|
| **AI CLI** | Generate, update, and analyze test cases from documentation | Yes |
| **MCP Engine** | Execute tests through deterministic AI-orchestrated protocol | Yes |

## Quick Start

```bash
# Install
dotnet tool install -g spectra

# Initialize and generate
spectra init
spectra docs index
spectra ai generate checkout --count 10

# Validate
spectra validate
```

**Prerequisites:** .NET 8.0+ and GitHub Copilot CLI (`copilot --version`). See [Getting Started](docs/getting-started.md) for auth setup and detailed instructions.

## How It Fits

SPECTRA is part of the [Automate The Planet](https://www.automatetheplanet.com/) ecosystem:

| Tool | Purpose |
|------|---------|
| [BELLATRIX](https://bellatrix.solutions) | Test automation framework |
| [Testimize](https://github.com/AntoanAngelov/Testimize) | Test case optimization (hybrid ABC algorithm) |
| **SPECTRA** | AI test generation and execution protocol |

**BELLATRIX** automates test execution. **Testimize** optimizes test case selection. **SPECTRA** generates and maintains the test cases themselves — closing the loop between documentation and quality assurance.

## Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Install, prerequisites, auth setup, first run |
| [CLI Reference](docs/cli-reference.md) | All commands, flags, and options |
| [Configuration](docs/configuration.md) | Full `spectra.config.json` reference |
| [Test Format](docs/test-format.md) | Markdown format, YAML frontmatter, metadata schema |
| [Coverage Analysis](docs/coverage.md) | Documentation, Requirements, and Automation coverage |
| [Generation Profiles](docs/generation-profiles.md) | Customize AI output style and quality |
| [Grounding Verification](docs/grounding-verification.md) | Dual-model critic for hallucination detection |
| [Document Index](docs/document-index.md) | Pre-built doc index for efficient generation |
| [Execution Agent](docs/execution-agent/overview.md) | MCP tools and AI-driven test execution |
| [Architecture](docs/architecture/overview.md) | System design and key decisions |
| [Development Guide](docs/DEVELOPMENT.md) | Building, testing, and running locally |

## Project Status

SPECTRA is in active development.

**Phase 1: AI Test Generation CLI** ✓
**Phase 2: MCP Execution Engine** ✓
**Phase 3: Dashboard & Coverage Analysis** ✓
**Phase 4: Test Generation Profiles** ✓
**Phase 5: Grounding Verification** ✓
**Phase 6: Integrations and Ecosystem** <- *current*

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License. See [LICENSE](LICENSE) for details.
