---
title: Home
layout: home
nav_order: 0
permalink: /
---

# SPECTRA

**AI-native test case generation and execution framework.**
From documentation to verified, grounded test cases, automatically.
{: .fs-6 .fw-300 }

[Get Started](getting-started){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/AutomateThePlanet/Spectra){: .btn .fs-5 .mb-4 .mb-md-0 }

---

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
Your docs → spectra ai generate → AI analysis → critic verification → test-cases/*.md
```

Or just say it in Claude Code: **"Generate test cases for checkout"**

## Key capabilities

- **AI Test Case Generation** driven from your Claude Code session, with independent critic verification
- **Acceptance Criteria Extraction** using MUST/SHOULD/MAY from docs with SHA-256 change tracking
- **Three-Dimensional Coverage** spanning docs, criteria, and automation in one dashboard
- **CLI-Native Execution** through `spectra run`, plus an optional local web console for manual verdicts
- **Testimize Integration** for optional algorithmic test data optimization (BVA, EP, pairwise)
- **Git-native** storage as Markdown and YAML, with no database and no vendor lock-in

## Quick install

```bash
dotnet tool install -g Spectra.CLI
spectra init
```

Then open Claude Code in your project and ask it to generate test cases. See
[Getting Started](getting-started) for the full walkthrough.

## Part of the Automate The Planet ecosystem

| Tool | Purpose |
|------|---------|
| [BELLATRIX](https://bellatrix.solutions) | Test automation framework |
| [Testimize](https://github.com/AutomateThePlanet/Testimize) | Test data optimization |
| **SPECTRA** | AI test case generation and execution |
