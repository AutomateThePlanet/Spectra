# FINDINGS — Repair Seam Closure & Inspection Surface

**Date:** 2026-06-19
**Investigator:** Claude Code (read-only, no files modified)
**Framework repo:** `C:\SourceCode\Spectra` (branch: claude-code-v2, HEAD: 27cf352)
**Consumer artifacts:** `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST`

---

## Part 1 — Repair seam

### Q1.1 — Ingest half: present / missing / present-but-unused?

**ANSWER: Present and wired — the seam is complete in both CLI and skill prose. The agent did not execute the full loop.**

The following commands are registered on `spectra ai` (CONFIRMED `src/Spectra.CLI/Commands/Ai/AiCommand.cs:46–49`):

```
spectra ai compile-repair-prompt   -- compiles the repair prompt from the partial verdict (Spec 071)
spectra ai ingest-grounding        -- writes condensed grounding block into the .md frontmatter (Spec 071)
spectra ai record-drop             -- writes the drop trail entry for hallucinated tests (Spec 071)
spectra ai review-flagged          -- lists / dispositions tests flagged after repair (Spec 071)
```

There is no `ingest-repair` or `ingest-repaired-test` verb; that role is filled by the **pre-existing** `spectra ai ingest-update`, which was explicitly chosen over a new verb (CONFIRMED `specs/071-verdict-disposition/research.md:49` — Decision 4: "Reuse existing `spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json`").

**Full repair loop as defined (all verbs exist):**

```
compile-repair-prompt --suite S --test ID      -> stdout (plain text repair prompt)
  -> agent patches test in-session
  -> agent writes .spectra/repaired.json (Write tool)
spectra ai ingest-update S --test-id ID --from .spectra/repaired.json --output-format json
  -> re-invoke spectra-critic subagent (same Step 8 procedure)
  -> spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-{id}.json --output-format json
  -> re-verdict gate:
      grounded:     spectra ai ingest-grounding --suite S --test ID --from ... --repaired --repair-attempts 1
      partial:      spectra ai ingest-grounding --suite S --test ID --from ... --repair-attempts 1
      hallucinated: spectra ai record-drop ... then spectra delete ...
```

**What `ingest-update` does** (CONFIRMED `src/Spectra.CLI/Commands/Generate/IngestUpdateCommand.cs`):
- Accepts `suite` (positional) + `--test-id {id}` + `--from {file}` (or stdin)
- Validates the edited test JSON
- Enforces drift guard on protected fields (priority, component, tags)
- Preserves the original ID — edits content fields (steps, expected_result, preconditions, title)
- Writes the `.md` file and updates `_index.json`
- Supports `--output-format json` for machine-readable exit

**Conclusion:** The seam is structurally closed. All CLI commands for the repair loop exist and pass integration tests. The consumer artifacts show the repair was **attempted** (CONFIRMED `.spectra/` listing: `repaired-TC-105.json`, `repaired-TC-106.json`, `repaired-TC-110.json`, `repaired-TC-111.json`, `repaired.json`) but **not completed** — `ingest-update` and `ingest-grounding` were not called after the repair files were written. This is a **skill fidelity gap**, not a missing CLI verb.

---

### Q1.2 — What the skill says vs what the agent did

**Skill prose for the partial path** — exact text from CONFIRMED `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:210–225`:

> `gate pass (verdict partial) — bounded repair attempt:`
>
> `spectra ai compile-repair-prompt --suite {suite} --test {id}`
>
> `-> Read the repair prompt from stdout. Patch the test in-session (rewrite ONLY the ungrounded elements; preserve id and structure). Write patched test JSON array to .spectra/repaired.json.`
>
> `spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json --output-format json`
>
> `-> Re-invoke the spectra-critic subagent for {id} (same procedure — writes .spectra/verdicts/critic-verdict-{id}.json again):`
>
> `spectra ai ingest-verdict --from .spectra/verdicts/critic-verdict-{id}.json --output-format json`
>
> `-> Read re-verdict gate:`
> `  - re-verdict grounded: spectra ai ingest-grounding ... --repaired --repair-attempts 1 ...`
> `  - re-verdict partial:  spectra ai ingest-grounding ... --repair-attempts 1 ...`
> `  - re-verdict hallucinated: spectra ai record-drop ... then spectra delete ...`

**What the skill prescribes (6-step partial path):**
1. `compile-repair-prompt` -> stdout (plain text)
2. Patch in-session; Write to `.spectra/repaired.json` (Write tool — fixed path)
3. `ingest-update {suite} --test-id {id} --from .spectra/repaired.json`
4. Re-invoke `spectra-critic` subagent (Task tool)
5. `ingest-verdict --from .spectra/verdicts/critic-verdict-{id}.json`
6. Gate on re-verdict -> `ingest-grounding` (grounded or partial) or `record-drop + delete` (hallucinated)

The skill is unambiguous: it names a specific deterministic ingest verb (`ingest-update`) and a specific path (`.spectra/repaired.json`). It does NOT instruct directly editing `TC-NNN.md`. It does NOT leave disposition vague.

**What the agent actually did (CONFIRMED from consumer artifacts):**
- `compile-repair-prompt` was invoked for 5 of 15 partial tests (repair files exist)
- Agent produced patched test JSON (CONFIRMED: `repaired-TC-105.json` is a valid one-element JSON array)
- Repair files were named `repaired-TC-NNN.json` (4 tests) and `repaired.json` (1 test, TC-101)
- **`ingest-update` was NOT called** — zero `grounding:` blocks in any partial `.md` file (CONFIRMED `VERIFICATION-spec-071.md:52–58`)
- **`ingest-grounding` was NOT called** for any partial test (CONFIRMED: `spectra ai review-flagged --no-interaction --output-format json` returned `{"flagged_count":0}`)

**Naming discrepancy — agent invention (INFERRED):** The skill says write to `.spectra/repaired.json` (fixed). The agent wrote `repaired-TC-105.json`, `repaired-TC-106.json`, etc. (per-test-ID). This naming is NOT mentioned in the skill prose, the research doc, or any `.cs` file (CONFIRMED: grep of `src/` for "repaired-TC" returned zero matches in source code). The per-ID naming is **agent invention**. The JSON is structurally correct and `ingest-update --from` accepts any path — so the naming does not break the seam — but the real problem is that `ingest-update` was never called at all.

**What the spectra-critic agent says about repair:** Nothing. CONFIRMED `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:109`: "You do not write or modify test files — grounding write-back is handled by `spectra ai ingest-grounding` (Spec 071) after the verdict is classified. Your job ends after `ingest-verdict` confirms the gate." The critic agent does not know about or participate in the repair path.

---

### Q1.3 — `.spectra/repaired-TC-NNN.json` origin

**ANSWER: Agent invention. The naming is not prescribed anywhere in the framework.**

**Consumer artifact structure** (CONFIRMED — `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST\.spectra\repaired-TC-105.json` read):

```json
[
  {
    "id": "TC-105",
    "title": "Swap button swaps source and target units in a single action",
    "priority": "high",
    "tags": ["unit-converter", "unit-selection", "swap", "happy-path"],
    "component": "unit-converter",
    "preconditions": "...",
    "steps": [...],
    "expected_result": "...",
    "source_refs": ["docs/unit-converter.md#Unit-Selection"],
    "criteria": ["AC-STANDARD-CALCULATOR-013"]
  }
]
```

This is a **single-element JSON array in the generation schema** — the same shape `ingest-update` accepts. Structurally valid; would be accepted by `ingest-update --from .spectra/repaired-TC-105.json`.

**All framework references to `repaired-TC` or `repaired.json`** (CONFIRMED via grep of entire `src/`):
- `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:214` — prescribes `.spectra/repaired.json` (fixed)
- `src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md:35` — prescribes `.spectra/repaired.json` (fixed)
- `specs/071-verdict-disposition/research.md:49,108,109` — design docs, same `.spectra/repaired.json`
- `specs/071-verdict-disposition/quickstart.md:42,43` — same `.spectra/repaired.json`
- **Zero `.cs` source files** reference either name (CONFIRMED: grep returned no matches in `src/`)

**Conclusion:** The `repaired-TC-NNN.json` naming is **agent invention**. The framework prescribes a single fixed file `.spectra/repaired.json` overwritten per test. The per-ID naming is a secondary symptom; the primary problem is that `ingest-update` was never called.

---

### Q1.4 — Symmetric closure & skill-fix-vs-CLI-fix decision

**`IngestTestsCommand` vs `IngestUpdateCommand`** (CONFIRMED both files read in full):

| Dimension | `ingest-tests` | `ingest-update` |
|-----------|----------------|-----------------|
| ID allocation | New IDs allocated | Original ID kept (edit-not-create) |
| Dedup check | Against existing corpus | Against original test |
| Drift guard | None | Fires on protected field changes |
| Grounding | Cleared (new test) | Carried from original |
| Re-critic step | Not included | Not included |

`ingest-update` already covers the repair path correctly: validates patched test, preserves original ID (CONFIRMED `IngestUpdateCommand.cs:108–111`), writes `.md` and updates `_index.json`, drift guard protects priority/component/tags. At repair time grounding is null (original freshly created) — `ingest-grounding` adds the fresh grounding block afterward. The re-critic step is intentionally a separate subagent invocation per the seam separation principle.

**Is closing the repair seam a skill-fix or a CLI fix?**

**SKILL-FIX ONLY.** All required CLI verbs exist and are correct:
- `compile-repair-prompt` — CONFIRMED present
- `ingest-update` — CONFIRMED present, covers all repair persistence needs
- `ingest-grounding` — CONFIRMED present (with `--repaired`/`--repair-attempts` flags)
- `ingest-verdict` — CONFIRMED present
- `record-drop`, `spectra delete` — CONFIRMED present

No new CLI verb is needed. The gap is the agent stopped after writing the repair file instead of proceeding through all 6 steps.

---

## Part 2 — Inspection surface

### Q2.1 — Inspection improvisation catalogue

**Commands confirmed from** `src/Spectra.CLI/Commands/Ai/AiCommand.cs` and `src/Spectra.CLI/Commands/` directory listing.

**1. `cat _index.json | python -m json.tool | head` — suite index contents**

- `spectra validate`: validates schema, does not print index. CONFIRMED `ValidateCommand.cs`.
- `spectra list --suite {s}`: prints test metadata (id, title, priority). CONFIRMED `ListCommand.cs`. Does not dump raw `_index.json`.
- `spectra run list-suites`: lists execution suites. CONFIRMED `RunCommand.cs`. Not the same thing.
- **Gap: CONFIRMED** — no `spectra index show` or equivalent. Raw `_index.json` inspection requires direct file read.

**2. `cat _index.json | python -c "...[id=='TC-112']..."` — test ID to file path**

- `spectra show TC-112 --output-format json`: CONFIRMED exists (`ShowCommand.cs`, `ShowHandler.cs`). Finds test by ID cross-suite. BUT: `TestDetail` model (`ShowHandler.cs:149–163`) does NOT include the `file` field. Fields present: Id, Title, Priority, Suite, Component, Tags, SourceRefs, Steps, ExpectedResults. File path omitted.
- **Gap: CONFIRMED (minor)** — `spectra show {id}` nearly closes this; adding `file` to `TestDetail` is a one-line fix.

**3. `ls test-cases/<suite>/ || ls test-cases/` — suite/file discovery**

- `spectra suite list`: lists suite names. CONFIRMED `SuiteCommand.cs`.
- `spectra list --suite {s}`: lists test IDs and titles. CONFIRMED `ListCommand.cs`.
- **Gap: PARTIAL** — logical state (suites, tests) is covered. Raw `.md` filename listing is not. For agent use, `spectra list` is the correct substitute.

**4. `cat .spectra/config.json || cat spectra.config.json || ls .spectra/` — config**

- `spectra config --raw`: prints raw JSON config. CONFIRMED `ConfigHandler.cs:169–175`.
- `spectra config` (no key): prints formatted config. CONFIRMED `ConfigHandler.cs:186–215`.
- `spectra config {key}`: reads one key. CONFIRMED `ConfigHandler.cs:130–167`.
- **Gap: NONE** — `spectra config --raw` fully covers this. Agent improvisation is unnecessary.

**5. `compile-critic-prompt ...; echo "EXIT: $?"` — exit code capture**

- This is **CLI distrust**, not a state-inspection gap. Noted separately. The agent used `echo "EXIT: $?"` to verify the exit code explicitly rather than relying on the Bash tool's non-zero exit surfacing. Not an inspection surface issue.

---

### Q2.2 — Persisted-output pattern: implementation & generalizability

**There is NO "persisted-output" mechanism in `CompileCriticPromptCommand.cs`.**

CONFIRMED `src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs:240–258`: the command writes ONLY to stdout via `Console.Out.Write`. No automatic file-save, no size threshold, no "saved to: ...tool-results...txt" mechanism, no shared helper. CONFIRMED by grep of `src/` for `persisted-output`, `tool-results`, `ToolResults`, `SavedTo`, `saved to` — zero matches.

**The "saved to: .../tool-results...txt" behavior is a Claude Code harness feature**, not a Spectra CLI feature. The harness automatically saves large stdout payloads from Bash tool calls to a temp file and presents the path in the tool result, enabling the Read tool to be used instead of parsing inline stdout. This is unconditionally available for any Spectra command that emits large stdout — no code changes needed. INFERRED from Claude Code harness behavior (not in Spectra source).

**Generalizability:** Automatic and already generalized at the harness level. Nothing to implement in Spectra.

---

### Q2.3 — `show` family vs persisted-output: footprint & human-reviewer angle

**Option B (persisted-output generalized):** Already provided unconditionally by the harness. Not a design choice for Spectra. No work needed.

**Option A (new read verbs):**

The VERIFICATION report (CONFIRMED `VERIFICATION-spec-071.md:235–250`) identifies these specific gaps that caused raw shell inspection:

1. No `spectra ai list-verdicts --suite {s}` — agent must `ls .spectra/verdicts/` and parse 35 individual JSON files
2. No `spectra ai audit-grounding --suite {s}` — agent must grep all `.md` frontmatter to find missing grounding blocks

These are the root cause of raw shell in Phase 1.2 and 2.2 checks. Crucially: `spectra ai review-flagged` is designed to surface flagged partials — but returns `{"flagged_count":0}` when `ingest-grounding` was never called. It provides zero inspection value when the seam breaks before grounding write-back, which is exactly the failure mode observed.

**Human-reviewer angle:** `review-flagged` renders `CondensedFindings` as readable bullets (CONFIRMED `ReviewFlaggedHandler.cs:78`) — good design for interactive review. But it depends on `ingest-grounding` having run first. An `audit-grounding` command serves both:
- **Agent:** non-stop corpus state inspection (which tests still need `ingest-grounding`?)
- **Human reviewer:** full corpus grounding state without opening individual `.md` files
- **Bootstrap:** the agent can run `audit-grounding` to discover which tests to fix, rather than relying on `review-flagged` which only shows tests where grounding is already written

**Conclusion:** Option A (new inspection verbs) is the targeted fix. Minimum additions:
1. `spectra ai audit-grounding --suite {s}` — lists each test with grounding state (has block / missing / partial-unflagged). Primary gap.
2. Add `file` to `spectra show {id} --output-format json` — one-field fix in `ShowHandler.cs:149–163`.
3. `spectra ai list-verdicts --suite {s}` — optional; lower priority if `audit-grounding` is present.

---

## Part 3 — Unifying claim

### Non-stop contract violations

**Contract definition:** every agent step must be either `Write {.spectra/path}` (Write tool to `.spectra/`) or `Bash(spectra *)`.

**Repair loop violations (observed in consumer run):**

| Step | What happened | Contract status | Closure |
|---|---|---|---|
| compile-repair-prompt | `Bash(spectra ai compile-repair-prompt ...)` | COMPLIANT | — |
| Read repair prompt | Read harness-saved temp file | COMPLIANT | — |
| Patch in-session | In-session generation | COMPLIANT | — |
| Write repair file | `Write(.spectra/repaired-TC-105.json)` (wrong path name vs skill spec) | DEVIATION | Skill clarification: prescribes `.spectra/repaired.json` |
| **ingest-update** | **NOT CALLED** | **CONTRACT VIOLATION** | Skill fidelity: agent must call `ingest-update` |
| **Re-critic subagent** | **NOT CALLED** | **CONTRACT VIOLATION** | Skill fidelity: agent must invoke spectra-critic via Task |
| **ingest-verdict** | **NOT CALLED** | **CONTRACT VIOLATION** | Skill fidelity: agent must call `ingest-verdict` |
| **ingest-grounding** | **NOT CALLED** | **CONTRACT VIOLATION** | Skill fidelity: agent must call `ingest-grounding` |

**Inspection violations (VERIFICATION cycle, CONFIRMED `VERIFICATION-spec-071.md:235–250`):**

| Inspection step | Method used | Contract status | Closure |
|---|---|---|---|
| `ls .spectra/verdicts/` | Raw shell | VIOLATION | New `spectra ai list-verdicts --suite {s}` |
| Grep `.md` frontmatter for grounding | Raw shell / file reads | VIOLATION | New `spectra ai audit-grounding --suite {s}` |
| `cat _index.json | python -c` ID->path | Raw shell + python | VIOLATION | Add `file` field to `spectra show {id} --output-format json` |
| `cat spectra.config.json` | Raw file read | VIOLATION (avoidable) | `spectra config --raw` already exists; agent unaware |

---

### Sizing

**Is closing the repair seam a skill-fix or a CLI spec?**
**Skill-fix only.** All CLI verbs for the 6-step partial path exist and are correct. No new verb needed. The fix is ensuring the agent follows the full loop per the skill's Step 8.

**Is the inspection surface a separate brief spec or foldable into repair closure?**
**Separate brief spec** (or a polish spec). The inspection gaps are independent of the repair seam gap — they exist because no `audit-grounding`/`list-verdicts` verbs exist, not because the repair loop is incomplete. The `audit-grounding` gap also affects the human-review cycle and the verification workflow, not just generation repair.

**Minimum set for a 100% non-stop generate+repair cycle:**

1. **Skill fidelity (no code change):** Agent follows `spectra-generate.md` Step 8 partial path all 6 steps. All CLI plumbing exists.
2. **`spectra ai audit-grounding --suite {s}` (new command):** Enables non-stop corpus grounding state inspection. Without this, neither agent nor human can see which tests are missing grounding blocks without raw shell. This is the only new CLI work strictly required.
3. **Add `file` to `ShowResult.TestDetail` (one-line fix, `ShowHandler.cs:149–163`):** Closes the ID-to-path improvisation. `spectra show {id} --output-format json` would then return the file path, eliminating python-json hackery.
4. **`spectra config --raw` already covers config reading** — no new work; agent education needed.

Items 2–4 are inspection surface fixes. The repair seam itself is a skill-fidelity fix (item 1).

---

## File evidence index

| Claim | Source | Tag |
|---|---|---|
| AiCommand.cs registers all Spec 071 verbs | `src/Spectra.CLI/Commands/Ai/AiCommand.cs:46–49` | CONFIRMED |
| compile-repair-prompt emits plain text to stdout only | `src/Spectra.CLI/Commands/Generate/CompileRepairPromptCommand.cs:141–142` | CONFIRMED |
| ingest-update is the prescribed repair persistence verb | `specs/071-verdict-disposition/research.md:49` (Decision 4) | CONFIRMED |
| Skill prescribes `.spectra/repaired.json` fixed name | `src/Spectra.CLI/Skills/Content/Skills/spectra-generate.md:214` | CONFIRMED |
| spectra-review-flagged also prescribes `.spectra/repaired.json` | `src/Spectra.CLI/Skills/Content/Skills/spectra-review-flagged.md:35` | CONFIRMED |
| Consumer has repaired-TC-NNN.json files | `C:\SourceCode\AutomateThePlanet_SystemTests_NEW_TEST\.spectra\` listing | CONFIRMED |
| Zero .md files have grounding block for partial tests | `VERIFICATION-spec-071.md:52–58` | CONFIRMED |
| review-flagged returned flagged_count:0 | `VERIFICATION-spec-071.md:197` | CONFIRMED |
| repaired-TC naming absent from all .cs source files | grep src/ returned zero matches | CONFIRMED |
| spectra show {id} JSON model omits file field | `src/Spectra.CLI/Commands/Show/ShowHandler.cs:149–163` | CONFIRMED |
| spectra config --raw prints resolved config | `src/Spectra.CLI/Commands/Config/ConfigHandler.cs:169–175` | CONFIRMED |
| No persisted-output mechanism in CompileCriticPromptCommand | `src/Spectra.CLI/Commands/Generate/CompileCriticPromptCommand.cs:240–258` | CONFIRMED |
| Harness saves large stdout to temp file (not CLI code) | Observed behavior; not in Spectra source | INFERRED |
| audit-grounding / list-verdicts verbs do not exist | AiCommand.cs + src/Spectra.CLI/Commands/ listing | CONFIRMED |
| review-flagged only surfaces tests with flagged_for_review: true | `src/Spectra.CLI/Commands/Review/ReviewFlaggedHandler.cs:72` | CONFIRMED |
| critic agent says nothing about repair path | `src/Spectra.CLI/Skills/Content/Agents/spectra-critic.agent.md:109` | CONFIRMED |
