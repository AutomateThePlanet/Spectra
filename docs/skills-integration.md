---
title: Skills Integration
parent: User Guide
nav_order: 8
---

# Skills Integration

How SPECTRA integrates with Claude Code through `.claude/skills/<name>/SKILL.md` files.

Related: [CLI Reference](cli-reference.md) | [Getting Started](getting-started.md)

---

## Overview

SPECTRA ships both authoring and execution orchestration as Claude Code skills under `.claude/skills/<name>/SKILL.md`. These SKILLs let Claude Code invoke CLI commands through natural language: they translate what users say into CLI commands with `--output-format json --verbosity quiet`, parse the JSON output, and present results conversationally. The bundled SKILLs are auto-discovered from embedded resources and installed/refreshed by `spectra init` and `spectra update-skills` through the same install pipeline.

The critic runs as a Claude Code subagent defined at `.claude/agents/spectra-critic.agent.md` (a `context: fork` subagent). The generation SKILL invokes `spectra-critic` as a **mandatory, explicit step** before a generated test is accepted.

> The port is complete: the test **execution** agent is a native Claude Code skill/agent
> (`.claude/skills/spectra-execute/`), not a GitHub Copilot agent. The in-process GitHub Copilot SDK
> generation path was removed entirely, so there is no transitional in-process path left.
> See [Claude Code v2 vs. the GitHub Copilot SDK v1](claude-code-v2-migration.md) for the full
> before/after picture.

## Architecture

```
User (Claude Code) → SKILL file → deterministic CLI commands → a turn in your session → CLI ingest → response
```

1. User asks: "Generate test cases for the checkout suite"
2. The `spectra-generate` SKILL runs `spectra ai compile-prompt --suite checkout --count <n>`, which emits a deterministic prompt to stdout, with no model call yet
3. The prompt is answered as an ordinary turn in the user's own Claude Code session
4. The SKILL persists the result via `spectra ai ingest-tests`, runs the mandatory `spectra-critic` subagent verification step per test, and presents: "Generated 10 test cases (8 grounded, 1 partial, 1 rejected)"

## Bundled SKILLs

Created by `spectra init` in `.claude/skills/` (one `SKILL.md` per skill directory):

| SKILL | Path | Drives |
|-------|------|-------|
| SPECTRA Generate | `.claude/skills/spectra-generate/SKILL.md` | Behavior analysis + generation over the `compile-prompt`/`ingest-tests` seam, with the mandatory critic step |
| SPECTRA Update | `.claude/skills/spectra-update/SKILL.md` | Classification (`spectra ai update`) + doc-aware edits over the `compile-update-prompt`/`ingest-update` seam |
| SPECTRA Coverage | `.claude/skills/spectra-coverage/SKILL.md` | `spectra ai analyze --coverage --auto-link` |
| SPECTRA Dashboard | `.claude/skills/spectra-dashboard/SKILL.md` | `spectra dashboard --output ./site` |
| SPECTRA Validate | `.claude/skills/spectra-validate/SKILL.md` | `spectra validate` |
| SPECTRA List | `.claude/skills/spectra-list/SKILL.md` | `spectra list` and `spectra show` |
| SPECTRA Profile | `.claude/skills/spectra-init-profile/SKILL.md` | `spectra init-profile` |
| SPECTRA Help | `.claude/skills/spectra-help/SKILL.md` | Help and command reference (terse, flag-oriented) |
| SPECTRA Criteria | `.claude/skills/spectra-criteria/SKILL.md` | `spectra docs changed` → extraction seam → `ingest-criteria`, plus import |
| SPECTRA Docs | `.claude/skills/spectra-docs/SKILL.md` | `spectra docs index` with progress page |
| SPECTRA Prompts | `.claude/skills/spectra-prompts/SKILL.md` | `spectra prompts list/show/reset/validate` for prompt template customization |
| SPECTRA Delete | `.claude/skills/spectra-delete/SKILL.md` | Preview-then-confirm test case deletion |
| SPECTRA Suite | `.claude/skills/spectra-suite/SKILL.md` | Suite list/rename/delete |
| SPECTRA Review Flagged | `.claude/skills/spectra-review-flagged/SKILL.md` | Human review of partial/flagged verdicts (accept, retry repair, delete) |
| SPECTRA Execute | `.claude/skills/spectra-execute/SKILL.md` | Orchestrates `spectra run` + the local web console for a manual test run |
| SPECTRA Quickstart | `.claude/skills/spectra-quickstart/SKILL.md` | Workflow-oriented onboarding & walkthroughs |

## Critic Subagent

Created by `spectra init` in `.claude/agents/`:

| Agent | Path | Purpose |
|-------|------|---------|
| SPECTRA Critic | `spectra-critic.agent.md` | Independent verification of generated tests, run as a `context: fork` subagent |

The generation SKILL invokes `spectra-critic` as a mandatory explicit step before a test is accepted. Execution (`spectra-execute`) is a skill, not a subagent, and it orchestrates `spectra run` directly rather than running in an isolated context.

## Key Patterns

### JSON Output for SKILLs

Every SKILL uses these flags:
```bash
spectra <command> --output-format json --verbosity quiet
```

- `--output-format json`: Structured JSON on stdout (no colors, spinners, or ANSI codes)
- `--verbosity quiet`: Only the final result (no progress indicators)

#### Analysis JSON includes a `technique_breakdown`

The `analysis` object emitted by `spectra ai ingest-analysis` exposes both a category `breakdown`
and a `technique_breakdown` map. Keys are short ISTQB codes (`BVA`, `EP`, `DT`, `ST`, `EG`, `UC`);
values are counts. Always present (`{}` when empty) so SKILL parsers can rely on the field existing.

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

#### `spectra testimize check` JSON

When the optional Testimize integration is in use, `spectra testimize check
--output-format json` returns a `TestimizeCheckResult` object with the
required fields `enabled`, `installed`, `healthy` plus `mode`, `strategy`,
and (when not installed) `install_command`. See [Testimize Integration](testimize-integration.md).

### Generation Flow in SKILLs

The generate SKILL drives the deterministic seam directly; there is no single `spectra ai generate`
command anymore:

```bash
# Analysis
spectra ai compile-analysis-prompt --suite {suite} --output-format json
# … a turn in your session answers it …
spectra ai ingest-analysis --suite {suite} --output-format json

# Bulk generation
spectra ai compile-prompt --suite {suite} --count {n} [--focus "{text}"] --output-format json
# … a turn in your session answers it …
spectra ai ingest-tests {suite} --output-format json

# From a plain-language description (single test)
spectra ai compile-prompt --suite {suite} --from-description "{text}" [--context "{ctx}"] --output-format json
# … a turn in your session answers it …
spectra ai ingest-tests {suite} --output-format json
```

Every `compile-*` call is followed by an `ingest-*` call for the generated test's mandatory
`spectra-critic` verification step before it's accepted.

### Intent Routing in Chat

The `spectra-generate` SKILL uses an intent-routing table to choose between `compile-prompt`'s
two modes:

| User intent | Signal | Flow |
|-------------|--------|------|
| Explore a feature area | "Generate tests for...", "Cover... module" | Analyze → generate with `--focus` |
| Create a specific test | "Add a test for...", "I need a test that verifies..." | `--from-description` (1 test, no analysis, no count question) |

The key rule: if you can read the user's request as a single test case title, the flow routes to `--from-description`. If it's a topic to explore, it routes to `--focus`. The flow never asks the user for count or scope to disambiguate, since the topic-vs-scenario shape is the only signal.

When `--from-description` runs in a project that has documentation and acceptance criteria, the CLI best-effort loads matching docs (capped at 3 docs × 8000 chars) and matching `.criteria.yaml` entries. The matching criteria are injected into the generation prompt as the **mandatory criteria-mapping instruction**, the same "you MUST map each test case to matching acceptance criteria" block the batch flow uses, so the model reliably populates the `criteria` frontmatter field.

The resulting test case has populated `source_refs` and `criteria` fields, but `grounding.verdict` stays `manual` by design, since from-description runs no independent critic and populating `criteria` is not verification. Doc context is used for terminology alignment only, never for verification. Consequently a from-description test counts toward **acceptance-criteria coverage** (its `criteria` field is populated) but is excluded from **grounded** statistics (its verdict is `manual`). See [Coverage](coverage.md) for how the two are tallied separately.

### CI Pipelines and Automation

The `compile-*`/`ingest-*` seam commands are deterministic and non-interactive by construction,
so there's no interactive prompt to suppress. What a CI pipeline needs from Claude is a way to answer
the compiled prompt: drive the seam from a headless Claude Code invocation rather than an
interactive session. `spectra validate`, `spectra ai analyze --coverage`, and the other
non-model-calling commands accept `--no-interaction --output-format json` as usual for the parts of
the pipeline that don't need a model turn at all.

## Customizing SKILLs

`SKILL.md` files are plain Markdown, and you can edit them freely to:
- Add project-specific instructions
- Change default flags
- Add custom examples for your domain

If you modify a `SKILL.md` file, `spectra update-skills` will skip it, preserving your changes.

## Updating SKILLs

When upgrading SPECTRA CLI:
```bash
spectra update-skills
```

- Unmodified files are updated to the latest version
- User-modified files are preserved (skipped with warning)
- Missing files are recreated

## Skipping SKILLs

For projects that don't use Claude Code:
```bash
spectra init --skip-skills
```

This creates only core files (config, directories, templates) without the `.claude/skills/` SKILLs or the `.claude/agents/` critic subagent.
