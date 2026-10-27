# Spectra Development Guidelines

Last updated: 2026-10-20 | History: `CHANGELOG.md` and `specs/NNN-*/` (one folder per spec)

## Tech Stack
- C# 12, .NET 8+; System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet, CsvHelper,
  Microsoft.Extensions.FileSystemGlobbing, Microsoft.Data.Sqlite (`.execution/spectra.db`).
- No in-process model: all inference is the user's Claude Code session. Authoring ships as bundled skills
  (`.claude/skills/<name>/SKILL.md`) and subagents (`.claude/agents/*.agent.md`) that drive deterministic
  `spectra ai compile-*` → session turn → `ingest-*` commands. The critic is the `spectra-critic` subagent on
  `ai.critic.model` (default `sonnet`), written into the installed agent by `init` / `update-skills`; the generator,
  updater and repairer subagents start it per test (the main session only on fallback), and it sees the cited sections.
- Read-only git (`Spectra.Core.Git`) for renamed docs and history baselines; everything works without git.
- File-based state: `test-cases/<suite>/*.md` + `_index.json` + `_source-baseline.json`, `docs/_index/` (doc index v2),
  `docs/criteria/` (per-doc `.criteria.yaml` + `_criteria_index.yaml`), `.spectra/` (prompts, snapshots, scratch),
  `.claude/skills/<name>/reference.md` (generate/update reference files).

## Project Structure

```text
src/
  Spectra.CLI/                        # the spectra command
    Commands/                         # one folder per command group (Ai, Baseline, Docs, Generate, Run, …)
    Agent/                            # prompt builders and tools kept from the agent era (Analysis, Critic, Tools)
    Analysis/ Generation/             # analyzer input, prompt compilers, generation plan and batches
    Baseline/ Criteria/ Extraction/   # change detection + triage, criteria store, criteria seam
    Verification/                     # critic prompts, verdict ingest, freshness, repair prompts
    Source/ Index/ IO/                # document map and index, _index.json, test file writers
    Validation/ Classification/ Review/ Interactive/   # dedup, legacy update classifier and review, suite picker
    Coverage/ Dashboard/              # coverage reports, dashboard data and templates
    Prompts/ Profile/ Skills/ Templates/   # template engine, profiles, bundled skills/agents, init templates
    Output/ Results/ Progress/        # presenters and hints, JSON result models, progress pages
    Infrastructure/ Options/ Services/ Cancellation/   # exit codes, logging, global options, cancellation
  Spectra.Core/                       # shared library
    Models/ Config/ Parsing/          # TestCase, config, Markdown+YAML parsing, lossless layout
    Baseline/ Git/ Text/ Diagnostics/ # section fingerprints, read-only git, text normalisation, parse counters
    Criteria/ Coverage/ Index/        # criteria matching, coverage analyzers, document index
    Validation/ Update/ IdAllocation/ Source/ Profile/ Storage/ BugReporting/
  Spectra.Execution/                  # execution engine behind `spectra run` (namespaces still Spectra.MCP.*)
    Execution/ Storage/ Reports/ Identity/ Infrastructure/ Screenshots/
dashboard-site/                       # static dashboard template (index.html, styles/, scripts/, functions/)
tests/
  Spectra.Core.Tests/ Spectra.CLI.Tests/ Spectra.Execution.Tests/ Spectra.Integration.Tests/ TestFixtures/
```

## Commands

```bash
dotnet build                                        # Build (TreatWarningsAsErrors on CLI and Core)
dotnet test                                         # Test
dotnet run --project src/Spectra.CLI -- <command>   # Run the CLI
# Global: --output-format json|human  --no-interaction  --verbosity quiet  --dry-run  --no-review

# Setup
spectra init [--skip-skills] [--agent <list>|auto] [--regenerate]   # --agent: copilot,cursor,devin,codex,gemini,antigravity,kiro,opencode
spectra update-skills
spectra config [<key> [<value>]] | spectra config add-automation-dir|remove-automation-dir|list-automation-dirs <path>
spectra prompts list|show|validate|reset [<template-id>] [--raw] [--all]

# Generation seam (driven by the spectra-generate skill; there is no `ai generate` command)
spectra ai compile-analysis-prompt --suite X [--doc-suite D] [--focus "..."] [--include-archived]
spectra ai ingest-analysis --suite X --from FILE
spectra ai compile-prompt --suite X (--batch B1 | --count N | --from-description "..." [--context "..."])
spectra ai ingest-tests X --from FILE
spectra ai compile-critic-prompt --suite X --test ID [--docs PATH] [--full-docs]
spectra ai ingest-verdict --suite X --all [--ids a,b] | spectra ai ingest-verdict --from FILE
spectra ai ingest-grounding --suite X (--all [--ids a,b] [--repaired] | --test ID)
spectra ai audit-grounding --suite X [--action ground|repair|review|critic|none] [--ids a,b]
spectra ai compile-repair-prompt --suite X --test ID | spectra ai compile-repair-batch --suite X
spectra ai review-flagged --suite X | spectra ai record-drop --suite X --test ID [--reason R]

# Criteria seam (driven by the spectra-criteria skill)
spectra docs changed [--include-unchanged]
spectra ai compile-extraction-prompt --doc PATH [--component C]
spectra ai ingest-criteria --doc PATH [--from FILE]
spectra docs apply-move --from OLD --to NEW | spectra docs retire --doc PATH | spectra docs migrate-criteria

# Update (driven by the spectra-update skill; `ai update` is the legacy classifier)
spectra ai update-triage (--suite X | --all-suites) [--no-auto-ack]
spectra ai relink-sources (--suite X | --all-suites) [--min-similarity 50-100]
spectra ai compile-update-prompt --suite X (--test-id ID | --group G2)
spectra ai ingest-update X (--all | --test-id ID [--from FILE]) [--keep-baseline]
spectra ai update-ack --suite X (--ids a,b | --group G2)
spectra ai source-baseline --init (--suite X | --all-suites) [--force] [--from-date ISO | --from-ref REV --owner PATH]

# Docs, coverage, dashboard, tests
spectra docs index [--force] [--no-migrate] [--suites a,b] | spectra docs list-suites | spectra docs show-suite <id>
spectra docs topology
spectra ai analyze --coverage [--format json|markdown] [--output FILE] [--auto-link]
spectra ai analyze --import-criteria FILE [--replace] | spectra ai analyze --list-criteria [--component X]
spectra dashboard --output ./site [--title "..."] [--preview]
spectra validate | spectra list [--suite X] | spectra show <id> | spectra index [--rebuild]
spectra delete <ids…> [--force] | spectra suite list|rename|delete | spectra doctor ids [--fix] | spectra cancel

# Execution (CLI-only)
spectra run list-suites|list-active|selections|history [--suite]
spectra run start <suite> [--priorities] [--tags] [--components] [--test-ids] [--selection] [--environment]
spectra run status|summary|show [<run-id>] | spectra run advance [<handle>] --status pass|fail|blocked|skip
spectra run skip|note|screenshot|screenshot-clipboard [<handle>] | spectra run bulk-record [<run-id>] --status S
spectra run retest|pause|resume|cancel|finalize [<run-id>] | spectra run cancel-all | spectra run console [--stop]
```

## Code Style
- PascalCase types/methods, camelCase locals; nullable reference types enabled.
- All I/O async with the `Async` suffix.
- xUnit tests with structured results (never throw on validation errors).
- In JSON mode stdout is only the JSON document; the human summary goes to stderr (constitution III).
- Config rules are checked against the embedded init template (`src/Spectra.CLI/Templates/spectra.config.json`).

## Execution
`spectra run` drives the `Spectra.Execution` engine over `.execution/spectra.db` (WAL); every short-lived process
reconstructs the queue from a durable snapshot. Reports are JSON, Markdown and HTML. The separate BELLATRIX/Nova MCP
that drives the system under test is unrelated to SPECTRA's removed execution adapter.

## Active Technologies
- C# 12, .NET 8+, System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet (unchanged since 099)

## Recent Changes

| Spec | Summary |
|------|---------|
| 104 | `spectra init --agent` for copilot/cursor/devin/codex/gemini/antigravity/kiro/opencode (`src/Spectra.CLI/Install/`, neutral emit-time transform, `.spectra/manifest.<agent>.json`); Claude install byte-identical (`ClaudeInstallGoldenTests`) |
| 103 | Human review record (merged from `078-human-review-record`): `review-flagged --action`, `reviewed_by/at`, `--re-verify`, suppression, staged `--fix` review |
| 102 | Review fixes for 101: critic uses the critic-verification template, case-insensitive ref grouping, no re-critic for unchanged repairs, `created:` lines, `Agent` tool name |
| 101 | Token economy: critic gets cited sections (`--full-docs` = old input), critic runs in work subagents, lean skills + `reference.md`, agent frontmatter |
| 100 | Docs, agent guidance and constitution 2.1.0 catch up with 078–099; "When documentation changes" guide; text only |
| 099 | Performance and cleanup: one suite parse per ingest, layout-free read-only parses, dead code removed, 0 warnings |
| 098 | Correctness fixes: legacy `ai.critic` ignored, audit actions by verdict, `generation.coverage_threshold` |
| 097 | Verification hardening: `--ids` grounding, truthful `audit-grounding`, stale-verdict refusals, `ai.critic.model` |
| 096 | Workspace safety: `update-skills` keeps edits, safe `suite delete`, `validate` exit 1, gitignore markers |
| 095 | One per-behaviour coverage rule; plans batch only uncovered behaviours |
| 094 | Section-scoped generation batches (`compile-prompt --batch`, `.spectra/generation-plan/`) |
| 093 | `spectra-generate` orchestrates `spectra-analyst` / `spectra-generator` subagents; `ingest-verdict --all` results |
| 092 | Update and criteria skills orchestrate `spectra-updater` / `spectra-repairer` / `spectra-extractor` subagents |
| 091 | Review fixes for 090: `compiled_at` freshness, `ingest-grounding --ids`, kept hallucinated tests |
| 090 | Re-verify after update: edits drop the verdict, `VERDICT_STALE`, `failed-<id>.json`, safe delete |
| 089 | Review fixes for 088: separate repair staging, `unchanged_pending`, exit 8 |
| 088 | Update prompts with section diffs and linked criteria; `--group` prompts; staged batch ingest |
| 087 | Review fixes for 086: submodules, git executable and environment, case-insensitive renames |
| 086 | Read-only git: `relink-sources`, RELINK bucket, history bootstrap, `docs topology` |
| 085 | Review fixes for 084: one anchor matcher, prune only when all parsed, `--keep-baseline` |
| 084 | Source-change baseline: `update-triage`, `update-ack`, `source-baseline` |
| 083 | One config rule: no section is ever required; checked against the init template |
| 082 | Review fixes for 081 (config sections, failure codes) |
| 081 | Review fixes for 080 (ownership, config, tag whitelist, failure shape) |
| 080 | Review fixes for 078/079 (own-file writes, source roots, frontmatter layout) |
| 079 | Stable criterion IDs, per-document criteria files, `docs retire` / `apply-move` |
| 078 | Lossless test writes, classify-only `ai update`, `SourceRefResolver` |

Detail for every spec: `CHANGELOG.md` and `specs/NNN-*/` (spec, plan, research, tasks with Notes).
