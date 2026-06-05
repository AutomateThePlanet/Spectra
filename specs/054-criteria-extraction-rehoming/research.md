# Phase 0 Research: Criteria Extraction Re-homing + Extractor Unification

All "NEEDS CLARIFICATION" items were resolved by reading the 053 precedent and the existing
extraction code. No open unknowns remain.

## R1 — What did 053 actually ship vs. what its spec said?

**Decision**: Treat 053 as an **additive CLI-surface** delivery and mirror that, not its
aspirational FR-001.

**Rationale**: 053's `spec.md` FR-001 says "the generation path MUST NOT call any model... MUST be
removed." But the merged implementation (commit `a204605`, subject literally tagged *"(CLI
surface)"*) only added `PromptCompiler` + `spectra ai compile-prompt` and `GeneratedTestIngestor` +
`spectra ai ingest-tests`. `GenerationAgent.GenerateTestsAsync` still calls
`session.SendAndWaitAsync` in-process. So the established, real precedent for this series is:
*ship the deterministic compile + fail-loud ingest surface additively; defer the in-process
model-call removal to the orchestration-skill spec.* This spec follows that precedent.

**Alternatives considered**: (a) Literal full removal now — rejected, breaks `ai analyze
--extract-criteria` and `docs index` until Spec 055. (b) Unification only — rejected, leaves FR-002
(deterministic compiler) and FR-003 (typed ingest boundary) undelivered.

## R2 — Shape of the deterministic compiler

**Decision**: `ExtractionPromptCompiler` static class with two entry points mirroring
`PromptCompiler`: `Assemble(...)` (lenient, relocated body of `CriteriaExtractor.BuildExtractionPrompt`)
and `Compile(...)` (validated → `ExtractionPromptCompileResult.Success | MissingRequired`).
`CriteriaExtractor.BuildExtractionPrompt` becomes a thin delegate to `Assemble` so there is one
source of prompt truth (exactly how `GenerationAgent.BuildFullPrompt` delegates to
`PromptCompiler.Assemble`).

**Rationale**: Determinism is already satisfied by the existing prompt text (no timestamps/GUIDs;
the only variability is the `templateLoader` path which is deterministic given the same template
files). Relocating it makes it a standalone, token-free testable artifact (FR-002).

**Required inputs for refuse-to-emit**: `documentPath` (names the source) and `documentContent`.
An **empty/whitespace `documentContent` is NOT a refusal** — it is the FR-003 short-circuit and is
handled *before* compilation by the caller/skill, which returns `Extracted, []` with no model turn
(preserving `CriteriaExtractor.cs:64`). `component` is optional. This matches `PromptCompiler.Compile`
treating `userPrompt`/`count`/`criteriaContext` as required and Testimize/focus as optional.

**Alternatives considered**: Making the compiler also own the empty-source decision — rejected; the
short-circuit is a classification/outcome concern (`Extracted, []`), not a "missing input" refusal,
and conflating them would muddy the two cases the spec explicitly separates (empty *source* vs empty
*response*).

## R3 — Shape of the ingest boundary

**Decision**: `CriteriaIngestor` mirroring `GeneratedTestIngestor`: a pure
`Classify(content, source, component)` that delegates to the **reused-verbatim**
`CriteriaExtractor.ClassifyResponse` (FR-003, do-not-modify), plus an `IngestAsync(...)` that, on
`Extracted`, persists through the existing `CriteriaFileWriter` + criteria-index upsert (the same
write path `AnalyzeHandler` uses at lines 642–668) and, on `EmptyResponse`/`ParseFailure`, persists
nothing and returns a fail-loud result. Only `Extracted` (`IsCacheable`) is ever written — FR-006.

**Rationale**: Reuses the exact classification + persistence the in-process path already uses, so
the agent-driven surface produces byte-identical on-disk results. The cache-poisoning guard is
structurally identical to `AnalyzeHandler.cs:575` (`if (!result.IsCacheable) … continue;`).

**Alternatives considered**: A brand-new parser — rejected; `ClassifyResponse` is the pinned
boundary contract this spec is built on and must be reused verbatim.

## R4 — Exit-code contract for the new commands

**Decision**: Mirror the 053 commands.
- `compile-extraction-prompt`: `0` success (prompt to stdout, nothing to disk), `4` refuse-to-emit
  (missing required input, machine-readable `missing_input`), `1` environment error.
- `ingest-criteria`: `0` success (persisted `Extracted`), `5` content-invalid (`EmptyResponse`),
  `6` parse-invalid (`ParseFailure`), `1` environment error. The `5`/`6` split lets a retry skill
  re-prompt precisely, matching `ingest-tests` (`5` content, `6` schema).

**Rationale**: Consistency with the established `compile-prompt` (exit 4) and `ingest-tests`
(exit 5/6) contracts so the future orchestration skill (Spec 055) sees a uniform surface.

## R5 — Unifying `RequirementsExtractor` without re-opening Spec 047

**Decision**: Introduce `RequirementsExtractionResult(ExtractionOutcome Outcome,
IReadOnlyList<RequirementDefinition> Requirements)` with `IsCacheable => Outcome ==
ExtractionOutcome.Extracted`, reusing the **same** `ExtractionOutcome` enum as
`CriteriaExtractionResult`. `RequirementsExtractor.ExtractFromDocumentAsync` returns it:
empty source → `(Extracted, [])`; empty response → `(EmptyResponse, [])`; parse failure →
`(ParseFailure, [])`. The internal 2-minute `Task.WhenAny` **throw** is removed — the per-document
deadline already lives in `DocsIndexHandler.ExtractCriteriaLoopAsync` (`Task.Delay` + `onSlowDoc`),
so a timeout becomes a loop-level "slow doc" with no exception. `#pragma warning disable CS0618`
legacy marker is removed.

**Rationale**: Satisfies FR-004 (typed result, no throw on empty/timeout) and FR-007 (one
failure-semantics contract — the shared enum + `IsCacheable` rule). Keeps the `RequirementDefinition`
payload so `RequirementsWriter` and the requirements-file persistence are unchanged (non-breaking).
Does **not** merge the two extractor classes — honoring the Spec 047 decision the spec says not to
re-open.

**Alternatives considered**: (a) Reuse `CriteriaExtractionResult` for the requirements path —
rejected; different payload type, would force a `docs index` persistence rewrite. (b) Keep throwing
and catch in the loop — rejected; FR-004 explicitly forbids the throw and the loop's broad
`catch (Exception)` masks the empty/timeout distinction.

## R6 — `ExtractCriteriaLoopAsync` / `TryExtractCriteriaAsync` migration

**Decision**: Change the loop's `extractPerDoc` delegate to return `RequirementsExtractionResult`;
aggregate only `Extracted.Requirements`; count `EmptyResponse`/`ParseFailure` as failed docs (via
the existing `onDocFailure` channel or a new typed counter). The corpus zero-criteria warning
(`ComputeCriteriaWarning`, Spec 048) and the `documents_failed`/`failed_documents` reporting are
preserved. The internal-throw removal means the loop's broad `catch` now only catches genuine
unexpected exceptions, not the expected empty/timeout cases.

**Rationale**: Keeps every existing `docs index` reporting behavior (Spec 047/048 guards) while
routing the now-typed outcomes through it. Minimal blast radius.

## R7 — Protected regression net (do not touch)

**Decision**: Do not modify any `Spectra.Core` parsing/requirements tests, nor the
`CriteriaExtractionResult`/`ClassifyResponse` classification tests. The
`RequirementsExtractor` throw-on-empty / throw-on-timeout tests **are** rewritten (they pin behavior
this spec deletes). Any break in the protected net is a regression to investigate.

**Rationale**: Directly from the spec's "DO NOT TOUCH — regression net" and "Rewrite" sections.
