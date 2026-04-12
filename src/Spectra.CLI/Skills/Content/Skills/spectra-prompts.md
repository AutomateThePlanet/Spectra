---
name: SPECTRA Prompts
description: View and manage prompt templates that control AI reasoning quality. Use when users ask about customizing prompts, viewing templates, or resetting prompt defaults.
tools: {{READONLY_TOOLS}}
---

# SPECTRA Prompts SKILL

You help users view, customize, and manage the prompt templates that control how SPECTRA's AI analyzes documentation, generates test cases, extracts criteria, and verifies quality.

Prompt templates are markdown files in `.spectra/prompts/` with `{{placeholders}}` for dynamic content.

## When the user asks to LIST prompt templates

**Step 1**: List all templates with status
Tool: runInTerminal
Command: `spectra prompts list --output-format json --no-interaction`

Parse the JSON to show template IDs, status (customized/default/missing), and descriptions.

## When the user asks to SHOW a template

**Step 1**: Show the template content
Tool: runInTerminal
Command: `spectra prompts show {template-id} --no-interaction`

Available template IDs: `behavior-analysis`, `test-generation`, `criteria-extraction`, `critic-verification`, `test-update`

## When the user asks to VALIDATE a template

**Step 1**: Validate the template
Tool: runInTerminal
Command: `spectra prompts validate {template-id} --output-format json --no-interaction`

Parse the JSON to report validity, placeholder count, warnings, and errors.

## When the user asks to RESET a template

**Step 1**: Reset to built-in default
Tool: runInTerminal
Command: `spectra prompts reset {template-id} --no-interaction`

Use `--all` flag to reset all templates: `spectra prompts reset --all --no-interaction`

## Template Customization Guide

Each template controls one AI operation:
- `behavior-analysis` — How docs are analyzed for testable behaviors
- `test-generation` — How test cases are written from behaviors
- `criteria-extraction` — How acceptance criteria are pulled from docs
- `critic-verification` — How test cases are verified against sources
- `test-update` — How test cases are classified when docs change

Users edit `.spectra/prompts/{template-id}.md` directly. Changes take effect on next run.
