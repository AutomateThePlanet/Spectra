# Phase 0 Research: Resilient Criteria Extraction (Spec 047)

**Branch**: `047-resilient-criteria-extraction`
**Date**: 2026-05-28
**Spec**: [spec.md](./spec.md)

## Source code reconnaissance

Verified line numbers from the spec against current `main` (commit `399b4ad`). The original spec's references are accurate but it mis-identifies the extractor used by the `docs index` path — that is `RequirementsExtractor`, not `CriteriaExtractor`.

| Symbol | Real path | Confirmed lines |
|---|---|---|
| `CriteriaExtractor.ExtractFromDocumentAsync` | `src/Spectra.CLI/Agent/Copilot/CriteriaExtractor.cs` | `:52` signature; `:58` empty-input `[]`; `:92` empty-response `[]` |
| `CriteriaExtractor.ParseResponse` | same | `:209-248`; `:217` no-`[`/`]`; `:227` deserialize-null; `:244-247` catch-all `return []` |
| `AnalyzeHandler.RunExtractCriteriaAsync` per-doc loop | `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs` | `:406-592`; hash compute fail `:425-429` (continue, no hash); cache-skip gate `:437`; per-doc 2-min `Task.WhenAny` deadline `:466-477`; exception handler `:481-491` (continue, no hash); hash recorded `:549-569` |
| `DocsIndexHandler.TryExtractCriteriaAsync` | `src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs` | `:247-332`; 60s corpus deadline `:288-296` |
| `RequirementsExtractor.ExtractAsync` | `src/Spectra.CLI/Agent/Copilot/RequirementsExtractor.cs` | `:30-81`; batches **all** documents into one prompt (`:88-97`); already has an internal 2-min `Task.WhenAny` on the AI call `:50-61` |

**Correction to the spec's solution sketch**: Part C cannot simply "apply the same per-document loop the analyze path uses" without changing extractor classes (the two extractors return different model types — `AcceptanceCriterion` vs `RequirementDefinition`). The compatible-with-out-of-scope realisation is **Option B from the spec text**: add a per-document method on the existing `RequirementsExtractor` and iterate documents in `DocsIndexHandler` with a 2-min deadline per call.

## Decisions

### D1. Typed result shape: dedicated record, not generic `Result<T, E>`

**Decision**: Introduce a sealed record `CriteriaExtractionResult(ExtractionOutcome Outcome, IReadOnlyList<AcceptanceCriterion> Criteria)` plus an `ExtractionOutcome` enum with three values: `Extracted`, `EmptyResponse`, `ParseFailure`. Add an `IsCacheable` computed property (`Outcome == Extracted`).

**Rationale**: Single use site (one extractor, one caller). A bespoke record is the simplest thing that works (Constitution V: YAGNI). A generic `Result<T,E>` would introduce a new abstraction with no second use case and would obscure the three-state classification at the call site.

**Alternatives considered**:
- Generic `Result<T, Error>` — premature abstraction; rejected.
- Out-parameter `(bool cacheable, IReadOnlyList<…> items, string? failureReason)` — loses the named enum, harder to pattern-match. Rejected.
- Throwing distinct exception types for `EmptyResponse`/`ParseFailure` — turns control flow into exceptions for an in-band, expected-during-normal-operation outcome. Rejected.

### D2. Retry budget and backoff: 2 attempts, 1.5s then 3s

**Decision**: At most 2 attempts total (i.e. 1 retry). Backoff: 1.5s before attempt 2. No jitter, no third attempt. Only `EmptyResponse` and `ParseFailure` are retried. `Extracted` is final by construction; thrown exceptions and per-doc timeouts retain today's "log and continue without caching" behaviour.

**Rationale**: The spec caps the budget at "max 2 attempts, short backoff (e.g. 1.5s, 3s)" and explicitly warns "Keep total work bounded — 2 attempts, not 5." A single retry covers transient model glitches and brief network blips; further retries on a corpus of hundreds of documents add cost without clear value. Per-doc timeout (2 min) already bounds latency per attempt.

**Test-time override**: The retry helper accepts an `IDelayProvider` (or `Func<TimeSpan, Task>`) seam so tests can substitute a no-op delay. Default is `Task.Delay`. This avoids real wall-clock 1.5s waits multiplied across the failing-document test cases.

**Alternatives considered**:
- Exponential backoff with jitter (e.g. 1, 2, 4s + jitter) — overkill for 2 attempts; complicates tests for no observable user benefit.
- Configurable retry count via `spectra.config.json` — pure YAGNI; no use case for tuning retries today.

### D3. Per-document timeout on the `docs index` path: add a per-doc method on `RequirementsExtractor`

**Decision**: Add `RequirementsExtractor.ExtractFromDocumentAsync(DocumentEntry doc, IReadOnlyList<RequirementDefinition> existing, CancellationToken ct)`. It sends one prompt per document (the existing prompt builder is trivially adapted to a single-doc form). Inside `DocsIndexHandler.TryExtractCriteriaAsync`, iterate `documentMap.Documents`, call the new per-doc method inside a 2-minute `Task.WhenAny` deadline (mirroring `AnalyzeHandler.cs:467`), aggregate the per-doc results, and report timed-out documents with the existing "run `spectra ai analyze --extract-criteria` separately" guidance, scoped to the offending document only.

**Rationale**: Delivers FR-007 (per-doc timeout), FR-008 (other docs continue), FR-009 (per-doc warning) without merging the two extractor classes (Out of Scope). `RequirementsExtractor` keeps its prompt, model type (`RequirementDefinition`), and post-processing — only the input scope shrinks from "all docs" to "one doc."

**Alternatives considered**:
- Just remove the outer 60s wrap and keep the whole-corpus batched call. Rejected — it leaves AC #7 (each doc that individually completes is extracted) only half-true: a single slow doc still kills the whole batch at the internal 2-min deadline, and the typed-result/retry work from Part A/B is bypassed entirely on this path.
- Route `docs index` through `CriteriaExtractor` + `AnalyzeHandler`'s per-doc loop. This is the spec's "preferred" option but it requires changing the output model (`AcceptanceCriterion` vs `RequirementDefinition`) and the downstream writer (`RequirementsWriter` consumes `RequirementDefinition`). That is a merge with regression surface across two commands → **Out of Scope** per the spec.

### D4. Catch-all logging: route through existing `DebugLogger`/`ErrorLogger` plumbing

**Decision**: The replaced `catch { return []; }` becomes `catch (Exception ex) { ... return ParseFailure; }`. Log the exception via `Spectra.CLI.Infrastructure.ErrorLogger.Write("criteria", $"doc={documentPath}", ex)` — the same call site used in `AnalyzeHandler.cs:484` for thrown exceptions. The existing `RecordAndLog` helper writes the success log; the failure path gets the parallel error-log call.

**Rationale**: Re-use existing infrastructure (Constitution V: YAGNI). No new logging surface; `ErrorLogger` already produces the structured "full exception context" output that Spec 043 introduced.

### D5. Failure surfacing: piggy-back on `documents_failed` / `failed_documents`

**Decision**: `EmptyResponse`/`ParseFailure` after the retry budget map to the existing `documentsFailed++` and `failedDocuments.Add(doc.Path)` calls in `AnalyzeHandler.RunExtractCriteriaAsync`. The new informational error line is `$"Extraction inconclusive for {doc.Path} ({outcome}); will retry on next run."` — written to stderr with `_verbosity >= VerbosityLevel.Normal` gating to match the surrounding code's verbosity discipline. Exit code is unchanged (existing convention already accounts for `documentsFailed > 0`).

**Rationale**: No new reporting surface; CI consumers of `documents_failed` keep working. FR-010 explicitly requires this.

### D6. Empty input content stays cacheable

**Decision**: `string.IsNullOrWhiteSpace(documentContent)` at `CriteriaExtractor.cs:58` returns `new CriteriaExtractionResult(Extracted, [])`. The caller writes a hash, so an empty source file is not re-read on every run.

**Rationale**: The spec mapping table makes this explicit ("genuinely nothing to extract — cacheable; avoids re-reading an empty doc forever"). FR-003 codifies it.

### D7. `SplitAndNormalizeAsync` is *not* refactored

**Decision**: `CriteriaExtractor.SplitAndNormalizeAsync` (`:101-142`) also calls `ParseResponse` but is an import-flow utility used by `ai analyze --import-criteria`, not the per-doc extraction loop targeted by this spec. It keeps returning `IReadOnlyList<AcceptanceCriterion>` and continues to translate any non-`Extracted` outcome into an empty list internally. No caller change required.

**Rationale**: Scope discipline — the spec targets cache poisoning on the extraction path; the split/normalize path doesn't cache. Touching it widens scope without measurable benefit.

### D8. Cancellation and exception semantics unchanged

**Decision**: `OperationCanceledException` and any other thrown exception inside `ExtractFromDocumentAsync` continue to propagate to `AnalyzeHandler`'s existing `try/catch` (`:481-491`) which logs full exception context and `continue`s without recording a hash. The new retry helper does NOT swallow exceptions; it only retries non-throwing non-cacheable outcomes.

**Rationale**: FR-005 explicitly forbids retrying thrown exceptions/timeouts; Constitution II (deterministic execution) — cancellation should not be silently re-attempted.

## Open questions resolved (no NEEDS CLARIFICATION remaining)

- **Retry count** → 2 attempts (D2).
- **Backoff** → 1.5s before attempt 2; test seam via `Func<TimeSpan,Task>` (D2).
- **Per-doc deadline for `docs index`** → 2 minutes (matches analyze path) (D3).
- **Whether to merge `RequirementsExtractor` and `CriteriaExtractor`** → No (out of scope) (D3).
- **Logging channel for swallowed exceptions** → existing `ErrorLogger` (D4).
- **Reporting/exit-code channel for non-cacheable docs** → existing `documents_failed`/`failed_documents`/existing exit-code convention (D5).
- **Cacheability of empty-input docs** → cacheable (D6).
- **Scope of `SplitAndNormalizeAsync`** → unchanged (D7).
