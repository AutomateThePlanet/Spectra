---
title: Overview
layout: home
nav_order: 0
permalink: /
description: "SPECTRA turns your product documentation into manual test cases in your repository. Ask your AI agent; SPECTRA plans, writes and double-checks the tests."
---

## What is SPECTRA?

SPECTRA turns your product documentation into manual test cases, stored as Markdown files in your repository. You ask
your AI coding agent, and SPECTRA's skills plan the tests, write them and check each one against your docs. You
review what it flags, run the tests from a browser console and follow coverage on a dashboard.

[Get started](getting-started){: .btn .btn-primary .mr-2 }
[View on GitHub](https://github.com/AutomateThePlanet/Spectra){: .btn }

## How it works

{% include flow.html name="user-journey" %}

## Quick start

```bash
dotnet tool install -g Spectra.CLI
spectra init
```

Then open Claude Code in your repository and say **"Extract acceptance criteria"**, then **"Generate test cases for
checkout"**. Using Copilot, Cursor or another agent? Run `spectra init --agent <name>`
([Use other AI agents](ai-agents)). The full walkthrough: [Getting started](getting-started). When your docs change:
[Update tests when your docs change](keep-tests-up-to-date.html).

## What you can do

<div class="spectra-cards">
{% include spectra_card.html page="getting-started.md" %}
{% include spectra_card.html page="usage.md" %}
{% include spectra_card.html page="review.md" %}
{% include spectra_card.html page="coverage.md" %}
{% include spectra_card.html page="keep-tests-up-to-date.md" %}
{% include spectra_card.html page="execution-agents.md" %}
</div>

SPECTRA is part of the [Automate The Planet](https://www.automatetheplanet.com) tools, with
[BELLATRIX](https://bellatrix.solutions) for test automation.
