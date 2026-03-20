# CLI Reference

All SPECTRA CLI commands organized by function.

Related: [Getting Started](getting-started.md) | [Configuration](configuration.md)

---

## Global Options

| Option | Short | Description |
|--------|-------|-------------|
| `--verbosity` | `-v` | Output level: `quiet`, `minimal`, `normal`, `detailed`, `diagnostic` |
| `--dry-run` | | Preview changes without executing |
| `--no-review` | | Skip interactive review (CI mode) |

---

## Core Commands

### `spectra init`

Initialize a repository for SPECTRA.

```bash
spectra init
```

Creates `spectra.config.json`, `docs/`, `tests/`, `docs/requirements/_requirements.yaml` (commented template), and `.github/skills/` agent definition.

### `spectra validate`

Validate all test files against the schema.

```bash
spectra validate
spectra validate --path tests/checkout
```

### `spectra index`

Rebuild `_index.json` files for all suites.

```bash
spectra index
spectra index --rebuild
```

### `spectra auth`

Check authentication status for all configured providers.

```bash
spectra auth
spectra auth -p github-models
spectra auth -p openai
```

---

## Documentation Commands

### `spectra docs index`

Build or incrementally update the documentation index (`docs/_index.md`).

```bash
spectra docs index                # Incremental update (only changed files)
spectra docs index --force        # Full rebuild
```

The index contains per-document metadata (title, sections with summaries, key entities, word/token counts, content hashes). The AI agent reads this lightweight index (~1-2K tokens) instead of scanning all files.

Content hashes enable incremental updates — only changed files are re-indexed. The index is also auto-refreshed before `spectra ai generate` runs.

See [Document Index](document-index.md) for full details.

---

## AI Generation Commands

### `spectra ai generate`

Generate test cases from documentation. Supports two modes.

**Interactive Mode** — guided prompts for exploratory generation:

```bash
spectra ai generate
```

Launches a guided session that:
1. Lists available suites with test counts (or create a new suite)
2. Asks what kind of tests you want (full coverage, negative only, specific area)
3. Shows existing tests matching your focus
4. Identifies coverage gaps in your documentation
5. Generates tests and writes them directly
6. Prompts to generate more for remaining gaps

**Direct Mode** — specify suite and options upfront:

```bash
spectra ai generate checkout --count 10
spectra ai generate checkout --focus "error handling"
spectra ai generate payments --focus "negative validation edge cases"
spectra ai generate checkout --skip-critic
spectra ai generate checkout --count 10 --no-interaction --no-review
spectra ai generate checkout --count 5 --dry-run
```

| Option | Short | Description |
|--------|-------|-------------|
| `--count` | `-n` | Number of tests to generate (default: 5) |
| `--focus` | `-f` | Focus area description for targeted generation |
| `--skip-critic` | | Skip grounding verification (faster) |
| `--no-interaction` | | Disable interactive prompts |
| `--no-review` | | Skip interactive review of generated tests |
| `--dry-run` | | Preview without writing files |

**Exit codes:** `0` = success, `1` = error.

### `spectra ai update`

Update existing tests when documentation changes. Classifies tests as UP_TO_DATE, OUTDATED, ORPHANED, or REDUNDANT.

```bash
spectra ai update                             # Interactive mode
spectra ai update checkout                    # Direct mode
spectra ai update checkout --diff             # Show proposed changes
spectra ai update checkout --delete-orphaned  # Auto-delete orphaned tests
spectra ai update checkout --no-interaction   # CI mode
```

| Option | Short | Description |
|--------|-------|-------------|
| `--diff` | `-d` | Show diff of proposed changes |
| `--delete-orphaned` | | Automatically delete orphaned tests |
| `--no-interaction` | | Disable interactive prompts |
| `--no-review` | | Skip interactive review |
| `--dry-run` | | Preview without applying changes |

### `spectra ai analyze --coverage`

Run unified coverage analysis across three dimensions.

```bash
spectra ai analyze --coverage
spectra ai analyze --coverage --format json --output coverage.json
spectra ai analyze --coverage --format markdown --output coverage.md
spectra ai analyze --coverage --auto-link
spectra ai analyze --coverage --verbosity detailed
```

| Option | Description |
|--------|-------------|
| `--format` | Output format: `text` (default), `json`, `markdown` |
| `--output` | Write report to file |
| `--auto-link` | Scan automation code and update `automated_by` fields in test files |

See [Coverage](coverage.md) for details on the three coverage types.

---

## Dashboard Commands

### `spectra dashboard`

Generate an interactive HTML dashboard.

```bash
spectra dashboard --output ./site
spectra dashboard --output ./site --title "My Project QA Dashboard"
spectra dashboard --output ./site --dry-run
spectra dashboard --output ./site --template ./my-template
```

The dashboard reads from `tests/*/_index.json`, `.execution/spectra.db`, and `reports/*.json`.

Features: Suite overview, execution history, three-section coverage analysis, coverage map (D3.js), test browser, trend analysis.

See [Getting Started](getting-started.md) for serving locally.

---

## Profile Commands

### `spectra init-profile`

Create or edit a test generation profile.

```bash
spectra init-profile                                    # Interactive wizard
spectra init-profile --non-interactive --detail-level detailed --min-negative 3
spectra init-profile --suite tests/checkout             # Suite-level override
spectra init-profile --edit --min-negative 5            # Update existing
spectra init-profile --force                            # Re-run full wizard
```

### `spectra profile show`

View the effective profile.

```bash
spectra profile show
spectra profile show --json
spectra profile show --context
spectra profile show --suite tests/checkout
```

See [Generation Profiles](generation-profiles.md) for details.

---

## Execution Commands

### `spectra mcp start`

Start the MCP execution server.

```bash
spectra mcp start
```

The server runs on stdio transport (JSON-RPC 2.0). See [Execution Agent Overview](execution-agent/overview.md) for available MCP tools.
