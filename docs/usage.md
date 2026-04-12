---
title: Usage (Copilot Chat)
parent: User Guide
nav_order: 9
---

# SPECTRA Usage Guide — VS Code Copilot Chat

This guide shows how to use SPECTRA through VS Code Copilot Chat. Every workflow below shows what to say and what to expect. For configuration and customization details, see `CUSTOMIZATION.md`.

## Prerequisites

1. VS Code with the GitHub Copilot extension installed.
2. `spectra init` completed in your project (creates SKILL files in `.github/skills/` and the bundled agent prompts in `.github/agents/`).
3. Documentation files present in `docs/` (the source material for test generation and acceptance criteria).

## Getting Started

Open Copilot Chat in VS Code (Ctrl+Shift+I) and select the **SPECTRA Generation** agent for any of the workflows below. For test execution use the **SPECTRA Execution** agent. If you don't know which workflow you want, just ask the assistant "help me get started" — the bundled `spectra-quickstart` SKILL will walk you through the options.

---

## Generating Test Cases

The most common workflow. Three levels of specificity:

**Basic:**

> "Generate test cases for checkout"

**With count and focus:**

> "Generate 15 test cases for checkout focusing on error handling"

**Analysis only (review before generating):**

> "Analyze the checkout docs and tell me what test cases you'd generate"

### What to expect

1. A live progress page opens in a tab showing analysis status.
2. Analysis phase: testable behaviors identified, existing coverage shown.
3. You approve the count → generation begins.
4. Test cases are verified by the critic model and written in batches.
5. Results show: test cases created, verification verdicts, file paths.
6. Optional: suggestions for additional coverage areas.

### Follow-up prompts

After generating, you can continue:

> "Generate the suggested test cases"
> "Generate suggestions 1 and 3"
> "Create a test case for user session timeout after 30 minutes"

---

## Extracting Acceptance Criteria

> "Extract acceptance criteria from my documentation"
> "Re-extract all criteria (force full extraction)"

### What to expect

1. Each document is processed individually (no truncation).
2. SHA-256 hashing skips documents that haven't changed since the last extraction.
3. Results are reported per document with criteria counts grouped by RFC 2119 level (MUST/SHOULD/MAY).
4. Files created: `docs/criteria/{docname}.criteria.yaml` and a master `docs/criteria/_criteria_index.yaml`.

### Follow-up prompts

> "List the high priority criteria for checkout"
> "Which MUST criteria don't have test cases yet?"

---

## Importing Criteria from External Tools

> "Import acceptance criteria from jira-export.csv"
> "Import criteria from sprint-42.yaml and replace existing"

Supported formats: CSV (with auto-detection of Jira/ADO column layouts), YAML, and JSON. Compound criteria are automatically split by AI into individual entries. Imports merge by default; pass replace mode if you want to overwrite the target file.

---

## Coverage Analysis

> "Show me coverage gaps"
> "Analyze coverage and link test cases to automation code"

Three dimensions are reported:

- **Documentation coverage** — docs ↔ test cases linkage.
- **Acceptance criteria coverage** — criteria ↔ test cases linkage.
- **Automation coverage** — test cases ↔ code via the `automated_by` frontmatter field.

The optional auto-link mode writes `automated_by` back into your test case files when it can resolve a match.

---

## Generating Dashboard

> "Generate a coverage dashboard"
> "Generate a dashboard preview with our branding"

Opens an interactive HTML dashboard with suite stats, coverage visualizations, and drill-down details. The preview mode renders sample data so you can verify branding and theme settings before running it on real data.

---

## Validating Test Cases

> "Validate my test case files"
> "Validate the checkout suite"

Checks: required frontmatter fields, unique test IDs, valid priority enum values, valid tags, dependency resolution.

---

## Updating Test Cases After Doc Changes

> "Update test cases for checkout — the payment docs changed"

Classifies every test case in the suite as **UP_TO_DATE**, **OUTDATED**, **ORPHANED**, or **REDUNDANT**. You approve which test cases to rewrite and the assistant updates them in place with diffs you can review.

---

## Executing Tests via MCP

> "Run the smoke test selection"
> "Execute the checkout suite"

Switch to the **SPECTRA Execution** agent for this workflow. The agent presents each test, you provide PASS/FAIL/SKIP/BLOCKED results, and an HTML report is generated at the end. Saved selections (like `smoke`) live in `spectra.config.json` and let you run a curated subset without naming individual test IDs.

### Prerequisite

The MCP server must be running. Check `.vscode/mcp.json` for the connection configuration — `spectra init` writes a working default.

---

## Creating a Custom Profile

> "Create a test generation profile for our API team"
> "Set up a custom test format with request/response fields"

Generation profiles control test output format — frontmatter fields, step structure, custom keys. The bundled init flow seeds `profiles/_default.yaml`; you can copy it to a named profile and customize it. Set the active profile in `spectra.config.json` under `ai.profile`. See `CUSTOMIZATION.md` for the full reference.

---

## Indexing Documentation

> "Index all documentation"
> "Rebuild the docs index from scratch"

Catalogs every document in `docs/` with metadata (sections, entities, tokens, content hashes). Required before first generation, and re-run automatically before generation, but you can also run it on demand. The force flag rebuilds from scratch ignoring hashes.

---

## Customizing SPECTRA

> "How do I customize test generation quality?"
> "Show me how to focus on security testing"

There are four customization surfaces:

1. **Prompt templates** in `.spectra/prompts/*.md` — control AI reasoning for analysis, generation, criteria extraction, critic verification, and updates.
2. **Generation profiles** in `profiles/` — control test output format.
3. **Behavior categories** in `spectra.config.json` — control what kinds of behaviors the analyzer considers.
4. **Focus filtering** — runtime focus passed via natural language ("focusing on security and error handling").

For the complete customization reference, see `CUSTOMIZATION.md`.

---

## Troubleshooting

### A command appears stuck or waiting for input

All bundled SKILLs pass non-interactive flags. If you see a hang, your installed SKILL files may be out of date — refresh them with the spectra CLI's update-skills command. Customizations you've made to SKILL files are preserved by hash tracking.

### Stale terminology in your installed SKILLs

Same fix — refresh installed SKILL files with the update-skills command.

### Dashboard coverage section empty

Run a coverage analysis first, then re-generate the dashboard. The dashboard reads coverage data; without a recent run it has nothing to show.

### No acceptance criteria found

Index the documentation first, then extract criteria. The order matters because extraction reads the index.

### Test cases aren't linked to acceptance criteria

Re-generate after extracting criteria. Generation auto-loads related criteria as prompt context and writes the linkage into the test case frontmatter.

---

## Complete Pipeline — Start to Finish

For a brand-new project, the recommended sequence is:

1. **Initialize SPECTRA** — `spectra init` (bootstraps config, SKILLs, agents, profiles).
2. **Index the documentation** — builds the doc catalog.
3. **Extract acceptance criteria** — pulls testable criteria out of the docs.
4. **Generate test cases** — for each suite, ask the assistant to generate and review.
5. **Validate the test cases** — catches frontmatter and ID issues.
6. **Show me coverage** — identifies gaps across docs, criteria, and automation.
7. **Generate a dashboard** — visual report.
8. **Execute a suite** — interactive run with PASS/FAIL/SKIP/BLOCKED reporting.

Each step is independent and can be re-run as the underlying docs and test cases evolve.
