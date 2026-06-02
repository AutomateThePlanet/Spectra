# Phase 1 Data Model: Resilient Criteria Extraction (Spec 047)

This spec is a behavioural fix on existing code paths. No persisted data model changes. No JSON schema changes, no YAML frontmatter changes, no SQLite migrations. The only new types are in-process value types in `Spectra.CLI.Agent.Copilot`.

## In-process types (new)

### `ExtractionOutcome` (enum)

Public, lives in `Spectra.CLI.Agent.Copilot`.

| Member | Meaning | Cacheable? |
|---|---|---|
| `Extracted` | A valid criteria list (possibly empty) is in hand — either the AI returned valid JSON, or the source document was empty/whitespace and there was nothing to extract. | **Yes** |
| `EmptyResponse` | The AI returned an empty/whitespace response — treated as a transport-class failure. | No |
| `ParseFailure` | A response was present but unparseable: no `[`/`]` delimiters, deserialize returned `null`, or an exception was raised while parsing. | No |

Three discrete states, deliberately no fourth "other" bucket. New states would need their own caching policy decision, so adding one is a deliberate change, not an oversight.

### `CriteriaExtractionResult` (record)

```csharp
public sealed record CriteriaExtractionResult(
    ExtractionOutcome Outcome,
    IReadOnlyList<AcceptanceCriterion> Criteria)
{
    public bool IsCacheable => Outcome == ExtractionOutcome.Extracted;
}
```

Invariants:
- `Criteria` is empty whenever `Outcome != Extracted`. Construction sites enforce this; callers may rely on it. (The constructor doesn't validate, but every internal call site obeys it. Adding validation here would be defensive coding for an unreachable state — Constitution V.)
- `IsCacheable` is the only thing the caller in `AnalyzeHandler` looks at when deciding whether to write a hash. Adding any other gate (e.g. "must have at least one criterion") would re-introduce the bug for legitimately empty docs.

### `IExtractionDelayProvider` *(internal test seam)*

```csharp
internal interface IExtractionDelayProvider
{
    Task DelayAsync(TimeSpan duration, CancellationToken ct);
}
```

Default implementation calls `Task.Delay`. The retry helper (`ExtractWithRetryAsync`, lives next to or inside `AnalyzeHandler`) takes one in its constructor / signature so tests can substitute a no-op. Internal only — no public surface.

## Existing types (unchanged by this spec)

For reference and explicit non-change. These were named in the original problem note as **out of scope** for this work.

| Type | Path | Why unchanged |
|---|---|---|
| `AcceptanceCriterion` | `Spectra.Core.Models.Coverage` | The criterion model is fine; the bug is in how the *list* of them is interpreted. |
| `CriteriaSource` | `Spectra.Core.Models.Coverage` | The persisted per-doc cache record. We change *when* it is written, not its shape. |
| `CriteriaFileWriter` | `Spectra.CLI` | Writes the per-doc `.criteria.yaml`. Empty-file-on-disk distinguishability is deferred to Spec 048+. |
| `CriteriaIndexWriter` | `Spectra.CLI` | Writes `_criteria_index.yaml`. Unchanged. |
| `FileHasher` | `Spectra.Core` | SHA-256 over file content. Unchanged. |
| `RequirementDefinition` | `Spectra.Core.Models.Coverage` | Output model of `RequirementsExtractor`. Unchanged; the merge with `AcceptanceCriterion` is explicitly Out of Scope. |
| `RequirementsWriter` | `Spectra.CLI` | Consumes `RequirementDefinition`. Unchanged. |

## State transitions

There is no state machine here. The retry helper is a simple bounded loop:

```text
state start --→ attempt 1 → result
                 ├── Extracted ──→ DONE(cacheable=true)
                 ├── EmptyResponse or ParseFailure
                 │       │  (attempt < 2 ?)
                 │       ├── yes → delay 1.5s → attempt 2 → result
                 │       │                                  ├── Extracted → DONE(cacheable=true)
                 │       │                                  └── EmptyResponse/ParseFailure → DONE(cacheable=false)
                 │       └── no (cannot reach: max is 2)
                 └── (thrown) ──→ propagate (handled by caller's existing try/catch, no retry)
```

Per-document timeouts (`Task.WhenAny` deadline) sit *outside* this loop — they are owned by the caller (`AnalyzeHandler` for the analyze path, `DocsIndexHandler` for the docs-index path). A timeout cancels the attempt and is *not* retried by `ExtractWithRetryAsync` (matches FR-005).

## Data flow (no persisted change)

```text
DocumentEntry ─→ CriteriaExtractor.ExtractFromDocumentAsync(...)
                       │
                       ▼
           CriteriaExtractionResult (Outcome, Criteria)
                       │
                       ▼
       AnalyzeHandler retry wrapper (max 2 attempts, EmptyResponse/ParseFailure only)
                       │
                       ▼
       IsCacheable? ──no──→ documentsFailed++; failedDocuments.Add(path); continue (no hash, no file write)
            │
           yes
            ▼
       CriteriaFileWriter.WriteAsync(...)       // unchanged
       CriteriaSource hash recorded              // unchanged shape; gated on cacheable
```

The only data-shape question worth calling out: **`Criteria.Count == 0` is now a valid cacheable outcome** (a real empty result from the AI, or an empty source document). Today the file is written either way, the source record gets `CriteriaCount: 0`, and the next non-`--force` run hits the hash-skip gate at `AnalyzeHandler.cs:437`. No code change is required to make this work; it already works correctly when the result genuinely is "no criteria." The fix is to stop other failure modes from masquerading as that case.
