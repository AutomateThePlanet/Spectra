# Quickstart: Customizable Root Prompt Templates

**Feature**: 030-prompt-templates | **Date**: 2026-04-10

## Getting Started

### 1. Initialize prompt templates

```bash
spectra init
# Creates .spectra/prompts/ with 5 default templates
```

If you already have a project, update to get templates:
```bash
spectra update-skills
# Creates any missing template files
```

### 2. View template status

```bash
spectra prompts list
# Shows all 5 templates with customization status
```

### 3. Customize a template

Edit any template in `.spectra/prompts/`:

```bash
# Open the behavior analysis template
code .spectra/prompts/behavior-analysis.md
```

Modify the prompt body (everything after the `---` frontmatter). Available placeholders are listed in the frontmatter.

### 4. Customize behavior categories

Edit `spectra.config.json`:

```json
{
  "analysis": {
    "categories": [
      { "id": "positive-path", "description": "Core flows work correctly" },
      { "id": "compliance", "description": "PSD2, GDPR regulatory requirements" },
      { "id": "security", "description": "Auth, input sanitization, data exposure" }
    ]
  }
}
```

### 5. Validate your changes

```bash
spectra prompts validate behavior-analysis
# Checks syntax, placeholder names, block closure
```

### 6. Generate with custom templates

```bash
spectra ai generate checkout
# Uses your customized templates automatically
```

### 7. Reset if needed

```bash
spectra prompts reset behavior-analysis   # Reset one template
spectra prompts reset --all               # Reset all templates
```

## Template Placeholders Quick Reference

| Template | Key Placeholders |
|----------|-----------------|
| behavior-analysis | `{{document_text}}`, `{{categories}}`, `{{focus_areas}}` |
| test-generation | `{{behaviors}}`, `{{count}}`, `{{profile_format}}` |
| criteria-extraction | `{{document_text}}`, `{{existing_criteria}}` |
| critic-verification | `{{test_case}}`, `{{source_document}}` |
| test-update | `{{test_case}}`, `{{current_source}}`, `{{criteria_changes}}` |
