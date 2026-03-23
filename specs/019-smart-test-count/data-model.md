# Data Model: Smart Test Count Recommendation

**Feature**: 019-smart-test-count
**Date**: 2026-03-23

## Entities

### BehaviorCategory (Enum)

Categorization of testable behaviors identified from documentation.

| Value | Description |
|-------|-------------|
| HappyPath | Normal successful user flows |
| Negative | Error handling, invalid inputs, failure scenarios |
| EdgeCase | Boundary conditions, unusual combinations, limits |
| Security | Permission checks, access control, authentication |
| Performance | Load, timeout, concurrent access |

**Location**: `Spectra.Core/Models/BehaviorCategory.cs`
**Serialization**: snake_case strings in JSON (`happy_path`, `negative`, `edge_case`, `security`, `performance`)

---

### IdentifiedBehavior (Record)

A single testable behavior identified by the AI analysis.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Category | BehaviorCategory | Yes | Which category this behavior belongs to |
| Title | string | Yes | Short description, max 80 characters |
| Source | string | Yes | Source document path (e.g., "docs/checkout-flow.md") |

**Location**: `Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs`

---

### BehaviorAnalysisResult (Record)

Complete result of the documentation analysis step.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| TotalBehaviors | int | Yes | Total distinct testable behaviors found |
| Breakdown | Dictionary<BehaviorCategory, int> | Yes | Count per category |
| Behaviors | IReadOnlyList<IdentifiedBehavior> | Yes | Full list of identified behaviors |
| AlreadyCovered | int | Yes | Count of behaviors matching existing tests |
| RecommendedCount | int | Yes | TotalBehaviors - AlreadyCovered |
| DocumentsAnalyzed | int | Yes | Number of source documents scanned |
| TotalWords | int | Yes | Approximate word count of analyzed documentation |

**Derived fields**:
- `RecommendedCount` = `TotalBehaviors` - `AlreadyCovered`
- `RemainingByCategory`: Computed by subtracting covered behaviors per category

**Location**: `Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs`

---

### CountSelection (Record)

User's choice from the interactive menu.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Count | int | Yes | Number of tests to generate |
| SelectedCategories | IReadOnlyList<BehaviorCategory>? | No | If user selected specific categories (null = all) |
| FreeTextDescription | string? | No | If user chose "describe what I want" |

**Location**: `Spectra.CLI/Interactive/CountSelector.cs` (nested record)

## Relationships

```
BehaviorAnalysisResult
  ├── contains many IdentifiedBehavior
  ├── summarized by Breakdown (BehaviorCategory → count)
  └── consumed by CountSelector → produces CountSelection
       └── count passed to existing GenerateTestsAsync(count)
```

## State Transitions

No new state machine. The analysis result is session-scoped (not persisted) and flows linearly:

```
Documents loaded → BehaviorAnalyzer.AnalyzeAsync() → BehaviorAnalysisResult
  → AnalysisPresenter.Display()
  → CountSelector.SelectAsync() [interactive only]
  → count resolved → existing generation flow
  → AnalysisPresenter.DisplayGapNotification() [if partial]
```

## JSON Schema (AI Response)

The AI analysis call returns JSON matching this schema:

```json
{
  "total": 18,
  "breakdown": {
    "happy_path": 8,
    "negative": 6,
    "edge_case": 3,
    "security": 1
  },
  "behaviors": [
    {
      "category": "happy_path",
      "title": "Successful checkout with credit card",
      "source": "docs/checkout-flow.md"
    }
  ]
}
```

Parsing uses `System.Text.Json` with `JsonPropertyNameAttribute` for snake_case mapping, consistent with existing JSON parsing patterns in the codebase.
