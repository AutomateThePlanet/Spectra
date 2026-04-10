# CLI Contract: spectra prompts

**Feature**: 030-prompt-templates | **Date**: 2026-04-10

## Commands

### spectra prompts list

List all prompt templates with their customization status.

```
spectra prompts list [--output-format human|json] [--no-interaction]
```

**Output (human)**:
```
behavior-analysis    ✓ customized    .spectra/prompts/behavior-analysis.md
test-generation      ○ default       .spectra/prompts/test-generation.md
criteria-extraction  ○ default       .spectra/prompts/criteria-extraction.md
critic-verification  ✗ missing       (using built-in)
test-update          ○ default       .spectra/prompts/test-update.md
```

**Output (JSON)**:
```json
{
  "command": "prompts list",
  "status": "success",
  "timestamp": "2026-04-10T12:00:00Z",
  "templates": [
    {
      "templateId": "behavior-analysis",
      "status": "customized",
      "filePath": ".spectra/prompts/behavior-analysis.md",
      "description": "How SPECTRA analyzes documentation to identify testable behaviors"
    }
  ]
}
```

**Exit codes**: 0 (success), 1 (error)

---

### spectra prompts show

Display a template's content.

```
spectra prompts show <template-id> [--raw] [--no-interaction]
```

**Arguments**:
- `template-id` (required): One of: behavior-analysis, test-generation, criteria-extraction, critic-verification, test-update

**Options**:
- `--raw`: Show template with `{{placeholders}}` unresolved (default behavior since there's no runtime context to resolve against)

**Exit codes**: 0 (success), 1 (template not found)

---

### spectra prompts reset

Reset a template to its built-in default.

```
spectra prompts reset <template-id> [--all] [--no-interaction]
```

**Arguments**:
- `template-id` (optional if `--all`): Template to reset

**Options**:
- `--all`: Reset all 5 templates to defaults

**Behavior**: Overwrites the file with built-in default content. Updates the skills manifest hash.

**Exit codes**: 0 (success), 1 (error)

---

### spectra prompts validate

Validate a template for syntax errors and unknown placeholders.

```
spectra prompts validate <template-id> [--output-format human|json] [--no-interaction]
```

**Checks**:
1. File exists and is readable
2. YAML frontmatter parses correctly
3. All `{{placeholder}}` names match declared placeholders (warnings for unknowns)
4. `{{#if}}` and `{{#each}}` blocks are properly closed
5. No nested control blocks

**Output (human)**:
```
✓ behavior-analysis: valid (7 placeholders, 0 warnings)
```

**Output (JSON)**:
```json
{
  "command": "prompts validate",
  "status": "success",
  "templateId": "behavior-analysis",
  "valid": true,
  "placeholders": 7,
  "warnings": [],
  "errors": []
}
```

**Exit codes**: 0 (valid), 1 (error), 2 (validation errors)
