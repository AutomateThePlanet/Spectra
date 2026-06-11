# Feature Specification: Remove the Spectra.MCP Execution Adapter

**Feature Branch**: `070-remove-mcp-adapter`
**Created**: 2026-06-11
**Status**: Draft
**Input**: User description: "Remove the Spectra.MCP execution adapter entirely — converge on a single CLI execution surface (`spectra run`). This reverses Spec 065's deliberate 'keep MCP as a thin adapter' decision; that reversal is an accepted product decision."

## Overview

SPECTRA today ships **two** execution surfaces over one engine: the first-class CLI surface
(`spectra run …`) and a thin MCP adapter (`Spectra.MCP`) that re-exposes the same engine as 25 JSON-RPC
tools. Spec 065 deliberately kept the MCP adapter "as an optional networked path" (`specs/065-execution-surface-consolidation/spec.md:12,153`). This feature **reverses that decision**: it removes
SPECTRA's own MCP adapter so SPECTRA becomes a single-surface CLI tool with no MCP server.

This is a **transport removal, not an engine change**. The execution engine is already transport-neutral
in `Spectra.Execution`, referencing only `Spectra.Core` with zero MCP/JSON-RPC dependency
(`src/Spectra.Execution/Spectra.Execution.csproj:27`, `ExecutionEngine.cs:13-38`). A first-class CLI
surface already drives the **same** engine over the **same** SQLite database with no MCP server
(`RunCommand.cs`, `RunHandler.cs`, `RunServices.cs:48-54`). Run state is durable in SQLite via a
build-time orchestration snapshot with cold-process queue reconstruction that fails loud
(`ExecutionEngine.cs:83-96,144-163,189-220`), so nothing about execution requires a live session.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run a full execution lifecycle with no MCP server present (Priority: P1)

A QA engineer or developer installs only `spectra` on a clean machine and runs a complete manual test
execution lifecycle — start a run, advance tests through verdicts, pause/resume, capture a screenshot,
bulk-record remaining results, and finalize with a report — entirely through `spectra run …`, with no
MCP server installed, configured, or running.

**Why this priority**: This is the core promise of the feature. If the CLI surface cannot stand alone
for a full lifecycle, removing the MCP adapter is a regression rather than a consolidation. Everything
else (init cleanup, skill re-homing, doc updates) is in service of this guarantee.

**Independent Test**: On a machine with only the `spectra` tool installed (no `spectra-mcp`), drive
`spectra run start → advance → pause → resume → screenshot → bulk-record → finalize` against a sample
suite and confirm each step succeeds and the final report is produced — without any MCP process.

**Acceptance Scenarios**:

1. **Given** a clean machine with only `spectra` installed and no MCP server, **When** the user runs
   `spectra run start <suite>` then advances each test to a verdict and runs `spectra run finalize`,
   **Then** the run completes and a report is generated, with no MCP server involved at any point.
2. **Given** an in-progress run, **When** the user runs `spectra run pause` and later `spectra run resume`
   in separate short-lived CLI processes, **Then** run state is reconstructed losslessly from SQLite and
   execution continues from the correct position.
3. **Given** the current test under execution, **When** the user runs `spectra run screenshot --file <path>`
   or `spectra run screenshot-clipboard`, **Then** the screenshot is attached to the current test via the
   shared screenshot service with no MCP server present.
4. **Given** several remaining tests, **When** the user runs `spectra run bulk-record --status <s> --remaining`,
   **Then** the remaining tests are recorded in one operation, matching prior MCP `bulk_record_results` behavior.

---

### User Story 2 - `spectra init` produces no MCP wiring (Priority: P1)

A user runs `spectra init` on a fresh repository. The initialization writes the authoring/execution
scaffolding but produces **no** `.vscode/mcp.json` and **no** `mcp__spectra__*` entry in
`.claude/settings.json`, and nothing it emits references a `spectra-mcp` server.

**Why this priority**: Init is the first thing a new user runs. Leaving behind MCP wiring for a server
that no longer exists would point users at a dead component and contradict the single-surface model.
This must ship with Story 1 or init actively misleads.

**Independent Test**: Run `spectra init` in an empty repository and inspect the emitted files: assert no
`.vscode/mcp.json` is created, no `mcp__spectra__*` allowlist entry appears in `.claude/settings.json`,
and no emitted file mentions `spectra-mcp`.

**Acceptance Scenarios**:

1. **Given** an empty repository, **When** the user runs `spectra init`, **Then** no `.vscode/mcp.json`
   file is created by SPECTRA.
2. **Given** an empty repository, **When** the user runs `spectra init`, **Then** the generated
   `.claude/settings.json` contains no `mcp__spectra__*` entry.
3. **Given** a repository that already contains a `.vscode/mcp.json` from a *peer* tool (e.g. a BELLATRIX
   server), **When** the user runs `spectra init`, **Then** that peer file is left untouched (SPECTRA
   neither writes to nor removes it).

---

### User Story 3 - Execution skill and agent reference only `spectra run` (Priority: P2)

The bundled execution SKILL (`spectra-execute.md`) and execution agent (`spectra-execution.agent.md`)
describe the manual-execution workflow exclusively in terms of `spectra run …`, with no optional MCP
path and no residual `mcp__spectra__*` fallback guidance.

**Why this priority**: The skill/agent are the operational guidance the model follows during a run. They
already lead with `spectra run` and "no MCP server required" but still carry residual MCP fallback notes
(`spectra-execution.agent.md:19-20`). Leaving stale fallback text would tell the model to reach for a
server that no longer exists. P2 because Story 1's engine path works regardless, but guidance must match.

**Independent Test**: Read the shipped skill and agent content and confirm every execution instruction
targets a `spectra run …` command and there is no instruction to use `mcp__spectra__*` tools or an MCP
server as an alternative execution path.

**Acceptance Scenarios**:

1. **Given** the bundled `spectra-execute` skill, **When** its content is reviewed, **Then** it contains
   no optional-MCP-path guidance and references only `spectra run …` commands.
2. **Given** the bundled `spectra-execution` agent, **When** its content is reviewed, **Then** the
   "Networked/remote setups may instead drive execution over the SPECTRA MCP server" fallback is removed
   and the SUT-driving MCP (BELLATRIX/Nova) reference, which is a *separate* component, is preserved.

---

### User Story 4 - Solution builds and the test suite stays green with `Spectra.MCP` gone (Priority: P1)

A maintainer removes the `Spectra.MCP` project from the solution. The solution builds, the full test
suite is green, and no test behavior that was unique to the MCP test corpus is lost — every behavior
formerly proven only by an MCP transport/tool test has a confirmed equivalent on the `spectra run` CLI
path or in the `Spectra.Execution` engine tests.

**Why this priority**: The ~14 MCP transport tests are Spec 065's SC-003 behavior-preservation proof
(`specs/065-execution-surface-consolidation/spec.md:130,153`). Deleting tests that leave a coverage hole
is a regression, not cleanup. The build must also stay intact — and the test topology has two
dependencies the headline request did not call out (see Edge Cases). P1 because a red build or a coverage
hole blocks the whole feature.

**Independent Test**: With `Spectra.MCP` removed from `Spectra.slnx` and its project deleted, run a full
`dotnet build` and `dotnet test`; confirm a green suite and a completed transport→CLI/engine mapping table
in which every retired-test row has a named equivalent.

**Acceptance Scenarios**:

1. **Given** the `Spectra.MCP` project is deleted and removed from `Spectra.slnx:6`, **When** the solution
   is built, **Then** the build succeeds with no dangling project or package references.
2. **Given** the MCP test corpus is being retired, **When** any test is deleted, **Then** a mapping table
   row records its equivalent assertion on the CLI path or in the engine tests, and no row is left without
   an equivalent.
3. **Given** engine-level tests that currently live under `tests/Spectra.MCP.Tests/` but exercise
   `Spectra.Execution` types (the `Execution/`, `Storage/`, `Reports/`, `Models/`, `Helpers/` folders),
   **When** the MCP test project is removed, **Then** those tests are **preserved** (relocated, e.g. to
   `Spectra.Execution.Tests`) rather than deleted.
4. **Given** the canary set — `Spectra.Core` tests, `TestPersistenceService` tests, and `Spectra.Execution`
   engine tests — **When** the work is in progress and at completion, **Then** they remain green and
   unmodified throughout; any failure in this set is treated as a genuine regression and is a stop signal.

---

### Edge Cases

- **`Spectra.Integration.Tests` depends on `Spectra.MCP`** (`tests/Spectra.Integration.Tests/Spectra.Integration.Tests.csproj` — "the only test project that references both assemblies"; it exercises the MCP execution/discovery tools in a cross-spec flow). Deleting `Spectra.MCP` breaks this project's build. It must be re-pointed to drive execution through the `spectra run` CLI surface / `Spectra.Execution` engine, or its MCP-tool-specific assertions migrated, so the solution still builds and the cross-spec coverage survives. *(Not named in the headline request; surfaced during grounding.)*
- **The MCP test project's scope is larger than "~14 transport tests."** `tests/Spectra.MCP.Tests/` also contains ~30 `Tools/` tests and a `Server/` protocol test that are genuinely adapter/transport-specific, **plus** engine-level tests (`Execution/`, `Storage/`, `Reports/`, `Models/`, `Helpers/`) that test `Spectra.Execution` types and only carry `Spectra.MCP.*` namespaces cosmetically. The whole project references only `Spectra.MCP` and stops compiling on deletion, so every file must be triaged: preserve/relocate the engine tests; map-then-retire the adapter/transport tests.
- **A peer `.vscode/mcp.json` already exists** (e.g. a BELLATRIX MCP). Init currently merges-by-key on `spectra` and preserves peers (`InitHandler.cs:384`); after removal init must simply not add a `spectra` key and must still leave any peer file untouched — it must not delete a file it no longer writes to.
- **A pre-existing workspace already has `.vscode/mcp.json` and the `mcp__spectra__*` allowlist from a prior `spectra init`.** This feature changes what *new* init emits; it does not actively scrub already-installed wiring from existing user repos. Behavior for pre-existing wiring is documented as out of scope (see Out of Scope).
- **An external/networked consumer is calling the SPECTRA MCP server.** After removal the server no longer exists and such a consumer breaks. This is an explicitly accepted risk (see Risks).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST delete the `Spectra.MCP` project in its entirety — the `Server/` layer
  (`McpServer.cs`, `McpProtocol.cs`, `ToolRegistry.cs`), the `Tools/` tool-registry layer, and the
  `Program.cs` tool registration of the 25 execution/data tool schemas (`Program.cs:136-167`).
- **FR-002**: The system MUST stop packaging the `spectra-mcp` dotnet tool (`Spectra.MCP.csproj:7,10-11`,
  `PackAsTool`/`ToolCommandName`/`PackageId`) so no MCP server artifact is produced or published.
- **FR-003**: The system MUST remove the `Spectra.MCP` project from the solution (`Spectra.slnx:6`).
- **FR-004**: `spectra init` MUST NOT emit a `.vscode/mcp.json` file; the VS Code MCP config emission
  (`InitHandler.cs:25,87-88,381-396`, `VsCodeMcpConfigInstaller.cs`) MUST be removed.
- **FR-005**: `spectra init` MUST NOT write a `mcp__spectra__*` entry into `.claude/settings.json`; the
  client-side MCP allowlist installation (`InitHandler.cs:70-72`, `ClaudeSettingsInstaller.cs:8,16-17`)
  MUST be removed. *(The settings file itself may still be emitted for non-MCP purposes; only the MCP
  allowlist entry is removed.)*
- **FR-006**: `spectra init` output and emitted files MUST NOT reference a `spectra-mcp` server anywhere
  (including the init success log lines at `InitHandler.cs:105,115`).
- **FR-007**: The bundled execution SKILL (`spectra-execute.md`) MUST describe execution exclusively
  through `spectra run …` with no optional MCP path.
- **FR-008**: The bundled execution agent (`spectra-execution.agent.md`) MUST remove the optional
  "drive execution over the SPECTRA MCP server" fallback (`spectra-execution.agent.md:19-20`) while
  preserving references to the *separate* SUT-driving MCP (BELLATRIX/Nova) and any bug-logging MCP.
- **FR-009**: The execution engine MUST remain behaviorally unchanged: `Spectra.Execution` and its tests
  MUST remain green and unmodified. This feature changes no engine behavior, state model, or SQLite schema.
- **FR-010**: The full execution lifecycle — start, advance, skip, note, bulk-record, retest,
  screenshot/screenshot-clipboard, pause, resume, cancel, finalize — MUST be available through `spectra run`
  with no MCP server present, preserving the guardrails (present test → wait for human verdict → advance;
  never fabricate a verdict; never auto-advance).
- **FR-011**: Before any MCP test is deleted, the system MUST establish, for each behavior that test
  asserts, an equivalent assertion on the `spectra run` CLI path or in the `Spectra.Execution` engine
  tests; a deliverable mapping table MUST record each retired test → its equivalent, and deletion is
  permitted only once every row has an equivalent.
- **FR-012**: Engine-level tests currently under `tests/Spectra.MCP.Tests/` that exercise
  `Spectra.Execution` types (`Execution/`, `Storage/`, `Reports/`, `Models/`, `Helpers/`) MUST be
  preserved by relocating them (e.g. into `Spectra.Execution.Tests`), not deleted.
- **FR-013**: `tests/Spectra.Integration.Tests` MUST continue to build and pass after `Spectra.MCP` is
  removed — its `Spectra.MCP` project reference MUST be removed and any MCP-tool-specific cross-spec
  coverage re-pointed to the `spectra run` CLI surface / `Spectra.Execution` engine.
- **FR-014**: After completion, the solution MUST build and the full test suite MUST be green, with no
  references to `Spectra.MCP` remaining in any project, solution, skill, agent, or doc that ships.
- **FR-015**: `docs/architecture/ARCHITECTURE-v2.md` and its duplicate `docs/specs/ARCHITECTURE-v2.md`
  MUST be updated to drop the "Execution → MCP, only here" / "stateful session → MCP" / "Do not unify"
  claims (`ARCHITECTURE-v2.md:38,41,43,99`), reflecting the single CLI execution surface.
- **FR-016**: The canary set — `Spectra.Core` tests, `TestPersistenceService` tests, and
  `Spectra.Execution` engine tests — MUST remain green and unmodified throughout; a failure in this set
  is a genuine regression and a stop signal, not a thing to "fix" by editing those tests.
- **FR-017**: Renaming the cosmetic `Spectra.MCP.*` namespaces that live inside the `Spectra.Execution`
  engine library (065's "zero using edits" design) is OPTIONAL and out of scope unless trivial; the work
  MUST NOT let such a rename expand the blast radius.

### Key Entities *(include if feature involves data)*

- **`Spectra.MCP` project**: The MCP execution adapter being removed — server layer, tool registry, 25
  tool schemas, and the `spectra-mcp` dotnet-tool packaging. Has no behavior the engine lacks.
- **`Spectra.Execution` engine**: The transport-neutral execution engine + storage + reports, referenced
  by both CLI and (formerly) MCP. Untouched by this feature; the single source of execution behavior.
- **`spectra run` CLI surface**: The first-class CLI execution commands (`Commands/Run/`) that become the
  *only* execution surface after removal.
- **Init MCP emissions**: `.vscode/mcp.json` (via `VsCodeMcpConfigInstaller`) and the `mcp__spectra__*`
  allowlist entry in `.claude/settings.json` (via `ClaudeSettingsInstaller`) — both removed from init.
- **MCP test corpus** (`tests/Spectra.MCP.Tests/`): mixed — adapter/transport tests (`Tools/`, `Server/`)
  to map-then-retire, and engine tests (`Execution/`, `Storage/`, `Reports/`, `Models/`, `Helpers/`) to
  preserve/relocate.
- **Transport→CLI/engine mapping table**: the deliverable that proves no behavior coverage is lost; one
  row per retired test with its named equivalent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a clean machine with only `spectra` installed and no MCP server present, a user completes
  a full execution lifecycle (start → advance → pause → resume → screenshot → bulk-record → finalize) with
  100% of steps succeeding and a final report produced.
- **SC-002**: `spectra init` on a fresh repository produces zero `.vscode/mcp.json` files written by
  SPECTRA and zero `mcp__spectra__*` allowlist entries, and zero references to `spectra-mcp` in any
  emitted file or output line.
- **SC-003**: The shipped execution skill and agent contain zero instructions to use an MCP server or
  `mcp__spectra__*` tools as an execution path (the separate SUT-driving / bug-logging MCP references may
  remain).
- **SC-004**: The solution builds with `Spectra.MCP` fully removed, and the full test suite is green, with
  zero remaining references to `Spectra.MCP` across projects, solution, skills, agents, and shipped docs.
- **SC-005**: The transport→CLI/engine mapping table has zero rows without a named equivalent; zero
  retired MCP tests leave a behavior uncovered elsewhere.
- **SC-006**: 100% of the engine-level tests that previously lived under `tests/Spectra.MCP.Tests/` are
  preserved (relocated) rather than deleted, and remain green.
- **SC-007**: The canary set (`Spectra.Core`, `TestPersistenceService`, `Spectra.Execution` engine tests)
  passes with zero modifications to those test files from start to finish.

## Out of Scope

- **The BELLATRIX/Nova MCP that drives the system-under-test** (the calculator app). It is a separate
  component, not SPECTRA's execution server (`ARCHITECTURE-v2.md:43`); removing SPECTRA's MCP does not
  touch it. Skill/agent references to it are preserved.
- **Any change to the execution engine's behavior, state model, or SQLite schema.** This is a transport
  removal, not an engine change.
- **Renaming the cosmetic `Spectra.MCP.*` namespaces inside `Spectra.Execution`** (065's "zero using
  edits" design) — optional, only if trivial; not a goal of this feature (FR-017).
- **Init's unrelated `.github/skills` emit cleanup and dashboard-skill staleness** — tracked separately.
  This feature owns ONLY the MCP-related init emissions (`.vscode/mcp.json`, the `mcp__spectra__*`
  allowlist). Do not absorb the unrelated init cleanups here.
- **Actively scrubbing already-installed MCP wiring from pre-existing user repos.** This feature changes
  what *new* `spectra init` emits; retroactive cleanup of prior installs is not in scope.

## Risks & Accepted Trade-offs

- **Breakage of unknown external/networked MCP consumers (ACCEPTED).** Spec 065 cited "unknown
  external/networked MCP consumers" as the reason to keep the adapter
  (`specs/065-execution-surface-consolidation/spec.md:50-54,153`). Removing the adapter accepts breakage
  for any such consumer. This is a knowing product decision recorded here; the population is unknowable
  and is not enumerated.
- **Loss of the networked execution path.** After removal, execution is local-CLI only. This is
  consistent with the grounding that nothing about execution requires a live session and that the local
  CLI host is a better screenshot host than a possibly remote MCP server. Networked execution, if ever
  needed again, would be a future, separate feature.
- **Test-coverage regression risk during retirement.** Mitigated by FR-011/FR-012 (map-before-delete,
  preserve engine tests) and the canary guard (FR-016).

## Dependencies / Coordination

- Owns ONLY the MCP-related init emissions (`.vscode/mcp.json`, `mcp__spectra__*` allowlist). The
  `.github/skills` emit cleanup and dashboard-skill staleness are tracked separately and must not be
  absorbed here.
- Reverses the Spec 065 decision to keep MCP as a thin adapter (`specs/065-execution-surface-consolidation/spec.md:12,153`); this reversal is an accepted product decision.
- Newly surfaced during grounding (not in the headline request): `Spectra.Integration.Tests` references
  `Spectra.MCP` and must be re-pointed (FR-013) for the solution to build.

## Assumptions

- "Preserve" for the engine-level MCP tests means relocating them into `Spectra.Execution.Tests` with
  their assertions intact (chosen over leaving them in a renamed but otherwise-MCP project, since the MCP
  project is being deleted).
- `spectra init` may continue to emit `.claude/settings.json` for non-MCP purposes; only the
  `mcp__spectra__*` allowlist entry is removed, not the whole file.
- The `spectra-execute` skill's existing "No MCP server required" framing is correct and stays; only the
  residual MCP-fallback wording in the agent (`spectra-execution.agent.md:19-20`) needs removal.
- Re-pointing `Spectra.Integration.Tests` to the `spectra run` CLI / engine preserves its cross-spec
  intent; no integration scenario is unique to the MCP transport such that it cannot be expressed against
  the CLI/engine surface. (To be confirmed during the test-mapping step.)
