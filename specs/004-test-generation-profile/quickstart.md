# Quickstart: Test Generation Profile

**Feature**: 004-test-generation-profile
**Date**: 2026-03-15

## Overview

This guide covers how to create and use test generation profiles to customize AI-generated test cases.

## Prerequisites

- SPECTRA CLI installed (`spectra` command available)
- Repository initialized with SPECTRA (`spectra.config.json` exists)

## Creating a Profile

### Interactive Mode

Run the profile initialization wizard:

```bash
spectra init-profile
```

The wizard guides you through all options:

```
SPECTRA Test Generation Profile Setup

This wizard will help you create a profile that customizes how
AI generates test cases for your repository.

[1/7] Detail Level
How detailed should test steps be?
  1. High-level - Brief steps, assumes tester knowledge
  2. Detailed - Comprehensive steps with expected results
  3. Very detailed - Granular steps, no assumptions
> 2

[2/7] Minimum Negative Scenarios
How many negative test cases per feature (minimum)?
> 3

[3/7] Default Priority
What should be the default priority for generated tests?
  1. High (P1)
  2. Medium (P2)
  3. Low (P3)
> 2

...

Profile created: spectra.profile.md
```

### Non-Interactive Mode (CI/Automation)

Create a profile with command-line flags:

```bash
spectra init-profile \
  --non-interactive \
  --detail-level detailed \
  --min-negative 3 \
  --default-priority medium \
  --step-format numbered \
  --domains payments,pii_gdpr \
  --exclude performance,load_testing
```

### From Template

Start with a template and customize:

```bash
spectra init-profile --template enterprise
```

Available templates:
- `minimal` - Basic defaults
- `enterprise` - Detailed steps, compliance focus
- `agile` - High-level, fast iteration
- `regulated` - Maximum detail, audit trail

## Viewing Current Profile

See what profile is currently active:

```bash
spectra profile show
```

Output:

```
Effective Profile
=================
Source: spectra.profile.md (repository)

Detail Level:       Detailed
Negative Scenarios: 3 minimum
Default Priority:   Medium

Formatting:
  Step Format:      Numbered
  Action Verbs:     Yes
  Screenshots:      No

Domains:
  - Payments
  - PII/GDPR
  PII Sensitivity:  Standard
  Compliance Notes: Yes

Exclusions:
  - performance
  - load_testing
```

### From Suite Directory

When in a suite directory with an override:

```bash
cd tests/checkout
spectra profile show
```

```
Effective Profile
=================
Source: _profile.md (suite override)
Inherits: spectra.profile.md (repository)

Detail Level:       Very Detailed (override)
Negative Scenarios: 5 minimum (override)
Default Priority:   High (override)
...
```

## Creating Suite Overrides

Create an override for a specific test suite:

```bash
cd tests/checkout
spectra init-profile --suite
```

Or create manually:

```bash
# tests/checkout/_profile.md
```

```markdown
---
profile_version: 1
description: "Extra detail for critical checkout tests"

options:
  detail_level: very_detailed
  min_negative_scenarios: 5
  default_priority: high
---

# Checkout Suite Override

This suite tests payment checkout and requires extra scrutiny.
```

## Profile Options Reference

### Detail Level

| Value | Description |
|-------|-------------|
| `high_level` | Brief steps for experienced testers |
| `detailed` | Standard comprehensive steps |
| `very_detailed` | Granular steps, no assumptions |

### Step Format

| Value | Example |
|-------|---------|
| `bullets` | `- Click Submit` |
| `numbered` | `1. Click Submit` |
| `paragraphs` | Prose description |

### Domains

| Domain | Adds considerations for |
|--------|------------------------|
| `payments` | PCI DSS, card handling, refunds |
| `authentication` | MFA, sessions, password rules |
| `pii_gdpr` | Consent, data access, deletion |
| `healthcare` | HIPAA, PHI protection |
| `financial` | Audit trails, SOX compliance |
| `accessibility` | WCAG, screen readers |

### Exclusions

Categories you can exclude from generation:

- `performance` - Performance tests
- `load_testing` - Load/stress tests
- `security` - Security tests
- `accessibility` - A11y tests
- `mobile_specific` - Mobile-only tests
- `api_only` - API tests
- `ui_only` - UI tests
- `edge_cases` - Edge case scenarios
- `negative` - Error/failure scenarios
- `localization` - i18n/l10n tests

## How Profiles Affect Generation

When you run test generation:

```bash
spectra ai generate --from docs/checkout.md
```

The profile is automatically loaded and applied:

1. SPECTRA checks for `_profile.md` in the target suite directory
2. Falls back to `spectra.profile.md` at repository root
3. Falls back to built-in defaults if no profile exists
4. Profile content is added to the AI context

### Example: Without Profile

```markdown
## TC-101: User Login

**Steps:**
1. Go to login page
2. Enter credentials
3. Click Login
4. Verify success
```

### Example: With Detailed Profile

```markdown
## TC-101: User Login

**Priority:** High
**Component:** Authentication

**Preconditions:**
- User account exists with valid credentials
- Browser cache cleared

**Steps:**
1. Navigate to the application login page at /login
2. Verify the login form displays username and password fields
3. Enter the test username "testuser@example.com" in the username field
4. Enter the test password in the password field
5. Click the "Sign In" button
6. Verify redirect to dashboard page within 3 seconds
7. Verify welcome message displays the user's name

**Expected Result:**
User is logged in and sees personalized dashboard.

**Compliance Notes:**
- Password field should mask input
- Failed login should not reveal whether username exists
```

## Updating a Profile

### Re-run Wizard

```bash
spectra init-profile
```

When a profile exists, you're prompted:

```
Profile already exists: spectra.profile.md

What would you like to do?
  1. Overwrite with new profile
  2. Edit existing profile
  3. Cancel
> 2
```

### Manual Edit

Profiles are human-readable Markdown. Edit directly:

```bash
code spectra.profile.md
```

## Validation

Check if your profile is valid:

```bash
spectra validate --profile
```

```
Validating profile: spectra.profile.md
  ✓ YAML syntax valid
  ✓ Required fields present
  ✓ Option values valid
  ! Warning: Unknown exclusion 'legacy_browser' (will be ignored)

Profile is valid with 1 warning.
```

## Troubleshooting

### "No profile found"

Profile is optional. Generation works with defaults. Create one with:

```bash
spectra init-profile
```

### "Profile format outdated"

Your profile uses an old format version. Update it:

```bash
spectra init-profile --upgrade
```

### Suite override not applying

Ensure the file is named `_profile.md` (with leading underscore) and is in the suite directory.

### Unknown options warning

Unknown options are ignored but generate warnings. Remove them or check spelling.

## Best Practices

1. **Start with repository profile** - Establish team-wide defaults first
2. **Use suite overrides sparingly** - Only when a suite truly needs different settings
3. **Document your choices** - Use the Markdown body to explain rationale
4. **Version control profiles** - Commit `spectra.profile.md` and `_profile.md` files
5. **Review periodically** - Update profiles as team preferences evolve
