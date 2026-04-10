# Data Model: Testimize Integration

**Feature**: 038-testimize-integration
**Date**: 2026-04-10

## Overview

This feature adds one new on-disk config section, two new in-memory entities, and one new typed result model. It does NOT introduce any test-file schema changes, database migrations, or new file types on disk.

## On-Disk Entities

### TestimizeConfig (NEW root section in `spectra.config.json`)

**File**: `src/Spectra.Core/Models/Config/TestimizeConfig.cs`

| Field | Type | JSON name | Default | Notes |
|-------|------|-----------|---------|-------|
| Enabled | bool | `enabled` | `false` | Master switch. When false, no MCP process is spawned and no Testimize tools are registered. |
| Mode | string | `mode` | `"exploratory"` | Either `"exploratory"` (Testimize generates from ranges) or `"precise"` (caller supplies explicit values). |
| Strategy | string | `strategy` | `"HybridArtificialBeeColony"` | One of `Pairwise`, `Combinatorial`, `HybridArtificialBeeColony`, `PairwiseOptimized`, `CombinatorialOptimized`. Unknown values fall back to default. |
| SettingsFile | string? | `settings_file` | `null` | Optional path to a `testimizeSettings.json` file consumed by the Testimize server itself. |
| Mcp | TestimizeMcpConfig | `mcp` | `new()` | Sub-object describing how to start the MCP server. |
| AbcSettings | TestimizeAbcSettings? | `abc_settings` | `null` | Optional ABC algorithm tuning. Null = use Testimize built-in defaults. |

### TestimizeMcpConfig (sub-object)

| Field | Type | JSON name | Default |
|-------|------|-----------|---------|
| Command | string | `command` | `"testimize-mcp"` |
| Args | string[] | `args` | `["--mcp"]` |

### TestimizeAbcSettings (optional sub-object)

| Field | Type | JSON name | Default |
|-------|------|-----------|---------|
| TotalPopulationGenerations | int | `total_population_generations` | `100` |
| MutationRate | double | `mutation_rate` | `0.6` |
| FinalPopulationSelectionRatio | double | `final_population_selection_ratio` | `0.5` |
| EliteSelectionRatio | double | `elite_selection_ratio` | `0.3` |
| AllowMultipleInvalidInputs | bool | `allow_multiple_invalid_inputs` | `false` |
| Seed | int? | `seed` | `null` |

### SpectraConfig (modified)

**File**: `src/Spectra.Core/Models/Config/SpectraConfig.cs`

Add one property:

| Field | Type | JSON name | Default |
|-------|------|-----------|---------|
| Testimize | TestimizeConfig | `testimize` | `new()` |

**Backward compatibility**: Configs that pre-date this feature have no `testimize` key. They MUST deserialize successfully with `Testimize` set to a default-constructed `TestimizeConfig` (i.e., `Enabled = false`). This is the standard `System.Text.Json` behavior for missing properties when the property is initialized to a non-null default.

## In-Memory Entities (not persisted)

### TestimizeMcpClient

**File**: `src/Spectra.CLI/Agent/Testimize/TestimizeMcpClient.cs`

A disposable wrapper around the Testimize child process and its stdio JSON-RPC pipe.

| Member | Purpose |
|--------|---------|
| `StartAsync(TestimizeMcpConfig, CancellationToken)` | Launch the child process. Returns false (no exception) if the executable is not found or exits immediately. |
| `IsHealthyAsync(CancellationToken)` | Probe the server with a lightweight tool list call. 5s timeout. |
| `CallToolAsync(string toolName, JsonElement parameters, CancellationToken)` | JSON-RPC `tools/call`. 30s per-call timeout. Returns null on any failure. |
| `DisposeAsync()` | Kill the child process and dispose pipes. Idempotent. |

**Lifecycle**: Created inside `GenerationAgent.GenerateTestsAsync` after the existing tools list is built; disposed in a `finally` block before the method returns.

**Threading**: Single-threaded use only (one generation run owns one client). No internal locking.

### TestimizeTools (static factory)

**File**: `src/Spectra.CLI/Agent/Testimize/TestimizeTools.cs`

Static factory class that produces `AIFunction` instances for the AI runtime.

| Factory | Purpose |
|---------|---------|
| `CreateGenerateTestDataTool(TestimizeMcpClient client, TestimizeConfig config)` | Returns an `AIFunction` whose body forwards calls to `client.CallToolAsync` for the appropriate Testimize MCP method (e.g., `generate_hybrid_test_cases`, `generate_pairwise_test_cases`) based on `config.Strategy`. |
| `CreateAnalyzeFieldSpecTool()` | Returns a local `AIFunction` (no MCP call) that parses a text snippet and extracts field specifications heuristically. Local-only; survives when the MCP client is unavailable, but is only registered alongside the MCP-backed tool. |

## Typed Result Models

### TestimizeCheckResult (NEW)

**File**: `src/Spectra.CLI/Results/TestimizeCheckResult.cs`

The structured JSON output for `spectra testimize check --output-format json`.

```json
{
  "command": "testimize-check",
  "status": "completed",
  "enabled": true,
  "installed": true,
  "healthy": true,
  "mode": "exploratory",
  "strategy": "HybridArtificialBeeColony",
  "settings_file": "testimizeSettings.json",
  "settings_file_found": true,
  "version": "1.2.0",
  "install_command": null
}
```

| Field | Type | Notes |
|-------|------|-------|
| Enabled | bool | Mirrors `testimize.enabled`. |
| Installed | bool | True if the MCP child process started successfully. |
| Healthy | bool | True if the server passed the health probe. |
| Mode | string | Mirror of config. |
| Strategy | string | Mirror of config. |
| SettingsFile | string? | Mirror of config. |
| SettingsFileFound | bool | True if `SettingsFile` exists on disk (or null if no settings file is configured). |
| Version | string? | Reported by the server, if known. |
| InstallCommand | string? | Populated only when `Installed = false`, with the recommended `dotnet tool install` command. |

`status` is always `completed` (this command never fails — even "tool not installed" is a normal report state).

## State Transitions

None. All entities are stateless or single-use within one CLI invocation.

## Validation Rules

| Rule | Source | Enforcement |
|------|--------|-------------|
| `mode` ∈ {exploratory, precise} | spec FR-003 | Soft: unknown values are accepted at parse time and forwarded; the Testimize server may reject. |
| `strategy` ∈ allowed list | spec FR-004, FR-007 | Soft fallback: unknown strategy → use default. Implemented in `TestimizeMcpClient` (or `GenerationAgent` site) right before the call is made. |
| Per-call timeout 30s | spec FR-013 | Hard: enforced inside `CallToolAsync` via a linked `CancellationTokenSource`. |
| Health probe timeout 5s | spec SC-007 | Hard: enforced inside `IsHealthyAsync`. |
| MCP client disposed in all exit paths | spec FR-015, SC-006 | Hard: `try/finally` in `GenerateTestsAsync` and in `TestimizeCheckHandler`. |

## Migration

No migration needed.

- **Existing `spectra.config.json` files** without a `testimize` section deserialize cleanly (the property defaults to `new TestimizeConfig()` with `Enabled = false`).
- **Existing user-edited prompt templates** without the `{{#if testimize_enabled}}` block continue to work — the conditional simply doesn't appear in the rendered prompt. Users who want the new block can run `spectra prompts reset behavior-analysis test-generation`.
- **No on-disk file format changes**, no schema versions to bump, no database migrations.
