# SPECTRA — Criteria Extraction Investigation

**Status:** Observation only. No fixes, no refactors, no proposals.
**Scope:** `AutomateThePlanet/Spectra` (`main`).
**Method:** Static reading. Every claim cites `path:line` or `path:method`.
Where static reading cannot conclude, the section is marked
**INCONCLUSIVE — needs runtime repro**.

**Hypothesis under test:** the per-document SHA-256 incremental cache is
"poisoned" — a document is recorded as processed (its hash stored) even
though extraction for that document failed or produced nothing, so
subsequent runs skip it forever.

---

## Q1 — Where is extraction triggered relative to indexing?

### Q1.1 Code path

1. `spectra docs index` → `DocsIndexHandler.ExecuteAsync(force, ct)`
   (`src/Spectra.CLI/Commands/Docs/DocsIndexHandler.cs:52`).
2. `ExecuteCoreAsync` reaches the extraction gate at
   `DocsIndexHandler.cs:192–199`:
   ```csharp
   if (!_skipCriteria)
   {
       WriteProgressResult("extracting-criteria", …);
       (criteriaExtracted, criteriaFile) =
           await TryExtractCriteriaAsync(currentDir, config, ct);
   }
   ```
   Plain `spectra docs index` (no flags) DOES trigger extraction. Only
   the explicit `--skip-criteria` flag suppresses it. The `--force` flag
   here is for the **document-index** layer (forcing re-checksum of
   `docs/_index/_checksums.json`), not for criteria extraction.
3. `TryExtractCriteriaAsync` (`DocsIndexHandler.cs:247–310`) constructs
   a `RequirementsExtractor` and applies a **60-second total deadline
   for the entire corpus**:
   ```csharp
   var extractor = new RequirementsExtractor(provider, currentDir, …);
   var extractTask = extractor.ExtractAsync(documentMap.Documents, existing, ct);
   var deadlineTask = Task.Delay(TimeSpan.FromSeconds(60), ct);
   var completed = await Task.WhenAny(extractTask, deadlineTask);
   if (completed == deadlineTask)
   {
       _progress.Warning("Acceptance criteria extraction timed out. "
           + "Run 'spectra ai analyze --extract-criteria' separately.");
       return (0, null);
   }
   ```
4. `spectra ai analyze --extract-criteria` → `AnalyzeHandler.
   RunExtractCriteriaAsync(dryRun, force, ct)`
   (`src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs:260`). This path
   uses the **`CriteriaExtractor`** service (per-doc, with the hash
   cache) — a different class from `RequirementsExtractor`.

### Q1.2 Observed behavior

- The two CLI entry points use **two different extractor implementations**:
  - `docs index` → `RequirementsExtractor.ExtractAsync(...)` with a
    **single 60-second deadline for the entire run**, then a single
    "no new criteria" / "timed out" message.
  - `ai analyze --extract-criteria` → `CriteriaExtractor.
    ExtractFromDocumentAsync(...)` per-document, with the SHA-256
    incremental cache and a 2-minute deadline per document.
- At scale, the `docs index` path will almost certainly hit the 60-second
  total deadline (one document's AI call can already approach a minute),
  return `(0, null)`, and tell the user to "run 'spectra ai analyze
  --extract-criteria' separately" (`DocsIndexHandler.cs:294`).

### Q1.3 Bearing on the poisoning hypothesis

- **Q1 does not by itself confirm or refute the hypothesis** — it only
  identifies *which* path owns the SHA-256 cache.
- The cache lives only in the `ai analyze --extract-criteria` path
  (`AnalyzeHandler.RunExtractCriteriaAsync`). The `docs index` path's
  `RequirementsExtractor` is a different design with no per-doc skip
  cache.
- A user reporting "missing criteria on a big project" may be running
  either: (a) `docs index` and silently hitting the 60-second deadline
  (no poisoning, just unfinished work) or (b) `ai analyze
  --extract-criteria` and hitting the hash cache (potential poisoning).

---

## Q2 — The SHA-256 incremental cache

### Q2.1 Code path

1. Per-document hash is computed by
   `FileHasher.ComputeFileHashAsync(filePath, ct)`
   (`src/Spectra.CLI/Infrastructure/FileHasher.cs:23`), which reads the
   file and calls `FileHasher.ComputeHash(content)` (`FileHasher.cs:14`),
   which calls `SHA256.HashData` and returns lowercase hex.
2. Per-document hashes are persisted into the master criteria index
   `docs/criteria/_criteria_index.yaml`. Each
   `CriteriaSource` carries `DocHash`, `CriteriaCount`, `LastExtracted`
   (`src/Spectra.Core/Models/Coverage/CriteriaSource.cs`). The writer is
   `CriteriaIndexWriter.WriteAsync(path, index, ct)`
   (`src/Spectra.Core/Parsing/CriteriaIndexWriter.cs:20`).
3. The per-doc `.criteria.yaml` files also embed the hash in a comment
   header (`CriteriaFileWriter.WriteAsync`,
   `src/Spectra.Core/Parsing/CriteriaFileWriter.cs:35`):
   ```csharp
   if (docHash is not null)
       headerLines.Add($"# Doc hash: {docHash}");
   ```
   This header is informational only; the authoritative cache is the
   master index.
4. The skip decision lives in `AnalyzeHandler.cs:436–442`:
   ```csharp
   // Skip if hash matches and not forced
   if (!force && existingSource is not null &&
       existingSource.DocHash == docHash)
   {
       documentsSkipped++;
       criteriaUnchanged += existingSource.CriteriaCount;
       continue;
   }
   ```
5. The write-ordering inside the per-document loop
   (`AnalyzeHandler.cs:406–592`) is:

   ```
   line 423:  docHash = await FileHasher.ComputeFileHashAsync(...)
              (on exception → continue; hash NOT recorded)

   line 437:  if hash matches existing → continue (skip)

   line 466:  extractTask = extractor.ExtractFromDocumentAsync(...)
   line 467:  deadlineTask = Task.Delay(2 minutes)
   line 470:  if deadline fires → continue (hash NOT recorded)

   line 479:  extracted = await extractTask
   line 481:  catch (Exception …) → continue (hash NOT recorded)

   // Reaches here only on a non-throwing AI call.
   // `extracted` may be a non-empty list OR an empty list `[]`.

   line 549:  await fileWriter.WriteAsync(criteriaFilePath, extracted,
                                          doc.Path, docHash, ct)
              // Writes .criteria.yaml even if extracted.Count == 0.
              // File contains `# Doc hash: …` header.

   line 552:  if (existingSource is not null)
                  existingSource.DocHash    = docHash
                  existingSource.CriteriaCount = extracted.Count
                  existingSource.LastExtracted = DateTime.UtcNow
                  existingSource.File         = criteriaFileName
              else
                  criteriaIndex.Sources.Add(new CriteriaSource {
                      DocHash      = docHash,
                      CriteriaCount = extracted.Count,
                      LastExtracted = DateTime.UtcNow, …
                  })

   // End of per-doc loop body.

   line 614:  await indexWriter.WriteAsync(criteriaIndexPath,
                                           criteriaIndex, ct)
              // Persists the in-memory index after the loop completes.
   ```

### Q2.2 Observed behavior

- The hash is **not** written before the AI call. It is written **after**
  the AI call returns (success or empty list), as part of the per-doc
  bookkeeping at lines 549/552/561.
- The hash is **per-document in memory, batched to disk at the end**:
  each iteration mutates the in-memory `criteriaIndex.Sources` (lines
  552–569), and the index file itself is written once after the loop
  (line 614).
- Critically, **the hash is recorded for any AI call that does not
  throw**, regardless of whether the AI returned a useful result. The
  only "skip the write" gates are:
  - `try { docHash = FileHasher… } catch { continue; }` at lines 421–430
    (hash compute failed — read error)
  - `if (completed == deadlineTask) { … continue; }` at lines 470–477
    (per-doc 2-minute timeout)
  - `catch (Exception ex) … { continue; }` at lines 481–491
    (AI call threw)
- An AI call that returns successfully but with **content the parser
  reduces to `[]`** falls through to line 549 and triggers hash storage.
  (See Q3 for why the parser frequently reduces non-trivial responses
  to `[]` at scale.)

### Q2.3 Bearing on the poisoning hypothesis

- **Partially confirms** the hypothesis. The hash IS written
  unconditionally on any non-throwing AI call — including when the
  response was garbage that the parser silently swallowed.
- **Partially refutes** the hypothesis for the genuine-failure path:
  on exception (`catch` at 481–491) or per-doc timeout (470–477) or
  hash-compute failure (425–429), the loop does `continue` *before*
  reaching the writer block, so the hash is NOT poisoned for those
  cases. The next run will re-attempt.
- The actual mechanism for poisoning is therefore narrower than the
  hypothesis stated, but real: it occurs whenever the AI returns a
  syntactically successful response that `CriteriaExtractor.
  ParseResponse` reduces to `[]`. Q3 establishes this is a very large
  class of cases.

---

## Q3 — Failure handling during extraction

### Q3.1 Code path — per-document loop and try/catch

- The loop body is `AnalyzeHandler.cs:406–592`. The AI call and its
  guards sit at lines 462–491 (reproduced verbatim in Q2.1 above).
- Loop control on failure: every per-doc failure (`continue`) is
  followed by the next iteration. **No retry, no backoff, no break.**
- Per-doc failure is surfaced to the user:
  - `Console.Error.WriteLine(...)` at lines 472, 487 (Normal verbosity).
  - Full exception logged via `ErrorLogger.Write("criteria",
    $"doc={doc.Path}", ex)` at line 484, written to
    `.spectra/ai-debug.log`.
  - Failed-document list reported at end of run in both human and JSON
    output modes (the result type carries `documents_failed` and
    `failed_documents`).
- Exit code reflects per-doc failures (read directly from
  `AnalyzeHandler.cs` after the loop): all-failed → `ExitCodes.Error`
  (1); some-failed → `ExitCodes.ValidationError` (2); none-failed →
  `ExitCodes.Success` (0).

### Q3.2 Empty AI response vs AI failure — the crucial finding

`CriteriaExtractor.ExtractFromDocumentAsync` (`CriteriaExtractor.cs:52`)
returns `[]` for several **structurally different** failure modes:

- Caller passes empty/whitespace content (line 58–59):
  ```csharp
  if (string.IsNullOrWhiteSpace(documentContent))
      return [];
  ```
- AI replied with empty string (line 92–93):
  ```csharp
  if (string.IsNullOrWhiteSpace(responseText))
      return [];
  ```
- The response did not contain `[` or `]` (parse `IndexOf`) — lines
  214–217:
  ```csharp
  var jsonStart = responseText.IndexOf('[');
  var jsonEnd = responseText.LastIndexOf(']');
  if (jsonStart < 0 || jsonEnd <= jsonStart)
      return [];
  ```
- `JsonSerializer.Deserialize` returned `null` — line 226–227.
- Any exception during deserialization — lines 244–247:
  ```csharp
  catch
  {
      return [];
  }
  ```

Every one of these conditions returns `IReadOnlyList<AcceptanceCriterion>`
of length 0. The caller (`AnalyzeHandler`) has no way to tell them apart
from a legitimate "AI thoughtfully replied with `[]` because the document
genuinely had no testable criteria".

### Q3.3 Partial-progress persistence

- Per-doc `.criteria.yaml` files are written **inside the loop**
  (`AnalyzeHandler.cs:549`), so docs processed before a mid-run crash do
  persist their per-doc files.
- The master `_criteria_index.yaml` is written **once after the loop**
  (`AnalyzeHandler.cs:614`), in `if (!dryRun)`. A mid-run process kill
  before that line leaves all per-doc files on disk but no index entries
  for them. The next run, having no `existingSource` to match, will
  re-attempt those docs from scratch (no poisoning).

### Q3.4 Observed behavior

- The handler is robust against thrown exceptions and per-doc timeouts.
  Those paths skip cache update and re-attempt next run.
- The handler is **not robust** against parser-shaped failures: any AI
  reply that confuses `ParseResponse` becomes a "successful 0 criteria"
  outcome. Combined with Q2's hash-write ordering, this is the
  poisoning mechanism.

### Q3.5 Bearing on the poisoning hypothesis

- **Confirms** the hypothesis along a narrower channel than originally
  stated: the cache gets poisoned not because failed extractions are
  recorded, but because parse-failure / truncation / off-template AI
  responses are silently treated as "extracted 0 criteria" and their
  hashes ARE recorded.
- The exception / timeout path was *not* the culprit.

---

## Q4 — What "0 criteria" looks like on disk

### Q4.1 Code path

- `CriteriaFileWriter.WriteAsync(filePath, criteria, sourceDoc, docHash,
  ct)` (`src/Spectra.Core/Parsing/CriteriaFileWriter.cs:20–46`) accepts
  an `IReadOnlyList<AcceptanceCriterion>` of any length (including zero)
  and writes the file unconditionally. The body is:
  ```csharp
  var doc = new CriteriaDocument { Criteria = criteria.ToList() };
  var yaml = Serializer.Serialize(doc);
  // header lines include "# Doc hash: …"
  await File.WriteAllTextAsync(tempPath, header + yaml, ct);
  File.Move(tempPath, filePath, overwrite: true);
  ```
  There is no `if (criteria.Count == 0) return;` guard.
- The master index entry is always populated when extraction returns
  (lines 552–569 in `AnalyzeHandler.cs`):
  - `CriteriaCount = extracted.Count` (which is `0` for the failure
    cases of Q3.2)
  - `DocHash = docHash`
  - `LastExtracted = DateTime.UtcNow`
  - `File = criteriaFileName`

### Q4.2 Observed behavior

- Zero-criteria extraction writes a per-doc file of the form:
  ```yaml
  # Extracted from: docs/whatever.md
  # Doc hash: <64-char hex>
  # Generated at: <ISO timestamp>
  criteria: []
  ```
  …and an index entry with `criteria_count: 0`, the SHA-256 hash, and a
  `last_extracted` timestamp.
- A user can distinguish "extraction never ran" from "extraction ran and
  produced nothing" by checking the master index: a missing
  `CriteriaSource` for a doc path means it has never been processed (or
  was processed but failed-and-skipped); a present entry with
  `criteria_count: 0` means extraction ran and returned `[]` (which Q3
  showed can be either a real empty result OR a silent parse failure).
- The `.criteria.yaml` per-doc files are written even for empty
  extractions. Their presence is therefore not a useful "real signal"
  marker either.

### Q4.3 Bearing on the poisoning hypothesis

- **Confirms** that the on-disk state is observably the same for "AI
  reported nothing testable" and "AI reply was garbled and parsed to
  `[]`". Both leave: `criteria_count: 0`, a doc_hash, a last_extracted
  timestamp, and a `criteria: []` per-doc file. Nothing on disk tells
  the user (or a future spectra run) that one of these is a legitimate
  result and the other is a silent failure.

---

## Q5 — Scale-specific behavior

### Q5.1 Token budget

- `ai.analysis.max_prompt_tokens` (default 96,000, declared in
  `src/Spectra.Core/Models/Config/AnalysisConfig.cs:22`) is enforced
  **only on the generation/analysis pre-flight path**, not on
  extraction. The pre-flight lives in
  `src/Spectra.CLI/Agent/Copilot/AnalyzerInputBuilder.cs:141–160`
  (`EnforcePreflight` → `PreFlightBudgetExceededException`).
- `AnalyzeHandler.RunExtractCriteriaAsync` does not invoke
  `EnforcePreflight` or any other budget check. Verified by reading
  the entire method (`AnalyzeHandler.cs:260–710`).
- `CriteriaExtractor.BuildExtractionPrompt`
  (`CriteriaExtractor.cs:144–187`) inlines the **full document content**
  via `{{content}}`. No truncation.

### Q5.2 Max docs per run

- **NOT FOUND.** The loop at `AnalyzeHandler.cs:406` is a plain
  `foreach (var doc in documentMap.Documents)` with no cap.

### Q5.3 Truncation

- **Not applied on the extraction path.** Compare:
  - Generation: `AnalyzerInputBuilder.PerDocContentCharCap = 2000`
    (`src/Spectra.CLI/Agent/Copilot/AnalyzerInputBuilder.cs:27`).
  - Generation's from-description side: `SourceDocumentLoader`
    `maxContentLengthPerDoc: 8000` (`GenerateHandler.cs:1776`).
  - Extraction: passes raw `content` to the AI as-is.

### Q5.4 Summary mode

- **NOT FOUND** for extraction. The 500-test threshold mentioned in
  project memory lives in `CoverageContextFormatter.SummaryModeThreshold`
  (`src/Spectra.CLI/Agent/Analysis/CoverageContextFormatter.cs:12`) and
  only affects coverage-context injection for behavior-analysis prompts.
  Extraction does not use coverage context.

### Q5.5 Timeouts

Two layered timeouts on the extraction path:

- `AnalyzeHandler.cs:467` — per-document deadline:
  ```csharp
  var deadlineTask = Task.Delay(TimeSpan.FromMinutes(2), ct);
  ```
- `CriteriaExtractor.cs:81` — Copilot session `SendAndWaitAsync`
  timeout: `TimeSpan.FromMinutes(2)`.

The `docs index` path has its own, much tighter deadline:
`DocsIndexHandler.cs:289` — **60-second deadline for the entire
corpus**, not per-doc.

### Q5.6 Concurrency / batching

- The per-doc loop at `AnalyzeHandler.cs:406` is fully sequential. No
  `Task.WhenAll`, `Parallel.ForEach`, or batching. With N documents,
  worst-case wall-clock is ~`N × 2 minutes`.

### Q5.7 Rate-limit handling

- Any provider exception falls through to the generic
  `catch (Exception ex) when (ex is not OperationCanceledException)`
  at `AnalyzeHandler.cs:481`. No retry, no exponential backoff. The
  document is logged-and-skipped exactly like any other failure
  (Q3.4).

### Q5.8 Bearing on the poisoning hypothesis

- **Confirms** that scale-only behaviors plausibly drive the AI into
  the parse-failure mode that triggers poisoning:
  - No content truncation → on a big project, individual docs can be
    large enough that the AI's reply gets truncated or includes a
    safety apology / preamble that breaks the strict-JSON contract.
    `ParseResponse` silently returns `[]`.
  - 2-minute per-doc deadline → close-to-deadline replies may be
    partial JSON. Again parsed as `[]`.
- These do not, by themselves, poison the cache — Q2 + Q3 are the
  mechanism. Scale is the *forcing function* that exercises the
  mechanism.

---

## Verdict on the hypothesis

**CONFIRMED, with a narrowed mechanism.**

The cache is *not* poisoned when extraction throws or times out — those
paths `continue` before the hash is written (`AnalyzeHandler.cs:425–429,
470–477, 481–491`). To that extent the original hypothesis is overstated.

The cache *is* poisoned when the AI returns a syntactically successful
response that `CriteriaExtractor.ParseResponse`
(`CriteriaExtractor.cs:209–248`) reduces to `[]`. There are five
distinct failure modes that produce that outcome — including the catch-
all `catch { return []; }` at lines 244–247 — and they are
indistinguishable to the caller from "AI legitimately found nothing".
The caller then writes the document's hash to
`_criteria_index.yaml` (`AnalyzeHandler.cs:552–569` → `:614`), so the
next non-`--force` run skips the document at `AnalyzeHandler.cs:437`.

**Single strongest piece of evidence:**
`CriteriaExtractor.cs:244–247`:
```csharp
catch
{
    return [];
}
```
…combined with `AnalyzeHandler.cs:493` (`criteriaExtracted += extracted.
Count;`) and `:552–569` (hash recorded against an empty `extracted`
list).

At scale, the absence of token-budget enforcement (Q5.1), content
truncation (Q5.3), and rate-limit backoff (Q5.7) all push the AI toward
malformed/truncated replies, increasing the rate at which
`ParseResponse` enters its `return [];` paths.

## Recommended runtime repro to close out residual uncertainty

The above conclusion is based on static reading, but the strength of the
correlation between "large doc → malformed reply → poisoned cache" rests
on assumptions about provider behavior at scale. A definitive repro:

1. Pick a doc large enough that the provider's response gets truncated
   past the closing `]`.
2. Run `spectra ai analyze --extract-criteria` against a corpus
   containing it.
3. Open `docs/criteria/_criteria_index.yaml`. Confirm the doc has an
   entry with `criteria_count: 0` and a populated `doc_hash`.
4. Open the per-doc `.criteria.yaml`. Confirm it contains `criteria: []`
   and a `# Doc hash:` matching the entry.
5. Re-run `spectra ai analyze --extract-criteria` (no `--force`).
   Confirm the doc is counted in `documentsSkipped` (visible in the
   handler's summary output) and the AI is not called for it.
6. Run with `--force`. Confirm the doc is re-attempted (and either
   succeeds, given a smaller doc, or repeats the failure).

A complementary test for the `docs index` 60-second deadline: time a
real run of `spectra docs index` on the large project and watch for the
`"Acceptance criteria extraction timed out…"` warning at
`DocsIndexHandler.cs:294`.

---

## Summary table

| Question | Finding | Confidence | Bearing on hypothesis |
|---|---|---|---|
| Q1 | `docs index` and `ai analyze --extract-criteria` use *different* extractors. The hash cache lives only in the latter. `docs index` enforces a 60-second deadline for the *entire corpus* (`DocsIndexHandler.cs:289`) — at scale it silently returns `(0, null)` and tells the user to run the analyze command separately | High | Doesn't directly confirm/refute; locates which path owns the cache |
| Q2 | Hash is written after the AI call returns successfully (lines 549/552/561 of `AnalyzeHandler.cs`), in-memory per-doc and persisted once to disk at line 614. Exception/timeout paths `continue` before the write — those are safe. A non-throwing AI call that yields `[]` causes the hash to be recorded | High | Confirms the mechanism, with the empty-result path being the only poisoning channel |
| Q3 | `CriteriaExtractor.ExtractFromDocumentAsync` returns `[]` for at least five structurally different reasons (empty input, empty response, no JSON delimiters, deserialize-null, any exception in parsing — `CriteriaExtractor.cs:58, 92, 216, 226, 244–247`). The caller cannot distinguish them from a real empty extraction | High | Confirms the poisoning mechanism — silent parse failures masquerade as success |
| Q4 | Zero-criteria extraction writes an empty `.criteria.yaml` (no guard in `CriteriaFileWriter.cs:20–46`) AND a master-index entry with `criteria_count: 0`, a `doc_hash`, and a `last_extracted` timestamp. On-disk state is identical for "real empty" and "silent parse failure" | High | Confirms the on-disk indistinguishability that lets poisoning persist undetected |
| Q5 | Extraction path has: NO token budget check; NO content truncation; NO max-docs cap; NO summary mode; NO concurrency; NO rate-limit retry. 2-minute per-doc deadline + 60-second `docs index` total deadline | High | Doesn't poison directly, but at scale forces the AI into the malformed-reply path that does |

---

*Document end. Observation only — no fixes proposed inside this document.*
