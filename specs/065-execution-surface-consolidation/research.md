# Phase 0 Research: Execution Surface Consolidation

All "unknowns" here are design decisions about *how* to extract and wire, not missing facts — the engine, tools, DB, and tests were read directly (`path:line` in the investigation doc and inline below).

## R1 — Extraction strategy: preserve namespaces vs. rename

**Decision**: Move the engine/storage/identity/config files into a new `Spectra.Execution` class library **without changing their namespaces** (`Spectra.MCP.Execution`, `Spectra.MCP.Storage`, `Spectra.MCP.Identity`, `Spectra.MCP.Infrastructure`).

**Rationale**: A `grep` for the namespaces proved that **every** protected test references them — all 15 `tests/Spectra.MCP.Tests/Tools/*.cs`, every `Integration/*.cs`, and `Spectra.Integration.Tests/Support/IntegrationWorkspace.cs` carry `using Spectra.MCP.Identity;` and/or `using Spectra.MCP.Infrastructure;`. Renaming would force edits to ~30 files the spec marks **DO NOT TOUCH**. A C# type's namespace is independent of its assembly, so a type can live in `Spectra.Execution.dll` while keeping namespace `Spectra.MCP.Execution`; consumers that reference the assembly resolve it through their existing `using`. Result: the extraction is *file move + `.csproj` reference rewiring only* — zero `using` edits, transport/tool/integration tests compile and pass byte-unchanged. That untouched-green corpus **is** the behavior-preservation proof (SC-003/SC-004). FR-001 says "no MCP/transport coupling in the relocated code" — satisfied: the code has no MCP *dependency*; the namespace string is cosmetic.

**Alternatives considered**:
- *Rename to `Spectra.Execution.*`*: cleaner long-term naming, but breaks the regression-net instruction and weakens the behavior-preservation guarantee for no functional gain. Deferred to a possible cosmetic-rename spec (YAGNI).
- *Reference `Spectra.MCP` from the CLI instead of extracting*: rejected — `Spectra.MCP` is an `OutputType=Exe`; referencing an executable to reuse its engine is the wrong dependency direction and drags MCP/ImageSharp/server wiring into the CLI.

## R2 — Engine's non-domain dependencies

**Decision**: Relocate `IUserIdentityResolver`/`UserIdentityResolver` and `McpConfig` into `Spectra.Execution` (namespaces preserved). Leave `McpLogging` in `Spectra.MCP`.

**Rationale**: The `ExecutionEngine` constructor is `(RunRepository, ResultRepository, QueueSnapshotRepository, IUserIdentityResolver, McpConfig)`. The ctor signature **cannot change** (protected tool tests construct the engine directly). So `Spectra.Execution` must own `IUserIdentityResolver` and `McpConfig`. Confirmed `_config` is stored but **never read** in any engine method (`grep _config` → only ctor assignment), so `McpConfig` travels purely to satisfy the signature; no behavior depends on it. `McpLogging` is used only by the MCP server, not the engine — it stays. `McpConfig.cs` also defines the `LogLevel` enum that `McpLogging` consumes; with `Spectra.MCP` referencing `Spectra.Execution`, `McpLogging` resolves `LogLevel` across the reference.

**Alternatives considered**: introduce a neutral `ExecutionConfig` and change the ctor — rejected (signature change cascades into protected tests).

## R3 — SQLite per-process safety (FR-004)

**Decision**: In `ExecutionDb.GetConnectionAsync`, immediately after `OpenAsync`, execute `PRAGMA journal_mode=WAL;` and `PRAGMA busy_timeout=5000;`.

**Rationale**: The investigation (Q5) found a bare `Data Source={dbPath}` connection with no PRAGMA — default `journal_mode=DELETE`, `busy_timeout=0`, so a second concurrent writer process hits `SQLITE_BUSY` immediately. WAL lets readers and a writer coexist and is persisted on the DB file (set once, sticks); `busy_timeout=5000` makes a contending writer wait up to 5 s and retry instead of failing instantly. Both are issued per connection at open, which is exactly the short-lived-process model. This lives in the shared assembly, so **both** CLI and MCP gain it. No file-lock is added (Constitution V) — the single-user model doesn't need true concurrent writers; the FileLockHandle pattern is available if that changes.

**Alternatives considered**: mirror `PersistentTestIdAllocator`'s `FileShare.None` cross-process lock — rejected as premature for single-user; WAL + busy_timeout is the minimum that satisfies SC-005.

## R4 — CLI command surface shape (FR-002)

**Decision**: Add a `spectra run` command group (`Commands/Run/RunCommand.cs`) following the established `DeleteCommand` → `DeleteHandler(verbosity, outputFormat)` pattern, with a `RunServices` factory that builds the engine + index/test-case/suite/selection loaders by porting `Spectra.MCP/Program.cs:38–130` (those loaders use only `Spectra.Core` types — `IndexWriter`, `TestCaseParser` — so they move to the CLI verbatim). Subcommands map one-to-one to the execution-specific MCP tools; A-class tools that already have first-class CLI homes are **not** duplicated.

**Rationale**: Mirroring the existing command/handler/JSON-writer convention keeps the CLI idiomatic and CI-friendly (deterministic exit codes, `--output-format json`, Constitution IV). The 25 MCP tools split into: execution-lifecycle + execution-reporting (new under `run`), and pure data/discovery that already exist as CLI commands. Duplicating the latter would violate Simplicity.

**Tool → CLI mapping** (full table in `contracts/run-cli.md`). Not re-implemented because already in the CLI: `validate_tests`→`spectra validate`; `rebuild_indexes`→`spectra index`; `find_test_cases`→`spectra list`/`show`; `analyze_coverage_gaps`→`spectra ai analyze`.

## R5 — Screenshot parity (FR-002)

**Decision**: Extract a neutral `ScreenshotService` into `Spectra.Execution/Screenshots/` carrying the ImageSharp encode/resize logic and the OS clipboard-capture shellout currently inside `SaveScreenshotTool`/`SaveClipboardScreenshotTool`. The MCP screenshot tools refactor to thin delegators (behavior-preserving); the CLI `spectra run screenshot` / `screenshot-clipboard` use the same service. `Spectra.Execution` therefore references `SixLabors.ImageSharp`.

**Rationale**: FR-002 requires the CLI to address the same operations the tools expose, and failure-evidence capture is part of the manual loop. A shared service is the one-implementation way to give both surfaces identical behavior. The investigation classified these as host-capability tools that a **local** CLI replicates as well as or better than a possibly-remote server. This is the lowest-priority slice (polish) so the core loop lands first.

**Alternatives considered**: keep screenshots MCP-only — rejected, would leave the CLI loop unable to attach failure evidence, undercutting FR-006's "no MCP needed for the CLI path."

## R6 — SKILL + agent for the CLI loop (FR-005)

**Decision**: Add a `spectra-execute` SKILL (embedded `Skills/Content/Skills/spectra-execute.md` + a `SkillContent.Execute` property) and re-point `spectra-execution.agent.md` at `spectra run …` invoked via the shell/Bash tool, porting the existing guardrails verbatim in spirit: present one test, collect the human verdict, **ask before recording FAIL/BLOCKED/SKIP**, never fabricate notes, never auto-advance. Keep the documentation-lookup and bug-logging sections.

**Rationale**: The execution agent already encodes the discipline (present → wait → advance; "NEVER fabricate failure notes"); the only change is the transport it drives (CLI subcommands instead of `mcp__spectra__*` tools). The SKILL registers automatically via the `Skills\Content\Skills\*.md` embed glob; `SkillContent` exposes it. The skills-manifest flag-conformance test must be told that `spectra-execute`'s `spectra run` commands are interactive-loop commands (like `spectra-generate`/`spectra-update` seam commands), not batch JSON commands, so the per-line `--no-interaction/--output-format/--verbosity` assertions exclude them.

**Rationale for retaining the MCP delegation table**: networked MCP clients still exist (US3); the agent keeps an MCP path as an option while defaulting to CLI.

## R7 — Regression & parity test strategy

**Decision**: (a) New `Spectra.Execution.Tests` for relocated-assembly smoke + WAL/busy_timeout concurrency (FR-004/SC-005). (b) New `Spectra.CLI.Tests/Commands/Run/*` for CLI↔engine parity, guardrail enforcement, and the single-install/no-MCP-config smoke (FR-007/SC-001/SC-002/SC-006). (c) Leave `Spectra.MCP.Tests` and `Spectra.Core.Tests`/`TestPersistenceServiceTests` **untouched** — their passing unchanged is SC-003/SC-004.

**Rationale**: Parity is best asserted at the engine boundary both surfaces share: a CLI handler and the matching tool both call the same `ExecutionEngine` over the same DB, so the strongest, least-brittle parity check drives an operation via the CLI handler and asserts the resulting DB state equals what the engine/tool path produces. The `Spectra.MCP.Tests` corpus already exhaustively covers the tool path; we do not re-assert it from the CLI, we assert the CLI reaches the same engine/state.
