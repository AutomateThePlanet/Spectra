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
AI Test Generation CLI       ← GitHub Copilot SDK (sole AI runtime)
  ↓                            Supports: github-models, azure-openai,
tests/                       ←          azure-anthropic, openai, anthropic
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
spectra ai generate checkout --count 10

# Generate with focus on specific scenarios
spectra ai generate checkout --focus "negative payment validation"

# Validate all tests
spectra validate

# Rebuild indexes
spectra index
```

### Prerequisites

- .NET 8.0+
- GitHub Copilot CLI installed (`copilot --version` to verify)

SPECTRA uses the GitHub Copilot SDK as its AI runtime. All providers (GitHub Models, Azure OpenAI, Azure Anthropic, OpenAI, Anthropic) are accessed through the Copilot SDK.

### Quick Auth Check

```bash
spectra auth
```

This verifies Copilot SDK availability and shows supported providers.

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

## CLI Reference

### Test Generation

SPECTRA supports two modes for test generation:

**Interactive Mode** - Guided prompts for exploratory generation:
```bash
spectra ai generate
```

This launches a guided session that:
1. Lists available suites with test counts (or create a new suite)
2. Asks what kind of tests you want (full coverage, negative only, specific area)
3. Shows existing tests matching your focus
4. Identifies coverage gaps in your documentation
5. Generates tests and writes them directly
6. Prompts to generate more for remaining gaps

**Direct Mode** - Specify suite and options upfront:
```bash
# Generate tests for a specific suite
spectra ai generate checkout --count 10

# Focus on specific scenarios
spectra ai generate checkout --focus "error handling"
spectra ai generate payments --focus "negative validation edge cases"

# Skip grounding verification (faster)
spectra ai generate checkout --skip-critic

# CI mode (no interactive prompts)
spectra ai generate checkout --count 10 --no-interaction --no-review

# Preview without writing files
spectra ai generate checkout --count 5 --dry-run
```

### Test Updates

```bash
# Interactive mode
spectra ai update

# Update specific suite
spectra ai update checkout

# Show diff of proposed changes
spectra ai update checkout --diff

# Auto-delete orphaned tests
spectra ai update checkout --delete-orphaned

# CI mode
spectra ai update checkout --no-interaction
```

### Coverage Analysis

```bash
# Analyze coverage gaps
spectra ai analyze --coverage

# Output as JSON
spectra ai analyze --coverage --format json --output coverage.json

# Output as Markdown
spectra ai analyze --coverage --format markdown --output coverage.md
```

### Global Options

| Option | Description |
|--------|-------------|
| `--verbosity`, `-v` | Output level: quiet, minimal, normal, detailed, diagnostic |
| `--dry-run` | Preview changes without executing |
| `--no-review` | Skip interactive review (CI mode) |

### Test ID Allocation

Test IDs are unique globally across all suites. When you generate tests:
- SPECTRA scans all `_index.json` files to find existing IDs
- New tests continue from the global maximum (e.g., if TC-150 exists anywhere, new tests start at TC-151)
- This prevents ID collisions when tests are moved between suites

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
- Unified AI runtime (GitHub Copilot SDK)
- Multi-provider support via SDK configuration
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
      { "name": "github-models", "model": "gpt-4o", "enabled": true }
    ],
    "critic": {
      "enabled": true,
      "provider": "github-models",
      "model": "gpt-4o-mini"
    }
  }
}
```

### Supported Providers (via Copilot SDK)

| Provider | Description |
|----------|-------------|
| `github-models` | GitHub Models API (default) |
| `azure-openai` | Azure OpenAI Service |
| `azure-anthropic` | Azure AI with Anthropic models |
| `openai` | OpenAI API (BYOK) |
| `anthropic` | Anthropic API (BYOK) |

For BYOK providers, set `api_key_env` to the environment variable name containing your API key.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

MIT License. See [LICENSE](LICENSE) for details.
