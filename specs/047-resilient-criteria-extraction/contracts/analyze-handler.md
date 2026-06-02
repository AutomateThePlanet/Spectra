# Contract: `AnalyzeHandler.RunExtractCriteriaAsync` per-document loop

**Project**: `Spectra.CLI` (`src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs:406-592`)
**Caller scope**: in-process. The CLI command `spectra ai analyze --extract-criteria` is the externally observable surface; its result JSON shape (`documents_failed`, `failed_documents`, exit code) is unchanged.

## Behaviour required

### Per-doc loop, conceptual flow (after this spec)

```csharp
foreach (var doc in documentMap.Documents)
{
    if (ShouldSkipDocument(...)) { documentsSkipped++; continue; }                        // unchanged
    if (!TryComputeHash(...))   { documentsFailed++; failedDocuments.Add(...); continue; } // unchanged
    if (!force && cacheHit(...)) { documentsSkipped++; ...; continue; }                    // unchanged
    if (!TryReadContent(...))   { documentsFailed++; failedDocuments.Add(...); continue; } // unchanged

    CriteriaExtractionResult result;
    try
    {
        result = await ExtractWithRetryAsync(extractor, doc.Path, content, component,
                                             maxAttempts: 2, ct);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex)                                                                  // unchanged
    {
        ErrorLogger.Write("criteria", $"doc={doc.Path}", ex);
        if (_verbosity >= VerbosityLevel.Normal)
            Console.Error.WriteLine($"  Failed to extract from {doc.Path}: {ex.Message}");
        documentsFailed++;
        failedDocuments.Add(doc.Path);
        continue;                                                                          // no hash recorded
    }

    if (!result.IsCacheable)                                                              // NEW gate
    {
        documentsFailed++;
        failedDocuments.Add(doc.Path);
        if (_verbosity >= VerbosityLevel.Normal)
            Console.Error.WriteLine(
                $"  Extraction inconclusive for {doc.Path} ({result.Outcome}); will retry on next run.");
        continue;                                                                          // no hash recorded
    }

    // Cacheable: record hash, write file, update index — UNCHANGED logic from :493-591.
    var extracted = result.Criteria;
    // ... existing ID assignment, file write, source-record upsert ...
}
```

### Helper signature

```csharp
private static async Task<CriteriaExtractionResult> ExtractWithRetryAsync(
    CriteriaExtractor extractor,
    string documentPath,
    string documentContent,
    string? component,
    int maxAttempts,                          // = 2
    CancellationToken ct,
    IExtractionDelayProvider? delayProvider = null)   // test seam; defaults to Task.Delay
```

Retry policy:

| Outcome on attempt | Action |
|---|---|
| `Extracted` (any count) | Return immediately. No further attempt. |
| `EmptyResponse` and `attempt < maxAttempts` | Wait 1.5s (or test-injected delay), retry. |
| `ParseFailure` and `attempt < maxAttempts` | Wait 1.5s (or test-injected delay), retry. |
| `EmptyResponse`/`ParseFailure` and `attempt == maxAttempts` | Return result (non-cacheable). |
| Exception thrown | Propagate to caller (no retry, no swallow). |

Note: with `maxAttempts: 2` there is exactly one retry, so only the first delay (1.5s) is ever used. The "3s before attempt 3" the original spec sketch mentions is moot at this budget; if the budget is later raised, the helper should use `1.5 * attempt` (1.5s, 3.0s, 4.5s, ...) — kept as a one-liner so it doesn't become a config knob.

### Timeout interaction (existing behaviour preserved)

The 2-minute `Task.WhenAny` deadline (`AnalyzeHandler.cs:466-477`) **remains** and **wraps each attempt**. If an attempt times out, it is treated like a thrown exception: no retry, no hash, the doc is counted as failed. This is FR-005 ("does not retry … timeouts").

Concretely: the `extractTask`/`deadlineTask` `WhenAny` block is *inside* `ExtractWithRetryAsync` per attempt, or we lift the deadline out and wrap it around `extractor.ExtractFromDocumentAsync(...)` calls inside the helper. Either is fine; the helper version keeps responsibilities tight.

## Externally observable outputs

| Output | Before | After |
|---|---|---|
| `documents_failed` count | Excludes silently-cached-empty cases | Includes them — they no longer go silent |
| `failed_documents` list | Same | Adds entries for `EmptyResponse`/`ParseFailure`-after-retry |
| Process exit code | Existing convention | Unchanged; reflects `documents_failed > 0` per existing convention |
| `_criteria_index.yaml` source records | One per processed doc | One per **cacheable** doc — non-cacheable docs leave no record (so next non-`--force` run re-attempts them) |
| Per-doc `.criteria.yaml` files | Written on any non-throwing extraction | Written only when `IsCacheable` |
| Stderr | `Failed to extract from … : {ex.Message}` on throws; `Timeout extracting from …` on deadline | Adds `Extraction inconclusive for … (Outcome); will retry on next run.` for non-cacheable. Gated by `_verbosity >= Normal`. |
| Result JSON shape | Same fields | Same fields, same types |

## Non-changes (explicit)

- `OrphanedSources` detection at `:594-601` and orphan removal at `:603-609`: untouched. (A doc that yields `ParseFailure` does NOT have an orphan record because it never had a record in the first place; this is the right behaviour.)
- `ExtractedCriterion` ID-assignment loop at `:519-540`: untouched.
- Dry-run handling at `:546`: untouched.
