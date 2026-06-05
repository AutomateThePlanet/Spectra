# Internal Contract: Unified `RequirementsExtractor` (typed outcome, no throw)

The `docs index` extraction path. After this change it returns a typed result sharing the
`ExtractionOutcome` enum and `IsCacheable` rule with `CriteriaExtractionResult` — one
failure-semantics contract (FR-004, FR-007). The in-process model call is **retained** (additive
scope); only the failure semantics change.

## Method contract
```csharp
// BEFORE: Task<IReadOnlyList<RequirementDefinition>>  — throws on empty/timeout
// AFTER:
Task<RequirementsExtractionResult> ExtractFromDocumentAsync(
    DocumentEntry document,
    IReadOnlyList<RequirementDefinition> existingRequirements,
    CancellationToken ct)
```

## Outcome table
| Input condition | BEFORE | AFTER |
|-----------------|--------|-------|
| empty/whitespace source | (n/a — read upstream) | `(Extracted, [])` short-circuit |
| empty agent response | `throw InvalidOperationException` | `(EmptyResponse, [])` |
| 2-min timeout | `throw TimeoutException` (internal `Task.WhenAny`) | internal throw removed → loop deadline reports slow-doc; no exception |
| parsed ≥1 requirement | `List<RequirementDefinition>` | `(Extracted, requirements)` |
| present but unparseable / 0 items | `[]` (silent) | `(ParseFailure, [])` |

`#pragma warning disable CS0618` legacy marker is removed.

## Consumer: `DocsIndexHandler`
- `ExtractCriteriaLoopAsync.extractPerDoc` delegate signature changes to return
  `RequirementsExtractionResult`.
- Aggregate only `result.Requirements` when `result.IsCacheable` (`Extracted`).
- `EmptyResponse`/`ParseFailure` increment the failed-doc count (existing `failed` list /
  `documents_failed` reporting) instead of surfacing as a thrown exception.
- The internal per-document `Task.Delay` deadline + `onSlowDoc` channel is **kept** (it, not the
  extractor's internal throw, is now the sole timeout mechanism).
- `ComputeCriteriaWarning` (Spec 048 corpus zero-criteria gate) and `documents_failed` /
  `failed_documents` reporting are preserved unchanged.

## Invariants preserved
- Per-document deadline behavior (one slow doc never aborts the corpus) — Spec 047.
- Corpus zero-criteria warning — Spec 048.
- Requirements-file persistence via `RequirementsWriter.MergeAndWriteAsync` — unchanged.
- `RequirementDefinition` payload + requirements parsing — unchanged (`Spectra.Core`, do-not-touch).
