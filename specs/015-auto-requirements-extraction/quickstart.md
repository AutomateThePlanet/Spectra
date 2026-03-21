# Quickstart: Automatic Requirements Extraction

**Feature**: 015-auto-requirements-extraction

## What This Feature Does

Automatically extracts testable requirements from project documentation using AI, writes them to a YAML file, and links generated tests to those requirements. Eliminates the need to manually author `_requirements.yaml`.

## Usage

### Standalone Extraction (scan all docs, build requirements file)

```bash
# Extract requirements from all documentation
spectra ai analyze --extract-requirements

# Preview what would be extracted (no file writes)
spectra ai analyze --extract-requirements --dry-run
```

### Integrated with Test Generation (extract + generate in one step)

```bash
# Interactive mode — review extracted requirements before saving
spectra ai generate

# Direct mode — extracts automatically, then generates tests
spectra ai generate checkout

# CI mode — fully automatic, no prompts
spectra ai generate checkout --no-interaction
```

### Check Coverage After Extraction

```bash
# Coverage analysis now works out of the box
spectra ai analyze --coverage
```

## How It Works

1. **Document scanning**: Loads documentation files from `docs/` (per `source` config)
2. **AI extraction**: Sends document content to the configured AI model with instructions to identify testable behaviors
3. **Deduplication**: Compares extracted titles against existing requirements; skips duplicates
4. **ID allocation**: Assigns sequential REQ-NNN IDs continuing from the highest existing ID
5. **Priority inference**: AI assigns high/medium/low based on RFC 2119 keywords in the source text
6. **Merge & write**: New requirements are appended to `docs/requirements/_requirements.yaml`
7. **Test linking**: During generation, tests include `requirements: [REQ-001, ...]` in frontmatter

## Configuration

No new configuration needed. Uses existing settings in `spectra.config.json`:

```json
{
  "coverage": {
    "requirements_file": "docs/requirements/_requirements.yaml"
  }
}
```

## Output Example

After running `spectra ai analyze --extract-requirements` against docs:

```yaml
# docs/requirements/_requirements.yaml
requirements:
  - id: REQ-001
    title: "User can log in with valid credentials"
    source: docs/authentication.md
    priority: high
  - id: REQ-002
    title: "Account locks after 5 consecutive failed login attempts"
    source: docs/authentication.md
    priority: high
  - id: REQ-003
    title: "Password reset email arrives within 30 seconds"
    source: docs/authentication.md
    priority: medium
```

Generated test frontmatter automatically includes:

```yaml
---
id: TC-042
title: "Verify account lockout after failed logins"
requirements:
  - REQ-002
---
```
