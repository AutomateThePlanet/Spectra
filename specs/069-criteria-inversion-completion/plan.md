# Implementation Plan: Criteria-extraction inversion — completion + Copilot SDK removal

**Branch**: `069-criteria-inversion-completion` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/069-criteria-inversion-completion/spec.md`
**Grounded by**: `INVESTIGATION-criteria-inversion.md` (2026-06-09)

## Summary

Complete the criteria-extraction inversion and delete the GitHub Copilot SDK. The model-free seam
(`spectra ai compile-extraction-prompt` / `ingest-criteria`, Spec 054) already exists but is unwired.
This feature: (1) **rescues** the model-free helpers that currently live inside the Copilot-coupled
`CriteriaExtractor` so the SDK folder can be deleted; (2) **rewires** the `spectra-criteria` skill to
drive a per-document compile→in-session-turn→ingest loop (mirroring the 053/059 generation seam) plus a
new model-free **changed-docs** command for incremental skip; (3) **converges** `docs index` onto the
canonical `AcceptanceCriterion`/`.criteria.yaml` artifact and retires `RequirementsExtractor` /
`_requirements.yaml`; (4) **drops** import compound-splitting; then (5) **deletes** `Agent/Copilot/` and
**strips** `ai.providers` + `ai.critic` from the config schema, generation, template, validation,
display, and the three interactive init steps. Serialization, ID scheme, and outcome gate remain
byte-compatible; `Spectra.Core` and `TestPersistenceService` tests are untouched.

## Technical Context

**Language/Version**: C# 12 / .NET 8
**Primary Dependencies**: System.CommandLine, Spectre.Console, System.Text.Json, YamlDotNet — **GitHub
Copilot SDK is being removed** (no replacement; inference moves to the user's Claude Code session)
**Storage**: file-based — `docs/criteria/*.criteria.yaml` + `_criteria_index.yaml`, `spectra.config.json`
**Testing**: xUnit (`Spectra.CLI.Tests`, `Spectra.Core.Tests` — the latter MUST stay unmodified)
**Target Platform**: cross-platform .NET global tool (`spectra`) + bundled Claude Code skill
**Project Type**: CLI + Claude Code skill/agent authoring artifacts
**Performance Goals**: `docs index` makes zero model calls (was inline-blocking per-doc 2-min deadlines);
extraction cost is per changed doc only (unchanged docs skipped with no model turn)
**Constraints**: `.criteria.yaml`/`_criteria_index.yaml` output byte-identical to pre-inversion;
`Spectra.Core` + `TestPersistenceService` tests unmodified; phase order is causal (gates between)
**Scale/Scope**: ~12 production files touched + 1 skill + 1 new command; net **deletion** of `Agent/Copilot/`

**Unknowns**: none. The spec + investigation resolved all open questions; the lone implementer's-call
(`--skip-splitting` flag disposition) is decided in `research.md` (remove, per YAGNI).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | Notes |
|---|---|---|
| I. GitHub as source of truth | ✅ Pass | Criteria stay file-based in Git; no new storage. |
| II. Deterministic execution | ✅ Pass | Seam is model-free + deterministic; the changed-docs command is a pure SHA-256 compare; inference is an explicit in-session turn, not engine state. |
| III. Orchestrator-agnostic / BYOK provider chain | ⚠️ Aligned, not violated | This **completes** the v2 inference inversion. Specs 058/059 already removed the in-process generation/critic provider chains; this removes the last one (criteria). BYOK now = the user's own Claude Code session, which is inherently orchestrator-provided. No in-process provider chain remains to "support." |
| IV. CLI-first | ✅ Pass | Every step is a named CLI command (`compile-extraction-prompt`, `ingest-criteria`, new changed-docs); the skill orchestrates them; AI writes nothing directly — persistence is through the fail-loud `ingest-criteria` handler. |
| V. Simplicity (YAGNI) | ✅ Pass (net reduction) | Deletes an entire SDK integration + a redundant extractor + a redundant artifact; `--skip-splitting` flag removed rather than kept as a shim. |

**No Complexity Tracking entries** — this feature reduces complexity. The Principle III note is recorded
as the planned conclusion of an already-ratified migration direction (052–068), not a new deviation.

## Project Structure

### Documentation (this feature)

```text
specs/069-criteria-inversion-completion/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions (rescue home, changed-docs command, splitting, config)
├── data-model.md        # Phase 1 — entities + config schema delta
├── contracts/           # Phase 1 — CLI command + skill-choreography contracts
│   ├── changed-docs-command.md
│   ├── docs-index-converged.md
│   └── skill-extraction-loop.md
├── quickstart.md        # Phase 1 — end-to-end verification recipe
└── tasks.md             # Phase 2 — /speckit.tasks output
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Extraction/                 # model-free home (existing): ExtractionPromptCompiler, CriteriaIngestor
│   ├── CriteriaResponseClassifier.cs   # NEW — rescued ClassifyResponse + normalizers (FR-001)
│   └── ExtractionOutcome.cs / *Result.cs  # NEW location — rescued enum + result records (FR-001)
├── Agent/Copilot/              # DELETED at FR-009 (CopilotService, ProviderMapping, CriteriaExtractor, RequirementsExtractor)
├── Commands/
│   ├── Ai/AiCommand.cs          # unchanged registration of the seam; analyze import path simplified (FR-008)
│   ├── Analyze/AnalyzeHandler.cs# drop --extract-criteria in-process path + import splitting (FR-007/008)
│   ├── Docs/DocsIndexHandler.cs # index-only; remove inline extraction block (FR-006)
│   ├── Docs/…                    # NEW changed-docs command (FR-005)
│   └── Init/InitHandler.cs       # remove 3 interactive AI steps (FR-011)
├── Config/ConfigLoader.cs        # GenerateConfig + validation drop providers/critic (FR-010)
├── Commands/Config/ConfigHandler.cs # display drops providers/critic (FR-010)
├── Templates/spectra.config.json # embedded template drops ai.providers/ai.critic (FR-010)
└── Skills/Content/Skills/spectra-criteria.md  # rewired to the seam loop (FR-003)

src/Spectra.Core/                 # UNTOUCHED (CriteriaMerger, CriteriaFileWriter/Reader, AcceptanceCriterion, RequirementDefinition removed only if Core-resident — see research)
tests/Spectra.CLI.Tests/          # updated/added tests (rescue, changed-docs, converged docs index, init)
tests/Spectra.Core.Tests/         # MUST NOT be modified
```

**Structure Decision**: Single-project CLI. The rescue target is the **existing** model-free
`Spectra.CLI.Extraction` namespace (already home to `ExtractionPromptCompiler` + `CriteriaIngestor`),
not a new `Spectra.CLI.Criteria` — minimizing new surface (YAGNI). See `research.md` R1.

## Complexity Tracking

> No violations. This feature is a net deletion; no entries required.
