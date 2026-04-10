# Phase 1 — Data Model: Fix BehaviorAnalyzer Category Injection

**Feature**: 036-fix-analyzer-categories
**Date**: 2026-04-10

This feature reshapes existing types rather than introducing new ones. The "data model" is therefore a before/after table for each affected type.

## Type 1 — `IdentifiedBehavior` (Spectra.CLI.Agent.Analysis)

| Aspect | Before | After |
|--------|--------|-------|
| Category field | `string CategoryRaw` (with `[JsonPropertyName("category")]`) | `string Category` (same JSON name) |
| Derived enum getter | `BehaviorCategory Category => ParseCategory(CategoryRaw)` (collapses unknowns to `HappyPath`) | **REMOVED** |
| Default for empty/null | Falls into HappyPath via the parser's `_ =>` arm | Stored as-is; the analyzer's grouping step substitutes `"uncategorized"` for empty/null |

### Validation rules
- `Category` MUST be preserved exactly as the AI sent it (no normalization, no rebadging) — this is the field-level guarantee for FR-006.
- The model carries no validation on the category string; the analyzer is responsible for the "uncategorized" fallback at grouping time.

## Type 2 — `BehaviorAnalysisResult` (Spectra.CLI.Agent.Analysis)

| Aspect | Before | After |
|--------|--------|-------|
| `Breakdown` type | `IReadOnlyDictionary<BehaviorCategory, int>` | `IReadOnlyDictionary<string, int>` |
| `GetRemainingByCategory` parameter | `IReadOnlyList<BehaviorCategory>?` | `IReadOnlyList<string>?` |
| `GetRemainingByCategory` return | `IReadOnlyDictionary<BehaviorCategory, int>` | `IReadOnlyDictionary<string, int>` |
| Implementation of `GetRemainingByCategory` | Looks up enum keys, zeroes them | Looks up string keys, zeroes them |

### Validation rules
- `Breakdown` keys are arbitrary non-empty strings (the "uncategorized" bucket included).
- `GetRemainingByCategory` MUST treat the parameter as a closed set of identifiers to subtract; behaviors with categories *not* in the parameter list are preserved.

## Type 3 — `BehaviorAnalyzer` (Spectra.CLI.Agent.Copilot)

| Aspect | Before | After |
|--------|--------|-------|
| Constructor signature | `BehaviorAnalyzer(SpectraProviderConfig? provider, Action<string>? onStatus = null)` | `BehaviorAnalyzer(SpectraProviderConfig? provider, Action<string>? onStatus = null, SpectraConfig? config = null, PromptTemplateLoader? templateLoader = null)` |
| Private fields | `_provider`, `_onStatus` | + `_config`, `_templateLoader` |
| `BuildAnalysisPrompt` call in `AnalyzeAsync` | `BuildAnalysisPrompt(documents, focusArea)` | `BuildAnalysisPrompt(documents, focusArea, _config, _templateLoader)` |
| Breakdown grouping in `AnalyzeAsync` | `behaviors.GroupBy(b => b.Category)` (enum) | `behaviors.GroupBy(b => string.IsNullOrWhiteSpace(b.Category) ? "uncategorized" : b.Category)` |
| `FilterByFocus` body | Enum keyword expansion + `behaviors.Where(b => matchingCategories.Contains(b.Category))` | String tokenization + substring match against normalized `b.Category` |
| `FilterByFocus` parameter type | `List<IdentifiedBehavior> behaviors, string focusArea` | unchanged |

### Validation rules
- The new constructor params remain optional with `null` defaults — required by FR-005's "Internal/test-only callers MAY pass null" clause.
- When `templateLoader` is null, the legacy fallback path runs unchanged (this is the path existing tests exercise).

## Type 4 — `AnalysisPresenter` (Spectra.CLI.Output)

| Aspect | Before | After |
|--------|--------|-------|
| `CategoryLabels` | `Dictionary<BehaviorCategory, string>` with 5 fixed entries | `Dictionary<string, string>` keyed by category ID, with the same 5 entries plus the 6 Spec 030 defaults; additional/unknown IDs render via fallback formatter `id.Replace('_', ' ').Replace('-', ' ')` |
| `RenderXxx(IReadOnlyList<BehaviorCategory>?)` parameter (line 65) | enum list | `IReadOnlyList<string>?` |

### Display rules
- A category ID without a label entry MUST render as a human-readable string by replacing `_` and `-` with spaces (e.g., `keyboard_interaction` → "keyboard interaction"). No exception, no warning.
- Order in display follows the order of keys in the breakdown dictionary as returned by the analyzer (no alphabetical re-sort) — preserves the AI's original ordering.

## Type 5 — `CountSelector` (Spectra.CLI.Interactive)

| Aspect | Before | After |
|--------|--------|-------|
| `SelectedCategories` property | `IReadOnlyList<BehaviorCategory>?` | `IReadOnlyList<string>?` |
| Internal `Categories` property (line 152) | `IReadOnlyList<BehaviorCategory>?` | `IReadOnlyList<string>?` |
| `CategoryLabels` dict | enum-keyed | string-keyed (same fallback formatter as `AnalysisPresenter`) |
| `analysis.Breakdown.OrderBy(...)` (line 97) | iterates enum keys | iterates string keys; ordering remains insertion order |

## Type 6 — `BehaviorCategory` enum (Spectra.Core.Models)

| Aspect | Before | After |
|--------|--------|-------|
| Existence | 5-value enum (`HappyPath`, `Negative`, `EdgeCase`, `Security`, `Performance`) with `[JsonStringEnumConverter]` | **DELETED** |

### Migration rules
- Every reference to `BehaviorCategory.HappyPath` becomes the literal string `"happy_path"`.
- Every reference to `BehaviorCategory.Negative` becomes `"negative"`.
- `BehaviorCategory.EdgeCase` → `"edge_case"`.
- `BehaviorCategory.Security` → `"security"`.
- `BehaviorCategory.Performance` → `"performance"`.
- Spec 030 defaults that were not in the legacy enum (`boundary`, `error_handling`) gain first-class display labels in `AnalysisPresenter.CategoryLabels` for nicer rendering.

## State transition: behavior category lifecycle

```
AI returns JSON                    [string identifier from prompt-allowed set or invented]
        │
        ▼
JSON deserialize                   [IdentifiedBehavior.Category : string, preserved verbatim]
        │
        ▼
GroupBy in AnalyzeAsync            [empty/null/whitespace → "uncategorized"; everything else preserved]
        │
        ▼
BehaviorAnalysisResult.Breakdown   [IReadOnlyDictionary<string,int>]
        │
        ├──────► AnalysisPresenter renders → CategoryLabels lookup → fallback formatter
        ├──────► CountSelector lists → CategoryLabels lookup → fallback formatter
        ├──────► JSON output → key/value preserved as-is
        └──────► SuggestionBuilder consumes → unchanged semantics
```

## Type-shape diff summary

| File | Lines changed (estimate) | Net production line delta |
|------|--------------------------|---------------------------|
| `IdentifiedBehavior.cs` | ~12 | -10 (drop derived getter) |
| `BehaviorAnalysisResult.cs` | ~4 | 0 |
| `BehaviorAnalyzer.cs` | ~30 | -5 (FilterByFocus simpler) |
| `AnalysisPresenter.cs` | ~10 | +5 (fallback formatter helper) |
| `CountSelector.cs` | ~10 | 0 |
| `GenerateHandler.cs` | ~6 (3 call sites × 2 lines each) | +6 |
| `BehaviorCategory.cs` | -16 | -16 (deleted) |
| **Total** | ~88 | ≈ −20 |

---

**Status**: Phase 1 data model complete.
