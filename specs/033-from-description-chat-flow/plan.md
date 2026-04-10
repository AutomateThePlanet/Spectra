# Implementation Plan: From-Description Chat Flow & Doc-Aware Manual Tests

**Branch**: `033-from-description-chat-flow` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)

## Summary

Three-part feature: (1) update `spectra-generate` SKILL with a dedicated single-test "from-description" flow and an intent routing table, (2) update the `spectra-generation` agent prompt with explicit intent-classification rules, (3) enhance `UserDescribedGenerator` to load relevant docs and acceptance criteria as best-effort formatting context — populating `source_refs` and `criteria` on the resulting test while keeping `grounding.verdict: manual`.

## Technical Context

**Language/Version**: C# 12, .NET 8
**Primary Dependencies**: Spectra.CLI (existing), Spectra.Core (TestCase, GroundingMetadata, AcceptanceCriterion)
**Storage**: File-based — embedded SKILL/agent `.md` resources in `Spectra.CLI`; SHA-256 hashes computed at install time
**Testing**: xUnit (`Spectra.CLI.Tests`)
**Target Platform**: Cross-platform .NET CLI
**Project Type**: CLI (single project)
**Constraints**: Best-effort doc/criteria loading must not block or fail the command. Doc context capped at 3 docs × 8000 chars.
**Scale/Scope**: ~3 source files modified, 1 SKILL md file, 1 agent md file, ~10 new tests, 9 doc files updated.

## Constitution Check

No constitution file. Standard CLAUDE.md guidelines apply: no unnecessary refactors, only test the changed paths, prefer small focused changes.

## Project Structure

### Documentation (this feature)

```text
specs/033-from-description-chat-flow/
├── spec.md
├── plan.md
├── tasks.md
├── checklists/
│   └── requirements.md
└── (no contracts/ — internal CLI feature, no API)
```

### Source Code (touched paths)

```text
src/Spectra.CLI/
├── Commands/Generate/
│   ├── UserDescribedGenerator.cs   # MODIFIED: add documentContext + criteriaContext params, refactor prompt builder for testability
│   └── GenerateHandler.cs          # MODIFIED: ExecuteFromDescriptionAsync loads doc + criteria context, populates source_refs
└── Skills/Content/
    ├── Skills/spectra-generate.md  # MODIFIED: add "create a specific test case" section + routing table
    └── Agents/spectra-generation.agent.md  # MODIFIED: add Test Creation Intent Routing section

tests/Spectra.CLI.Tests/
├── Commands/Generate/
│   └── UserDescribedGeneratorTests.cs  # NEW: prompt-building tests
└── Skills/
    └── GenerateSkillContentTests.cs    # NEW: SKILL/agent content assertions
```

## Phases

### Phase 0 — Research / discovery

No external research needed. All primitives exist:
- `SourceDocumentLoader.LoadAllAsync(basePath, maxDocuments, maxContentLengthPerDoc, ct)` already supports caps.
- `LoadCriteriaContextAsync` (private static in `GenerateHandler`, line 1943) is the criteria primitive — promote to internal/static-helper-callable from the from-description branch.
- `SkillContent` / `AgentContent` already load embedded resources via `SkillResourceLoader`. SHA-256 hashes are computed at install time, not stored — so editing the `.md` resources is sufficient; no manifest table to regenerate.

### Phase 1 — SKILL & agent content (no code changes beyond .md files)

1. Add new section to `Skills/Content/Skills/spectra-generate.md`:
   - Heading: `## When the user wants to create a specific test case`
   - Numbered Step 1..5 sequence (open progress page → runInTerminal → awaitTerminal → readFile → present).
   - Command line: `spectra ai generate --suite {suite} --from-description "{description}" --context "{context}" --no-interaction --output-format json --verbosity quiet`.
   - Explicit "Do NOT run analysis. Do NOT ask how many tests. Always 1 test." line.
   - Routing table mapping intent signal → flow.

2. Add new section to `Skills/Content/Agents/spectra-generation.agent.md`:
   - Heading: `## Test Creation Intent Routing`.
   - Three intent classes (Intent 1: explore area → `--focus`, Intent 2: specific test → `--from-description`, Intent 3: from suggestions → `--from-suggestions`) with examples and actions.
   - Ambiguous-intent rule: topic-vs-scenario; never ask about count.

3. Verify SkillContent/AgentContent dictionaries still resolve (smoke test in build).

### Phase 2 — Doc-aware `--from-description` (CLI code)

1. **`UserDescribedGenerator.cs`** — refactor:
   - Add public `static string BuildPrompt(string description, string? context, string suite, IReadOnlyCollection<string> existingIds, string? documentContext, string? criteriaContext)` method that returns the AI prompt string. This makes prompt construction testable without invoking AI.
   - Add optional parameters `string? documentContext = null`, `string? criteriaContext = null`, and `IReadOnlyList<string>? sourceRefPaths = null` to `GenerateAsync(...)`.
   - When `documentContext` is non-null: insert "## Reference Documentation (for formatting context only)" section in the prompt.
   - When `criteriaContext` is non-null: insert "## Related Acceptance Criteria" section.
   - When `sourceRefPaths` is non-null: populate the returned `TestCase.SourceRefs` from those paths instead of `[]`.
   - Keep AI's `criteria` output (already populated by `agent.GenerateTestsAsync`) flowing into `TestCase.Criteria`.
   - Keep `grounding.verdict = Manual` unconditionally.

2. **`GenerateHandler.cs`** — modify `ExecuteFromDescriptionAsync`:
   - Promote `LoadCriteriaContextAsync` from `private static` to allow reuse, OR call directly (it is already in the same class).
   - After loading config, before calling `generator.GenerateAsync`, perform best-effort load:
     ```csharp
     string? docContext = null;
     IReadOnlyList<string> docPaths = [];
     try
     {
         var loader = new SourceDocumentLoader(config.Source);
         var allDocs = await loader.LoadAllAsync(currentDir, maxDocuments: null, maxContentLengthPerDoc: 8000, ct);
         var matching = allDocs
             .Where(d => MatchesSuite(d, suite))
             .Take(3)
             .ToList();
         if (matching.Count > 0)
         {
             docContext = FormatDocContext(matching);
             docPaths = matching.Select(d => d.Path).ToList();
         }
     }
     catch { /* best-effort */ }

     string? criteriaContext = null;
     try { criteriaContext = await LoadCriteriaContextAsync(currentDir, suite, config, ct); }
     catch { /* best-effort */ }
     ```
   - `MatchesSuite` is a small private helper: case-insensitive contains on `doc.Path` filename or `doc.Title`.
   - `FormatDocContext` produces a delimited string of `## {title}\n{content}\n`.
   - Pass `docContext`, `criteriaContext`, `docPaths` to `generator.GenerateAsync`.

3. **JSON result** — no shape change. `source_refs` and `criteria` are persisted via `TestFileWriter`, which already writes them. No `GenerateResult` schema change needed.

### Phase 3 — Tests

1. **`UserDescribedGeneratorTests`** (new):
   - `BuildPrompt_WithoutContext_DoesNotIncludeRefSection`
   - `BuildPrompt_WithDocContext_IncludesRefDocumentationSection`
   - `BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaSection`
   - `BuildPrompt_WithBothContexts_IncludesBoth`
   - `BuildPrompt_IncludesUserDescriptionAsSourceOfTruth`

2. **`GenerateSkillContentTests`** (new):
   - `GenerateSkill_HasFromDescriptionSection` — asserts `SkillContent.Generate.Contains("create a specific test case")`.
   - `GenerateSkill_HasIntentRoutingTable` — asserts the table headers ("User intent", "Signal", "Flow") all present.
   - `GenerateSkill_FromDescriptionUsesCorrectFlags` — asserts `--from-description` line contains `--no-interaction` and `--output-format json` and `--verbosity quiet`.
   - `GenerationAgent_HasIntentRoutingSection` — asserts agent content contains "Test Creation Intent Routing" + "--from-description" + "--focus".
   - `GenerationAgent_RoutesToFromDescriptionForSpecificTest` — asserts agent content includes the example "Add a test for".
   - `GenerationAgent_DoesNotAskAboutCountInRoutingRules` — asserts the "do NOT ask clarifying questions about count" instruction exists.

3. **Integration tests** — deferred. The from-description path invokes AgentFactory which requires real AI. Coverage of FR-008..FR-014 is via the prompt-building unit tests + manual smoke; no integration test will be added in this spec to keep the test suite isolated from network/AI dependencies.

### Phase 4 — Documentation updates

Update the 9 doc files listed in the spec:
- `CLAUDE.md` — add 033 to Recent Changes.
- `PROJECT-KNOWLEDGE.md` — add 033 implemented entry.
- `README.md` — add "create a specific test" example near Quick Start.
- `docs/getting-started.md` — add from-description example.
- `docs/cli-reference.md` — note `--from-description` doc/criteria context.
- `docs/skills-integration.md` — describe new from-description flow + intent routing.
- `docs/test-format.md` — note `source_refs`/`criteria` may be populated for manual tests.
- `docs/cli-vs-chat-generation.md` — update Dimension 8.
- `docs/coverage.md` — note manual tests can now contribute to coverage.

(If any of these files do not exist, skip that line item — they are optional polish.)

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Doc loading slows from-description noticeably | Cap at 3 docs × 8000 chars; load synchronously inside best-effort try block; no timeout needed since file I/O is bounded. |
| AI emits criteria IDs that don't exist | Acceptable — coverage analyzer will simply not match them. The criteria context tells the AI which IDs are valid, so this should be rare. |
| SKILL .md changes break existing skill content tests | Search existing tests for hardcoded SKILL strings before edit; update them in the same change. |
| Refactoring `BuildPrompt` to static breaks existing call site | The existing `GenerateAsync` will still build the prompt internally (calling `BuildPrompt`), so call sites are unchanged. |
