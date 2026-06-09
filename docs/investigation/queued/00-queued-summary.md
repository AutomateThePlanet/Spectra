# Queued features 048–051 — fate against the post-migration architecture

> **Investigation-only.** No production code, specs, configs, or skills were written or modified.
> This summarizes four per-feature investigations; each claim there is cited `file:line`.

## Scope and two framing facts

These four features were sketched **before** the v2 migration (repo specs 053–059), against the old
Copilot-driven, in-process-model architecture. That architecture is gone: the CLI is model-free,
generation runs on a compile→in-session-generate→ingest seam, the critic is a `context: fork`
subagent, the MCP engine is reused unchanged, and coverage is purely lexical. Each feature is judged
against the *current* code.

1. **No draft files exist** for any of the four. No `queued/`/`backlog/`/`draft/` directory exists;
   content searches for the feature phrases found nothing. Intent was reconstructed from the
   one-line summaries.
2. **Numbering collision — important.** The repo already ships *implemented, unrelated* specs
   `048-criteria-coverage-guards`, `049-from-description-index-parity`,
   `050-from-desc-criteria-injection`, `051-filter-schema-alignment` (all in v1.52.6). The numbers
   048–051 are reused here only as working labels for the *conceptual features*. They are not those
   specs.

## Verdict table

| # | Feature | Verdict | Owning seam (post-migration) | Key file reference |
|---|---------|---------|------------------------------|--------------------|
| 050 | Coverage doc exclusions | **SURVIVES UNCHANGED** | Lexical coverage path: `CoverageConfig` + `DocumentationCoverageAnalyzer`, reusing `ExclusionPatternMatcher` | `DocumentationCoverageAnalyzer.cs:20-39`; `AnalyzeHandler.cs:174-208` |
| 051 | Execution report enrichment | **SURVIVES UNCHANGED** | MCP engine report pipeline (client-agnostic) | `ExecutionReport.cs:8-68`; `TestResultEntry.cs:8-66`; `ReportGenerator.cs:14-69`; `ReportWriter.cs` |
| 048 | Critic boundary-value detection | **SURVIVES-WITH-REWRITE** | Critic `context: fork` subagent **or** the new analysis phase — not the retired in-process critic | `spectra-critic.agent.md:17-26,42-69`; `CriticFactory.cs:100-117`; `spectra-generation.agent.md:13` |
| 049 | Targeted update instructions | **SURVIVES-WITH-REWRITE** | A net-new inverted update seam (`compile-update-prompt`→generate→`ingest-update`) + `spectra-update` skill; `TestClassifier` stays as selector | `UpdateHandler.cs:234-237,537-539,634-702`; `AiCommand.cs:20-35` |

## Why the two "unchanged" vs. the two "rewrite"

- **050 and 051 sit on paths the migration never touched.** Coverage was lexical before and after
  (no model, no embeddings); the MCP report pipeline was always deterministic and client-agnostic.
  Both features are additive (config+filter; schema+rendering) and implementable roughly as
  originally intended.
- **048 and 049 sat on the in-process model layer that the migration dismantled.** The in-process
  critic is retired (`CriticFactory.cs:100-117`) and the update flow has no model path at all
  (`UpdateHandler.cs:234-237`). Both intents are still wanted, but the *mechanism* must be rebuilt
  onto the new seams (subagent/analysis-phase for 048; a new inverted compile/ingest pair for 049).

## Independence — confirmed

The four are **independent features, not a dependency chain**, and can be implemented in parallel.
They touch disjoint seams with no shared files:

- 050 → `Spectra.Core/Coverage` + `CoverageConfig` (+ CLI `AnalyzeHandler`).
- 051 → `Spectra.MCP/Reports` + `Spectra.Core/Models/Execution`.
- 048 → critic subagent / verdict-ingest **or** analysis-ingest (`Spectra.CLI`).
- 049 → a new update compile/ingest pair + `Spectra.Core/Update/TestClassifier` + the update skill.

The only soft overlaps are shared regression-net components — `TestPersistenceService` (049's ingest
boundary) and the `Spectra.Core` model records (050/051) — which argue for sequencing tests
carefully, not for a hard ordering between features.

## Blocked by the still-pending provider/SDK retirement?

**None of the four are blocked.** Spec 059 deliberately retained `CopilotService`, `ProviderMapping`,
`ai.providers`, and the GitHub Copilot SDK because in-process **criteria extraction** and **Copilot
auth** still use them. None of these four features touches those two paths:

- **048** — the critic's SDK use was already retired in Spec 058 (`CriticFactory.cs:100-117`); the
  analysis-phase alternative runs in the interactive session.
- **049** — the new update seam runs in the interactive session (no in-process model), exactly like
  post-059 generation.
- **050 / 051** — pure deterministic paths (lexical coverage; MCP reports) that import no model and
  no Copilot SDK.

So all four can proceed before that shared-infra cleanup lands.

## Notes (not recommendations)

- This investigation proposes **no spec numbers** and writes no specs — it records each feature's
  fate and owning seam only.
- Two stale-text cleanups surfaced and are worth folding into whichever spec touches them:
  `spectra-update.md:9-12,31` claims the update command "rewrites" tests (false under the model-free
  flow), and the codebase now has three overlapping doc-exclusion concepts
  (`SourceConfig.ExcludePatterns`, `CoverageConfig.AnalysisExcludePatterns`, and the proposed
  coverage-scoped exclusion) that must be named distinctly to avoid confusion.

## Per-feature documents

- [`048-critic-boundary-values.md`](048-critic-boundary-values.md)
- [`049-targeted-update-instructions.md`](049-targeted-update-instructions.md)
- [`050-coverage-doc-exclusions.md`](050-coverage-doc-exclusions.md)
- [`051-execution-report-enrichment.md`](051-execution-report-enrichment.md)
