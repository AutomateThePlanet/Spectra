# Implementation Plan: Generate Skill as Orchestrator (Phase 5b)

**Branch**: `093-generate-subagents` | **Date**: 2026-10-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/093-generate-subagents/spec.md`

## Summary

The generation skill keeps its analyze → approve → generate → verify → repair flow but hands analysis, each batch's
generation and the repairs to forked agents (`spectra-analyst`, `spectra-generator`, the 092 `spectra-repairer`). Both
the generation and update skills route each test by the verdict the CLI classified — the one CLI addition gives the
batch verdict ingest per-test `results` and an `--ids` filter. The repairer reports every ingested file by id, and every
written edit or repair is re-verified or listed unverified (the 092 review findings).

## Technical Context

**Language/Version**: C# 12, .NET 8 (one command change + a shared helper); Markdown skill and agent content.
**Primary Dependencies**: `IngestVerdictCommand`, `IngestGroundingCommand` (its `--ids` parsing becomes shared),
`VerdictIngestor`, the bundled skills `spectra-generate` and `spectra-update`, the agents (092 layout and install rule).
No new packages.
**Storage**: none new. The new agents write `.spectra/analysis.json` and `.spectra/generated.json`, which the skill
writes today.
**Testing**: xUnit 2.9.3; CLI rows on `VerdictWorkspace` / `BaselineTestWorkspace` from the real init (`CriteriaCommands`
collection); text-contract tests over `SkillContent` / `AgentContent`; install through the real `InitHandler` and
`update-skills`. Baseline at `20ac624`: Core 1219, CLI 2068 (one known failure), Execution 228, Integration 8; 4
known warnings.
**Target Platform**: Windows and Linux; Claude Code as the assistant runtime.
**Project Type**: CLI with bundled assistant skills and agents.
**Performance Goals**: main-session growth ≤ 300 tokens per generated test; no doc text, compiled prompt, test JSON or
verdict JSON in the main transcript (manual acceptance).
**Constraints**:
- the analyze → approve STOP gate, the mandatory critic, bounded retries, NON-STOP and the written==0 guard verbatim;
- the 089–092 staging, exit-8, `--ids`, `unchanged_pending` rules;
- no other CLI / config / template / format change;
- sequential subagents;
- nothing the user saw disappears;
- each guard fails on `20ac624`.

**Scale/Scope**: `IngestVerdictCommand` + `IdList` helper; 2 new agents; the repairer, the update skill (Step 5a) and
the generation skill (Steps 1–9) edited; 4 docs; 4 new test classes; retargeted generate rows (inventory in tasks.md);
`AgentContent_HasAllAgents` 4 → 6.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | No format change; tests and grounding are still written only by the ingest commands | ✅ |
| II. Deterministic execution | Engine untouched; `results` is a deterministic report of the existing classification | ✅ |
| III. Orchestrator-agnostic, minimal output | The CLI addition is usable from any orchestrator; `results` is one small object per test; agents are Claude Code packaging of the same seams (Spec 056) | ✅ |
| IV. CLI-first; validated writes; batch `--dry-run` | Agents write only files the ingest commands validate. `ingest-verdict` writes nothing, so `--dry-run` has nothing to suppress; the tracked IV gap is `ingest-grounding --all` (091 Complexity Tracking, Phase 6) | ✅ |
| V. Simplicity | Output-only CLI change, shared `--ids` parsing; agents reuse the existing recipes | ✅ |
| Test-required discipline | T1–T5 plus retargeted rows; each guard proven against `20ac624` | ✅ |

**Post-design re-check**: no violations. One deliberate behaviour change: the batch verdict ingest's human summary also
goes to stderr in JSON mode (091's rule for the grounding batch).

## Project Structure

### Documentation

```text
specs/093-generate-subagents/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
├── acceptance/setup-workspace.sh   (implementation)
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.CLI/Commands/Generate/
├── IdList.cs                                   # NEW: --ids parsing + the two usage messages (shared)
├── IngestVerdictCommand.cs                     # --ids, results, not_found, stderr summary in JSON mode
└── IngestGroundingCommand.cs                   # uses IdList (behaviour unchanged)
src/Spectra.CLI/Skills/Content/
├── Agents/spectra-analyst.agent.md             # NEW
├── Agents/spectra-generator.agent.md           # NEW
├── Agents/spectra-repairer.agent.md            # one line per results entry (FR-013)
├── Skills/spectra-generate.md                  # orchestrator (FR-006–FR-012, FR-015)
└── Skills/spectra-update.md                    # Step 5a: route by results; every written line re-verified (FR-014, FR-015)

tests/Spectra.CLI.Tests/
├── Commands/Generate/VerdictResultsTests.cs    # T1
├── Skills/AgentInstallLayoutTests.cs           # T2 (extended to six agents)
├── Skills/GenerateAgentTests.cs                # T3
├── Skills/GenerateOrchestratorTests.cs         # T4, T5
└── Skills/SkillsManifestTests.cs               # retargeted rows; agent count
```

## Implementation Order

1. **Baseline** on `20ac624`; write T1–T5 and run them on the untouched sources (proof).
2. **CLI** (FR-D): `IdList`, `ingest-verdict --ids` / `results` / `not_found` / summary.
3. **Agents**: analyst, generator; the repairer's return.
4. **Generation skill**: contract, Steps 1–9, the reference table; retarget rows.
5. **Update skill**: Step 5a routing and the re-verify set.
6. **Install**: the agent count; the pre-093 install row.
7. **Docs**: usage, cli-reference, CHANGELOG, CLAUDE.md.
8. **Finish**: full run, CLI and install checks with the built CLI, the acceptance setup script, Notes; ask before
   committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Main still reads verdict files "to check" | The contract names each forbidden read; T4/T5 assert no `Read .spectra/verdicts` and routing by `results` |
| The written==0 guard or NON-STOP text drifts | Existing verbatim rows stay on the skill; T4 re-asserts them |
| Batches change generation quality | SC-002 compares the grounded rate with the 092 build (manual acceptance) |
| A stderr summary in JSON mode breaks a caller of `ingest-verdict --all` | Skills parse stdout only; tests asserting an empty stderr are checked during implementation |
| Analyst batch counts don't add up | The rule (R2) is stated in the agent and asserted in T3 |

## Complexity Tracking

No constitution violations.
