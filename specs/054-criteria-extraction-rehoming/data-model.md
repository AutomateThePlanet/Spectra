# Phase 1 Data Model: Criteria Extraction Re-homing + Extractor Unification

No persisted data-model changes. The `.criteria.yaml` schema, `_criteria_index.yaml`, the
requirements markdown file, and the content-hash (`DocHash`) cache key are all **unchanged**. The
entities below are in-memory contract types.

## Reused verbatim (MUST NOT modify)

### `CriteriaExtractionResult` (`Agent/Copilot/CriteriaExtractionResult.cs`)
```
record CriteriaExtractionResult(ExtractionOutcome Outcome, IReadOnlyList<AcceptanceCriterion> Criteria)
  bool IsCacheable => Outcome == ExtractionOutcome.Extracted
```
### `ExtractionOutcome` (same file)
```
enum ExtractionOutcome { Extracted, EmptyResponse, ParseFailure }
```
### `CriteriaExtractor.ClassifyResponse(responseText, source, component, onException?)` — pure
function producing the above. The new `CriteriaIngestor` calls it; it is **not** changed.

## New types

### `ExtractionPromptCompileResult` (`Extraction/ExtractionPromptCompileResult.cs`)
Mirrors `PromptCompileResult`. Models "missing required input" as a value, not an exception.

| Member | Type | Notes |
|--------|------|-------|
| `IsSuccess` | `bool` | true when `Prompt` populated |
| `Prompt` | `string?` | compiled extraction prompt; non-null on success |
| `MissingInput` | `string?` | machine-readable name (`document_path`, `document_content`) on failure |
| `Message` | `string?` | human-readable refusal reason on failure |
| `Success(prompt)` / `MissingRequired(input, message)` | factory | — |

### `RequirementsExtractionResult` (`Agent/Copilot/RequirementsExtractionResult.cs`)
The unified failure-semantics wrapper for the `docs index` path. **Reuses** `ExtractionOutcome`.

| Member | Type | Notes |
|--------|------|-------|
| `Outcome` | `ExtractionOutcome` | shared enum — the unification point (FR-007) |
| `Requirements` | `IReadOnlyList<RequirementDefinition>` | payload; non-empty only on `Extracted` |
| `IsCacheable` | `bool` (computed) | `Outcome == ExtractionOutcome.Extracted` — identical rule to `CriteriaExtractionResult` (FR-006) |

Outcome mapping for `RequirementsExtractor.ExtractFromDocumentAsync`:
| Condition | Result |
|-----------|--------|
| empty/whitespace source content | `(Extracted, [])` — short-circuit, no model turn |
| empty/whitespace agent response | `(EmptyResponse, [])` — **was** `throw InvalidOperationException` |
| timeout | handled by loop deadline as slow-doc — **was** `throw TimeoutException` (internal throw removed) |
| response parses to ≥1 requirement | `(Extracted, requirements)` |
| response present but unparseable / 0 items | `(ParseFailure, [])` |

### `CriteriaIngestResult` (`Extraction/CriteriaIngestor.cs` or sibling)
Outcome of the model-free ingest boundary.

| Member | Type | Notes |
|--------|------|-------|
| `Outcome` | `ExtractionOutcome` | from `ClassifyResponse` |
| `PersistedCriteria` | `IReadOnlyList<AcceptanceCriterion>` | populated only on `Extracted` + persisted |
| `IsSuccess` | `bool` | `Outcome == Extracted` |
| `Errors` | `IReadOnlyList<string>` | populated on `EmptyResponse`/`ParseFailure` |

## Validation rules (from requirements)

- **FR-002 / determinism**: `ExtractionPromptCompiler.Assemble` output is a pure function of its
  inputs (no `DateTime`, GUID, or unordered enumeration). Equal inputs ⇒ equal string.
- **FR-002 / refuse-to-emit**: `Compile` returns `MissingRequired` when `documentPath` or
  `documentContent` is null/whitespace; never emits a partial prompt.
- **FR-003 / short-circuit**: empty-source ⇒ `Extracted, []` with no compile + no handoff
  (decision made before compilation).
- **FR-006 / cache gate**: persistence happens **iff** `IsCacheable` (`Extracted`). `EmptyResponse`
  and `ParseFailure` persist nothing and leave the `DocHash` unrecorded.
- **FR-004 / no throw**: `RequirementsExtractor` returns a typed outcome for empty and timeout —
  zero exceptions for those two cases.

## State / flow

```
compile (deterministic, model-free)        ingest (model-free)
──────────────────────────────────         ───────────────────────────────
document → [empty? → Extracted,[] ]         agent content
          → Compile → Success(prompt)  ──▶  → ClassifyResponse
            | MissingRequired(input)          → Extracted   → persist (writer + index upsert) → success
                                              → EmptyResponse → fail-loud, persist nothing (exit 5)
                                              → ParseFailure  → fail-loud, persist nothing (exit 6)

retry = skill choreography (Spec 055, out of scope): on exit 5/6, re-prompt agent, re-ingest,
        bounded by configured attempts. Preserves "retry only on non-cacheable; never cache a
        non-cacheable result" (FR-005/006). The retained in-process `ExtractWithRetryAsync`
        (Spec 047) is unchanged for the in-process path.
```
