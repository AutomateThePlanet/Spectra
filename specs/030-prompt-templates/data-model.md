# Data Model: Customizable Root Prompt Templates

**Feature**: 030-prompt-templates | **Date**: 2026-04-10

## Entities

### PromptTemplate

Represents a loaded prompt template with metadata and body.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| SpectraVersion | string | yes | Template format version (e.g., "1.0") |
| TemplateId | string | yes | Unique template identifier (e.g., "behavior-analysis") |
| Description | string | yes | Human-readable description of what this template controls |
| Placeholders | IReadOnlyList\<PlaceholderSpec\> | yes | Declared placeholders with names and descriptions |
| Body | string | yes | The prompt text with `{{placeholder}}` syntax |
| IsUserCustomized | bool | no | True if loaded from `.spectra/prompts/`, false if built-in default |

**Location**: `Spectra.Core/Models/PromptTemplate.cs`

**Validation rules**:
- SpectraVersion must be a non-empty string
- TemplateId must match one of the known template IDs
- Body must be non-empty
- Placeholders may be empty list but not null

---

### PlaceholderSpec

Declares a single placeholder used in a template.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | yes | Placeholder name (used in `{{name}}` syntax) |
| Description | string | no | Optional human-readable description |

**Location**: `Spectra.Core/Models/PromptTemplate.cs` (nested in same file)

---

### CategoryDefinition

A behavior category for test analysis, configurable per project.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | string | yes | Category identifier (e.g., "positive-path", "compliance") |
| Description | string | yes | Human-readable description guiding the AI |

**Location**: `Spectra.Core/Models/Config/CategoryDefinition.cs`

**Validation rules**:
- Id must be non-empty, lowercase, hyphen-separated (slug format)
- Description must be non-empty

---

### AnalysisConfig

Configuration section for behavior analysis settings.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Categories | IReadOnlyList\<CategoryDefinition\> | no | Custom behavior categories. Defaults to 6 built-in categories when absent. |

**Location**: `Spectra.Core/Models/Config/AnalysisConfig.cs`

**JSON path**: `analysis.categories` in `spectra.config.json`

---

### PromptsListResult

JSON result model for `spectra prompts list --output-format json`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Command | string | yes | Always "prompts list" |
| Status | string | yes | "success" or "error" |
| Templates | IReadOnlyList\<TemplateStatus\> | yes | Status of each template |

**TemplateStatus fields**:

| Field | Type | Description |
|-------|------|-------------|
| TemplateId | string | Template identifier |
| Status | string | "customized", "default", or "missing" |
| FilePath | string? | Path to template file (null if missing) |
| Description | string | Template description |

**Location**: `Spectra.CLI/Results/PromptsListResult.cs`

---

## Relationships

```text
SpectraConfig
  └── AnalysisConfig (new "analysis" property)
        └── CategoryDefinition[] (0..N categories)

PromptTemplate
  └── PlaceholderSpec[] (0..N declared placeholders)

SkillsManifest
  └── Files (extended with prompt template paths)
```

## Config Schema Extension

```json
{
  "analysis": {
    "categories": [
      { "id": "positive-path", "description": "Core functionality works as documented" },
      { "id": "negative-path", "description": "System handles invalid input correctly" },
      { "id": "edge-case", "description": "Unusual but valid scenarios" },
      { "id": "boundary", "description": "Values at exact limits" },
      { "id": "error-handling", "description": "Appropriate errors and recovery" },
      { "id": "security", "description": "Authorization, sanitization, exposure prevention" }
    ]
  }
}
```

## Template File Format

```markdown
---
spectra_version: "1.0"
template_id: behavior-analysis
description: "How SPECTRA analyzes documentation to identify testable behaviors"
placeholders:
  - name: document_text
    description: "Full text of the document being analyzed"
  - name: categories
    description: "Configured behavior categories from spectra.config.json"
---

<!-- This comment is stripped before sending to AI -->

You are a senior QA architect analyzing product documentation...

{{document_text}}

{{#each categories}}
- **{{id}}**: {{description}}
{{/each}}
```

## Default Categories (6)

| Id | Description |
|----|-------------|
| positive-path | Core functionality works as documented under normal conditions |
| negative-path | System handles invalid, missing, or malformed input correctly |
| edge-case | Unusual but valid scenarios: empty collections, max-length strings, concurrent access |
| boundary | Values at exact limits: file size = 10MB, password length = 8, count = 0 and max |
| error-handling | System returns appropriate errors, logs failures, and recovers gracefully |
| security | Authorization checks, input sanitization, data exposure prevention |

## Known Template IDs

| Template ID | Maps to | Placeholders |
|-------------|---------|-------------|
| behavior-analysis | BehaviorAnalyzer.BuildAnalysisPrompt | document_text, document_title, existing_tests, suite_name, focus_areas, acceptance_criteria, categories |
| test-generation | GenerationAgent.BuildFullPrompt | behaviors, suite_name, existing_tests, acceptance_criteria, profile_format, count, focus_areas |
| criteria-extraction | CriteriaExtractor.BuildExtractionPrompt | document_text, document_title, existing_criteria |
| critic-verification | CriticPromptBuilder.BuildSystemPrompt | test_case, source_document, acceptance_criteria |
| test-update | UpdateHandler AI rewrite call | test_case, original_source, current_source, criteria_changes |
