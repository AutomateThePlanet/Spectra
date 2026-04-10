---
title: Home
layout: home
nav_order: 0
permalink: /
---

# SPECTRA Documentation

AI-native test generation and execution framework. From documentation to deterministic test execution.
{: .fs-6 .fw-300 }

[Get Started](getting-started){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 }
[View on GitHub](https://github.com/AutomateThePlanet/Spectra){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## What is SPECTRA?

SPECTRA reads your product documentation, generates comprehensive test suites with AI-powered grounding verification, and executes them through a deterministic MCP-based protocol. Tests are plain Markdown files versioned alongside your code.

### Key capabilities

**AI Test Generation** — Generate test cases from documentation through an iterative session with dual-model grounding verification.

**Three-Dimensional Coverage** — Track documentation, acceptance criteria, and automation coverage in a unified report.

**MCP Execution Engine** — Execute tests through Copilot Chat, Claude, or any MCP client with a deterministic state machine.

**Copilot Chat Integration** — 12 bundled SKILL files translate natural language into CLI commands automatically.

**Visual Dashboard** — Static HTML dashboard with suite browser, run history, and coverage visualizations.

---

## Quick install

```bash
dotnet tool install -g Spectra.CLI
spectra init
spectra docs index
spectra ai generate
```

See [Getting Started](getting-started) for prerequisites and authentication setup.

## Part of the Automate The Planet ecosystem

| Tool | Purpose |
|------|---------|
| [BELLATRIX](https://bellatrix.solutions) | Test automation framework |
| [Testimize](https://github.com/AutomateThePlanet/Testimize) | Test case optimization |
| **SPECTRA** | AI test generation and execution |
