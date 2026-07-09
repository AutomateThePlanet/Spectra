---
title: "Claude Code v2 vs. the GitHub Copilot SDK v1"
parent: Migration Guides
nav_order: 41
---

# Claude Code v2 vs. the GitHub Copilot SDK v1

This guide covers releases 2.0.0 and later. It's a partially breaking change, since the command
surface changed even though config remains backward compatible; nothing needs to be backed up,
because there is no destructive migration and old config keys are simply ignored.

This page compares day-to-day usage and functionality between pre-v2 SPECTRA (which ran its own
embedded GitHub Copilot SDK session) and v2 (which runs entirely inside your interactive Claude
Code session). For the cost/billing side specifically, see the
[AI Models & Cost Guide](ai-models-cost-guide.md).

---

## The one-sentence version

v1 SPECTRA opened its own AI session and made model calls itself, in-process, from inside a long
CLI command. v2 SPECTRA makes no model calls at all; instead it emits deterministic prompts, which
you (or the skill on your behalf) answer as ordinary turns in your own Claude Code session, and
SPECTRA then validates and persists whatever comes back.

---

## Architecture: before / after

**v1 (in-process SDK session):**

```
spectra ai generate checkout --count 20
  └─ one long-running CLI process
       ├─ authenticates via spectra auth / gh auth login
       ├─ opens a Copilot SDK session against ai.providers[0]
       ├─ runs behavior analysis, generation, and (optionally) an in-process critic
       │  entirely inside that one process
       └─ writes test files + index when the process exits
```

**v2 (compile → in-session turn → ingest seam):**

```
"generate 20 tests for checkout"  (you, in conversation with Claude Code)
  └─ the spectra-generate SKILL orchestrates, calling SPECTRA CLI commands
     that never talk to a model themselves:
       spectra ai compile-analysis-prompt  → (a turn in YOUR session) → spectra ai ingest-analysis
       spectra ai compile-prompt           → (a turn in YOUR session) → spectra ai ingest-tests
       spectra ai compile-critic-prompt    → (the spectra-critic subagent, context: fork) → spectra ai ingest-verdict
       spectra ai ingest-grounding --all   (durable verdict write-back)
```

Every "AI-touching" flow in v2 (bulk generation, from-description generation, behavior analysis,
criteria extraction, and targeted updates) follows the same three-step shape: a `compile-*`
command emits a deterministic prompt to stdout, a model turn answers it, and an `ingest-*` command
validates and persists the answer (failing loud on malformed output rather than silently accepting
it).

---

## What's gone

Everything below was removed by v2.4.0, the release that completed the migration to Claude Code
sessions.

| Removed | Notes |
|---|---|
| GitHub Copilot SDK (`CopilotService`, `Agent/Copilot/`) | No in-process model runtime remains anywhere in SPECTRA. |
| `ai.providers` / `ai.critic` config blocks | Old configs still parse; the keys are inert. |
| `spectra auth` | Nothing to authenticate separately, since you're already signed in to Claude Code. |
| `spectra ai generate` (the do-everything command) | Replaced by the `spectra-generate` skill driving `compile-prompt`/`ingest-tests` turns. |
| In-process critic (`CopilotCritic`), provider-keyed critic model switch | Verification runs as the `spectra-critic` subagent instead. |
| SPECTRA's own MCP execution server (`spectra-mcp`, 25 tool schemas) | `spectra run` is the sole execution surface; `spectra init` no longer writes `.vscode/mcp.json` for it. |
| `docs/requirements/_requirements.yaml`, `RequirementsExtractor` | `spectra docs index` is index-only now; criteria extraction is a separate seam (`spectra docs changed` → `spectra-criteria` skill). |
| Import compound-splitting, `--skip-splitting` | Import is a plain deterministic pass-through. |

---

## What's new

- **The compile → in-session → ingest seam**, applied uniformly to generation, from-description
  generation, behavior analysis, criteria extraction (`spectra docs changed` diffs by SHA-256 so
  only changed docs get re-prompted), and targeted updates (`compile-update-prompt` /
  `ingest-update`, with a drift guard that fails loud on out-of-scope field changes).
- The `spectra-critic` subagent (`context: fork`) is an isolated, per-test verification call
  that sees only the test artifact and its source docs, never the generator's reasoning.
- Under the new verdict disposition policy, a `grounded` verdict writes a durable `grounding:`
  block into the test's frontmatter plus a full verdict JSON under `.spectra/verdicts/`; a
  `partial` verdict gets one bounded repair attempt (`compile-repair-prompt` → patch → re-critic)
  without blocking the rest of the batch; and a `hallucinated` verdict is recorded to a
  `.spectra/dropped-tests.json` trail before the existing clean delete.
- New batch ingest verbs, added in v2.4.0, replace per-test shell loops with a single call:
  `ingest-verdict --suite --all` and `ingest-update --all` classify and ingest everything staged
  for a suite in one pass.
- `spectra run` is now a first-class CLI surface over `.execution/spectra.db`. Short-lived CLI
  processes reconstruct run state losslessly, and the store runs in WAL mode so concurrent
  invocations don't hit `SQLITE_BUSY`.
- `spectra run console` is an optional local, detached web console (no MCP client needed) where a
  tester can drive PASS/FAIL/BLOCKED, notes, and screenshots from a browser, backed by the same
  SQLite store as the CLI.
- `spectra ai audit-grounding` and `compile-repair-batch`, added in v2.2.0, provide a resume-safe
  oracle for "what still needs grounding" and a deterministic batch repair manifest, so an
  interrupted repair loop can resume without re-reading test files by hand.

---

## Usage differences, day to day

**Generating tests**

In v1 you ran `spectra ai generate checkout --count 20` once, authenticated up front via
`spectra auth`/`gh auth login`, and one long CLI process did everything and exited. In v2 you ask
Claude Code in conversation (*"generate 20 tests for the checkout suite"*), and the
`spectra-generate` skill drives you through analysis, then generation, then the mandatory critic
step, each as its own turn or subagent call, with a bounded fail-loud retry on malformed output.
There is no single long-running "generate" process, and nothing to separately authenticate,
because you're already using the account that's running your Claude Code session.

**Executing tests**

In v1 you could execute tests either with `spectra run …` directly, or through any MCP client
wired up to SPECTRA's own execution server (25 tools) alongside the CLI. In v2, `spectra run …` is
the only path, plus the optional `spectra run console` browser UI for manual verdict recording.
There's no MCP client configuration for execution to set up at all, since the execution
agent/skill orchestrate `spectra run` directly.

**Repairing flagged (partial) tests**

In v1, no repair loop existed at all, because a `partial` critic verdict had no defined
disposition. In v2, repair is either driven one test at a time by the skill, or batched:
`compile-repair-batch` emits a manifest, the agent reads it in-context (never piped through
`python`/`jq`), patches are written to `.spectra/updates/{suite}/updated-{id}.json`, and are then
ingested and re-verified in bulk.

**Scripting / CI**

In v1, `spectra ai generate X --auto-complete --output-format json` ran the whole pipeline
non-interactively in one process, which was a natural fit for a CI job. In v2, there is no single
command that "runs the whole pipeline, no prompts" anymore, because the model turn is not
something the CLI can invoke on its own; it's an interactive (or headless-Claude-Code-driven)
session turn. A CI pipeline that scripted the old flow needs to move to a Claude
Code-orchestrated run (e.g. driving the skill headlessly) or call the `compile-*`/`ingest-*` seam
commands directly around whatever entry point the CI job uses to talk to Claude.

---

## Cost model

The GitHub Copilot premium-request multiplier table from the v1 guide no longer applies to
anything, because there is no `ai.providers` left to route against. Cost is now exactly your normal
Claude Code usage (subscription plan limits, or API/Console token billing), because the model turns
that used to run inside SPECTRA's own SDK session now run inside yours. Full detail in the
[AI Models & Cost Guide](ai-models-cost-guide.md).

---

## Do you need to change anything?

1. **Nothing is destructive.** A pre-v2 `spectra.config.json` with `ai.providers`/`ai.critic` still
   loads; the keys are simply ignored. `spectra init`/`spectra validate` no longer ask about or
   warn on AI providers.
2. **Run `spectra update-skills`** after upgrading to pick up the new/changed bundled skills and
   agents (`spectra-critic`, `spectra-review-flagged`, the rewritten `spectra-generate` and
   `spectra-execute`). Files you've customized are skipped with a warning rather than overwritten.
3. **Nothing to clean up in `.vscode/mcp.json`.** SPECTRA no longer writes its own entry there; a
   peer tool's entry (e.g. a BELLATRIX/Nova MCP server) is left untouched either way.
4. **Retire any script that called `spectra ai generate` or `spectra auth` directly.** See
   "Scripting / CI" above.
