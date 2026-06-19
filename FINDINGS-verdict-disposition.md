# FINDINGS — Verdict Gate Disposition

**Repo:** `C:\SourceCode\Spectra`
**Mode:** Read-only investigation
**Date:** 2026-06-19
**Branch:** claude-code-v2

Tags: CONFIRMED (file:line verified) / INFERRED (logic derivation, named source)

---

## Part 1 — Gate Wiring

### Q1.1 — Where does `drop` come from?

**CONFIRMED** — `src/Spectra.CLI/Verification/VerdictIngestResult.cs:52`

```csharp
public bool Drops => Result?.Verdict == VerificationVerdict.Hallucinated;
```

Rules:
- `drop:true` iff and only iff `verdict == "hallucinated"`.
- `Grounded`, `Partial`, `Manual`, and any damage path (empty / parse failure) all resolve to `Drops = false`.
- **Score is never part of the gate.** A partial with a score of 0.01 passes. A hallucinated with 0.99 drops. There is no score threshold.
- `VerdictIngestor.Classify` parses four verdict strings (`grounded`, `partial`, `hallucinated`, `manual`) — any unknown string is a `ParseFailure`, which is damage, not a soft coercion to partial. (`VerdictIngestor.cs:108-114`)
- The classifier also emits `verdict`, `score`, and `findings` as structured output on a success path. The skill reads `drop` from the JSON stdout. The findings text is available to the skill but currently has no consumer.

**CONFIRMED** — `src/Spectra.Core/Models/Grounding/VerificationVerdict.cs`

The enum's doc comment for `Hallucinated` reads: "Test is rejected and NOT written to disk." This comment is **stale** — it describes the old in-process path (pre-Spec-059). With the inverted seam, tests ARE written to disk at ingest-tests time before the critic runs. See Part 2.

### Q1.2 — Does `ingest-verdict` itself change anything on disk?

**CONFIRMED** — `src/Spectra.CLI/Commands/Generate/IngestVerdictCommand.cs` (entire file)

`IngestVerdictCommand` is a **pure classifier**. It:
1. Reads critic JSON from `--from <file>` or stdin.
2. Calls `VerdictIngestor.Classify()`.
3. Writes the gate decision JSON to **stdout** only (`{outcome, verdict, score, drop}`).
4. **Writes nothing to disk. No file I/O beyond reading the input.**

The class comment at line 17–18 says: "Unlike those, it **persists nothing** (the grounding write-back stays in the reused `CreateTestWithGrounding`)." The `CreateTestWithGrounding` reference is a **stale comment** — that function does not exist in the current codebase (removed with the in-process path in Spec 059). The grounding write-back is not happening anywhere in the current flow (see Q2.2 / Q3.1).

**The gate decision is acted on by the skill (`spectra-generate.md:196-197`), not by any C# command.** The skill reads `drop` from the `ingest-verdict` JSON stdout and conditionally calls `spectra delete {id}`.

---

## Part 2 — What "drop" Actually Does

### Q2.1 — When is the test written vs gated?

**CONFIRMED** — `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:175-197`

The sequence is unambiguous:

```
Step 7 (line 175): spectra ai ingest-tests {suite} --from .spectra/generated.json
  → Exit 0: "The tests are written and the index updated. Proceed to the MANDATORY critic step."

Step 8 (line 193+): For EACH id in the ingest-tests ids list — invoke spectra-critic subagent.
```

Tests are **written to `test-cases/<suite>/TC-NNN.md` AND registered in `_index.json` at ingest-tests time (Step 7), BEFORE the critic runs on any of them (Step 8).** A "drop" is therefore always a DELETE of an already-written file, never a "skip write." This sequence is by design — `IngestTestsCommand` handles schema validation; the critic handles grounding. They are separate boundary checks.

**CONFIRMED** — `src/Spectra.CLI/Commands/Generate/IngestTestsCommand.cs:94-116`

`GeneratedTestIngestor.IngestAsync` → `TestPersistenceService.PersistAsync` → writes `.md` files + regenerates `_index.json` in one atomic pass. All of this happens at ingest-tests time.

### Q2.2 — What does a `drop:true` verdict cause for that test's `.md` file?

**CONFIRMED** — `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:197`

```
gate `drop` (verdict `hallucinated`) → remove it:
  `spectra delete {id} --force --no-interaction --output-format json --verbosity quiet`
```

**CONFIRMED** — `src/Spectra.CLI/Commands/Delete/DeleteHandler.cs:205`

```csharp
File.Delete(item.File!);
```

`DeleteHandler` phase 3c (line 199–220) calls `File.Delete` on the resolved file path. **The `TC-NNN.md` file IS deleted from `test-cases/<suite>/` when dropped.**

"Excluding TC-138" in the Step 9 report means TC-138.md was deleted. There is no code path that excludes a test from the kept-count while leaving it on disk.

### Q2.3 — What does drop do to `_index.json`?

**CONFIRMED** — `src/Spectra.CLI/Commands/Delete/DeleteHandler.cs:173, 405-432`

`DeleteHandler` executes in this order (phase 3):
1. **3a**: `UpdateSuiteIndexAsync` — reads `test-cases/<suite>/_index.json`, filters out the deleted ID(s), rewrites the index. This happens **before** the file is deleted.
2. **3b**: `StripDependsOnAsync` — rewrites `depends_on` in any test that referenced the deleted test.
3. **3c**: `File.Delete` — deletes the `.md` file.

`UpdateSuiteIndexAsync` (lines 405-432):
```csharp
var remaining = index.Tests.Where(t => t.Id is null || !removedIds.Contains(t.Id)).ToList();
// ... rewrites the index with only `remaining`
```

**`_index.json` IS updated atomically on drop. No dangling index reference to a deleted test.**

The consistency contract between `.md` file, `_index.json`, and `depends_on` references is maintained by `DeleteHandler` in a single call. Coverage reads from disk files (not the index), so the file deletion alone would prevent coverage inflation — but the index is cleaned too.

### Q2.4 — What does drop do to criteria backlinks?

**CONFIRMED** — `src/Spectra.Core/Models/Coverage/AcceptanceCriterion.cs:40-41`

```csharp
[YamlMember(Alias = "linked_test_ids")]
public List<string> LinkedTestIds { get; set; } = [];
```

**CONFIRMED** — `src/Spectra.CLI/Commands/Analyze/AnalyzeHandler.cs:758-760` (read-only usage only)

`LinkedTestIds` is declared in the model and read during coverage analysis. It is **never written by the generate flow or the delete flow.** No code path in `DeleteHandler`, `TestPersistenceService`, `GeneratedTestIngestor`, or `IngestVerdictCommand` modifies criteria backlinks.

**CONFIRMED by absence:** Grep for `LinkedTestIds` across `src/` returns two read-only hits (`AcceptanceCriterion.cs:41` declaration; `AnalyzeHandler.cs:760` read during coverage) and one preserve-from-original in `CriteriaMerger.cs:51`. Zero write calls.

**Current state:** Backlinks are not automatically populated by the generate flow. They would only be present if manually maintained. Drop does not clean them because there are no backlinks to clean. **This is consistent — if backlinks were populated in the future, drop would NOT clean them, which would be a dangling reference.**

### Q2.5 — What does coverage/dashboard show for a dropped test?

**CONFIRMED** — `src/Spectra.CLI/Agent/Tools/BatchReadTestsTool.cs:51-88`

Coverage analysis (`spectra ai analyze --coverage`) reads test files by scanning `test-cases/{suite}/*.md` on disk:

```csharp
var testFiles = Directory.GetFiles(suitePath, "*.md")
    .Where(f => !Path.GetFileName(f).StartsWith("_"))
    .ToList();
```

No filter by status, no filter by verdict. **All files present on disk are counted.**

Because dropped (hallucinated) tests are physically deleted, they are **not present on disk** when coverage runs, so they are **not counted.** Coverage cannot inflate from hallucinated tests under the current auto-delete model.

Partial and grounded tests ARE counted. Since grounding metadata is not written back to test files (Q3.1), coverage has no way to distinguish partial from grounded — both look identical on disk. Coverage accuracy is not affected by verdict (partial tests are kept because they have real behaviors; the partial status means some claims are unverified but the test case itself tests something real).

### Q2.6 — Is there any existing "rejected"/"quarantine" concept?

**CONFIRMED** — `src/Spectra.Core/Models/TestCase.cs:103-116`

```csharp
/// <summary>Test status (e.g., "orphaned" when documentation is removed).</summary>
public string? Status { get; init; }

public string? OrphanedReason { get; init; }
public DateTimeOffset? OrphanedDate { get; init; }
```

The `Status` field exists but the only documented value is `"orphaned"` (set by the update flow when docs change). There is **no `"rejected"`, `"flagged"`, `"hallucinated"`, `"partial"`, or `"quarantined"` value.** The schema is strictly binary: a test is either present and valid, or deleted. A "keep-and-flag" model would need a new status value plus write-back infrastructure.

---

## Part 3 — What "Partial" Does

### Q3.1 — Is a partial verdict recorded anywhere on the test?

**CONFIRMED by absence** across multiple paths:

- `IngestVerdictCommand` persists nothing (Part 1, Q1.2).
- The skill gate for partial is "keep the test" with no follow-up write (`spectra-generate.md:196`).
- `TestFileWriter.cs:110-123` has grounding write-back code:
  ```csharp
  if (testCase.Grounding is not null)
  {
      sb.AppendLine("grounding:");
      sb.AppendLine($"  verdict: {testCase.Grounding.Verdict.ToString().ToLowerInvariant()}");
      sb.AppendLine($"  score: {testCase.Grounding.Score:F2}");
      // ...unverified_claims for partial...
  }
  ```
  This code exists and is syntactically correct. But it only fires when `testCase.Grounding is not null`.

- **`GroundingMetadata` is never constructed in the generate flow.** Grep for `new GroundingMetadata` returns only two hits, both in `GroundingFrontmatter.cs` — the YAML deserializer that reads grounding FROM existing frontmatter. There is no code path in `GeneratedTestIngestor`, `IngestTestsCommand`, `IngestVerdictCommand`, or the generate skill that constructs a `GroundingMetadata` and attaches it to a new test.

- `spectra-critic.agent.md:105` states: "You do not write or modify test files (the **grounding write-back is the CLI's job**)." The CLI's job is never actually performed. This comment is stale — it was written when the grounding write-back was planned as a next step after the critic boundary was established. The write-back was never implemented in the seam-inverted flow.

- The `VerificationVerdict.Partial` enum doc comment ("Test is written with warning marker and unverified_claims list") is also stale.

**A partial verdict leaves NO trace on the test artifact.** The test file is kept unchanged from the moment it was ingested at Step 7. A human reviewing TC-113 tomorrow has no on-artifact signal that it was partial — the grounding frontmatter block is absent.

### Q3.2 — Are the per-test verdict files the only record, and are they durable?

**CONFIRMED** — `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:88-93`

The critic writes to `.spectra/verdicts/critic-verdict.json`. This is a **single fixed filename**, overwritten for each test in the sequential critic loop. For a batch of N tests, only the Nth test's verdict file survives on disk.

**CONFIRMED by absence** — `FINDINGS-verdict-seam-investigation.md:84`

> "No code path deletes or rotates verdict files. There is no `spectra clean` command, no run-start cleanup, no run-end cleanup."

The verdict file is **scratch, not durable.** After a run:
- The last test's verdict survives in `.spectra/verdicts/critic-verdict.json`.
- All earlier tests' verdicts (including TC-113/117/127/135 from the live run) are gone.
- The file is not gitignored (confirmed in `FINDINGS-verdict-seam-investigation.md:98`).

**Verdict durability summary:** There is no audit trail. Once the generate run completes, there is no machine-readable record of:
- Which tests were dropped as hallucinated (and why — what the critic's findings were).
- Which tests are partial (and what the unverified claims are).
- The score, critic model, or `verified_at` timestamp for any kept test.

The only human-visible record is the Step 9 text printed to the terminal by the skill.

---

## Part 4 — Design Space

### Consistency baseline

Before mapping models: the four stores are `test-cases/<suite>/*.md`, `_index.json`, criteria backlinks (`.criteria.yaml` / `_criteria_index.yaml`), and coverage/dashboard.

Current consistency contract:
- **`test-cases/*.md` ↔ `_index.json`**: maintained by `DeleteHandler` (drop cleans both) and `TestPersistenceService` (ingest writes both). **In sync.**
- **`test-cases/*.md` ↔ criteria backlinks**: `linked_test_ids` is never populated automatically. **No sync needed currently — and no sync performed.** If backlinks were ever populated (e.g. by a future `spectra ai auto-link` command), drop would leave dangling backlinks. This is a latent gap.
- **`test-cases/*.md` ↔ coverage**: coverage reads from disk. Deletion = invisible to coverage. **In sync.**
- **`_index.json` ↔ coverage**: coverage does not read `_index.json` — it scans disk directly. They stay consistent as long as the file system is the source of truth.

The user's instinct ("mechanical delete leaves them in indexes") does NOT apply to the current auto-delete path — `DeleteHandler` keeps the file and index consistent. The risk would only appear if a delete path that DIDN'T call `DeleteHandler` (e.g., manual `File.Delete` or a new code path) were used.

---

### Model A — Auto-delete hallucinated (current behavior)

**What happens today:**
1. Test `.md` is deleted via `DeleteHandler` (confirmed).
2. `_index.json` entry is removed atomically in the same call (confirmed).
3. `depends_on` references in other tests are stripped (confirmed).
4. Criteria backlinks: not touched (but none are populated, so no gap currently).
5. Coverage: deleted file is absent from disk → not counted. Clean.

**What's missing:**
- No audit trail. TC-138's deletion leaves no on-disk record that the test existed, was hallucinated, or what the finding was ("1 KB = 1000 bytes" contradicts "1024 bytes").
- The only evidence is the Step 9 terminal output, which is ephemeral unless the user copies it.
- Future implication: if backlinks are ever auto-populated, drop would need to call a backlink-cleanup step (not currently required).

**Cost of change:** Near zero — this is already implemented. Adding an audit trail (e.g., a `.spectra/dropped-tests.json` that accumulates entries) would be a small addition to `DeleteHandler` or the skill.

---

### Model B — Keep-and-flag

**What would change:**
1. New `status` value needed (e.g., `"hallucinated"` or `"rejected"`) in `TestCase.Status`. `TestFileWriter` would write it to frontmatter.
2. A grounding write-back step is needed in the generate flow: after the critic verdict, write verdict+score+findings back to the test's `.md` frontmatter (via `TestFileWriter` with a non-null `Grounding`). The infrastructure exists (`TestFileWriter.cs:110-123`, `GroundingMetadata`) but no code path currently calls it for new tests.
3. `BatchReadTestsTool` / `AnalyzeHandler` would need to skip tests with `status: rejected` (or equivalent) to prevent coverage inflation from flagged tests. Currently there is no status filter anywhere in the coverage path.
4. `_index.json` would retain the entry. The index would need a matching flag (`rejected: true`) or coverage would need to re-read status from the `.md` to decide whether to count the test.
5. A human action path is needed to review and resolve flagged tests (approve → remove the flag; reject → call `spectra delete`).

**Audit trail benefit:** The test file itself becomes the record — verdict, score, findings, and `unverified_claims` written to frontmatter. A human reviewer tomorrow can read TC-138.md and see exactly what the critic found.

**Cost:** A spec-sized change. Several new touch points:
- `TestCase` model: new `status` value.
- `TestFileWriter`: write `grounding:` block for new tests (the code already exists, just needs to be wired).
- Generate flow: new step between Step 8 (critic) and Step 9 (report) to write back grounding.
- Coverage / `BatchReadTestsTool`: status filter.
- `_index.json` schema: optional `rejected` flag.
- CLI: a review/resolution command (`spectra review`, `spectra approve <id>`, or similar).

---

### Model C — Repair loop

**Core split:** Repair means two different things for the two verdict categories.

**Partial → targeted patch:**
The critic produces structured findings with `element`, `claim`, `status: "unverified"`, and `reason`. For TC-113/117/127/135 the user described the partial as "derivable-but-not-verbatim" — the finding would be something like "claim: 'converts at 1024 bytes'; status: unverified; reason: 'conversion factor not found verbatim in docs; docs say X on page Y'". This IS actionable: a repair prompt could inject (a) the test's current content, (b) the specific failing claims from `findings`, and (c) the relevant doc section, and ask the generator to correct just those claims. This is meaningfully different from full regeneration — it's a targeted rewrite of 1–3 claims rather than a from-scratch generation. The generator already has file access and could apply the patch in one turn.

However: verdict files for TC-113/117/127/135 are gone (scratch). Repair would only be actionable during the same run (before the verdict file is overwritten by the next test's critic), unless verdict findings are persisted to the test's frontmatter (requires the grounding write-back from Model B, at least for partials).

**Hallucinated → targeted correction:**
TC-138's finding ("1 KB = 1000 bytes" contradicts docs' "1024 bytes") is a specific factual error — a targeted correction, not a full regeneration. The repair prompt could say: "your test claims X; the docs say Y (line N); fix the test to match the docs." This IS distinct from drop-and-regenerate-in-next-batch, because: (1) it preserves the test's structure, priority, and other claims; (2) it targets only the hallucinated element; (3) it reuses the already-allocated test ID.

Whether repair makes sense for hallucinated tests depends on the severity. A test that invents an entire behavior not in the docs at all is better dropped and regenerated. A test that gets a specific value wrong (e.g., a byte-count or threshold) is better repaired. The critic's `findings[].status` field can distinguish: an `element` with `status: "hallucinated"` is a direct contradiction; `status: "unverified"` is a missing claim. Targeted repair is most defensible for a single hallucinated claim against an otherwise-sound test.

**Seam machinery needed:**
A repair loop would need:
1. `compile-repair-prompt` — new verb: takes suite + test ID + critic verdict findings (from the persisted verdict file or findings JSON); compiles a prompt that includes the test's current content, the failing findings, and the relevant doc sections. Mirrors `compile-critic-prompt` in structure.
2. Generator runs in-session to produce the repaired test (same `generated.json` scratch file, one-element array).
3. `ingest-tests {suite} --from .spectra/generated.json` — BUT replacing the existing test with the same ID, not adding a new one. This requires `TestPersistenceService` to handle the case where the ID already exists (currently it allocates a new ID; an update-in-place mode would be needed — or the flow could call `spectra delete {id}` then `ingest-tests`).
4. Re-critic the repaired test.
5. If still non-grounded: apply the user-configured disposition (auto-drop or ask-human).

This is a **brief-sized spec** — probably 3–5 days. The generate seam is the template; `compile-repair-prompt` is a new `CriticPromptCompiler`-style command; the in-place replace for `ingest-tests` is a small extension or can be avoided with delete+reingest. The skill loop is 10–15 new lines. The biggest uncertainty is whether the repair loop should persist the intermediate verdict finding (which requires Model B's grounding write-back).

**Partial-vs-hallucinated split in practice:**
Under Model C, the most defensible design is:
- **Partial**: always attempt one repair cycle (the finding is specific enough to act on; the test has real behaviors).
- **Hallucinated**: optionally attempt one repair cycle if the finding is a single-element contradiction; auto-drop if the hallucination is pervasive or the repair cycle still produces hallucinated. 
- Bounded: at most 2 repair attempts (matching the existing retry convention), then apply the terminal disposition (auto-drop or ask-human).

**Prerequisite:** Verdict findings must be available when the repair prompt is compiled. Currently they are only in the ephemeral `.spectra/verdicts/critic-verdict.json` (overwritten per test). A repair loop either (a) must run immediately after the critic for each test (during the same iteration, before the next test's critic overwrites the file), or (b) requires the findings to be persisted to the test's frontmatter (Model B's grounding write-back). Option (a) is architecturally simpler and requires no new persistence; it just changes the skill loop to: critic → if partial/hallucinated → compile-repair-prompt → repair → re-critic → gate.

---

### Cross-cutting consistency contract

For all three models, the stores that must stay consistent:

| Store | Drop (A) | Keep-and-flag (B) | Repair-pass (C) |
|-------|----------|-------------------|-----------------|
| `TC-NNN.md` | Deleted | Kept + `status: rejected` written back | Overwritten with repaired version |
| `_index.json` | Entry removed | Entry kept (or `rejected: true` added) | Entry kept (same ID, same suite) |
| Criteria backlinks | Not touched (none populated) | Not touched (same) | Not touched (same) |
| Coverage | File absent → not counted | File present + status filter needed | File present (repaired test is valid) |
| Audit trail | None | Frontmatter grounding block | Transient findings only (or frontmatter if B wired) |

**The user's instinct is correct as a general risk.** The current auto-delete path keeps the four stores consistent because `DeleteHandler` is a single coordinated write. The risk materializes if a "keep" decision is added without wiring coverage exclusion — a flagged (Model B) test sitting in `test-cases/` with no status filter would silently inflate coverage. That filter is the critical wiring point for Model B.

---

## Key Facts Summary

| Question | Answer | Tag |
|----------|--------|-----|
| `drop:true` gate formula | `verdict == "hallucinated"` only; score irrelevant | CONFIRMED |
| Low-score partial ever drops? | No — score is never in the gate | CONFIRMED |
| `ingest-verdict` writes to disk? | Nothing | CONFIRMED |
| Tests written before or after critic? | BEFORE (Step 7 ingest, Step 8 critic) | CONFIRMED |
| Drop deletes the `.md` file? | **YES** — `File.Delete` in `DeleteHandler.cs:205` | CONFIRMED |
| Drop removes `_index.json` entry? | **YES** — `UpdateSuiteIndexAsync` runs before file delete | CONFIRMED |
| Drop cleans criteria backlinks? | No — but none are populated automatically | CONFIRMED |
| Coverage counts dropped tests? | No — file is deleted, not on disk | CONFIRMED |
| Coverage counts partial tests? | **YES** — no verdict filter, all disk files counted | CONFIRMED |
| Partial verdict written to test frontmatter? | **NO** — `IngestVerdictCommand` persists nothing; grounding write-back is dead code in current flow | CONFIRMED |
| Grounding metadata ever set for new tests? | **NEVER** — `new GroundingMetadata` only in YAML deserializer | CONFIRMED |
| Verdict file durable? | **NO** — single scratch file, overwritten per test, not gitignored | CONFIRMED |
| Existing quarantine/flagged status? | Only `"orphaned"` — no `"rejected"` or `"partial"` value | CONFIRMED |
| `depends_on` cleaned on drop? | YES — `StripDependsOnAsync` in `DeleteHandler.cs:389` | CONFIRMED |

---

## Critical Stale Comments (identified, not fixed)

These comments describe the intended behavior of the old in-process path, not the current seam-inverted reality:

| File | Line | Stale claim |
|------|------|-------------|
| `VerificationVerdict.cs:22` | `Hallucinated` enum doc | "Test is rejected and NOT written to disk" — now written then deleted |
| `VerificationVerdict.cs:18` | `Partial` enum doc | "Test is written with warning marker and unverified_claims list" — no writeback occurs |
| `IngestVerdictCommand.cs:17-18` | Class doc | "grounding write-back stays in the reused `CreateTestWithGrounding`" — that function doesn't exist |
| `spectra-critic.agent.md:105` | Agent instructions | "the grounding write-back is the CLI's job" — the CLI never does it |
