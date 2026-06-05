# Implementation Plan: Criteria Extraction Re-homing + Extractor Unification

**Branch**: `054-criteria-extraction-rehoming` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/054-criteria-extraction-rehoming/spec.md`

## Summary

Apply the 053 prompt-compiler / handoff pattern to **criteria extraction**, and unify the legacy
`RequirementsExtractor` onto the typed-outcome failure contract. Two halves:

1. **Additive model-free extraction surface** (mirrors what 053 actually shipped — a *CLI surface*,
   commit `a204605`): a deterministic, model-free **extraction prompt-compiler** (`spectra ai
   compile-extraction-prompt`) and a fail-loud, model-free **ingest boundary** (`spectra ai
   ingest-criteria`) that classifies agent-produced content through the existing
   `CriteriaExtractor.ClassifyResponse` into a typed `CriteriaExtractionResult` and persists only
   genuine `Extracted` results through the unchanged criteria writer + index path.
2. **Extractor unification**: `RequirementsExtractor` (the `docs index` path) stops throwing on
   empty/timeout and returns a typed result sharing the **same** `ExtractionOutcome` enum and
   `IsCacheable => Outcome == Extracted` rule as `CriteriaExtractionResult`. `docs index` consumes
   the typed outcome — one failure-semantics contract, no second throwing path.

**Scope decision (user-confirmed)**: *Additive surface, matching 053.* The existing in-process
model calls in `CriteriaExtractor.ExtractFromDocumentAsync` and
`RequirementsExtractor.ExtractFromDocumentAsync` are **kept working** so `ai analyze
--extract-criteria` and `docs index` do not break. The literal FR-001 "remove the model call" is
**deferred** exactly as 053 deferred its own identically-worded FR-001 (053 shipped the compile +
ingest surface and left `GenerationAgent.SendAndWaitAsync` in place). Full removal lands when the
orchestration/extraction skills exist (Spec 055) and 053's generation-side removal is also done.
This keeps the series internally consistent and every command green at each step.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, System.Text.Json, GitHub Copilot SDK (only on the
*retained* in-process paths; the new compile/ingest surface is SDK-free), Spectre.Console
**Storage**: File-based — `docs/criteria/*.criteria.yaml`, `docs/criteria/_criteria_index.yaml`,
`config.Coverage.RequirementsFile` (requirements markdown); content-hash cache via per-source
`DocHash` in the criteria index
**Testing**: xUnit — `Spectra.CLI.Tests` (boundary/compile/ingest/unification), structured results,
no model calls
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS)
**Project Type**: Single project (CLI + Core libraries)
**Performance Goals**: Compile + classify are pure/deterministic and run offline (no token spend);
no regression to the existing per-document extraction latency on retained paths
**Constraints**: Deterministic compiler output (byte-identical for identical input — no timestamps,
GUIDs, unordered enumeration); fail-loud boundary persists nothing on non-`Extracted` outcomes;
empty-source short-circuit preserves "no model turn"
**Scale/Scope**: ~4 new source files, ~3 modified source files, ~5 new/affected test files; zero
data-model changes to the criteria index or `.criteria.yaml` schema

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ Criteria files + index stay the authoritative, git-tracked store. No new external storage. Cache key (`DocHash`) unchanged. |
| **II. Deterministic Execution** | ✅ The new compiler is deterministic by construction (FR-002); classification is the existing pure `ClassifyResponse`. No state moves into the orchestrator. |
| **III. Orchestrator-Agnostic** | ✅ The new surface is model-free and self-contained: any agent can run the compiled prompt and hand content back to `ingest-criteria`. Strengthens this principle. |
| **IV. CLI-First** | ✅ Two new named commands with explicit params and deterministic exit codes (4 refuse, 5 content-invalid, 6 parse-invalid) — CI-friendly, mirrors `compile-prompt`/`ingest-tests`. |
| **V. Simplicity (YAGNI)** | ⚠️ Adds compile/ingest types parallel to the retained in-process path (two paths coexist). Justified below — this is the *third* use of an established pattern and the coexistence is the user-confirmed non-breaking requirement, not speculative. |

**Result**: PASS. The single soft flag (two coexisting paths) is tracked in Complexity Tracking and
is an explicit, precedent-matching decision, not unjustified complexity.

## Project Structure

### Documentation (this feature)

```text
specs/054-criteria-extraction-rehoming/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (CLI command contracts)
│   ├── compile-extraction-prompt.md
│   ├── ingest-criteria.md
│   └── unified-requirements-extractor.md
├── checklists/
│   └── requirements.md  # (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Extraction/                        # NEW — model-free extraction surface (parallel to Generation/)
│   ├── ExtractionPromptCompiler.cs    # NEW — deterministic Compile/Assemble (relocated BuildExtractionPrompt)
│   ├── ExtractionPromptCompileResult.cs # NEW — Success | MissingRequired (mirrors PromptCompileResult)
│   └── CriteriaIngestor.cs            # NEW — fail-loud boundary: ClassifyResponse + persist-on-Extracted
├── Commands/Generate/                 # (existing 053 surface lives here; new commands sit beside)
│   ├── CompileExtractionPromptCommand.cs # NEW — `spectra ai compile-extraction-prompt`
│   └── IngestCriteriaCommand.cs       # NEW — `spectra ai ingest-criteria`
├── Agent/Copilot/
│   ├── CriteriaExtractor.cs           # MODIFY — BuildExtractionPrompt delegates to compiler (retain model call)
│   ├── RequirementsExtractor.cs       # MODIFY — return typed RequirementsExtractionResult; no throw on empty/timeout
│   └── RequirementsExtractionResult.cs # NEW — (ExtractionOutcome, IReadOnlyList<RequirementDefinition>), IsCacheable
├── Commands/Docs/DocsIndexHandler.cs  # MODIFY — ExtractCriteriaLoopAsync + TryExtractCriteriaAsync consume typed result
└── Commands/Generate/GenerateCommand.cs # MODIFY — register the two new subcommands

tests/Spectra.CLI.Tests/
├── Extraction/
│   ├── ExtractionPromptCompilerTests.cs # NEW — determinism + refuse-to-emit
│   └── CriteriaIngestorTests.cs       # NEW — Extracted persists; Empty/Parse fail-loud, nothing persisted
├── Commands/
│   └── IngestCriteriaCommandTests.cs  # NEW — exit-code contract
└── Agent/
    └── RequirementsExtractorUnificationTests.cs # NEW — typed outcome, no throw on empty/timeout
```

**Structure Decision**: Single-project layout. The model-free pieces live in a new
`src/Spectra.CLI/Extraction/` folder mirroring the existing `src/Spectra.CLI/Generation/` (where
053's `PromptCompiler`/`GeneratedTestIngestor` live), keeping the model-free boundary physically
separated from the SDK-coupled `Agent/Copilot/` extractors — the same "decouple by location"
principle 053 applied (investigation 03 F-2). The two new commands sit beside the existing
`CompilePromptCommand`/`IngestTestsCommand` under `Commands/Generate/` and register on the same
`ai` command group.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Two coexisting extraction paths (retained in-process model call **and** new model-free compile/ingest surface) | User-confirmed *additive* scope: removing the in-process call now would break `ai analyze --extract-criteria` and `docs index` until the driving skills land (Spec 055). Mirrors exactly what 053 shipped. | "Full removal now" rejected: breaks two working commands and contradicts the 053 precedent + this spec's own "prerequisite not yet met" note. "Unification only" rejected: delivers <half the spec (no compile/ingest surface, FR-002/003 unmet). |
| New `RequirementsExtractionResult` rather than reusing `CriteriaExtractionResult` | The two extractors carry different payloads (`RequirementDefinition` vs `AcceptanceCriterion`); Spec 047 deliberately kept the implementations separate. Unification is of the *failure-semantics contract* (shared `ExtractionOutcome` enum + `IsCacheable` rule), per FR-004/FR-007. | Merging payload types rejected: would change `docs index` persistence (RequirementsWriter consumes `RequirementDefinition`) and re-open the Spec 047 separation decision, which this spec explicitly does not do. |
