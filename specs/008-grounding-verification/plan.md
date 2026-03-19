# Implementation Plan: Grounding Verification Pipeline

**Branch**: `008-grounding-verification` | **Date**: 2026-03-19 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-grounding-verification/spec.md`

## Summary

Add a dual-model verification pipeline to SPECTRA's test generation that uses a configured "critic" model to verify each generated test against source documentation. Tests receive one of three verdicts (grounded, partial, hallucinated) with grounding metadata persisted in test frontmatter.

## Technical Context

**Language/Version**: C# 12, .NET 8+
**Primary Dependencies**: Spectra.CLI (commands, agents), Spectra.Core (models, parsing), System.Text.Json, Azure.AI.Inference/OpenAI SDKs
**Storage**: File system (test Markdown files with YAML frontmatter)
**Testing**: xUnit with structured results
**Target Platform**: Cross-platform CLI (.NET runtime)
**Project Type**: CLI tool extension
**Performance Goals**: Verification adds <2 seconds per test on average (SC-001)
**Constraints**: Batch verification for 20 tests completes in <30 seconds
**Scale/Scope**: Typical generation of 5-20 tests per invocation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| Simplicity | PASS | Single verification step integrated into existing generate flow |
| Minimal Abstraction | PASS | Reuses existing IAgentRuntime pattern, adds CriticConfig to AiConfig |
| Flat Structure | PASS | New files in existing directories (Agent/, Models/Config/) |
| No Over-Engineering | PASS | Three verdicts, no complex ML pipelines, uses existing AI providers |
| No Premature Optimization | PASS | Sequential verification first, batch parallelization as enhancement |

## Project Structure

### Documentation (this feature)

```text
specs/008-grounding-verification/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── contracts/           # Phase 1 output
```

### Source Code (repository root)

```text
src/
├── Spectra.Core/
│   └── Models/
│       ├── Config/
│       │   ├── AiConfig.cs            # MODIFY: Add Critic property
│       │   └── CriticConfig.cs        # NEW: Critic configuration model
│       └── Grounding/
│           ├── VerificationVerdict.cs  # NEW: grounded/partial/hallucinated enum
│           ├── GroundingMetadata.cs    # NEW: Metadata for test frontmatter
│           ├── CriticFinding.cs        # NEW: Individual claim assessment
│           └── VerificationResult.cs   # NEW: Full verification response
├── Spectra.CLI/
│   ├── Agent/
│   │   ├── ICriticRuntime.cs          # NEW: Interface for critic model
│   │   ├── CriticFactory.cs           # NEW: Create critic from config
│   │   └── Critic/
│   │       ├── GoogleCritic.cs        # NEW: Gemini Flash implementation
│   │       ├── OpenAiCritic.cs        # NEW: GPT-4o-mini implementation
│   │       └── CriticPromptBuilder.cs # NEW: Build verification prompts
│   ├── Commands/Generate/
│   │   └── GenerateHandler.cs         # MODIFY: Add verification step
│   ├── IO/
│   │   └── TestFileWriter.cs          # MODIFY: Write grounding metadata
│   └── Output/
│       └── VerificationPresenter.cs   # NEW: Display ✓ ⚠ ✗ verdicts
└── Spectra.MCP/
    └── (no changes)

tests/
├── Spectra.Core.Tests/
│   └── Models/Grounding/
│       └── GroundingMetadataTests.cs  # NEW
└── Spectra.CLI.Tests/
    ├── Agent/Critic/
    │   ├── CriticFactoryTests.cs      # NEW
    │   └── CriticPromptBuilderTests.cs # NEW
    └── Commands/Generate/
        └── VerificationIntegrationTests.cs # NEW
```

**Structure Decision**: Single project structure extending existing Spectra.CLI and Spectra.Core. New Grounding models in Core, new Critic agents in CLI Agent directory.

## Complexity Tracking

No constitution violations. All complexity is necessary for the core feature.

---

## Phase 0: Research

### Research Questions

1. **Critic Model Integration**: How to integrate a second AI model call without duplicating the existing agent infrastructure?
   - Option A: Extend IAgentRuntime with VerifyTestAsync method
   - Option B: Create separate ICriticRuntime interface (simpler, cleaner separation)
   - **Decision**: Option B - separate interface for clarity

2. **Batch vs Sequential Verification**: Should verification run per-test or in batches?
   - Single test per call: Simpler, clearer findings per test
   - Batch (5-10 tests): Fewer API calls, but harder to attribute findings
   - **Decision**: Single test per call initially, add batching later if needed

3. **Provider Support**: Which providers support critic functionality?
   - Gemini Flash: Primary recommendation (cheap, fast, good at NLI)
   - GPT-4o-mini: Alternative (OpenAI ecosystem)
   - Claude Haiku: Alternative (Anthropic ecosystem)
   - **Decision**: Support all three via provider-agnostic interface

4. **Grounding Metadata Schema**: What fields need to be in frontmatter?
   - Required: verdict, score, generator, critic, verified_at
   - For partial: unverified_claims array
   - **Decision**: Per spec FR-014 through FR-017

5. **CLI Flag Handling**: How does --skip-critic interact with --no-interaction?
   - Both flags independent
   - --skip-critic simply bypasses verification step
   - **Decision**: Independent flags, simple boolean check

### Decisions Log

| Decision | Chosen Option | Rationale |
|----------|---------------|-----------|
| Critic Interface | Separate ICriticRuntime | Clean separation from generation, different model selection |
| Verification Mode | Per-test | Clearer attribution of findings, simpler error handling |
| Default Behavior | No verification | Backward compatible (FR-013), opt-in via config |
| Metadata Location | YAML frontmatter | Consistent with existing test format |

---

## Phase 1: Design

### Data Model

See [data-model.md](./data-model.md) for complete entity definitions.

### Contracts

See [contracts/](./contracts/) for:
- `critic-prompt.schema.json`: Critic prompt structure
- `critic-response.schema.json`: Expected response format
- `grounding-metadata.schema.json`: Frontmatter schema

### Integration Points

1. **GenerateHandler.cs**: After `agent.GenerateTestsAsync()`, before writing tests
2. **AiConfig.cs**: Add `Critic` property of type `CriticConfig`
3. **TestFileWriter.cs**: Add `GroundingMetadata` to frontmatter serialization
4. **spectra.config.json**: New `ai.critic` section

### Quickstart

See [quickstart.md](./quickstart.md) for:
- Configuration examples
- CLI usage patterns
- Expected output samples

---

## Implementation Phases

### Phase 2: Core Models (T001-T010)

- CriticConfig model
- VerificationVerdict enum
- GroundingMetadata record
- CriticFinding record
- VerificationResult class
- Update AiConfig with Critic property
- Unit tests for all models

### Phase 3: Critic Infrastructure (T011-T020)

- ICriticRuntime interface
- CriticFactory class
- CriticPromptBuilder
- Base critic implementation structure
- Unit tests for factory and prompt builder

### Phase 4: Provider Implementations (T021-T035)

- GoogleCritic (Gemini Flash)
- OpenAiCritic (GPT-4o-mini)
- AnthropicCritic (Claude Haiku)
- Response parsing for each provider
- Integration tests with mock responses

### Phase 5: CLI Integration (T036-T050)

- Add --skip-critic flag to GenerateCommand
- Modify GenerateHandler verification step
- Update TestFileWriter for grounding metadata
- VerificationPresenter for CLI output
- Integration tests for full flow

### Phase 6: Output & Polish (T051-T060)

- ✓ ⚠ ✗ symbol display
- Summary counts
- Detailed rejection reasons
- Partial verdict explanations
- End-to-end testing

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Critic API unavailable | Medium | Low | Graceful fallback to no-verification (FR-019) |
| Malformed critic response | Medium | Low | Parse with fallback to "unverified" status |
| Performance regression | Low | Medium | Async verification, timeout handling |
| Cost overruns | Low | Low | Clear documentation of expected costs |

---

## Success Metrics

From spec SC-001 through SC-005:
- [ ] Verification <2 seconds per test average
- [ ] 95% hallucination detection accuracy
- [ ] Users understand flagged tests within 10 seconds
- [ ] <5% unverified claims in output
- [ ] Zero breaking changes to existing workflows
