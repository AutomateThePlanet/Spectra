---
title: Skills Integration
parent: User Guide
nav_order: 8
---

# Skills Integration

How SPECTRA integrates with GitHub Copilot Chat through SKILL files.

Related: [CLI Reference](cli-reference.md) | [Getting Started](getting-started.md)

---

## Overview

SPECTRA ships 12 SKILL files and 2 agent prompts that enable Copilot Chat to invoke CLI commands through natural language. SKILLs translate what users say in chat into CLI commands with `--output-format json --verbosity quiet`, parse the JSON output, and present results conversationally. The bundled SKILLs are auto-discovered from embedded resources and refreshed by `spectra update-skills`.

## Architecture

```
User (Copilot Chat) → SKILL file → CLI command → JSON output → Chat response
```

1. User asks: "Generate test cases for the checkout suite"
2. SKILL matches the request and builds: `spectra ai generate --suite checkout --output-format json --verbosity quiet`
3. CLI executes and outputs structured JSON
4. SKILL parses JSON and presents: "Generated 10 test cases (8 grounded, 1 partial, 1 rejected)"

## Bundled SKILLs

Created by `spectra init` in `.github/skills/`:

| SKILL | Path | Wraps |
|-------|------|-------|
| SPECTRA Generate | `spectra-generate/SKILL.md` | `spectra ai generate` with all session flags |
| SPECTRA Update | `spectra-update/SKILL.md` | `spectra ai update` with classification reporting |
| SPECTRA Coverage | `spectra-coverage/SKILL.md` | `spectra ai analyze --coverage --auto-link` |
| SPECTRA Dashboard | `spectra-dashboard/SKILL.md` | `spectra dashboard --output ./site` |
| SPECTRA Validate | `spectra-validate/SKILL.md` | `spectra validate` |
| SPECTRA List | `spectra-list/SKILL.md` | `spectra list` and `spectra show` |
| SPECTRA Profile | `spectra-init-profile/SKILL.md` | `spectra init-profile` |
| SPECTRA Help | `spectra-help/SKILL.md` | Help and command reference (terse, flag-oriented) |
| SPECTRA Criteria | `spectra-criteria/SKILL.md` | `spectra ai analyze --extract-criteria` and import |
| SPECTRA Docs | `spectra-docs/SKILL.md` | `spectra docs index` with progress page |
| SPECTRA Prompts | `spectra-prompts/SKILL.md` | `spectra prompts list/show/reset/validate` for prompt template customization |
| SPECTRA Quickstart | `spectra-quickstart/SKILL.md` | Workflow-oriented onboarding & walkthroughs (12 workflows) |

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

#### Analysis JSON includes a `technique_breakdown` (spec 037)

The `analysis` subobject in `.spectra-result.json` from `spectra ai generate
--analyze-only` exposes both the existing category `breakdown` and a new
`technique_breakdown` map. Keys are short ISTQB codes (`BVA`, `EP`, `DT`,
`ST`, `EG`, `UC`); values are counts. Always present (`{}` when empty)
so SKILL parsers can rely on the field existing.

```json
{
  "analysis": {
    "total_behaviors": 141,
    "breakdown": { "happy_path": 42, "boundary": 38, "negative": 24, "edge_case": 18 },
    "technique_breakdown": { "BVA": 38, "EP": 24, "UC": 32, "EG": 15, "DT": 18, "ST": 14 }
  }
}
```

The `spectra-generate` SKILL renders both breakdowns to the user when
presenting the analyze recommendation.

#### `spectra testimize check` JSON (spec 038)

When the optional Testimize integration is in use, `spectra testimize check
--output-format json` returns a `TestimizeCheckResult` object with the
required fields `enabled`, `installed`, `healthy` plus `mode`, `strategy`,
and (when not installed) `install_command`. See [Testimize Integration](testimize-integration.md).

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

### Intent Routing in Chat (spec 033)

The `spectra-generate` SKILL contains a dedicated section for `--from-description` and an intent-routing table that the `spectra-generation` agent uses to choose between flows:

| User intent | Signal | Flow |
|-------------|--------|------|
| Explore a feature area | "Generate tests for...", "Cover... module" | Main analyze → generate flow with `--focus` |
| Create a specific test | "Add a test for...", "I need a test that verifies..." | `--from-description` (1 test, no analysis, no count question) |
| Generate from suggestions | "Use the previous suggestions" | `--from-suggestions` |

**Key rule**: if you can read the user's request as a single test case title, the agent routes to `--from-description`. If it's a topic to explore, the agent routes to `--focus`. The agent never asks the user for count or scope to disambiguate — the topic-vs-scenario shape is the only signal.

When `--from-description` runs in a project that has documentation and acceptance criteria, the CLI best-effort loads matching docs (capped at 3 docs × 8000 chars) and matching `.criteria.yaml` entries as formatting context. The resulting test case has populated `source_refs` and `criteria` fields, but `grounding.verdict` stays `manual` — doc context is used for terminology alignment only, never for verification.

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
