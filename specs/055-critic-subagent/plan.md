# Implementation Plan: Critic as a `context: fork` Subagent (+ Gating Semantics)

**Branch**: `055-critic-subagent` | **Date**: 2026-06-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/055-critic-subagent/spec.md`

## Summary

Re-home the critic onto a `context: fork` subagent and make its damage paths fail loud while its
verdict stays advisory. Three halves, all delivered **additively** (matching what 053 and 054
shipped — a *CLI surface* + skill, with the live in-process call retained):

1. **Additive model-free verification surface** (mirrors `Generation/` from 053 and `Extraction/`
   from 054): a deterministic, model-free **critic prompt-compiler** (`spectra ai
   compile-critic-prompt`) that wraps the reused-verbatim `CriticPromptBuilder` behind a validated
   refuse-to-emit boundary, and a **fail-loud verdict-ingest boundary** (`spectra ai
   ingest-verdict`) that classifies an agent-produced critic JSON into a typed outcome
   (`Verdict | EmptyResponse | ParseFailure`) — replacing the silent `Partial` / `0.5` defaults
   with specific errors (FR-006), while never throwing.
2. **Net-new `context: fork` critic subagent skill** (`spectra-critic.agent.md`): a fresh-context
   subagent whose input is restricted to the test artifact + selected source documents (FR-002),
   designed for explicit (mandatory-step) invocation, never auto-invocation (FR-003). The skill is
   delivered and registered here; the *wiring* that makes the generation skill invoke it lands in
   the subsequent spec.
3. **Single model selector + dead/duplicate-code removal** (FR-004/FR-008): collapse the
   provider→default-model switch — duplicated in `CopilotCritic.GetEffectiveModel`
   (`GroundingAgent.cs:192`) and `CopilotService.GetCriticModel` (`CopilotService.cs:319`) — to one
   `CriticModelResolver`, making `ai.critic.model` the single source of truth; delete the
   unreferenced `CopilotCriticFactory` (`GroundingAgent.cs:226`, investigation F-1); and update the
   stale cross-architecture comments (`GroundingAgent.cs:197`, `CopilotService.cs:324`) to the
   §32 same-family direction.

**Scope decision (precedent-matched, additive)**: The existing in-process critic model call
(`CopilotCritic.VerifyTestAsync` → `session.SendAsync`, `GroundingAgent.cs:124`) is **kept
working** so `spectra ai generate`'s batch verification does not break. The literal FR-001 "remove
the model call from C#" is **deferred** exactly as 053 deferred its identically-worded FR-001 (053
shipped compile + ingest and left `GenerationAgent.SendAndWaitAsync` in place; 054 did the same for
its two extractors). The swap of the live invocation to the subagent lands in the subsequent wiring
spec. This keeps the series internally consistent and every command green at each step. The
verdict-gating contract (`GenerateHandler.cs:847`, `Verdict != Hallucinated`) and the
`Manual`-preservation logic (`GenerateHandler.cs:2134`) are **reused verbatim** and unchanged.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: System.CommandLine, System.Text.Json, GitHub Copilot SDK (only on the
*retained* in-process critic path; the new compile/ingest surface is SDK-free), Spectre.Console
**Storage**: File-based — grounding metadata written onto test frontmatter via the unchanged
`CreateTestWithGrounding`; no new persisted store. Critic model from `ai.critic.model` in
`spectra.config.json`
**Testing**: xUnit — `Spectra.CLI.Tests` (compiler/ingest/resolver/command/skill-isolation),
structured results, no model calls. `Spectra.Core.Tests` grounding-model tests are the untouched
regression net
**Target Platform**: Cross-platform CLI (Windows/Linux/macOS)
**Project Type**: Single project (CLI + Core libraries)
**Performance Goals**: Compile + classify are pure/deterministic and run offline (no token spend);
no regression to existing per-test critic latency on the retained in-process path
**Constraints**: Deterministic compiler output (byte-identical for identical input — no timestamps,
GUIDs, unordered enumeration); fail-loud boundary never coerces a missing `verdict`/`score` into a
soft pass and never throws; verdict gating and `Manual`-preservation byte-for-byte unchanged
**Scale/Scope**: ~6 new source files (4 model-free surface + 2 commands), 1 new skill file, ~3
modified source files (GroundingAgent, CopilotService, AiCommand) + manifest/agent-content
registration; ~5 new test files; zero data-model changes to the verdict enum or grounding schema

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|-----------|------------|
| **I. GitHub as Source of Truth** | ✅ Grounding metadata stays on git-tracked test frontmatter via the unchanged write-back. No new external storage. Critic model is config in the git-tracked `spectra.config.json`. |
| **II. Deterministic Execution** | ✅ The new critic-prompt compiler is deterministic by construction (wraps the existing builder, adds no timestamps/GUIDs). The verdict-ingest boundary is a pure classify that never throws. No state moves into the orchestrator. |
| **III. Orchestrator-Agnostic** | ✅ The new surface is model-free and self-contained: any agent can run the compiled critic prompt and hand the verdict JSON back to `ingest-verdict`. The `context: fork` subagent strengthens isolation (artifact + docs only). |
| **IV. CLI-First** | ✅ Two new named commands with explicit params and deterministic exit codes (4 refuse, 5 empty, 6 parse-invalid) — CI-friendly, mirrors the `compile-prompt`/`ingest-tests` and `compile-extraction-prompt`/`ingest-criteria` pairs. |
| **V. Simplicity (YAGNI)** | ⚠️ Adds a model-free verification surface parallel to the retained in-process critic (two paths coexist) — the *third* application of an established pattern (053, 054). Coexistence is the user/precedent-confirmed non-breaking requirement, not speculative. Tracked below. |

**Result**: PASS. The single soft flag (two coexisting paths) is an explicit, precedent-matching
decision tracked in Complexity Tracking, not unjustified complexity. The FR-004/FR-008 cleanup
*reduces* duplication (two switches → one resolver; deletes a dead factory), net-improving
Principle V.

## Project Structure

### Documentation (this feature)

```text
specs/055-critic-subagent/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── compile-critic-prompt.md      # CLI command contract
│   ├── ingest-verdict.md             # CLI command contract
│   ├── critic-subagent-skill.md      # subagent skill contract (context: fork, isolation)
│   └── critic-model-selector.md      # single-selector + dead-code-removal contract
├── checklists/
│   └── requirements.md  # (from /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Spectra.CLI/
├── Verification/                       # NEW — model-free verification surface (parallel to Generation/, Extraction/)
│   ├── CriticPromptCompiler.cs         # NEW — deterministic Compile (validated) / Assemble (delegates to reused CriticPromptBuilder)
│   ├── CriticPromptCompileResult.cs    # NEW — Success | MissingRequired (mirrors PromptCompileResult / ExtractionPromptCompileResult)
│   ├── VerdictIngestor.cs              # NEW — fail-loud Classify: missing/unparseable verdict|score → ParseFailure (no soft default), never throws
│   └── VerdictIngestResult.cs          # NEW — VerdictIngestOutcome { Verdict | EmptyResponse | ParseFailure } + parsed VerificationResult + gate decision + Errors
├── Commands/Generate/
│   ├── CompileCriticPromptCommand.cs   # NEW — `spectra ai compile-critic-prompt` (--test, --docs); exit 4 on refuse
│   └── IngestVerdictCommand.cs         # NEW — `spectra ai ingest-verdict` (--from/stdin); exit 0 verdict / 5 empty / 6 parse-invalid
├── Agent/Critic/
│   └── CriticModelResolver.cs          # NEW — single source of truth: config.Model ?? same-family default (replaces the two duplicated switches)
├── Agent/Copilot/
│   ├── GroundingAgent.cs               # MODIFY — GetEffectiveModel delegates to CriticModelResolver; DELETE dead CopilotCriticFactory; same-family comment (retain in-process model call)
│   └── CopilotService.cs               # MODIFY — GetCriticModel delegates to CriticModelResolver; same-family comment
├── Commands/Ai/AiCommand.cs            # MODIFY — register the two new subcommands
└── Skills/
    ├── Content/Agents/spectra-critic.agent.md  # NEW — context: fork subagent skill (artifact + docs only; explicit invocation)
    ├── SkillsManifest.cs               # MODIFY — register the new agent
    └── AgentContent.cs                 # MODIFY — surface the new agent content

tests/Spectra.CLI.Tests/
├── Verification/
│   ├── CriticPromptCompilerTests.cs    # NEW — determinism + refuse-to-emit on missing artifact (FR-002)
│   └── VerdictIngestorTests.cs         # NEW — fail-loud on missing/unparseable verdict|score; never throws; gate decision (FR-005, FR-006)
├── Commands/
│   └── CriticVerificationCommandsTests.cs # NEW — exit-code contract: compile refuse → 4; ingest 0/5/6
├── Agent/
│   └── CriticModelResolverTests.cs     # NEW — config.Model is single selector; no provider switch reachable; dead factory gone (FR-004, FR-008)
└── Skills/
    └── CriticSubagentSkillTests.cs     # NEW — skill is context: fork; input restricted to artifact + docs, no generator state (FR-002)
```

**Structure Decision**: Single-project layout. The model-free pieces live in a new
`src/Spectra.CLI/Verification/` folder mirroring `src/Spectra.CLI/Generation/` (053) and
`src/Spectra.CLI/Extraction/` (054), keeping the model-free boundary physically separated from the
SDK-coupled `Agent/Copilot/` critic runtime — the same "decouple by location" principle 053/054
applied (investigation 03 F-2, 02 §7). The two new commands sit beside the existing
`CompilePromptCommand`/`IngestTestsCommand` and `CompileExtractionPromptCommand`/`IngestCriteriaCommand`
under `Commands/Generate/` and register on the same `ai` command group. The new critic subagent
skill joins the existing `spectra-generation.agent.md` / `spectra-execution.agent.md` agents.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Two coexisting critic paths (retained in-process model call **and** new model-free compile/ingest surface + subagent skill) | Precedent-matched *additive* scope: removing the in-process call now would break `spectra ai generate`'s batch verification until the generation skill wires the subagent (subsequent spec). Mirrors exactly what 053/054 shipped. | "Full removal now" rejected: breaks the working generate flow and contradicts the 053/054 precedent + this spec's "subsequent wiring spec is a prerequisite" note. "Gating cleanup only" rejected: delivers <half the spec (no subagent skill, FR-002/FR-003 unmet). |
| New `VerdictIngestResult` typed outcome rather than reusing `VerificationResult` directly at the boundary | `VerificationResult` cannot distinguish *damage* (parse-miss → loud, FR-006) from *failure* (critic call threw → Unverified-style, FR-007) — today both collapse to `Partial` + `Errors`. A typed ingest outcome makes the two distinct (FR-007) without changing the reused-verbatim `VerificationResult`/`VerificationVerdict`. | Reusing `CreateErrorResult` (Partial/0) rejected: that is exactly the conflation FR-006/FR-007 exist to remove. Adding a new verdict enum value rejected: would mutate `VerificationVerdict`, which is in the do-not-touch regression net. |
| New `CriticModelResolver` (one more type) | Collapses two divergent provider→default switches into one source of truth (FR-004/FR-008) — a *net reduction* in duplication, not new complexity. | Leaving the switch in `GetEffectiveModel` and having `CopilotService` call it rejected: cross-namespace coupling and the comment/direction fix would still live in two places; a tiny dedicated resolver is the cleaner single source. |
