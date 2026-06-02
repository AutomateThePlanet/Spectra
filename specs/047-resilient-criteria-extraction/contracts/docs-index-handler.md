# Contract: `DocsIndexHandler.TryExtractCriteriaAsync` per-document timeout

**Project**: `Spectra.CLI` (`src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs:247-332`)
**Caller scope**: in-process. The CLI command `spectra docs index` is the externally observable surface; its result JSON shape (`CriteriaExtracted`, `CriteriaFile`) is unchanged in shape, but `CriteriaExtracted` is no longer 0 on large corpora that previously hit the 60-second cliff.

## Important note on which extractor this path uses

`docs index` uses `RequirementsExtractor` (whole-corpus batched, returns `RequirementDefinition`). It does NOT use `CriteriaExtractor` (per-doc, returns `AcceptanceCriterion`). The two extractors have different output models and write to different files via different writers (`RequirementsWriter` vs `CriteriaFileWriter`/`CriteriaIndexWriter`).

The original Spec 046 sketch suggested "route docs index through the same per-doc loop the analyze path uses." That would require converting `RequirementsExtractor` output to `AcceptanceCriterion` (or vice versa) and re-wiring the writer pipeline — i.e. merging the two extractors. **That merge is explicitly Out of Scope** in the original spec. This contract therefore implements the spec's stated fallback: add a per-doc method on the existing `RequirementsExtractor` and iterate documents in `DocsIndexHandler`.

## Behaviour required

### Before (current `main`)

```csharp
var extractor = new RequirementsExtractor(provider, currentDir, onStatus);
var extractTask = extractor.ExtractAsync(documentMap.Documents, existing, ct);     // all docs in one prompt
var deadlineTask = Task.Delay(TimeSpan.FromSeconds(60), ct);                       // <-- corpus-wide deadline
var completed = await Task.WhenAny(extractTask, deadlineTask);
if (completed == deadlineTask)
{
    _progress.Warning("Acceptance criteria extraction timed out. Run '…' separately.");
    return (0, null);                                                              // <-- whole run aborted
}
```

### After

```csharp
var extractor = new RequirementsExtractor(provider, currentDir, onStatus);
var aggregated = new List<RequirementDefinition>(capacity: documentMap.Documents.Count * 8);
var timedOut = new List<string>();

foreach (var doc in documentMap.Documents)
{
    try
    {
        var extractTask  = extractor.ExtractFromDocumentAsync(doc, existing, ct);  // NEW per-doc method
        var deadlineTask = Task.Delay(TimeSpan.FromMinutes(2), ct);                // matches AnalyzeHandler:467
        var completed    = await Task.WhenAny(extractTask, deadlineTask);

        if (completed == deadlineTask)
        {
            timedOut.Add(doc.Path);
            _progress.Warning(
                $"Acceptance criteria extraction timed out for {doc.Path}. " +
                "Run 'spectra ai analyze --extract-criteria' separately for this document.");
            continue;
        }

        aggregated.AddRange(await extractTask);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)
    {
        _progress.Warning($"Acceptance criteria extraction failed for {doc.Path}: {ex.Message}");
        if (_verbosity >= VerbosityLevel.Detailed)
            Console.Error.WriteLine(ex.StackTrace);
        // do not record success for this doc, do not abort the rest of the corpus
    }
}

// existing post-processing (writer merge, etc.) consumes `aggregated`
```

### New `RequirementsExtractor` method

```csharp
public Task<IReadOnlyList<RequirementDefinition>> ExtractFromDocumentAsync(
    DocumentEntry doc,
    IReadOnlyList<RequirementDefinition> existing,
    CancellationToken ct);
```

The existing prompt template (`RequirementsExtractor.cs:104-126`) is adapted to a single document by formatting `docContent` as one `## Document: {path}\n\n{content}` block — i.e. the body of the existing `foreach (var doc in documents)` loop for one document. `ExtractAsync(IReadOnlyList<...>)` may either be removed or implemented as a loop over the new per-doc method; either is fine. (Removal is cleaner and there is no public caller in tree other than `DocsIndexHandler`.)

The internal `Task.WhenAny` 2-minute SDK guard inside `RequirementsExtractor` (`:50-61`) is retained on the per-doc method — it's a belt-and-braces guard for the SDK not honouring cancellation. The outer 2-minute deadline in `DocsIndexHandler` and the inner 2-minute SDK guard are independent safety nets.

## Externally observable outputs

| Output | Before | After |
|---|---|---|
| `CriteriaExtracted` (JSON result) | 0 on any corpus exceeding 60s wall-clock | Sum of per-doc extractions that completed within 2 minutes each |
| Warning channel | One whole-corpus "extraction timed out. Run … separately." | One per timed-out document, naming the document |
| Behaviour when one doc is slow | All extraction aborted | Slow doc skipped; rest succeed |
| Behaviour when one doc errors | Whole `try` block fails; no extraction | That doc skipped; rest succeed |
| `RequirementsWriter` invocation | Once, with aggregated batch | Once, with aggregated batch (same shape) |
| Exit code | `Success` | `Success` (this command doesn't surface per-doc failures in its exit code; that is a separate concern handled in the analyze command) |

## Non-changes (explicit)

- `RequirementsWriter.MergeAndWriteAsync` and its output file are unchanged.
- `RequirementDefinition` model unchanged.
- `_progress.Warning` channel and verbosity gating unchanged.
- The "documents in skip_analysis suites are dropped" filter (`:271-272`) is unchanged.
- Dry-run handling unchanged.
- The `docs index` JSON result shape (`DocumentsIndexed`, `DocumentsUpdated`, ..., `CriteriaExtracted`, `CriteriaFile`) is unchanged.
