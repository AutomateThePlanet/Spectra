---
title: How Test Case Generation Works
parent: Architecture
nav_order: 2
---

# How Test Case Generation Works

SPECTRA generates test cases through a pipeline: read docs → analyze behaviors → AI generation → critic verification → write `.md` files.

## Two ways to use it

**Copilot Chat (recommended)** — say "Generate test cases for checkout". The bundled SKILL handles CLI invocation, progress tracking, and result presentation automatically.

**CLI directly** — `spectra ai generate --suite checkout`. Same pipeline. Useful for CI/CD and scripting.

Both run the same engine. Chat is the interface, CLI is the engine.

## Why the CLI pipeline matters

The generation pipeline (doc loading, profile merging, ID allocation, batch generation, dual-model critic verification) runs as a single CLI process. This keeps the critic model independent from the generator — critical for catching hallucinations. Chat invokes this process through SKILLs rather than reimplementing it.
