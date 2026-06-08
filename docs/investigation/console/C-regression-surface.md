# C. Regression Surface (both specs)

## C13. The console is a NEW transport over the SAME engine — these tests stay green and untouched

The console (Spec 1) and the agent/SKILL revision (Spec 2) must **not change engine behaviour**. The
following tests assert that behaviour. **If any of them needs to change, that is a signal the console
altered engine behaviour it should not** — stop and reconsider, don't edit the test.

### Engine / state-machine / reconstruction (`tests/Spectra.MCP.Tests/Execution/`)

> These live under `Spectra.MCP.Tests` but exercise the extracted `Spectra.Execution` engine (spec 065
> kept the namespaces, so the references are unchanged).

| File | Asserts |
|---|---|
| `StateMachineTests.cs` | Legal/illegal status transitions — the determinism core. |
| `DependencyResolverTests.cs` | Dependency-blocking propagation. |
| `ReconstructionParityTests.cs` | A queue reconstructed in a fresh engine is indistinguishable from the original in-memory queue (spec 064 parity). |
| `ReconstructionFailLoudTests.cs` | Missing/inconsistent/dangling snapshot fails loud (`QueueReconstructionException`), benign null only when truly not-found. |
| `ReconstructionBlockingParityTests.cs` | Blocking cascade is identical after reconstruction. |
| `ReconstructionOrderingParityTests.cs` | Next-test selection is priority-then-topological, not alphabetical, after reconstruction. |
| `TestQueueReconstructionTests.cs` | The `TestQueue.AddReconstructed` lossless-rebuild path. |
| `TestQueueFilterTests.cs` | Filtering by priorities/tags/components. |

These matter to the console because **every page refresh and write-back goes through the same
reconstruct-aware `GetQueueAsync`** (A3). The console must not need a different reconstruction path.

### CLI parity + guardrails (`tests/Spectra.CLI.Tests/Commands/Run/`)

| File | Asserts | Relevance |
|---|---|---|
| `ParityTests.cs` | `spectra run` handlers leave the **same** engine/DB state as driving `ExecutionEngine` directly (same status/notes/handle, same blocking, same ordering). | The console is a sibling transport; it must satisfy the **same** parity (see additive net below). |
| `GuardrailTests.cs` | Mechanical human-in-the-loop: advance without `--status` records nothing; records exactly the supplied verdict; fail without notes is rejected. | These are exactly the checks the console's write-back endpoint must replicate (B11). |
| `RunLoopSmokeTests.cs` | End-to-end `spectra run` loop. | Engine behaviour the console reuses. |

### Concurrency (`tests/Spectra.Execution.Tests/Storage/`)

| File | Asserts |
|---|---|
| `WalConcurrencyTests.cs` | WAL + `busy_timeout` lets concurrent short-lived writers proceed without `SQLITE_BUSY` (`ExecutionDb.cs:49-52`). |

This is the test that protects the detached console + concurrent `spectra run` writers (A7).

### MCP transport corpus (`tests/Spectra.MCP.Tests`, ~56 test files)

The whole MCP corpus — tool tests (`Tools/*`), integration (`Integration/ExecutionFlowTests`,
`BlockingCascadeTests`, `FilteredExecutionTests`, `ReconstructedExecutionFlowTests`), storage
(`Storage/ExecutionDbTests`), and the reconstruction-error surface
(`Tools/ReconstructionErrorSurfaceTests`) — proved (spec 065) that the engine extraction is
behaviour-preserving. A new HTTP transport must leave all of it **byte-unchanged green**.

### Core (`tests/Spectra.Core.Tests`)

All of it (parsing, validation, coverage, config, ID allocation, models, …) is untouched by the
console. It is **not** in the console's change set; if a console change reaches into `Spectra.Core`,
that is out of scope and a red flag.

### Skill/agent tests (`tests/Spectra.CLI.Tests/Skills/`)

| File | Note |
|---|---|
| `ExecuteSkillTests.cs` | Asserts the `spectra-execute` SKILL is bundled/registered and encodes the present→wait→advance guardrails driving `spectra run`. **Spec 2 will edit this on purpose** — the assertions about the per-test loop change when the console owns the loop. This is the one regression file that *should* move, and only in Spec 2. |
| `ExecutionAgentPortTests.cs` | Asserts the execution agent drives the CLI (not MCP-by-default). Spec 2 updates this alongside the agent rewrite. |

---

## The additive net Spec 1 *should* bring (not this investigation's job to write)

By the parity logic above, the eventual **Spec 1 should add a `ConsoleParityTests`-style net**: the
console write-back endpoint leaves the **same** DB state as `ExecutionEngine` direct / `RunHandler` —
mirroring `ParityTests.cs` and re-asserting the `GuardrailTests.cs` checks at the HTTP boundary (B11).
That is the new green the console brings; the tables above are the green it must **not** disturb.

---

## One-line rule for both specs

> Console = **new transport, same engine, same SQLite.** Engine/state-machine/reconstruction/parity/
> guardrail/WAL/MCP/Core tests stay green **unchanged**; only `ExecuteSkillTests`/`ExecutionAgentPortTests`
> change, and only in Spec 2.
