---
title: Overview
layout: home
nav_order: 0
permalink: /
---

## What Is SPECTRA?

**AI-native test case generation and execution framework.**
From documentation to verified, grounded test cases, automatically.

[Get Started](getting-started){: .btn .btn-primary .mr-2 }
[View on GitHub](https://github.com/AutomateThePlanet/Spectra){: .btn }

{% include spectra_visual.html %}

## Why SPECTRA?

| Manual approach | With SPECTRA |
|----------------|--------------|
| Write test cases in spreadsheets or Jira | AI generates from your docs |
| Test cases drift as docs change | Doc changes auto-flag outdated test cases |
| No systematic boundary/edge coverage | 6 ISTQB techniques applied automatically |
| Coverage gaps invisible until bugs | Three-dimensional coverage dashboard |
| Subjective test case reviews | Dual-model grounding verification |
| Locked into test management tools | Markdown files versioned in Git |

## How it works

```
Your docs → spectra-generate skill (analysis → batches) → critic verification → test-cases/*.md
```

Or just say it in Claude Code, or any of eight other AI agents: **"Generate test cases for checkout"**

<ol class="spectra-steps">
{% include spectra_step.html n="01" icon="index" title="Index" href="/cli-reference.html#spectra-docs-index" text="`spectra docs index` builds the document index. **Acceptance Criteria Extraction** (the `spectra-criteria` skill) uses MUST/SHOULD/MAY from docs with SHA-256 change tracking." %}
{% include spectra_step.html n="02" icon="generate" title="Generate" href="/cli-reference.html#generation-in-session-via-the-spectra-generate-skill" text="**AI Test Case Generation** driven from your Claude Code session: the `spectra-generate` skill applies six ISTQB techniques (EP, BVA, DT, ST, EG, UC)." %}
{% include spectra_step.html n="03" icon="verify" title="Verify" href="/grounding-verification.html" text="Independent critic verification: the `spectra-critic` subagent classifies every test as grounded, partial or hallucinated." %}
{% include spectra_step.html n="04" icon="execute" title="Execute" href="/execution-agents" text="**CLI-Native Execution** through `spectra run`, plus an optional local web console for manual verdicts." %}
{% include spectra_step.html n="05" icon="cover" title="Cover" href="/coverage.html" text="**Three-Dimensional Coverage** spanning docs, criteria, and automation in one dashboard." %}
</ol>

## Key capabilities

- **Works in your AI agent**: Claude Code by default; `spectra init --agent` also installs for Copilot, Cursor, Devin,
  Codex, Gemini, Antigravity, Kiro and OpenCode — see [AI Agents](ai-agents)
- **Doc-Change Updates** that find the tests a doc change touches, edit them and re-verify them — see
  [When documentation changes](usage.md#when-documentation-changes)
- **Testimize Integration** for optional algorithmic test data optimization (BVA, EP, pairwise)
- **Git-native** storage as Markdown and YAML, with no database and no vendor lock-in

## Start Here

<div class="spectra-cards">
{% include spectra_card.html page="getting-started.md" desc="Install SPECTRA, authenticate, and generate your first test case suite." %}
{% include spectra_card.html page="usage.md" desc="This guide shows how to use SPECTRA through Claude Code." %}
{% include spectra_card.html page="test-format.md" desc="How SPECTRA stores manual test cases as Markdown files with YAML frontmatter." %}
{% include spectra_card.html page="cli-reference.md" desc="All SPECTRA CLI commands organized by function." %}
{% include spectra_card.html page="coverage.md" desc="Three-dimensional coverage analysis: Documentation, Acceptance Criteria, and Automation." %}
{% include spectra_card.html page="grounding-verification.md" desc="Dual-model critic flow to detect hallucinated test cases." %}
</div>

## Quick install

```bash
dotnet tool install -g Spectra.CLI
spectra init
```

Then open Claude Code in your project and ask it to generate test cases. Using another agent? Run
`spectra init --agent <name>` instead (for example `--agent cursor`); see [AI Agents](ai-agents). See
[Getting Started](getting-started) for the full walkthrough.

{% include spectra_terminal.html %}

## Part of the Automate The Planet ecosystem

| Tool | Purpose |
|------|---------|
| [BELLATRIX](https://bellatrix.solutions) | Test automation framework |
| [Testimize](https://github.com/AutomateThePlanet/Testimize) | Test data optimization |
| **SPECTRA** | AI test case generation and execution |
