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

<ol class="spectra-steps">
{% include spectra_step.html n="01" icon="index" title="Criteria" href="/criteria.html" text="Say **“Extract acceptance criteria”**. SPECTRA finds the MUST, SHOULD and MAY requirements in your docs." %}
{% include spectra_step.html n="02" icon="generate" title="Generate" href="/usage.html" text="Say **“Generate test cases for checkout”**. The agent proposes what to test and waits for your OK." %}
{% include spectra_step.html n="03" icon="verify" title="Review" href="/review.html" text="A separate check compares every test with your docs. You decide on the ones it flags." %}
{% include spectra_step.html n="04" icon="execute" title="Run" href="/execution-agents" text="Say **“Run the smoke test cases”**. You record each result in a local browser console." %}
{% include spectra_step.html n="05" icon="cover" title="Cover" href="/coverage.html" text="See which docs, criteria and tests are covered, on a dashboard you can publish." %}
</ol>

## Quick start

```bash
dotnet tool install -g Spectra.CLI
spectra init
```

Then open Claude Code in your repository and say **"Extract acceptance criteria"**, then **"Generate test cases for
checkout"**. Using Copilot, Cursor or another agent? Run `spectra init --agent <name>`
([Use other AI agents](ai-agents)). The full walkthrough: [Getting started](getting-started).

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
