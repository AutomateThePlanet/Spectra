# Contract: `GenerateHandler` no-match note + `LoadCriteriaContextAsync` refactor

**File**: `src/Spectra.CLI/Commands/Generate/GenerateHandler.cs`

## Part 1 — `LoadCriteriaContextAsync` return-type refactor

### Existing signature

```csharp
private static async Task<string?> LoadCriteriaContextAsync(
    string basePath,
    string suiteName,
    SpectraConfig config,
    CancellationToken ct)
```

### New signature

```csharp
private static async Task<CriteriaContextResult> LoadCriteriaContextAsync(
    string basePath,
    string suiteName,
    SpectraConfig config,
    CancellationToken ct);

internal sealed record CriteriaContextResult(
    string? Context,
    int SuiteMatchedCount,
    int TotalCriteriaCount);
```

### Field semantics (cross-reference)

See `data-model.md` for full field semantics. Briefly:

- `Context` — formatted markdown context for the prompt; today's `string?` return.
- `SuiteMatchedCount` — the count *before* the last-resort all-criteria fallback at `:2485-2486`. Strictly zero when no criterion matched the suite by component, source-doc, or file-name.
- `TotalCriteriaCount` — total criteria loaded across all `.criteria.yaml` files (informational).

### Existing logic preservation

The match-and-format logic at `:2452-2502` is unchanged in shape. The refactor adds:

1. Capture the suite-matched count immediately after the existing `.Where(...)` filter at `:2452-2462` (BEFORE the file-name fallback at `:2467` and BEFORE the last-resort at `:2485`).
2. After the file-name fallback, IF it produced matches, those are also suite-matched (the file name matched the suite name explicitly) — add their count to `SuiteMatchedCount`.
3. The last-resort fallback at `:2485-2486` does NOT contribute to `SuiteMatchedCount`. If `SuiteMatchedCount == 0` after steps 1-2, it stays zero; the last-resort fallback still happens for `Context` so the prompt has *something* to work with.
4. `TotalCriteriaCount = allCriteria.Count` from `:2448`.

### Call sites that change

Two callers update their `await`:

1. `ExecuteDirectModeAsync` `:672` — `var criteriaContext = await LoadCriteriaContextAsync(...);` → split into `var criteriaResult = await LoadCriteriaContextAsync(...); var criteriaContext = criteriaResult.Context;`. The existing prompt-injection path uses `criteriaContext` as before; the note logic later in the method reads `criteriaResult.SuiteMatchedCount`.
2. `ExecuteFromDescriptionAsync` `:1793` — same pattern.

---

## Part 2 — No-match note attachment

### When the note is added (D5)

| Result-build site | File/lines | Note added? |
|---|---|---|
| `ExecuteDirectModeAsync` final completion | `:985-1013` | **Yes**, when `criteriaResult.SuiteMatchedCount == 0` |
| `ExecuteFromDescriptionAsync` JSON write | `:1852-1867` | **Yes**, when `criteriaResult.SuiteMatchedCount == 0` |
| `ExecuteDirectModeAsync` "all behaviors covered" early-exit | `:462-477` | No — no tests written; note adds noise |
| `ExecuteDirectModeAsync` "no gaps for focus" early-exit | `:609-624` | No — same rationale |
| `ExecuteDirectModeAsync` "no tests generated" branch | `:924-940` | No — same rationale |
| `ExecuteDirectModeAsync` `analyzeOnly` branch | `:530-587` | No — no tests written; not a generation result |
| `ExecuteInteractiveModeAsync` | entire method | No — interactive flow is out of scope |
| `ExecuteFromSuggestionsAsync` | `:1672-1739` | No — delegates to `ExecuteDirectModeAsync`, which handles it |

### Behavioural contract

| Condition | `notes` value in JSON | Console echo |
|---|---|---|
| `SuiteMatchedCount == 0` AND result is built on a covered site (table above) | Single-element list with the note string | Echoed at `_verbosity >= Normal`; suppressed at `Quiet` |
| `SuiteMatchedCount > 0` on any site | Key absent from JSON (`WhenWritingNull`) | No console line |
| Any covered site, regardless of console verbosity | `notes` is present in JSON if condition holds; otherwise absent | n/a |

Key invariants:

1. The note never prevents writes, never changes exit code, never prompts.
2. The note is present in the JSON regardless of `--verbosity quiet` (FR-010). Tests assert this via the JSON channel, not stdout.
3. The note's wording is fixed up to the `{suite}` interpolation; SKILLs render verbatim.

## Message format (D4)

```text
No acceptance criteria matched suite '{suite}'. Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. Run 'spectra ai analyze --extract-criteria' if criteria are expected.
```

`{suite}` is the resolved suite name (`suite` parameter on `ExecuteDirectModeAsync`, identical on `ExecuteFromDescriptionAsync`).

## JSON shape

```jsonc
{
  "command": "generate",
  "status": "completed",
  "suite": "checkout",
  "generation": { "tests_written": 5, "tests_generated": 5, "tests_rejected_by_critic": 0 },
  "files_created": [ "test-cases/checkout/TC-001.md", "..." ],
  "notes": [
    "No acceptance criteria matched suite 'checkout'. Generated tests have no criteria linkage; acceptance-criteria coverage will not include them. Run 'spectra ai analyze --extract-criteria' if criteria are expected."
  ]
}
```

When `notes` is absent, the `notes` key does not appear in the JSON (NOT `null`, NOT `[]`).

## Test contract

| Test | Setup | Asserts |
|---|---|---|
| `Generate_Batch_NoMatchedCriteria_AddsNote` | Direct-mode generation with a stub `_criteria_index.yaml` whose criteria do not match the target suite by component, source-doc, or file-name; tests successfully generated by stub agent | `result.Notes` is a single-element list; the message contains the suite name and the recovery command |
| `Generate_FromDescription_NoMatchedCriteria_AddsNote` | From-description generation with the same criteria mismatch | Same shape; from-description path JSON contains `notes` |
| `Generate_WithMatchedCriteria_NoNote` | Direct-mode generation where at least one criterion matches the suite component | `result.Notes` is null; `notes` key absent from JSON |
| `Generate_Note_PresentInJson_EvenWhenQuiet` | Direct-mode generation with no-match criteria; `_verbosity = VerbosityLevel.Quiet` | `result.Notes` is the one-element list; the JSON output (not the suppressed stdout) carries it |

Tests use the existing direct-mode test harness (mock agent, fixture criteria files); no real AI call. The `LoadCriteriaContextAsync` refactor is exercised end-to-end via the fixture-driven tests; a dedicated lower-level test for the refactor is not necessary (its outputs are observable through the handler-level assertions).
