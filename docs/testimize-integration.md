---
title: Testimize Integration
nav_order: 12
---

# Testimize Integration

Optional integration with [Testimize](https://github.com/AutomateThePlanet/Testimize)
that adds **algorithmic test data optimization** to SPECTRA's AI generation
pipeline: precise boundary value analysis, equivalence partitioning,
pairwise covering arrays, and ABC heuristic optimization.

> **Disabled by default.** SPECTRA works fine without Testimize — the AI
> applies ISTQB techniques (spec 037) and approximates boundary values from
> documentation. Testimize adds *algorithmic precision* on top: exact min-1/
> min/max/max+1 boundaries, security-focused invalid patterns (XSS, SQLi,
> SSTI), and optimized multi-field combinations.

## Prerequisites

- SPECTRA v1.48.3+
- No separate installation needed — the Testimize library ships as a bundled
  NuGet dependency inside `Spectra.CLI`. When you install `spectra`, Testimize
  is included automatically.

## Setup

1. Enable in `spectra.config.json`:

   ```json
   {
     "testimize": {
       "enabled": true,
       "mode": "exploratory",
       "strategy": "HybridArtificialBeeColony"
     }
   }
   ```

2. Verify:

   ```bash
   spectra testimize check
   ```

   Expected output:
   ```
   Testimize Integration Status
     Enabled:     true
     Library:     loaded (v1.1.10)
     Ready:       yes
     Mode:        exploratory
     Strategy:    HybridArtificialBeeColony

   Ready to generate optimized test data.
   ```

## How it works

When `testimize.enabled` is `true`, SPECTRA runs the Testimize engine
**in-process** during test generation — no child process, no MCP server, no
JSON-RPC. The flow integrates into the existing generation pipeline:

1. **Behavior analysis** (AI step, unchanged) — the AI analyzes your
   documentation and identifies testable behaviors. When Testimize is enabled,
   the prompt also asks the AI to emit a `field_specs[]` array listing every
   constrained input field it finds (name, type, min/max, required, allowed
   values, expected error messages).

2. **Field spec extraction** — SPECTRA parses the AI's `field_specs` from the
   response JSON. If the AI returned none (e.g., the documentation describes a
   button-tap UI with no text inputs), a local regex fallback
   (`FieldSpecAnalysisTools.Analyze`) scans the raw documentation for patterns
   like "3 to 20 characters" or "between 18 and 100".

3. **TestimizeRunner** — maps each field spec to a Testimize parameter type
   (`IntegerDataParameter`, `EmailDataParameter`, `DateDataParameter`, etc.)
   and calls `TestimizeEngine.Configure(...).Generate()` in-process:
   - **Single field**: bypasses generators entirely and reads the pre-computed
     BVA boundary values + invalid equivalence classes directly from the
     parameter's `TestValues` (populated at construction time by the field's
     strategy). Produces e.g., 4 boundary values + security-focused invalids.
   - **Multiple fields (2+)**: runs the full Pairwise or Hybrid-ABC generator
     for optimized cross-field combinations.

4. **Prompt embedding** — the pre-computed test data is rendered as a literal
   YAML block in the test-generation prompt, attributed to Testimize:

   ```
   ## PRE-COMPUTED ALGORITHMIC TEST DATA (from Testimize, strategy=HybridArtificialBeeColony)
   ```

   The AI uses these exact values verbatim in the generated test cases. It does
   not need to "decide" to call a tool — the data is already in the prompt as
   authoritative facts.

5. **Generation + critic** — proceed normally. Test steps reference the exact
   boundary values and expected error messages from the Testimize data.

### What Testimize computes vs what the AI handles

| Testimize (algorithmic, deterministic) | AI (contextual, from docs) |
|---------------------------------------|---------------------------|
| Precise BVA: exact min-1, min, max, max+1 | Valid equivalence partition values (context-aware) |
| Security invalids: XSS, SQLi, SSTI patterns | Domain-specific invalid scenarios |
| Multi-field pairwise/ABC combinations | Normal EP from documentation context |
| Type-confusion invalids (e.g., "abc" for integer) | Workflow / state-transition test data |

## Configuration reference

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `enabled` | bool | `false` | Master switch. |
| `mode` | string | `"exploratory"` | Maps to Testimize's `TestCaseCategory`: `"exploratory"` or unknown → `All` (valid + invalid + boundary), `"valid"` → `Valid` only, `"validation"` → `Validation` (invalid only). |
| `strategy` | string | `"HybridArtificialBeeColony"` | Algorithm for multi-field generation. One of: `Pairwise`, `OptimizedPairwise`, `Combinatorial`, `OptimizedCombinatorial`, `HybridArtificialBeeColony`. Single-field suites bypass this (direct BVA/EP extraction). |
| `settings_file` | string? | `null` | Optional path to a `testimizeSettings.json` with ABC tuning and per-type equivalence classes. When null, uses SPECTRA's bundled defaults. |

### `abc_settings` (optional tuning)

When null, Testimize uses sensible defaults. Override individual properties to
tune the Hybrid-ABC algorithm:

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| `abc_settings.total_population_generations` | int | `100` | Number of ABC algorithm iterations. Higher = more thorough but slower. |
| `abc_settings.mutation_rate` | double | `0.6` | Probability of mutating a test value during each generation (0.0–1.0). |
| `abc_settings.final_population_selection_ratio` | double | `0.5` | Fraction of final population to keep (0.0–1.0). |
| `abc_settings.elite_selection_ratio` | double | `0.3` | Fraction of best solutions preserved without modification. |
| `abc_settings.allow_multiple_invalid_inputs` | bool | `false` | When true, a single test case may contain invalid values for multiple fields simultaneously. |
| `abc_settings.seed` | int? | `null` | Fix the random seed for reproducible runs. When null, uses the bundled default (12345). |

Example with tuning:

```json
{
  "testimize": {
    "enabled": true,
    "strategy": "HybridArtificialBeeColony",
    "abc_settings": {
      "total_population_generations": 50,
      "mutation_rate": 0.4,
      "seed": 42
    }
  }
}
```

## Without Testimize

SPECTRA does not require Testimize. When disabled (the default), the AI
generation pipeline uses spec 037's ISTQB technique-driven prompts and
approximates boundary values from documentation. You get systematic boundary
coverage either way — Testimize makes the boundary values mathematically
precise (exact min-1/max+1) and adds security-focused invalid patterns the
AI might not consistently include.

## Graceful degradation

If `testimize.enabled` is `true` but the engine produces no usable data
(no field specs found, engine error, etc.), SPECTRA logs a diagnostic line
and proceeds with normal AI-only generation. The run always completes
successfully — Testimize failures never block test generation.

Skip reasons logged to `.spectra-debug.log`:

| Log line | Meaning |
|----------|---------|
| `TESTIMIZE SKIP reason=disabled` | `testimize.enabled` is `false` in config |
| `TESTIMIZE SKIP reason=no_field_specs suite=X` | Neither AI nor regex fallback found any constrained fields |
| `TESTIMIZE SKIP reason=insufficient_fields fields=1 suite=X` | Only 1 field found but it's a type that needs generators (rare — most single fields use direct BVA extraction) |
| `TESTIMIZE FALLBACK source=regex fields=N` | AI returned no field specs; regex extractor recovered N fields from raw docs |
| `TESTIMIZE ERROR exception=Type message="..."` | Engine threw; generation continues without Testimize data |
| `TESTIMIZE OK strategy=X fields=N test_data_sets=M elapsed=Xs` | Success — N fields produced M test data rows |

## Health check

```bash
spectra testimize check
spectra testimize check --output-format json
```

JSON output contains the fields `enabled`, `installed`, `healthy`, `mode`,
`strategy`, and `version`. Since v1.48.3, `installed` is always `true`
(the library is a compile-time dependency) and `version` shows the bundled
Testimize assembly version (e.g., `"1.1.10.0"`).
