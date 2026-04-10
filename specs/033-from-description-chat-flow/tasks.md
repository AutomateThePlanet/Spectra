# Tasks: From-Description Chat Flow & Doc-Aware Manual Tests

**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Branch**: `033-from-description-chat-flow`

Tasks are listed in dependency order. Each task includes the file(s) touched, the user story it serves, and acceptance bullets.

---

## Phase 1: SKILL & Agent content (Story 3 — P2)

### T001. Add "create a specific test case" section to spectra-generate SKILL
- **File**: `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- **Story**: US3 (FR-001, FR-003, FR-004)
- **Acceptance**:
  - New `## When the user wants to create a specific test case` section exists.
  - Numbered Step 1..5 (open progress page, runInTerminal, awaitTerminal, readFile, present).
  - Command line includes `--no-interaction --output-format json --verbosity quiet`.
  - Section explicitly states "Do NOT run analysis", "Do NOT ask how many tests", "always 1 test".

### T002. Add intent routing table to spectra-generate SKILL
- **File**: `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md`
- **Story**: US3 (FR-002)
- **Depends**: T001
- **Acceptance**:
  - `## How to choose between generation flows` section with markdown table.
  - Table headers: `User intent | Signal | Flow`.
  - Three rows: explore area → main flow with `--focus`; specific test → from-description; from suggestions → `--from-suggestions`.
  - Includes "Key rule" line about test-title-shaped vs topic-shaped requests.

### T003. Add Test Creation Intent Routing section to spectra-generation agent
- **File**: `src/Spectra.CLI/Skills/Content/Agents/spectra-generation.agent.md`
- **Story**: US1 (FR-005, FR-006, FR-007)
- **Acceptance**:
  - New `## Test Creation Intent Routing` section.
  - Three intents documented (explore area → `--focus`; specific test → `--from-description`; from suggestions → `--from-suggestions`) with example phrases each.
  - Ambiguous-intent rule explicit: do NOT ask about count or scope.
  - At least one example phrase like "Add a test for double-click submit creating duplicate orders".

### T004. Update existing skill content tests for new content
- **File**: `tests/Spectra.CLI.Tests/...` (search-and-update any existing assertions on `SkillContent.Generate` / agent content)
- **Story**: US3
- **Depends**: T001, T002, T003
- **Acceptance**:
  - `dotnet test` passes against the modified SKILL/agent content.
  - No regressions in existing skill manifest / terminology audit tests.

---

## Phase 2: Doc-aware UserDescribedGenerator (Stories 1, 2 — P1)

### T005. Refactor UserDescribedGenerator prompt to a testable static builder
- **File**: `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs`
- **Story**: US2 (FR-011, FR-012)
- **Acceptance**:
  - New `public static string BuildPrompt(string description, string? context, string suite, IReadOnlyCollection<string> existingIds, string? documentContext, string? criteriaContext)` method.
  - When `documentContext` is non-null, prompt contains `## Reference Documentation (for formatting context only)` and the doc context body.
  - When `criteriaContext` is non-null, prompt contains `## Related Acceptance Criteria` and the criteria body.
  - When both are null, prompt is functionally identical to the current inline prompt (regression-safe).
  - Existing `GenerateAsync` calls `BuildPrompt(...)` instead of constructing the string inline.

### T006. Add optional context parameters to GenerateAsync
- **File**: `src/Spectra.CLI/Commands/Generate/UserDescribedGenerator.cs`
- **Story**: US2 (FR-010, FR-013, FR-015)
- **Depends**: T005
- **Acceptance**:
  - `GenerateAsync` signature gains `string? documentContext = null`, `string? criteriaContext = null`, `IReadOnlyList<string>? sourceRefPaths = null`.
  - When `sourceRefPaths` is non-null and non-empty, the returned `TestCase.SourceRefs` is set from those paths (instead of `[]`).
  - The returned `TestCase.Criteria` reflects whatever the AI populated (no override, no clearing).
  - `grounding.verdict` remains `VerificationVerdict.Manual` unconditionally.
  - Default-arg call sites (no params passed) produce identical behavior to today.

### T007. Wire doc + criteria context into ExecuteFromDescriptionAsync
- **File**: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`
- **Story**: US2 (FR-008, FR-009, FR-010, FR-016, FR-017)
- **Depends**: T006
- **Acceptance**:
  - Before calling `generator.GenerateAsync`, the handler best-effort loads docs via `SourceDocumentLoader` (with `maxContentLengthPerDoc: 8000`), filtered by suite name match against doc filename / title / sections, capped at 3 docs.
  - Best-effort calls `LoadCriteriaContextAsync(currentDir, suite, config, ct)` to get criteria context.
  - Both load operations are wrapped in try/catch — exceptions are swallowed (best-effort, non-blocking).
  - Loaded `docContext`, `criteriaContext`, and matching doc paths are passed to `generator.GenerateAsync`.
  - When neither doc nor criteria exists, behavior matches the current implementation exactly.
  - Existing JSON result shape (`GenerateResult`) is unchanged.

### T008. Add unit tests for UserDescribedGenerator.BuildPrompt
- **File**: `tests/Spectra.CLI.Tests/Commands/Generate/UserDescribedGeneratorTests.cs` (NEW)
- **Story**: US2
- **Depends**: T005
- **Acceptance** — at least these tests:
  - `BuildPrompt_WithoutContext_DoesNotIncludeReferenceSection`
  - `BuildPrompt_WithDocContext_IncludesReferenceDocumentationHeader`
  - `BuildPrompt_WithCriteriaContext_IncludesAcceptanceCriteriaHeader`
  - `BuildPrompt_WithBothContexts_IncludesBoth`
  - `BuildPrompt_WithDocContext_StatesUserDescriptionIsSourceOfTruth`
  - `BuildPrompt_AlwaysIncludesUserDescription`
  - `BuildPrompt_AlwaysAvoidsExistingIds`

### T009. Add SKILL/agent content tests
- **File**: `tests/Spectra.CLI.Tests/Skills/GenerateSkillContentTests.cs` (NEW)
- **Story**: US1, US3
- **Depends**: T001, T002, T003
- **Acceptance** — at least these tests:
  - `GenerateSkill_HasFromDescriptionSection` — `SkillContent.Generate.Contains("create a specific test case")`.
  - `GenerateSkill_HasIntentRoutingTable` — Generate content contains a row referencing both `--focus` and `--from-description`.
  - `GenerateSkill_FromDescriptionUsesCorrectFlags` — `--from-description` line includes `--no-interaction`, `--output-format json`, `--verbosity quiet`.
  - `GenerateSkill_FromDescriptionStatesNoAnalysisNoCount` — content asserts "Do NOT run analysis" / "Do NOT ask".
  - `GenerationAgent_HasIntentRoutingSection` — agent content contains "Test Creation Intent Routing".
  - `GenerationAgent_RoutesToFromDescriptionForSpecificTest` — agent content references `--from-description` and an example like "Add a test for".
  - `GenerationAgent_RoutesToFocusForExploreArea` — agent content references `--focus` and an example like "Generate tests for".
  - `GenerationAgent_RoutingForbidsCountQuestions` — agent content contains "do NOT ask" referencing count/scope.

---

## Phase 3: Build & verify

### T010. Build solution
- **Command**: `dotnet build`
- **Depends**: T001..T009
- **Acceptance**: clean build, zero new warnings.

### T011. Run all tests
- **Command**: `dotnet test`
- **Depends**: T010
- **Acceptance**: full suite passes (1434+ → 1444+ tests).

---

## Phase 4: Documentation

### T012. Update CLAUDE.md
- **File**: `CLAUDE.md`
- **Acceptance**: Recent Changes entry for spec 033 added at the top of the list.

### T013. Update PROJECT-KNOWLEDGE.md
- **File**: `PROJECT-KNOWLEDGE.md` (if present)
- **Acceptance**: implemented-features list updated; SKILL section mentions from-description flow.

### T014. Update user-facing docs
- **Files** (if present): `README.md`, `docs/getting-started.md`, `docs/cli-reference.md`, `docs/skills-integration.md`, `docs/test-format.md`, `docs/cli-vs-chat-generation.md`, `docs/coverage.md`
- **Acceptance**: each file gains a brief mention of the from-description flow (or doc/criteria context for manual tests, where relevant).

---

## Story-to-Task Mapping

| Story | Tasks |
|-------|-------|
| US1 (P1) - Specific test creation from chat | T003, T009 |
| US2 (P1) - Doc-aware manual tests | T005, T006, T007, T008 |
| US3 (P2) - Discoverable SKILL flow | T001, T002, T004, T009 |

## MVP Slice

**US1 alone** (T003 + T009) ships a usable improvement: chat agents will route correctly even before the CLI loads doc context. **US2** (T005..T008) is the deeper enhancement. **US3** (T001, T002) is documentation polish but should ship together with US1 to keep SKILL and agent in sync.
