# Tasks: Fix BehaviorAnalyzer Category Injection

**Input**: Design documents from `/specs/036-fix-analyzer-categories/`
**Prerequisites**: spec.md, plan.md, research.md, data-model.md, contracts/analyzer-api.md, quickstart.md

**Tests**: Required by the user's spec test plan and by FR-014/SC-006. New tests are added to existing test files; existing tests are updated for the enum→string transition.

**Organization**: Tasks are grouped by phase. Each user story (US1-US3) is satisfied by a slice of phases — see "Story Dependencies" at the bottom.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P] tasks (different files, no dependencies)
- **[Story]**: User story this task supports (US1, US2, US3) or [Foundation]/[Polish]
- All paths are absolute or relative to repository root `C:/SourceCode/Spectra/`

---

## Phase 1: Setup & Audit

**Purpose**: Confirm working state and audit one ambiguity (does `SuggestionBuilder` reference `BehaviorCategory` directly?).

- [ ] T001 Verify branch is `036-fix-analyzer-categories` and working tree is clean (`git status`).
- [ ] T002 Audit `src/Spectra.CLI/Session/SuggestionBuilder.cs` and `tests/Spectra.CLI.Tests/Session/SuggestionBuilderTests.cs` for direct references to `BehaviorCategory`. Note in your task scratch which files need editing in Phase 2.

---

## Phase 2: Foundation — Core Type Refactor

**Purpose**: Reshape the data types (model + result + downstream consumers) so the new analyzer wiring has somewhere to put the strings. This phase is the largest mechanical change and must complete cleanly before the analyzer wiring (Phase 3) can compile.

**⚠️ CRITICAL**: All of Phase 2 must build cleanly before starting Phase 3. The build is the gate.

### Model layer

- [ ] T003 [P] [Foundation] Edit `src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs`:
  - Rename property `CategoryRaw` → `Category`. Keep `[JsonPropertyName("category")]`.
  - **Delete** the derived `BehaviorCategory Category => ParseCategory(CategoryRaw)` getter.
  - **Delete** the `private static BehaviorCategory ParseCategory(string raw)` method.
  - Remove the now-unused `using Spectra.Core.Models;` directive.
  - Result: a 2-field record (`Category`, `Title`, `Source`) with no enum dependency.
- [ ] T004 [P] [Foundation] Edit `src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs`:
  - Change `Breakdown` type from `IReadOnlyDictionary<BehaviorCategory, int>` → `IReadOnlyDictionary<string, int>`.
  - Change `GetRemainingByCategory` parameter from `IReadOnlyList<BehaviorCategory>?` → `IReadOnlyList<string>?` and return type to `IReadOnlyDictionary<string, int>`.
  - Update the internal `Dictionary<BehaviorCategory, int>` instantiation inside the method to `Dictionary<string, int>`.
  - Remove the `using Spectra.Core.Models;` directive.

### Presenter & selector layers

- [ ] T005 [P] [Foundation] Edit `src/Spectra.CLI/Output/AnalysisPresenter.cs`:
  - Change `CategoryLabels` from `Dictionary<BehaviorCategory, string>` → `Dictionary<string, string>`. Replace the 5 enum keys with their string equivalents (`"happy_path"`, `"negative"`, `"edge_case"`, `"security"`, `"performance"`) AND add Spec 030 defaults that didn't exist before: `"boundary"` → "boundary conditions", `"error_handling"` → "error handling", `"uncategorized"` → "uncategorized".
  - Change the `RenderXxx` method's `generatedCategories` parameter (line 65) from `IReadOnlyList<BehaviorCategory>?` → `IReadOnlyList<string>?`.
  - Add a private static helper `FormatCategoryLabel(string id)` that returns `CategoryLabels.TryGetValue(id, out var label) ? label : id.Replace('_', ' ').Replace('-', ' ')`. Use it everywhere the rendering code currently does a direct dict lookup.
  - Remove `using Spectra.Core.Models;` directive.
- [ ] T006 [P] [Foundation] Edit `src/Spectra.CLI/Interactive/CountSelector.cs`:
  - Change `SelectedCategories` property type from `IReadOnlyList<BehaviorCategory>?` → `IReadOnlyList<string>?`.
  - Change inner `Categories` property at line 152 from `IReadOnlyList<BehaviorCategory>?` → `IReadOnlyList<string>?`.
  - Change `CategoryLabels` from enum-keyed → string-keyed using the same key set as `AnalysisPresenter.CategoryLabels`. Add the same fallback formatter.
  - Update the `analysis.Breakdown.OrderBy(...)` block (line 97) to iterate string keys; preserve insertion order rather than enum-defined order.
  - Remove `using Spectra.Core.Models;` directive.

### Suggestion builder (conditional)

- [ ] T007 [Foundation] If T002 found `SuggestionBuilder.cs` references `BehaviorCategory`, edit it: replace enum-keyed dictionary access with string-keyed access. Otherwise mark this task complete with no changes.

### Generate handler

- [ ] T008 [Foundation] Edit `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`:
  - At each of the **3 call sites** (lines ≈284, ≈341, ≈914 — line numbers may shift), change the `BehaviorAnalyzer` instantiation from `new BehaviorAnalyzer(provider, status => ...)` (or `new BehaviorAnalyzer(provider)`) to:
    ```csharp
    var loader = new PromptTemplateLoader(currentDir);
    var analyzer = new BehaviorAnalyzer(provider, status => ..., config, loader);
    ```
    Reuse a single `loader` per `currentDir` scope where possible. The `currentDir` local already exists at lines ≈183, 754, 1323, 1396; verify each call site has a `currentDir` in scope and capture/declare as needed.
  - Add `using Spectra.CLI.Prompts;` to the file's using directives if not already present.
  - Update any `Breakdown?.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value)` projections (line ~310) to `Breakdown?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)` — `kvp.Key` is now already a string.

### Build gate

- [ ] T009 [Foundation] Run `dotnet build -c Release` from repo root. Expected: build fails ONLY in the test projects (because tests still reference `BehaviorCategory`). If production code (`Spectra.CLI`) doesn't build cleanly, fix it before continuing — Phase 3 cannot start until production builds.

---

## Phase 3: Analyzer Wiring (US1 & US2 core)

**Purpose**: Make `BehaviorAnalyzer` actually use the config and template loader. This is the core fix.

**Goal**: After this phase, custom categories from config flow through to the AI prompt and back into the breakdown.

- [ ] T010 [US1] Edit `src/Spectra.CLI/Agent/Copilot/BehaviorAnalyzer.cs`:
  - Add private readonly fields `_config: SpectraConfig?` and `_templateLoader: PromptTemplateLoader?`.
  - Update constructor to `BehaviorAnalyzer(SpectraProviderConfig? provider, Action<string>? onStatus = null, SpectraConfig? config = null, PromptTemplateLoader? templateLoader = null)` and assign the new fields.
  - In `AnalyzeAsync`, change the call at line ~56 from `BuildAnalysisPrompt(documents, focusArea)` → `BuildAnalysisPrompt(documents, focusArea, _config, _templateLoader)`.
  - Change the breakdown grouping (line ~112) from `behaviors.GroupBy(b => b.Category)` to `behaviors.GroupBy(b => string.IsNullOrWhiteSpace(b.Category) ? "uncategorized" : b.Category)`. The `.ToDictionary(g => g.Key, g => g.Count())` continues to work.
- [ ] T011 [US2] In the same `BehaviorAnalyzer.cs`, rewrite `FilterByFocus`:
  ```csharp
  internal static List<IdentifiedBehavior> FilterByFocus(
      List<IdentifiedBehavior> behaviors, string focusArea)
  {
      var tokens = focusArea.ToLowerInvariant()
          .Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries);
      if (tokens.Length == 0)
          return behaviors;

      var matches = behaviors.Where(b =>
      {
          var normalized = (b.Category ?? "").ToLowerInvariant()
              .Replace('_', ' ').Replace('-', ' ');
          return tokens.Any(t => normalized.Contains(t));
      }).ToList();

      return matches.Count > 0 ? matches : behaviors;
  }
  ```
  - Remove the now-unused `BehaviorCategory` references from this file.
  - Remove `using Spectra.Core.Models;` if it's no longer needed (the file may still need other imports from that namespace — verify).

---

## Phase 4: Delete the dead enum

- [ ] T012 [Foundation] Delete `src/Spectra.Core/Models/BehaviorCategory.cs`.
- [ ] T013 [Foundation] Run `dotnet build -c Release`. Expected: production code builds cleanly. Tests still don't compile.

---

## Phase 5: Update existing tests for the enum → string transition

**Purpose**: Restore the test build to green by replacing every `BehaviorCategory.X` with the corresponding string literal.

- [ ] T014 [P] [US3] Edit `tests/Spectra.CLI.Tests/Output/AnalysisPresenterTests.cs`: replace each `BehaviorCategory.HappyPath` → `"happy_path"`, etc. Update any `Dictionary<BehaviorCategory, int>` literals to `Dictionary<string, int>`. Remove `using Spectra.Core.Models;`.
- [ ] T015 [P] [US3] Edit `tests/Spectra.CLI.Tests/Interactive/CountSelectorTests.cs`: same transformations as T014, including `SelectedCategories` assertions which now compare against `IReadOnlyList<string>`.
- [ ] T016 [P] [US3] Edit `tests/Spectra.CLI.Tests/Session/SuggestionBuilderTests.cs`: same transformations if and only if T002 flagged it. Otherwise no edit.
- [ ] T017 [US3] Edit `tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs`: replace every `BehaviorCategory.X` reference; replace `b.Category` (which used to be the enum getter) with the string field; update any breakdown assertions to compare string keys.

### Build & test gate

- [ ] T018 [US3] Run `dotnet build -c Release`. Expected: clean build, zero errors, zero new warnings.
- [ ] T019 [US3] Run `dotnet test -c Release --no-build`. Expected: all 1453+ existing tests pass. If any test fails, it indicates the transformation in T014–T017 missed something — fix and rerun until green. **Do not proceed to Phase 6 until this is green.**

---

## Phase 6: New tests for the bug fix (US1 & US2)

**Purpose**: Lock in the fix with tests that map 1:1 to the user's stated test plan and the spec's success criteria.

All 6 tests live in `tests/Spectra.CLI.Tests/Agent/BehaviorAnalyzerTests.cs`. They are added at the end of the file (or alongside existing `BuildAnalysisPrompt` / `FilterByFocus` / `ParseAnalysisResponse` tests, whichever location is more discoverable).

- [ ] T020 [US1] Add test `BuildPrompt_WithTemplateLoader_UsesTemplate`:
  - Create a temp working dir, write a `.spectra/prompts/behavior-analysis.md` with a unique sentinel string in the body (e.g., `"<<UNIT-TEST-MARKER>>"`).
  - Construct `new PromptTemplateLoader(tempDir)`.
  - Call `BehaviorAnalyzer.BuildAnalysisPrompt(docs, focus: null, config: null, templateLoader: loader)`.
  - Assert the returned string contains the sentinel.
  - Cleanup temp dir.
- [ ] T021 [P] [US1] Add test `BuildPrompt_WithCustomCategories_InjectsAll`:
  - Construct a `SpectraConfig` with `Analysis.Categories = [keyboard_interaction, screen_reader_support, color_contrast, focus_management]` (4 entries).
  - Construct a `PromptTemplateLoader` against a temp dir with no user template (so the built-in is used).
  - Call `BuildAnalysisPrompt(docs, focus: null, config, loader)`.
  - Assert the returned prompt contains all 4 IDs and does NOT contain `happy_path`.
- [ ] T022 [P] [US1] Add test `BuildPrompt_WithoutLoader_UsesLegacy`:
  - Call `BuildAnalysisPrompt(docs, focus: null, config: null, templateLoader: null)`.
  - Assert the returned prompt contains the legacy hardcoded category names (`happy_path`, `negative`, `edge_case`, `security`, `performance`) — proves the legacy fallback is preserved for tests.
- [ ] T023 [P] [US1] Add test `BuildPrompt_WithEmptyCategories_UsesDefaults`:
  - Construct a `SpectraConfig` with `Analysis.Categories = new List<CategoryDefinition>()` (empty).
  - Construct a `PromptTemplateLoader` against a temp dir.
  - Call `BuildAnalysisPrompt`.
  - Assert the prompt contains all 6 Spec 030 defaults: `happy_path`, `negative`, `edge_case`, `boundary`, `error_handling`, `security`.
- [ ] T024 [P] [US2] Add test `FilterByFocus_CustomCategory_Matches`:
  - Build a list with 3 `IdentifiedBehavior` items: one with `Category = "keyboard_interaction"`, one with `Category = "color_contrast"`, one with `Category = "happy_path"`.
  - Call `FilterByFocus(behaviors, "keyboard")`.
  - Assert the result contains exactly the `keyboard_interaction` behavior.
  - Add a second sub-assertion: call `FilterByFocus(behaviors, "happy path")` and assert exactly the `happy_path` behavior is returned (legacy regression check).
  - Add a third sub-assertion: call `FilterByFocus(behaviors, "asdfqwerty")` and assert all 3 behaviors are returned (no-match fallback).
- [ ] T025 [P] [US1] Add test `ParseResponse_CustomCategory_Preserved`:
  - Pass JSON `{"behaviors":[{"category":"keyboard_interaction","title":"Tab navigation works","source":"docs/a11y.md"}]}` through `ParseAnalysisResponse`.
  - Assert the returned list has 1 item with `Category == "keyboard_interaction"` (string equality, not collapsed to a default).

### Final gate

- [ ] T026 [US3] Run `dotnet build -c Release && dotnet test -c Release --no-build`. Expected: clean build + all 1459+ tests passing (1453 prior + 6 new = at least 1459, possibly more if T014–T017 split or added assertions). Zero failures.

---

## Phase 7: Polish & Verification

- [ ] T027 [Polish] Run a final grep for `BehaviorCategory` across the entire repo: `grep -r "BehaviorCategory" src/ tests/ --include="*.cs"`. Expected: zero matches outside `specs/` (which are historical).
- [ ] T028 [Polish] Walk through `quickstart.md` mentally: each step should map to a real code path that the new code supports. No discrepancies.
- [ ] T029 [Polish] Final commit with message `fix(036): wire BehaviorAnalyzer to config + template loader; widen breakdown to strings`. Stage:
  - 6 production files modified (IdentifiedBehavior, BehaviorAnalysisResult, BehaviorAnalyzer, AnalysisPresenter, CountSelector, GenerateHandler)
  - 1 production file deleted (BehaviorCategory.cs)
  - 4 test files modified (BehaviorAnalyzerTests, AnalysisPresenterTests, CountSelectorTests, optionally SuggestionBuilderTests)
  - All `specs/036-fix-analyzer-categories/` documents
  - Optionally: SuggestionBuilder.cs if T007 modified it

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup & Audit)**: T001, T002 — read-only.
- **Phase 2 (Foundation)**: T003–T009 — must complete before Phase 3. T003–T006 can run in parallel; T007 depends on T002's audit; T008 depends on T003 (the field rename); T009 is the build gate.
- **Phase 3 (Analyzer Wiring)**: T010–T011 — depends on Phase 2 build green.
- **Phase 4 (Delete enum)**: T012–T013 — depends on Phase 3 (analyzer no longer references the enum).
- **Phase 5 (Existing test updates)**: T014–T019 — depends on Phase 4 (production builds; tests are the only thing left to fix).
- **Phase 6 (New tests)**: T020–T026 — depends on Phase 5 green.
- **Phase 7 (Polish)**: T027–T029 — depends on Phase 6 green.

### Story Dependencies

- **US1 (P1, custom categories drive analysis)**: Needs T003, T004, T005, T006, T008, T010 (production wiring) + T020, T021, T022, T023, T025 (tests).
- **US2 (P2, focus filter on custom categories)**: Needs T011 (production wiring) + T024 (test).
- **US3 (P1, no regression)**: Needs T014–T019 (existing tests adjusted and green) + T027 (final grep).

### Within Each Phase

- All [P] tasks within a phase touch different files and are safe to parallelize. Sequential gates are explicitly called out (T009, T013, T018, T019, T026).

---

## Parallel Opportunities

```bash
# Phase 2 — model + presenter + selector + handler in parallel:
T003 [P] IdentifiedBehavior.cs
T004 [P] BehaviorAnalysisResult.cs
T005 [P] AnalysisPresenter.cs
T006 [P] CountSelector.cs
# Then T007 (conditional), T008, T009 sequentially.

# Phase 5 — test updates in parallel:
T014 [P] AnalysisPresenterTests.cs
T015 [P] CountSelectorTests.cs
T016 [P] SuggestionBuilderTests.cs (conditional)
# Then T017 (BehaviorAnalyzerTests has more transformations + new tests later), T018, T019.

# Phase 6 — new tests are all in BehaviorAnalyzerTests.cs but logically independent:
T020 [P] sentinel test
T021 [P] custom-categories-injected test
T022 [P] no-loader-legacy test
T023 [P] empty-categories-defaults test
T024 [P] focus-custom test
T025 [P] parse-custom-category test
# (Mark all [P] but accept the merge cost — they share one file.)
```

---

## Implementation Strategy

### Single-pass refactor (recommended)

This is a tightly-coupled refactor; do it in one branch and merge as one PR:

1. Phase 1 (Setup & Audit) — 5 min
2. Phase 2 (Foundation) — 30 min, build-driven
3. Phase 3 (Analyzer Wiring) — 15 min
4. Phase 4 (Delete enum) — 2 min
5. Phase 5 (Update existing tests) — 20 min, test-driven
6. Phase 6 (New tests) — 30 min
7. Phase 7 (Polish + commit) — 5 min

Total estimated effort: ~2 hours assuming clean execution. Build/test breakage between phases is expected and intentional — the build is the safety net.

---

## Notes

- The user's spec mentioned `UpdateHandler` as a possible caller — verified during planning that it does not instantiate `BehaviorAnalyzer`. No change needed there.
- The user's spec was inaccurate about `IdentifiedBehavior.Category` being an enum field — it was already `string CategoryRaw`, surfaced as a derived enum getter. The fix is mechanically simpler than the user's draft suggested (drop the getter, rename field).
- A latent identical bug in `CopilotGenerationAgent.BuildFullPrompt` (test-generation template never loaded) is **not fixed** in this feature. Documented in research.md D8 as a follow-up.
- The new constructor params on `BehaviorAnalyzer` are optional with `null` defaults, so all existing test instantiations continue to compile and exercise the legacy fallback path — that is by design (and is what the legacy-path test T022 verifies).
