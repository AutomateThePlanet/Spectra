# Phase 1 Data Model: Remove the Spectra.MCP Execution Adapter

**No runtime data model changes.** This feature is a transport removal. The execution engine's state
model, the SQLite schema (`.execution/spectra.db`: `runs`, `results`, `queue_snapshot`, …), and every
`Spectra.Core` / `Spectra.Execution` model type are **unchanged** (FR-009, out-of-scope: engine/state/schema).

The "entities" here are build/project and test artifacts being moved or removed.

## Artifact inventory

| Artifact | Kind | Action | Notes |
|----------|------|--------|-------|
| `Spectra.MCP` project | .NET exe project | DELETE | Server/, Tools/, Program.cs, Infrastructure/McpLogging.cs, .csproj, nupkg packaging |
| `spectra-mcp` dotnet tool | NuGet tool package | STOP PUBLISHING | `PackAsTool`/`ToolCommandName`/`PackageId` removed with the project |
| `Spectra.slnx` | solution file | EDIT | Remove `Spectra.MCP` + `Spectra.MCP.Tests` project entries |
| `Spectra.MCP.Tests` project | xUnit test project | DELETE (after triage) | All files relocated/ported/retired first (research.md R4) |
| `Spectra.Execution.Tests` project | xUnit test project | GROW | Receives relocated engine tests + ported engine-flow tests; new subfolders `Execution/`, `Storage/`, `Models/`, `Helpers/`, `Reports/`, `Integration/` |
| `Spectra.Integration.Tests` project | xUnit test project | EDIT | Drop `Spectra.MCP` ProjectReference; re-point `IntegrationWorkspace` to engine/CLI |
| `VsCodeMcpConfigInstaller.cs` | source | DELETE | `.vscode/mcp.json` writer |
| `ClaudeSettingsInstaller.cs` | source | DELETE | `mcp__spectra__*` allowlist writer (verify no non-MCP use first) |
| `InitHandler.cs` | source | EDIT | Remove MCP emission calls + const + log lines |
| `spectra-execute.md` | bundled skill | EDIT | CLI-only |
| `spectra-execution.agent.md` | bundled agent | EDIT | Remove SPECTRA-MCP fallback; keep SUT/bug-log MCP |
| `docs/architecture/ARCHITECTURE-v2.md` | doc | EDIT | Drop MCP-only / do-not-unify claims |
| `docs/specs/ARCHITECTURE-v2.md` | doc | EDIT | Same (duplicate) |

## Preserved-unchanged (canary + engine)

- `Spectra.Core` and `Spectra.Core.Tests` — untouched.
- `TestPersistenceService` and its tests — untouched.
- `Spectra.Execution` engine source — untouched (incl. cosmetic `Spectra.MCP.*` namespaces, FR-017).
- `Spectra.Execution.Tests` **pre-existing** files (`ExtractionSmokeTests`, `Identity/…`, `Storage/WalConcurrencyTests`) — untouched; only **added to**.

## State transition (build/solution), not runtime

```
Before: [Spectra.CLI] [Spectra.Core] [Spectra.Execution] [Spectra.MCP→Execution,Core]
        tests: Core.Tests, CLI.Tests, Execution.Tests, Integration.Tests(+MCP), MCP.Tests(→MCP)

After:  [Spectra.CLI] [Spectra.Core] [Spectra.Execution]
        tests: Core.Tests, CLI.Tests, Execution.Tests(+relocated/ported), Integration.Tests(no MCP)
```

The single invariant: **no `Spectra.MCP` reference remains anywhere that ships or builds**, and **no test
behavior is lost** (every retired test has a named equivalent — see research.md R4).
