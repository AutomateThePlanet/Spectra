# Data Model: ISTQB Test Design Techniques

**Feature**: 037-istqb-test-techniques
**Date**: 2026-04-10

## Overview

This feature adds two model fields and one in-memory aggregation map. No new entities, no schema migrations, no database changes. All additions are backward compatible (defaults preserve existing parsing of legacy data).

## Entities

### IdentifiedBehavior (modified)

**File**: `src/Spectra.CLI/Agent/Analysis/IdentifiedBehavior.cs`

| Field | Type | JSON name | Default | Notes |
|-------|------|-----------|---------|-------|
| Category | string | `category` | `""` | Existing — one of configured categories |
| Title | string | `title` | `""` | Existing — short description, max 80 chars |
| Source | string | `source` | `""` | Existing — originating document path |
| **Technique** | **string** | **`technique`** | **`""`** | **NEW — one of `EP`, `BVA`, `DT`, `ST`, `EG`, `UC`, or empty** |

**Validation**: None. The field is informational. Empty string is acceptable (legacy responses).

**Backward compatibility**: AI responses lacking the field deserialize to `Technique = ""`. The field is round-tripped through `System.Text.Json` with default settings.

---

### AcceptanceCriterion (modified)

**File**: `src/Spectra.Core/Models/AcceptanceCriterion.cs`

| Field | Type | JSON / YAML name | Default | Notes |
|-------|------|------------------|---------|-------|
| (existing fields) | — | — | — | Unchanged |
| **TechniqueHint** | **string?** | **`technique_hint`** | **`null`** | **NEW — `BVA`, `EP`, `DT`, `ST`, or null** |

**Validation**: None. Optional, free-form short code.

**Backward compatibility**: Existing `.criteria.yaml` files do not contain the field. They deserialize with `TechniqueHint = null`. When writing, YAML writers omit the field if null (existing convention for nullable strings on this model).

---

### BehaviorAnalysisResult (modified)

**File**: `src/Spectra.CLI/Agent/Analysis/BehaviorAnalysisResult.cs`

| Field | Type | Notes |
|-------|------|-------|
| Behaviors | `List<IdentifiedBehavior>` | Existing |
| Breakdown | `Dictionary<string,int>` | Existing — counts by category |
| **TechniqueBreakdown** | **`Dictionary<string,int>`** | **NEW — counts by technique short code** |

**Computation**: Populated in `BehaviorAnalyzer` after parsing the AI response, by grouping behaviors by their `Technique` field. Empty techniques are grouped under key `""` (or omitted — see below).

**Empty-technique handling**: If a behavior's `Technique` is `""`, it is excluded from `TechniqueBreakdown` entirely. This keeps the map clean for callers and ensures legacy responses produce an empty map rather than a `{"": 152}` entry.

**Display order at render time**: Fixed sequence `BVA, EP, DT, ST, EG, UC`. Unknown keys (e.g., a future `PW` for pairwise) render alphabetically after the known set.

---

### GenerateResult.AnalysisSection (modified)

**File**: `src/Spectra.CLI/Results/GenerateResult.cs`

The existing `analysis` subobject in `.spectra-result.json` gains one field:

```json
{
  "analysis": {
    "total_behaviors": 141,
    "already_covered": 0,
    "recommended": 141,
    "breakdown": { "happy_path": 42, "boundary": 38, "...": "..." },
    "technique_breakdown": { "BVA": 38, "EP": 24, "UC": 32, "EG": 15, "DT": 18, "ST": 14 }
  }
}
```

**JSON name**: `technique_breakdown` (snake_case to match `breakdown` and other existing keys).

**Type**: `Dictionary<string,int>` serialized via `System.Text.Json` with the project's existing camelCase/snake_case policy (snake_case for keys via `JsonPropertyName`).

**Empty case**: When `TechniqueBreakdown` is empty, the field serializes as `{}` (not omitted). This makes the field's presence a stable contract for SKILL/CI consumers.

---

## State Transitions

None. This feature does not introduce stateful entities.

## Validation Rules

The only validation introduced is implicit in the prompt instructions (executed by the AI):

| Rule | Source | Enforcement |
|------|--------|-------------|
| Numeric range → ≥4 BVA behaviors | behavior-analysis.md | Prompt instruction (advisory) |
| Multi-condition rule → DT behaviors | behavior-analysis.md | Prompt instruction (advisory) |
| Workflow → ≥1 invalid ST | behavior-analysis.md | Prompt instruction (advisory) |
| No category exceeds 40% | behavior-analysis.md | Prompt instruction (advisory) |
| Each behavior carries a technique | behavior-analysis.md | Prompt instruction; parsing tolerates absence |

There is **no runtime validation** that rejects AI responses violating these rules. Adherence is observed via the success criteria, not enforced.

## Migration

Two on-disk file types are touched:

1. **`.spectra/prompts/*.md` (per-project)** — never auto-overwritten. Users opt in via `spectra prompts reset` or `spectra update-skills`.
2. **`.criteria.yaml` files** — not rewritten unless the user runs criteria extraction. New writes will include `technique_hint` when present; existing files load without the field.

No schema version bump. No database migration. No config schema change.
