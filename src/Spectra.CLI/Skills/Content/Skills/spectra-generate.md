---
name: spectra-generate
description: Generate test cases from documentation — analyze behaviors, get approval, generate, and verify with the mandatory spectra-critic subagent. No `spectra ai generate` command; uses the compile→in-session→ingest seam.
tools: [{{GENERATE_TOOLS}}]
---

# SPECTRA Test Generation

The CLI never calls a model: it **compiles a grounded prompt**, a model generates, and the CLI **ingests** and validates the result.

This session **orchestrates**: `spectra-analyst` analyses and saves a plan; `spectra-generator` generates a batch, has `spectra-critic` verify each test and returns one verdict line per test; `spectra-repairer` repairs partial tests the same way. You ask the user, route by the verdict lines, write grounding through the CLI and report.

There is **no** `spectra ai generate` command anymore. Do not look for one.

**ALWAYS follow the full analyze → approve → generate flow. Never skip the analysis step.**

## Additional resources

[reference.md](reference.md) (this skill's folder) holds what you rarely need; read only the part you need, never during Steps 7–9: **CLI flags reference** (and the pre-flight refusal), **Step 4 presentation** (at Step 4), **Resume an interrupted run**, **From-description detail**, **Test Creation Intent Routing**, **Other tasks (delegation)**, **Notes**.

## IMPORTANT RULES

- **HELP** ("help", "what can I do"): follow the **`spectra-help`** SKILL. **QUICKSTART** ("how do I get started", "tutorial", "I'm new"): follow the **`spectra-quickstart`** SKILL.
- **Do NOT probe at startup** (no Glob of `.claude/skills/**`, no `spectra --help`).

## MANDATORY: Analyze first, every time

If the user asks you to **generate, create, add, write, or build test cases for an area, feature, module, suite, page, or topic**, you **MUST** start with the analysis step, present the recommendation, and **STOP and wait for the user to approve** before generating anything.
- Do NOT generate tests before running the analysis step and getting approval.
- Do NOT invent a `--count` value. There is no default — the analysis step recommends a number.
- Do NOT ask the user how many test cases they want — the analyze step will recommend a number.

The ONLY time you skip analysis is when the user describes a single concrete scenario (see "When the user wants to create a specific test case").

## Context budget contract

This session **never reads** docs, compiled prompts, test files, generated / staged JSON or verdict files. Its calls: `spectra` commands with small JSON output, writes to `.spectra/progress.json`, Agent calls to the subagents (`spectra-critic` only on the Step 8 fallback and in the from-description flow), and [reference.md](reference.md) outside Steps 7–9. Report every line a subagent returns, as returned. No shell improvisation (`ls`, `find`, `cat`): use `spectra ai audit-grounding` and `spectra config --raw`.

## Main generation flow (user names an area)

**Focus**: the user's full intent (type + topic), or none. **Count**: a number the user stated, else `recommended`. NEVER fall back to "5".

**Step 0** — run `spectra ai init-seam-progress`, then write `.spectra/progress.json`:
```json
{"phases":["Step 1 — Compile analysis","Step 2 — Identify behaviors","Step 3 — Ingest analysis","Step 4 — Review (waiting for approval)","Step 5 — Compile generation","Step 6 — Generate tests","Step 7 — Ingest","Step 8 — Critic","Step 9 — Report"],"active":0}
```

### Steps 1–3 — Analysis (the `spectra-analyst` subagent)

Invoke the `spectra-analyst` subagent with the Agent tool, passing `suite: {suite}`, `doc_suite: {docSuite}` and, when there is one, `focus: {focus}`. It saves the generation plan and returns:
- the recommendation lines (`recommended:`, `breakdown:`, `techniques:`, `boundary_gap:` …), `batch <id> | count <k> | …`, `covered:` (never generated — Spec 095) and `unresolved:` lines;
- or `refused: <code>` / `refusal: …` lines (the pre-flight budget refusal): show them verbatim and offer a recovery path from [reference.md](reference.md) — re-running means invoking `spectra-analyst` again. Do NOT proceed;
- a `stop: …` line → report it and stop.

Update `.spectra/progress.json`: `{"active":3}`

### Step 4 — Present the recommendation and STOP

Show the recommendation as laid out in reference.md (**Step 4 presentation**), with **Already covered — not generated:** one bullet per `covered:` line.

Shall I proceed?

**STOP. Wait for user approval.** If `recommended` is 0, tell the user every identified behavior is already covered and do not generate.

---

## After user approves

Update `.spectra/progress.json`: `{"active":4}`

### Steps 5–7 — Generation, one batch at a time (the `spectra-generator` subagent)

Use the batch lines in plan order. An explicit user total overrides `recommended`: split it over the batches in proportion to their counts (largest remainder), passed as `count: <k>`. **Never invoke the generator for a batch whose count is 0.** **`recommended` is advisory guidance, not a quota.**

For each batch, one at a time — never in parallel (the id allocator and the index have a single writer) — invoke the `spectra-generator` subagent with the Agent tool, passing `suite: {suite}`, `batch: <id>` and, only for an explicit total, `count: <k>`. It returns:
- one `<id> | created: <title>` line per created test, a `baseline: …` line and `warning: …` lines (report each distinct template warning once per run);
- one verdict line per created test (`<id> | verdict <verdict> <score>`, `<id> | critic-disagrees …`, `<id> | unverified: <reason>`), or `critic: not run`;
- or `failed <error_code>: <first error>` — report it and run the next batch; never generate a batch in this session (`PLAN_STALE` and `PROMPT_TOO_LARGE` are handled in Step 9);
- a `stop: …` line → report it and stop.

Report every line, as returned. Update `.spectra/progress.json`: `{"active":6}`

---

## NON-STOP CONTRACT — Steps 7–9

**Every step in this section must be a single `Bash(spectra *)` call or a `Write` to a spectra-authored path.** If no single `spectra` call covers a step, **STOP and report a missing affordance** — never work around it.

**Prohibited behaviors (each is a missing-affordance signal, not a license to improvise):**
- **(a) Shell loops over per-test verbs** — `for id in …; do spectra …; done` — NEVER. Use the corresponding `--all` batch verb.
- **(b) Piping spectra output to any interpreter** — `python -c "…"`, `jq`, `node -e`, or any data-transform shell — NEVER. Read the output file with the Read tool and iterate in-context.
- **(c) Manual `.md` file editing** — rewriting test content directly instead of using `ingest-update` — NEVER.
- **(d) Verdict / grounding field rewrites by hand** — writing `grounding:`, `verdict:`, or `score:` frontmatter directly instead of using `ingest-grounding` / `record-drop` — NEVER.

**Never accept a "scripting for all projects" allowlist option** — that is the inverse of this contract. The per-seam guards below (e.g., the grounding `written:0` check) are specific instances of this general rule.

---

## MANDATORY: verify every generated test with the critic subagent

Verification is **mandatory and explicit — never skipped**: the generator and the repairer start `spectra-critic` per test they wrote (a fresh context each, on `ai.critic.model`); this session does only on the fallback and in the from-description flow. It is the single critic of record; a test is not accepted until its critic step has passed.

### Step 8 — Critic routing + repair

Update `.spectra/progress.json`: `{"active":7}`

**8a — Route each created test by its verdict line** (the verdict the CLI classified, copied by the generator):
1. **`verdict grounded` (gate `pass`):** add the id to the kept-grounded list (`kept-grounded` = its length). Only these tests get a grounding block. A test reported **unverified** (rows 4 and 5) is never in the list.
2. **`verdict partial`:** note the id for the repair in 8b.
3. **`verdict hallucinated` (gate `drop`)** — only when the critic's line agrees or there is no line:
   ```
   spectra ai record-drop --suite {suite} --test {id} \
     --from .spectra/verdicts/critic-verdict-{id}.json --output-format json
   spectra delete {id} --force --no-interaction --output-format json --verbosity quiet
   ```
   → Trail written before delete. Add to dropped count.
4. **`unverified: <reason>`:** kept as **unverified** — no grounding block, never regenerated or deleted; listed in Step 9 under "Unverified (critic could not judge)".
5. **`critic-disagrees`** → kept as **unverified** and reported with both verdicts — never dropped.

**Fallback.** For each created id of a batch that returned `critic: not run`, or with no verdict line: invoke the `spectra-critic` subagent with the Agent tool — suite + test id only (it runs `compile-critic-prompt`), **never a hand-built file path**; compare its final line, never read the verdict file. Then `spectra ai ingest-verdict --suite {suite} --all --ids <those ids> --output-format json` and route by `results` with rows 1–3; no entry → the critic once more and the ingest again, then row 4; the critic's line names another verdict → row 5.

**After routing — batch grounding-ingest for the kept-grounded tests (one call, no loop; Spec 097):**
```
spectra ai ingest-grounding --suite {suite} --all --ids {kept-grounded ids} --output-format json
```
Skip this call when no test was kept as grounded (and skip the check below). `skipped_hallucinated` is not a failure: report the ids and continue.

**After batch grounding-ingest — check the result before proceeding to 8b (MANDATORY):**

Parse the JSON result of the batch call. Check `written` against the `kept-grounded` count tracked during the 8a loop:

- **If `written == 0` AND `kept-grounded > 0`:** STOP immediately. Do NOT proceed to 8b. Emit a diagnostic:
  > GROUNDING-INGEST ANOMALY: `ingest-grounding --all` returned `written: 0` but {kept-grounded} tests were marked grounded in the critic loop. This means the grounding blocks were not written. Likely causes: `_index.json` file paths are suite-prefixed (path-doubling bug) or the suite directory could not be resolved. **STOP — do not proceed to 8b, do not edit `.md` files by hand, do not rewrite verdict fields by hand.** Run `spectra ai audit-grounding --suite {suite}` to diagnose the current grounding state, then report the anomaly to the user and halt.

- **If `written != kept-grounded`** (partial write — `written > 0` but less than expected): Surface a warning before continuing:
  > GROUNDING-INGEST WARNING: expected {kept-grounded} grounding blocks written, got {written}. Continuing to 8b — run `spectra ai audit-grounding --suite {suite}` to inspect which tests are missing their grounding block.

- **If `written == kept-grounded`** (or `kept-grounded == 0`): Proceed to 8b normally.

**NON-STOP CONTRACT (restated):** A broken or zero-result CLI verb is a STOP signal — it is NEVER a license to do the work by hand. Manual editing of `.md` files, rewriting of `grounding:` frontmatter, and synthesizing verdict fields are PROHIBITED regardless of how blocked you are. If a CLI verb returns an anomaly, stop and report. Do not improvise. Exit `8` from `ingest-update --all` is neither broken nor zero-result: the batch completed and `results` lists the failed files — retry them as 8b describes.

**8b — Repair (for partials).** If 8a noted partial ids, invoke the `spectra-repairer` subagent with the Agent tool, passing `suite: {suite}` and `ids: <partial ids>`. It patches only what the critic flagged, ingests with `ingest-update {suite} --all --keep-baseline`, retries a failed repair ingest at most twice (the same repair attempt), has `spectra-critic` verify each `written` repair (an `unchanged` one keeps its verdict), and returns `<id> | written`, `<id> | unchanged` or `<id> | failed <error_code>: <first error>`, then a verdict line per `written` / `unchanged` test (or `critic: not run`). Report every line.
- `failed` → report it; the test stays **unverified** with its partial verdict.
- Every `written` line is re-verified, leftovers from an earlier run included; one without a verdict line, or `critic: not run`, goes through the fallback — the critic for `written` ids only, `unchanged` ids straight to its ingest call.

**Re-critic phase** — route by the repairer's verdict lines: `verdict grounded` → repaired-to-grounded count; `verdict partial` → flagged-partial count (**continue — do NOT stop the batch**); `verdict hallucinated` (the critic's line agreed or there was none) → `record-drop` then `spectra delete` as in row 3, dropped count; `unverified:` / `critic-disagrees` → unverified.

**After the re-critic — batch grounding-ingest for the re-verified tests (one call, no loop):**
```
spectra ai ingest-grounding --suite {suite} --all --repaired --repair-attempts 1 --ids {ids} --output-format json
```
`{ids}`: the tests whose repair reported `written` or `unchanged` in `results`, minus any dropped — never a failed repair. If the list is empty, skip this call.

An interrupted run is resumed as reference.md describes (**Resume an interrupted run**).

Update `.spectra/progress.json`: `{"active":8}`

### Step 9 — Report

Report every created test by id and title with its outcome:
- Kept grounded; Repaired to grounded; Flagged partial (incl. failed repairs); Dropped hallucinated — counts, ids, titles;
- Failed batches with their code; batches "not generated (count 0 after splitting your total)";
- **Already covered — not generated:** each `covered:` line;
- **Unverified (critic could not judge):** id, title, reason, and the next step — run this skill's critic fallback (Step 8) for these ids, or `spectra ai audit-grounding --suite {suite}` (shows `none` / `critic`).

Also every `baseline:` and `warning:` line. If {flagged} > 0: "Run `spectra ai review-flagged --suite {suite}` to accept, retry repair, or delete flagged tests." If total-kept < requested, say "Run again to generate more." Never present a test as accepted unless its critic step passed.

A warning that "the generation plan predates per-behaviour coverage", or batches that failed with `PLAN_STALE` / `PROMPT_TOO_LARGE`: name them and offer once to re-run the analysis. If the user accepts, invoke `spectra-analyst` again and go **back to Step 4**, naming the batches already run with their created test ids, and STOP for approval. Nothing is regenerated automatically.

Write `.spectra/progress.json`: `{"active":9}` (terminal — seam-progress page renders Complete).

---

## When the user wants to create a specific test case

**Exactly 1 test case. Do NOT run analysis. Do NOT ask how many test cases to generate.**
1. `spectra ai compile-prompt --suite {suite} --from-description "{description}" [--context "{context}"] --output-format json`
2. Generate the one test IN-SESSION and write the one-element array to `.spectra/generated.json`.
3. `spectra ai ingest-tests {suite} --from .spectra/generated.json --output-format json` (exit 5/6: the same bounded retry).
4. Run the **Step 8** critic fallback on the persisted test — this session starts `spectra-critic` for it — then route it with 8a and write its grounding.
5. Show the id, title, suite, linked criteria, grounding verdict and notes.

---

## How to choose between generation flows

| User intent | Flow |
|-------------|------|
| Explore a feature area ("Generate test cases for…", "Cover… error handling") | Main generation flow, with `--focus` |
| Create a specific test case ("Add a test case for…", "Create a test case where…") | From-description flow |

**Key rule**: a single test case title → `--from-description`; a topic to explore → the main flow. Never ask about count or scope to decide.
