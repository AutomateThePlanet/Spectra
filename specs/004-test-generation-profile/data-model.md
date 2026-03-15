# Data Model: Test Generation Profile

**Feature**: 004-test-generation-profile
**Date**: 2026-03-15

## Entities

### Profile Domain

#### GenerationProfile

The root entity representing a complete test generation profile.

| Field | Type | Description |
|-------|------|-------------|
| profile_version | int | Profile format version (currently 1) |
| created_at | DateTime | When profile was created |
| updated_at | DateTime | When profile was last modified |
| description | string? | Human-readable description of profile purpose |
| options | ProfileOptions | All configuration options |

#### ProfileOptions

Container for all profile configuration options.

| Field | Type | Description |
|-------|------|-------------|
| detail_level | DetailLevel | Level of detail for test steps |
| min_negative_scenarios | int | Minimum negative test cases per feature (default: 2) |
| default_priority | Priority | Default priority for generated tests |
| formatting | FormattingOptions | Test formatting preferences |
| domain | DomainOptions | Domain-specific generation settings |
| exclusions | string[] | Categories of tests NOT to generate |

#### DetailLevel (enum)

Level of detail for generated test steps.

```
HighLevel       # Brief steps, assumes tester knowledge
Detailed        # Comprehensive steps with expected results
VeryDetailed    # Granular steps, no assumptions
```

#### Priority (enum)

Default priority for generated test cases.

```
High            # P1 - Critical functionality
Medium          # P2 - Standard functionality (default)
Low             # P3 - Nice-to-have coverage
```

#### FormattingOptions

Preferences for how test content is formatted.

| Field | Type | Description |
|-------|------|-------------|
| step_format | StepFormat | Bullet points vs numbered vs paragraphs |
| use_action_verbs | bool | Start steps with action verbs (Click, Enter, Verify) |
| include_screenshots | bool | Suggest screenshot capture points |
| max_steps_per_test | int? | Maximum steps per test case (null = unlimited) |

#### StepFormat (enum)

Format for test steps.

```
Bullets         # - Step 1\n- Step 2
Numbered        # 1. Step 1\n2. Step 2
Paragraphs      # Prose format
```

#### DomainOptions

Domain-specific generation preferences.

| Field | Type | Description |
|-------|------|-------------|
| domains | DomainType[] | Active domains requiring special handling |
| pii_sensitivity | PiiSensitivity | Level of PII/GDPR consideration |
| include_compliance_notes | bool | Add compliance reminders to relevant tests |

#### DomainType (enum)

Specialized domains that affect test generation.

```
Payments        # Credit card, transactions, PCI considerations
Authentication  # Login, MFA, session management
PiiGdpr         # Personal data handling, consent, deletion
Healthcare      # HIPAA, PHI considerations
Financial       # Audit trails, regulations
Accessibility   # WCAG, screen reader considerations
```

#### PiiSensitivity (enum)

Level of PII/GDPR handling in tests.

```
None            # No PII considerations
Standard        # Basic PII awareness
Strict          # Full GDPR/privacy compliance focus
```

### Profile Resolution Domain

#### EffectiveProfile

The resolved profile after applying inheritance rules.

| Field | Type | Description |
|-------|------|-------------|
| source | ProfileSource | Where this profile came from |
| profile | GenerationProfile | The resolved profile options |
| inheritance_chain | ProfileSource[] | Order of profiles applied |

#### ProfileSource

Identifies the origin of a profile.

| Field | Type | Description |
|-------|------|-------------|
| type | SourceType | Repository or suite level |
| path | string | File path to the profile |
| exists | bool | Whether the file exists |

#### SourceType (enum)

```
Repository      # spectra.profile.md at repo root
Suite           # _profile.md in suite folder
Default         # Built-in defaults (no file)
```

### Validation Domain

#### ProfileValidationResult

Result of validating a profile file.

| Field | Type | Description |
|-------|------|-------------|
| is_valid | bool | Whether profile passed validation |
| errors | ValidationError[] | Critical errors that block usage |
| warnings | ValidationWarning[] | Non-critical issues |
| profile | GenerationProfile? | Parsed profile if valid |

#### ValidationError

A critical validation error.

| Field | Type | Description |
|-------|------|-------------|
| code | string | Error code (e.g., "INVALID_YAML") |
| message | string | Human-readable error description |
| line | int? | Line number if applicable |
| field | string? | Field path if applicable |

#### ValidationWarning

A non-critical validation warning.

| Field | Type | Description |
|-------|------|-------------|
| code | string | Warning code (e.g., "UNKNOWN_OPTION") |
| message | string | Human-readable warning description |
| field | string? | Field path if applicable |
| default_used | string? | Default value being used |

### Questionnaire Domain

#### QuestionnaireState

Tracks progress through the interactive questionnaire.

| Field | Type | Description |
|-------|------|-------------|
| current_step | int | Current question index (0-based) |
| total_steps | int | Total number of questions |
| answers | Dictionary<string, object> | Collected answers by question key |
| completed | bool | Whether questionnaire is complete |

#### Question

A single questionnaire question.

| Field | Type | Description |
|-------|------|-------------|
| key | string | Unique question identifier |
| text | string | Question text to display |
| type | QuestionType | Type of response expected |
| options | string[]? | Valid options for choice questions |
| default_value | object? | Default if user skips |
| help_text | string? | Additional context for the question |

#### QuestionType (enum)

```
SingleChoice    # Select one from options
MultiChoice     # Select multiple from options
Number          # Enter a numeric value
YesNo           # Boolean yes/no
Text            # Free-form text input
```

## State Transitions

### Profile Lifecycle

```
(No Profile)
  ↓ spectra init-profile
Created (spectra.profile.md exists)
  ↓ spectra init-profile (with existing)
Updated (user chooses to overwrite)
  ↓ spectra profile show
Displayed (read-only view)
```

### Validation Flow

```
File Read
  ↓ parse YAML frontmatter
Parse Success → Validate Schema
  ↓                    ↓
Parse Failure     Schema Valid → Check Values
  ↓                    ↓              ↓
ValidationError   SchemaError    Values Valid
                       ↓              ↓
                  ValidationError  Warnings + Profile
```

### Profile Resolution

```
Start: suite path provided
  ↓ check suite/_profile.md
Exists?
  Yes → Load suite profile as base
  No  → Continue
  ↓ check repo/spectra.profile.md
Exists?
  Yes → Load repo profile (merge if suite exists)
  No  → Continue
  ↓ use built-in defaults
Return EffectiveProfile with inheritance chain
```

## Validation Rules

1. **profile_version**: Must be integer >= 1
2. **detail_level**: Must be valid enum value
3. **min_negative_scenarios**: Must be integer >= 0, <= 20
4. **default_priority**: Must be valid enum value
5. **step_format**: Must be valid enum value
6. **max_steps_per_test**: If present, must be integer >= 1, <= 50
7. **domains**: Must be array of valid enum values
8. **exclusions**: Must be array of strings, max 20 items
9. **File encoding**: Must be UTF-8
10. **YAML syntax**: Must be valid YAML in frontmatter

## Relationships

```
Repository 1 ←──────── 0..1 has ──────────── 1 RepositoryProfile
Suite 1 ←──────────── 0..1 has ──────────── 1 SuiteProfile
SuiteProfile 1 ←────── 0..1 overrides ────── 1 RepositoryProfile
EffectiveProfile 1 ←── 1 resolves_from ───── N ProfileSource
GenerationProfile 1 ←─ 1 contains ─────────── 1 ProfileOptions
```

## Default Values

| Option | Default |
|--------|---------|
| detail_level | Detailed |
| min_negative_scenarios | 2 |
| default_priority | Medium |
| step_format | Numbered |
| use_action_verbs | true |
| include_screenshots | false |
| max_steps_per_test | null (unlimited) |
| domains | [] (none) |
| pii_sensitivity | None |
| include_compliance_notes | false |
| exclusions | [] (none) |
