# Research: Verdict Disposition Policy (Spec 071)

**Date**: 2026-06-19
**Status**: Complete — all unknowns resolved, no external research required. Decisions derived from `FINDINGS-verdict-disposition.md` (code-confirmed) + review of existing seam patterns in `CompileCriticPromptCommand`, `IngestUpdateCommand`, and `TestFileWriter`.

---

## Decision 1 — `dropped-tests.json` gitignore vs. committed audit

**Decision**: Gitignored scratch (consistent with all other `.spectra/` JSON scratch files).

**Rationale**: `.spectra/` is the per-workspace scratch directory; other residents (`generated.json`, `analysis.json`, `verdicts/`) are already gitignored or should be. An audit trail that is workspace-local and not committed is still durable within a session (survives process restarts) and avoids polluting commit history with run-specific noise. Teams that want a permanent audit trail can commit it manually via `git add -f`.

**Alternatives considered**:
- Committed as audit artifact: creates noise in history and causes merge conflicts when two developers run generation in the same branch. Rejected.

---

## Decision 2 — Per-test verdict file naming

**Decision**: `.spectra/verdicts/critic-verdict-{id}.json` (per-test named files). The current single fixed filename `critic-verdict.json` is overwritten each test in a batch; naming per test ID makes all verdicts available after a run.

**Rationale**: Repair (Phase 2) reads the verdict JSON to compile the repair prompt. If the file were overwritten by the next test's critic, the findings for an earlier partial would be gone by the time repair runs (or before review). Per-test naming requires only a one-line change in the critic agent's instruction; no C# change.

**Alternatives considered**:
- Rotate on run-start (copy to `critic-verdict-{runId}-{id}.json`): requires knowing the run ID, adds complexity. Rejected.
- Append to a single NDJSON log: not readable without parsing; harder for repair to address a specific test's findings. Rejected.

---

## Decision 3 — Grounding write-back: new `ingest-grounding` command vs. extending `ingest-verdict`

**Decision**: New `spectra ai ingest-grounding --suite {s} --test {id} [--from {file}]` command.

**Rationale**: `ingest-verdict` is designed as a "pure classifier — persists nothing" (its class doc, confirmed in investigation). Adding persistence to it would break that contract and make it harder to test. A separate `ingest-grounding` command:
- Mirrors the existing seam pattern: compile (deterministic) → agent turn (model) → ingest (persist)
- Is independently testable
- Leaves `ingest-verdict` semantically unchanged (still advisory gate only)
- Mirrors `ingest-update` and `ingest-tests` in its "fail-loud boundary" design

**Alternatives considered**:
- `--write-back --suite {s} --test {id}` flags on `ingest-verdict`: conflates gate classification with persistence. Rejected.
- Skill writes grounding block directly with Write tool: requires agent to know the exact frontmatter format; not deterministic (agent could mangle YAML). Rejected.

---

## Decision 4 — Repair persistence: reuse `ingest-update` vs. new `ingest-repaired-test`

**Decision**: Reuse existing `spectra ai ingest-update {suite} --test-id {id} --from .spectra/repaired.json`.

**Rationale**: `ingest-update` already implements update-in-place with the same ID, with drift guard on protected fields (priority, component, tags). Repair changes content fields (steps, expected_result, preconditions, title) which are NOT protected. The drift guard will not fire. `ApplyEdit` preserves `Grounding = original.Grounding` — at repair time the original test was freshly ingested with no grounding (null), so the repaired test also has null grounding. Then `ingest-grounding` writes the fresh grounding from the re-critic verdict. This produces the correct sequence without any new persistence command.

**Edge case**: the `ingest-update` drift guard compares against the on-disk original. After `ingest-tests` persists a new test with null grounding, `ingest-update` sees that original as having null grounding — which it carries forward cleanly. After `ingest-grounding` writes the verdict, the grounding is final.

**Alternatives considered**:
- New `ingest-repaired-test` command: duplicates 95% of `ingest-update`'s logic. Rejected (YAGNI / Principle V).

---

## Decision 5 — Review surface: C# command vs. skill-only

**Decision**: C# command `spectra ai review-flagged [--suite {s}]` handles list + accept + delete. Retry-repair is delegated to the `spectra-review-flagged` skill (agent turn needed for repair inference).

**Rationale**: Accept and delete are deterministic (clear a flag; trail + delete). They require no model inference and belong in C# for testability and CI-friendliness. Retry-repair requires compiling a repair prompt + agent turn + re-critic — exactly the pattern that lives in a skill. Splitting this way keeps C# responsible for deterministic state and skills responsible for agent-driven inference.

**Accept action detail**: `accept` clears `flagged_for_review: true` in the grounding block while keeping `status: partial` and all condensed findings. It does NOT upgrade the verdict — the test is acknowledged as partial, not re-verified. This is correct: accept means "I've reviewed it and I'm OK with it as-is."

**Alternatives considered**:
- Fully skill-driven review (no C# command): harder to test, not CI-friendly, can't be invoked non-interactively. Rejected.
- Fully C# command including repair: C# can't do model inference; would need to call out to the skill, creating awkward nesting. Rejected.

---

## Decision 6 — `GroundingMetadata` new fields

**Decision**: Add `FlaggedForReview` (bool, default false), `RepairAttempts` (int, default 0), `Repaired` (bool, default false), and `CondensedFindings` (list of `{Element, Reason}` records) to `GroundingMetadata` and `GroundingFrontmatter`.

**Rationale**: These fields must survive roundtrip (written to `.md` frontmatter → parsed back by TestCaseParser → available for `ingest-grounding` and `review-flagged`). Placing them directly in `GroundingMetadata` is the simplest path since `TestFileWriter` already writes that block conditionally.

**`CondensedFindings` content**: populated from the verdict JSON `findings` array, filtering to entries with `status: unverified` or `status: hallucinated`. Each entry records `element` (e.g., "Step 3") and `reason` (one-line reason from the finding). Does NOT include `evidence` or `claim` text (those stay in the full JSON for brevity in the frontmatter).

**Alternatives considered**:
- Separate `ReviewMetadata` object alongside `GroundingMetadata`: duplicates model complexity for a simple flag. Rejected.
- Store flags in test `Status` field: `Status` is a string field designed for orphaned state; overloading it with grounding flags would conflict. Rejected.

---

## Decision 7 — Repair prompt content and format

**Decision**: `compile-repair-prompt` emits PLAIN TEXT to stdout (no JSON envelope). Content: the repair prompt includes the original test artifact (title + preconditions + steps + expected result), the critic's condensed findings (element + claim + reason for each non-grounded finding, from the per-test verdict JSON), and the relevant source doc sections (from the test's `source_refs`, mirroring `compile-critic-prompt`'s `LoadDocumentsFromRefsAsync`). The instruction to the agent is: "Here is a test that the critic flagged as partial. Here are the specific claims it could not verify. Rewrite ONLY those elements to make them traceable to the documentation below. Return a JSON array containing the ONE corrected test."

**Format**: plain text (like `compile-critic-prompt` in non-json mode) — no JSON envelope the agent must parse (this was identified as a friction source in prior seam work, referenced as "D2 friction lesson" in spec).

---

## Decision 8 — Phase boundary: `ingest-grounding` in the generate skill flow

**Decision**: `ingest-grounding` is called by the skill (spectra-generate.md) AFTER the critic verdict is known (from `ingest-verdict`), in the same Step 8 iteration for grounded and for the re-critic result of partial (post-repair). The skill drives this; no C# command chain needed.

**Updated Step 8 flow (per test ID):**
```
1. Update progress (loop state)
2. Invoke spectra-critic subagent (compile-critic-prompt → render verdict → write .spectra/verdicts/critic-verdict-{id}.json → ingest-verdict)
3. Read gate: {verdict, score, drop} from ingest-verdict stdout
4a. If grounded → ingest-grounding --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json → add to kept-grounded count
4b. If partial →
    a. compile-repair-prompt --suite {s} --test {id} → repair prompt (uses .spectra/verdicts/critic-verdict-{id}.json internally)
    b. Read prompt; patch test in-session; write .spectra/repaired.json
    c. ingest-update {suite} --test-id {id} --from .spectra/repaired.json
    d. Re-invoke spectra-critic subagent for {id}
    e. Read gate from re-critic ingest-verdict
    f. ingest-grounding --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json [--repaired --repair-attempts 1]
    g. If re-verdict = hallucinated → record-drop → delete
    h. Add to repaired-to-grounded OR flagged-partial count
4c. If hallucinated →
    a. record-drop --suite {s} --test {id} --from .spectra/verdicts/critic-verdict-{id}.json
    b. spectra delete {id} --force --no-interaction --output-format json --verbosity quiet
    c. Add to dropped count
```
