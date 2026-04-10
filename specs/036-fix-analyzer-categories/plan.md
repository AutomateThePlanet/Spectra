# Implementation Plan: Fix BehaviorAnalyzer Category Injection

**Branch**: `036-fix-analyzer-categories` | **Date**: 2026-04-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/036-fix-analyzer-categories/spec.md`

## Summary

Wire `SpectraConfig` and `PromptTemplateLoader` into `BehaviorAnalyzer` so the Spec 030 prompt template and the user's `analysis.categories` configuration are no longer dead code, then refactor the per-category breakdown from a closed enum (`BehaviorCategory`) to an open string identifier so custom categories survive end-to-end (analyzer → presenter → count selector → JSON output → suggestion builder). The fix is mechanical (rename + retype) but touches multiple downstream consumers; the existing test suite is the safety net for the refactor.

Discovered during planning:

- `BehaviorAnalyzer` constructor takes only `(ProviderConfig?, Action<string>?)` and three call sites in `GenerateHandler.cs` (lines 284, 341, 914) instantiate it that way. No `UpdateHandler` callers.
- `BehaviorAnalyzer.AnalyzeAsync` calls `BuildAnalysisPrompt(documents, focusArea)` at line 56 — no config, no loader. Falls into legacy hardcoded prompt with 5 hardcoded categories.
- `IdentifiedBehavior` already stores `CategoryRaw: string` (the AI's raw value) and exposes a derived `Category: BehaviorCategory` getter that **collapses any unknown raw value to `BehaviorCategory.HappyPath`**. So the second bug isn't a JSON deserialization failure (the user's spec was inaccurate on this) — it's a derived-getter that silently rebadges custom categories as HappyPath in the breakdown.
- `BehaviorAnalysisResult.Breakdown` is `IReadOnlyDictionary<BehaviorCategory, int>` (enum-keyed). To make custom categories visible end-to-end, this type must change to `IReadOnlyDictionary<string, int>`.
- Downstream consumers using the enum: `AnalysisPresenter` (CategoryLabels dict + render method param), `CountSelector` (CategoryLabels dict + `SelectedCategories: IReadOnlyList<BehaviorCategory>?` + `Categories` property), `BehaviorAnalysisResult.GetRemainingByCategory`, plus four test files (`BehaviorAnalyzerTests`, `AnalysisPresenterTests`, `CountSelectorTests`, `SuggestionBuilderTests`).
- `PromptTemplateLoader` constructor is `(string workingDirectory, ILogger? logger = null)` — **not** `(SpectraConfig)` as the user-supplied draft suggested. The handler needs to pass `Directory.GetCurrentDirectory()` (or the equivalent local variable that already exists at line 183).
- `PromptTemplateLoader.GetCategories(config)` already exists and correctly returns `config.Analysis.Categories` if non-empty, else `DefaultCategories.All` (the 6 Spec 030 defaults). The fix is simply to call it.
- A separate but identical bug exists in `CopilotGenerationAgent.BuildFullPrompt`: it accepts an optional `templateLoader` parameter that the production caller (line 141) never passes, so the test-generation template is also dead code. **Out of scope** for this feature per the user's narrowly-scoped spec; documented in research.md as a follow-up.

## Technical Context

**Language/Version**: C# 12, .NET 8.0
**Primary Dependencies**: None added; all needed types (`SpectraConfig`, `PromptTemplateLoader`, `CategoryDefinition`, `DefaultCategories`) already exist
**Storage**: N/A
**Testing**: xUnit (`Spectra.CLI.Tests`); 6 new tests + several existing tests need adjustment for the enum→string transition
**Target Platform**: cross-platform (build runs on Windows/Linux/Mac as today)
**Project Type**: CLI library
**Performance Goals**: N/A — fix is in a once-per-generation code path
**Constraints**:
- Zero behavior change for default-configured users (FR-013)
- Existing test suite must remain green (FR-014, SC-006)
- Public API surface of `BehaviorAnalyzer` constructor must keep new params optional with sensible defaults so test instantiations can pass `null` and exercise the legacy branch
**Scale/Scope**: ~6 production files modified, ~4 test files modified, 6 new tests, 0 new projects

## Constitution Check

| Principle | Compliance | Notes |
|-----------|------------|-------|
| I. GitHub as Source of Truth | ✅ N/A | No storage changes. |
| II. Deterministic Execution | ✅ N/A | No MCP execution engine changes. |
| III. Orchestrator-Agnostic Design | ✅ N/A | No MCP API changes. |
| IV. CLI-First Interface | ✅ Pass | No CLI surface changes. The `--focus` flag is unchanged in shape; only its matching logic improves. |
| V. Simplicity (YAGNI) | ✅ Pass | No new abstractions. Removes one — collapses an enum-derived getter that was actively misleading. The widening of `Breakdown` from enum-keyed to string-keyed is a *removal* of a constraint, not an addition of complexity. |

**Quality Gates**: `spectra validate` is unaffected (no schema changes). Existing CI must continue to pass (FR-014).

**Result**: PASS — no violations. Complexity Tracking left empty.

## Project Structure

### Documentation (this feature)

```text
specs/036-fix-analyzer-categories/
├── spec.md              # /speckit.specify output
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions for the refactor
├── data-model.md        # Phase 1 — type-shape changes (enum→string transition)
├── quickstart.md        # Phase 1 — how a user verifies the fix
├── contracts/
│   └── analyzer-api.md  # Phase 1 — public/internal API of BehaviorAnalyzer & friends
├── checklists/
│   └── requirements.md  # Already complete
└── tasks.md             # /speckit.tasks output (created next)
```

### Source Code (repository root)

This feature edits existing files only — no new directories or projects.

```text
src/Spectra.CLI/
├── Agent/
│   ├── Analysis/
│   │   ├── IdentifiedBehavior.cs      # MODIFY: drop derived enum getter; rename CategoryRaw → Category (string)
│   │   └── BehaviorAnalysisResult.cs  # MODIFY: Breakdown becomes IReadOnlyDictionary<string,int>; GetRemainingByCategory adapts
│   ├── Copilot/
│   │   └── BehaviorAnalyzer.cs        # MODIFY: constructor takes config+loader; AnalyzeAsync passes them; FilterByFocus rewritten string-based
│   └── (no other Agent files touched)
├── Output/
│   └── AnalysisPresenter.cs           # MODIFY: CategoryLabels keyed by string ID; rendering iterates whatever keys are in the dict
├── Interactive/
│   └── CountSelector.cs               # MODIFY: SelectedCategories becomes IReadOnlyList<string>?; CategoryLabels keyed by string with fallback formatter
├── Session/
│   └── SuggestionBuilder.cs           # AUDIT — modify only if it consumes BehaviorCategory keys
└── Commands/Generate/
    └── GenerateHandler.cs             # MODIFY: 3 BehaviorAnalyzer instantiation sites pass (config, new PromptTemplateLoader(currentDir))

src/Spectra.Core/Models/
└── BehaviorCategory.cs                # DELETE — no longer referenced after the refactor

tests/Spectra.CLI.Tests/
├── Agent/
│   └── BehaviorAnalyzerTests.cs       # MODIFY existing tests; ADD 6 new tests
├── Output/
│   └── AnalysisPresenterTests.cs      # MODIFY: enum keys become string keys
├── Interactive/
│   └── CountSelectorTests.cs          # MODIFY: SelectedCategories becomes string list
└── Session/
    └── SuggestionBuilderTests.cs      # MODIFY only if it constructs BehaviorCategory; AUDIT first
```

**Structure Decision**: All edits are localized to the `Spectra.CLI` project (the analyzer, its model, its presenters/selectors, and the handler) plus the deletion of one model in `Spectra.Core`. No new files for the production code; new tests added inline to the existing `BehaviorAnalyzerTests.cs`.

## Complexity Tracking

> No constitution violations. Table left intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | (none)     | (none)                              |
