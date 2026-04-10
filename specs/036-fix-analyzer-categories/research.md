# Phase 0 — Research: Fix BehaviorAnalyzer Category Injection

**Feature**: 036-fix-analyzer-categories
**Date**: 2026-04-10

No NEEDS CLARIFICATION markers in the spec. Research below documents design decisions and corrections to the user-supplied draft.

## D1 — Wire config and template loader through the constructor (not a setter)

**Decision**: Add `SpectraConfig? config = null` and `PromptTemplateLoader? templateLoader = null` as **optional constructor parameters** of `BehaviorAnalyzer`. Store as private readonly fields. `AnalyzeAsync` passes them to `BuildAnalysisPrompt`.

**Rationale**:
- Constructor injection is the existing project convention (`CopilotGenerationAgent`, `CopilotCritic` all take config in their constructors).
- Optional with `null` defaults preserves all current test instantiations and lets unit tests deliberately exercise the legacy hardcoded prompt path.
- A setter would create a half-constructed object and a footgun for the next refactor.

**Alternatives considered**:
- *Static factory taking config*: indirection for no benefit; the class is small.
- *Pass config to `AnalyzeAsync` directly*: works but each call site has to thread the config through, and there are 3 call sites today; the constructor approach centralizes it.

## D2 — `PromptTemplateLoader` is constructed with a working directory, not config

**Decision**: In `GenerateHandler`, build the loader as `new PromptTemplateLoader(Directory.GetCurrentDirectory())` and pass that instance into `BehaviorAnalyzer`.

**Rationale**:
- The user-supplied draft wrote `new PromptTemplateLoader(config)`, but `PromptTemplateLoader`'s real constructor signature is `(string workingDirectory, ILogger? logger = null)`. The draft was wrong.
- `Directory.GetCurrentDirectory()` is the path the user runs `spectra ai generate` from, which is the same path used by every other working-directory consumer in `GenerateHandler` (already at lines 183, 754, 1323, 1396).
- All 3 BehaviorAnalyzer call sites are inside the same `ExecuteAsync`-style method, so each can capture the local `currentDir` variable. A single shared loader instance per command invocation is sufficient — the loader is stateless aside from the directory path, so reuse vs. fresh-construction makes no functional difference; reuse is just tidier.

**Alternatives considered**:
- *Inject the loader from outside the handler*: out of scope; the handler currently builds nothing of the sort, and DI would be a separate refactor.
- *Make the loader a singleton*: not needed; it's cheap to construct and bounded to one per handler invocation.

## D3 — Categories: open string identifiers, not a closed enum

**Decision**: Delete `Spectra.Core.Models.BehaviorCategory` (the 5-value enum). Replace every consumer with a `string` category identifier. The breakdown becomes `IReadOnlyDictionary<string, int>`.

**Rationale**:
- The user's spec calls custom categories the "entire value of Spec 030." If the breakdown stays enum-keyed, custom categories must either (a) be rejected (FR-006 violation) or (b) be silently rebadged to a default enum value (the bug we are fixing). There is no third option; the only way to make custom categories visible end-to-end is to widen the type to a free-form string.
- The current `IdentifiedBehavior.Category` getter that maps unknown raw strings to `BehaviorCategory.HappyPath` is the actual root cause of the silent-rebadging bug. Deleting the enum forces every consumer off this footgun in a single mechanical change rather than leaving a trap for future maintenance.
- The widening removes a constraint, not adds one — Constitution V (Simplicity).

**Alternatives considered**:
- *Keep enum, add `Custom` value with a `string CustomLabel`*: hybrid approach, doubles the surface area, every consumer needs an `if (cat == Custom) use label else use enum.ToString()` branch — a worse footgun than what exists today.
- *Use `[JsonStringEnumConverter]` with fallback to a sentinel `Unknown`*: still loses identity (every custom category collapses into one bucket).
- *Keep `CategoryRaw` field, change derived getter to throw on unknown*: would surface the bug but break runtime instead of fixing it.

## D4 — `IdentifiedBehavior` shape change

**Decision**:
- Rename `CategoryRaw` (string) → `Category` (string) with `[JsonPropertyName("category")]`.
- Delete the derived enum getter.
- Treat empty/null/whitespace as `"uncategorized"` at the analyzer's grouping step (not at the model level — the model preserves whatever the AI sent for debuggability).

**Rationale**:
- The "Raw" suffix existed only because the derived getter was named `Category`. Without the getter the suffix is dead weight; the field IS the category.
- The "uncategorized" mapping happens at one place (the grouping in `AnalyzeAsync`) so the model stays a faithful echo of the AI response, and the bucket label is consistent with FR-008.

**Alternatives considered**:
- *Default empty to "happy_path"*: the legacy behavior, explicitly forbidden by FR-008.
- *Drop empty-category behaviors*: breaks count accuracy and silently loses data.

## D5 — `BehaviorAnalysisResult.Breakdown` retype

**Decision**: Change `Breakdown` from `IReadOnlyDictionary<BehaviorCategory, int>` to `IReadOnlyDictionary<string, int>`. Change `GetRemainingByCategory(IReadOnlyList<BehaviorCategory>?)` to take `IReadOnlyList<string>?`.

**Rationale**:
- Required for FR-007 — breakdown must be keyed by the AI-returned identifier.
- `GetRemainingByCategory` is called by the suggestion path with the *categories the user already generated*. Those categories must also be strings — they are the ones the analyzer surfaced in the breakdown. The string-list signature aligns producer and consumer.

**Blast radius for downstream consumers** (mechanical, not architectural):
1. `AnalysisPresenter.CategoryLabels` becomes `Dictionary<string, string>` keyed by category ID. Render method iterates the dict's actual keys and looks up a friendly label, falling back to `id.Replace('_', ' ')` for unknown IDs.
2. `CountSelector.CategoryLabels` same treatment. `SelectedCategories` becomes `IReadOnlyList<string>?`.
3. `BehaviorAnalysisResult.GetRemainingByCategory` parameter retypes; internal logic unchanged (it just compares keys).
4. `SuggestionBuilder` — audit only; it consumes `Breakdown` but most likely just iterates `KeyValuePair`s, which works the same with string keys.
5. Test instantiations that build `Dictionary<BehaviorCategory, int>` literals → `Dictionary<string, int>` literals with `"happy_path"`, `"negative"`, etc. as keys.

## D6 — `FilterByFocus` rewrite

**Decision**: Rewrite `FilterByFocus` to operate on string category identifiers, with two layered matching strategies:

1. **Substring match**: Tokenize `focusArea` on whitespace/`,`/`;`, lowercase, normalize underscores/hyphens to spaces. Tokenize each behavior's `Category` the same way. A behavior matches if **any** focus token is a substring of the normalized category string. (Satisfies FR-010 and the legacy FR-012 cases automatically: "happy" is a substring of "happy path"; "edge" is a substring of "edge case"; "perf" is a substring of "performance"; etc.)
2. **Fallback**: If the substring pass returns zero matches, return all behaviors unchanged (FR-011).

**Rationale**:
- A single string-substring pass subsumes the old enum keyword expansions because every legacy enum name is a substring of itself (and of common search terms like "happy path"). No second matching strategy is needed.
- This intentionally supports the user's `keyboard_interaction` example: tokenized to `keyboard interaction`, and `--focus "keyboard"` substring-matches.

**Alternatives considered**:
- *Keep the legacy hardcoded keyword expansions and add a second pass for custom categories*: redundant — substring is a generalization of the legacy mapping.
- *Use Levenshtein/fuzzy*: overkill for "the user typed `keyboard` and we should find `keyboard_interaction`."

## D7 — Default categories live in `PromptTemplateLoader.GetCategories(config)`

**Decision**: Use the existing `PromptTemplateLoader.GetCategories(config)` static method as the single source of truth for "what categories should the analyzer prompt for." It already returns `config.Analysis.Categories` if present, else `DefaultCategories.All` (the 6 Spec 030 defaults).

**Rationale**:
- This method already exists and is correctly implemented for the test-generation template path. Reusing it satisfies FR-004 with zero new code.
- Keeps the defaults in one place; if Spec 030 ever updates them, the analyzer follows automatically.

**Alternatives considered**:
- *Duplicate the defaults inside `BehaviorAnalyzer`*: drift hazard; rejected.

## D8 — Out of scope: `CopilotGenerationAgent` has the same dead-loader bug

**Discovered, not fixing**: `CopilotGenerationAgent.GenerateTestsAsync` (line 141) calls `BuildFullPrompt(prompt, requestedCount, criteriaContext, profileFormat: profileFormat)` — note the missing `templateLoader` argument. `BuildFullPrompt` accepts an optional `PromptTemplateLoader? templateLoader = null` parameter that, when null, returns the legacy hardcoded prompt for test generation. So the test-generation template at `.spectra/prompts/test-generation.md` is **also** dead code today.

**Why not fix it now**: The user's spec is narrowly scoped to `BehaviorAnalyzer`. Per CLAUDE.md ("Don't add features, refactor code, or make 'improvements' beyond what was asked"), the test-generation fix needs its own spec. It is documented here so it doesn't get lost.

**Suggested follow-up**: A future spec "037-fix-generation-template-injection" applying the identical pattern to `CopilotGenerationAgent`: store `_templateLoader` field, pass it through to `BuildFullPrompt`, and update `AgentFactory` to instantiate the loader.

## D9 — Test strategy

**Decision**: 6 new tests in `BehaviorAnalyzerTests.cs`, plus mechanical updates to existing tests across 4 test files for the enum→string transition.

**New tests** (per the user's spec test plan):

1. `BuildPrompt_WithTemplateLoader_UsesTemplate` — confirms the template path is taken when loader is non-null.
2. `BuildPrompt_WithCustomCategories_InjectsAll` — config with 4 custom categories → prompt contains all 4 IDs.
3. `BuildPrompt_WithoutLoader_UsesLegacy` — null loader → legacy prompt (backward compat for existing tests).
4. `BuildPrompt_WithEmptyCategories_UsesDefaults` — config with empty list → 6 Spec 030 defaults appear in prompt.
5. `FilterByFocus_CustomCategory_Matches` — focus "keyboard" + category `keyboard_interaction` → matches.
6. `ParseResponse_CustomCategory_Preserved` — JSON `"category": "keyboard_interaction"` → behavior.Category is `"keyboard_interaction"` (not collapsed to a default).

**Existing tests adjusted**:
- `BehaviorAnalyzerTests` — any test that asserts `BehaviorCategory.HappyPath` becomes `"happy_path"`.
- `AnalysisPresenterTests` — same rename.
- `CountSelectorTests` — same rename; `SelectedCategories` becomes string list.
- `SuggestionBuilderTests` — verify it doesn't reference the enum; if it does, rename.

**Rationale**:
- The 6 new tests map 1:1 to the user's stated test plan and to the 8 success criteria.
- The mechanical existing-test updates are the cost of widening the type; without them the build breaks.

---

**Status**: Phase 0 complete. No NEEDS CLARIFICATION markers. Ready for Phase 1.
