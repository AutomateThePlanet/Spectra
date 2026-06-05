# Contract: `spectra-critic` `context: fork` subagent skill

A net-new subagent skill (`src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md`) that
performs the critic's model turn in a **fresh, isolated context**. This spec delivers and registers
the skill; the *wiring* that makes the generation skill invoke it as a mandatory step lands in the
subsequent spec.

## Frontmatter contract

```yaml
---
name: spectra-critic
description: Verifies a generated test against its source documents and returns a JSON verdict.
tools: [{{CRITIC_TOOLS}}]          # read-only doc/test access; no generation tools
model: {{ai.critic.model}}          # resolved from config (target: Sonnet 4.6) — single selector
disable-model-invocation: true      # explicit invocation only — never auto-invoked (FR-003)
context: fork                       # fresh, isolated context (FR-002)
---
```

> The exact frontmatter key for fork isolation follows the repository's agent-file convention (the
> same mechanism the existing `spectra-generation.agent.md` / `spectra-execution.agent.md` agents
> use). `disable-model-invocation: true` is carried over verbatim from those agents and is what
> realizes "never auto-invocation."

## Input contract (isolation — FR-002)

The subagent receives **only**:
- the test artifact to verify (id, title, preconditions, steps, expected result, test data), and
- the ≤5 selected source documents (each ≤8000 chars).

It MUST NOT receive the generator's prompt, reasoning, tool calls, or token usage. This matches the
isolation the reused `CriticPromptBuilder` already enforces by construction
(`CriticPromptBuilder.cs:76–141`).

## Output contract

The subagent's final message is the verdict JSON consumed by `ingest-verdict`:

```json
{
  "verdict": "grounded" | "partial" | "hallucinated",
  "score": 0.0,
  "findings": [
    { "element": "...", "claim": "...", "status": "grounded|unverified|hallucinated",
      "evidence": "... | null", "reason": "... | null" }
  ]
}
```

A response missing `verdict` or `score` is **damage** — the `ingest-verdict` boundary fails loud
(exit 6); the skill must always render both fields.

## Invocation contract (FR-003)

- The skill is authored for **explicit, mandatory-step** invocation inside the generation skill's
  procedure (delivered next spec). It is never auto-invoked (`disable-model-invocation: true`).
- Procedure: compile the critic prompt (`spectra ai compile-critic-prompt`) → run the model turn in
  the forked context → hand the verdict JSON to `spectra ai ingest-verdict`.

## Registration

- `SkillsManifest.cs` — register the `spectra-critic` agent.
- `AgentContent.cs` — surface the new agent content so `spectra update-skills` / `spectra init`
  deploy it.

## Test contract

- The skill file exists, parses, and declares fork isolation + `disable-model-invocation: true`.
- Its instruction restricts input to artifact + source documents (no generator-state tokens such as
  generator prompt/reasoning/tool-calls).
