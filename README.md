# SPECTRA

**AI-native test generation and execution framework.**
From documentation to deterministic test execution.

---

SPECTRA reads your product documentation, generates comprehensive manual test suites, keeps them in sync as your system evolves, and executes them through a deterministic AI-orchestrated protocol.

It doesn't replace your existing tools — it adds an AI layer. Bugs go to Azure DevOps, notifications to Teams, tests live in GitHub.

## What SPECTRA Does

**Generates tests from documentation.** Point the CLI at your docs folder. The AI agent discovers relevant documentation, understands the system, and produces a complete batch of manual test cases as Markdown files with structured metadata.

**Keeps tests in sync.** When documentation changes, SPECTRA sweeps your test suites, identifies outdated, orphaned, and redundant tests, and proposes batch updates.

**Executes tests deterministically.** An MCP-based execution engine provides a deterministic state machine that any LLM orchestrator (GitHub Copilot, Claude, custom agents) can drive without holding state. The AI navigates; the engine enforces.

**Integrates without migration.** The orchestrator connects to Azure DevOps, Jira, Teams, or Slack through their own MCP servers. No bidirectional sync. No data migration. Each system does what it's good at.

## Architecture

```
docs/                        ← Source documentation
  ↓
AI Test Generation CLI       ← Copilot SDK + custom tools
  ↓
tests/                       ← Markdown test cases in GitHub
  ↓
MCP Execution Engine         ← Deterministic state machine
  ↓
LLM Orchestrator             ← Copilot Chat, Claude, any MCP client
  ↓ (as needed)
Azure DevOps / Jira / Teams  ← Bug logging, notifications via their MCPs
```

### Two Independent Subsystems

| Subsystem | Purpose | Can be used independently |
|---|---|---|
| **AI CLI** | Generate, update, and analyze test cases from documentation | Yes — tests are useful even without the execution engine |
| **MCP Engine** | Execute tests through deterministic AI-orchestrated protocol | Yes — works with any Markdown tests, not just AI-generated ones |

## Quick Start

> **Note:** SPECTRA is under active development. The instructions below describe the target experience.

```bash
# Install
dotnet tool install -g spectra

# Initialize a repo
spectra init

# Generate tests from documentation
spectra ai generate --suite checkout

# Validate all tests
spectra validate

# Rebuild indexes
spectra index
```

### Prerequisites

- .NET 8.0+
- One of the following authentication options:
  - GitHub CLI authenticated (`gh auth login`), OR
  - `GITHUB_TOKEN` environment variable, OR
  - `OPENAI_API_KEY` / `ANTHROPIC_API_KEY` for alternative providers

### Quick Auth Check

```bash
spectra auth
```

This shows authentication status for all configured providers.

## Test Case Format

Tests are Markdown files with YAML frontmatter, stored in `tests/{suite}/`:

```markdown
---
id: TC-102
priority: high
tags: [payments, negative]
component: checkout
source_refs: [docs/features/checkout/payment-methods.md]
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4
  critic: gemini-2.0-flash
  verified_at: 2026-03-19T10:30:00Z
---

# Checkout with expired card

## Steps
1. Navigate to checkout
2. Enter expired card details (exp: 01/2020)
3. Click "Pay Now"

## Expected Result
- Payment is rejected
- Error message displays: card expired
```

## How It Fits

SPECTRA is part of the [Automate The Planet](https://www.automatetheplanet.com/) ecosystem:

| Tool | Purpose |
|---|---|
| [BELLATRIX](https://bellatrix.solutions) | Test automation framework |
| [Testimize](https://github.com/AntoanAngelov/Testimize) | Test case optimization (hybrid ABC algorithm) |
| **SPECTRA** | AI test generation and execution protocol |

**BELLATRIX** automates test execution. **Testimize** optimizes test case selection. **SPECTRA** generates and maintains the test cases themselves — closing the loop between documentation and quality assurance.

## Documentation

- [Development Guide](docs/DEVELOPMENT.md) — Building, testing, and running locally
- [CLI Quickstart](specs/001-ai-test-generation-cli/quickstart.md) — Using the AI test generation CLI
- [MCP Server Quickstart](specs/002-mcp-execution-server/quickstart.md) — Using the MCP execution server
- [Architecture Specification](spec-kit/architecture.md) — Full system design
- [ADR Index](spec-kit/adr/) — Architecture Decision Records

## Project Status

SPECTRA is in active development. See the [roadmap](#roadmap) for current priorities.

### Roadmap

**Phase 1: AI Test Generation CLI** ✓ *complete*
- Markdown test format with metadata schema
- Document map builder + selective loading
- Batch generation and batch update
- Provider chain (Copilot + BYOK fallback)
- Validation, indexing, deduplication

**Phase 2: MCP Execution Engine** ✓ *complete*
- Deterministic state machine
- SQLite execution storage
- Test handles, run lifecycle
- Orchestrator-agnostic MCP API

**Phase 3: Dashboard & Coverage Analysis** ✓ *complete*
- Interactive HTML dashboard
- Coverage reports (JSON, Markdown)
- Test-to-automation linking

**Phase 4: Test Generation Profiles** ✓ *complete*
- Repository-level profiles (`spectra.profile.md`)
- Suite-level profiles (`_profile.md`)
- Profile management commands

**Phase 5: Grounding Verification** ✓ *complete*
- Dual-model critic flow (generator + verifier)
- Three verdicts: grounded, partial, hallucinated
- Grounding metadata in test frontmatter
- Configurable critic provider (Google, OpenAI, Anthropic, GitHub)

**Phase 6: Integrations and Ecosystem** ← *current*
- Cross-MCP patterns (Azure DevOps, Teams, Slack)
- Copilot Spaces as knowledge source
- Optional Runner UI

## Configuration

SPECTRA is configured via `spectra.config.json` at the repository root. See [Configuration Reference](spec-kit/configuration.md) for the full schema.

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
      { "name": "copilot", "model": "gpt-5", "priority": 1 },
      { "name": "anthropic", "model": "claude-sonnet-4-5", "api_key_env": "ANTHROPIC_API_KEY", "priority": 2 }
    ],
    "fallback_strategy": "auto",
    "critic": {
      "enabled": true,
      "provider": "google",
      "model": "gemini-2.0-flash"
    }
  }
}
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License. See [LICENSE](LICENSE) for details.
