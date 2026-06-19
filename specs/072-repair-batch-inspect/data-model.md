# Data Model: Repair-Orchestration Hardening & Inspection Surface

**Date**: 2026-06-19

---

## New types

### `AuditGroundingResult` (result/ShowResult.cs → new file AuditGroundingResult.cs)

Typed JSON result for `spectra ai audit-grounding --output-format json`.

```csharp
// src/Spectra.CLI/Results/AuditGroundingResult.cs
public sealed class AuditGroundingResult : CommandResult
{
    [JsonPropertyName("suite")]
    public required string Suite { get; init; }

    [JsonPropertyName("tests")]
    public required IReadOnlyList<AuditGroundingEntry> Tests { get; init; }

    [JsonPropertyName("summary")]
    public required AuditGroundingSummary Summary { get; init; }
}

public sealed class AuditGroundingEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }           // "grounded" | "partial" | "hallucinated"

    [JsonPropertyName("score")]
    public required double Score { get; init; }

    [JsonPropertyName("grounding_written")]
    public required bool GroundingWritten { get; init; }    // grounding: block present in .md

    [JsonPropertyName("flagged_for_review")]
    public required bool FlaggedForReview { get; init; }

    [JsonPropertyName("action_needed")]
    public required string ActionNeeded { get; init; }      // "repair" | "review" | "none"

    [JsonPropertyName("file")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? File { get; init; }                      // relative path to .md (null if not found)
}

public sealed class AuditGroundingSummary
{
    [JsonPropertyName("total")]
    public required int Total { get; init; }

    [JsonPropertyName("grounding_written")]
    public required int GroundingWritten { get; init; }

    [JsonPropertyName("partial_pending_repair")]
    public required int PartialPendingRepair { get; init; }

    [JsonPropertyName("flagged_for_review")]
    public required int FlaggedForReview { get; init; }
}
```

---

### `RepairBatchEntry` (internal manifest shape — stdout JSON)

Not a C# result type — just the JSON structure emitted to stdout by `compile-repair-batch`. The harness saves it to a file; the agent reads it.

```json
[
  {
    "id": "TC-101",
    "suite": "unit-converter",
    "file": "test-cases/unit-converter/TC-101.md",
    "source_refs": ["docs/unit-converter.md"],
    "repair_prompt": "# SPECTRA Test Repair\n\n..."
  },
  ...
]
```

Fields:
- `id` — test ID
- `suite` — suite name (passed from `--suite`)
- `file` — working-dir-relative path to `.md` (for `ingest-update`)
- `source_refs` — resolved source refs from the test (informational)
- `repair_prompt` — full plain-text repair prompt (from `RepairPromptCompiler.Compile()`)

---

## Modified types

### `TestDetail` (in ShowResult.cs) — add `File` property

```csharp
// Add to existing TestDetail in src/Spectra.CLI/Results/ShowResult.cs:
[JsonPropertyName("file")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? File { get; init; }
```

Value: `Path.GetRelativePath(basePath, testPath)` — working-dir-relative path to the test `.md` file.

---

## State transitions (grounding lifecycle)

```
verdict file on disk (.spectra/verdicts/critic-verdict-TC-NNN.json)
           │
           ▼
    verdict = partial?
    ┌───────┴──────────┐
    │                  │
  grounded         partial
    │                  │
    ▼                  ▼
ingest-grounding   compile-repair-batch filters to these
writes block         │
to .md               ▼ (for each entry in manifest)
                  agent patches test
                     │
                     ▼
                  ingest-update
                     │
                     ▼
                  critic subagent (agent-driven, irreducible)
                     │
            ┌────────┴─────────┐
         grounded           still partial
            │                  │
            ▼                  ▼
      ingest-grounding    ingest-grounding
      --repaired           (flagged_for_review=true)
            │                  │
            ▼                  ▼
       grounding block    grounding block
       in .md             in .md (flagged)
       action_needed=none action_needed=review
```

`audit-grounding` reads the current position in this lifecycle from verdict files + `.md` frontmatter.
