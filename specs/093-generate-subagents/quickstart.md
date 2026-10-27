# Quickstart: verifying Spec 093

## Automated

```bash
dotnet test --filter "FullyQualifiedName~VerdictResultsTests"          # T1 (ingest-verdict results / --ids)
dotnet test --filter "FullyQualifiedName~AgentInstallLayoutTests"      # T2 (six agents)
dotnet test --filter "FullyQualifiedName~GenerateAgentTests"           # T3 (analyst, generator, repairer lines)
dotnet test --filter "FullyQualifiedName~GenerateOrchestratorTests"    # T4, T5 (generate / update skill guards)
dotnet test --filter "FullyQualifiedName~SkillsManifestTests"          # retargeted rows; the written==0 guard verbatim
dotnet test                                                             # everything; only ProfileFormatLoaderTests fails
```

## With the built CLI

1. `ingest-verdict --suite s --all --ids TC-101,TC-999 --output-format json` on a workspace with verdict files →
   `results` for TC-101, `not_found: ["TC-999"]`, counts as before; `--ids` alone → exit 1 with the message.
2. A new workspace (`spectra init`) → six agents under `.claude/agents/`; a pre-093 workspace (the two new agent files
   and manifest entries removed, the generate skill customised) → `update-skills` adds the two, skips the skill.

## Manual acceptance (a live Claude Code session; the user runs it)

`acceptance/setup-workspace.sh <empty-dir> [spectra command]` builds a workspace from the real init with three indexed
documents (storage, sharing, retention) — enough for ≥ 24 tests in ≥ 3 batches.
1. **SC-001:** `/context`, then "generate tests for <area>", approve, let it finish (critic + repairs), `/context`
   again: no auto-compaction; growth / created tests ≤ 300 tokens; no doc text, compiled prompt, test JSON or verdict
   JSON in the main transcript.
2. **SC-002:** the same run on the 092 build (same model): the grounded share is not lower with 093.
3. **SC-003:** the final report lists every created test by id and title with its outcome.
