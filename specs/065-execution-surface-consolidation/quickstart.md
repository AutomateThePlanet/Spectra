# Quickstart: Driving execution from the CLI (`spectra run`)

This is the end-to-end manual-execution loop after Spec 065 — one install, no MCP server, no MCP config.

## Install (once)

```bash
dotnet tool install -g Spectra
```

That single tool now provides **both** generation (`spectra ai …`) and execution (`spectra run …`). No `Spectra.MCP` install and no `.vscode/mcp.json` / `.mcp.json` / `claude_desktop_config.json` are required for the CLI execution path.

## Run a suite

```bash
# See what's runnable
spectra run list-suites

# Start a run (optionally filter)
spectra run start checkout --priorities high --name "Checkout smoke"
#  → prints run id and the first actionable test (priority-then-topological order)

# See where you are
spectra run status            # active run for the current user

# Inspect the current test
spectra run show <run-id>     # auto-resolves the in-progress/next-pending test

# Record a verdict and get the next test
spectra run advance <handle> --status pass
spectra run advance <handle> --status fail --notes "Login button unresponsive"
spectra run skip   <handle> --reason "Env unavailable"
spectra run note   <handle> --note "Repro intermittent; 2/5 attempts"

# Attach failure evidence (local CLI is the host)
spectra run screenshot           <handle> --file ./bug.png
spectra run screenshot-clipboard <handle>

# Re-run a flaky test
spectra run retest <run-id> --test-id TC-201

# Finish
spectra run finalize <run-id>          # blocks if tests still pending
spectra run finalize <run-id> --force  # finalize anyway; generates reports
```

## Equivalence to MCP (what to expect)

Every `spectra run` subcommand calls the **same** `ExecutionEngine` over the same `.execution/spectra.db` as the MCP tool of the same name. Dependency-blocking, priority/topological ordering, status counts, handle semantics, and terminal outcomes are identical — whether you drive via the CLI (short-lived process) or MCP (long-lived server), because the queue is reconstructed losslessly from the DB (Spec 064).

## Agent-driven loop

The `spectra-execute` SKILL and the re-pointed execution agent drive this loop with human-in-the-loop discipline:

1. Present **one** test at a time (numbered steps, expected result, progress).
2. **Wait** for the human's verdict — never fabricate a result, never auto-advance.
3. For FAIL/BLOCKED/SKIP, **ask for the reason first**, then record it.
4. `finalize` when all tests are terminal; open the HTML report; offer to log bugs for failures.

## Validation gates (acceptance)

- **SC-001**: complete `start → advance(all) → finalize` on a box with only `spectra` installed and no MCP config files present.
- **SC-002**: a `run` subcommand and its MCP tool leave equivalent DB state for the same run.
- **SC-003/SC-004**: `Spectra.MCP.Tests`, `Spectra.Core.Tests`, and `TestPersistenceServiceTests` pass unchanged.
- **SC-005**: concurrent short-lived writers complete with zero database-locked failures (WAL + busy_timeout).
- **SC-006**: the loop never advances without an explicit verdict and never records an unsupplied one.
