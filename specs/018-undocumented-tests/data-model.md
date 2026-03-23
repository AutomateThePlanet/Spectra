# Data Model: Undocumented Behavior Test Cases

**Feature**: 018-undocumented-tests
**Date**: 2026-03-23

## Modified Entities

### VerificationVerdict (enum extension)

**File**: `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs`

| Value | Existing | Description |
|-------|----------|-------------|
| Grounded | Yes | All claims traceable to documentation |
| Partial | Yes | Some claims verified, some unverified |
| Hallucinated | Yes | Invented behaviors — test rejected |
| **Manual** | **New** | User-described behavior, no documentation source |

**Validation rules**:
- `Manual` verdict tests skip critic verification entirely
- `Manual` verdict does not require `score`, `generator`, or `critic` fields

### GroundingFrontmatter (field extensions)

**File**: `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`

| Field | Type | YAML Alias | Existing | Description |
|-------|------|------------|----------|-------------|
| verdict | string? | `verdict` | Yes | Now accepts "manual" in addition to existing values |
| score | double | `score` | Yes | Not required for manual verdict |
| generator | string? | `generator` | Yes | Not required for manual verdict |
| critic | string? | `critic` | Yes | Not required for manual verdict |
| verified_at | string? | `verified_at` | Yes | Not required for manual verdict |
| unverified_claims | List\<string\> | `unverified_claims` | Yes | Not used for manual verdict |
| **source** | **string?** | **`source`** | **New** | Origin: "user-described", "doc-generated", "imported" |
| **created_by** | **string?** | **`created_by`** | **New** | Username of person who described the behavior |
| **note** | **string?** | **`note`** | **New** | Context about why behavior isn't documented |

**Conversion rules** (`ToMetadata()`):
- When `verdict == "manual"`: return `GroundingMetadata` with `Generator="user"`, `Critic="none"`, `Score=1.0`, `VerifiedAt=DateTimeOffset.UtcNow`
- The `source`, `created_by`, and `note` fields are serialized to YAML but not included in `GroundingMetadata` (frontmatter-only fields)

### DocumentationCoverage (field extensions)

**File**: `src/Spectra.Core/Models/Coverage/DocumentationCoverage.cs`

| Field | Type | Existing | Description |
|-------|------|----------|-------------|
| TotalDocs | int | Yes | Total documentation files |
| CoveredDocs | int | Yes | Docs with at least one test |
| Percentage | double | Yes | Coverage percentage |
| Details | List | Yes | Per-document detail |
| **UndocumentedTestCount** | **int** | **New** | Tests with empty source_refs |
| **UndocumentedTestIds** | **List\<string\>** | **New** | IDs of tests with empty source_refs |

### DocumentationSectionData (field extensions)

**File**: `src/Spectra.Core/Models/Dashboard/CoverageSummaryData.cs`

| Field | Type | Existing | Description |
|-------|------|----------|-------------|
| Covered | int | Yes | Covered doc count |
| Total | int | Yes | Total doc count |
| Percentage | double | Yes | Coverage percentage |
| Details | List | Yes | Per-doc detail |
| **UndocumentedTestCount** | **int** | **New** | Tests with no doc source |
| **UndocumentedTestIds** | **List\<string\>** | **New** | IDs for dashboard display |

## YAML Frontmatter Examples

### Undocumented test case (new)

```yaml
id: TC-205
priority: high
type: manual
tags: [payments, validation, iban]
component: payment-processing
source_refs: []
grounding:
  verdict: manual
  source: user-described
  created_by: angelovstanton
  note: "IBAN validation behavior — not yet documented"
```

### Documented test case (unchanged)

```yaml
id: TC-101
priority: medium
tags: [checkout]
component: checkout
source_refs: ["docs/checkout.md#Payment-Flow"]
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4-5
  critic: gemini-2.0-flash
  verified_at: "2026-03-20T10:00:00Z"
```

## State Transitions

### Manual Test Creation Flow

```
User describes behavior
    → Agent asks clarifying questions (if needed)
    → Agent checks duplicates (find_test_cases)
    → Agent generates draft
    → User reviews and approves
    → Test written with verdict: "manual"
    → Index updated
    → Reminder shown (consider documenting)
```

### Verdict in Verification Pipeline

```
Test has grounding.verdict?
    ├── "manual" → SKIP verification → include in write list
    ├── null → RUN verification → apply verdict from critic
    └── other → already verified → respect existing verdict
```
