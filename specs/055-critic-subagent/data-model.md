# Phase 1 Data Model: Critic as a `context: fork` Subagent

This feature is contract-and-boundary work, not persistence work. **No on-disk schema changes**:
the verdict enum, grounding metadata, and test frontmatter are unchanged. The "entities" below are
the in-process types the new model-free surface introduces, plus the reused-verbatim types they
build on.

## New types (model-free verification surface)

### `CriticPromptCompileResult` (new)
Mirror of `PromptCompileResult` (053) / `ExtractionPromptCompileResult` (054).

| Field | Type | Notes |
|-------|------|-------|
| `IsSuccess` | `bool` | True when a prompt was emitted. |
| `Prompt` | `string?` | The compiled critic prompt (`{system}\n\n---\n\n{user}`); null on refusal. |
| `MissingInput` | `string?` | Name of the absent required input on refusal (e.g. `test_artifact`); null on success. |
| `Message` | `string?` | Human/skill-readable reason on refusal. |

- Factories: `Success(prompt)`, `MissingRequired(input, message)`.
- **Validation rules**: refuses (`MissingRequired("test_artifact", …)`) when the test is null or has
  no id/title. Source documents may be empty (the builder emits "*No relevant documentation
  provided.*") — an empty doc set is **not** a refusal.
- **Determinism**: identical `(test, docs)` → byte-identical `Prompt` (no timestamps/GUIDs;
  document order = input order).

### `VerdictIngestOutcome` (new enum)

| Value | Meaning | Cacheable/Gate |
|-------|---------|----------------|
| `Verdict` | A well-formed critic response was classified into a `VerificationResult`. | Gate decision derived from the verdict. |
| `EmptyResponse` | The response was empty/whitespace. | Damage → fail loud (exit 5). Never a verdict. |
| `ParseFailure` | Missing/unparseable `verdict` or `score`, or non-JSON. | Damage → fail loud (exit 6). Never a verdict. |

### `VerdictIngestResult` (new)

| Field | Type | Notes |
|-------|------|-------|
| `Outcome` | `VerdictIngestOutcome` | The typed classification. |
| `IsSuccess` | `bool` | `=> Outcome == Verdict`. |
| `Result` | `VerificationResult?` | The parsed verdict/score/findings on `Verdict`; null otherwise. Uses the **reused-verbatim** `VerificationResult`. |
| `Drops` | `bool` | Gate decision: `=> Result?.Verdict == Hallucinated`. False for every other outcome (failure/damage never silently drop). |
| `Errors` | `IReadOnlyList<string>` | Specific error(s) on `EmptyResponse`/`ParseFailure`; empty on success. |

- Factories: `FromVerdict(VerificationResult)`, `Failure(VerdictIngestOutcome, errors)`.
- **State transitions**: `Classify(json)` is total and pure — every input maps to exactly one
  outcome and **never throws**. Missing `verdict` → `ParseFailure` ("critic response missing
  required 'verdict' field"). Missing `score` → `ParseFailure`. Non-JSON / no `{…}` → `ParseFailure`.
  Empty/whitespace → `EmptyResponse`. Valid `{ verdict, score, … }` → `Verdict`.

## New types (config + skill)

### `CriticModelResolver` (new, static)
Single source of truth for the critic model (FR-004/FR-008).

- `Resolve(CriticConfig?) : string` — returns `config.Model` when non-empty, else the single
  same-family `DefaultCriticModel` constant (§32 direction; target Sonnet 4.6).
- Replaces the duplicated provider→default switch in `CopilotCritic.GetEffectiveModel`
  (`GroundingAgent.cs:192`) and `CopilotService.GetCriticModel` (`CopilotService.cs:319`).
- **Validation rule**: no provider-keyed branch remains; the only inputs that affect the result are
  `config.Model` (override) and the single default constant.

### `spectra-critic.agent.md` (new skill artifact)
A `context: fork` subagent definition (frontmatter + procedure).

| Field | Value | Notes |
|-------|-------|-------|
| `name` | `spectra-critic` | Registered in `SkillsManifest` / `AgentContent`. |
| `description` | Verifies a generated test against its source documents and returns a JSON verdict. | |
| context isolation | `fork` (fresh context) | Formalizes existing isolation (artifact + docs only). |
| `disable-model-invocation` | `true` | Explicit invocation only (FR-003) — never auto-invoked. |
| input | test artifact + ≤5 selected source documents | No generator prompt/reasoning/tool-calls/tokens. |
| output | `{ verdict, score, findings }` JSON | The shape `ingest-verdict` consumes. |

## Reused verbatim (unchanged — the boundary this spec builds on)

- **`VerificationVerdict`** (`Spectra.Core`): `Grounded | Partial | Hallucinated | Manual`.
  Unchanged. "Unverified" is modeled as `Partial` + `Errors`; it passes the gate.
- **`VerificationResult`** (`Spectra.Core`): verdict/score/findings/critic-model/errors +
  `IsSuccess => Errors.Count == 0` and `ToMetadata(generatorModel)`. Unchanged.
- **`GroundingMetadata`** (`Spectra.Core`): the frontmatter record written by the unchanged
  `CreateTestWithGrounding` write-back. Unchanged.
- **`CriticPromptBuilder`**: assembles system + user prompt (artifact + ≤5 docs, 8000-char
  truncation). Reused unchanged; the new compiler delegates to it.
- **`CriticResponseParser` JSON *shape***: fence-strip + `{…}` slice + verdict/score/findings
  extraction structure. The parse *structure* is reused; the missing-field *defaults* are
  re-decided at the new boundary (FR-006). The in-process path keeps the existing parser.
- **Verdict-gating + `Manual`-preservation**: `Verdict != Hallucinated` filter
  (`GenerateHandler.cs:847`) and the `Manual` skip/preserve (`GenerateHandler.cs:2134`,
  `2237–2260`). Unchanged.

## Relationships

```
compile-critic-prompt ── CriticPromptCompiler.Compile ──▶ CriticPromptCompileResult
                                   │ delegates
                                   ▼
                          CriticPromptBuilder (reused)

[critic subagent skill runs the compiled prompt → returns { verdict, score, findings }]

ingest-verdict ── VerdictIngestor.Classify ──▶ VerdictIngestResult
                                   │ on Verdict carries
                                   ▼
                          VerificationResult (reused) ──▶ gate: Drops iff Hallucinated

critic model:  CopilotCritic.GetEffectiveModel ─┐
                                                 ├─▶ CriticModelResolver.Resolve (single source)
               CopilotService.GetCriticModel  ──┘
```
