# Quickstart: Execution Agent as Orchestrator

After this revision the SPECTRA execution agent no longer runs the per-test loop in chat. It
orchestrates and stays on-call; the tester drives the run in the browser console (Spec 066).

## The new flow

```
You:    "Run the checkout smoke tests."
Agent:  selects the tests → `spectra run start checkout --selection smoke`
        → `spectra run console`
        → "Run started. Open the console to drive it:  http://127.0.0.1:7878/
           Click PASS / FAIL / BLOCKED, add a comment, drop a screenshot. I'm on call if you
           need a step explained."
You:    (drive the run in the browser — every verdict is a click)

You:    (mid-run, in chat) "What does step 3 of the current test mean?"
Agent:  reads `spectra run status` (SQLite) for the current test
        → reads its `source_refs` docs with Read/Grep/Glob
        → gives a short grounded answer
You:    (back to the browser)

You:    "We're done — finalize."
Agent:  `spectra run finalize` → surfaces the HTML report
```

What changed: the agent never presents tests one-by-one in chat, never asks "pass or fail?", and never
records a verdict on your behalf. **The console is the verdict channel**; its buttons enforce the
discipline (explicit verdict, comment required for FAIL/BLOCKED/SKIP, no auto-advance) that the agent
prose used to promise.

## What the agent still does

- **Select** tests (suite, filters, saved selections, smart/risk-based intent).
- **Start** the run and **launch the console**, handing you the URL.
- **On-call**: answer step/expected-result questions from the source docs, reading current state from
  `spectra run status` (the database) — never from the browser page.
- **Lifecycle**: `finalize`, `pause`/`resume`/`cancel`, `retest`, resume-by-run-id, bug logging.
- **Non-execution CLI tasks** (update, coverage, validate, …) via its delegation table — unchanged.

## Validate the revision (developer)

```bash
dotnet build

# The two rewritten contract tests assert orchestrate-not-drive:
dotnet test --filter "FullyQualifiedName~Skills.ExecuteSkillTests"
dotnet test --filter "FullyQualifiedName~Skills.ExecutionAgentPortTests"

# The frozen oracle MUST stay green unchanged (≤200 agent lines, refs kept, counts 15/3):
dotnet test --filter "FullyQualifiedName~Skills.SkillsManifestTests"

# Full net — guardrail/engine/parity/MCP/Core unchanged and green (SC-005/SC-006):
dotnet test
```

Manual check (SC-001/SC-002): ask the agent to run a suite and confirm it launches the console and hands
over a URL instead of presenting tests; confirm it declines to record a verdict in chat and points to the
console.
