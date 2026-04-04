# Skills Integration

How SPECTRA integrates with GitHub Copilot Chat through SKILL files.

Related: [CLI Reference](cli-reference.md) | [Getting Started](getting-started.md)

---

## Overview

SPECTRA ships 6 SKILL files and 2 agent prompts that enable Copilot Chat to invoke CLI commands through natural language. SKILLs translate what users say in chat into CLI commands with `--output-format json --verbosity quiet`, parse the JSON output, and present results conversationally.

## Architecture

```
User (Copilot Chat) → SKILL file → CLI command → JSON output → Chat response
```

1. User asks: "Generate test cases for the checkout suite"
2. SKILL matches the request and builds: `spectra ai generate --suite checkout --output-format json --verbosity quiet`
3. CLI executes and outputs structured JSON
4. SKILL parses JSON and presents: "Generated 10 tests (8 grounded, 1 partial, 1 rejected)"

## Bundled SKILLs

Created by `spectra init` in `.github/skills/`:

| SKILL | Path | Wraps |
|-------|------|-------|
| SPECTRA Generate | `spectra-generate/SKILL.md` | `spectra ai generate` with all session flags |
| SPECTRA Coverage | `spectra-coverage/SKILL.md` | `spectra ai analyze --coverage --auto-link` |
| SPECTRA Dashboard | `spectra-dashboard/SKILL.md` | `spectra dashboard --output ./site` |
| SPECTRA Validate | `spectra-validate/SKILL.md` | `spectra validate` |
| SPECTRA List | `spectra-list/SKILL.md` | `spectra list` and `spectra show` |
| SPECTRA Profile | `spectra-init-profile/SKILL.md` | `spectra init-profile` |

## Agent Prompts

Created by `spectra init` in `.github/agents/`:

| Agent | Path | Purpose |
|-------|------|---------|
| SPECTRA Execution | `spectra-execution.agent.md` | Test execution via MCP tools |
| SPECTRA Generation | `spectra-generation.agent.md` | Test generation via terminal + CLI |

## Key Patterns

### JSON Output for SKILLs

Every SKILL uses these flags:
```bash
spectra <command> --output-format json --verbosity quiet
```

- `--output-format json`: Structured JSON on stdout (no colors, spinners, or ANSI codes)
- `--verbosity quiet`: Only the final result (no progress indicators)

### Generation Session in SKILLs

The generate SKILL supports the full session flow:
```bash
# Standard generation
spectra ai generate --suite {suite} --output-format json --verbosity quiet

# From previous suggestions
spectra ai generate --suite {suite} --from-suggestions --output-format json --verbosity quiet

# User-described test
spectra ai generate --suite {suite} --from-description "{text}" --context "{ctx}" --output-format json --verbosity quiet

# Full auto (CI)
spectra ai generate --suite {suite} --auto-complete --output-format json --verbosity quiet
```

### Non-Interactive Mode

For CI pipelines and automated workflows:
```bash
spectra ai generate checkout --no-interaction --output-format json
```

Exit code 3 is returned if required arguments are missing when `--no-interaction` is set.

## Customizing SKILLs

SKILL files are plain Markdown — edit them freely to:
- Add project-specific instructions
- Change default flags
- Add custom examples for your domain

**Important**: If you modify a SKILL file, `spectra update-skills` will skip it (preserving your changes).

## Updating SKILLs

When upgrading SPECTRA CLI:
```bash
spectra update-skills
```

- Unmodified files are updated to the latest version
- User-modified files are preserved (skipped with warning)
- Missing files are recreated

## Skipping SKILLs

For projects that don't use Copilot Chat:
```bash
spectra init --skip-skills
```

This creates only core files (config, directories, templates) without `.github/skills/` or `.github/agents/`.
