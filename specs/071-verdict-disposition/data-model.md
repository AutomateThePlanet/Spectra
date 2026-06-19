# Data Model: Verdict Disposition Policy (Spec 071)

**Date**: 2026-06-19

---

## 1. Enhanced `GroundingMetadata` record

**File**: `src/Spectra.Core/Models/Grounding/GroundingMetadata.cs`

Existing fields preserved unchanged. New fields added:

```csharp
public sealed record GroundingMetadata
{
    // --- EXISTING (unchanged) ---
    public required VerificationVerdict Verdict { get; init; }
    public required double Score { get; init; }
    public required string Generator { get; init; }
    public required string Critic { get; init; }
    public required DateTimeOffset VerifiedAt { get; init; }
    public IReadOnlyList<string> UnverifiedClaims { get; init; } = [];

    // --- NEW (Spec 071) ---
    /// <summary>True when verdict is partial and the test awaits human review.</summary>
    public bool FlaggedForReview { get; init; }

    /// <summary>Number of automatic repair attempts applied (0 = no repair attempted).</summary>
    public int RepairAttempts { get; init; }

    /// <summary>True when a repair cycle upgraded the test from partial to grounded.</summary>
    public bool Repaired { get; init; }

    /// <summary>Condensed non-grounded findings for human scanning (element + one-line reason).
    /// Full findings remain in the per-test verdict JSON.</summary>
    public IReadOnlyList<CondensedFinding> CondensedFindings { get; init; } = [];
}
```

**Validation rule** (existing `IsValid()` extended):
- `FlaggedForReview = true` is only valid when `Verdict == Partial`
- `Repaired = true` is only valid when `Verdict == Grounded` (repair upgraded it)
- `RepairAttempts >= 0` always

---

## 2. New `CondensedFinding` record

**File**: `src/Spectra.Core/Models/Grounding/CondensedFinding.cs` (new)

```csharp
/// <summary>
/// Condensed critic finding for frontmatter embedding — element + one-line reason only.
/// Full findings (claim, evidence, status) are in the per-test verdict JSON file.
/// </summary>
public sealed record CondensedFinding
{
    /// <summary>The test element this finding applies to (e.g., "Step 3", "Expected Result").</summary>
    public required string Element { get; init; }

    /// <summary>One-line reason the claim could not be grounded.</summary>
    public required string Reason { get; init; }
}
```

---

## 3. Enhanced `GroundingFrontmatter` YAML DTO

**File**: `src/Spectra.Core/Models/Grounding/GroundingFrontmatter.cs`

New YAML properties added (existing unchanged):

```csharp
[YamlMember(Alias = "flagged_for_review")]
public bool FlaggedForReview { get; set; }

[YamlMember(Alias = "repair_attempts")]
public int RepairAttempts { get; set; }

[YamlMember(Alias = "repaired")]
public bool Repaired { get; set; }

[YamlMember(Alias = "condensed_findings")]
public List<CondensedFindingFrontmatter> CondensedFindings { get; set; } = [];
```

Supporting DTO (new file `CondensedFindingFrontmatter.cs` in same namespace):

```csharp
public sealed class CondensedFindingFrontmatter
{
    [YamlMember(Alias = "element")]
    public string? Element { get; set; }

    [YamlMember(Alias = "reason")]
    public string? Reason { get; set; }
}
```

`ToMetadata()` extended: map `CondensedFindings` → `IReadOnlyList<CondensedFinding>`, `FlaggedForReview`, `RepairAttempts`, `Repaired`.

---

## 4. `TestFileWriter` grounding block format (updated)

**File**: `src/Spectra.CLI/IO/TestFileWriter.cs` (lines 110–128 updated)

Full format for a **grounded** test (minimal block):
```yaml
grounding:
  verdict: grounded
  score: 0.95
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:00:00Z
```

Full format for a **partial** test (flagged, 1 repair attempt):
```yaml
grounding:
  verdict: partial
  score: 0.72
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:00:00Z
  flagged_for_review: true
  repair_attempts: 1
  condensed_findings:
    - element: "Step 3"
      reason: "Conversion factor not verbatim in documentation"
    - element: "Expected Result"
      reason: "Error message text not found in docs"
```

Full format for a **repaired** test (upgraded from partial to grounded):
```yaml
grounding:
  verdict: grounded
  score: 0.93
  generator: claude-sonnet-4-6
  critic: claude-sonnet-4-6
  verified_at: 2026-06-19T10:05:00Z
  repaired: true
  repair_attempts: 1
```

`TestFileWriter` writes new fields conditionally (like existing `unverified_claims`):
- `repaired: true` only when `Repaired = true`
- `flagged_for_review: true` only when `FlaggedForReview = true`
- `repair_attempts: N` only when `RepairAttempts > 0`
- `condensed_findings:` list only when `CondensedFindings.Count > 0`

---

## 5. Per-test verdict JSON (`critic-verdict-{id}.json`)

**Location**: `.spectra/verdicts/critic-verdict-{id}.json` (new per-test naming; previously fixed `critic-verdict.json`)

**Written by**: `spectra-critic.agent.md` (via Write tool in-session)

**Format** (existing, unchanged — only the filename changes):
```json
{
  "verdict": "grounded" | "partial" | "hallucinated",
  "score": 0.0,
  "critic_model": "claude-sonnet-4-6",
  "findings": [
    {
      "element": "Step 1",
      "claim": "the specific claim being checked",
      "status": "grounded" | "unverified" | "hallucinated",
      "evidence": "verbatim quote from docs (if grounded)" | null,
      "reason": "why unverified or hallucinated (if not grounded)" | null
    }
  ]
}
```

**Lifecycle**: Per-test files persist for the duration of the session (not gitignored at the file level, but `.spectra/verdicts/` is gitignored). Repair and review read from these files.

---

## 6. Drop trail (`dropped-tests.json`)

**Location**: `.spectra/dropped-tests.json`
**Written by**: `spectra ai record-drop` command (append; creates file on first run)
**Gitignore**: Yes — scratch file like all `.spectra/` JSON

**Format** (append-only NDJSON — one JSON object per line):
```json
{"id":"TC-138","suite":"file-management","title":"Verify 1 KB file size display","drop_reason":"hallucinated","contradicting_claim":"1 KB = 1000 bytes","doc_ref":"docs/file-management/sizes.md","critic_model":"claude-sonnet-4-6","timestamp":"2026-06-19T10:02:00Z","source":"critic"}
{"id":"TC-114","suite":"file-management","title":"Verify empty folder behavior","drop_reason":"user_decided","contradicting_claim":null,"doc_ref":null,"critic_model":null,"timestamp":"2026-06-19T11:00:00Z","source":"review"}
```

**Fields**:
- `id`: test ID
- `suite`: suite name
- `title`: test title (from `_index.json` or test frontmatter)
- `drop_reason`: `"hallucinated"` (critic drop) | `"user_decided"` (review delete)
- `contradicting_claim`: the specific hallucinated claim (from verdict `findings[].claim` where `status=hallucinated`); null for user-decided
- `doc_ref`: the source doc the claim contradicts (from verdict `findings[].evidence` context or `source_refs`); null for user-decided
- `critic_model`: the critic model that issued the verdict; null for user-decided
- `timestamp`: ISO 8601 UTC
- `source`: `"critic"` | `"review"`

**NDJSON rationale**: append-only, no read-parse-rewrite needed; each drop is one atomic append (no concurrent write conflicts in sequential generation flow); trivially `grep`-able.

---

## 7. New C# types inventory

| Type | File | Description |
|------|------|-------------|
| `CondensedFinding` | `Spectra.Core/Models/Grounding/CondensedFinding.cs` | Record: element + reason |
| `CondensedFindingFrontmatter` | `Spectra.Core/Models/Grounding/CondensedFindingFrontmatter.cs` | YAML DTO |
| `IngestGroundingCommand` | `Spectra.CLI/Commands/Generate/IngestGroundingCommand.cs` | `spectra ai ingest-grounding` |
| `RecordDropCommand` | `Spectra.CLI/Commands/Generate/RecordDropCommand.cs` | `spectra ai record-drop` |
| `CompileRepairPromptCommand` | `Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs` | `spectra ai compile-repair-prompt` |
| `RepairPromptCompiler` | `Spectra.CLI/Verification/RepairPromptCompiler.cs` | Deterministic repair prompt builder |
| `ReviewFlaggedCommand` | `Spectra.CLI/Commands/Review/ReviewFlaggedCommand.cs` | `spectra ai review-flagged` |
| `ReviewFlaggedHandler` | `Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs` | Accept/delete logic |
| `DroppedTestsTrail` | `Spectra.CLI/IO/DroppedTestsTrail.cs` | Append-only NDJSON writer |
| `GroundingWriteBackService` | `Spectra.CLI/IO/GroundingWriteBackService.cs` | Loads test → sets Grounding → writes via TestFileWriter |

---

## 8. Consistency contract (FR7)

Across all three verdict outcomes:

| Outcome | `TC-NNN.md` | `_index.json` | Criteria backlinks | Coverage |
|---------|------------|---------------|-------------------|----------|
| Grounded | Present + grounding block | Entry present | Unchanged (none populated) | Counted |
| Partial (flagged) | Present + partial grounding block | Entry present | Unchanged | Counted |
| Hallucinated (dropped) | DELETED | Entry REMOVED | Unchanged (latent: would need cleanup if ever populated) | NOT counted |
| User-deleted (review) | DELETED | Entry REMOVED | Unchanged | NOT counted |
| Accepted (partial→acknowledged) | Present + partial block, `flagged_for_review` cleared | Entry present | Unchanged | Counted |

**Latent gap** (FR7 note): If `linked_test_ids` are ever automatically populated in criteria files, the delete path (`DeleteHandler`) MUST be extended to clean them. This is out of scope for Spec 071 but the contract requires it to be documented and enforced when that feature ships.
