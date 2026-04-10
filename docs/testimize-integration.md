---
title: Testimize Integration
nav_order: 12
---

# Testimize Integration

Optional integration with [Testimize](https://github.com/AutomateThePlanet/Testimize.MCP.Server)
that lets SPECTRA's AI generation pipeline call Testimize for **algorithmic
test data optimization** (boundary value analysis, equivalence partitioning,
pairwise covering arrays, ABC heuristic optimization).

> **Disabled by default.** SPECTRA works fine without Testimize — the AI
> applies ISTQB techniques (spec 037) and approximates boundary values from
> documentation. Testimize adds *algorithmic precision* on top.

## Prerequisites

- Testimize MCP Server installed as a global .NET tool
- SPECTRA v1.38+

## Setup

1. Install the Testimize MCP server (one-time):

   ```bash
   dotnet tool install --global Testimize.MCP.Server
   ```

2. Enable in `spectra.config.json`:

   ```json
   {
     "testimize": {
       "enabled": true,
       "mode": "exploratory",
       "strategy": "HybridArtificialBeeColony"
     }
   }
   ```

3. Verify:

   ```bash
   spectra testimize check
   ```

## How it works

When enabled, `spectra ai generate` registers two extra AI tools alongside
the existing seven generation tools:

- **GenerateTestData** — forwards field specifications to Testimize and
  returns boundary values, equivalence classes, and pairwise combinations.
- **AnalyzeFieldSpec** — local heuristic that extracts field specs from a
  documentation snippet (e.g., "username: 3-20 characters" → `{type: text,
  min: 3, max: 20}`).

The AI reads your documentation, identifies input fields with validation
rules, calls `AnalyzeFieldSpec` to extract them, then calls
`GenerateTestData` to compute mathematically optimal test values. It uses
those exact values verbatim in the generated test cases instead of
approximating them.

## Configuration reference

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `enabled` | bool | `false` | Master switch. |
| `mode` | string | `"exploratory"` | `"exploratory"` (ranges → values) or `"precise"` (explicit values + error messages). |
| `strategy` | string | `"HybridArtificialBeeColony"` | One of `Pairwise`, `Combinatorial`, `HybridArtificialBeeColony`, `PairwiseOptimized`, `CombinatorialOptimized`. |
| `settings_file` | string? | `null` | Optional path to a `testimizeSettings.json` consumed by the Testimize server. |
| `mcp.command` | string | `"testimize-mcp"` | Command to start the MCP server. |
| `mcp.args` | string[] | `["--mcp"]` | Arguments to pass. |
| `abc_settings` | object? | `null` | Optional ABC tuning. When null, Testimize uses its built-in defaults. |
| `abc_settings.seed` | int? | `null` | Fix the seed for reproducible runs. |

## Without Testimize

SPECTRA does not require Testimize. When disabled (the default), the AI
generation pipeline uses spec 037's ISTQB technique-driven prompts and
approximates boundary values from documentation. You get systematic boundary
coverage either way — Testimize just makes the values mathematically
optimal instead of AI-approximated.

## Graceful degradation

If `testimize.enabled` is `true` but the tool is missing, crashes, or times
out, SPECTRA logs a warning and falls back to AI-only generation for the
affected request. `spectra ai generate` always exits with code 0 in
degradation paths.

## Health check

```bash
spectra testimize check
spectra testimize check --output-format json
```

JSON output contains the fields `enabled`, `installed`, `healthy`, `mode`,
`strategy`, and (when not installed) `install_command`.
