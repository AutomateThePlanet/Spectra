# Generation Profiles

Customize how AI generates test cases with repository-level and suite-level profiles.

Related: [CLI Reference](cli-reference.md) | [Test Format](test-format.md) | [Configuration](configuration.md)

---

## Overview

Profiles control test generation style: detail level, negative scenario count, step formatting, domain-specific considerations, and more. They're optional but recommended for consistent output.

## Create a Profile

### Interactive Mode (Recommended)

```bash
spectra init-profile
```

The wizard guides you through options:

```
SPECTRA Test Generation Profile Setup

[1/7] Detail Level
How detailed should test steps be?
  1. High-level - Brief steps, assumes tester knowledge
  2. Detailed - Comprehensive steps with expected results
  3. Very detailed - Granular steps, no assumptions
> 2

[2/7] Minimum Negative Scenarios
How many negative test cases per feature (minimum)?
> 3

...

Profile created: spectra.profile.md
```

### Non-Interactive Mode (CI/Scripts)

```bash
spectra init-profile \
  --non-interactive \
  --detail-level detailed \
  --min-negative 3 \
  --priority medium \
  --step-format numbered \
  --domains payments,authentication \
  --pii-sensitivity standard
```

## View Current Profile

```bash
spectra profile show              # Show effective profile
spectra profile show --json       # Show as JSON
spectra profile show --context    # Show AI context that would be generated
spectra profile show --suite tests/checkout  # Show profile for a specific suite
```

## Suite-Level Overrides

Create a profile that only applies to a specific test suite:

```bash
spectra init-profile --suite tests/checkout
```

This creates `tests/checkout/_profile.md` that inherits from and overrides the repository profile.

## Profile Resolution Order

When running `spectra ai generate checkout --count 10`:

1. `tests/checkout/_profile.md` (suite override)
2. `spectra.profile.md` (repository profile)
3. Built-in defaults

The profile content is injected into the AI prompt context.

## Profile Options

| Option | Values | Description |
|--------|--------|-------------|
| `--detail-level` | `high_level`, `detailed`, `very_detailed` | How granular steps should be |
| `--min-negative` | `1-10` | Minimum negative scenarios per feature |
| `--priority` | `high`, `medium`, `low` | Default test priority |
| `--step-format` | `numbered`, `bullets`, `paragraphs` | How steps are formatted |
| `--action-verbs` | `true/false` | Start steps with action verbs |
| `--screenshots` | `true/false` | Include screenshot suggestions |
| `--domains` | `payments`, `authentication`, `pii_gdpr`, `healthcare`, `financial`, `accessibility` | Domain-specific considerations |
| `--pii-sensitivity` | `none`, `standard`, `strict` | PII handling level |
| `--exclusions` | `performance`, `security`, `edge_cases`, etc. | Categories to exclude |

## Edit Existing Profile

```bash
spectra init-profile --edit --min-negative 5 --priority high   # Update specific options
spectra init-profile --force                                    # Re-run full wizard
```

## Effect on Generated Tests

### Without Profile

```markdown
## TC-101: User Login

**Steps:**
1. Go to login page
2. Enter credentials
3. Click Login
```

### With Detailed Profile + Domains

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

**Expected Result:**
User is logged in and sees personalized dashboard.

**Compliance Notes:**
- Password field should mask input
- Failed login should not reveal whether username exists
```
