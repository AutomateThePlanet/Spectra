---
title: How Test Case Generation Works
parent: Architecture
nav_order: 2
---

# How Test Case Generation Works

SPECTRA generates test cases through a pipeline: read docs → analyze behaviors → generate in your
Claude Code session → critic verification (a separate subagent call) → write `.md` files.

## Two ways to drive it

With Claude Code (recommended), you say "Generate test cases for checkout". The bundled
`spectra-generate` skill drives the whole pipeline: it compiles a deterministic prompt, you
generate the answer as a turn in your session, and the skill validates/persists the result.

With the CLI directly, you run the underlying seam commands yourself (`spectra ai compile-prompt` →
answer the prompt → `spectra ai ingest-tests`). It's the same pipeline, useful when scripting the individual
steps. There is no single `spectra ai generate` command that does everything in one non-interactive
call, because the model turn is always a real turn in a session, not something the CLI can invoke on its
own. See [CLI Reference](../cli-reference.md#generation-in-session-via-the-spectra-generate-skill).

## Why generation and verification are separate

Behavior analysis, generation, and grounding verification are deliberately split: verification runs
as the `spectra-critic` subagent in a **fresh, isolated context**, seeing only the generated test
and its source docs, never the generator's reasoning or prompt. That isolation is what makes the
critic's grounding check meaningful rather than the generator grading its own work.
