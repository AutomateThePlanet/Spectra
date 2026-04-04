# SPECTRA Feature Spec: CLI Non-Interactive Mode and Structured Output

**Status:** Draft — ready for spec-kit cycle  
**Priority:** HIGH — prerequisite for all SKILL-based workflows  
**Depends on:** Phase 1 (CLI)  
**Blocks:** SKILL files spec, Generation Session spec

---

## 1. Problem

The CLI currently uses Spectre.Console interactive prompts (suite selection, confirmations, spinners). This blocks SKILL-based workflows where Copilot Chat calls CLI commands in the terminal — the chat cannot respond to interactive prompts.

Additionally, CLI output is human-formatted (colors, tables, spinners) which the chat cannot parse reliably.

## 2. Solution

Two changes to every CLI command:

1. **Non-interactive mode:** If all required arguments are provided, execute without any prompts
2. **Structured output:** `--output-format json` flag produces parseable JSON on stdout

---

## 3. Non-Interactive Rules

For ALL CLI commands:

- If all required args are present → execute silently, output result, no prompts
- If required args are MISSING and `--no-interaction` flag is set → fail with exit code 3 and error listing missing args
- If required args are MISSING and no flag → interactive mode (current behavior, preserved for direct human usage)

SKILL files will ALWAYS pass all args. Interactive mode is only for direct human CLI usage.

## 4. Structured Output Format

Add `--output-format` flag to ALL commands:

- `human` (default): current Spectre.Console formatted output with colors, spinners, tables
- `json`: structured JSON on stdout, no colors, no spinners, no progress indicators

### JSON schemas per command:

**spectra ai generate:**
```json
{
  "command": "generate",
  "status": "completed",
  "timestamp": "2026-04-04T10:30:00Z",
  "suite": "checkout",
  "analysis": {
    "total_behaviors": 18,
    "already_covered": 8,
    "recommended": 10,
    "breakdown": {
      "happy_path": 4,
      "negative": 3,
      "edge_case": 2,
      "security": 1
    }
  },
  "generation": {
    "tests_generated": 10,
    "tests_written": 8,
    "tests_rejected_by_critic": 2,
    "grounding": {
      "grounded": 6,
      "partial": 2,
      "hallucinated": 2
    }
  },
  "suggestions": [
    { "title": "Payment timeout after 30s", "category": "edge_case" },
    { "title": "Concurrent checkout sessions", "category": "edge_case" }
  ],
  "files_created": [
    "tests/checkout/TC-201.md",
    "tests/checkout/TC-202.md"
  ]
}
```

**spectra ai analyze --coverage:**
```json
{
  "command": "analyze-coverage",
  "status": "completed",
  "timestamp": "2026-04-04T10:30:00Z",
  "documentation": { "percentage": 75.0, "covered": 9, "total": 12 },
  "requirements": { "percentage": 60.0, "covered": 18, "total": 30 },
  "automation": { "percentage": 58.6, "linked": 34, "total": 58 },
  "undocumented_tests": 5,
  "uncovered_areas": [
    { "doc": "gdpr-compliance.md", "reason": "no linked tests" },
    { "requirement": "REQ-025", "reason": "no test coverage" }
  ]
}
```

**spectra validate:**
```json
{
  "command": "validate",
  "status": "errors_found",
  "timestamp": "2026-04-04T10:30:00Z",
  "total_files": 58,
  "valid": 55,
  "errors": [
    { "file": "tests/checkout/TC-101.md", "line": 3, "message": "Missing required field: priority" },
    { "file": "tests/auth/TC-005.md", "line": 1, "message": "Duplicate test ID: TC-005" }
  ]
}
```

**spectra dashboard:**
```json
{
  "command": "dashboard",
  "status": "completed",
  "timestamp": "2026-04-04T10:30:00Z",
  "output_path": "./site",
  "pages_generated": 4,
  "suites_included": 6,
  "tests_included": 58,
  "runs_included": 24
}
```

**spectra list:**
```json
{
  "command": "list",
  "status": "completed",
  "suites": [
    { "name": "checkout", "test_count": 28, "last_modified": "2026-04-03" },
    { "name": "authentication", "test_count": 15, "last_modified": "2026-04-01" }
  ]
}
```

**spectra show {test-id}:**
```json
{
  "command": "show",
  "status": "completed",
  "test": {
    "id": "TC-101",
    "title": "Successful checkout with credit card",
    "priority": "high",
    "suite": "checkout",
    "component": "payment-processing",
    "tags": ["payments", "credit-card"],
    "source_refs": ["docs/checkout-flow.md"],
    "steps": ["Navigate to checkout", "Enter card details", "Click Pay"],
    "expected_results": ["Payment confirmed", "Order created"]
  }
}
```

**spectra init:**
```json
{
  "command": "init",
  "status": "completed",
  "timestamp": "2026-04-04T10:30:00Z",
  "created": [
    "spectra.config.json",
    "tests/",
    "docs/",
    "templates/bug-report.md",
    ".github/agents/spectra-execution.agent.md",
    ".github/agents/spectra-generation.agent.md",
    ".github/skills/spectra-generate/SKILL.md",
    ".github/skills/spectra-coverage/SKILL.md",
    ".github/skills/spectra-dashboard/SKILL.md",
    ".github/skills/spectra-validate/SKILL.md"
  ]
}
```

## 5. Exit Codes

Standardize across all commands:

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Command failed (build error, missing config, runtime error) |
| 2 | Validation errors found (validate command only) |
| 3 | Missing required arguments in non-interactive mode |

## 6. Verbosity Flag

Add `--verbosity` flag: `quiet`, `normal` (default), `verbose`

- `quiet`: only final result (JSON or one-line summary)
- `normal`: current behavior with progress indicators
- `verbose`: step-by-step logging of what CLI is doing internally

SKILL files will use: `--output-format json --verbosity quiet`  
CI pipelines will use: `--output-format json --verbosity normal`  
Humans will use: defaults (human + normal)

## 7. Documentation Updates

- Update `docs/cli-reference.md` with new flags for every command
- Add `docs/skills-integration.md` explaining the SKILL → CLI → JSON pattern
- Update `README.md` to mention SKILL-based workflow

---

## 8. Spec-Kit Prompt

```
/speckit.specify Refactor SPECTRA CLI for non-interactive mode and structured output.

Read spec-kit/feature-cli-non-interactive.md for the complete design.

Key deliverables:
- All CLI commands work without prompts when all args are provided
- --output-format json flag on all commands producing parseable JSON
- --no-interaction flag fails with exit code 3 if args missing
- --verbosity flag (quiet/normal/verbose)
- Standardized exit codes (0/1/2/3)
- JSON schemas defined per command
- Preserve interactive mode as default for backward compatibility

Tech: C# System.CommandLine, remove Spectre.Console prompts when args
are present, JSON serialization with System.Text.Json, conditional
output formatting based on --output-format flag.
```
