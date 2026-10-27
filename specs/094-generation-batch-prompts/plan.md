# Implementation Plan: Section-Scoped Generation Batches (Phase 5c)

**Branch**: `094-generation-batch-prompts` | **Date**: 2026-10-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/094-generation-batch-prompts/spec.md`

## Summary

The analysis ingest resolves each behaviour's source to a doc section and saves a per-suite generation plan: behaviour
ids, section fingerprints, and batches packed by section in document order with counts that never drop a batch. The new
`compile-prompt --batch` mode inlines exactly one batch's behaviours, sections and criteria, so the generator subagent
stops reading documents. Batches whose sections changed since the analysis are refused. The analyst copies the CLI's
batches, the generator takes a batch id, and every generation prompt drops the dead tool workflow. Grounding quality is
checked against the 093 count mode.

## Technical Context

**Language/Version**: C# 12, .NET 8; Markdown templates, skill and agent content.

**Primary Dependencies**: reused, not changed in behaviour:
- `BaselineContext` (section resolution and fingerprints, 084/085);
- `SectionExtractor` and `SourceFingerprint`;
- `CriteriaStore.ScanFilesAsync`, `CriteriaPaths`, `CriteriaProvenance`;
- `TokenEstimator` and `PreFlightTokenChecker.DefaultBudgetTokens` (088);
- the `UpdatePromptRenderer` fence (088);
- `PromptTemplateLoader` / `PlaceholderResolver`;
- `ConfigLoader` (083);
- `SpectraGitIgnore` (089/090);
- `BaselineCommandIO.Fail`.

No new packages.

**Storage**: `.spectra/generation-plan/<suite>.json` (new, scratch, gitignored). No other format change.

**Testing**: xUnit 2.9.3. CLI rows on `BaselineTestWorkspace` from the real init (the `CriteriaCommands` collection).
Template and compiler rows are pure. Text-contract rows run over `SkillContent` / `AgentContent`; gitignore rows go
through the real `InitHandler` / `update-skills`. Baseline at `55b541f`: Core 1219, CLI 2092 (one known failure),
Execution 228, Integration 8; 4 known warnings.

**Target Platform**: Windows and Linux; Claude Code as the assistant runtime.

**Project Type**: CLI with bundled assistant skills and agents.

**Performance Goals**:
- 0 document reads by generators of numbered batches (manual acceptance);
- the batch prompt is bounded by its sections (`PROMPT_TOO_LARGE` otherwise);
- the plan build is linear in behaviours, with one read per referenced document.

**Constraints**:
- no change to the critic, update, criteria or repair flows;
- count mode keeps its options, refusal shape and config loading;
- from-description is unchanged apart from the removed tool text;
- NON-STOP and the written==0 guard text stay verbatim;
- 083 config rule; byte-identical plans and prompts;
- each new rule fails on `55b541f`;
- verification on a real-init workspace and a pre-094 one.

**Scale/Scope**:
- 1 Core helper (`SectionRef`) and 5 CLI types under `Generation/Plan/`;
- 2 commands changed (`ingest-analysis`, `compile-prompt`);
- 1 config key; 1 gitignore entry;
- 2 templates (`behavior-analysis.md`, `test-generation.md`) and the built-in fallback;
- 2 agents and 1 skill;
- 4 docs;
- ~8 new test classes;
- 3 retargeted 093 rows.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Assessment | Status |
|---|---|---|
| I. Git as source of truth | Tests are still written only by `ingest-tests`. The plan is scratch (gitignored) and derived from the docs; no committed format changes | ✅ |
| II. Deterministic execution | Model-free CLI. The plan and prompts are byte-identical for identical inputs; staleness is detected by fingerprint, never guessed | ✅ |
| III. Orchestrator-agnostic, minimal output | `--batch` and the plan are plain CLI/JSON usable by any orchestrator. The agents are Claude Code packaging of the same seams (Spec 056) | ✅ |
| IV. CLI-first; validated writes; `--dry-run` | `compile-prompt` writes nothing. `ingest-analysis` now writes the scratch plan and has no `--dry-run` — tracked in Complexity Tracking | ✅ (resolved by 097: `--dry-run` added) |
| V. Simplicity | Reuses the 084 resolver, 088 fence/token/template rules and the 083 loader. Two placeholders, no engine change | ✅ |
| Test-required discipline | T1–T9 (tasks.md), each proven against `55b541f`; 3 deliberate 093 row changes | ✅ |

**Post-design re-check**: one tracked gap (IV, Complexity Tracking). Deliberate behaviour changes:
- `ingest-analysis` loads config through the checked loader. A missing config is still exit 1, now with `CONFIG_MISSING`
  and the shared message.
- `ingest-analysis` writes the plan, and its human summary goes to stderr in JSON mode.
- The shipped generation template and fallback lose the dead tool text.

## Project Structure

### Documentation

```text
specs/094-generation-batch-prompts/
├── spec.md  plan.md  research.md  data-model.md  quickstart.md
├── contracts/contracts.md
├── checklists/requirements.md
├── acceptance/setup-workspace.sh   (implementation)
└── tasks.md   (/speckit.tasks)
```

### Source Code

```text
src/Spectra.Core/
├── Baseline/SectionRef.cs                       # NEW: canonical section ref (R1)
├── Models/Config/GenerationConfig.cs            # batch_size (int?), EffectiveBatchSize
└── Config/ConfigLoader.cs                       # "generation" in CheckedSections; 1–50 validation
src/Spectra.CLI/
├── Generation/Plan/GenerationPlan.cs            # NEW: model + strict serializer
├── Generation/Plan/GenerationPlanBuilder.cs     # NEW: ids, grouping, packing, counts (R3, R4)
├── Generation/Plan/GenerationPlanStore.cs       # NEW: path, atomic save, strict load
├── Generation/Plan/BatchCriteriaSelector.cs     # NEW: R6
├── Generation/Plan/BatchPromptCompiler.cs       # NEW: sections, behaviours, template (R7, R8)
├── Generation/PromptCompiler.cs                 # fallback text; read_docs/source_sections values
├── Generation/CriteriaContextLoader.cs          # Format(...) extracted (same output)
├── Generation/UpdatePromptRenderer.cs           # fence helper shared (internal)
├── Commands/Generate/IngestAnalysisCommand.cs   # checked config, plan, batches/unresolved, summary
├── Commands/Generate/CompilePromptCommand.cs    # --batch mode, refusals
├── Baseline/BaselineCommandIO.cs                # refusal codes
├── Commands/Init/SpectraGitIgnore.cs            # .spectra/generation-plan/; Pre090Offered explicit
├── Prompts/Content/behavior-analysis.md         # source = most specific doc#anchor
├── Prompts/Content/test-generation.md           # no dead tools; read_docs / source_sections blocks
└── Skills/Content/
    ├── Agents/spectra-analyst.agent.md          # copies the CLI's batch lines
    ├── Agents/spectra-generator.agent.md        # {suite, batch, count?}; no doc reads for --batch
    └── Skills/spectra-generate.md               # batch ids; PLAN_STALE / PROMPT_TOO_LARGE offer

tests/Spectra.Core.Tests/Baseline/SectionRefTests.cs                 # T1
tests/Spectra.CLI.Tests/Generation/Plan/GenerationPlanBuilderTests.cs # T2
tests/Spectra.CLI.Tests/Commands/Generate/GenerationPlanIngestTests.cs # T3
tests/Spectra.CLI.Tests/Commands/Generate/CompileBatchPromptTests.cs  # T4 (content, determinism)
tests/Spectra.CLI.Tests/Commands/Generate/CompileBatchRefusalTests.cs # T5 (incl. PLAN_STALE)
tests/Spectra.CLI.Tests/Generation/GenerationTemplateToolTextTests.cs # T6 (count mode, fallback, from-description)
tests/Spectra.CLI.Tests/Config/GenerationBatchSizeConfigTests.cs      # T7 (range, init template, pre-094)
tests/Spectra.CLI.Tests/Commands/Init/GenerationPlanGitIgnoreTests.cs # T8
tests/Spectra.CLI.Tests/Skills/BatchGenerationAgentTests.cs           # T9 (+ retargeted 093 rows)
```

## Implementation Order

1. **Baseline** on `55b541f`; write T1–T9 and run them on the untouched sources (proof). The user measures the SC-003
   baseline on the 093 build.
2. **Core:** `SectionRef`; `generation.batch_size` and validation.
3. **Plan:** builder, serializer and store; `ingest-analysis` (checked config, plan, result, summary).
4. **Prompt:** the template and fallback text; criteria `Format` extraction; the shared fence; `BatchCriteriaSelector`,
   `BatchPromptCompiler`; `compile-prompt --batch` and its refusals.
5. **Analysis template:** the `source` wording.
6. **Gitignore** entry and `Pre090Offered`.
7. **Agents and skill**; retarget the 093 rows.
8. **Docs:** cli-reference, usage, CHANGELOG, CLAUDE.md.
9. **Finish:**
   - the full run;
   - built-CLI checks on a real-init and a pre-094 workspace (quickstart);
   - the acceptance script;
   - Notes;
   - ask before committing and merging; never push.

## Risks

| Risk | Mitigation |
|---|---|
| Grounding drops because a section lacks context the whole doc had | SC-003 compares against the 093 count mode (2 runs each). Whole-document refs remain possible. The unsourced batch keeps doc reading |
| The model gives sources the resolver can't match | Canonical grouping by heading index; `unresolved` with reasons; the unsourced batch still generates them |
| A heading slug that does not round-trip | `SectionRef` falls back to the source as given (T1 covers it) |
| A pre-090 workspace never gets the new gitignore offer | `Pre090Offered` becomes an explicit 089 list (T8) |
| An existing ingest-analysis test breaks on the checked config loader | Its config has `source`/`tests`/`ai`; checked during implementation, and any change is noted |
| The template's `{{#if}}` blocks can't nest | The two new blocks are top-level; `prompts validate` passes on the shipped template (T6) |
| Every pre-094 workspace has a template copy without the batch placeholders | No refusal: an unedited copy uses the bundled template, a customised one falls back to count mode per batch, both with a warning (R16, T5 rows) |
| Init and update-skills record template hashes under different keys | The lookup accepts both key forms (R16); the pre-existing overwrite defect is flagged separately |

## Complexity Tracking

None. The constitution IV row tracked here was resolved by Spec 097 (`--dry-run` on the batch ingests).
