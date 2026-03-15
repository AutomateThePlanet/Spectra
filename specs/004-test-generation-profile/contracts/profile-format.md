# Contract: Profile Format

**Feature**: 004-test-generation-profile
**Date**: 2026-03-15

## Overview

This document defines the format for SPECTRA test generation profile files. Profiles are stored as Markdown files with YAML frontmatter, consistent with the test case format.

## File Locations

| Profile Type | File Name | Location |
|--------------|-----------|----------|
| Repository Profile | `spectra.profile.md` | Repository root |
| Suite Override | `_profile.md` | Suite directory |

## File Format

### Structure

```markdown
---
# YAML frontmatter with profile configuration
profile_version: 1
options:
  detail_level: detailed
  # ... other options
---

# Profile Description (optional)

Human-readable documentation about this profile's purpose and choices.
```

### Complete Example

```markdown
---
profile_version: 1
created_at: 2026-03-15T10:30:00Z
updated_at: 2026-03-15T10:30:00Z
description: "Enterprise payment system test generation preferences"

options:
  detail_level: very_detailed
  min_negative_scenarios: 5
  default_priority: high

  formatting:
    step_format: numbered
    use_action_verbs: true
    include_screenshots: true
    max_steps_per_test: 15

  domain:
    domains:
      - payments
      - pii_gdpr
    pii_sensitivity: strict
    include_compliance_notes: true

  exclusions:
    - performance
    - load_testing
    - mobile_specific
---

# Payment System Test Profile

This profile configures test generation for our enterprise payment processing system.

## Rationale

### Detail Level: Very Detailed
Our QA team includes junior testers who benefit from granular step instructions.

### Domain Focus: Payments + PII/GDPR
Payment processing involves PCI compliance and customer data protection.

### Exclusions
- Performance/load testing handled by dedicated perf team
- Mobile testing uses separate automation framework
```

## YAML Schema

### Root Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| profile_version | integer | Yes | Schema version (currently 1) |
| created_at | datetime | No | ISO 8601 creation timestamp |
| updated_at | datetime | No | ISO 8601 last modified timestamp |
| description | string | No | Brief description of profile purpose |
| options | object | Yes | Profile configuration options |

### options Object

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| detail_level | string | No | `detailed` | `high_level`, `detailed`, or `very_detailed` |
| min_negative_scenarios | integer | No | `2` | Minimum negative test cases per feature (0-20) |
| default_priority | string | No | `medium` | `high`, `medium`, or `low` |
| formatting | object | No | (defaults) | Formatting preferences |
| domain | object | No | (defaults) | Domain-specific settings |
| exclusions | array | No | `[]` | Test categories to exclude |

### options.formatting Object

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| step_format | string | No | `numbered` | `bullets`, `numbered`, or `paragraphs` |
| use_action_verbs | boolean | No | `true` | Start steps with action verbs |
| include_screenshots | boolean | No | `false` | Suggest screenshot capture points |
| max_steps_per_test | integer | No | null | Maximum steps per test (1-50, null=unlimited) |

### options.domain Object

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| domains | array | No | `[]` | Active domain types |
| pii_sensitivity | string | No | `none` | `none`, `standard`, or `strict` |
| include_compliance_notes | boolean | No | `false` | Add compliance reminders |

### Domain Types

Valid values for `options.domain.domains`:

| Value | Description |
|-------|-------------|
| `payments` | Credit card, transactions, PCI DSS |
| `authentication` | Login, MFA, session management |
| `pii_gdpr` | Personal data, consent, deletion rights |
| `healthcare` | HIPAA, PHI handling |
| `financial` | Audit trails, SOX compliance |
| `accessibility` | WCAG, assistive technology |

### Exclusion Categories

Valid values for `options.exclusions`:

| Value | Description |
|-------|-------------|
| `performance` | Performance/timing tests |
| `load_testing` | Load and stress tests |
| `security` | Security-focused tests |
| `accessibility` | Accessibility tests |
| `mobile_specific` | Mobile-only scenarios |
| `api_only` | API-specific tests |
| `ui_only` | UI-specific tests |
| `edge_cases` | Unusual edge case scenarios |
| `negative` | Negative/error scenarios |
| `localization` | i18n/l10n tests |

## Suite Override Format

Suite-level profiles use the same format but only specify options to override. Unspecified options inherit from the repository profile.

### Suite Override Example

```markdown
---
profile_version: 1
description: "Override for payment-critical checkout suite"

options:
  detail_level: very_detailed
  min_negative_scenarios: 8

  domain:
    domains:
      - payments
    include_compliance_notes: true
---

# Checkout Suite Profile

This suite tests checkout flow and requires extra detail and negative scenarios.
```

### Merge Behavior

When a suite profile exists:
1. Load repository profile (or defaults if none exists)
2. Load suite profile
3. For each option category in suite profile:
   - Replace the entire category from repo profile
   - Categories not specified in suite profile retain repo values

**Example**:
- Repo profile: `detail_level: detailed`, `formatting: {step_format: numbered, use_action_verbs: true}`
- Suite profile: `detail_level: very_detailed`
- Result: `detail_level: very_detailed`, `formatting: {step_format: numbered, use_action_verbs: true}`

The `formatting` category was not overridden, so it's inherited completely.

## Validation Rules

### Required
- `profile_version` must be present and equal to `1`
- `options` must be present (even if empty object)

### Value Constraints
- `detail_level`: Must be one of `high_level`, `detailed`, `very_detailed`
- `min_negative_scenarios`: Integer 0-20
- `default_priority`: Must be one of `high`, `medium`, `low`
- `step_format`: Must be one of `bullets`, `numbered`, `paragraphs`
- `max_steps_per_test`: Integer 1-50 or null
- `pii_sensitivity`: Must be one of `none`, `standard`, `strict`
- `domains`: Array of valid domain type strings
- `exclusions`: Array of valid exclusion category strings, max 20 items

### Warning Conditions (non-fatal)
- Unknown fields in `options` (ignored with warning)
- Unknown values in `domains` array (item ignored with warning)
- Unknown values in `exclusions` array (item ignored with warning)

### Error Conditions (fatal)
- Invalid YAML syntax
- Missing `profile_version`
- Invalid `profile_version` value
- Missing `options` object
- Type mismatches (e.g., string where integer expected)

## AI Context Integration

When loaded, the profile is converted to an AI context prompt section:

```markdown
## Test Generation Profile

Generate tests following these preferences:

**Detail Level**: Very Detailed
- Include granular step-by-step instructions
- Assume no prior knowledge of the system
- Specify exact UI elements, values, and expected results

**Formatting**:
- Use numbered steps (1. 2. 3.)
- Start each step with an action verb (Click, Enter, Verify, Navigate)
- Include screenshot capture suggestions

**Domain Considerations**:
- Payments: Include PCI DSS compliance checks
- PII/GDPR: Consider data protection, consent, deletion scenarios

**Test Case Requirements**:
- Default priority: High
- Minimum 5 negative scenarios per feature
- Maximum 15 steps per test case

**Exclusions** (do NOT generate):
- Performance tests
- Load testing scenarios
- Mobile-specific tests
```

## Migration

### Version 1 to Future Versions

When `profile_version` is less than the current supported version:

1. Detect during profile loading
2. Display warning: "Profile format version X is outdated. Run 'spectra init-profile --upgrade' to update."
3. Attempt to load with best-effort compatibility
4. Log any incompatible options that were ignored

Migration preserves user preferences while adding new default values for new options.
