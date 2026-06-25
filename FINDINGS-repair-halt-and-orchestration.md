# FINDINGS — Why the Repair Loop Stopped

**Date:** 2026-06-19
**Investigator:** Claude Code (read-only, no files modified)
**Framework repo:** `C:\SourceCode\Spectra` (branch: claude-code-v2, HEAD: 30a5247)
**Consumer artifacts:** `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST\.spectra\`
**Context:** `FINDINGS-repair-seam-and-inspection.md` established all 6 repair-loop CLI verbs exist and Step 8 prose is defined. This doc pins WHY the agent stopped mid-loop.

---

## Part 1 — Halt cause

### Q1.1 — ingest-update input contract

CONFIRMED (`src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs:32–33, 79–91`):

- `--from <path>` is **optional**. When omitted, `ingest-update` reads from **stdin**.
- There is **no fixed default path**. The CLI does not fall back to `.spectra/repaired.json` or any preset name.
- When `--from` is provided, the value is entirely caller-supplied. `repaired.json`, `repaired-TC-105.json`, or any other filename all work equally.

```csharp
// Line 33
var fromOption = new Option<string?>("--from",
    "File containing the model's edited-test JSON (omit to read stdin)");

// Lines 79–91
if (!string.IsNullOrWhiteSpace(from))
{
    if (!File.Exists(from)) { Console.Error.WriteLine(...); return ExitError; }
    content = await File.ReadAllTextAsync(from, ct);
}
else
{
    content = await Console.In.ReadToEndAsync(ct);
}
```

**Consequence:** The per-test filename the agent used (`repaired-TC-105.json`) is NOT the cause of a CLI failure — `ingest-update --from .spectra/repaired-TC-105.json` would succeed. The name mismatch is cosmetic from the CLI's perspective.

---

### Q1.2 — Step 8 prose name vs handler — verdict: **(a) agent deviated from correct prose**

**Prose** — CONFIRMED `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:213–216`:

```
→ Patch the test in-session ... Write patched test JSON array to `.spectra/repaired.json`.
  spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json --output-format json
```

The prose prescribes the **fixed name `.spectra/repaired.json`** on BOTH sides — Write instruction and `--from` argument.

**What the agent actually wrote** — CONFIRMED `.spectra/` listing with mtimes:

| File | Written at | Matches prose? |
|---|---|---|
| `repaired.json` | 16:45:15 | **Yes** — TC-101, first partial processed |
| `repaired-TC-106.json` | 16:47:51 | **No** — per-test name |
| `repaired-TC-105.json` | 17:53:31 | **No** — per-test name |
| `repaired-TC-110.json` | 17:53:32 | **No** — per-test name |
| `repaired-TC-111.json` | 17:53:33 | **No** — per-test name |

**Verdict: case (a)** — prose prescribes fixed `.spectra/repaired.json`; agent deviated to per-test naming after the first partial. Source of drift INFERRED: `spectra-critic.agent.md:88–98` explicitly uses per-test naming for verdict files (`.spectra/verdicts/critic-verdict-{id}.json`) with rationale "per-test naming ensures all verdict files survive the full batch." The agent applied that same convention to repair files, even though the generate skill prose prescribes a fixed overwrite-per-test pattern.

**Critical additional finding:** The per-test name does NOT cause CLI failure (Q1.1 confirmed). The name mismatch is cosmetic. The probe shifts to Q1.3 — did the agent fail at ingest-update, or stop before reaching it?

---

### Q1.3 — Artifact pattern + causation

**15 partial tests** — CONFIRMED verdict files: TC-101, TC-105, TC-106, TC-110, TC-111, TC-114, TC-119, TC-121–125, TC-127, TC-128, TC-132.

**5 repaired files, 10 partials untouched.**

**Timeline analysis** — CONFIRMED via mtime inspection of consumer `.spectra/` and `test-cases/unit-converter/`:

- **TC-101 (`repaired.json` → mtime 16:45:15):** `TC-101.md` mtime = 16:47:58. `ingest-update` ran and **succeeded** using the prose-prescribed fixed name. No grounding block in frontmatter — re-critic and `ingest-grounding` were NOT called.
- **TC-106 (`repaired-TC-106.json` → mtime 16:47:51):** `TC-106.md` and `_index.json` both mtime 17:51:25. `ingest-update` ran and **succeeded** over an hour later using a per-test name. No grounding block.
- **TC-105, TC-110, TC-111 (repair files → mtimes 17:53:31–33):** `TC-105.md`, `TC-110.md`, `TC-111.md` all still mtime 16:42:30 (original batch ingest). `ingest-update` was **never called** for these three.
- **TC-114, TC-119, TC-121–125, TC-127, TC-128, TC-132:** No repaired file. No `ingest-update`. Nothing beyond original ingest.
- **Zero partial tests have a grounding block** in `.md` frontmatter — CONFIRMED all 15 inspected.
- **Zero re-critic runs:** `.spectra/verdicts/` has exactly 35 files, all mtime 16:44–16:57 (original critic pass). No file was re-created.

**Causation: (A) — batch-write then stop, not ingest-failure.**

The agent completed `ingest-update` for 2 tests (TC-101, TC-106) and even in those cases did NOT proceed to re-critic or `ingest-grounding`. It then wrote repair files for 3 more without ingesting them. The remaining 10 partials received nothing. This pattern is consistent with **context exhaustion or session termination mid-loop**, not with CLI failures at `ingest-update`. INFERRED (no run log available): the 90–135 sequential operation requirement caused the session to terminate before completing even the 2-step ingest-grounding sub-loop for any test.

**The name mismatch is NOT the halt cause.** The halt cause is orchestration volume exhausting the session before the loop completed.

---

## Part 2 — Orchestration depth

### Q2.1 — Prose structure and step count

CONFIRMED `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:211–225`: The partial repair path is a **sequential per-test loop** — for each partial, do these steps in order. There is no "patch all then ingest all" batching.

**Discrete agent actions per partial test (happy path — re-verdict grounded):**

| # | Action | Tool |
|---|---|---|
| 1 | `compile-repair-prompt` | Bash |
| 2 | Write patched test to `.spectra/repaired.json` | Write |
| 3 | `ingest-update {suite} --test-id {id} --from .spectra/repaired.json` | Bash |
| 4 | Invoke `spectra-critic` subagent | Task |
| 4a | (critic internal) `compile-critic-prompt` | Bash inside Task |
| 4b | (critic internal) Write verdict JSON | Write inside Task |
| 4c | (critic internal) `ingest-verdict` | Bash inside Task |
| 5 | Read re-verdict gate | (Bash exit code) |
| 6 | `ingest-grounding --repaired --repair-attempts 1` | Bash |

**9 operations per test (6 outer + 3 inner inside critic subagent).**

**15 partials × 9 = 135 sequential operations minimum** for a complete batch. This is the root structural cause: the generation seam is 2 operations per test (compile → ingest); the repair seam is 9 per test. At 15 partials, repair asks for 67× more prose-orchestrated steps than generation does for the same batch.

---

### Q2.2 — Loop state / checkpointing

CONFIRMED `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:188–191`: The `progress.json` `loop` field (`{"active":7,"loop":{"current":i,"total":N}}`) is a UI display hint updated per-test, NOT a durable checkpoint for resume.

No "processed partials" set is maintained anywhere. CONFIRMED `AiCommand.cs`: no `audit-grounding` or equivalent exists to query what's been done vs pending.

**Natural checkpoint exists structurally** (INFERRED, structurally sound): the two-level state on disk is:
- Test with `grounding:` block in `.md` → fully processed (both `ingest-update` and `ingest-grounding` ran).
- Test with `repaired-TC-NNN.json` but no grounding block → `ingest-update` still needed (or `ingest-update` ran but re-critic didn't).
- Test without repair file or grounding block → `compile-repair-prompt` still needed.

This checkpoint is readable from disk today but requires raw shell to extract (`.spectra/verdicts/` + `.md` frontmatter scan). It is NOT currently readable by any CLI command — wire into `audit-grounding` to make it resumable without shell.

---

### Q2.3 — repair-partials feasibility: decisive constraint

CONFIRMED `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:1–7, 30–43`: The critic is a `context: fork` Claude Code subagent invoked via the `Task` tool. It performs a model inference turn in-session and uses the Write tool to persist the verdict JSON. It is **structurally impossible to run inside a C# CLI command** — doing so would require the CLI binary to spawn a Claude Code agent session, creating a circular dependency.

**A pure `repair-partials` CLI verb is impossible.** The critic subagent step structurally requires agent orchestration.

**Minimal reliable hybrid** that reduces 135 sequential operations to a viable count:

```
spectra ai compile-repair-batch --suite <s>     [1 Bash call — new CLI verb]
  → reads all .spectra/verdicts/critic-verdict-{id}.json for suite
  → filters to: verdict=partial AND .md has no grounding block (natural checkpoint)
  → for each pending: calls compile-repair-prompt deterministically
  → emits JSON manifest:
    { "pending_repairs": [{ "id": "TC-105", "repair_prompt": "...", "verdict_path": "..." }] }
```

Then the agent processes the manifest sequentially, per test:

| Action | Operations |
|---|---|
| Write `.spectra/repaired-{id}.json` from manifest prompt | 1 Write |
| `ingest-update {suite} --test-id {id} --from .spectra/repaired-{id}.json` | 1 Bash |
| Task: invoke spectra-critic for {id} | 1 Task (3 internal) |
| `ingest-grounding --suite {s} --test {id} --from ... --repaired --repair-attempts 1` | 1 Bash |

**Total: 1 batch CLI call + 15 × 5 = 76 operations** (vs. 135 today). The manifest is regenerated from checkpoint state on each invocation — resume after interruption costs exactly 1 CLI call (`compile-repair-batch` re-reads the disk state and emits only the still-pending tests).

A complementary `ingest-repair-batch` is NOT viable — the per-test branching re-verdict gate (grounded → ingest-grounding; partial → ingest-grounding flagged; hallucinated → record-drop + delete) requires per-test decisions the CLI cannot pre-batch without knowing the re-critic outcome.

---

## Part 3 — Inspection gaps confirmed

### Q3.1 — audit-grounding: confirmed gap

CONFIRMED `src/Spectra.CLI/Commands/Ai/AiCommand.cs:14–53`: 17 commands registered on `spectra ai`. None reads grounding state. `audit-grounding` does not exist.

CONFIRMED `.spectra/verdicts/` listing: 35 files, each containing a parseable `verdict`, `score`, and `findings` array.

**What `spectra ai audit-grounding --suite <s>` would read:**
- `.spectra/verdicts/critic-verdict-{id}.json` — verdict + score + findings per test
- `test-cases/{suite}/_index.json` — maps id → `file` path (`TestIndexEntry.File`)
- Each `.md` frontmatter — presence/absence of `grounding:` block and `flagged_for_review: true`

**What it would emit (per test):**
```json
{
  "id": "TC-105",
  "verdict": "partial",
  "score": 0.65,
  "grounding_written": false,
  "flagged_for_review": false,
  "repair_file_exists": true,
  "repair_file_path": ".spectra/repaired-TC-105.json",
  "action_needed": "ingest-update → re-critic → ingest-grounding"
}
```
Plus summary: `{ "pending_repair": N, "pending_grounding": N, "flagged": N, "clean": N }`.

This command also serves as the resume oracle for `compile-repair-batch` — the "which tests are still pending" filter.

---

### Q3.2 — show file field: one-line fix confirmed

CONFIRMED `src/Spectra.CLI/Results/ShowResult.cs:1–41`: `TestDetail` has no `File` property. Present properties: `Id`, `Title`, `Priority`, `Suite`, `Component`, `Tags`, `SourceRefs`, `Steps`, `ExpectedResults`.

CONFIRMED `src/Spectra.CLI/Commands/Show/ShowHandler.cs:77–84`: variable `testEntry` (type `TestIndexEntry`) is in scope and carries `testEntry.File` (e.g. `"TC-105.md"` relative to suite dir). It is used to build `testPath` (line ~80) but NOT passed into `DisplayTestAsync` or included in the JSON result.

**Fix:** Add `public string? File { get; init; }` to `TestDetail` in `ShowResult.cs`, then pass `File = testEntry.File` in the `TestDetail` initializer in `ShowHandler.cs`. Source property: `TestIndexEntry.File` — CONFIRMED `src/Spectra.Core/Models/TestIndexEntry.cs:14` (`[JsonPropertyName("file")]`).

This closes the `cat _index.json | python -c "...[id=='TC-112']"` improvisation — `spectra show TC-112 --output-format json` would then return the file path directly.

---

### Q3.3 — config --raw: confirmed existing command

CONFIRMED `src/Spectra.CLI/Commands/Config/ConfigHandler.cs:169–175`: `spectra config --raw` (alias `-r`) calls `ShowAllConfig(configJson, showRaw: true)`, which writes `configJson` verbatim to stdout via `Console.WriteLine(configJson)`. Raw JSON output. Fully equivalent to `cat spectra.config.json`.

This is an existing command the agent should use instead of raw file reads. No code change needed — only skill/doc awareness.

---

## Sizing

### The halt cause

The agent stopped mid-loop due to **session context exhaustion from 135 sequential orchestration operations**, not from a CLI failure or prose/CLI name mismatch. The name mismatch (per-test vs fixed filename) is cosmetic — `ingest-update --from` accepts any path. The 2 tests where `ingest-update` succeeded prove the CLI path works; the fact that even those 2 tests were never followed by re-critic or `ingest-grounding` confirms the loop simply ran out of session before completing any test end-to-end.

### Fix sizing

| Fix | Type | Size | Spec or brief |
|---|---|---|---|
| **Prose: adopt per-test name** `repaired-{id}.json` in Step 8 (reconcile agent behavior) | Skill-only, no C# | Trivial | Brief amendment to `spectra-generate.md` |
| **`spectra ai compile-repair-batch --suite <s>`** | New CLI command (~100 LOC) | Small | Same spec as `audit-grounding` |
| **`spectra ai audit-grounding --suite <s>`** | New CLI command (~150 LOC) | Small | |
| **`spectra show {id}` file field** | One-line `TestDetail` + minor `ShowHandler` | Trivial | Can fold into same spec |
| **`spectra config --raw` awareness** | Skill/doc note, no C# | Zero | |

**Repair reliability fix = brief prose amendment + one new spec ("repair-orchestration hardening").** The prose amendment stops the name-drift and is shippable immediately. The spec (`compile-repair-batch` + `audit-grounding` + `show` field) addresses orchestration depth — reducing 135 operations to 76 with built-in resume — and is the permanent structural fix. These are NOT the same spec: the prose amendment is immediate; the hardening spec is distinct follow-on work.

**This is the third recurrence of the prose-skip pattern** (seam-progress, partial-repair mid-stop × 2). The evidence is now sufficient to conclude that 6×N prose-orchestrated steps across a 15-test batch is structurally unreliable regardless of prose clarity. The minimal reliable shape is `compile-repair-batch` (deterministic, model-free) + per-test agent loop over the manifest (5 operations per test, vs. 9 today), with `audit-grounding` as the resume oracle.

---

## File evidence index

| Claim | Source | Tag |
|---|---|---|
| `ingest-update --from` is caller-supplied, no fixed default | `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs:32–33, 79–91` | CONFIRMED |
| Step 8 prescribes `.spectra/repaired.json` fixed name on both sides | `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:213–216` | CONFIRMED |
| Consumer `.spectra/` has 5 repaired files (4 per-ID, 1 fixed) | `.spectra/` listing with mtimes | CONFIRMED |
| `ingest-update` succeeded for TC-101 and TC-106 | `TC-101.md` and `TC-106.md` mtime vs repair file mtime | CONFIRMED |
| Zero re-critic runs (all verdict files original batch mtime) | `.spectra/verdicts/` 35 files, all mtime 16:44–16:57 | CONFIRMED |
| Zero grounding blocks in any partial `.md` | All 15 partial `.md` files inspected | CONFIRMED |
| Halt cause = session exhaustion (135 operations) | Artifact pattern; no run log available | INFERRED |
| Critic must be agent-driven (Task tool, `context: fork`) | `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:1–7, 30–43` | CONFIRMED |
| No `audit-grounding` command registered | `src/Spectra.CLI/Commands/Ai/AiCommand.cs:14–53` (17 commands listed) | CONFIRMED |
| `TestDetail` omits `File` field | `src/Spectra.CLI/Results/ShowResult.cs:1–41` | CONFIRMED |
| `testEntry.File` in scope in `ShowHandler.cs` | `src/Spectra.CLI/Commands/Show/ShowHandler.cs:77–84` | CONFIRMED |
| `spectra config --raw` prints raw JSON config | `src/Spectra.CLI/Commands/Config/ConfigHandler.cs:169–175` | CONFIRMED |
| Per-test repair naming not prescribed anywhere in source | grep `src/` for `repaired-TC` — zero matches | CONFIRMED |
| Critic name-deviation source: critic uses per-test verdict naming | `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:88–98` | CONFIRMED |
