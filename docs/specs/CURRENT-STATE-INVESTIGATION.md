# SPECTRA — Current-State Investigation

**Status:** Observation only. No fixes, no refactors, no proposals.
**Scope:** AutomateThePlanet/Spectra (`main`), AutomateThePlanet/Spectra_Demo.
**Method:** Static reading. Every claim cites `path:method` or `path:line`.
Where static reading cannot conclude, the section is marked
**INCONCLUSIVE — needs runtime repro**.

---

## Area A — Criteria loading during generation (from-description path)

### A.1 Code path — Batch flow (`spectra ai generate --suite X --focus "..."`)

1. `src/Spectra.CLI/Commands/Generate/GenerateCommand.cs` parses CLI args.
2. `GenerateHandler.ExecuteAsync` → `GenerateHandler.ExecuteCoreAsync` →
   `GenerateHandler.ExecuteDirectModeAsync`
   (`src/Spectra.CLI/Commands/Generate/GenerateHandler.cs:233`).
3. `ExecuteDirectModeAsync` calls
   `LoadCriteriaContextAsync(currentDir, suite, config, ct)` at
   `GenerateHandler.cs:672`. That helper reads
   `docs/criteria/*.criteria.yaml` via
   `Spectra.Core/Parsing/CriteriaFileReader.cs::ReadAsync`
   (recursive scan, `SearchOption.AllDirectories`).
4. Component-match filter for the active suite applied at
   `GenerateHandler.cs:2452–2486` (exact, then partial; last-resort returns
   all criteria when no match).
5. Per-batch generation call at `GenerateHandler.cs:758` passes the loaded
   string through:
   ```csharp
   batchResult = await agent.GenerateTestsAsync(
       prompt, documents, mutableExistingTests, batchRequestCount,
       criteriaContext: criteriaContext,            // <-- forwarded
       testimizeData: testimizeDataset, ct: ct);
   ```
6. Inside `GenerationAgent.BuildFullPrompt`
   (`src/Spectra.CLI/Agent/Copilot/GenerationAgent.cs:448`), placeholder
   `{{acceptance_criteria}}` in the `test-generation` template is resolved
   to `criteriaContext` at line 479 via the
   `Spectra.CLI/Prompts/PlaceholderResolver`.
7. `GenerationAgent.cs:527` emits a conditional block to the AI:
   ```
   ## ACCEPTANCE CRITERIA — MANDATORY
   You MUST map each test case to matching acceptance criteria below
   …
   ```
   The block is included **only when `criteriaContext` is non-empty**.
8. After the AI returns, batch tests are serialized by
   `TestFileWriter.WriteAsync`
   (`src/Spectra.CLI/IO/TestFileWriter.cs`); `criteria:` is written from
   `test.Criteria` (populated by the model from the JSON response).
9. `Grounding.Verdict` is set by the critic loop
   (`src/Spectra.CLI/Agent/Critic/CriticPromptBuilder.cs` +
   `GenerateHandler.cs` ~727–1600) to one of `Grounded` / `Partial` /
   `Hallucinated`
   (`src/Spectra.Core/Models/Grounding/VerificationVerdict.cs:12,18,24`).

### A.2 Code path — From-description flow (`--from-description "..."`)

1. Same `GenerateCommand` entry, dispatched to
   `GenerateHandler.ExecuteFromDescriptionAsync`
   (`GenerateHandler.cs:1744`).
2. Criteria **are loaded** at `GenerateHandler.cs:1793`:
   ```csharp
   criteriaContext = await LoadCriteriaContextAsync(
       currentDir, suite, config, ct);
   ```
   Wrapped in a try/catch that swallows failures ("best-effort").
3. The loaded string is forwarded into
   `UserDescribedGenerator.GenerateAsync(... criteriaContext: criteriaContext, ...)`
   at `GenerateHandler.cs:1800`.
4. `UserDescribedGenerator.GenerateAsync`
   (`src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs:82`)
   embeds the criteria string into the user-prompt **body** via
   `BuildPrompt` (lines 61–73 of the same file):
   ```csharp
   if (!string.IsNullOrWhiteSpace(criteriaContext))
   {
       prompt += $"""
           ## Related Acceptance Criteria
           …
           {criteriaContext}
           """;
   }
   ```
5. **Critical observation:** the call into the AI at
   `UserDescribedGenerator.cs:113–120` passes `null` for the
   `criteriaContext` parameter:
   ```csharp
   var result = await agent.GenerateTestsAsync(
       prompt,
       [],
       [],
       1,
       criteriaContext: null,            // <-- not the loaded value
       testimizeData: null,
       ct: ct);
   ```
   Because the MANDATORY block in `GenerationAgent.cs:527` is gated on
   the parameter (not the prompt body), the AI receives only the loose
   `## Related Acceptance Criteria` section the generator wrote — without
   the "You MUST map each test case…" instruction.
6. `TestCase.Criteria` is whatever the AI returns; the file is serialized
   by the same `TestFileWriter.WriteAsync` used by the batch flow
   (`GenerateHandler.cs:1840`).
7. `Grounding.Verdict` is **hard-coded** to `VerificationVerdict.Manual`
   at `UserDescribedGenerator.cs:149`:
   ```csharp
   Grounding = new GroundingMetadata
   {
       Verdict = VerificationVerdict.Manual,
       Score = 1.0,
       Generator = …,
       Critic = "user-described",
       VerifiedAt = DateTimeOffset.UtcNow
   }
   ```
   No critic runs for this flow.

### A.3 Side-by-side: Batch vs From-Description

| Step | Batch flow | From-description flow |
|---|---|---|
| Entry handler | `GenerateHandler.ExecuteDirectModeAsync` (`GenerateHandler.cs:233`) | `GenerateHandler.ExecuteFromDescriptionAsync` (`GenerateHandler.cs:1744`) |
| Criteria load called? | **Yes** — `LoadCriteriaContextAsync` at `GenerateHandler.cs:672` | **Yes** — `LoadCriteriaContextAsync` at `GenerateHandler.cs:1793` |
| Loader implementation | `CriteriaFileReader.ReadAsync` (recursive `docs/criteria/*.criteria.yaml`) | Same |
| Component filter | `GenerateHandler.cs:2452–2486` | Same helper |
| Prompt template | `test-generation` template via `GenerationAgent.BuildFullPrompt` (`GenerationAgent.cs:448`) | Hand-built string in `UserDescribedGenerator.BuildPrompt` (`UserDescribedGenerator.cs:18–76`) — no template engine |
| `{{acceptance_criteria}}` placeholder resolved? | **Yes** — `GenerationAgent.cs:479` via `PlaceholderResolver` | N/A (no template) |
| MANDATORY criteria block emitted to AI? | **Yes** — `GenerationAgent.cs:527`, conditional on `criteriaContext` parameter being non-empty | **No** — call at `UserDescribedGenerator.cs:118` passes `criteriaContext: null`, so the conditional block is skipped |
| Loose `## Related Acceptance Criteria` block in prompt body? | No (uses the MANDATORY block) | **Yes** — `UserDescribedGenerator.cs:61–73` writes the loaded criteria into the prompt text, but without the "You MUST map…" instruction |
| `criteria:` frontmatter written? | Yes, from AI's `criteria` JSON field, via `TestFileWriter.WriteAsync` | Yes, same writer — but field is populated only if the AI volunteers it from the loose section |
| `grounding.verdict` source | Critic loop → `Grounded` / `Partial` / `Hallucinated` (`VerificationVerdict.cs:12,18,24`) | Hard-coded to `Manual` (`UserDescribedGenerator.cs:149`) |

### A.4 Observed behavior

- Batch flow injects the loaded criteria into the AI prompt as a MANDATORY
  mapping instruction. The model is then asked to populate `criteria:` per
  test, and `TestFileWriter` writes the resulting IDs unconditionally
  (`TestFileWriter.WriteAsync` always emits `criteria:` array, even if
  empty).
- From-description flow loads criteria, embeds them into the user-facing
  prompt body, but then passes `null` for the SDK-level `criteriaContext`
  parameter. The downstream MANDATORY block is therefore skipped. The AI
  may or may not include criteria IDs from the loose section — there is
  no explicit instruction telling it to.

### A.5 Confirmed root cause (Area A)

The from-description flow's failure mode is **(2) load criteria but fail to
inject them as an actionable instruction to the AI**. Criteria *are*
loaded and *are* present in the prompt text, but the MANDATORY mapping
block gated on `criteriaContext` (`GenerationAgent.cs:527`) is bypassed
because the call site at `UserDescribedGenerator.cs:118` passes `null`.

- Evidence: `UserDescribedGenerator.cs:113–120` (literal `criteriaContext: null`).
- Evidence: `GenerationAgent.cs:527` (block emitted only when parameter is
  non-empty).

Verdict = `manual` is independently hard-coded at
`UserDescribedGenerator.cs:149`; it is not a consequence of the criteria
issue and would still be `manual` even if criteria injection were fixed.

### A.6 Evidence gaps (Area A)

- The exact text the model receives in the from-description flow can be
  read directly from `UserDescribedGenerator.BuildPrompt`, but a runtime
  log of an actual outbound prompt (e.g. via Copilot SDK transcript)
  would close the loop.
- Whether the model spontaneously maps criteria when only the loose
  section is present is empirically uncertain without a test run on a
  real suite.

---

## Area B — Test writing & indexing (why from-description tests differ)

### B.1 Code path — File serializer

- Both flows call **the same** writer:
  `Spectra.CLI/IO/TestFileWriter.WriteAsync(path, testCase, ct)`.
  - Batch: `GenerateHandler.cs:872`.
  - From-description: `GenerateHandler.cs:1840`.
- `TestFileWriter.FormatTestCase` (`TestFileWriter.cs:33–170`) emits YAML
  frontmatter + Markdown body. Unconditional frontmatter fields:
  `id`, `priority`, `criteria` (`[]` if empty).
- Optional frontmatter fields written when present: `tags`, `component`,
  `depends_on`, `source_refs`, `scenario_from_doc`, `estimated_duration`,
  `status`, `orphaned_reason`, `orphaned_date`, `grounding`
  (`verdict`, `score`, `generator`, `critic`, `verified_at`,
  `unverified_claims`).
- Both flows write to `test-cases/{suite}/{id}.md` via
  `TestFileWriter.GetFilePath(testsPath, suite, id)`
  (`TestFileWriter.cs:175`); no temp or alternate destinations.

### B.2 Code path — `_index.json` writer

- Batch flow (`ExecuteDirectModeAsync`) updates the index **per batch** at
  `GenerateHandler.cs:882–888`:
  ```csharp
  var indexGenerator = new IndexGenerator();
  var batchIndex = indexGenerator.Generate(suite, allTestsForIndex);
  var indexWriter = new IndexWriter();
  await indexWriter.WriteAsync(
      Path.Combine(suitePath, "_index.json"), batchIndex, ct);
  ```
- From-description flow (`ExecuteFromDescriptionAsync`,
  `GenerateHandler.cs:1744–1870`) **never** instantiates `IndexGenerator`
  or `IndexWriter`. Verified by reading the entire method end-to-end:
  the only post-write actions are `_progress.Success(...)` (line 1841)
  and a `SessionStore.SaveAsync` call (lines 1843–1850) that records the
  new test ID into `UserDescribed` for session continuity.

### B.3 Frontmatter shape — written file vs index entry

Written `.md` frontmatter (both flows, via `TestFileWriter`):
`id`, `title`, `priority`, `tags`, `component`, `criteria`,
`depends_on`, `source_refs`, `estimated_duration`,
`scenario_from_doc`, `status`, `orphaned_reason`, `orphaned_date`,
`grounding{…}`.

`_index.json` entry — `Spectra.Core/Models/TestIndexEntry`
(`TestIndexEntry.cs:8–71`) fields:
`id`, `file`, `title`, `description`, `priority`, `tags`, `component`,
`estimated_duration`, `depends_on`, `source_refs`, `redundant_of`,
`redundant_reason`, `automated_by`, `requirements`, `criteria`, `bugs`.

Fields populated by `IndexGenerator.CreateEntry` (read directly:
`src/Spectra.Core/Index/IndexGenerator.cs:33–60`). Notably:
- `priority` is serialized as
  `testCase.Priority.ToString().ToLowerInvariant()` (string, not enum).
- `grounding{}` is in the file frontmatter but **not** copied into the
  index entry — `IndexGenerator` does not read it.

Between the two flows, the file frontmatter shape is identical (same
writer). The shape difference doesn't matter for indexability, because
the from-description path never invokes the index writer at all.

### B.4 Code path — MCP executor discovery

- `src/Spectra.MCP/Program.cs:53–59` registers the index loader used by
  every MCP tool that needs to find tests:
  ```csharp
  Func<string, IEnumerable<TestIndexEntry>> indexLoader = suite =>
  {
      var indexPath = Path.Combine(
          basePath, "test-cases", suite, "_index.json");
      if (!File.Exists(indexPath)) return [];
      var index = indexWriter.ReadAsync(indexPath).GetAwaiter().GetResult();
      return index?.Tests ?? [];
  };
  ```
- `src/Spectra.MCP/Program.cs:104–112` registers the suite-list loader
  used by tools that enumerate suites; it filters to suites that have an
  `_index.json` present:
  ```csharp
  return Directory.GetDirectories(testsDir)
      .Select(Path.GetFileName)
      .Where(name => name is not null &&
          File.Exists(Path.Combine(
              basePath, "test-cases", name, "_index.json")))
      .Cast<string>();
  ```
- `StartExecutionRunTool.ExecuteWithSuiteAsync` at
  `src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs:130`
  reads `_indexLoader(request.Suite)` exclusively; it does not scan
  `.md` files for run candidates.

### B.5 Observed behavior

- A from-description test materializes on disk as
  `test-cases/{suite}/{id}.md` with full frontmatter (including
  `grounding.verdict: manual`) and gets recorded in the session store.
- The same test is **absent** from `test-cases/{suite}/_index.json`
  because no index writer call was made by the orchestrator.
- The MCP executor and every MCP data tool route discovery through the
  index. A missing entry means the test is invisible to
  `find_test_cases`, `start_execution_run`, `list_available_suites`,
  saved-selection counts, etc.

### B.6 Confirmed root cause (Area B)

**(a) The orchestrator never calls the index writer for the
from-description flow.**

- Evidence: `GenerateHandler.cs:1744–1870` — no `IndexGenerator` or
  `IndexWriter` reference anywhere in the method.
- Evidence: the same method in the batch path
  (`GenerateHandler.cs:882–888`) shows the call pattern that is absent
  here.
- Evidence: `Program.cs:53–59` and `Program.cs:104–112` confirm the
  executor only consults `_index.json`.

Not (b) — both flows write to the same folder. Not (c) — both flows use
the same `TestFileWriter`, so frontmatter shape is identical.

### B.7 Sample evidence

`tests/TestFixtures/tests/auth/_index.json` (real fixture):

```json
{
  "id": "TC-201",
  "title": "Login with valid credentials",
  "priority": "high",
  "file": "auth/TC-201.md",
  "tags": ["smoke", "auth"],
  "source_refs": ["docs/features/auth/user-authentication.md"]
}
```

Priority is a **lowercase string**, matching
`IndexGenerator.cs:41`'s `.ToLowerInvariant()`.

### B.8 Evidence gaps (Area B)

- None for the indexing claim — the absence is verifiable from the
  source. A runtime end-to-end repro (generate one from-description
  test, then call `find_test_cases`) would only re-confirm the static
  reading.

---

## Area C — Filtering & smart selection (priority filter)

### C.1 Code path — `find_test_cases`

- Tool class: `src/Spectra.MCP/Tools/Data/FindTestCasesTool.cs`.
- Source of priority/tags/component values:
  `_indexLoader(suite)` at `FindTestCasesTool.cs:68` returns
  `IEnumerable<TestIndexEntry>` from `_index.json`. Filters read
  `t.Entry.Priority`, `t.Entry.Tags`, `t.Entry.Component`.
- Priority comparison (`FindTestCasesTool.cs:88–92`):
  ```csharp
  if (request?.Priorities is { Count: > 0 })
  {
      var priorities = new HashSet<string>(
          request.Priorities, StringComparer.OrdinalIgnoreCase);
      filtered = filtered.Where(t => priorities.Contains(t.Entry.Priority));
  }
  ```
  When a `HashSet<string>` is constructed with `StringComparer.
  OrdinalIgnoreCase`, the comparer is retained and used for
  `Contains`/`Add`/`Remove`. So `Contains("high")`, `Contains("High")`,
  and `Contains("HIGH")` all match an entry whose `Priority == "high"`.
- Tags and components use the same case-insensitive HashSet pattern
  (lines 94–98 and 100–104).

### C.2 Code path — Saved selection evaluator

- Shared filter: `ListSavedSelectionsTool.ApplyFilters`
  (`src/Spectra.MCP/Tools/Data/ListSavedSelectionsTool.cs:91–121`):
  ```csharp
  internal static List<TestIndexEntry> ApplyFilters(
      List<TestIndexEntry> tests, SavedSelectionConfig config)
  {
      var filtered = tests.AsEnumerable();

      if (config.Priorities is { Count: > 0 })
      {
          var priorities = new HashSet<string>(
              config.Priorities, StringComparer.OrdinalIgnoreCase);
          filtered = filtered.Where(t => priorities.Contains(t.Priority));
      }
      …
  }
  ```
- Priority comes from `TestIndexEntry.Priority` (the index entry, not the
  `.md` file).
- Comparison is **case-insensitive** because of the constructor comparer.
  This contradicts a common first guess; verified by reading the code
  directly.
- Selections themselves are loaded from `spectra.config.json` via the
  `_selectionsLoader` Func registered in `Program.cs`. A typical
  fixture entry (`tests/Spectra.MCP.Tests/Integration/SmartSelectionFlowTests.cs:65–69`):
  ```csharp
  ["smoke"] = new() { Description = "Smoke tests",
                      Priorities = ["high"], Tags = ["smoke"] }
  ```

### C.3 Code path — `start_execution_run` with `selection` mode

- `StartExecutionRunTool.ExecuteWithSelectionAsync`
  (`src/Spectra.MCP/Tools/RunManagement/StartExecutionRunTool.cs:209–250`):
  1. Resolve the selection name against `_selectionsLoader()`. If
     missing, return `SELECTION_NOT_FOUND`.
  2. Build `allTests` by concatenating `_indexLoader(suite)` across
     every suite returned by `_suiteListLoader()`.
  3. Apply `ListSavedSelectionsTool.ApplyFilters(allTests, selectionConfig)`.
  4. If `matched.Count == 0`, return `NO_TESTS_MATCHED`.
  5. Otherwise call `_engine.StartRunAsync($"selection:{name}",
     matched, request.Environment)`.
- The selection path never touches `.md` files directly; it operates on
  `TestIndexEntry` records.

### C.4 Index entry shape — priority field

- `src/Spectra.Core/Models/TestIndexEntry.cs:23–24`:
  ```csharp
  [JsonPropertyName("priority")]
  public required string Priority { get; init; }
  ```
- Populated by `IndexGenerator.CreateEntry`
  (`src/Spectra.Core/Index/IndexGenerator.cs:41`):
  ```csharp
  Priority = testCase.Priority.ToString().ToLowerInvariant()
  ```
  `TestCase.Priority` is an enum (`High` / `Medium` / `Low`); the index
  always serializes lowercase.

### C.5 Observed behavior

- For a suite whose `_index.json` exists and contains entries with
  `priority` set, `find_test_cases` with `priorities: ["high"]` (or
  `["High"]`, `["HIGH"]`) matches the high-priority entries correctly.
- Saved selections evaluate against the same index entries through the
  same `ApplyFilters` and exhibit the same case-insensitive matching.
- A suite without `_index.json` is silently skipped by
  `_suiteListLoader()` (`Program.cs:104–112` — filters by file
  presence) and never reaches the filter at all.
- Tests written by the from-description flow are not in `_index.json`
  (Area B), so they cannot match any selection or `find_test_cases`
  query.

### C.6 Confirmed root cause (Area C)

**INCONCLUSIVE from static reading alone — but the most likely cause is
(c): the filter is reading from a source (`_index.json`) that is not
populated with the user's expected tests.**

Three sub-cases that are *not* the cause, with evidence:

- *Not* case-mismatch — `HashSet<string>(_, StringComparer.OrdinalIgnoreCase)`
  is honored by `.Contains()` (`FindTestCasesTool.cs:90–91`,
  `ListSavedSelectionsTool.cs:97–98`).
- *Not* format-mismatch — `IndexGenerator.cs:41` always writes lowercase.
- *Not* a null-priority bug for batch-flow tests — `TestIndexEntry.Priority`
  is `required` (`TestIndexEntry.cs:23`), and `IndexGenerator` always
  derives it from the `TestCase.Priority` enum.

Candidate causes that remain plausible and would need runtime evidence to
distinguish:

1. **Tests written via from-description aren't indexed** (Area B). Any
   user who has been building tests via `--from-description` and then
   tries to filter `priority: high` from a suite would see zero matches.
2. **The suite's `_index.json` is stale or missing** — for example, if a
   user hand-edited `.md` files without running the indexer, or the
   suite was never generated through the batch flow.
3. **The saved selection in `spectra.config.json` has a typo** (e.g.
   `Priorities: ["P1"]` instead of `["high"]`). Filter code is
   case-insensitive but not value-translating: `"p1"` does not match
   `"high"`.

### C.7 Evidence gaps (Area C)

- A copy of the user's real `spectra.config.json` `saved_selections`
  block, to confirm the priority value strings used.
- The actual `_index.json` contents of the affected suite in
  `Spectra_Demo`, to confirm whether high-priority entries exist with
  `priority: "high"`.
- A repro: run `find_test_cases` with `priorities: ["high"]` against
  that suite and capture the response (matched count, warnings array).

---

## Summary

| Area | Symptom | Confirmed root cause | Confidence | Spec it should feed |
|---|---|---|---|---|
| A | "Criteria extraction is not working on generation" (from-description) | Criteria are loaded and inlined in the user prompt, but `UserDescribedGenerator.cs:118` passes `criteriaContext: null` to `GenerationAgent.GenerateTestsAsync`, so the MANDATORY mapping block at `GenerationAgent.cs:527` is skipped | **High** | A future spec to wire the loaded `criteriaContext` through to the agent call in the from-description path, and to decide whether `grounding.verdict` should remain `manual` when criteria *are* now visible to the model |
| B | From-description tests are invisible to the executor and absent from the index | `GenerateHandler.ExecuteFromDescriptionAsync` (`GenerateHandler.cs:1744–1870`) writes the `.md` via `TestFileWriter` but never calls `IndexGenerator`/`IndexWriter`; MCP executor and tools route discovery through `_index.json` only (`Program.cs:53–59`) | **High** | A future spec to ensure every test-writing path updates `_index.json` (or to centralize the writer so individual orchestrators cannot forget) |
| C | "High priority from a suite doesn't work" | INCONCLUSIVE from static reading. The filter code is case-insensitive (`StringComparer.OrdinalIgnoreCase`), the index writes lowercase, and `TestIndexEntry.Priority` is `required`. The most likely real cause is that the affected tests are absent from `_index.json` (Area B), or the suite's index is stale, or the saved-selection priority strings are typoed | **Medium** (filter code verified; missing piece is runtime data on which tests are actually indexed) | Same spec as Area B (since the most plausible root cause for C is a consequence of B); a separate diagnostic spec for "spectra doctor index" / saved-selection validation may also be worth scoping |

---

*Document end. Observation only — no fixes proposed inside this document.*
