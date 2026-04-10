# Contract: BehaviorAnalyzer Public/Internal API

**Feature**: 036-fix-analyzer-categories
**Date**: 2026-04-10

This is the contract for the consumers of `BehaviorAnalyzer` and the types it produces. It is internal-only (the analyzer is not part of any external API surface), but the contract still matters because three call sites in `GenerateHandler` and four test files depend on it.

## `BehaviorAnalyzer` constructor

```csharp
public BehaviorAnalyzer(
    SpectraProviderConfig? provider,
    Action<string>? onStatus = null,
    SpectraConfig? config = null,
    PromptTemplateLoader? templateLoader = null)
```

### Parameter contract

| Parameter | Required | Effect when null |
|-----------|----------|------------------|
| `provider` | No | AI calls fail at runtime; analysis returns null. (Same as today.) |
| `onStatus` | No | No progress callbacks emitted. (Same as today.) |
| `config` | No | Categories injected into the prompt fall back to the documented Spec 030 defaults via `PromptTemplateLoader.GetCategories(null)`. |
| `templateLoader` | No | Legacy hardcoded prompt is used (the existing `BuildAnalysisPrompt` legacy branch). Tests rely on this. |

### Calling contract

- Production callers in `GenerateHandler` MUST pass non-null `config` and `templateLoader`. (Verified by inspection during tasks; no automated enforcement.)
- Test callers MAY pass `null` for any combination.

## `IdentifiedBehavior` JSON contract

The AI returns objects shaped as:

```json
{ "category": "<string>", "title": "<string>", "source": "<string>" }
```

### Deserialization contract

- `category` is deserialized as a `string` (was already a string at the field level today, surfaced through `CategoryRaw`; the rename to `Category` is a public API change).
- An empty string, missing field, or null value MUST NOT cause deserialization to fail; the field accepts it as-is. The "uncategorized" substitution happens at grouping time, not at parse time.
- Any string the AI returns is accepted; there is no closed enum to validate against.

## `BehaviorAnalysisResult.Breakdown` contract

```csharp
public IReadOnlyDictionary<string, int> Breakdown { get; init; }
```

### Producer guarantees

- Keys are non-empty strings.
- Empty/null/whitespace AI categories are bucketed as `"uncategorized"`.
- Sum of values equals `TotalBehaviors`.
- Iteration order is the order in which categories first appear in the AI response (insertion order via `GroupBy` + `ToDictionary`).

### Consumer obligations

- Consumers MUST handle arbitrary string keys, not a fixed set.
- Consumers MUST NOT cast keys to a closed enum.
- Consumers MUST provide a fallback rendering for unknown keys (the standard fallback is `id.Replace('_', ' ').Replace('-', ' ')`).

## `FilterByFocus` contract

```csharp
internal static List<IdentifiedBehavior> FilterByFocus(
    List<IdentifiedBehavior> behaviors, string focusArea)
```

### Behavior

1. Tokenize `focusArea` on whitespace, `,`, `;`. Lowercase.
2. For each behavior, normalize `Category` by lowercasing and replacing `_` and `-` with spaces.
3. A behavior matches if **any** focus token is a substring of its normalized category.
4. If at least one behavior matches, return the matching subset.
5. If no behavior matches, return the input list unchanged (focus is then applied downstream by the generation prompt instead).

### Edge cases

| Input | Output |
|-------|--------|
| `focusArea = ""` | Returns input unchanged (after `IsNullOrWhiteSpace` check at the analyzer's caller — `AnalyzeAsync` only invokes `FilterByFocus` when focus is non-empty, so this is a defensive guarantee). |
| `focusArea = "happy"` and category list contains `"happy_path"` | Matches. |
| `focusArea = "keyboard"` and category list contains `"keyboard_interaction"` | Matches. |
| `focusArea = "asdf"` and no category contains "asdf" | Returns input unchanged. |
| Multiple focus tokens, some match, some don't | Returns the union of behaviors that any single token matched. |

## `GenerateHandler` call site contract

All three `BehaviorAnalyzer` instantiation sites in `GenerateHandler.cs` MUST pass `config` and a `PromptTemplateLoader`:

```csharp
var loader = new PromptTemplateLoader(currentDir);  // currentDir already exists in scope
var analyzer = new BehaviorAnalyzer(provider, onStatusCallback, config, loader);
```

The `currentDir` variable already exists at lines 183, 754, 1323, 1396 of the handler. Each of the three call sites is inside (or downstream of) one of those `currentDir` declarations, so capturing it is straightforward.

## Out of contract

- `CopilotGenerationAgent` and its `BuildFullPrompt` are not changed by this feature even though they have an analogous bug. Documented as a follow-up in research.md D8.
- The Spec 030 default category list is not changed by this feature.
- The shape of `analysis.categories` in `spectra.config.json` is not changed.
- The `PromptTemplateLoader` API is not changed.
- The dashboard's category breakdown rendering is not changed (out of scope per spec).
